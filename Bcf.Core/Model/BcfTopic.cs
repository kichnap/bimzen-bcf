using System;
using System.Collections.Generic;

namespace Bcf.Core.Model
{
    /// <summary>
    /// A topic. The model follows the buildingSMART specification rather than
    /// the shape of the zip archive: the standard describes the same entities
    /// twice — XML in a file and JSON over HTTP — and an HTTP client can stand
    /// next to the ZIP serializer over this very model.
    ///
    /// The set of fields follows 3.0 as the richer version; the 2.1 serializer
    /// drops what 2.1 does not have and says so in its report.
    ///
    /// Замечание. Модель построена по спецификации buildingSMART, а не по
    /// структуре zip-архива: те же сущности описаны в стандарте дважды — XML
    /// в файле и JSON по HTTP, — и рядом с ZIP-сериализатором может встать
    /// HTTP-клиент над этой же моделью.
    ///
    /// Состав полей — по 3.0 как по более полной версии; сериализатор 2.1
    /// отбрасывает то, чего в 2.1 нет, и пишет об этом в отчёт.
    /// </summary>
    public class BcfTopic
    {
        /// <summary>
        /// The topic identifier, stable between exports — see
        /// <see cref="Bcf.Core.Conversion.StableTopicKey"/>. Clash identifiers
        /// of a clash-detection tool are regenerated when a test is reset, so
        /// they cannot be relied upon.
        ///
        /// Идентификатор замечания, устойчивый между выгрузками — см.
        /// <see cref="Bcf.Core.Conversion.StableTopicKey"/>. Идентификаторы
        /// коллизий пересоздаются при сбросе проверки, полагаться на них нельзя.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// The identifier assigned by a server — a human-readable number.
        /// Introduced in 3.0 and dropped when writing 2.1.
        ///
        /// Идентификатор, присвоенный сервером, — человекочитаемый номер.
        /// Появился в 3.0, при выгрузке в 2.1 отбрасывается.
        /// </summary>
        public string ServerAssignedId { get; set; }

        /// <summary>
        /// The topic type. For clash export it is Clash.
        /// Тип замечания. Для выгрузки коллизий — Clash.
        /// </summary>
        public string TopicType { get; set; }

        /// <summary>
        /// The status, resolved through the mapping table of the export settings.
        /// Статус по таблице отображения из настроек выгрузки.
        /// </summary>
        public string TopicStatus { get; set; }

        /// <summary>
        /// The title: the clash or group name plus the name of the test.
        /// Заголовок: имя коллизии или группы плюс имя проверки.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The priority, a value of the vocabulary.
        /// Приоритет, значение из справочника.
        /// </summary>
        public string Priority { get; set; }

        /// <summary>
        /// An ordinal number. Marked obsolete in 3.0 but still allowed by the schema.
        /// Порядковый номер. В 3.0 помечен устаревшим, но схемой разрешён.
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// Labels: discipline, nature, origin. `Auto` marks everything produced
        /// automatically.
        ///
        /// Метки: дисциплина, характер, источник. `Auto` помечает всё, что
        /// создано автоматически.
        /// </summary>
        public IList<string> Labels { get; } = new List<string>();

        /// <summary>
        /// External links. In 2.1 they are written as repeated ReferenceLink elements.
        /// Внешние ссылки. В 2.1 пишутся повторяющимися элементами ReferenceLink.
        /// </summary>
        public IList<string> ReferenceLinks { get; } = new List<string>();

        /// <summary>
        /// When the topic was created.
        /// Когда замечание было создано.
        /// </summary>
        public DateTimeOffset CreationDate { get; set; }

        /// <summary>
        /// Who created the topic; the schema requires this field.
        /// Кто создал замечание; схема требует это поле.
        /// </summary>
        public string CreationAuthor { get; set; }

        /// <summary>
        /// When the topic was last modified.
        /// Когда замечание правилось в последний раз.
        /// </summary>
        public DateTimeOffset? ModifiedDate { get; set; }

        /// <summary>
        /// Who modified the topic last.
        /// Кто правил замечание последним.
        /// </summary>
        public string ModifiedAuthor { get; set; }

        /// <summary>
        /// The date the issue is due.
        /// Срок, к которому замечание должно быть закрыто.
        /// </summary>
        public DateTimeOffset? DueDate { get; set; }

        /// <summary>
        /// The assignee. In BCF this is an email address; in a clash-detection
        /// tool it is free text. The value is preserved as it is, and only
        /// something that looks like an address reaches the Users list of the
        /// vocabulary declaration — see <see cref="Bcf.Core.Vocabulary.BcfUsers"/>.
        ///
        /// Исполнитель. В BCF это адрес электронной почты, в инструменте
        /// коллизий — произвольный текст. Значение сохраняется как есть,
        /// а в список Users объявления справочников попадает только похожее
        /// на адрес — см. <see cref="Bcf.Core.Vocabulary.BcfUsers"/>.
        /// </summary>
        public string AssignedTo { get; set; }

        /// <summary>
        /// The project stage, a value of the vocabulary.
        /// Стадия проекта, значение из справочника.
        /// </summary>
        public string Stage { get; set; }

        /// <summary>
        /// The description: clash type, distance, coordinates, level and grid
        /// location, element paths.
        ///
        /// Описание: тип коллизии, расстояние, координаты, уровень и ось сетки,
        /// пути элементов.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The comments on this topic.
        /// Комментарии к этому замечанию.
        /// </summary>
        public IList<BcfComment> Comments { get; } = new List<BcfComment>();

        /// <summary>
        /// The viewpoints of this topic.
        /// Точки зрения этого замечания.
        /// </summary>
        public IList<BcfViewpoint> Viewpoints { get; } = new List<BcfViewpoint>();

        /// <summary>
        /// The models the topic refers to — the Header section.
        /// Модели, к которым относится замечание, — секция Header.
        /// </summary>
        public IList<BcfFile> Files { get; } = new List<BcfFile>();

        /// <summary>
        /// Topics related to this one.
        /// Замечания, связанные с этим.
        /// </summary>
        public IList<Guid> RelatedTopics { get; } = new List<Guid>();

        /// <summary>
        /// Field values that were not found in the vocabulary while reading
        /// someone else's archive. The key is the field name, the value is what
        /// stood in the file. A topic carrying such values is not discarded:
        /// the standard does not fix the vocabularies, and a file from
        /// BIMcollab or Revizto legitimately arrives with its own.
        ///
        /// Значения полей, не найденные в справочнике при чтении чужого архива.
        /// Ключ — имя поля, значение — то, что стояло в файле. Замечание
        /// с такими значениями не отбрасывается: стандарт словари не фиксирует,
        /// и файл из BIMcollab или Revizto законно приходит со своими.
        /// </summary>
        public IDictionary<string, string> ExternalValues { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Markup elements this model does not keep: attachments, document
        /// references, IFC snippets. Filled in when reading someone else's
        /// archive.
        ///
        /// They exist so that an update of an existing file knows what it would
        /// deprive the user of: a topic carrying such data is not rewritten but
        /// carried over as it is.
        ///
        /// Элементы разметки, которых эта модель не хранит: вложения, ссылки
        /// на документы, фрагменты IFC. Заполняется при чтении чужого архива.
        ///
        /// Нужны, чтобы обновление существующего файла знало, чего оно лишит
        /// пользователя: замечание с такими данными не переписывается,
        /// а переносится как есть.
        /// </summary>
        public IList<string> UnsupportedData { get; } = new List<string>();
    }
}
