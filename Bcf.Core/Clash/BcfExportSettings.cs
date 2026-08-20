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

        /// <summary>Переносить ли Assigned To из Clash Detective.</summary>
        public bool CarryAssignedTo { get; set; } = true;

        public bool IncludeComments { get; set; } = true;

        /// <summary>Включать ли расстояние и глубину проникновения в описание.</summary>
        public bool IncludeDistance { get; set; } = true;

        /// <summary>Включать ли пути элементов в дереве модели в описание.</summary>
        public bool IncludeElementPaths { get; set; } = true;

        /// <summary>Имя и идентификатор проекта для project.bcfp.</summary>
        public string ProjectName { get; set; }

        public string ProjectId { get; set; }

        /// <summary>Куда пишется архив.</summary>
        public string OutputPath { get; set; }

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
                MaxSnapshots = MaxSnapshots,
                StatusMapping = new Dictionary<string, string>(StatusMapping, StringComparer.Ordinal),
                TopicType = TopicType,
                Priority = Priority,
                Stage = Stage,
                Labels = new List<string>(Labels),
                DisciplineLabelRules = new List<DisciplineLabelRule>(DisciplineLabelRules),
                Author = Author,
                CarryAssignedTo = CarryAssignedTo,
                IncludeComments = IncludeComments,
                IncludeDistance = IncludeDistance,
                IncludeElementPaths = IncludeElementPaths,
                ProjectName = ProjectName,
                ProjectId = ProjectId,
                OutputPath = OutputPath
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
