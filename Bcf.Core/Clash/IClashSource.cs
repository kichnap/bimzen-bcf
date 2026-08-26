using System;
using System.Collections.Generic;
using System.Threading;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// The port to a source of clashes. The only place through which this
    /// library receives host data: it references no BIM application and has to
    /// build and test where none is installed.
    ///
    /// The implementation lives in the embedding tool; a headless agent brings
    /// its own.
    ///
    /// Порт к источнику коллизий. Единственное место, через которое библиотека
    /// получает данные хоста: она не ссылается ни на одно BIM-приложение
    /// и обязана собираться и тестироваться там, где его нет.
    ///
    /// Реализация живёт во встраивающем инструменте; у агента без интерфейса
    /// она своя.
    /// </summary>
    public interface IClashSource
    {
        /// <summary>
        /// The document: name, path, units, the models it holds.
        /// Документ: имя, путь, единицы, состав моделей.
        /// </summary>
        ClashDocumentInfo GetDocument();

        /// <summary>
        /// The list of clash tests.
        /// Список проверок на коллизии.
        /// </summary>
        IReadOnlyList<ClashTestInfo> GetTests();

        /// <summary>
        /// The clashes of one test, returned lazily: five thousand results must
        /// not materialise as a list before writing starts.
        ///
        /// Коллизии одной проверки, возвращаемые лениво: пять тысяч
        /// результатов не должны материализоваться списком до начала записи.
        /// </summary>
        /// <param name="test">The test to enumerate.</param>
        /// <param name="cancellationToken">Cancels a long enumeration.</param>
        IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken);

        /// <summary>
        /// A viewpoint for a clash: the camera, clipping planes and, if asked
        /// for, a snapshot. Restoring the view and rendering is the slowest
        /// operation of an export, so it is called once per topic.
        ///
        /// Точка зрения на коллизию: камера, секущие плоскости и, если просили,
        /// снимок. Восстановление вида и отрисовка — самая медленная операция
        /// выгрузки, поэтому вызывается один раз на замечание.
        /// </summary>
        /// <param name="clash">The clash to look at.</param>
        /// <param name="snapshot">What exactly is asked for.</param>
        /// <param name="cancellationToken">Cancels a slow capture.</param>
        ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken);
    }

    /// <summary>
    /// The document an export runs over.
    /// Документ, из которого идёт выгрузка.
    /// </summary>
    public class ClashDocumentInfo
    {
        /// <summary>
        /// The document title shown to a person.
        /// Заголовок документа, который видит человек.
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The full path to the document. A project identifier can be derived from it.
        /// Полный путь к документу. Из него можно вывести идентификатор проекта.
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The document units. Needed by the adapter; coordinates reach this
        /// library already in metres.
        ///
        /// Единицы документа. Нужны адаптеру; в саму библиотеку координаты
        /// приходят уже в метрах.
        /// </summary>
        public LengthUnit Units { get; set; }

        /// <summary>
        /// The models loaded into the document.
        /// Модели, загруженные в документ.
        /// </summary>
        public IList<ClashModelInfo> Models { get; } = new List<ClashModelInfo>();
    }

    /// <summary>
    /// A loaded model.
    /// Загруженная модель.
    /// </summary>
    public class ClashModelInfo
    {
        /// <summary>The model file name. / Имя файла модели.</summary>
        public string FileName { get; set; }

        /// <summary>When the model file was produced. / Когда файл модели был получен.</summary>
        public DateTimeOffset? Date { get; set; }
    }

    /// <summary>
    /// A clash test.
    /// Проверка на коллизии.
    /// </summary>
    public class ClashTestInfo
    {
        /// <summary>
        /// A stable identifier of the test, used by the export settings.
        /// Устойчивый идентификатор проверки, которым пользуются настройки выгрузки.
        /// </summary>
        public string Id { get; set; }

        /// <summary>The test name. / Имя проверки.</summary>
        public string Name { get; set; }

        /// <summary>
        /// The index within the document collection.
        /// Индекс в коллекции документа.
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// The number of clashes — for a counter in a dialog and for progress.
        /// Число коллизий — для счётчика в диалоге и для оценки прогресса.
        /// </summary>
        public int ClashCount { get; set; }
    }

    /// <summary>
    /// A single clash.
    /// Одна коллизия.
    /// </summary>
    public class ClashItem
    {
        /// <summary>The identifier of the test it belongs to. / Идентификатор проверки, которой она принадлежит.</summary>
        public string TestId { get; set; }

        /// <summary>The name of that test. / Имя этой проверки.</summary>
        public string TestName { get; set; }

        /// <summary>
        /// The group name, when the clash is grouped.
        /// Имя группы, если коллизия сгруппирована.
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// The clash name. Tools regenerate these names when a test is reset,
        /// so they must not be used as an identity.
        ///
        /// Имя коллизии. Инструменты раздают эти имена заново при сбросе
        /// проверки, поэтому опознавать по ним нельзя.
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The clash status: New, Active, Reviewed, Approved, Resolved.
        /// Статус коллизии: New, Active, Reviewed, Approved, Resolved.
        /// </summary>
        public string Status { get; set; }

        /// <summary>
        /// The distance or penetration depth, in metres.
        /// Расстояние или глубина проникновения, метры.
        /// </summary>
        public double? DistanceMeters { get; set; }

        /// <summary>
        /// The clash point, in metres.
        /// Точка коллизии, метры.
        /// </summary>
        public Vector3? CenterMeters { get; set; }

        /// <summary>
        /// The nearest level of the document grid.
        /// Ближайший уровень по сетке документа.
        /// </summary>
        public string LevelName { get; set; }

        /// <summary>
        /// The nearest grid intersection.
        /// Ближайшее пересечение осей.
        /// </summary>
        public string GridLocation { get; set; }

        /// <summary>When the clash was first found. / Когда коллизия была найдена впервые.</summary>
        public DateTimeOffset? CreatedDate { get; set; }

        /// <summary>
        /// The Assigned To value — free text in most clash tools.
        /// Значение поля Assigned To — в большинстве инструментов произвольный текст.
        /// </summary>
        public string AssignedTo { get; set; }

        /// <summary>Who approved the clash, if anyone. / Кто утвердил коллизию, если утверждал.</summary>
        public string ApprovedBy { get; set; }

        /// <summary>The elements that collide. / Элементы, которые столкнулись.</summary>
        public IList<ClashElementInfo> Elements { get; } = new List<ClashElementInfo>();

        /// <summary>The comments kept on the clash. / Комментарии, хранящиеся у коллизии.</summary>
        public IList<ClashCommentInfo> Comments { get; } = new List<ClashCommentInfo>();

        /// <summary>
        /// A handle to the original host object. This library never
        /// dereferences it and does not know its type — it simply hands it back
        /// to the source when asking for a viewpoint. Otherwise host types
        /// would have to be dragged in here, or every snapshot would have to be
        /// captured up front and held in memory.
        ///
        /// Ссылка на исходный объект хоста. Библиотека её не разыменовывает
        /// и не знает её типа — просто отдаёт обратно источнику, когда просит
        /// точку зрения. Иначе пришлось бы либо тащить сюда типы хоста, либо
        /// снимать все кадры заранее и держать их в памяти.
        /// </summary>
        public object SourceHandle { get; set; }
    }

    /// <summary>
    /// An element taking part in a clash.
    /// Элемент, участвующий в коллизии.
    /// </summary>
    public class ClashElementInfo
    {
        /// <summary>
        /// The 22-character identifier. Empty when it could not be resolved.
        /// 22-символьный идентификатор. Пустой, если сопоставить не удалось.
        /// </summary>
        public string IfcGuid { get; set; }

        /// <summary>
        /// The identifier in the authoring system, such as a Revit element id.
        /// Идентификатор в исходной системе — например, Revit Element Id.
        /// </summary>
        public string ElementId { get; set; }

        /// <summary>The model file the element belongs to. / Файл модели, которому принадлежит элемент.</summary>
        public string ModelFileName { get; set; }

        /// <summary>
        /// The element path in the model tree.
        /// Путь элемента в дереве модели.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// How the identifier was obtained — it reaches the export report.
        /// Как получен идентификатор — попадает в итоговый отчёт.
        /// </summary>
        public ElementIdOrigin Origin { get; set; }

        /// <summary>
        /// Where the numeric identifier came from: the property, and how many
        /// levels above the element it was found.
        ///
        /// A consumer comparing this export with its own reading of the model
        /// needs it. For a composite element — a stair with tiling, a wall with
        /// layers — the number of the geometry and the number of the element
        /// itself differ, and knowing which one is in the file matters more
        /// than it seems: mixing them up identifies a neighbouring element, and
        /// that does not look like an error.
        ///
        /// Откуда взялся числовой идентификатор: свойство и на сколько уровней
        /// выше элемента оно нашлось.
        ///
        /// Нужно потребителю, который сверяет выгрузку со своим разбором
        /// модели. У составного элемента — лестницы с плиткой, стены со слоями —
        /// номер геометрии и номер самого элемента разные, и знать, который
        /// из них в файле, важнее, чем кажется: перепутав, потребитель опознает
        /// соседний элемент, и это не выглядит как ошибка.
        /// </summary>
        public string ElementIdSource { get; set; }
    }

    /// <summary>
    /// Where an element identifier came from. The order of the values repeats
    /// the order in which they are attempted: an IFC property, then a Revit
    /// UniqueId, then the host's own instance identifier.
    ///
    /// Источник идентификатора элемента. Порядок значений повторяет порядок
    /// попыток: свойство IFC, затем Revit UniqueId, затем собственный
    /// идентификатор экземпляра у хоста.
    /// </summary>
    public enum ElementIdOrigin
    {
        /// <summary>
        /// Nothing matched: the element will not be highlighted.
        /// Сопоставить не удалось: элемент не будет подсвечен.
        /// </summary>
        None,

        /// <summary>
        /// An IFC GUID property — the model came from IFC.
        /// Свойство IFC GUID — модель пришла из IFC.
        /// </summary>
        IfcProperty,

        /// <summary>
        /// A Revit UniqueId converted into an IFC GUID.
        /// Revit UniqueId, пересчитанный в IFC GUID.
        /// </summary>
        RevitUniqueId,

        /// <summary>
        /// The host's own instance identifier — the last resort.
        /// Собственный идентификатор экземпляра у хоста — последний рубеж.
        /// </summary>
        InstanceGuid
    }

    /// <summary>
    /// A comment kept by the clash-detection tool.
    /// Комментарий, хранящийся в инструменте поиска коллизий.
    /// </summary>
    public class ClashCommentInfo
    {
        /// <summary>The comment author. / Автор комментария.</summary>
        public string Author { get; set; }

        /// <summary>The comment text. / Текст комментария.</summary>
        public string Text { get; set; }

        /// <summary>When it was written. / Когда он написан.</summary>
        public DateTimeOffset Date { get; set; }

        /// <summary>The status recorded with the comment. / Статус, записанный вместе с комментарием.</summary>
        public string Status { get; set; }
    }

    /// <summary>
    /// How the image is captured.
    /// Как снимается изображение.
    /// </summary>
    public enum SnapshotMode
    {
        /// <summary>
        /// Native: the view is restored as the host offers it and a frame is
        /// taken. Nothing is isolated or re-aimed — what a user would see by
        /// simply jumping to the clash.
        ///
        /// Встроенный: вид восстанавливается таким, каким его отдаёт хост,
        /// и снимается кадр. Ничего не изолируется и не перенацеливается —
        /// то, что пользователь увидел бы, просто перейдя к коллизии.
        /// </summary>
        Native,

        /// <summary>
        /// Custom: the camera is aimed at the clash and fitted to its bounds,
        /// the surroundings are cut away, the selection is highlighted.
        ///
        /// Кастомный: камера наводится на коллизию и подгоняется по её
        /// габаритам, окружение обрезается, выделение подсвечивается.
        /// </summary>
        Custom
    }

    /// <summary>
    /// What to do with the surroundings of a clash in the custom mode.
    /// Что делать с окружением коллизии в кастомном режиме.
    /// </summary>
    public enum SnapshotIsolation
    {
        /// <summary>Leave everything alone. / Ничего не трогать.</summary>
        None,

        /// <summary>
        /// A section box around the clash. It removes the building envelope and
        /// the slabs while keeping the context around — what a clash tool does
        /// in its own interface and what a saved view does not carry.
        ///
        /// Секущий бокс вокруг коллизии. Убирает оболочку здания и перекрытия,
        /// оставляя контекст вокруг, — то, что инструмент коллизий делает
        /// в своём интерфейсе и чего нет в сохранённом виде.
        /// </summary>
        SectionBox,

        /// <summary>
        /// Hide everything except the elements of the clash.
        /// Скрыть всё, кроме элементов коллизии.
        /// </summary>
        HideOthers,

        /// <summary>
        /// The box and the hiding together. Visually indistinguishable from
        /// plain hiding: once only the clash elements are left in the scene, the
        /// box has nothing to cut. Kept for cases where the viewpoint should
        /// carry the clipping planes as well.
        ///
        /// Бокс и скрытие вместе. Внешне не отличается от простого скрытия:
        /// когда в сцене остались только элементы коллизии, боксу уже нечего
        /// резать. Оставлен для случаев, где важно, чтобы вид нёс и секущие
        /// плоскости тоже.
        /// </summary>
        SectionBoxAndHideOthers,

        /// <summary>
        /// The box plus translucent surroundings. The clash is fully visible
        /// and in colour while walls and slabs stay as ghosts — the snapshot
        /// then shows not only what collided but where. Clash tools do the same
        /// with dimming in their own interface.
        ///
        /// This never reaches the BCF viewpoint: there, visibility stays as
        /// explicit lists, because other receiving tools each render dimming
        /// their own way.
        ///
        /// Бокс и полупрозрачное окружение. Коллизия видна целиком и в цвете,
        /// а стены и перекрытия остаются призраком — на снимке понятно
        /// не только что столкнулось, но и где. То же самое инструменты
        /// коллизий делают у себя прозрачным затемнением.
        ///
        /// В саму точку зрения BCF это не попадает: там по-прежнему явные
        /// списки видимости, потому что чужие приёмники рисуют затемнение
        /// каждый по-своему.
        /// </summary>
        SectionBoxAndTransparentSurroundings
    }

    /// <summary>
    /// What the source is asked for when a viewpoint is requested.
    /// Что просят у источника, когда запрашивают точку зрения.
    /// </summary>
    public class SnapshotRequest
    {
        /// <summary>
        /// Whether a snapshot is wanted at all.
        /// Нужен ли снимок вообще.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>The frame width in pixels. / Ширина кадра в пикселях.</summary>
        public int Width { get; set; } = 800;

        /// <summary>The frame height in pixels. / Высота кадра в пикселях.</summary>
        public int Height { get; set; } = 600;

        /// <summary>How the frame is captured. / Как снимается кадр.</summary>
        public SnapshotMode Mode { get; set; } = SnapshotMode.Custom;

        /// <summary>What happens to the surroundings. / Что происходит с окружением.</summary>
        public SnapshotIsolation Isolation { get; set; } = SnapshotIsolation.SectionBox;

        /// <summary>
        /// The margin around the clash bounds, in metres.
        /// Поле вокруг габаритов коллизии, метры.
        /// </summary>
        public double BoxMarginMeters { get; set; } = 2.5;

        /// <summary>
        /// How many seconds to allow for drawing the frame. A host draws the
        /// scene progressively and returns whatever it managed: without a
        /// budget the snapshot shows the background and bounding boxes instead
        /// of geometry.
        ///
        /// Сколько секунд дать на отрисовку кадра. Хост рисует сцену
        /// постепенно и возвращает то, что успел: без бюджета на снимке
        /// остаётся фон и габаритные коробки вместо геометрии.
        /// </summary>
        public double TimeBudgetSeconds { get; set; } = 5.0;

        /// <summary>
        /// Whether the overlay is drawn — without it the highlighting of the
        /// selected elements is invisible.
        ///
        /// Рисовать ли оверлей — без него не видно подсветки выделенных
        /// элементов.
        /// </summary>
        public bool IncludeOverlay { get; set; } = true;
    }

    /// <summary>
    /// A viewpoint received from the host.
    /// Точка зрения, полученная от хоста.
    /// </summary>
    public class ClashViewpointData
    {
        /// <summary>
        /// The camera, in metres. When the host has no stored view, the adapter
        /// builds one itself.
        ///
        /// Камера в метрах. Если у хоста нет сохранённого вида, адаптер строит
        /// её сам.
        /// </summary>
        public BcfCamera Camera { get; set; }

        /// <summary>
        /// The PNG bytes. Null when snapshots are off or the capture failed.
        /// Байты PNG. Null, если снимки отключены или снять не удалось.
        /// </summary>
        public byte[] Snapshot { get; set; }

        /// <summary>
        /// The frame came out empty: almost all background. Counted separately
        /// from frames that were never taken — otherwise a report cheerfully
        /// says "51 snapshots captured" while all of them are useless, and that
        /// surfaces only at the receiving end.
        ///
        /// Кадр вышел пустым: почти весь фон. Считается отдельно от неснятых —
        /// иначе отчёт бодро сообщает «снимков снято: 51», когда все они
        /// бесполезны, и это выясняется только у приёмника.
        /// </summary>
        public bool SnapshotIsEmpty { get; set; }

        /// <summary>
        /// The clipping planes the host applied, if any.
        /// Секущие плоскости, которые применил хост, если применял.
        /// </summary>
        public IList<BcfClippingPlane> ClippingPlanes { get; } = new List<BcfClippingPlane>();

        /// <summary>
        /// An explanation for the report when something did not work out.
        /// Пояснение для отчёта, если что-то не получилось.
        /// </summary>
        public string Warning { get; set; }
    }
}
