using System;
using Bcf.Core;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Xunit;

namespace Bcf.Core.Tests
{
    public class CameraConverterTests
    {
        private const double Tolerance = 1e-9;

        [Fact]
        public void NoRotation_LooksAlongMinusZ()
        {
            // The base orientation of a Navisworks camera: looking along -Z, up along +Y
            AssertVector(new Vector3(0, 0, -1), CameraConverter.GetDirection(Rotation.Identity));
            AssertVector(new Vector3(0, 1, 0), CameraConverter.GetUpVector(Rotation.Identity));
        }

        [Fact]
        public void QuarterTurnAroundX_GivesHorizontalViewInZUpWorld()
        {
            // A 90 degree turn about X makes the look along -Z into a look along +Y and
            // "up" into +Z. That is an ordinary elevation view in a Z-up world: had the
            // axes been mixed up, the camera would be looking at the ground.
            Rotation rotation = Rotation.FromAxisAngle(new Vector3(1, 0, 0), Math.PI / 2);

            AssertVector(new Vector3(0, 1, 0), CameraConverter.GetDirection(rotation));
            AssertVector(new Vector3(0, 0, 1), CameraConverter.GetUpVector(rotation));
        }

        [Fact]
        public void QuarterTurnAroundZ_TurnsUpVectorOnly()
        {
            // A spin about the viewing axis changes no direction, only "up"
            Rotation rotation = Rotation.FromAxisAngle(new Vector3(0, 0, 1), Math.PI / 2);

            AssertVector(new Vector3(0, 0, -1), CameraConverter.GetDirection(rotation));
            AssertVector(new Vector3(-1, 0, 0), CameraConverter.GetUpVector(rotation));
        }

        [Fact]
        public void HalfTurnAroundY_FlipsDirection()
        {
            Rotation rotation = Rotation.FromAxisAngle(new Vector3(0, 1, 0), Math.PI);

            AssertVector(new Vector3(0, 0, 1), CameraConverter.GetDirection(rotation));
            AssertVector(new Vector3(0, 1, 0), CameraConverter.GetUpVector(rotation));
        }

        [Fact]
        public void NonUnitQuaternion_StillGivesUnitVectors()
        {
            // A quaternion from an API may arrive slightly denormalised after turns have
            // accumulated; the vectors in the file have to stay unit length
            var rotation = new Rotation(0.6, 0.0, 0.0, 0.6);

            Assert.Equal(1.0, CameraConverter.GetDirection(rotation).Length, 9);
            Assert.Equal(1.0, CameraConverter.GetUpVector(rotation).Length, 9);
        }

        [Fact]
        public void DirectionAndUp_StayPerpendicular()
        {
            var rotation = Rotation.FromAxisAngle(new Vector3(0.3, -0.7, 0.5), 1.234);

            double dot = CameraConverter.GetDirection(rotation).Dot(CameraConverter.GetUpVector(rotation));

            Assert.Equal(0.0, dot, 9);
        }

        [Fact]
        public void PerspectiveCamera_ConvertsUnitsAndAngle()
        {
            BcfPerspectiveCamera camera = CameraConverter.ToPerspective(
                new Vector3(10, 20, 30),
                Rotation.Identity,
                Math.PI / 3,          // 60 градусов
                16.0 / 9.0,
                LengthUnit.Feet);

            // 10 feet = 3.048 m — not one raw coordinate may reach the file
            AssertVector(new Vector3(3.048, 6.096, 9.144), camera.ViewPoint, 1e-9);
            Assert.Equal(60.0, camera.FieldOfViewDegrees, 9);
            Assert.Equal(16.0 / 9.0, camera.AspectRatio, 9);
        }

        [Fact]
        public void OrthogonalCamera_ConvertsViewToWorldScaleToMeters()
        {
            BcfOrthogonalCamera camera = CameraConverter.ToOrthogonal(
                new Vector3(0, 0, 1000),
                Rotation.Identity,
                5000,                 // миллиметры
                1.5,
                LengthUnit.Millimeters);

            Assert.Equal(5.0, camera.ViewToWorldScale, 9);
            AssertVector(new Vector3(0, 0, 1), camera.ViewPoint, 1e-9);
        }

        [Fact]
        public void FieldOfView_ClampedForBcf21()
        {
            bool clamped;

            // The 2.1 schema allows only [45; 60] — otherwise the file fails validation
            Assert.Equal(45.0, CameraConverter.ClampFieldOfView(30.0, BcfVersion.Bcf21, out clamped));
            Assert.True(clamped);

            Assert.Equal(60.0, CameraConverter.ClampFieldOfView(90.0, BcfVersion.Bcf21, out clamped));
            Assert.True(clamped);

            Assert.Equal(55.0, CameraConverter.ClampFieldOfView(55.0, BcfVersion.Bcf21, out clamped));
            Assert.False(clamped);
        }

        [Fact]
        public void FieldOfView_KeptForBcf30()
        {
            bool clamped;

            Assert.Equal(90.0, CameraConverter.ClampFieldOfView(90.0, BcfVersion.Bcf30, out clamped));
            Assert.False(clamped);

            // In 3.0 the (0; 180) bounds are open: exactly 180 cannot be written
            Assert.True(CameraConverter.ClampFieldOfView(180.0, BcfVersion.Bcf30, out clamped) < 180.0);
            Assert.True(clamped);
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-1.0)]
        [InlineData(double.NaN)]
        public void InvalidAspectRatio_Throws(double aspectRatio)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraConverter.ToPerspective(
                Vector3.Zero, Rotation.Identity, Math.PI / 4, aspectRatio, LengthUnit.Meters));
        }

        [Theory]
        [InlineData(0.0)]
        [InlineData(-0.5)]
        [InlineData(4.0)]   // больше PI: значит, на вход пришли градусы вместо радиан
        public void InvalidFieldOfView_Throws(double radians)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CameraConverter.ToPerspective(
                Vector3.Zero, Rotation.Identity, radians, 1.0, LengthUnit.Meters));
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, double tolerance = Tolerance)
        {
            Assert.True(Math.Abs(expected.X - actual.X) < tolerance
                        && Math.Abs(expected.Y - actual.Y) < tolerance
                        && Math.Abs(expected.Z - actual.Z) < tolerance,
                "Ожидалось " + expected + ", получено " + actual);
        }
    }
}
