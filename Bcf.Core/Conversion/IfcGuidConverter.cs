using System;
using System.Globalization;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Element identifiers for BCF.
    ///
    /// In BCF a component is addressed by a 22-character IFC GUID. Models that
    /// came from IFC carry it in their properties; a model exported from Revit
    /// does not — it has a UniqueId of its own, which has to be converted by
    /// the very algorithm the Revit IFC exporter uses. Without a correct
    /// identifier a topic still opens in a receiving tool, but the element is
    /// never highlighted, which makes the topic useless.
    ///
    /// Идентификаторы элементов для BCF.
    ///
    /// В BCF компонент адресуется 22-символьным IFC GUID. У моделей, пришедших
    /// из IFC, он есть в свойствах; у выгруженной из Revit — нет: там свой
    /// UniqueId, и пересчитать его нужно тем самым алгоритмом, которым
    /// пользуется экспортёр IFC у Revit. Без верного идентификатора замечание
    /// в приёмнике откроется, но элемент не подсветится — то есть окажется
    /// бесполезным.
    /// </summary>
    public static class IfcGuidConverter
    {
        /// <summary>
        /// The base64 alphabet of the buildingSMART specification. It differs
        /// from ordinary base64 in its order and in the last two characters —
        /// System.Convert.ToBase64String cannot stand in for it.
        ///
        /// Алфавит base64 из спецификации buildingSMART. Отличается от обычного
        /// base64 порядком и двумя последними символами: подставить
        /// System.Convert.ToBase64String нельзя.
        /// </summary>
        public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        /// <summary>
        /// The length of a compressed IFC GUID.
        /// Длина сжатого IFC GUID.
        /// </summary>
        public const int IfcGuidLength = 22;

        /// <summary>
        /// A 128-bit identifier compressed into 22 characters. The first
        /// character carries only two bits: 22 × 6 = 132 bits of which 128 are
        /// meaningful, so the leading group takes two characters and the other
        /// five take four each.
        ///
        /// 128-битный идентификатор, сжатый в 22 символа. Первый символ несёт
        /// только два бита: 22 × 6 = 132 бита, из них значащих 128, поэтому
        /// старшая группа занимает два символа, а остальные пять — по четыре.
        /// </summary>
        /// <param name="guid">The identifier to compress.</param>
        public static string ToIfcGuid(Guid guid)
        {
            byte[] b = guid.ToByteArray();

            // The bytes are taken apart by hand rather than through
            // BitConverter: the Guid layout is fixed (Data1 and Data2/3 are
            // stored little-endian) while the byte order of a machine is not
            uint data1 = (uint)((b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0]);
            uint data2 = (uint)((b[5] << 8) | b[4]);
            uint data3 = (uint)((b[7] << 8) | b[6]);

            var groups = new uint[6];
            groups[0] = data1 / 16777216;                                   // the high 8 bits of Data1
            groups[1] = data1 % 16777216;                                   // the low 24 bits of Data1
            groups[2] = data2 * 256 + data3 / 256;                          // Data2 and the high byte of Data3
            groups[3] = (data3 % 256) * 65536 + (uint)(b[8] * 256 + b[9]);
            groups[4] = (uint)(b[10] * 65536 + b[11] * 256 + b[12]);
            groups[5] = (uint)(b[13] * 65536 + b[14] * 256 + b[15]);

            var result = new char[IfcGuidLength];
            int position = 0;
            int digits = 2;

            for (int i = 0; i < groups.Length; i++)
            {
                WriteDigits(groups[i], result, position, digits);
                position += digits;
                digits = 4;
            }

            return new string(result);
        }

        /// <summary>
        /// The conversion back: 22 characters into an identifier.
        /// Обратное преобразование: 22 символа в идентификатор.
        /// </summary>
        /// <param name="ifcGuid">The compressed identifier.</param>
        public static Guid FromIfcGuid(string ifcGuid)
        {
            if (!IsValidIfcGuid(ifcGuid))
            {
                throw new ArgumentException(
                    "'" + (ifcGuid ?? "<null>") + "' is not an IFC GUID: " + IfcGuidLength +
                    " characters of the alphabet " + Alphabet + " are expected.", nameof(ifcGuid));
            }

            var groups = new uint[6];
            int position = 0;
            int digits = 2;

            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = ReadDigits(ifcGuid, position, digits);
                position += digits;
                digits = 4;
            }

            uint data1 = groups[0] * 16777216 + groups[1];
            var data2 = (ushort)(groups[2] / 256);
            var data3 = (ushort)((groups[2] % 256) * 256 + groups[3] / 65536);

            return new Guid(
                data1,
                data2,
                data3,
                (byte)((groups[3] / 256) % 256),
                (byte)(groups[3] % 256),
                (byte)(groups[4] / 65536),
                (byte)((groups[4] / 256) % 256),
                (byte)(groups[4] % 256),
                (byte)(groups[5] / 65536),
                (byte)((groups[5] / 256) % 256),
                (byte)(groups[5] % 256));
        }

        /// <summary>
        /// A Revit UniqueId turned into an identifier the same way the Revit
        /// IFC exporter does it: the low 32 bits of the episode GUID are
        /// exclusive-ored with the element id.
        ///
        /// A UniqueId looks like "8-4-4-4-12" plus a hyphen and eight
        /// hexadecimal digits: 4f1a2b3c-…-9e8d7c6b5a4f-000133f2. The first part
        /// is the same for every element created in one session; it is the
        /// second part that tells elements apart. Taking the episode GUID alone
        /// would give every element the same identifier.
        ///
        /// Revit UniqueId, превращённый в идентификатор тем же способом, каким
        /// это делает экспортёр IFC у Revit: младшие 32 бита эпизодного GUID
        /// складываются по XOR с номером элемента.
        ///
        /// UniqueId выглядит как «8-4-4-4-12» плюс дефис и восемь
        /// шестнадцатеричных цифр: 4f1a2b3c-…-9e8d7c6b5a4f-000133f2. Первая
        /// часть одинакова для всех элементов одного сеанса, различает элементы
        /// вторая. Взяв один эпизодный GUID, мы дали бы всем элементам
        /// одинаковый идентификатор.
        /// </summary>
        /// <param name="uniqueId">The Revit UniqueId as the host reports it.</param>
        public static Guid FromRevitUniqueId(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                throw new ArgumentException("The Revit UniqueId is empty.", nameof(uniqueId));
            }

            string value = uniqueId.Trim();

            if (value.Length < 38 || value[36] != '-')
            {
                throw new ArgumentException(
                    "'" + uniqueId + "' does not look like a Revit UniqueId: a GUID, a hyphen and a hexadecimal element id are expected.",
                    nameof(uniqueId));
            }

            string episode = value.Substring(0, 36);
            string elementPart = value.Substring(37);

            Guid episodeGuid;
            if (!Guid.TryParseExact(episode, "D", out episodeGuid))
            {
                throw new ArgumentException(
                    "The first part '" + episode + "' does not parse as a GUID.", nameof(uniqueId));
            }

            ulong elementId;
            if (!ulong.TryParse(elementPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out elementId))
            {
                throw new ArgumentException(
                    "The part '" + elementPart + "' does not parse as a hexadecimal element id.",
                    nameof(uniqueId));
            }

            uint tail = uint.Parse(episode.Substring(28, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            // Since Revit 2024 element ids are 64-bit, while the exclusive-or
            // still takes the low 32 bits — that is what the Revit exporter does
            uint mixed = tail ^ (uint)(elementId & 0xFFFFFFFF);

            string combined = episode.Substring(0, 28) + mixed.ToString("x8", CultureInfo.InvariantCulture);

            return Guid.ParseExact(combined, "D");
        }

        /// <summary>
        /// A Revit UniqueId straight into a 22-character IFC GUID.
        /// Revit UniqueId сразу в 22-символьный IFC GUID.
        /// </summary>
        /// <param name="uniqueId">The Revit UniqueId as the host reports it.</param>
        public static string RevitUniqueIdToIfcGuid(string uniqueId)
        {
            return ToIfcGuid(FromRevitUniqueId(uniqueId));
        }

        /// <summary>
        /// Whether the value looks like a Revit UniqueId.
        /// Похоже ли значение на Revit UniqueId.
        /// </summary>
        /// <param name="value">The value to look at.</param>
        public static bool IsRevitUniqueId(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;

            string trimmed = value.Trim();
            if (trimmed.Length < 38 || trimmed[36] != '-') return false;

            Guid ignored;
            if (!Guid.TryParseExact(trimmed.Substring(0, 36), "D", out ignored)) return false;

            ulong elementId;
            return ulong.TryParse(trimmed.Substring(37), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out elementId);
        }

        /// <summary>
        /// Checks the shape of a 22-character identifier.
        /// Проверяет форму 22-символьного идентификатора.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsValidIfcGuid(string value)
        {
            if (value == null || value.Length != IfcGuidLength) return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (Alphabet.IndexOf(value[i]) < 0) return false;
            }

            // The first character carries two bits only: a value above 3 would
            // mean more than 128 bits, so the string did not come from a GUID
            return Alphabet.IndexOf(value[0]) <= 3;
        }

        private static void WriteDigits(uint value, char[] destination, int position, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                destination[position + i] = Alphabet[(int)(value % 64)];
                value /= 64;
            }
        }

        private static uint ReadDigits(string source, int position, int count)
        {
            uint value = 0;

            for (int i = 0; i < count; i++)
            {
                value = value * 64 + (uint)Alphabet.IndexOf(source[position + i]);
            }

            return value;
        }
    }
}
