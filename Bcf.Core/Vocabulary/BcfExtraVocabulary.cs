using System;
using System.Collections.Generic;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Значения справочника, которых нет в нашем, но которые оказались
    /// в записываемом архиве.
    ///
    /// Появляются при обновлении существующего файла: замечание из BIMcollab
    /// со статусом «На согласовании» переносится в новый архив как есть, и
    /// строгий приёмник вправе спросить, откуда этот статус. Объявление
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

        public IReadOnlyList<string> TopicTypes { get { return _topicTypes; } }

        public IReadOnlyList<string> TopicStatuses { get { return _topicStatuses; } }

        public IReadOnlyList<string> Priorities { get { return _priorities; } }

        public IReadOnlyList<string> TopicLabels { get { return _topicLabels; } }

        public IReadOnlyList<string> Stages { get { return _stages; } }

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

        public void AddTopicType(string value)
        {
            Add(_topicTypes, value, BcfVocabulary.TopicTypes.All);
        }

        public void AddTopicStatus(string value)
        {
            Add(_topicStatuses, value, BcfVocabulary.TopicStatuses.All);
        }

        public void AddPriority(string value)
        {
            Add(_priorities, value, BcfVocabulary.Priorities.All);
        }

        public void AddTopicLabel(string value)
        {
            Add(_topicLabels, value, BcfVocabulary.TopicLabels.All);
        }

        public void AddStage(string value)
        {
            Add(_stages, value, BcfVocabulary.Stages.All);
        }

        /// <summary>Справочник плюс чужие значения, в порядке справочника.</summary>
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
