using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Устойчивый ключ замечания.
    ///
    /// Идентификаторы коллизий Navisworks пересоздаются при Reset теста, поэтому
    /// на них нельзя опираться: клиент выгружает один и тот же набор раз в неделю,
    /// и топики не должны дублироваться. Ключ считается от того, что переживает
    /// пересчёт — имени теста и идентификаторов участвующих элементов.
    ///
    /// Ключ хранится в карте «ключ -> Topic.Guid» рядом с .nwf, чтобы повторная
    /// выгрузка переиспользовала ранее выданные GUID, в том числе выданные сервером.
    /// </summary>
    public static class StableTopicKey
    {
        /// <summary>Разделитель частей. Символ-разделитель записей ASCII: в именах тестов и в GUID он не встречается.</summary>
        private const char PartSeparator = '\u001F';

        /// <summary>
        /// Ключ отдельной коллизии: имя теста плюс отсортированные идентификаторы
        /// её элементов. Сортировка обязательна — Navisworks может поменять
        /// местами элемент 1 и элемент 2 между прогонами.
        /// </summary>
        public static string ForClash(string testName, IEnumerable<string> componentIds)
        {
            if (componentIds == null) throw new ArgumentNullException(nameof(componentIds));

            var parts = new List<string> { Normalize(testName) };
            parts.AddRange(NormalizeIds(componentIds));

            return Compute(parts);
        }

        /// <summary>
        /// Ключ группы коллизий: имя теста плюс имя группы.
        ///
        /// Состав группы намеренно не участвует. Между выгрузками в неё
        /// добавляются и уходят отдельные коллизии, и если считать ключ
        /// по составу, каждое такое изменение порождало бы новый топик —
        /// то есть дубль ровно там, где группировка и нужна.
        /// </summary>
        public static string ForGroup(string testName, string groupName)
        {
            return Compute(new[] { Normalize(testName), Normalize(groupName) });
        }

        /// <summary>Ключ из произвольного набора частей.</summary>
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
        /// GUID топика по ключу — детерминированный, чтобы первая выгрузка
        /// на любой машине дала тот же идентификатор.
        ///
        /// Биты версии выставляются в 8 («custom», RFC 9562), а не в 4: это
        /// не случайный GUID, и притворяться случайным ему незачем. Формат
        /// от этого не страдает — BCF требует лишь шаблон 8-4-4-4-12.
        /// </summary>
        public static Guid ToTopicGuid(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Пустой ключ топика.", nameof(key));
            }

            byte[] hash;
            using (SHA256 sha = SHA256.Create())
            {
                hash = sha.ComputeHash(new UTF8Encoding(false).GetBytes(key.Trim()));
            }

            var bytes = new byte[16];
            Array.Copy(hash, bytes, 16);

            // Версия в старших четырёх битах седьмого байта
            bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
            // Вариант RFC 4122 в старших битах девятого байта
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

            return new Guid(bytes);
        }

        /// <summary>
        /// GUID в том виде, в каком его ждёт BCF: нижний регистр, формат D.
        /// Схема 3.0 проверяет это шаблоном и заглавные буквы отвергает.
        /// </summary>
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
