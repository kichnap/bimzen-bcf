using System;
using System.Globalization;
using System.Xml;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Numbers and dates for the output files.
    ///
    /// The only place where this library turns numbers into strings. It is a
    /// class of its own not for tidiness: under a locale that uses a comma as
    /// the decimal separator, default formatting yields "12,5", and the parser
    /// on the receiving side falls apart on the coordinates. That has to be
    /// closed by code and a test, not by the care of everyone who edits a
    /// serializer.
    ///
    /// Числа и даты для выходных файлов.
    ///
    /// Единственное место, где библиотека превращает числа в строки. Отдельным
    /// классом это сделано не ради красоты: на локали с запятой в роли
    /// разделителя форматирование по умолчанию даёт «12,5», и парсер
    /// на приёмной стороне разваливается на координатах. Такое должно быть
    /// закрыто кодом и тестом, а не аккуратностью каждого, кто правит
    /// сериализатор.
    /// </summary>
    public static class BcfNumber
    {
        /// <summary>
        /// A number by the XML rules: a dot as the separator, whatever the locale.
        /// Число по правилам XML: точка как разделитель, независимо от локали.
        /// </summary>
        /// <param name="value">The value to write; NaN and infinity are refused.</param>
        public static string Format(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value), value, "NaN and infinity cannot be written into BCF.");
            }

            return XmlConvert.ToString(value);
        }

        /// <summary>
        /// Parses a number from XML. The reader's locale plays no part.
        /// Разбирает число из XML. Локаль читателя роли не играет.
        /// </summary>
        /// <param name="value">The text as it stood in the file.</param>
        public static double ParseDouble(string value)
        {
            return XmlConvert.ToDouble(value);
        }

        /// <summary>
        /// An integer by the XML rules.
        /// Целое по правилам XML.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public static string Format(int value)
        {
            return XmlConvert.ToString(value);
        }

        /// <summary>
        /// A date in ISO 8601 with an explicit offset: 2026-08-18T10:30:00+03:00.
        /// Local time without a zone is not acceptable in BCF — a receiving tool
        /// reads it as UTC and the moment shifts.
        ///
        /// Дата в ISO 8601 с явным смещением: 2026-08-18T10:30:00+03:00.
        /// Локальное время без зоны в BCF недопустимо: приёмник истолкует его
        /// как UTC, и момент сдвинется.
        /// </summary>
        /// <param name="value">The moment to write.</param>
        public static string Format(DateTimeOffset value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ssK", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// A date from a DateTime value. A time with no kind (Unspecified) is
        /// taken as local: that is what hosts usually hand out.
        ///
        /// Дата из значения DateTime. Время без указания вида (Unspecified)
        /// считается локальным: обычно именно такое отдают хосты.
        /// </summary>
        /// <param name="value">The moment to write.</param>
        public static string Format(DateTime value)
        {
            DateTime source = value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(value, DateTimeKind.Local)
                : value;

            return Format(new DateTimeOffset(source));
        }

        /// <summary>
        /// Parses a date from XML, keeping its offset.
        /// Разбирает дату из XML, сохраняя смещение.
        /// </summary>
        /// <param name="value">The text as it stood in the file.</param>
        public static DateTimeOffset ParseDate(string value)
        {
            return XmlConvert.ToDateTimeOffset(value);
        }
    }
}
