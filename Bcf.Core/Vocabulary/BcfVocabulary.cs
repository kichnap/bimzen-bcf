using System;
using System.Collections.Generic;
using System.Linq;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Проверки значений справочника. Значения и таблицы — в BcfVocabulary.g.cs,
    /// он генерируется из bcf-vocabularies/bcf-extensions.json.
    ///
    /// Валидация асимметрична и это принципиально:
    /// на выход — строго (<see cref="EnsureTopicStatus"/> и соседи бросают исключение),
    /// на вход — терпимо (<see cref="IsKnownTopicStatus"/> лишь помечает значение чужим).
    /// </summary>
    public static partial class BcfVocabulary
    {
        /// <summary>
        /// Сравнение wire-значений строгое, с регистром и пробелами:
        /// "In Progress" не равно "in progress" и не равно "InProgress".
        /// </summary>
        private static readonly StringComparer ValueComparer = StringComparer.Ordinal;

        public static bool IsKnownTopicType(string value)
        {
            return Contains(TopicTypes.All, value);
        }

        public static bool IsKnownTopicStatus(string value)
        {
            return Contains(TopicStatuses.All, value);
        }

        public static bool IsKnownPriority(string value)
        {
            return Contains(Priorities.All, value);
        }

        public static bool IsKnownTopicLabel(string value)
        {
            return Contains(TopicLabels.All, value);
        }

        public static bool IsKnownStage(string value)
        {
            return Contains(Stages.All, value);
        }

        /// <summary>Строгая проверка типа замечания перед записью.</summary>
        public static void EnsureTopicType(string value)
        {
            Ensure("TopicType", value, TopicTypes.All);
        }

        /// <summary>Строгая проверка статуса перед записью.</summary>
        public static void EnsureTopicStatus(string value)
        {
            Ensure("TopicStatus", value, TopicStatuses.All);
        }

        /// <summary>Строгая проверка приоритета перед записью.</summary>
        public static void EnsurePriority(string value)
        {
            Ensure("Priority", value, Priorities.All);
        }

        /// <summary>Строгая проверка метки перед записью.</summary>
        public static void EnsureTopicLabel(string value)
        {
            Ensure("TopicLabel", value, TopicLabels.All);
        }

        /// <summary>Строгая проверка стадии перед записью.</summary>
        public static void EnsureStage(string value)
        {
            Ensure("Stage", value, Stages.All);
        }

        /// <summary>
        /// Статус Clash Detective в TopicStatus BCF.
        /// </summary>
        /// <param name="navisworksStatus">Значение ClashResultStatus, например "Approved".</param>
        /// <param name="overrides">
        /// Переопределения из диалога экспорта. Приоритетнее дефолтов справочника:
        /// у Approved две трактовки — «проверено и принято» (Closed, дефолт)
        /// и «пересечение признано допустимым» (Rejected).
        /// </param>
        /// <returns>Статус BCF либо null, если сопоставить не удалось.</returns>
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
        /// Русская подпись для UI. Для незнакомого (внешнего) значения возвращает
        /// его само: в интерфейсе оно показывается как есть, с пометкой «внешнее».
        /// </summary>
        public static string GetRussianLabel(IReadOnlyDictionary<string, string> labels, string value)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (value == null) return null;

            string label;
            return labels.TryGetValue(value, out label) ? label : value;
        }

        /// <summary>Разрешён ли переход между статусами по модели жизненного цикла.</summary>
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
                "Значение '" + (value ?? "<null>") + "' недопустимо для поля " + field +
                ". Допустимые значения: " + string.Join(", ", allowed.ToArray()) + ".")
            {
                Field = field,
                Value = value
            };
        }
    }
}
