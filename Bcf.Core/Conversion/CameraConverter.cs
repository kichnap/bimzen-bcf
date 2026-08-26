using System;
using Bcf.Core.Geometry;
using Bcf.Core.Model;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// A host camera turned into a BCF camera.
    ///
    /// This is the place where a mistake is invisible in the output and shows
    /// up only at the coordinator's desk: the view opens onto nothing. That is
    /// why three things are stated explicitly here — the base orientation of
    /// the camera, the normalisation of the vectors, and the units.
    ///
    /// Камера хоста, превращённая в камеру BCF.
    ///
    /// Это место, где ошибка не видна на выходе и проявляется только
    /// у координатора: вид открывается «в пустоту». Поэтому здесь явно
    /// держатся три вещи — базовая ориентация камеры, нормировка векторов
    /// и единицы.
    /// </summary>
    public static class CameraConverter
    {
        /// <summary>
        /// Where a camera looks with no rotation applied: along −Z, with +Y up.
        /// The rotation of a view — a quaternion — is applied to this pair.
        ///
        /// Куда смотрит камера без всякого поворота: вдоль −Z, «вверх» +Y.
        /// Поворот вида — кватернион — применяется именно к этой паре.
        /// </summary>
        public static readonly Vector3 BaseDirection = new Vector3(0, 0, -1);

        /// <summary>
        /// The base up direction of a camera.
        /// Базовое направление «вверх» камеры.
        /// </summary>
        public static readonly Vector3 BaseUpVector = new Vector3(0, 1, 0);

        /// <summary>
        /// The smallest field of view the BCF 2.1 schema allows.
        /// Минимальный угол обзора, допустимый схемой BCF 2.1.
        /// </summary>
        public const double Bcf21MinFieldOfView = 45.0;

        /// <summary>
        /// The largest field of view the BCF 2.1 schema allows.
        /// Максимальный угол обзора, допустимый схемой BCF 2.1.
        /// </summary>
        public const double Bcf21MaxFieldOfView = 60.0;

        /// <summary>
        /// The viewing direction derived from the rotation of a view.
        /// Направление взгляда, выведенное из поворота вида.
        /// </summary>
        /// <param name="rotation">The rotation of the view.</param>
        public static Vector3 GetDirection(Rotation rotation)
        {
            return rotation.Rotate(BaseDirection).Normalized();
        }

        /// <summary>
        /// The up direction derived from the rotation of a view.
        /// Направление «вверх», выведенное из поворота вида.
        /// </summary>
        /// <param name="rotation">The rotation of the view.</param>
        public static Vector3 GetUpVector(Rotation rotation)
        {
            return rotation.Rotate(BaseUpVector).Normalized();
        }

        /// <summary>
        /// A perspective camera.
        /// Перспективная камера.
        /// </summary>
        /// <param name="position">The camera position in the internal units of the document.</param>
        /// <param name="rotation">The rotation of the view.</param>
        /// <param name="verticalFieldOfViewRadians">The vertical field of view, in radians.</param>
        /// <param name="aspectRatio">The width of the view divided by its height.</param>
        /// <param name="units">The units of the document.</param>
        public static BcfPerspectiveCamera ToPerspective(
            Vector3 position,
            Rotation rotation,
            double verticalFieldOfViewRadians,
            double aspectRatio,
            LengthUnit units)
        {
            EnsureAspectRatio(aspectRatio);

            if (verticalFieldOfViewRadians <= 0 || verticalFieldOfViewRadians >= Math.PI)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalFieldOfViewRadians), verticalFieldOfViewRadians,
                    "The field of view must be in radians and lie within (0; PI).");
            }

            return new BcfPerspectiveCamera
            {
                ViewPoint = UnitConverter.ToMeters(position, units),
                Direction = GetDirection(rotation),
                UpVector = GetUpVector(rotation),
                FieldOfViewDegrees = RadiansToDegrees(verticalFieldOfViewRadians),
                AspectRatio = aspectRatio
            };
        }

        /// <summary>
        /// An orthogonal camera.
        /// Ортогональная камера.
        /// </summary>
        /// <param name="position">The camera position in the internal units of the document.</param>
        /// <param name="rotation">The rotation of the view.</param>
        /// <param name="verticalExtent">The visible height of the view, in the internal units of the document.</param>
        /// <param name="aspectRatio">The width of the view divided by its height.</param>
        /// <param name="units">The units of the document.</param>
        public static BcfOrthogonalCamera ToOrthogonal(
            Vector3 position,
            Rotation rotation,
            double verticalExtent,
            double aspectRatio,
            LengthUnit units)
        {
            EnsureAspectRatio(aspectRatio);

            if (verticalExtent <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(verticalExtent), verticalExtent, "The height of an orthogonal view must be positive.");
            }

            return new BcfOrthogonalCamera
            {
                ViewPoint = UnitConverter.ToMeters(position, units),
                Direction = GetDirection(rotation),
                UpVector = GetUpVector(rotation),
                ViewToWorldScale = UnitConverter.ToMeters(verticalExtent, units),
                AspectRatio = aspectRatio
            };
        }

        /// <summary>
        /// Radians to degrees.
        /// Радианы в градусы.
        /// </summary>
        /// <param name="radians">The angle in radians.</param>
        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        /// <summary>
        /// Degrees to radians.
        /// Градусы в радианы.
        /// </summary>
        /// <param name="degrees">The angle in degrees.</param>
        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Fits the field of view into what the schema of a given version
        /// allows.
        ///
        /// The 2.1 schema permits [45; 60] only. A real view angle often falls
        /// outside those bounds, and without clamping the file fails
        /// validation. The fact that clamping happened is returned to the
        /// caller: the user has to know that the 2.1 view differs from the
        /// original.
        ///
        /// Подгоняет угол обзора под то, что разрешает схема конкретной версии.
        ///
        /// Схема 2.1 допускает только [45; 60]. Настоящий угол вида часто
        /// выходит за эти границы, и без подрезки файл не проходит проверку.
        /// Факт подрезки возвращается наружу: пользователь должен знать, что
        /// вид в 2.1 отличается от исходного.
        /// </summary>
        /// <param name="degrees">The angle to fit, in degrees.</param>
        /// <param name="version">The version whose limits apply.</param>
        /// <param name="clamped">True when the value had to be changed.</param>
        public static double ClampFieldOfView(double degrees, BcfVersion version, out bool clamped)
        {
            double min, max;

            if (version == BcfVersion.Bcf21)
            {
                min = Bcf21MinFieldOfView;
                max = Bcf21MaxFieldOfView;
            }
            else
            {
                // In 3.0 the bounds are open: (0; 180). We step a hair away from
                // them so that the value never lands on an excluded bound
                min = 0.001;
                max = 179.999;
            }

            if (degrees < min)
            {
                clamped = true;
                return min;
            }

            if (degrees > max)
            {
                clamped = true;
                return max;
            }

            clamped = false;
            return degrees;
        }

        private static void EnsureAspectRatio(double aspectRatio)
        {
            if (aspectRatio <= 0 || double.IsInfinity(aspectRatio) || double.IsNaN(aspectRatio))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(aspectRatio), aspectRatio,
                    "The aspect ratio must be positive: the 3.0 schema declares it as PositiveDouble.");
            }
        }
    }
}
