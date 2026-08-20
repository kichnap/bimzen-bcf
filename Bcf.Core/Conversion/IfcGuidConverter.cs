using System;
using System.Globalization;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Идентификаторы элементов для BCF.
    ///
    /// В BCF компонент адресуется 22-символьным IFC GUID. У моделей из IFC он
    /// есть в свойствах, у NWC из Revit — нет: там свой UniqueId, и его надо
    /// пересчитать тем же алгоритмом, каким это делает экспортёр IFC у Revit.
    /// Без корректного идентификатора замечание в приёмнике откроется,
    /// но нужный элемент не подсветит — то есть будет бесполезно.
    /// </summary>
    public static class IfcGuidConverter
    {
        /// <summary>
        /// Алфавит base64 из спецификации buildingSMART. Отличается от обычного
        /// base64 порядком и двумя последними символами — подставлять
        /// System.Convert.ToBase64String нельзя.
        /// </summary>
        public const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz_$";

        /// <summary>Длина сжатого IFC GUID.</summary>
        public const int IfcGuidLength = 22;

        /// <summary>
        /// 128-битный GUID в 22 символа. Первый символ несёт только два бита:
        /// 22 x 6 = 132 бита, из них значащих 128, поэтому старшая группа
        /// пишется двумя символами, а остальные пять — по четыре.
        /// </summary>
        public static string ToIfcGuid(Guid guid)
        {
            byte[] b = guid.ToByteArray();

            // Разбираем байты явно, а не через BitConverter: раскладка Guid
            // фиксирована (Data1 и Data2/3 хранятся little-endian), а порядок
            // байт машины — нет.
            uint data1 = (uint)((b[3] << 24) | (b[2] << 16) | (b[1] << 8) | b[0]);
            uint data2 = (uint)((b[5] << 8) | b[4]);
            uint data3 = (uint)((b[7] << 8) | b[6]);

            var groups = new uint[6];
            groups[0] = data1 / 16777216;                                   // старшие 8 бит Data1
            groups[1] = data1 % 16777216;                                   // младшие 24 бита Data1
            groups[2] = data2 * 256 + data3 / 256;                          // Data2 и старший байт Data3
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

        /// <summary>Обратное преобразование: 22 символа в Guid.</summary>
        public static Guid FromIfcGuid(string ifcGuid)
        {
            if (!IsValidIfcGuid(ifcGuid))
            {
                throw new ArgumentException(
                    "'" + (ifcGuid ?? "<null>") + "' не является IFC GUID: нужно " + IfcGuidLength +
                    " символов из алфавита " + Alphabet + ".", nameof(ifcGuid));
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
        /// Revit UniqueId в Guid тем же способом, что применяет экспортёр IFC
        /// у Revit: младшие 32 бита эпизодного GUID складываются по XOR
        /// с идентификатором элемента.
        ///
        /// UniqueId выглядит как «8-4-4-4-12» плюс дефис и восемь шестнадцатеричных
        /// цифр: 4f1a2b3c-...-9e8d7c6b5a4f-000133f2. Первая часть одинакова для
        /// всех элементов сеанса создания файла, различает элементы вторая —
        /// поэтому просто взять эпизодный GUID нельзя, все элементы окажутся
        /// с одним идентификатором.
        /// </summary>
        public static Guid FromRevitUniqueId(string uniqueId)
        {
            if (string.IsNullOrWhiteSpace(uniqueId))
            {
                throw new ArgumentException("Пустой Revit UniqueId.", nameof(uniqueId));
            }

            string value = uniqueId.Trim();

            if (value.Length < 38 || value[36] != '-')
            {
                throw new ArgumentException(
                    "'" + uniqueId + "' не похож на Revit UniqueId: ожидается GUID, дефис и шестнадцатеричный идентификатор элемента.",
                    nameof(uniqueId));
            }

            string episode = value.Substring(0, 36);
            string elementPart = value.Substring(37);

            Guid episodeGuid;
            if (!Guid.TryParseExact(episode, "D", out episodeGuid))
            {
                throw new ArgumentException(
                    "Первая часть '" + episode + "' не разбирается как GUID.", nameof(uniqueId));
            }

            ulong elementId;
            if (!ulong.TryParse(elementPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out elementId))
            {
                throw new ArgumentException(
                    "Часть '" + elementPart + "' не разбирается как шестнадцатеричный идентификатор элемента.",
                    nameof(uniqueId));
            }

            uint tail = uint.Parse(episode.Substring(28, 8), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            // С Revit 2024 идентификаторы стали 64-битными, а в XOR по-прежнему
            // участвуют младшие 32 бита — так делает и сам экспортёр Revit.
            uint mixed = tail ^ (uint)(elementId & 0xFFFFFFFF);

            string combined = episode.Substring(0, 28) + mixed.ToString("x8", CultureInfo.InvariantCulture);

            return Guid.ParseExact(combined, "D");
        }

        /// <summary>Revit UniqueId сразу в 22-символьный IFC GUID.</summary>
        public static string RevitUniqueIdToIfcGuid(string uniqueId)
        {
            return ToIfcGuid(FromRevitUniqueId(uniqueId));
        }

        /// <summary>Похоже ли значение на Revit UniqueId.</summary>
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

        /// <summary>Проверка формата 22-символьного идентификатора.</summary>
        public static bool IsValidIfcGuid(string value)
        {
            if (value == null || value.Length != IfcGuidLength) return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (Alphabet.IndexOf(value[i]) < 0) return false;
            }

            // Первый символ кодирует всего два бита: значения выше 3 означают
            // больше 128 бит, то есть строка не из GUID.
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
