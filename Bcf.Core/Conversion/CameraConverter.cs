using System;
using Bcf.Core.Geometry;
using Bcf.Core.Model;

namespace Bcf.Core.Conversion
{
    /// <summary>
    /// Камера Navisworks в камеру BCF.
    ///
    /// Место, где ошибка не видна на выходе и проявляется только у координатора:
    /// вид открывается «в пустоту». Поэтому здесь три вещи держатся явно —
    /// базовая ориентация камеры, нормировка векторов и единицы.
    /// </summary>
    public static class CameraConverter
    {
        /// <summary>
        /// Куда смотрит камера Navisworks без поворота: вдоль −Z, «вверх» +Y.
        /// Поворот вида (кватернион Rotation3D) применяется именно к этой паре.
        /// </summary>
        public static readonly Vector3 BaseDirection = new Vector3(0, 0, -1);

        /// <summary>Базовое направление «вверх» камеры Navisworks.</summary>
        public static readonly Vector3 BaseUpVector = new Vector3(0, 1, 0);

        /// <summary>Минимальный угол обзора, допустимый схемой BCF 2.1.</summary>
        public const double Bcf21MinFieldOfView = 45.0;

        /// <summary>Максимальный угол обзора, допустимый схемой BCF 2.1.</summary>
        public const double Bcf21MaxFieldOfView = 60.0;

        /// <summary>Направление взгляда из поворота вида.</summary>
        public static Vector3 GetDirection(Rotation rotation)
        {
            return rotation.Rotate(BaseDirection).Normalized();
        }

        /// <summary>Направление «вверх» из поворота вида.</summary>
        public static Vector3 GetUpVector(Rotation rotation)
        {
            return rotation.Rotate(BaseUpVector).Normalized();
        }

        /// <summary>
        /// Перспективная камера.
        /// </summary>
        /// <param name="position">Положение камеры во внутренних единицах документа.</param>
        /// <param name="rotation">Поворот вида (Viewpoint.Rotation).</param>
        /// <param name="verticalFieldOfViewRadians">Вертикальный угол обзора в радианах (Viewpoint.HeightField).</param>
        /// <param name="aspectRatio">Отношение ширины вида к высоте.</param>
        /// <param name="units">Единицы документа (Document.Units).</param>
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
                    "Угол обзора должен быть в радианах и лежать в интервале (0; PI).");
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
        /// Ортогональная камера.
        /// </summary>
        /// <param name="position">Положение камеры во внутренних единицах документа.</param>
        /// <param name="rotation">Поворот вида.</param>
        /// <param name="verticalExtent">Видимая высота вида во внутренних единицах документа (Viewpoint.VerticalExtent).</param>
        /// <param name="aspectRatio">Отношение ширины вида к высоте.</param>
        /// <param name="units">Единицы документа.</param>
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
                    nameof(verticalExtent), verticalExtent, "Высота ортогонального вида должна быть положительной.");
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

        public static double RadiansToDegrees(double radians)
        {
            return radians * 180.0 / Math.PI;
        }

        public static double DegreesToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Подгоняет угол обзора под ограничения схемы конкретной версии.
        ///
        /// В 2.1 схема разрешает только [45; 60] — реальный угол вида Navisworks
        /// часто выходит за эти границы, и без подрезки файл не проходит
        /// валидацию. Факт подрезки возвращается наружу: пользователь должен
        /// знать, что в 2.1 вид отличается от исходного.
        /// </summary>
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
                // В 3.0 границы открытые: (0; 180). Отступаем от них на волос,
                // чтобы значение не совпало с исключённой границей.
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
                    "Отношение сторон вида должно быть положительным: в схеме 3.0 это PositiveDouble.");
            }
        }
    }
}
