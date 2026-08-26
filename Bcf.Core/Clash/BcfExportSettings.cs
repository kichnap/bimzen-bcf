using System;
using System.Collections.Generic;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// How clashes are folded into topics.
    /// Как коллизии складываются в замечания.
    /// </summary>
    public enum ClashGroupingMode
    {
        /// <summary>
        /// One Clash Detective group makes one topic. The default mode.
        /// Одна группа Clash Detective — одно замечание. Режим по умолчанию.
        /// </summary>
        GroupPerTopic,

        /// <summary>
        /// Every clash makes a topic of its own. On large sets this gives a file
        /// nobody can lift.
        ///
        /// Каждая коллизия — отдельное замечание. На больших наборах даёт
        /// неподъёмный файл.
        /// </summary>
        ClashPerTopic,

        /// <summary>
        /// A topic per level or zone — by the nearest level of the document grid.
        /// Замечание на уровень или зону — по ближайшему уровню сетки документа.
        /// </summary>
        LevelPerTopic
    }

    /// <summary>
    /// What to do when the export file already exists.
    /// Что делать, если файл выгрузки уже существует.
    /// </summary>
    public enum BcfUpdateMode
    {
        /// <summary>
        /// Overwrite the whole file. The default behaviour.
        /// Перезаписать файл целиком. Поведение по умолчанию.
        /// </summary>
        Overwrite,

        /// <summary>
        /// Add only the new topics and leave the existing ones entirely alone.
        /// The safest mode: everything the receiving tool added to the file —
        /// statuses, comments, attachments — stays byte for byte.
        ///
        /// Добавить только новые замечания, существующие не трогать вовсе.
        /// Самый безопасный режим: всё, что появилось в файле у приёмника —
        /// статусы, комментарии, вложения — остаётся байт в байт.
        /// </summary>
        AppendNew,

        /// <summary>
        /// Add the new topics and update the existing ones from Navisworks.
        /// A topic where the receiving tool left something the model does not
        /// hold is not rewritten: losing someone else's data quietly is worse
        /// than not updating.
        ///
        /// Добавить новые и обновить существующие данными из Navisworks.
        /// Замечание, в котором приёмник оставил то, чего нет в модели,
        /// не переписывается: молча терять чужие данные хуже, чем не обновить.
        /// </summary>
        UpdateAndAppend
    }

    /// <summary>
    /// The export settings — a plain serializable object rather than property
    /// of a dialog.
    ///
    /// A dialog only fills it in; the exporter only takes it. The second kind of
    /// consumer — an agent exporting clashes on a schedule — has no window at
    /// all: the same decisions reach it as a job file. Had the parameters lived
    /// in a form, the export could not have been reused, and a second
    /// implementation of the same settings would have appeared.
    ///
    /// Настройки выгрузки — простой сериализуемый объект, а не собственность
    /// диалога.
    ///
    /// Диалог его только заполняет, экспортёр только принимает. У второго рода
    /// потребителей — агента, выгружающего коллизии по расписанию, — окна нет
    /// вовсе: те же решения приходят к нему файлом задания. Живи параметры
    /// в форме, выгрузку нельзя было бы переиспользовать, и появилась бы вторая
    /// реализация тех же настроек.
    /// </summary>
    public class BcfExportSettings
    {
        /// <summary>
        /// The format version. 3.0 by default, 2.1 as a switchable option.
        /// Версия формата. 3.0 по умолчанию, 2.1 — переключаемая опция.
        /// </summary>
        public BcfVersion Version { get; set; } = BcfVersion.Bcf30;

        /// <summary>
        /// The identifiers of the selected clash tests. Empty means all of them.
        /// Идентификаторы выбранных проверок. Пусто — значит все.
        /// </summary>
        public IList<string> SelectedTestIds { get; set; } = new List<string>();

        /// <summary>
        /// Which Clash Detective statuses to export. New and Active by default:
        /// a coordinator usually has no use for the resolved and closed ones.
        ///
        /// Какие статусы Clash Detective выгружать. По умолчанию New и Active:
        /// разобранные и закрытые коллизии координатору обычно не нужны.
        /// </summary>
        public IList<string> IncludedClashStatuses { get; set; } = new List<string> { "New", "Active" };

        /// <summary>
        /// How clashes are folded into topics.
        /// Как коллизии складываются в замечания.
        /// </summary>
        public ClashGroupingMode Grouping { get; set; } = ClashGroupingMode.GroupPerTopic;

        /// <summary>
        /// Whether snapshots are captured — the slowest part of an export.
        /// Снимать ли изображения — самая медленная часть выгрузки.
        /// </summary>
        public bool IncludeSnapshots { get; set; } = true;

        /// <summary>
        /// The snapshot width in pixels.
        /// Ширина снимка в пикселях.
        /// </summary>
        public int SnapshotWidth { get; set; } = 800;

        /// <summary>
        /// The snapshot height in pixels.
        /// Высота снимка в пикселях.
        /// </summary>
        public int SnapshotHeight { get; set; } = 600;

        /// <summary>
        /// How to capture: the built-in Navisworks way, or the custom one, with
        /// the camera aimed and the surroundings cut back.
        ///
        /// Как снимать: встроенным способом Navisworks или своим, с наведением
        /// камеры и обрезкой окружения.
        /// </summary>
        public SnapshotMode SnapshotMode { get; set; } = SnapshotMode.Custom;

        /// <summary>
        /// What to do with the surroundings of a clash in the custom mode. A
        /// section box and translucent surroundings by default: the snapshot
        /// then shows both what collided and where exactly. Chosen from runs on
        /// real projects.
        ///
        /// Что делать с окружением коллизии в своём режиме. По умолчанию бокс
        /// и полупрозрачное окружение: на снимке видно и что столкнулось,
        /// и где именно. Выбрано по итогам прогонов на реальных проектах.
        /// </summary>
        public SnapshotIsolation SnapshotIsolation { get; set; } = SnapshotIsolation.SectionBoxAndTransparentSurroundings;

        /// <summary>
        /// The margin around the clash bounds when cutting back, in metres.
        /// Поле вокруг габаритов коллизии при обрезке, метры.
        /// </summary>
        public double SnapshotBoxMarginMeters { get; set; } = 2.5;

        /// <summary>
        /// The time budget for drawing a frame, in seconds. Navisworks draws a
        /// scene gradually: without a budget the frame comes back before the
        /// geometry has loaded, and the snapshot holds nothing but background.
        ///
        /// Бюджет времени на отрисовку кадра, секунды. Navisworks рисует сцену
        /// постепенно: без бюджета кадр возвращается раньше, чем загрузилась
        /// геометрия, и на снимке остаётся один фон.
        /// </summary>
        public double SnapshotTimeBudgetSeconds { get; set; } = 5.0;

        /// <summary>
        /// Whether to draw the highlight over the selected elements.
        /// Рисовать ли подсветку выделенных элементов.
        /// </summary>
        public bool SnapshotIncludeOverlay { get; set; } = true;

        /// <summary>
        /// The limit on the number of snapshots. Zero means no limit. Capturing
        /// an image is the slowest operation of the export, and across thousands
        /// of clashes people cap it deliberately.
        ///
        /// Предел числа снимков. Ноль — без ограничения. Снятие изображения —
        /// самая медленная операция выгрузки, и на тысячах коллизий её
        /// осознанно ограничивают.
        /// </summary>
        public int MaxSnapshots { get; set; }

        /// <summary>
        /// Overrides for the status table. Empty means the vocabulary defaults
        /// are taken. The decision about Approved lives here too: Closed or
        /// Rejected.
        ///
        /// Переопределения таблицы статусов. Пусто — берутся умолчания
        /// справочника. Здесь же живёт решение по Approved: Closed или
        /// Rejected.
        /// </summary>
        public IDictionary<string, string> StatusMapping { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// The type given to topics made from clashes.
        /// Тип, который получают замечания из коллизий.
        /// </summary>
        public string TopicType { get; set; } = BcfVocabulary.TopicTypes.Default;

        /// <summary>
        /// The priority given to every topic.
        /// Приоритет, который получает каждое замечание.
        /// </summary>
        public string Priority { get; set; } = BcfVocabulary.Priorities.Default;

        /// <summary>
        /// The project stage recorded in every topic.
        /// Стадия проекта, записываемая в каждое замечание.
        /// </summary>
        public string Stage { get; set; } = BcfVocabulary.Stages.Default;

        /// <summary>
        /// The labels put on every topic. Auto by default: it lets a service
        /// tell automatic clashes from hand-written topics without reading the
        /// text.
        ///
        /// Метки на каждое замечание. Auto по умолчанию: она позволяет сервису
        /// отличать автоматические коллизии от ручных замечаний, не разбирая
        /// текст.
        /// </summary>
        public IList<string> Labels { get; set; } = new List<string> { BcfVocabulary.TopicLabels.Auto };

        /// <summary>
        /// The rules for the discipline label: a substring in a test name maps
        /// to a label. Empty by default — every client names their tests their
        /// own way and guessing is not allowed: nothing matched means no label.
        ///
        /// Правила метки дисциплины: подстрока в имени проверки → метка.
        /// По умолчанию пусто — у каждого заказчика свои имена проверок,
        /// и угадывать их нельзя: не сопоставилось, значит метки нет.
        /// </summary>
        public IList<DisciplineLabelRule> DisciplineLabelRules { get; set; } = new List<DisciplineLabelRule>();

        /// <summary>
        /// The author of the topics — an email address.
        /// Автор замечаний — email.
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Whether a group topic gets a viewpoint per clash of the group. A flat
        /// list of components does not preserve the pairs — an element taking
        /// part in three clashes appears in it once — while a viewpoint does:
        /// it holds exactly the two components of one clash.
        ///
        /// No snapshot is captured for such viewpoints: a frame costs about a
        /// second, and the viewpoint itself is a couple of kilobytes of XML.
        ///
        /// Добавлять ли в групповое замечание точку зрения на каждую коллизию
        /// группы. Плоский список компонентов пары не сохраняет — элемент,
        /// участвующий в трёх коллизиях, лежит в нём один раз, — а точка зрения
        /// сохраняет: в ней ровно два компонента одной коллизии.
        ///
        /// Снимок у таких точек зрения не снимается: кадр стоит около секунды,
        /// а сама точка зрения — пара килобайт XML.
        /// </summary>
        public bool ViewpointPerClash { get; set; } = true;

        /// <summary>
        /// Whether topics that fell into one Clash Detective group are tied
        /// together through RelatedTopics. It earns its keep in the
        /// topic-per-clash mode: otherwise the group membership shows only in
        /// the text of the description.
        ///
        /// Связывать ли через RelatedTopics замечания, попавшие в одну группу
        /// Clash Detective. Имеет смысл в режиме «замечание на коллизию»:
        /// иначе принадлежность к группе выражена только текстом описания.
        /// </summary>
        public bool LinkGroupTopics { get; set; } = true;

        /// <summary>
        /// Whether the group name becomes a label of the topic. No by default:
        /// labels are filters in a receiving tool, and three hundred grid lines
        /// turn a filter into a rubbish heap. It is switched on when filtering by
        /// group is needed in the receiving tool itself; the values are then
        /// declared in the vocabulary of the archive.
        ///
        /// Ставить ли имя группы меткой замечания. По умолчанию нет: метки
        /// в приёмнике — это фильтры, и триста осей превращают фильтр в свалку.
        /// Включают, когда фильтровать по группе нужно именно в приёмнике;
        /// значения при этом объявляются в справочнике архива.
        /// </summary>
        public bool GroupNameAsLabel { get; set; }

        /// <summary>
        /// Whether Assigned To is carried over from Clash Detective.
        /// Переносить ли Assigned To из Clash Detective.
        /// </summary>
        public bool CarryAssignedTo { get; set; } = true;

        /// <summary>
        /// Whether comments are carried over from the source.
        /// Переносить ли комментарии из источника.
        /// </summary>
        public bool IncludeComments { get; set; } = true;

        /// <summary>
        /// Whether the distance and the penetration depth go into the description.
        /// Включать ли в описание расстояние и глубину проникновения.
        /// </summary>
        public bool IncludeDistance { get; set; } = true;

        /// <summary>
        /// Whether the element paths in the model tree go into the description.
        /// Включать ли в описание пути элементов в дереве модели.
        /// </summary>
        public bool IncludeElementPaths { get; set; } = true;

        /// <summary>
        /// Whether saved views are exported as topics of their own. These are
        /// the issues clash logic cannot produce: a device turned the wrong way
        /// round, a pipe crossing the middle of a room.
        ///
        /// Выгружать ли сохранённые виды отдельными замечаниями. Это замечания,
        /// которых не даёт логика коллизий: прибор повёрнут не той стороной,
        /// труба посреди помещения.
        /// </summary>
        public bool IncludeSavedViewpoints { get; set; }

        /// <summary>
        /// The identifiers of the selected views. Empty means all of them.
        /// Идентификаторы выбранных видов. Пусто — значит все.
        /// </summary>
        public IList<string> SelectedViewpointIds { get; set; } = new List<string>();

        /// <summary>
        /// The type given to topics made from saved views. Issue by default
        /// rather than Clash: this is a hand-written issue, not the result of an
        /// automatic test.
        ///
        /// Тип замечаний, созданных из сохранённых видов. По умолчанию Issue,
        /// а не Clash: это ручное замечание, а не результат автопроверки.
        /// </summary>
        public string SavedViewpointTopicType { get; set; } = BcfVocabulary.TopicTypes.Issue;

        /// <summary>
        /// The project name for project.bcfp.
        /// Имя проекта для project.bcfp.
        /// </summary>
        public string ProjectName { get; set; }

        /// <summary>
        /// The project identifier. Empty — the host derives one from the document path.
        /// Идентификатор проекта. Пусто — хост выводит его из пути документа.
        /// </summary>
        public string ProjectId { get; set; }

        /// <summary>
        /// Where the archive is written.
        /// Куда пишется архив.
        /// </summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// What to do when the file already exists. Overwrite by default: that
        /// is how the first edition of the export behaved, and changing the
        /// behaviour quietly, without asking, is not on.
        ///
        /// Что делать, если файл уже существует. По умолчанию перезаписать: так
        /// вело себя первое издание выгрузки, а менять поведение молча,
        /// не спросив, нельзя.
        /// </summary>
        public BcfUpdateMode UpdateMode { get; set; } = BcfUpdateMode.Overwrite;

        /// <summary>
        /// Whether an update keeps the status, the assignee and the due date as
        /// the receiving tool changed them. Yes by default: a repeat export must
        /// not wipe out the work a coordinator did in BIMcollab or Solibri. It is
        /// switched off when Clash Detective is the source of truth for statuses.
        ///
        /// Сохранять ли при обновлении статус, исполнителя и срок, изменённые
        /// в приёмнике. По умолчанию да: работу координатора в BIMcollab или
        /// Solibri повторная выгрузка затирать не должна. Выключают, когда
        /// источник истины по статусам — Clash Detective.
        /// </summary>
        public bool KeepReceiverChanges { get; set; } = true;

        /// <summary>
        /// The time of the export: it goes into the CreationDate of the topics
        /// and into the timestamps of the archive entries. Unset means the
        /// current moment. Two callers set it: an agent that needs the timestamp
        /// of its job, and the generator of reference files that needs
        /// reproducibility.
        ///
        /// Время выгрузки: попадает в CreationDate замечаний и в метки времени
        /// записей архива. Не задано — берётся текущий момент. Задают его двое:
        /// агент, которому нужна отметка времени задания, и генератор эталонных
        /// файлов, которому нужна воспроизводимость.
        /// </summary>
        public DateTimeOffset? ExportTime { get; set; }

        /// <summary>
        /// A copy of the settings — a dialog edits this and not the original.
        /// Копия настроек — диалог правит её, а не оригинал.
        /// </summary>
        public BcfExportSettings Clone()
        {
            return new BcfExportSettings
            {
                Version = Version,
                SelectedTestIds = new List<string>(SelectedTestIds),
                IncludedClashStatuses = new List<string>(IncludedClashStatuses),
                Grouping = Grouping,
                IncludeSnapshots = IncludeSnapshots,
                SnapshotWidth = SnapshotWidth,
                SnapshotHeight = SnapshotHeight,
                SnapshotMode = SnapshotMode,
                SnapshotIsolation = SnapshotIsolation,
                SnapshotBoxMarginMeters = SnapshotBoxMarginMeters,
                SnapshotTimeBudgetSeconds = SnapshotTimeBudgetSeconds,
                SnapshotIncludeOverlay = SnapshotIncludeOverlay,
                MaxSnapshots = MaxSnapshots,
                StatusMapping = new Dictionary<string, string>(StatusMapping, StringComparer.Ordinal),
                TopicType = TopicType,
                Priority = Priority,
                Stage = Stage,
                Labels = new List<string>(Labels),
                DisciplineLabelRules = new List<DisciplineLabelRule>(DisciplineLabelRules),
                Author = Author,
                ViewpointPerClash = ViewpointPerClash,
                LinkGroupTopics = LinkGroupTopics,
                GroupNameAsLabel = GroupNameAsLabel,
                CarryAssignedTo = CarryAssignedTo,
                IncludeComments = IncludeComments,
                IncludeDistance = IncludeDistance,
                IncludeElementPaths = IncludeElementPaths,
                IncludeSavedViewpoints = IncludeSavedViewpoints,
                SelectedViewpointIds = new List<string>(SelectedViewpointIds),
                SavedViewpointTopicType = SavedViewpointTopicType,
                ProjectName = ProjectName,
                ProjectId = ProjectId,
                OutputPath = OutputPath,
                UpdateMode = UpdateMode,
                KeepReceiverChanges = KeepReceiverChanges,
                ExportTime = ExportTime
            };
        }
    }

    /// <summary>
    /// The rule that derives a discipline label from a test name.
    /// Правило вывода метки дисциплины из имени проверки.
    /// </summary>
    public class DisciplineLabelRule
    {
        /// <summary>
        /// Creates an empty rule; the serializer needs this constructor.
        /// Создаёт пустое правило; этот конструктор нужен сериализатору.
        /// </summary>
        public DisciplineLabelRule()
        {
        }

        /// <summary>
        /// Creates a rule from a substring and the label it implies.
        /// Создаёт правило из подстроки и метки, которую она означает.
        /// </summary>
        /// <param name="substring">The substring to look for in a test name.</param>
        /// <param name="label">The label from the vocabulary.</param>
        public DisciplineLabelRule(string substring, string label)
        {
            Substring = substring;
            Label = label;
        }

        /// <summary>
        /// The substring in a test name. The comparison ignores case.
        /// Подстрока в имени проверки. Сравнение без учёта регистра.
        /// </summary>
        public string Substring { get; set; }

        /// <summary>
        /// The label from the vocabulary.
        /// Метка из справочника.
        /// </summary>
        public string Label { get; set; }
    }
}
