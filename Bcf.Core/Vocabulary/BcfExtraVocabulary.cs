using System;
using System.Collections.Generic;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Vocabulary values absent from ours that nevertheless ended up in the
    /// archive being written.
    ///
    /// They appear when an existing file is updated: a topic from another tool
    /// carrying a status of its own is carried into the new archive as it is,
    /// and a strict receiver may fairly ask where that status came from.
    /// Declaring it in extensions answers the question: a file we write must
    /// declare everything it contains.
    ///
    /// Значения справочника, которых нет в нашем, но которые оказались
    /// в записываемом архиве.
    ///
    /// Появляются при обновлении существующего файла: замечание из чужого
    /// инструмента со своим статусом переносится в новый архив как есть,
    /// и строгий приёмник вправе спросить, откуда этот статус. Объявление
    /// в extensions отвечает на этот вопрос: файл, который мы пишем, обязан
    /// объявлять всё, что в нём есть.
    /// </summary>
    public class BcfExtraVocabulary
    {
        private readonly List<string> _topicTypes = new List<string>();
        private readonly List<string> _topicStatuses = new List<string>();
        private readonly List<string> _priorities = new List<string>();
        private readonly List<string> _topicLabels = new List<string>();
        private readonly List<string> _stages = new List<string>();

        /// <summary>Foreign topic types. / Чужие типы замечаний.</summary>
        public IReadOnlyList<string> TopicTypes { get { return _topicTypes; } }

        /// <summary>Foreign statuses. / Чужие статусы.</summary>
        public IReadOnlyList<string> TopicStatuses { get { return _topicStatuses; } }

        /// <summary>Foreign priorities. / Чужие приоритеты.</summary>
        public IReadOnlyList<string> Priorities { get { return _priorities; } }

        /// <summary>Foreign labels. / Чужие метки.</summary>
        public IReadOnlyList<string> TopicLabels { get { return _topicLabels; } }

        /// <summary>Foreign stages. / Чужие стадии.</summary>
        public IReadOnlyList<string> Stages { get { return _stages; } }

        /// <summary>
        /// True when nothing foreign was met and extensions can be written
        /// from the vocabulary alone.
        ///
        /// Истина, когда ничего чужого не встретилось и extensions можно
        /// писать по одному справочнику.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                return _topicTypes.Count == 0
                       && _topicStatuses.Count == 0
                       && _priorities.Count == 0
                       && _topicLabels.Count == 0
                       && _stages.Count == 0;
            }
        }

        /// <summary>
        /// Remembers a topic type unless the vocabulary already has it.
        /// Запоминает тип замечания, если его ещё нет в справочнике.
        /// </summary>
        /// <param name="value">The value as it stood in the archive.</param>
        public void AddTopicType(string value)
        {
            Add(_topicTypes, value, BcfVocabulary.TopicTypes.All);
        }

        /// <summary>
        /// Remembers a status unless the vocabulary already has it.
        /// Запоминает статус, если его ещё нет в справочнике.
        /// </summary>
        /// <param name="value">The value as it stood in the archive.</param>
        public void AddTopicStatus(string value)
        {
            Add(_topicStatuses, value, BcfVocabulary.TopicStatuses.All);
        }

        /// <summary>
        /// Remembers a priority unless the vocabulary already has it.
        /// Запоминает приоритет, если его ещё нет в справочнике.
        /// </summary>
        /// <param name="value">The value as it stood in the archive.</param>
        public void AddPriority(string value)
        {
            Add(_priorities, value, BcfVocabulary.Priorities.All);
        }

        /// <summary>
        /// Remembers a label unless the vocabulary already has it.
        /// Запоминает метку, если её ещё нет в справочнике.
        /// </summary>
        /// <param name="value">The value as it stood in the archive.</param>
        public void AddTopicLabel(string value)
        {
            Add(_topicLabels, value, BcfVocabulary.TopicLabels.All);
        }

        /// <summary>
        /// Remembers a stage unless the vocabulary already has it.
        /// Запоминает стадию, если её ещё нет в справочнике.
        /// </summary>
        /// <param name="value">The value as it stood in the archive.</param>
        public void AddStage(string value)
        {
            Add(_stages, value, BcfVocabulary.Stages.All);
        }

        /// <summary>
        /// The vocabulary followed by the foreign values, in vocabulary order.
        /// Справочник, а следом чужие значения, в порядке справочника.
        /// </summary>
        /// <param name="known">The values of the vocabulary.</param>
        /// <param name="extra">The foreign values met in the archive.</param>
        public static IReadOnlyList<string> Combine(IReadOnlyList<string> known, IReadOnlyList<string> extra)
        {
            if (extra == null || extra.Count == 0) return known;

            var combined = new List<string>(known);
            combined.AddRange(extra);

            return combined;
        }

        private static void Add(List<string> target, string value, IReadOnlyList<string> known)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            string trimmed = value.Trim();

            foreach (string existing in known)
            {
                if (string.Equals(existing, trimmed, StringComparison.Ordinal)) return;
            }

            if (target.Contains(trimmed)) return;

            target.Add(trimmed);
        }
    }
}
