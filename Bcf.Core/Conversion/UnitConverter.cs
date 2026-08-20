using System;
using Bcf.Core.Geometry;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Перевод длин из единиц документа в метры.
    ///
    /// BCF задаёт координаты строго в метрах. Navisworks хранит модель во
    /// внутренних единицах документа — у файла из Revit это могут быть футы,
    /// у собранного из IFC — метры, и различие проявляется не при экспорте,
    /// а у координатора, когда точка замечания оказывается в трёхстах метрах
    /// от здания.
    /// </summary>
    public static class UnitConverter
    {
        /// <summary>Множитель перевода единицы в метры.</summary>
        public static double ScaleFactorToMeters(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Meters: return 1.0;
                case LengthUnit.Centimeters: return 0.01;
                case LengthUnit.Millimeters: return 0.001;
                case LengthUnit.Kilometers: return 1000.0;

                // Международный фут: 0.3048 м ровно, отсюда и остальные
                case LengthUnit.Feet: return 0.3048;
                case LengthUnit.Inches: return 0.0254;
                case LengthUnit.Yards: return 0.9144;
                case LengthUnit.Miles: return 1609.344;

                case LengthUnit.Micrometers: return 1e-6;
                case LengthUnit.Mils: return 2.54e-5;
                case LengthUnit.Microinches: return 2.54e-8;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(unit), unit, "Неизвестная единица длины документа.");
            }
        }

        /// <summary>Длина в метрах.</summary>
        public static double ToMeters(double value, LengthUnit unit)
        {
            return value * ScaleFactorToMeters(unit);
        }

        /// <summary>Точка в метрах.</summary>
        public static Vector3 ToMeters(Vector3 point, LengthUnit unit)
        {
            return point.Scaled(ScaleFactorToMeters(unit));
        }

        /// <summary>
        /// Обратный перевод — понадобится на втором этапе, когда вид из BCF
        /// нужно будет восстановить в Navisworks.
        /// </summary>
        public static double FromMeters(double meters, LengthUnit unit)
        {
            return meters / ScaleFactorToMeters(unit);
        }

        /// <summary>Точка из метров в единицы документа.</summary>
        public static Vector3 FromMeters(Vector3 meters, LengthUnit unit)
        {
            return meters.Scaled(1.0 / ScaleFactorToMeters(unit));
        }
    }
}
