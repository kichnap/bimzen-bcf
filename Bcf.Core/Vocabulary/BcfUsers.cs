using System;
using System.Collections.Generic;
using System.Linq;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Подготовка списка пользователей для раздела Users в справочниках архива.
    ///
    /// В BCF идентификатор пользователя — email. В Clash Detective поле
    /// «Assigned To» — произвольный текст, туда пишут и «Иванов», и «ОВ»,
    /// и пустоту. Такие значения остаются в поле AssignedTo топика как есть
    /// (иначе потеряется информация), но в объявленный список Users не попадают:
    /// он должен оставаться списком идентификаторов, а не свалкой подписей.
    /// </summary>
    public static class BcfUsers
    {
        /// <summary>
        /// Приводит значения к нижнему регистру, убирает дубликаты и сортирует.
        /// </summary>
        /// <param name="values">Автор выгрузки плюс все встреченные AssignedTo.</param>
        /// <param name="skipped">
        /// Значения, не похожие на email. Их показывает итоговый отчёт экспорта:
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
        /// Проверка «похоже на email»: ровно одна собака не с краю и точка после неё.
        /// Намеренно грубая — задача не проверить адрес, а отсечь подписи вида
        /// «Иванов (ОВ)» до того, как кириллица попадёт в файл.
        /// </summary>
        public static bool LooksLikeEmail(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;

            foreach (char c in value)
            {
                // Кириллица и любые не-ASCII символы в идентификаторе недопустимы
                if (c > 127 || char.IsWhiteSpace(c)) return false;
            }

            int at = value.IndexOf('@');
            if (at <= 0 || at != value.LastIndexOf('@') || at == value.Length - 1) return false;

            int dot = value.IndexOf('.', at + 1);
            return dot > at + 1 && dot < value.Length - 1;
        }
    }
}
