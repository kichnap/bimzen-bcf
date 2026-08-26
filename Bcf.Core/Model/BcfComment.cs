using System;

namespace Bcf.Core.Model
{
    /// <summary>
    /// A comment on a topic. When exporting clashes it comes from the comments
    /// the clash-detection tool keeps on a clash result.
    ///
    /// Комментарий к замечанию. При выгрузке коллизий берётся из комментариев,
    /// которые инструмент поиска коллизий хранит на результате проверки.
    /// </summary>
    public class BcfComment
    {
        /// <summary>
        /// The comment identifier, unique within the archive.
        /// Идентификатор комментария, уникальный в пределах архива.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// When the comment was written.
        /// Когда комментарий был написан.
        /// </summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>
        /// The author, an email address as far as the specification is concerned.
        /// Автор; по спецификации это адрес электронной почты.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// The text. BCF 3.0 makes the element optional and 2.1 makes it
        /// mandatory, so an empty comment is skipped when writing 2.1.
        ///
        /// Текст. В схеме 3.0 поле необязательное, в 2.1 — обязательное,
        /// поэтому при выгрузке в 2.1 пустой комментарий пропускается.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// The viewpoint this comment is attached to, if any.
        /// Точка зрения, к которой привязан комментарий, если она есть.
        /// </summary>
        public Guid? ViewpointGuid { get; set; }

        /// <summary>
        /// When the comment was last edited.
        /// Когда комментарий правился в последний раз.
        /// </summary>
        public DateTimeOffset? ModifiedDate { get; set; }

        /// <summary>
        /// Who edited the comment last.
        /// Кто правил комментарий последним.
        /// </summary>
        public string ModifiedAuthor { get; set; }
    }
}
