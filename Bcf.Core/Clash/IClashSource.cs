using System;
using System.Collections.Generic;
using System.Threading;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Порт к источнику коллизий. Единственное место, через которое Bcf.Core
    /// получает данные хоста: сама библиотека не ссылается на Navisworks
    /// и обязана собираться и тестироваться там, где его нет.
    ///
    /// Реализация живёт в плагине; у второго потребителя — агента, выгружающего
    /// коллизии по расписанию, — будет своя.
    /// </summary>
    public interface IClashSource
    {
        /// <summary>Документ: имя, путь, единицы, состав моделей.</summary>
        ClashDocumentInfo GetDocument();

        /// <summary>Список проверок на коллизии.</summary>
        IReadOnlyList<ClashTestInfo> GetTests();

        /// <summary>
        /// Коллизии проверки. Возвращается лениво: пять тысяч результатов
        /// не должны материализоваться списком до начала записи.
        /// </summary>
        IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken);

        /// <summary>
        /// Точка зрения на коллизию: камера, секущие плоскости и, если просили,
        /// снимок. Самая медленная операция экспорта — восстановление вида
        /// и рендер, — поэтому вызывается ровно один раз на топик.
        /// </summary>
        ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken);
    }

    /// <summary>Документ, из которого идёт выгрузка.</summary>
    public class ClashDocumentInfo
    {
        public string Title { get; set; }

        /// <summary>Полный путь к .nwf/.nwd. Из него выводится идентификатор проекта.</summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Единицы документа. Нужны адаптеру; в сам Bcf.Core координаты
        /// приходят уже в метрах.
        /// </summary>
        public LengthUnit Units { get; set; }

        public IList<ClashModelInfo> Models { get; } = new List<ClashModelInfo>();
    }

    /// <summary>Загруженная модель.</summary>
    public class ClashModelInfo
    {
        public string FileName { get; set; }

        public DateTimeOffset? Date { get; set; }
    }

    /// <summary>Проверка на коллизии.</summary>
    public class ClashTestInfo
    {
        /// <summary>Устойчивый идентификатор проверки для настроек экспорта.</summary>
        public string Id { get; set; }

        public string Name { get; set; }

        /// <summary>Индекс в коллекции документа.</summary>
        public int Index { get; set; }

        /// <summary>Число коллизий — для счётчика в диалоге и оценки прогресса.</summary>
        public int ClashCount { get; set; }
    }

    /// <summary>Одна коллизия.</summary>
    public class ClashItem
    {
        public string TestId { get; set; }

        public string TestName { get; set; }

        /// <summary>Имя группы, если коллизия сгруппирована.</summary>
        public string GroupName { get; set; }

        public string DisplayName { get; set; }

        /// <summary>Статус Clash Detective: New, Active, Reviewed, Approved, Resolved.</summary>
        public string Status { get; set; }

        /// <summary>Расстояние или глубина проникновения, метры.</summary>
        public double? DistanceMeters { get; set; }

        /// <summary>Точка коллизии, метры.</summary>
        public Vector3? CenterMeters { get; set; }

        /// <summary>Ближайший уровень по сетке документа.</summary>
        public string LevelName { get; set; }

        /// <summary>Ближайшее пересечение осей.</summary>
        public string GridLocation { get; set; }

        public DateTimeOffset? CreatedDate { get; set; }

        /// <summary>Значение поля Assigned To — произвольный текст.</summary>
        public string AssignedTo { get; set; }

        public string ApprovedBy { get; set; }

        public IList<ClashElementInfo> Elements { get; } = new List<ClashElementInfo>();

        public IList<ClashCommentInfo> Comments { get; } = new List<ClashCommentInfo>();

        /// <summary>
        /// Ссылка на исходный объект хоста. Bcf.Core её не разыменовывает
        /// и не знает её типа — просто отдаёт обратно источнику, когда просит
        /// точку зрения. Иначе пришлось бы либо тащить сюда типы Navisworks,
        /// либо держать снимки всех коллизий в памяти заранее.
        /// </summary>
        public object SourceHandle { get; set; }
    }

    /// <summary>Элемент, участвующий в коллизии.</summary>
    public class ClashElementInfo
    {
        /// <summary>22-символьный идентификатор. Пустой, если сопоставить не удалось.</summary>
        public string IfcGuid { get; set; }

        /// <summary>Идентификатор в исходной системе — например, Revit Element Id.</summary>
        public string ElementId { get; set; }

        public string ModelFileName { get; set; }

        /// <summary>Путь элемента в дереве модели.</summary>
        public string Path { get; set; }

        /// <summary>Откуда взялся идентификатор — попадает в итоговый отчёт.</summary>
        public ElementIdOrigin Origin { get; set; }
    }

    /// <summary>
    /// Источник идентификатора элемента. Порядок значений повторяет порядок
    /// попыток: свойство IFC, затем Revit UniqueId, затем InstanceGuid.
    /// </summary>
    public enum ElementIdOrigin
    {
        /// <summary>Сопоставить не удалось: элемент не попадёт в выделение.</summary>
        None,

        /// <summary>Свойство IFC GUID — модель пришла из IFC.</summary>
        IfcProperty,

        /// <summary>Revit UniqueId, пересчитанный в IFC GUID.</summary>
        RevitUniqueId,

        /// <summary>ModelItem.InstanceGuid — последний рубеж.</summary>
        InstanceGuid
    }

    /// <summary>Комментарий Clash Detective.</summary>
    public class ClashCommentInfo
    {
        public string Author { get; set; }

        public string Text { get; set; }

        public DateTimeOffset Date { get; set; }

        public string Status { get; set; }
    }

    /// <summary>Как снимается изображение.</summary>
    public enum SnapshotMode
    {
        /// <summary>
        /// Встроенный: восстанавливается вид, как его отдаёт Navisworks,
        /// и снимается кадр. Ничего не изолируется и не перенацеливается —
        /// то, что пользователь увидел бы, просто перейдя к коллизии.
        /// </summary>
        Native,

        /// <summary>
        /// Кастомный: камера наводится на коллизию и подгоняется по её
        /// габаритам, окружение обрезается, выделение подсвечивается.
        /// </summary>
        Custom
    }

    /// <summary>Что делать с окружением коллизии в кастомном режиме.</summary>
    public enum SnapshotIsolation
    {
        /// <summary>Ничего не трогать.</summary>
        None,

        /// <summary>
        /// Секущий бокс вокруг коллизии. Убирает оболочку здания и перекрытия,
        /// оставляя контекст вокруг — то, что Clash Detective делает в своём
        /// интерфейсе и чего нет в сохранённом виде.
        /// </summary>
        SectionBox,

        /// <summary>Скрыть всё, кроме элементов коллизии.</summary>
        HideOthers,

        /// <summary>
        /// Бокс и скрытие вместе. Внешне не отличается от простого скрытия:
        /// когда в сцене остались только элементы коллизии, боксу уже нечего
        /// резать. Оставлен для случаев, где важно, чтобы вид нёс и секущие
        /// плоскости тоже.
        /// </summary>
        SectionBoxAndHideOthers,

        /// <summary>
        /// Бокс и полупрозрачное окружение. Коллизия видна целиком и в цвете,
        /// а стены и перекрытия остаются призраком — на снимке понятно
        /// не только что столкнулось, но и где. То же самое Clash Detective
        /// делает у себя прозрачным затемнением.
        ///
        /// В саму точку зрения BCF это не попадает: там по-прежнему явные
        /// списки видимости, потому что чужие приёмники рисуют затемнение
        /// каждый по-своему.
        /// </summary>
        SectionBoxAndTransparentSurroundings
    }

    /// <summary>Что просят у источника при получении точки зрения.</summary>
    public class SnapshotRequest
    {
        public bool Enabled { get; set; } = true;

        public int Width { get; set; } = 800;

        public int Height { get; set; } = 600;

        public SnapshotMode Mode { get; set; } = SnapshotMode.Custom;

        public SnapshotIsolation Isolation { get; set; } = SnapshotIsolation.SectionBox;

        /// <summary>Поле вокруг габаритов коллизии, метры.</summary>
        public double BoxMarginMeters { get; set; } = 2.5;

        /// <summary>
        /// Сколько секунд дать на отрисовку кадра. Navisworks рисует сцену
        /// постепенно и возвращает то, что успел: без бюджета на снимке
        /// остаётся фон и габаритные коробки вместо геометрии.
        /// </summary>
        public double TimeBudgetSeconds { get; set; } = 5.0;

        /// <summary>Рисовать ли оверлей — без него не видно подсветки выделенных элементов.</summary>
        public bool IncludeOverlay { get; set; } = true;
    }

    /// <summary>Точка зрения, полученная от хоста.</summary>
    public class ClashViewpointData
    {
        /// <summary>Камера в метрах. Если хост не отдал вид, адаптер строит её сам.</summary>
        public BcfCamera Camera { get; set; }

        /// <summary>PNG. Null, если снимки отключены или снять не удалось.</summary>
        public byte[] Snapshot { get; set; }

        /// <summary>
        /// Кадр вышел пустым: почти весь фон. Считается отдельно от неснятых —
        /// иначе отчёт бодро сообщает «снимков снято: 51», когда все они
        /// бесполезны, и это выясняется только у приёмника.
        /// </summary>
        public bool SnapshotIsEmpty { get; set; }

        public IList<BcfClippingPlane> ClippingPlanes { get; } = new List<BcfClippingPlane>();

        /// <summary>Пояснение для отчёта, если что-то не получилось.</summary>
        public string Warning { get; set; }
    }
}
