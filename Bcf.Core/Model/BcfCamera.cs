using Bcf.Core.Geometry;

namespace Bcf.Core.Model
{
    /// <summary>
    /// The camera of a viewpoint. Coordinates are in metres, vectors are unit
    /// vectors, and the coordinate system is right-handed with Z up — BCF
    /// requires that regardless of how the host stores its model.
    ///
    /// Камера точки зрения. Координаты в метрах, векторы единичные, система
    /// координат правая с Z вверх: так требует BCF независимо от того, в чём
    /// хранит модель хост.
    /// </summary>
    public abstract class BcfCamera
    {
        /// <summary>
        /// The camera position, in metres.
        /// Положение камеры, метры.
        /// </summary>
        public Vector3 ViewPoint { get; set; }

        /// <summary>
        /// The viewing direction, a unit vector.
        /// Направление взгляда, единичный вектор.
        /// </summary>
        public Vector3 Direction { get; set; }

        /// <summary>
        /// The up direction, a unit vector.
        /// Направление «вверх», единичный вектор.
        /// </summary>
        public Vector3 UpVector { get; set; }

        /// <summary>
        /// The width of the view divided by its height. BCF 3.0 requires the
        /// element and requires it to be positive; 2.1 has no such element and
        /// the serializer drops it.
        ///
        /// Отношение ширины вида к высоте. В 3.0 поле обязательное и должно
        /// быть положительным; в 2.1 его нет, и сериализатор его отбрасывает.
        /// </summary>
        public double AspectRatio { get; set; }
    }

    /// <summary>
    /// A perspective camera.
    /// Перспективная камера.
    /// </summary>
    public sealed class BcfPerspectiveCamera : BcfCamera
    {
        /// <summary>
        /// The vertical field of view in degrees. The 3.0 schema allows
        /// (0; 180); the 2.1 schema allows [45; 60] only, so writing 2.1 has to
        /// clamp the value and say so in the report.
        ///
        /// Вертикальный угол обзора в градусах. Схема 3.0 разрешает диапазон
        /// (0; 180), схема 2.1 — только [45; 60], поэтому при выгрузке в 2.1
        /// значение приходится подрезать и сообщать об этом в отчёте.
        /// </summary>
        public double FieldOfViewDegrees { get; set; }
    }

    /// <summary>
    /// An orthogonal camera.
    /// Ортогональная камера.
    /// </summary>
    public sealed class BcfOrthogonalCamera : BcfCamera
    {
        /// <summary>
        /// The visible height of the view, in metres.
        /// Видимая высота вида в метрах.
        /// </summary>
        public double ViewToWorldScale { get; set; }
    }
}
