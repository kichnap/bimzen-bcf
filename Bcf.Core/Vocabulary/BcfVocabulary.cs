using System;
using System.Collections.Generic;
using System.Linq;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Checks against the vocabulary. The values and the tables live in
    /// BcfVocabulary.g.cs, which is generated from
    /// bcf-vocabularies/bcf-extensions.json.
    ///
    /// The validation is asymmetric, and that is the point: strict on the way
    /// out (<see cref="EnsureTopicStatus"/> and its neighbours throw), lenient
    /// on the way in (<see cref="IsKnownTopicStatus"/> merely marks a value as
    /// foreign).
    ///
    /// Проверки по справочнику. Значения и таблицы лежат в BcfVocabulary.g.cs,
    /// который генерируется из bcf-vocabularies/bcf-extensions.json.
    ///
    /// Проверка асимметрична, и в этом суть: на выход строго
    /// (<see cref="EnsureTopicStatus"/> и соседи бросают исключение), на вход
    /// терпимо (<see cref="IsKnownTopicStatus"/> лишь помечает значение чужим).
    /// </summary>
    public static partial class BcfVocabulary
    {
        /// <summary>
        /// Wire values are compared strictly, case and spaces included:
        /// "In Progress" is neither "in progress" nor "InProgress".
        ///
        /// Wire-значения сравниваются строго, с учётом регистра и пробелов:
        /// "In Progress" — это не "in progress" и не "InProgress".
        /// </summary>
        private static readonly StringComparer ValueComparer = StringComparer.Ordinal;

        /// <summary>
        /// Whether the topic type belongs to the vocabulary.
        /// Принадлежит ли тип замечания справочнику.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsKnownTopicType(string value)
        {
            return Contains(TopicTypes.All, value);
        }

        /// <summary>
        /// Whether the status belongs to the vocabulary.
        /// Принадлежит ли статус справочнику.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsKnownTopicStatus(string value)
        {
            return Contains(TopicStatuses.All, value);
        }

        /// <summary>
        /// Whether the priority belongs to the vocabulary.
        /// Принадлежит ли приоритет справочнику.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsKnownPriority(string value)
        {
            return Contains(Priorities.All, value);
        }

        /// <summary>
        /// Whether the label belongs to the vocabulary.
        /// Принадлежит ли метка справочнику.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsKnownTopicLabel(string value)
        {
            return Contains(TopicLabels.All, value);
        }

        /// <summary>
        /// Whether the stage belongs to the vocabulary.
        /// Принадлежит ли стадия справочнику.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static bool IsKnownStage(string value)
        {
            return Contains(Stages.All, value);
        }

        /// <summary>
        /// The strict check of a topic type before writing.
        /// Строгая проверка типа замечания перед записью.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static void EnsureTopicType(string value)
        {
            Ensure("TopicType", value, TopicTypes.All);
        }

        /// <summary>
        /// The strict check of a status before writing.
        /// Строгая проверка статуса перед записью.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static void EnsureTopicStatus(string value)
        {
            Ensure("TopicStatus", value, TopicStatuses.All);
        }

        /// <summary>
        /// The strict check of a priority before writing.
        /// Строгая проверка приоритета перед записью.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static void EnsurePriority(string value)
        {
            Ensure("Priority", value, Priorities.All);
        }

        /// <summary>
        /// The strict check of a label before writing.
        /// Строгая проверка метки перед записью.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static void EnsureTopicLabel(string value)
        {
            Ensure("TopicLabel", value, TopicLabels.All);
        }

        /// <summary>
        /// The strict check of a stage before writing.
        /// Строгая проверка стадии перед записью.
        /// </summary>
        /// <param name="value">The value to check.</param>
        public static void EnsureStage(string value)
        {
            Ensure("Stage", value, Stages.All);
        }

        /// <summary>
        /// A clash-tool status turned into a BCF TopicStatus.
        /// Статус инструмента коллизий, превращённый в TopicStatus BCF.
        /// </summary>
        /// <param name="navisworksStatus">The clash status, "Approved" for instance.</param>
        /// <param name="overrides">
        /// Overrides from the export settings. They win over the vocabulary
        /// defaults: Approved has two readings — "verified and accepted"
        /// (Closed, the default) and "the intersection was accepted as
        /// tolerable" (Rejected).
        ///
        /// Переопределения из настроек выгрузки. Они важнее умолчаний
        /// справочника: у Approved две трактовки — «проверено и принято»
        /// (Closed, по умолчанию) и «пересечение признано допустимым»
        /// (Rejected).
        /// </param>
        /// <returns>The BCF status, or null when nothing matched.</returns>
        public static string MapNavisworksStatus(string navisworksStatus, IReadOnlyDictionary<string, string> overrides = null)
        {
            if (string.IsNullOrEmpty(navisworksStatus)) return null;

            string mapped;

            if (overrides != null && overrides.TryGetValue(navisworksStatus, out mapped) && !string.IsNullOrEmpty(mapped))
            {
                EnsureTopicStatus(mapped);
                return mapped;
            }

            return NavisworksStatusToBcf.TryGetValue(navisworksStatus, out mapped) ? mapped : null;
        }

        /// <summary>
        /// The display label for a user interface. For a foreign value it
        /// returns the value itself: the interface shows it as it is, marked as
        /// external.
        ///
        /// Подпись для интерфейса. Для чужого значения возвращает его само:
        /// интерфейс показывает его как есть, с пометкой «внешнее».
        /// </summary>
        public static string GetRussianLabel(IReadOnlyDictionary<string, string> labels, string value)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (value == null) return null;

            string label;
            return labels.TryGetValue(value, out label) ? label : value;
        }

        /// <summary>
        /// Whether the lifecycle model allows this transition between statuses.
        /// Разрешает ли модель жизненного цикла такой переход между статусами.
        /// </summary>
        /// <param name="fromStatus">The status to move from.</param>
        /// <param name="toStatus">The status to move to.</param>
        public static bool IsTransitionAllowed(string fromStatus, string toStatus)
        {
            IReadOnlyList<string> allowed;
            if (fromStatus == null || !TopicStatuses.AllowedTransitions.TryGetValue(fromStatus, out allowed))
            {
                return false;
            }

            return Contains(allowed, toStatus);
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            if (value == null) return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (ValueComparer.Equals(values[i], value)) return true;
            }

            return false;
        }

        private static void Ensure(string field, string value, IReadOnlyList<string> allowed)
        {
            if (Contains(allowed, value)) return;

            throw new BcfVocabularyException(
                "The value '" + (value ?? "<null>") + "' is not allowed for the field " + field +
                ". Allowed values: " + string.Join(", ", allowed.ToArray()) + ".")
            {
                Field = field,
                Value = value
            };
        }
    }
}
