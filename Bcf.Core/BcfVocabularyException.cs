using System;

namespace Bcf.Core
{
    /// <summary>
    /// Значение вне справочника при формировании исходящих данных.
    ///
    /// Кидается только на выход (сборка топика, запись файла, будущий POST).
    /// На вход — при чтении чужого .bcfzip — незнакомые значения сохраняются
    /// как есть: стандарт не фиксирует словари, и файл из BIMcollab или Revizto
    /// со своими статусами полностью законен.
    /// </summary>
    public class BcfVocabularyException : Exception
    {
        public BcfVocabularyException()
        {
        }

        public BcfVocabularyException(string message)
            : base(message)
        {
        }

        public BcfVocabularyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>Поле, в котором обнаружено неизвестное значение (TopicStatus, Priority, ...).</summary>
        public string Field { get; set; }

        /// <summary>Само значение.</summary>
        public string Value { get; set; }
    }
}
