namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Единица длины документа. Повторяет набор <c>Autodesk.Navisworks.Api.Units</c>,
    /// но объявлена здесь: Bcf.Core не ссылается на Navisworks, поэтому
    /// адаптер в плагине переводит <c>Document.Units</c> в это перечисление.
    ///
    /// BCF всегда в метрах — сырых координат документа в выходном XML быть не должно.
    /// </summary>
    public enum LengthUnit
    {
        Meters,
        Centimeters,
        Millimeters,
        Feet,
        Inches,
        Yards,
        Kilometers,
        Miles,
        Micrometers,

        /// <summary>Тысячная доля дюйма.</summary>
        Mils,

        /// <summary>Миллионная доля дюйма.</summary>
        Microinches
    }
}
