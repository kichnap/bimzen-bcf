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
    /// Assembles a topic out of one clash or out of a group of them. It knows
    /// only the BCF model and the settings — nothing here is about Navisworks or
    /// about the file format.
    ///
    /// Собирает замечание из одной коллизии или из их группы. Знает только
    /// модель BCF и настройки — ни про Navisworks, ни про формат файла здесь
    /// ничего нет.
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
        /// Builds a topic out of one or more clashes.
        /// Строит замечание из одной или нескольких коллизий.
        /// </summary>
        /// <param name="stableKey">The stable key; comment identifiers are derived from it.</param>
        /// <param name="topicGuid">
        /// The topic identifier: issued earlier or deterministic. Clash
        /// identifiers are regenerated when a test is reset, so they cannot be
        /// relied upon.
        ///
        /// Идентификатор замечания: ранее выданный либо детерминированный.
        /// Идентификаторы коллизий пересоздаются при сбросе проверки, поэтому
        /// опираться на них нельзя.
        /// </param>
        /// <param name="title">The topic title.</param>
        /// <param name="clashes">The clashes this topic covers; at least one.</param>
        public BcfTopic Build(string stableKey, Guid topicGuid, string title, IReadOnlyList<ClashItem> clashes)
        {
            if (clashes == null || clashes.Count == 0)
            {
                throw new ArgumentException("There are no clashes for the topic.", nameof(clashes));
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

        /// <summary>
        /// A topic made from a saved view.
        ///
        /// The type is Issue and not Clash, and it carries no automatic-test
        /// label: this is what a person saw with their own eyes and recorded as
        /// a view, not the result of an automatic test. A service has to be able
        /// to tell the two apart without reading the text.
        ///
        /// Замечание из сохранённого вида.
        ///
        /// Тип — Issue, а не Clash, и без метки автопроверки: это то, что
        /// человек увидел глазами и зафиксировал видом, а не результат
        /// автоматической проверки. Сервис должен уметь различать их, не
        /// разбирая текст.
        /// </summary>
        /// <param name="topicGuid">The identifier for the topic.</param>
        /// <param name="viewpoint">The saved view to build from.</param>
        public BcfTopic BuildFromViewpoint(Guid topicGuid, SavedViewpointInfo viewpoint)
        {
            if (viewpoint == null) throw new ArgumentNullException(nameof(viewpoint));

            var topic = new BcfTopic
            {
                Guid = topicGuid,
                TopicType = _settings.SavedViewpointTopicType,
                TopicStatus = BcfVocabulary.TopicStatuses.Default,
                Title = string.IsNullOrWhiteSpace(viewpoint.Name) ? "Замечание" : viewpoint.Name,
                Priority = _settings.Priority,
                Stage = _settings.Stage,
                CreationDate = viewpoint.CreatedDate ?? _exportTime,
                CreationAuthor = _settings.Author,
                Description = BuildViewpointDescription(viewpoint)
            };

            foreach (string label in _settings.Labels)
            {
                // Auto means "an automatic test found this" — on a hand-written
                // topic the label lies, and a service builds wrong figures on it
                if (string.IsNullOrWhiteSpace(label)) continue;
                if (string.Equals(label, BcfVocabulary.TopicLabels.Auto, StringComparison.Ordinal)) continue;

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
                AddViewpointComments(topic, viewpoint);
            }

            return topic;
        }

        private static string BuildViewpointDescription(SavedViewpointInfo viewpoint)
        {
            var sb = new StringBuilder();

            sb.AppendLine("Замечание из сохранённого вида Navisworks.");

            if (!string.IsNullOrWhiteSpace(viewpoint.FolderPath))
            {
                sb.Append("Папка: ").AppendLine(viewpoint.FolderPath);
            }

            if (viewpoint.HasVisibilityOverrides)
            {
                // An important warning for the receiving tool: the author of the
                // view hid something, and opened elsewhere it will look different
                sb.AppendLine("В виде скрыта часть модели — снимок показывает то, что видел автор.");
            }

            return sb.ToString().TrimEnd();
        }

        private void AddViewpointComments(BcfTopic topic, SavedViewpointInfo viewpoint)
        {
            foreach (ClashCommentInfo comment in viewpoint.Comments)
            {
                if (string.IsNullOrWhiteSpace(comment.Text)) continue;

                string key = comment.Author + "|" + comment.Date.ToString("O", CultureInfo.InvariantCulture) + "|" + comment.Text;

                topic.Comments.Add(new BcfComment
                {
                    Guid = StableTopicKey.ToTopicGuid(StableTopicKey.Compute(new[] { topic.Guid.ToString("D"), key })),
                    Author = string.IsNullOrWhiteSpace(comment.Author) ? _settings.Author : comment.Author,
                    Date = comment.Date == default(DateTimeOffset) ? _exportTime : comment.Date,
                    Text = comment.Text
                });
            }
        }

        /// <summary>
        /// The elements of every clash of the topic, with no repeats.
        /// Элементы всех коллизий замечания, без повторов.
        /// </summary>
        /// <param name="clashes">The clashes to take the elements from.</param>
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
        /// The topic status for a group of clashes. The most "open" one by the
        /// lifecycle model wins: a group counts as closed only when it is closed
        /// entirely, or part of the work drops out of sight.
        ///
        /// Статус замечания для группы коллизий. Побеждает самый «открытый»
        /// по модели жизненного цикла: группа считается закрытой, только когда
        /// закрыта целиком, иначе часть работы пропадёт из виду.
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
                        "The Clash Detective status '" + (clash.Status ?? "<empty>") +
                        "' is not mapped to the vocabulary; " + BcfVocabulary.TopicStatuses.Default + " was used.");

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

            // The group name as a label: the exporter declares the value, here
            // it is simply added to the rest
            if (_settings.GroupNameAsLabel && !string.IsNullOrWhiteSpace(clash.GroupName))
            {
                yield return clash.GroupName.Trim();
            }

            // The discipline label is derived from the test name by the rules
            // given. No rules, no label: every client names their tests their own
            // way, and guessing means putting a wrong label on quietly.
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

                // Within a group the same comment is often repeated on every
                // clash — the topic needs it once
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

            // Per-clash topics need the group name too: without it the group
            // membership is lost entirely, and the other side filters by it
            if (!string.IsNullOrWhiteSpace(first.GroupName)) sb.Append("Группа: ").AppendLine(first.GroupName);

            if (!string.IsNullOrWhiteSpace(first.LevelName)) sb.Append("Уровень: ").AppendLine(first.LevelName);
            if (!string.IsNullOrWhiteSpace(first.GridLocation)) sb.Append("Оси: ").AppendLine(first.GridLocation);

            if (_settings.IncludeDistance && first.DistanceMeters.HasValue)
            {
                // The invariant culture: the description is read by a person and
                // by a parser alike
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
            // Listing the elements of every clash of a group makes no sense: the
            // description would grow past reading. The first clash is shown.
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
