using System;

namespace Bcf.Core
{
    /// <summary>
    /// The version of the BCF format.
    /// Версия формата BCF.
    /// </summary>
    public enum BcfVersion
    {
        /// <summary>
        /// 2.0 — read only. Archives in this version still turn up; they are
        /// read as far as 2.0 and 2.1 agree, but nothing is ever written in it.
        ///
        /// 2.0 — только чтение. Архивы этой версии до сих пор встречаются;
        /// они читаются в той мере, в какой 2.0 совпадает с 2.1, но записать
        /// в ней нельзя ничего.
        /// </summary>
        Bcf20,

        /// <summary>
        /// 2.1 — for receiving tools where 3.0 is not supported everywhere yet.
        /// 2.1 — для приёмников, где 3.0 поддержан не везде.
        /// </summary>
        Bcf21,

        /// <summary>
        /// 3.0 — the primary format.
        /// 3.0 — основной формат.
        /// </summary>
        Bcf30
    }

    /// <summary>
    /// Conversion between <see cref="BcfVersion"/> and the string the
    /// <c>bcf.version</c> file carries.
    ///
    /// Перевод между <see cref="BcfVersion"/> и строкой, которую несёт файл
    /// <c>bcf.version</c>.
    /// </summary>
    public static class BcfVersionExtensions
    {
        /// <summary>
        /// The value of the VersionId attribute in bcf.version.
        /// Значение атрибута VersionId в файле bcf.version.
        /// </summary>
        /// <param name="version">The version to write.</param>
        public static string ToVersionId(this BcfVersion version)
        {
            switch (version)
            {
                case BcfVersion.Bcf20: return "2.0";
                case BcfVersion.Bcf21: return "2.1";
                case BcfVersion.Bcf30: return "3.0";
                default: throw new ArgumentOutOfRangeException(nameof(version), version, null);
            }
        }

        /// <summary>
        /// Parses the VersionId value read from bcf.version.
        /// Разбирает значение VersionId, прочитанное из bcf.version.
        /// </summary>
        /// <param name="versionId">The attribute value: "2.0", "2.1" or "3.0".</param>
        public static BcfVersion Parse(string versionId)
        {
            switch (versionId)
            {
                case "2.0": return BcfVersion.Bcf20;
                case "2.1": return BcfVersion.Bcf21;
                case "3.0": return BcfVersion.Bcf30;
                default:
                    throw new NotSupportedException(
                        "BCF version '" + (versionId ?? "<null>") + "' is not supported: this library reads 2.0, 2.1 and 3.0.");
            }
        }
    }
}
