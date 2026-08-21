using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Собирает замечание из одной коллизии или из их группы.
    /// Знает только модель BCF и настройки — ни про Navisworks, ни про формат
    /// файла здесь ничего нет.
    /// </summary>
    internal sealed class ClashTopicBuilder
    {
        private readonly BcfExportSettings _settings;
        private readonly ClashDocumentInfo _document;
        private readonly BcfExportResult _result;
        private readonly DateTimeOffset _exportTime;
        private readonly IReadOnlyDictionary<string, string> _statusMapping;

        public ClashTopicBuilder(
            BcfExportSettings settings,
            ClashDocumentInfo document,
            BcfExportResult result,
            DateTimeOffset exportTime)
        {
            _settings = settings;
            _document = document;
            _result = result;
            _exportTime = exportTime;
            _statusMapping = new Dictionary<string, string>(settings.StatusMapping, StringComparer.Ordinal);
        }

        /// <summary>
        /// Строит замечание.
        /// </summary>
        /// <param name="stableKey">Устойчивый ключ — из него выводятся идентификаторы комментариев.</param>
        /// <param name="topicGuid">
        /// Идентификатор замечания: ранее выданный либо детерминированный.
        /// Идентификаторы коллизий Navisworks пересоздаются при Reset теста,
        /// поэтому опираться на них нельзя.
        /// </param>
        public BcfTopic Build(string stableKey, Guid topicGuid, string title, IReadOnlyList<ClashItem> clashes)
        {
            if (clashes == null || clashes.Count == 0)
            {
                throw new ArgumentException("Нет коллизий для замечания.", nameof(clashes));
            }

            ClashItem first = clashes[0];

            var topic = new BcfTopic
            {
                Guid = topicGuid,
                TopicType = _settings.TopicType,
                TopicStatus = ResolveStatus(clashes),
                Title = title,
                Priority = _settings.Priority,
                Stage = _settings.Stage,
                CreationDate = _exportTime,
                CreationAuthor = _settings.Author,
                Description = BuildDescription(clashes)
            };

            if (_settings.CarryAssignedTo)
            {
                topic.AssignedTo = clashes
                    .Select(c => c.AssignedTo)
                    .FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            }

            foreach (string label in Labels(first))
            {
                if (!topic.Labels.Contains(label)) topic.Labels.Add(label);
            }

            foreach (ClashModelInfo model in _document.Models)
            {
                topic.Files.Add(new BcfFile
                {
                    Filename = model.FileName,
                    Date = model.Date,
                    IsExternal = true
                });
            }

            if (_settings.IncludeComments)
            {
                AddComments(topic, clashes);
            }

            return topic;
        }

        /// <summary>Элементы всех коллизий замечания, без повторов.</summary>
        public static IReadOnlyList<BcfComponent> Components(IReadOnlyList<ClashItem> clashes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var components = new List<BcfComponent>();

            foreach (ClashElementInfo element in clashes.SelectMany(c => c.Elements))
            {
                if (string.IsNullOrWhiteSpace(element.IfcGuid)) continue;
                if (!seen.Add(element.IfcGuid)) continue;

                components.Add(new BcfComponent(element.IfcGuid)
                {
                    OriginatingSystem = "Navisworks",
                    AuthoringToolId = element.ElementId
                });
            }

            return components;
        }

        /// <summary>
        /// Статус замечания для группы коллизий. Побеждает самый «открытый»
        /// по модели жизненного цикла: группа считается закрытой только тогда,
        /// когда закрыта целиком, иначе часть работы потеряется из виду.
        /// </summary>
        private string ResolveStatus(IReadOnlyList<ClashItem> clashes)
        {
            string best = null;
            int bestRank = int.MaxValue;

            foreach (ClashItem clash in clashes)
            {
                string status = BcfVocabulary.MapNavisworksStatus(clash.Status, _statusMapping);

                if (status == null)
                {
                    _result.Warn(
                        "Статус Clash Detective '" + (clash.Status ?? "<пусто>") +
                        "' не сопоставлен со справочником, использован " + BcfVocabulary.TopicStatuses.Default + ".");

                    status = BcfVocabulary.TopicStatuses.Default;
                }

                int rank = IndexOf(status);
                if (rank >= bestRank) continue;

                bestRank = rank;
                best = status;
            }

            return best ?? BcfVocabulary.TopicStatuses.Default;
        }

        private static int IndexOf(string status)
        {
            for (int i = 0; i < BcfVocabulary.TopicStatuses.All.Count; i++)
            {
                if (string.Equals(BcfVocabulary.TopicStatuses.All[i], status, StringComparison.Ordinal)) return i;
            }

            return int.MaxValue;
        }

        private IEnumerable<string> Labels(ClashItem clash)
        {
            foreach (string label in _settings.Labels)
            {
                if (!string.IsNullOrWhiteSpace(label)) yield return label;
            }

            // Метка дисциплины выводится из имени теста по заданным правилам.
            // Правил нет — метки нет: у каждого заказчика свои имена проверок,
            // и угадывать их значит ставить неверную метку молча.
            string discipline = MatchDiscipline(clash.TestName);
            if (discipline != null) yield return discipline;
        }

        private string MatchDiscipline(string testName)
        {
            if (string.IsNullOrWhiteSpace(testName)) return null;

            foreach (DisciplineLabelRule rule in _settings.DisciplineLabelRules)
            {
                if (string.IsNullOrWhiteSpace(rule.Substring) || string.IsNullOrWhiteSpace(rule.Label)) continue;

                if (testName.IndexOf(rule.Substring, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return rule.Label;
                }
            }

            return null;
        }

        private void AddComments(BcfTopic topic, IReadOnlyList<ClashItem> clashes)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (ClashCommentInfo comment in clashes.SelectMany(c => c.Comments))
            {
                if (string.IsNullOrWhiteSpace(comment.Text)) continue;

                // В группе один и тот же комментарий часто продублирован
                // на каждой коллизии — в замечании он нужен один раз
                string key = comment.Author + "|" + comment.Date.ToString("O", CultureInfo.InvariantCulture) + "|" + comment.Text;
                if (!seen.Add(key)) continue;

                topic.Comments.Add(new BcfComment
                {
                    Guid = StableTopicKey.ToTopicGuid(StableTopicKey.Compute(new[] { topic.Guid.ToString("D"), key })),
                    Author = string.IsNullOrWhiteSpace(comment.Author) ? _settings.Author : comment.Author,
                    Date = comment.Date == default(DateTimeOffset) ? _exportTime : comment.Date,
                    Text = comment.Text
                });
            }
        }

        private string BuildDescription(IReadOnlyList<ClashItem> clashes)
        {
            var sb = new StringBuilder();
            ClashItem first = clashes[0];

            sb.Append("Проверка: ").AppendLine(first.TestName);

            if (clashes.Count > 1)
            {
                sb.Append("Коллизий в группе: ").AppendLine(clashes.Count.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                sb.Append("Коллизия: ").AppendLine(first.DisplayName);
            }

            if (!string.IsNullOrWhiteSpace(first.LevelName)) sb.Append("Уровень: ").AppendLine(first.LevelName);
            if (!string.IsNullOrWhiteSpace(first.GridLocation)) sb.Append("Оси: ").AppendLine(first.GridLocation);

            if (_settings.IncludeDistance && first.DistanceMeters.HasValue)
            {
                // Инвариантная культура: описание читает и человек, и парсер
                sb.Append("Расстояние: ").Append(BcfNumber.Format(first.DistanceMeters.Value)).AppendLine(" м");
            }

            if (first.CenterMeters.HasValue)
            {
                Vector3 center = first.CenterMeters.Value;
                sb.Append("Точка: X=").Append(BcfNumber.Format(center.X))
                  .Append(", Y=").Append(BcfNumber.Format(center.Y))
                  .Append(", Z=").Append(BcfNumber.Format(center.Z))
                  .AppendLine(" (м)");
            }

            if (_settings.IncludeElementPaths)
            {
                AppendElements(sb, clashes);
            }

            return sb.ToString().TrimEnd();
        }

        private static void AppendElements(StringBuilder sb, IReadOnlyList<ClashItem> clashes)
        {
            // У группы перечислять элементы всех коллизий бессмысленно:
            // описание разрастётся до нечитаемого. Показываем первую коллизию.
            IList<ClashElementInfo> elements = clashes[0].Elements;
            int number = 1;

            foreach (ClashElementInfo element in elements)
            {
                sb.Append("Элемент ").Append(number.ToString(CultureInfo.InvariantCulture)).Append(": ");
                sb.Append(string.IsNullOrWhiteSpace(element.Path) ? "(путь неизвестен)" : element.Path);

                if (!string.IsNullOrWhiteSpace(element.ModelFileName)) sb.Append(" — ").Append(element.ModelFileName);
                if (!string.IsNullOrWhiteSpace(element.ElementId)) sb.Append(", id ").Append(element.ElementId);
                if (element.Origin == ElementIdOrigin.None) sb.Append(" (без идентификатора IFC)");

                sb.AppendLine();
                number++;
            }
        }
    }
}
