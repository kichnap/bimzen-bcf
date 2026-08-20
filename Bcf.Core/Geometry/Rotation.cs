using System;

namespace Bcf.Core.Geometry
{
    /// <summary>
    /// Кватернион поворота. В Navisworks ориентация вида хранится именно так
    /// (<c>Viewpoint.Rotation</c>, тип <c>Rotation3D</c>), а BCF просит два
    /// вектора — направление взгляда и «вверх». Перевод делает
    /// <see cref="Bcf.Core.Conversion.CameraConverter"/>.
    /// </summary>
    public struct Rotation : IEquatable<Rotation>
    {
        /// <param name="x">Мнимая часть i (в Navisworks — Rotation3D.A).</param>
        /// <param name="y">Мнимая часть j (Rotation3D.B).</param>
        /// <param name="z">Мнимая часть k (Rotation3D.C).</param>
        /// <param name="w">Скалярная часть (Rotation3D.D).</param>
        public Rotation(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public double W { get; }

        /// <summary>Отсутствие поворота.</summary>
        public static Rotation Identity
        {
            get { return new Rotation(0, 0, 0, 1); }
        }

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z + W * W); }
        }

        /// <summary>
        /// Поворот вокруг произвольной оси. Нужен в основном тестам:
        /// эталонные повороты читаются глазами, а сырые кватернионы — нет.
        /// </summary>
        public static Rotation FromAxisAngle(Vector3 axis, double angleRadians)
        {
            Vector3 unit = axis.Normalized();
            double half = angleRadians / 2.0;
            double sin = Math.Sin(half);

            return new Rotation(unit.X * sin, unit.Y * sin, unit.Z * sin, Math.Cos(half));
        }

        public Rotation Normalized()
        {
            double length = Length;

            if (length <= double.Epsilon)
            {
                throw new InvalidOperationException("Нулевой кватернион нормализовать нельзя.");
            }

            return new Rotation(X / length, Y / length, Z / length, W / length);
        }

        /// <summary>
        /// Поворачивает вектор. Формула Родрига в кватернионной записи:
        /// v' = v + 2w(q x v) + 2(q x (q x v)), где q — мнимая часть.
        /// Дешевле и устойчивее, чем разворачивать кватернион в матрицу.
        /// </summary>
        public Vector3 Rotate(Vector3 vector)
        {
            Rotation q = Normalized();
            var axis = new Vector3(q.X, q.Y, q.Z);

            Vector3 first = axis.Cross(vector);
            Vector3 second = axis.Cross(first);

            return new Vector3(
                vector.X + 2.0 * (q.W * first.X + second.X),
                vector.Y + 2.0 * (q.W * first.Y + second.Y),
                vector.Z + 2.0 * (q.W * first.Z + second.Z));
        }

        public bool Equals(Rotation other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        }

        public override bool Equals(object obj)
        {
            return obj is Rotation && Equals((Rotation)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                hash = (hash * 397) ^ W.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Rotation left, Rotation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Rotation left, Rotation right)
        {
            return !left.Equals(right);
        }
    }
}
