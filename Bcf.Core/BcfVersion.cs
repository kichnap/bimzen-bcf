using System;

namespace Bcf.Core
{
    /// <summary>Версия формата BCF.</summary>
    public enum BcfVersion
    {
        /// <summary>2.1 — для сторонних приёмников, где 3.0 поддержан не везде.</summary>
        Bcf21,

        /// <summary>3.0 — основной формат.</summary>
        Bcf30
    }

    public static class BcfVersionExtensions
    {
        /// <summary>Значение атрибута VersionId в файле bcf.version.</summary>
        public static string ToVersionId(this BcfVersion version)
        {
            switch (version)
            {
                case BcfVersion.Bcf21: return "2.1";
                case BcfVersion.Bcf30: return "3.0";
                default: throw new ArgumentOutOfRangeException(nameof(version), version, null);
            }
        }

        /// <summary>Разбор значения VersionId из файла bcf.version.</summary>
        public static BcfVersion Parse(string versionId)
        {
            switch (versionId)
            {
                case "2.1": return BcfVersion.Bcf21;
                case "3.0": return BcfVersion.Bcf30;
                default:
                    throw new NotSupportedException(
                        "Версия BCF '" + (versionId ?? "<null>") + "' не поддерживается: плагин работает с 2.1 и 3.0.");
            }
        }
    }
}
