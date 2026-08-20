using System;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Xunit;

namespace Bcf.Core.Tests
{
    public class UnitConverterTests
    {
        [Theory]
        [InlineData(LengthUnit.Meters, 1.0)]
        [InlineData(LengthUnit.Centimeters, 0.01)]
        [InlineData(LengthUnit.Millimeters, 0.001)]
        [InlineData(LengthUnit.Kilometers, 1000.0)]
        [InlineData(LengthUnit.Feet, 0.3048)]
        [InlineData(LengthUnit.Inches, 0.0254)]
        [InlineData(LengthUnit.Yards, 0.9144)]
        [InlineData(LengthUnit.Miles, 1609.344)]
        [InlineData(LengthUnit.Micrometers, 1e-6)]
        [InlineData(LengthUnit.Mils, 2.54e-5)]
        [InlineData(LengthUnit.Microinches, 2.54e-8)]
        public void ScaleFactors_MatchDefinitions(LengthUnit unit, double expected)
        {
            Assert.Equal(expected, UnitConverter.ScaleFactorToMeters(unit), 12);
        }

        [Fact]
        public void FeetToMeters_UsesInternationalFoot()
        {
            // Модель из Revit чаще всего приходит в футах: 100 футов = 30.48 м.
            // Ошибка здесь незаметна в файле и видна только у координатора —
            // замечание оказывается в трёхстах метрах от здания.
            Assert.Equal(30.48, UnitConverter.ToMeters(100, LengthUnit.Feet), 9);
        }

        [Fact]
        public void PointIsConvertedComponentwise()
        {
            Vector3 meters = UnitConverter.ToMeters(new Vector3(1000, 2000, 3000), LengthUnit.Millimeters);

            Assert.Equal(1.0, meters.X, 9);
            Assert.Equal(2.0, meters.Y, 9);
            Assert.Equal(3.0, meters.Z, 9);
        }

        [Theory]
        [InlineData(LengthUnit.Feet)]
        [InlineData(LengthUnit.Millimeters)]
        [InlineData(LengthUnit.Inches)]
        public void RoundTrip_ReturnsOriginalValue(LengthUnit unit)
        {
            // Обратный перевод понадобится на втором этапе: вид из BCF
            // нужно будет восстановить в единицах документа
            var original = new Vector3(12.5, -7.25, 300.125);

            Vector3 restored = UnitConverter.FromMeters(UnitConverter.ToMeters(original, unit), unit);

            Assert.Equal(original.X, restored.X, 9);
            Assert.Equal(original.Y, restored.Y, 9);
            Assert.Equal(original.Z, restored.Z, 9);
        }

        [Fact]
        public void UnknownUnit_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => UnitConverter.ScaleFactorToMeters((LengthUnit)999));
        }
    }
}
