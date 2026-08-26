using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// The stable key of a topic.
    ///
    /// Clash identifiers are regenerated whenever a test is reset, so they
    /// cannot be relied upon: a coordinator exports the same set once a week
    /// and the topics must not duplicate. The key is computed from what
    /// survives a re-run — the test name and the identifiers of the elements
    /// involved.
    ///
    /// The key is kept in a "key to topic Guid" map next to the model, so that
    /// a repeated export reuses the identifiers issued earlier, including the
    /// ones issued by a server.
    ///
    /// Устойчивый ключ замечания.
    ///
    /// Идентификаторы коллизий пересоздаются при сбросе проверки, поэтому
    /// опираться на них нельзя: координатор выгружает один и тот же набор раз
    /// в неделю, и замечания не должны дублироваться. Ключ считается от того,
    /// что переживает пересчёт, — имени проверки и идентификаторов
    /// участвующих элементов.
    ///
    /// Ключ хранится в карте «ключ → Guid замечания» рядом с моделью, чтобы
    /// повторная выгрузка переиспользовала ранее выданные идентификаторы,
    /// в том числе выданные сервером.
    /// </summary>
    public static class StableTopicKey
    {
        /// <summary>
        /// The separator between parts: the ASCII unit separator, which occurs
        /// neither in test names nor in identifiers.
        ///
        /// Разделитель частей: символ-разделитель ASCII, который не встречается
        /// ни в именах проверок, ни в идентификаторах.
        /// </summary>
        private const char PartSeparator = '\u001F';

        /// <summary>
        /// The key of a single clash: the test name plus the sorted identifiers
        /// of its elements. Sorting is mandatory — a host may swap element one
        /// and element two between runs.
        ///
        /// Ключ отдельной коллизии: имя проверки плюс отсортированные
        /// идентификаторы её элементов. Сортировка обязательна: хост может
        /// поменять местами первый и второй элементы между прогонами.
        /// </summary>
        /// <param name="testName">The name of the clash test.</param>
        /// <param name="componentIds">The identifiers of the colliding elements.</param>
        public static string ForClash(string testName, IEnumerable<string> componentIds)
        {
            if (componentIds == null) throw new ArgumentNullException(nameof(componentIds));

            var parts = new List<string> { Normalize(testName) };
            parts.AddRange(NormalizeIds(componentIds));

            return Compute(parts);
        }

        /// <summary>
        /// The key of a clash group: the test name plus the group name.
        ///
        /// The membership of the group deliberately plays no part. Between
        /// exports individual clashes join it and leave it, and a key computed
        /// from the membership would produce a new topic on every such change —
        /// a duplicate exactly where grouping was meant to help.
        ///
        /// Ключ группы коллизий: имя проверки плюс имя группы.
        ///
        /// Состав группы намеренно не участвует. Между выгрузками в неё
        /// приходят и уходят отдельные коллизии, и ключ, считанный по составу,
        /// порождал бы новое замечание при каждом таком изменении — дубль ровно
        /// там, где группировка и нужна.
        /// </summary>
        /// <param name="testName">The name of the clash test.</param>
        /// <param name="groupName">The name of the group inside that test.</param>
        public static string ForGroup(string testName, string groupName)
        {
            return Compute(new[] { Normalize(testName), Normalize(groupName) });
        }

        /// <summary>
        /// A key from an arbitrary set of parts.
        /// Ключ из произвольного набора частей.
        /// </summary>
        /// <param name="parts">The parts, in a fixed order.</param>
        public static string Compute(IEnumerable<string> parts)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));

            string material = string.Join(PartSeparator.ToString(), parts.Select(Normalize).ToArray());

            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(material));
                return ToHex(hash);
            }
        }

        /// <summary>
        /// The topic identifier derived from the key — deterministic, so that a
        /// first export on any machine yields the same identifier.
        ///
        /// The version bits are set to 8 ("custom", RFC 9562) rather than to 4:
        /// this is not a random identifier and has no reason to pretend it is.
        /// The format does not suffer — BCF asks only for the 8-4-4-4-12 shape.
        ///
        /// Идентификатор замечания, выведенный из ключа: детерминированный,
        /// чтобы первая выгрузка на любой машине дала тот же идентификатор.
        ///
        /// Биты версии выставляются в 8 («custom», RFC 9562), а не в 4: это
        /// не случайный идентификатор, и притворяться случайным ему незачем.
        /// Формат от этого не страдает — BCF требует лишь шаблон 8-4-4-4-12.
        /// </summary>
        /// <param name="key">The stable key to derive from.</param>
        public static Guid ToTopicGuid(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("The topic key is empty.", nameof(key));
            }

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(key.Trim()));
            }

            var bytes = new byte[16];
            Array.Copy(hash, bytes, 16);

            // The version lives in the high four bits of the seventh byte
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
            // The RFC 4122 variant lives in the high bits of the ninth byte
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

            return new Guid(bytes);
        }

        /// <summary>
        /// The identifier in the shape BCF expects: lower case, format D. The
        /// 3.0 schema checks this with a pattern and rejects capitals.
        ///
        /// Идентификатор в том виде, какого ждёт BCF: нижний регистр, формат D.
        /// Схема 3.0 проверяет это шаблоном и заглавные буквы отвергает.
        /// </summary>
        /// <param name="guid">The identifier to format.</param>
        public static string FormatGuid(Guid guid)
        {
            return guid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        private static IEnumerable<string> NormalizeIds(IEnumerable<string> componentIds)
        {
            return componentIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal);
        }

        private static string Normalize(string part)
        {
            return part == null ? string.Empty : part.Trim();
        }

        private static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);

            foreach (byte b in bytes)
            {
                sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }
    }
}
