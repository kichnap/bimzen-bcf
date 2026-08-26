using System;
using System.Globalization;

namespace Bcf.Core.Geometry
{
    /// <summary>
    /// A point or a direction in a right-handed coordinate system with Z up —
    /// the system BCF requires. A type of its own rather than a host type:
    /// this library must know nothing about any host.
    ///
    /// Точка или направление в правой системе координат с осью Z вверх —
    /// такой её требует BCF. Собственный тип, а не тип хоста: библиотека
    /// не должна ничего знать про хост.
    /// </summary>
    public struct Vector3 : IEquatable<Vector3>
    {
        /// <summary>
        /// Creates a vector from its three components.
        /// Создаёт вектор из трёх составляющих.
        /// </summary>
        /// <param name="x">The X component.</param>
        /// <param name="y">The Y component.</param>
        /// <param name="z">The Z component.</param>
        public Vector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>The X component. / Составляющая X.</summary>
        public double X { get; }

        /// <summary>The Y component. / Составляющая Y.</summary>
        public double Y { get; }

        /// <summary>The Z component. / Составляющая Z.</summary>
        public double Z { get; }

        /// <summary>The zero vector. / Нулевой вектор.</summary>
        public static Vector3 Zero
        {
            get { return new Vector3(0, 0, 0); }
        }

        /// <summary>The length of the vector. / Длина вектора.</summary>
        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z); }
        }

        /// <summary>
        /// The unit vector of the same direction. The direction and up vectors
        /// in BCF have to be normalised: some receiving tools scale the camera
        /// otherwise and the view drifts away.
        ///
        /// Единичный вектор того же направления. Векторы направления и «вверх»
        /// в BCF обязаны быть нормализованными: часть приёмников иначе
        /// масштабирует камеру, и вид уезжает.
        /// </summary>
        public Vector3 Normalized()
        {
            double length = Length;

            if (length <= double.Epsilon)
            {
                throw new InvalidOperationException("A zero vector cannot be normalised.");
            }

            return new Vector3(X / length, Y / length, Z / length);
        }

        /// <summary>
        /// The vector multiplied by a factor.
        /// Вектор, умноженный на множитель.
        /// </summary>
        /// <param name="factor">The multiplier.</param>
        public Vector3 Scaled(double factor)
        {
            return new Vector3(X * factor, Y * factor, Z * factor);
        }

        /// <summary>
        /// The dot product with another vector.
        /// Скалярное произведение с другим вектором.
        /// </summary>
        /// <param name="other">The other vector.</param>
        public double Dot(Vector3 other)
        {
            return X * other.X + Y * other.Y + Z * other.Z;
        }

        /// <summary>
        /// The cross product with another vector.
        /// Векторное произведение с другим вектором.
        /// </summary>
        /// <param name="other">The other vector.</param>
        public Vector3 Cross(Vector3 other)
        {
            return new Vector3(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        /// <summary>
        /// Component-wise equality.
        /// Равенство по составляющим.
        /// </summary>
        /// <param name="other">The vector to compare with.</param>
        public bool Equals(Vector3 other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Vector3 && Equals((Vector3)obj);
        }

        /// <inheritdoc />
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

        /// <summary>
        /// Component-wise equality.
        /// Равенство по составляющим.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        public static bool operator ==(Vector3 left, Vector3 right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Component-wise inequality.
        /// Неравенство по составляющим.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        public static bool operator !=(Vector3 left, Vector3 right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// A diagnostic representation. Invariant culture, otherwise a locale
        /// that uses a comma as the decimal separator shows commas here.
        ///
        /// Диагностическое представление. Инвариантная культура — иначе
        /// на локали с запятой в роли разделителя здесь будут запятые.
        /// </summary>
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "({0}, {1}, {2})", X, Y, Z);
        }
    }
}
