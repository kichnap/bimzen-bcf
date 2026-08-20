using System;
using System.Collections.Generic;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Итог чтения архива: замечания плюс всё, что стоит показать пользователю.
    /// </summary>
    public class BcfReadResult
    {
        private readonly List<string> _warnings = new List<string>();
        private readonly List<BcfExternalValue> _externalValues = new List<BcfExternalValue>();

        /// <summary>Версия, объявленная в bcf.version.</summary>
        public BcfVersion Version { get; internal set; } = BcfVersion.Bcf30;

        /// <summary>Проект из project.bcfp, если он есть.</summary>
        public BcfProject Project { get; internal set; }

        public IList<BcfTopic> Topics { get; } = new List<BcfTopic>();

        public IReadOnlyList<string> Warnings
        {
            get { return _warnings; }
        }

        /// <summary>
        /// Значения, которых нет в справочнике. Не ошибка: стандарт не фиксирует
        /// словари, и файл из BIMcollab, Revizto или Solibri законно приходит
        /// со своими статусами. Значения сохраняются как есть, а пользователь
        /// видит их списком и может сопоставить вручную.
        /// </summary>
        public IReadOnlyList<BcfExternalValue> ExternalValues
        {
            get { return _externalValues; }
        }

        internal void Warn(string message)
        {
            if (!string.IsNullOrEmpty(message) && !_warnings.Contains(message))
            {
                _warnings.Add(message);
            }
        }

        internal void AddExternalValue(string field, string value, Guid topicGuid)
        {
            foreach (BcfExternalValue existing in _externalValues)
            {
                if (existing.Field == field && existing.Value == value)
                {
                    existing.Count++;
                    return;
                }
            }

            _externalValues.Add(new BcfExternalValue
            {
                Field = field,
                Value = value,
                Count = 1,
                FirstTopic = topicGuid
            });
        }
    }

    /// <summary>Незнакомое значение справочника, встреченное при чтении.</summary>
    public class BcfExternalValue
    {
        /// <summary>Поле: TopicStatus, TopicType, Priority, Stage или Label.</summary>
        public string Field { get; internal set; }

        /// <summary>Значение как оно записано в файле.</summary>
        public string Value { get; internal set; }

        /// <summary>Сколько раз встретилось.</summary>
        public int Count { get; internal set; }

        /// <summary>Первый топик с этим значением — чтобы было куда посмотреть.</summary>
        public Guid FirstTopic { get; internal set; }

        public override string ToString()
        {
            return Field + " = '" + Value + "' (" + Count + ")";
        }
    }
}
