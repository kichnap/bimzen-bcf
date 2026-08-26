using System;

namespace Bcf.Core.Geometry
{
    /// <summary>
    /// A rotation quaternion. BIM applications commonly store the orientation
    /// of a view this way, while BCF asks for two vectors — the viewing
    /// direction and the up vector. The conversion is done by
    /// <see cref="Bcf.Core.Conversion.CameraConverter"/>.
    ///
    /// Кватернион поворота. BIM-приложения обычно хранят ориентацию вида
    /// именно так, а BCF просит два вектора — направление взгляда и «вверх».
    /// Перевод делает <see cref="Bcf.Core.Conversion.CameraConverter"/>.
    /// </summary>
    public struct Rotation : IEquatable<Rotation>
    {
        /// <summary>
        /// Creates a quaternion from its four components.
        /// Создаёт кватернион из четырёх составляющих.
        /// </summary>
        /// <param name="x">The imaginary i part.</param>
        /// <param name="y">The imaginary j part.</param>
        /// <param name="z">The imaginary k part.</param>
        /// <param name="w">The scalar part.</param>
        public Rotation(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        /// <summary>The imaginary i part. / Мнимая часть i.</summary>
        public double X { get; }

        /// <summary>The imaginary j part. / Мнимая часть j.</summary>
        public double Y { get; }

        /// <summary>The imaginary k part. / Мнимая часть k.</summary>
        public double Z { get; }

        /// <summary>The scalar part. / Скалярная часть.</summary>
        public double W { get; }

        /// <summary>No rotation at all. / Отсутствие поворота.</summary>
        public static Rotation Identity
        {
            get { return new Rotation(0, 0, 0, 1); }
        }

        /// <summary>The length of the quaternion. / Длина кватерниона.</summary>
        public double Length
        {
            get { return Math.Sqrt(X * X + Y * Y + Z * Z + W * W); }
        }

        /// <summary>
        /// A rotation around an arbitrary axis. Mostly needed by tests:
        /// a rotation stated as an axis and an angle can be read by eye,
        /// a raw quaternion cannot.
        ///
        /// Поворот вокруг произвольной оси. Нужен в основном тестам: поворот,
        /// заданный осью и углом, читается глазами, а сырой кватернион — нет.
        /// </summary>
        /// <param name="axis">The axis to rotate around; it is normalised here.</param>
        /// <param name="angleRadians">The angle in radians.</param>
        public static Rotation FromAxisAngle(Vector3 axis, double angleRadians)
        {
            Vector3 unit = axis.Normalized();
            double half = angleRadians / 2.0;
            double sin = Math.Sin(half);

            return new Rotation(unit.X * sin, unit.Y * sin, unit.Z * sin, Math.Cos(half));
        }

        /// <summary>
        /// The quaternion scaled to unit length.
        /// Кватернион, приведённый к единичной длине.
        /// </summary>
        public Rotation Normalized()
        {
            double length = Length;

            if (length <= double.Epsilon)
            {
                throw new InvalidOperationException("A zero quaternion cannot be normalised.");
            }

            return new Rotation(X / length, Y / length, Z / length, W / length);
        }

        /// <summary>
        /// Rotates a vector. Rodrigues' formula in quaternion form:
        /// v' = v + 2w(q × v) + 2(q × (q × v)), where q is the imaginary part.
        /// Cheaper and more stable than expanding the quaternion into a matrix.
        ///
        /// Поворачивает вектор. Формула Родрига в кватернионной записи:
        /// v' = v + 2w(q × v) + 2(q × (q × v)), где q — мнимая часть. Дешевле
        /// и устойчивее, чем разворачивать кватернион в матрицу.
        /// </summary>
        /// <param name="vector">The vector to rotate.</param>
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

        /// <summary>
        /// Component-wise equality.
        /// Равенство по составляющим.
        /// </summary>
        /// <param name="other">The quaternion to compare with.</param>
        public bool Equals(Rotation other)
        {
            return X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z) && W.Equals(other.W);
        }

        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            return obj is Rotation && Equals((Rotation)obj);
        }

        /// <inheritdoc />
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

        /// <summary>
        /// Component-wise equality.
        /// Равенство по составляющим.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        public static bool operator ==(Rotation left, Rotation right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Component-wise inequality.
        /// Неравенство по составляющим.
        /// </summary>
        /// <param name="left">The left operand.</param>
        /// <param name="right">The right operand.</param>
        public static bool operator !=(Rotation left, Rotation right)
        {
            return !left.Equals(right);
        }
    }
}
