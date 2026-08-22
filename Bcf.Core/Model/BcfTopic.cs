using System;
using System.Collections.Generic;

namespace Bcf.Core.Model
{
    /// <summary>
    /// Замечание. Модель построена по спецификации buildingSMART, а не по
    /// структуре zip-архива: те же сущности описаны в стандарте дважды — XML
    /// в файле и JSON по HTTP, — и на втором этапе рядом с ZIP-сериализатором
    /// встанет HTTP-клиент над этой же моделью.
    ///
    /// Состав полей — по 3.0 как по более полной версии; сериализатор 2.1
    /// отбрасывает то, чего в 2.1 нет, и пишет об этом в отчёт.
    /// </summary>
    public class BcfTopic
    {
        /// <summary>
        /// Идентификатор замечания. Стабильный между выгрузками — см.
        /// <see cref="Bcf.Core.Conversion.StableTopicKey"/>: идентификаторы
        /// коллизий Navisworks пересоздаются при Reset теста, полагаться на них
        /// нельзя.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Идентификатор, присвоенный сервером (человекочитаемый номер).
        /// Появился в 3.0; при выгрузке в 2.1 отбрасывается.
        /// </summary>
        public string ServerAssignedId { get; set; }

        /// <summary>Тип замечания. Для выгрузки из Clash Detective — Clash.</summary>
        public string TopicType { get; set; }

        /// <summary>Статус по таблице маппинга из настроек экспорта.</summary>
        public string TopicStatus { get; set; }

        /// <summary>Заголовок: имя коллизии или группы плюс имя теста.</summary>
        public string Title { get; set; }

        public string Priority { get; set; }

        /// <summary>Порядковый номер. В 3.0 помечен как устаревший, но схемой разрешён.</summary>
        public int? Index { get; set; }

        /// <summary>Метки: дисциплина, характер, источник. Auto ставится на всё автоматическое.</summary>
        public IList<string> Labels { get; } = new List<string>();

        /// <summary>Внешние ссылки. В 2.1 пишутся как повторяющиеся элементы ReferenceLink.</summary>
        public IList<string> ReferenceLinks { get; } = new List<string>();

        public DateTimeOffset CreationDate { get; set; }

        public string CreationAuthor { get; set; }

        public DateTimeOffset? ModifiedDate { get; set; }

        public string ModifiedAuthor { get; set; }

        public DateTimeOffset? DueDate { get; set; }

        /// <summary>
        /// Исполнитель. В BCF это email, в Clash Detective — произвольный текст;
        /// значение сохраняется как есть, а в список Users справочника попадает
        /// только похожее на адрес (см. <see cref="Bcf.Core.Vocabulary.BcfUsers"/>).
        /// </summary>
        public string AssignedTo { get; set; }

        public string Stage { get; set; }

        /// <summary>Описание: тип коллизии, расстояние, координаты, уровень и ось сетки, пути элементов.</summary>
        public string Description { get; set; }

        public IList<BcfComment> Comments { get; } = new List<BcfComment>();

        public IList<BcfViewpoint> Viewpoints { get; } = new List<BcfViewpoint>();

        /// <summary>Модели, к которым относится замечание (секция Header).</summary>
        public IList<BcfFile> Files { get; } = new List<BcfFile>();

        public IList<Guid> RelatedTopics { get; } = new List<Guid>();

        /// <summary>
        /// Значения полей, не найденные в справочнике при чтении чужого архива.
        /// Ключ — имя поля, значение — как было в файле. Топик с такими
        /// значениями не отбрасывается: словари стандартом не зафиксированы,
        /// и файл из BIMcollab или Revizto законно приходит со своими.
        /// </summary>
        public IDictionary<string, string> ExternalValues { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Элементы markup, которых эта модель не хранит: вложения, ссылки
        /// на документы, фрагменты IFC. Заполняется при чтении чужого архива.
        ///
        /// Нужны, чтобы обновление существующего файла знало, чего оно лишит
        /// пользователя: замечание с такими данными не переписывается, а
        /// переносится в новый архив как есть.
        /// </summary>
        public IList<string> UnsupportedData { get; } = new List<string>();
    }
}
