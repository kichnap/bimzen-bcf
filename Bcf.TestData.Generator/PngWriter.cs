using System;
using System.IO;
using System.IO.Compression;

namespace Bcf.TestData.Generator
{
    /// <summary>
    /// A minimal PNG encoder.
    ///
    /// The reference archives travel to other teams as import fixtures, so the
    /// snapshots inside them have to be real pictures any viewer opens, not a
    /// stub of eight signature bytes. System.Drawing is deliberately left out:
    /// the generator has to work outside Windows too, and the result has to be
    /// reproducible byte for byte.
    ///
    /// Минимальный кодировщик PNG.
    ///
    /// Эталонные архивы уходят другим командам как фикстуры для импорта,
    /// поэтому снимки в них должны быть настоящими картинками, которые откроет
    /// любой просмотрщик, а не заглушкой из восьми байт подписи. System.Drawing
    /// намеренно не используется: генератор должен работать и вне Windows,
    /// а результат — быть побайтово воспроизводимым.
    /// </summary>
    internal static class PngWriter
    {
        public static byte[] Create(int width, int height, byte red, byte green, byte blue)
        {
            byte[] raw = RawScanlines(width, height, red, green, blue);

            using (var png = new MemoryStream())
            {
                png.Write(Signature, 0, Signature.Length);

                WriteChunk(png, "IHDR", Header(width, height));
                WriteChunk(png, "IDAT", ZlibCompress(raw));
                WriteChunk(png, "IEND", Array.Empty<byte>());

                return png.ToArray();
            }
        }

        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        private static byte[] Header(int width, int height)
        {
            var header = new byte[13];

            WriteBigEndian(header, 0, (uint)width);
            WriteBigEndian(header, 4, (uint)height);

            header[8] = 8;  // bits per channel
            header[9] = 2;  // colour type: RGB with no palette
            header[10] = 0; // compression: deflate
            header[11] = 0; // filtering: the standard one
            header[12] = 0; // no interlacing

            return header;
        }

        /// <summary>
        /// The rows of the image: each is preceded by a filter byte. The
        /// picture is a vertical gradient, so that a snapshot does not look like
        /// an empty square.
        ///
        /// Строки изображения: перед каждой идёт байт фильтра. Картинка —
        /// вертикальный градиент, чтобы снимок не выглядел пустым квадратом.
        /// </summary>
        private static byte[] RawScanlines(int width, int height, byte red, byte green, byte blue)
        {
            var raw = new byte[height * (1 + width * 3)];
            int position = 0;

            for (int y = 0; y < height; y++)
            {
                raw[position++] = 0; // the None filter

                double shade = 0.55 + 0.45 * y / Math.Max(1, height - 1);

                for (int x = 0; x < width; x++)
                {
                    raw[position++] = (byte)(red * shade);
                    raw[position++] = (byte)(green * shade);
                    raw[position++] = (byte)(blue * shade);
                }
            }

            return raw;
        }

        private static byte[] ZlibCompress(byte[] data)
        {
            using (var buffer = new MemoryStream())
            {
                // The zlib header: deflate, a 32K window, no dictionary
                buffer.WriteByte(0x78);
                buffer.WriteByte(0x9C);

                using (var deflate = new DeflateStream(buffer, CompressionLevel.Optimal, leaveOpen: true))
                {
                    deflate.Write(data, 0, data.Length);
                }

                byte[] checksum = new byte[4];
                WriteBigEndian(checksum, 0, Adler32(data));
                buffer.Write(checksum, 0, checksum.Length);

                return buffer.ToArray();
            }
        }

        private static void WriteChunk(Stream stream, string type, byte[] data)
        {
            var length = new byte[4];
            WriteBigEndian(length, 0, (uint)data.Length);
            stream.Write(length, 0, length.Length);

            var typeBytes = new byte[4];
            for (int i = 0; i < 4; i++) typeBytes[i] = (byte)type[i];

            stream.Write(typeBytes, 0, typeBytes.Length);
            stream.Write(data, 0, data.Length);

            uint crc = Crc32(typeBytes, data);
            var crcBytes = new byte[4];
            WriteBigEndian(crcBytes, 0, crc);
            stream.Write(crcBytes, 0, crcBytes.Length);
        }

        private static void WriteBigEndian(byte[] target, int offset, uint value)
        {
            target[offset] = (byte)(value >> 24);
            target[offset + 1] = (byte)(value >> 16);
            target[offset + 2] = (byte)(value >> 8);
            target[offset + 3] = (byte)value;
        }

        private static uint Adler32(byte[] data)
        {
            uint a = 1, b = 0;

            foreach (byte value in data)
            {
                a = (a + value) % 65521;
                b = (b + a) % 65521;
            }

            return (b << 16) | a;
        }

        private static readonly uint[] CrcTable = BuildCrcTable();

        private static uint[] BuildCrcTable()
        {
            var table = new uint[256];

            for (uint i = 0; i < 256; i++)
            {
                uint value = i;

                for (int bit = 0; bit < 8; bit++)
                {
                    value = (value & 1) != 0 ? 0xEDB88320u ^ (value >> 1) : value >> 1;
                }

                table[i] = value;
            }

            return table;
        }

        private static uint Crc32(byte[] first, byte[] second)
        {
            uint crc = 0xFFFFFFFF;

            foreach (byte value in first) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);
            foreach (byte value in second) crc = CrcTable[(crc ^ value) & 0xFF] ^ (crc >> 8);

            return crc ^ 0xFFFFFFFF;
        }
    }
}
