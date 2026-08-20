using System;

namespace Bcf.Core.Model
{
    /// <summary>Комментарий к замечанию. Источник при выгрузке — ClashResult.Comments.</summary>
    public class BcfComment
    {
        public Guid Guid { get; set; }

        public DateTimeOffset Date { get; set; }

        public string Author { get; set; }

        /// <summary>
        /// Текст. В схеме 3.0 поле необязательное, в 2.1 — обязательное,
        /// поэтому при выгрузке в 2.1 пустой комментарий пропускается.
        /// </summary>
        public string Text { get; set; }

        /// <summary>Точка зрения, к которой привязан комментарий.</summary>
        public Guid? ViewpointGuid { get; set; }

        public DateTimeOffset? ModifiedDate { get; set; }

        public string ModifiedAuthor { get; set; }
    }
}
