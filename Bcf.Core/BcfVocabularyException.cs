using System;

namespace Bcf.Core
{
    /// <summary>
    /// A value outside the vocabulary found while producing outgoing data.
    ///
    /// It is thrown on the way out only — building a topic, writing a file,
    /// posting to an API. On the way in, while reading someone else's
    /// `.bcfzip`, unknown values are kept as they are: the standard does not
    /// fix the vocabularies, and a file from BIMcollab or Revizto carrying its
    /// own statuses is entirely legitimate.
    ///
    /// Значение вне справочника, найденное при формировании исходящих данных.
    ///
    /// Кидается только на выход — сборка замечания, запись файла, отправка
    /// в API. На вход, при чтении чужого `.bcfzip`, незнакомые значения
    /// сохраняются как есть: стандарт словари не фиксирует, и файл
    /// из BIMcollab или Revizto со своими статусами полностью законен.
    /// </summary>
    public class BcfVocabularyException : Exception
    {
        /// <summary>
        /// Creates the exception without a message.
        /// Создаёт исключение без сообщения.
        /// </summary>
        public BcfVocabularyException()
        {
        }

        /// <summary>
        /// Creates the exception with a message.
        /// Создаёт исключение с сообщением.
        /// </summary>
        /// <param name="message">What exactly is wrong with the value.</param>
        public BcfVocabularyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Creates the exception with a message and an inner exception.
        /// Создаёт исключение с сообщением и вложенным исключением.
        /// </summary>
        /// <param name="message">What exactly is wrong with the value.</param>
        /// <param name="innerException">The exception that caused this one.</param>
        public BcfVocabularyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// The field the unknown value was found in: TopicStatus, Priority, and so on.
        /// Поле, в котором обнаружено неизвестное значение: TopicStatus, Priority и прочие.
        /// </summary>
        public string Field { get; set; }

        /// <summary>
        /// The value itself.
        /// Само значение.
        /// </summary>
        public string Value { get; set; }
    }
}
