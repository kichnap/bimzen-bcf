using System;
using System.Globalization;
using System.Xml;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Числа и даты для выходных файлов.
    ///
    /// Единственное место, где Bcf.Core превращает числа в строки. Сделано
    /// отдельным классом не ради красоты: на русской локали форматирование
    /// по умолчанию даёт «12,5», и парсер на стороне сервиса разваливается
    /// на координатах. Такое должно быть закрыто кодом и тестом, а не
    /// аккуратностью каждого, кто правит сериализатор.
    /// </summary>
    public static class BcfNumber
    {
        /// <summary>Число по правилам XML: точка как разделитель, независимо от локали.</summary>
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "NaN и бесконечность в BCF записать нельзя.");
            }

            return XmlConvert.ToString(value);
        }

        /// <summary>Разбор числа из XML. Локаль читателя роли не играет.</summary>
        public static double ParseDouble(string value)
        {
            return XmlConvert.ToDouble(value);
        }

        /// <summary>Целое по правилам XML.</summary>
        public static string Format(int value)
        {
            return XmlConvert.ToString(value);
        }

        /// <summary>
        /// Дата в ISO 8601 с явным смещением: 2026-08-18T10:30:00+03:00.
        /// Локальное время без зоны в BCF недопустимо — на приёмнике оно
        /// истолкуется как UTC и сдвинется.
        /// </summary>
        public static string Format(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Дата из значения DateTime. Время без указания вида (Unspecified)
        /// считается локальным: это то, что отдаёт Navisworks.
        /// </summary>
        public static string Format(DateTime value)
        {
            DateTime source = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Local)
                : value;

            return Format(new DateTimeOffset(source));
        }

        /// <summary>Разбор даты из XML с сохранением смещения.</summary>
        public static DateTimeOffset ParseDate(string value)
        {
            return XmlConvert.ToDateTimeOffset(value);
        }
    }
}
