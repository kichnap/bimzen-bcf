using System;
using System.Collections.Generic;
using System.Linq;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Preparing the list of users for the Users section of the vocabulary
    /// declaration.
    ///
    /// In BCF a user identifier is an email address. In a clash-detection tool
    /// the "Assigned To" field is free text: people put a surname there, or a
    /// discipline code, or nothing at all. Such values stay in the topic's
    /// AssignedTo field exactly as they are — dropping them would lose
    /// information — but they do not reach the declared Users list, which has
    /// to remain a list of identifiers rather than a heap of captions.
    ///
    /// Подготовка списка пользователей для раздела Users в объявлении
    /// справочников.
    ///
    /// В BCF идентификатор пользователя — адрес почты. В инструменте коллизий
    /// поле «Assigned To» — произвольный текст: туда пишут и фамилию, и код
    /// дисциплины, и ничего. Такие значения остаются в поле AssignedTo
    /// замечания как есть — отбросив их, мы потеряли бы сведения, — но
    /// в объявленный список Users не попадают: он должен оставаться списком
    /// идентификаторов, а не свалкой подписей.
    /// </summary>
    public static class BcfUsers
    {
        /// <summary>
        /// Lower-cases the values, drops duplicates and sorts what is left.
        /// Приводит значения к нижнему регистру, убирает повторы и сортирует.
        /// </summary>
        /// <param name="values">The export author plus every AssignedTo encountered.</param>
        /// <param name="skipped">
        /// The values that do not look like an address. The export report shows
        /// them: the user has to know that the mapping of assignees is
        /// incomplete.
        ///
        /// Значения, не похожие на адрес. Отчёт выгрузки их показывает:
        /// пользователь должен знать, что сопоставление исполнителей неполное.
        /// </param>
        public static IReadOnlyList<string> Normalize(IEnumerable<string> values, out IReadOnlyList<string> skipped)
        {
            var accepted = new SortedSet<string>(StringComparer.Ordinal);
            var rejected = new List<string>();

            if (values != null)
            {
                foreach (string raw in values)
                {
                    if (string.IsNullOrWhiteSpace(raw)) continue;

                    string value = raw.Trim();

                    if (LooksLikeEmail(value))
                    {
                        accepted.Add(value.ToLowerInvariant());
                    }
                    else if (!rejected.Contains(value, StringComparer.Ordinal))
                    {
                        rejected.Add(value);
                    }
                }
            }

            skipped = rejected;
            return accepted.ToList();
        }

        /// <summary>
        /// The "looks like an email" check: exactly one at-sign, not at either
        /// end, with a dot after it. Deliberately crude — the job is not to
        /// validate an address but to stop a caption such as "Ivanov (HVAC)"
        /// before non-ASCII text reaches the file.
        ///
        /// Проверка «похоже на адрес»: ровно одна собака, не с краю, и точка
        /// после неё. Намеренно грубая — задача не проверить адрес, а отсечь
        /// подпись вида «Иванов (ОВ)» до того, как не-ASCII текст попадёт
        /// в файл.
        /// </summary>
        /// <param name="value">The value to look at.</param>
        public static bool LooksLikeEmail(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            foreach (char c in value)
            {
                // Non-ASCII characters have no place in an identifier
                if (c > 127 || char.IsWhiteSpace(c)) return false;
            }

            int at = value.IndexOf('@');
            if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;

            int dot = value.IndexOf('.', at + 1);
            return dot > at + 1 && dot < value.Length - 1;
        }
    }
}
