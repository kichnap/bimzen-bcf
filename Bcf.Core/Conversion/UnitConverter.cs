using System;
using Bcf.Core.Geometry;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Converting lengths from the units of a document into metres.
    ///
    /// BCF states coordinates strictly in metres. A host keeps the model in the
    /// internal units of its document — feet for a file coming from one
    /// authoring tool, metres for one assembled from IFC — and the difference
    /// does not show up during the export. It shows up at the coordinator's
    /// desk, when the issue point sits three hundred metres away from the
    /// building.
    ///
    /// Перевод длин из единиц документа в метры.
    ///
    /// BCF задаёт координаты строго в метрах. Хост хранит модель во внутренних
    /// единицах своего документа — у файла из одного приложения это футы,
    /// у собранного из IFC метры, — и различие проявляется не при выгрузке.
    /// Оно проявляется у координатора, когда точка замечания оказывается
    /// в трёхстах метрах от здания.
    /// </summary>
    public static class UnitConverter
    {
        /// <summary>
        /// The factor that turns the unit into metres.
        /// Множитель, переводящий единицу в метры.
        /// </summary>
        /// <param name="unit">The unit of the host document.</param>
        public static double ScaleFactorToMeters(LengthUnit unit)
        {
            switch (unit)
            {
                case LengthUnit.Meters: return 1.0;
                case LengthUnit.Centimeters: return 0.01;
                case LengthUnit.Millimeters: return 0.001;
                case LengthUnit.Kilometers: return 1000.0;

                // The international foot: exactly 0.3048 m, and the rest follows
                case LengthUnit.Feet: return 0.3048;
                case LengthUnit.Inches: return 0.0254;
                case LengthUnit.Yards: return 0.9144;
                case LengthUnit.Miles: return 1609.344;

                case LengthUnit.Micrometers: return 1e-6;
                case LengthUnit.Mils: return 2.54e-5;
                case LengthUnit.Microinches: return 2.54e-8;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(unit), unit, "Unknown document length unit.");
            }
        }

        /// <summary>
        /// A length in metres.
        /// Длина в метрах.
        /// </summary>
        /// <param name="value">The length in document units.</param>
        /// <param name="unit">The unit of the host document.</param>
        public static double ToMeters(double value, LengthUnit unit)
        {
            return value * ScaleFactorToMeters(unit);
        }

        /// <summary>
        /// A point in metres.
        /// Точка в метрах.
        /// </summary>
        /// <param name="point">The point in document units.</param>
        /// <param name="unit">The unit of the host document.</param>
        public static Vector3 ToMeters(Vector3 point, LengthUnit unit)
        {
            return point.Scaled(ScaleFactorToMeters(unit));
        }

        /// <summary>
        /// The conversion back, needed by a host that restores a BCF viewpoint
        /// inside its own document.
        ///
        /// Обратный перевод — он нужен хосту, который восстанавливает точку
        /// зрения BCF в своём документе.
        /// </summary>
        /// <param name="meters">The length in metres.</param>
        /// <param name="unit">The unit of the host document.</param>
        public static double FromMeters(double meters, LengthUnit unit)
        {
            return meters / ScaleFactorToMeters(unit);
        }

        /// <summary>
        /// A point from metres into document units.
        /// Точка из метров в единицы документа.
        /// </summary>
        /// <param name="meters">The point in metres.</param>
        /// <param name="unit">The unit of the host document.</param>
        public static Vector3 FromMeters(Vector3 meters, LengthUnit unit)
        {
            return meters.Scaled(1.0 / ScaleFactorToMeters(unit));
        }
    }
}
