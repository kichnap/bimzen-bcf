using Bcf.Core.Geometry;

namespace Bcf.Core.Model
{
    /// <summary>
    /// Камера точки зрения. Координаты — в метрах, векторы — единичные,
    /// система координат правая с Z вверх: так требует BCF независимо от того,
    /// в чём хранит модель хост.
    /// </summary>
    public abstract class BcfCamera
    {
        /// <summary>Положение камеры, метры.</summary>
        public Vector3 ViewPoint { get; set; }

        /// <summary>Направление взгляда, единичный вектор.</summary>
        public Vector3 Direction { get; set; }

        /// <summary>Направление «вверх», единичный вектор.</summary>
        public Vector3 UpVector { get; set; }

        /// <summary>
        /// Отношение ширины вида к высоте. В 3.0 поле обязательное и должно быть
        /// положительным; в 2.1 его нет и сериализатор его отбрасывает.
        /// </summary>
        public double AspectRatio { get; set; }
    }

    /// <summary>Перспективная камера.</summary>
    public sealed class BcfPerspectiveCamera : BcfCamera
    {
        /// <summary>
        /// Вертикальный угол обзора в градусах. Схема 3.0 разрешает диапазон
        /// (0; 180), схема 2.1 — только [45; 60], поэтому при выгрузке в 2.1
        /// значение приходится подрезать и сообщать об этом в отчёте.
        /// </summary>
        public double FieldOfViewDegrees { get; set; }
    }

    /// <summary>Ортогональная камера.</summary>
    public sealed class BcfOrthogonalCamera : BcfCamera
    {
        /// <summary>Видимая высота вида в метрах.</summary>
        public double ViewToWorldScale { get; set; }
    }
}
