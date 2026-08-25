using System;
using System.Collections.Generic;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Clash
{
    /// <summary>Как коллизии складываются в замечания.</summary>
    public enum ClashGroupingMode
    {
        /// <summary>Одна группа Clash Detective — одно замечание. Режим по умолчанию.</summary>
        GroupPerTopic,

        /// <summary>Каждая коллизия — отдельное замечание. На больших наборах даёт неподъёмный файл.</summary>
        ClashPerTopic,

        /// <summary>Замечание на уровень или зону — по ближайшему уровню сетки документа.</summary>
        LevelPerTopic
    }

    /// <summary>Что делать, если файл выгрузки уже существует.</summary>
    public enum BcfUpdateMode
    {
        /// <summary>Перезаписать файл целиком. Поведение по умолчанию.</summary>
        Overwrite,

        /// <summary>
        /// Добавить только новые замечания, существующие не трогать вовсе.
        /// Самый безопасный режим: всё, что появилось в файле у приёмника —
        /// статусы, комментарии, вложения, — остаётся байт в байт.
        /// </summary>
        AppendNew,

        /// <summary>
        /// Добавить новые и обновить существующие данными из Navisworks.
        /// Замечание, в котором приёмник оставил то, чего нет в нашей модели,
        /// не переписывается: молча терять чужие данные хуже, чем не обновить.
        /// </summary>
        UpdateAndAppend
    }

    /// <summary>
    /// Настройки экспорта — простой сериализуемый объект, а не собственность диалога.
    ///
    /// Диалог его только заполняет, экспортёр только принимает. У второго
    /// потребителя библиотеки — агента, выгружающего коллизии по расписанию, —
    /// окна нет вовсе: те же решения приходят к нему файлом задания. Если бы
    /// параметры жили в форме, переиспользовать экспорт было бы нельзя,
    /// и появилась бы вторая реализация тех же настроек.
    /// </summary>
    public class BcfExportSettings
    {
        /// <summary>Версия формата. 3.0 по умолчанию, 2.1 — переключаемая опция.</summary>
        public BcfVersion Version { get; set; } = BcfVersion.Bcf30;

        /// <summary>Идентификаторы выбранных проверок. Пусто — значит все.</summary>
        public IList<string> SelectedTestIds { get; set; } = new List<string>();

        /// <summary>
        /// Какие статусы Clash Detective выгружать. По умолчанию New и Active:
        /// разобранные и закрытые коллизии координатору обычно не нужны.
        /// </summary>
        public IList<string> IncludedClashStatuses { get; set; } = new List<string> { "New", "Active" };

        public ClashGroupingMode Grouping { get; set; } = ClashGroupingMode.GroupPerTopic;

        public bool IncludeSnapshots { get; set; } = true;

        public int SnapshotWidth { get; set; } = 800;

        public int SnapshotHeight { get; set; } = 600;

        /// <summary>
        /// Как снимать: встроенным способом Navisworks или кастомным,
        /// с наведением камеры и обрезкой окружения.
        /// </summary>
        public SnapshotMode SnapshotMode { get; set; } = SnapshotMode.Custom;

        /// <summary>
        /// Что делать с окружением коллизии в кастомном режиме. По умолчанию —
        /// бокс и полупрозрачное окружение: на снимке видно и что столкнулось,
        /// и где именно. Выбрано по результатам прогонов на реальных проектах.
        /// </summary>
        public SnapshotIsolation SnapshotIsolation { get; set; } = SnapshotIsolation.SectionBoxAndTransparentSurroundings;

        /// <summary>Поле вокруг габаритов коллизии при обрезке, метры.</summary>
        public double SnapshotBoxMarginMeters { get; set; } = 2.5;

        /// <summary>
        /// Бюджет времени на отрисовку кадра, секунды. Navisworks рисует сцену
        /// постепенно: без бюджета кадр возвращается раньше, чем загрузилась
        /// геометрия, и на снимке остаётся фон.
        /// </summary>
        public double SnapshotTimeBudgetSeconds { get; set; } = 5.0;

        /// <summary>Рисовать ли подсветку выделенных элементов.</summary>
        public bool SnapshotIncludeOverlay { get; set; } = true;

        /// <summary>
        /// Предел числа снимков. Ноль — без ограничения. Снятие изображения —
        /// самая медленная операция экспорта, и на тысячах коллизий её
        /// осознанно ограничивают.
        /// </summary>
        public int MaxSnapshots { get; set; }

        /// <summary>
        /// Переопределения таблицы статусов. Пусто — берутся дефолты справочника.
        /// Здесь же живёт решение по Approved: Closed или Rejected.
        /// </summary>
        public IDictionary<string, string> StatusMapping { get; set; } = new Dictionary<string, string>(StringComparer.Ordinal);

        public string TopicType { get; set; } = BcfVocabulary.TopicTypes.Default;

        public string Priority { get; set; } = BcfVocabulary.Priorities.Default;

        public string Stage { get; set; } = BcfVocabulary.Stages.Default;

        /// <summary>
        /// Метки на каждое замечание. Auto по умолчанию: она позволяет сервису
        /// отличать автоматические коллизии от ручных замечаний без разбора текста.
        /// </summary>
        public IList<string> Labels { get; set; } = new List<string> { BcfVocabulary.TopicLabels.Auto };

        /// <summary>
        /// Правила метки дисциплины: подстрока в имени теста -> метка.
        /// По умолчанию пусто — у каждого заказчика свои имена проверок,
        /// и угадывать их нельзя: не сопоставилось, значит метки нет.
        /// </summary>
        public IList<DisciplineLabelRule> DisciplineLabelRules { get; set; } = new List<DisciplineLabelRule>();

        /// <summary>Автор замечаний — email.</summary>
        public string Author { get; set; }

        /// <summary>
        /// Добавлять ли в групповое замечание точку зрения на каждую коллизию
        /// группы. Плоский список компонентов пары не сохраняет — элемент,
        /// участвующий в трёх коллизиях, лежит в нём один раз, — а точка зрения
        /// сохраняет: в ней ровно два компонента одной коллизии.
        ///
        /// Снимок у таких точек зрения не снимается: он стоит секунду на кадр,
        /// а сама точка зрения — пара килобайт XML.
        /// </summary>
        public bool ViewpointPerClash { get; set; } = true;

        /// <summary>
        /// Связывать ли через RelatedTopics замечания, попавшие в одну группу
        /// Clash Detective. Имеет смысл в режиме «замечание на коллизию»:
        /// принадлежность к группе иначе выражается только текстом описания.
        /// </summary>
        public bool LinkGroupTopics { get; set; } = true;

        /// <summary>
        /// Ставить ли имя группы меткой замечания. По умолчанию нет: метки
        /// в приёмнике — фильтры, и триста осей превращают фильтр в свалку.
        /// Включают, когда фильтровать по группе нужно именно в приёмнике;
        /// значения при этом объявляются в справочнике архива.
        /// </summary>
        public bool GroupNameAsLabel { get; set; }

        /// <summary>Переносить ли Assigned To из Clash Detective.</summary>
        public bool CarryAssignedTo { get; set; } = true;

        public bool IncludeComments { get; set; } = true;

        /// <summary>Включать ли расстояние и глубину проникновения в описание.</summary>
        public bool IncludeDistance { get; set; } = true;

        /// <summary>Включать ли пути элементов в дереве модели в описание.</summary>
        public bool IncludeElementPaths { get; set; } = true;

        /// <summary>
        /// Выгружать ли сохранённые виды как отдельные замечания.
        /// Это замечания, которые нельзя вывести из логики коллизий:
        /// прибор повёрнут не той стороной, труба посреди помещения.
        /// </summary>
        public bool IncludeSavedViewpoints { get; set; }

        /// <summary>Идентификаторы выбранных видов. Пусто — значит все.</summary>
        public IList<string> SelectedViewpointIds { get; set; } = new List<string>();

        /// <summary>
        /// Тип замечаний, созданных из сохранённых видов. По умолчанию Issue,
        /// а не Clash: это ручное замечание, а не результат автопроверки.
        /// </summary>
        public string SavedViewpointTopicType { get; set; } = BcfVocabulary.TopicTypes.Issue;

        /// <summary>Имя и идентификатор проекта для project.bcfp.</summary>
        public string ProjectName { get; set; }

        public string ProjectId { get; set; }

        /// <summary>Куда пишется архив.</summary>
        public string OutputPath { get; set; }

        /// <summary>
        /// Что делать, если файл уже существует. По умолчанию — перезаписать:
        /// так вело себя первое издание экспорта, и менять поведение молча,
        /// не спросив, нельзя.
        /// </summary>
        public BcfUpdateMode UpdateMode { get; set; } = BcfUpdateMode.Overwrite;

        /// <summary>
        /// Сохранять ли при обновлении статус, исполнителя и срок, изменённые
        /// в приёмнике. По умолчанию да: работу координатора в BIMcollab
        /// или Solibri повторная выгрузка затирать не должна. Выключается,
        /// когда источник истины по статусам — Clash Detective.
        /// </summary>
        public bool KeepReceiverChanges { get; set; } = true;

        /// <summary>
        /// Время выгрузки: попадает в CreationDate замечаний и в метки времени
        /// записей архива. Не задано — берётся текущий момент. Задают его агент,
        /// которому нужна отметка времени задания, и генератор эталонных файлов,
        /// которому нужна воспроизводимость.
        /// </summary>
        public DateTimeOffset? ExportTime { get; set; }

        /// <summary>Копия настроек — диалог правит её, а не оригинал.</summary>
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

    /// <summary>Правило вывода метки дисциплины из имени проверки.</summary>
    public class DisciplineLabelRule
    {
        public DisciplineLabelRule()
        {
        }

        public DisciplineLabelRule(string substring, string label)
        {
            Substring = substring;
            Label = label;
        }

        /// <summary>Подстрока в имени теста. Сравнение без учёта регистра.</summary>
        public string Substring { get; set; }

        /// <summary>Метка из справочника.</summary>
        public string Label { get; set; }
    }
}
