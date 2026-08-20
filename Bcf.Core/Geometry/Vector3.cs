using System;
using System.Globalization;

namespace Bcf.Core.Geometry
{
    /// <summary>
    /// Точка или направление в правой системе координат с осью Z вверх —
    /// такой её требует BCF. Собственный тип, а не тип Navisworks: Bcf.Core
    /// не должен ничего знать про хост.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        public static Vector3 Zero
        {
            get { return new Vector3(0, 0, 0); }
        }

        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        /// <summary>
        /// Единичный вектор того же направления. Векторы направления и «вверх»
        /// в BCF обязаны быть нормализованными: часть приёмников иначе
        /// масштабирует камеру и вид уезжает.
        /// </summary>
        public Vector3 Normalized()
        {
            double length = Length;

            if (length <= double.Epsilon)
            {
                throw new InvalidOperationException("Нулевой вектор нормализовать нельзя.");
            }

            return new Vector3(X / length, Y / length, Z / length);
        }

        public Vector3 Scaled(double factor)
        {
            return new Vector3(X * factor, Y * factor, Z * factor);
        }

        public double Dot(Vector3 other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        public Vector3 Cross(Vector3 other)
        {
            return new Vector3(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        public bool Equals(Vector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        public override bool Equals(object obj)
        {
            return obj is Vector3 && Equals((Vector3)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = X.GetHashCode();
                hash = (hash * 397) ^ Y.GetHashCode();
                hash = (hash * 397) ^ Z.GetHashCode();
                return hash;
            }
        }

        public static bool operator ==(Vector3 left, Vector3 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Vector3 left, Vector3 right)
        {
            return !left.Equals(right);
        }

        /// <summary>Диагностическое представление. Инвариантная культура — иначе на русской локали здесь будут запятые.</summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X, Y, Z);
        }
    }
}
