namespace Bcf.Core.Conversion
{
    /// <summary>
    /// The length unit a host document works in. The set mirrors what BIM
    /// applications offer, but it is declared here because the library
    /// references no host: an adapter maps the document unit onto this enum.
    ///
    /// BCF is always in metres — raw document coordinates must never reach the
    /// output XML.
    ///
    /// Единица длины документа хоста. Набор повторяет то, что предлагают
    /// BIM-приложения, но объявлен здесь: библиотека не ссылается ни на один
    /// хост, и адаптер переводит единицу документа в это перечисление.
    ///
    /// BCF всегда в метрах — сырых координат документа в выходном XML быть
    /// не должно.
    /// </summary>
    public enum LengthUnit
    {
        /// <summary>Metres. / Метры.</summary>
        Meters,

        /// <summary>Centimetres. / Сантиметры.</summary>
        Centimeters,

        /// <summary>Millimetres. / Миллиметры.</summary>
        Millimeters,

        /// <summary>Feet. / Футы.</summary>
        Feet,

        /// <summary>Inches. / Дюймы.</summary>
        Inches,

        /// <summary>Yards. / Ярды.</summary>
        Yards,

        /// <summary>Kilometres. / Километры.</summary>
        Kilometers,

        /// <summary>Miles. / Мили.</summary>
        Miles,

        /// <summary>Micrometres. / Микрометры.</summary>
        Micrometers,

        /// <summary>A thousandth of an inch. / Тысячная доля дюйма.</summary>
        Mils,

        /// <summary>A millionth of an inch. / Миллионная доля дюйма.</summary>
        Microinches
    }
}
