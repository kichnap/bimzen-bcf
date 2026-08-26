using System;
using System.Globalization;
using System.Threading;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// The numbers in the output files must not depend on the locale of a machine.
    /// Under a Russian locale the default formatting gives "12,5", and a parser on
    /// the receiving side falls apart on the coordinates. This has to be held by a
    /// test rather than by carefulness.
    /// </summary>
    public class InvariantCultureTests
    {
        [Theory]
        [InlineData("ru-RU")]
        [InlineData("de-DE")]
        [InlineData("fr-FR")]
        public void Numbers_UseDotSeparator_OnAnyCulture(string cultureName)
        {
            RunWithCulture(cultureName, () =>
            {
                Assert.Equal("12.5", BcfNumber.Format(12.5));
                Assert.Equal("-0.125", BcfNumber.Format(-0.125));
                Assert.Equal("0", BcfNumber.Format(0.0));
            });
        }

        [Fact]
        public void Numbers_SurviveRoundTripOnRussianLocale()
        {
            RunWithCulture("ru-RU", () =>
            {
                const double value = 3.14159265358979;

                Assert.Equal(value, BcfNumber.ParseDouble(BcfNumber.Format(value)), 12);
            });
        }

        [Fact]
        public void CameraCoordinates_HaveNoCommas_OnRussianLocale()
        {
            RunWithCulture("ru-RU", () =>
            {
                BcfPerspectiveCamera camera = CameraConverter.ToPerspective(
                    new Vector3(10.5, -20.25, 30.125),
                    Rotation.Identity,
                    Math.PI / 3,
                    1.5,
                    LengthUnit.Meters);

                string x = BcfNumber.Format(camera.ViewPoint.X);
                string fov = BcfNumber.Format(camera.FieldOfViewDegrees);

                Assert.DoesNotContain(",", x, StringComparison.Ordinal);
                Assert.DoesNotContain(",", fov, StringComparison.Ordinal);
                Assert.Equal("10.5", x);
            });
        }

        [Fact]
        public void VectorDiagnostics_StayInvariant()
        {
            RunWithCulture("ru-RU", () => Assert.Equal("(1.5, 2.5, 3.5)", new Vector3(1.5, 2.5, 3.5).ToString()));
        }

        [Fact]
        public void Dates_CarryExplicitOffset()
        {
            RunWithCulture("ru-RU", () =>
            {
                var moment = new DateTimeOffset(2026, 8, 18, 10, 30, 0, TimeSpan.FromHours(3));

                Assert.Equal("2026-08-18T10:30:00+03:00", BcfNumber.Format(moment));
            });
        }

        [Fact]
        public void Dates_InUtc_CarryNumericOffset()
        {
            // ISO 8601 allows both "Z" and "+00:00". A numeric offset is always written:
            // one form for every date is one reason fewer for parsers to disagree.
            var moment = new DateTimeOffset(2026, 8, 18, 7, 30, 0, TimeSpan.Zero);

            Assert.Equal("2026-08-18T07:30:00+00:00", BcfNumber.Format(moment));
        }

        [Fact]
        public void UnspecifiedDateTime_IsTreatedAsLocal()
        {
            // Navisworks hands over comment dates with no kind specified;
            // treating them as UTC would shift the time at the receiving end
            var local = new DateTime(2026, 8, 18, 10, 30, 0, DateTimeKind.Unspecified);

            string formatted = BcfNumber.Format(local);

            Assert.StartsWith("2026-08-18T10:30:00", formatted, StringComparison.Ordinal);
            Assert.True(formatted.Length > 19, "Дата обязана нести явное смещение: " + formatted);
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        public void NotANumber_IsRejected(double value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => BcfNumber.Format(value));
        }

        private static void RunWithCulture(string cultureName, Action action)
        {
            CultureInfo previousCulture = CultureInfo.CurrentCulture;
            CultureInfo previousUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                var culture = new CultureInfo(cultureName);
                Thread.CurrentThread.CurrentCulture = culture;
                Thread.CurrentThread.CurrentUICulture = culture;

                action();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previousCulture;
                Thread.CurrentThread.CurrentUICulture = previousUiCulture;
            }
        }
    }
}
