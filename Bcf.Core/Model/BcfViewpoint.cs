using System;
using System.Collections.Generic;
using System.Globalization;
using Bcf.Core.Geometry;

namespace Bcf.Core.Model
{
    /// <summary>
    /// A viewpoint: the camera, the selected components, visibility and
    /// clipping planes. It lands in the archive as two files — {guid}.bcfv and
    /// the snapshot.
    ///
    /// Точка зрения: камера, выделенные элементы, видимость и секущие
    /// плоскости. Ложится в архив двумя файлами — {guid}.bcfv и снимком.
    /// </summary>
    public class BcfViewpoint
    {
        /// <summary>
        /// The viewpoint identifier; it also names the .bcfv file.
        /// Идентификатор точки зрения; им же назван файл .bcfv.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// The camera, perspective or orthogonal. BCF 3.0 requires one.
        /// Камера, перспективная или ортогональная. В 3.0 обязательна.
        /// </summary>
        public BcfCamera Camera { get; set; }

        /// <summary>
        /// The components a receiving tool should highlight.
        /// Элементы, которые приёмник должен выделить.
        /// </summary>
        public IList<BcfComponent> Selection { get; } = new List<BcfComponent>();

        /// <summary>
        /// Visibility, expressed as explicit lists rather than as the
        /// transparency some hosts use: other applications render dimming
        /// differently, and the intent would not survive the trip.
        ///
        /// Видимость, заданная явными списками, а не прозрачностью, которой
        /// пользуются некоторые хосты: другие приложения отображают затемнение
        /// иначе, и замысел не пережил бы дорогу.
        /// </summary>
        public BcfVisibility Visibility { get; set; }

        /// <summary>
        /// Components painted in a colour of their own, one entry per colour.
        /// A colour with no components carries nothing and is not written.
        ///
        /// Элементы, окрашенные в свой цвет, — по записи на цвет. Цвет без
        /// элементов ничего не несёт и не записывается.
        /// </summary>
        public IList<BcfColoring> Coloring { get; } = new List<BcfColoring>();

        /// <summary>
        /// The clipping planes of this viewpoint.
        /// Секущие плоскости этой точки зрения.
        /// </summary>
        public IList<BcfClippingPlane> ClippingPlanes { get; } = new List<BcfClippingPlane>();

        /// <summary>
        /// The sort order of viewpoints within a topic.
        /// Порядок сортировки точек зрения внутри замечания.
        /// </summary>
        public int? Index { get; set; }

        /// <summary>
        /// The snapshot as PNG bytes. It is held in memory only while a single
        /// topic is written: the export runs as a stream, otherwise five
        /// thousand clashes would not fit.
        ///
        /// Снимок в виде байтов PNG. Держится в памяти ровно на время записи
        /// одного замечания: выгрузка идёт потоком, иначе пять тысяч коллизий
        /// не поместятся.
        /// </summary>
        public byte[] Snapshot { get; set; }

        /// <summary>
        /// The snapshot file name inside the topic folder. ASCII only —
        /// archive entry names must not depend on a locale.
        ///
        /// Имя файла снимка в папке замечания. Только ASCII: имена записей
        /// в архиве не должны зависеть от локали.
        /// </summary>
        public string SnapshotFileName { get; set; } = "snapshot.png";
    }

    /// <summary>
    /// A model element referenced by a viewpoint.
    /// Элемент модели, на который ссылается точка зрения.
    /// </summary>
    public class BcfComponent
    {
        /// <summary>
        /// Creates an empty component.
        /// Создаёт пустой элемент.
        /// </summary>
        public BcfComponent()
        {
        }

        /// <summary>
        /// Creates a component with an IFC GUID.
        /// Создаёт элемент с идентификатором IFC.
        /// </summary>
        /// <param name="ifcGuid">The 22-character IFC GUID.</param>
        public BcfComponent(string ifcGuid)
        {
            IfcGuid = ifcGuid;
        }

        /// <summary>
        /// The 22-character identifier. Without it a receiving tool cannot
        /// highlight the element.
        ///
        /// 22-символьный идентификатор. Без него приёмник не подсветит элемент.
        /// </summary>
        public string IfcGuid { get; set; }

        /// <summary>
        /// The system the element came from.
        /// Система, из которой пришёл элемент.
        /// </summary>
        public string OriginatingSystem { get; set; }

        /// <summary>
        /// The element identifier in the authoring system, such as a Revit element id.
        /// Идентификатор элемента в исходной системе — например, Revit Element Id.
        /// </summary>
        public string AuthoringToolId { get; set; }
    }

    /// <summary>
    /// Element visibility within a viewpoint.
    /// Видимость элементов в точке зрения.
    /// </summary>
    public class BcfVisibility
    {
        /// <summary>
        /// Whether everything else is visible by default.
        /// Видно ли всё остальное по умолчанию.
        /// </summary>
        public bool DefaultVisibility { get; set; }

        /// <summary>
        /// The elements whose visibility differs from the default.
        /// Элементы, чья видимость отличается от умолчания.
        /// </summary>
        public IList<BcfComponent> Exceptions { get; } = new List<BcfComponent>();

        /// <summary>
        /// Hints about showing spaces and openings. In 3.0 they live inside
        /// Visibility, in 2.1 at the Components level; each serializer places
        /// them its own way.
        ///
        /// Подсказки о показе пространств и проёмов. В 3.0 они лежат внутри
        /// Visibility, в 2.1 — на уровне Components; каждый сериализатор
        /// кладёт их по-своему.
        /// </summary>
        public BcfViewSetupHints Hints { get; set; }
    }

    /// <summary>
    /// One colour of a viewpoint and the components painted in it. The colour
    /// is stored as ARGB: alpha first, as BCF writes it, and an opaque colour
    /// goes out as six hex digits rather than eight.
    ///
    /// Один цвет точки зрения и элементы, окрашенные в него. Цвет хранится
    /// как ARGB: альфа впереди, как пишет BCF, а непрозрачный цвет уходит
    /// шестью шестнадцатеричными знаками, а не восемью — так его поймёт
    /// и тот приёмник, что прозрачности не знает.
    /// </summary>
    public class BcfColoring
    {
        /// <summary>
        /// Creates an opaque black colouring with no components.
        /// Создаёт непрозрачную чёрную раскраску без элементов.
        /// </summary>
        public BcfColoring()
        {
        }

        /// <summary>
        /// Creates a colouring of the given colour.
        /// Создаёт раскраску заданного цвета.
        /// </summary>
        /// <param name="red">The red channel. / Красный канал.</param>
        /// <param name="green">The green channel. / Зелёный канал.</param>
        /// <param name="blue">The blue channel. / Синий канал.</param>
        /// <param name="alpha">The opacity, 255 for opaque. / Непрозрачность, 255 — непрозрачный.</param>
        public BcfColoring(byte red, byte green, byte blue, byte alpha = 255)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        /// <summary>
        /// The opacity: 255 is opaque, 0 is fully transparent.
        /// Непрозрачность: 255 — непрозрачный, 0 — полностью прозрачный.
        /// </summary>
        public byte Alpha { get; set; } = 255;

        /// <summary>The red channel. / Красный канал.</summary>
        public byte Red { get; set; }

        /// <summary>The green channel. / Зелёный канал.</summary>
        public byte Green { get; set; }

        /// <summary>The blue channel. / Синий канал.</summary>
        public byte Blue { get; set; }

        /// <summary>
        /// The components painted in this colour.
        /// Элементы, окрашенные в этот цвет.
        /// </summary>
        public IList<BcfComponent> Components { get; } = new List<BcfComponent>();

        /// <summary>
        /// The colour as BCF writes it: RRGGBB when opaque, AARRGGBB otherwise,
        /// upper-case — the 2.1 schema accepts nothing else.
        ///
        /// Цвет в том виде, в каком его пишет BCF: RRGGBB для непрозрачного,
        /// AARRGGBB для остальных, заглавными буквами — схема 2.1 строчных
        /// не принимает.
        /// </summary>
        public string ToHex()
        {
            string rgb = Red.ToString("X2", CultureInfo.InvariantCulture)
                + Green.ToString("X2", CultureInfo.InvariantCulture)
                + Blue.ToString("X2", CultureInfo.InvariantCulture);

            return Alpha == 255 ? rgb : Alpha.ToString("X2", CultureInfo.InvariantCulture) + rgb;
        }

        /// <summary>
        /// Parses a BCF colour: six hex digits (RGB) or eight (ARGB), either
        /// case. A leading '#', which some tools write, is forgiven.
        ///
        /// Разбирает цвет BCF: шесть шестнадцатеричных знаков (RGB) или восемь
        /// (ARGB), в любом регистре. Решётку в начале, которую пишут некоторые
        /// программы, прощаем: замысел цвета понятен, а отбросить его значит
        /// потерять раскраску целиком.
        /// </summary>
        /// <param name="value">The attribute value. / Значение атрибута.</param>
        /// <param name="coloring">A colouring of that colour with no components. / Раскраска этого цвета без элементов.</param>
        /// <returns>Whether the value is a colour. / Цвет ли это.</returns>
        public static bool TryParseHex(string value, out BcfColoring coloring)
        {
            coloring = null;

            if (string.IsNullOrWhiteSpace(value)) return false;

            string hex = value.Trim().TrimStart('#');
            uint parsed;

            if ((hex.Length != 6 && hex.Length != 8)
                || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out parsed))
            {
                return false;
            }

            coloring = new BcfColoring(
                (byte)(parsed >> 16),
                (byte)(parsed >> 8),
                (byte)parsed,
                hex.Length == 8 ? (byte)(parsed >> 24) : (byte)255);

            return true;
        }
    }

    /// <summary>
    /// Hints to a receiving tool about showing auxiliary geometry.
    /// Подсказки приёмнику о показе служебной геометрии.
    /// </summary>
    public class BcfViewSetupHints
    {
        /// <summary>Whether spaces are shown. / Показывать ли пространства.</summary>
        public bool SpacesVisible { get; set; }

        /// <summary>Whether space boundaries are shown. / Показывать ли границы пространств.</summary>
        public bool SpaceBoundariesVisible { get; set; }

        /// <summary>Whether openings are shown. / Показывать ли проёмы.</summary>
        public bool OpeningsVisible { get; set; }
    }

    /// <summary>
    /// A clipping plane. Coordinates are in metres.
    /// Секущая плоскость. Координаты в метрах.
    /// </summary>
    public class BcfClippingPlane
    {
        /// <summary>
        /// A point the plane passes through, in metres.
        /// Точка, через которую проходит плоскость, в метрах.
        /// </summary>
        public Vector3 Location { get; set; }

        /// <summary>
        /// The plane normal; what lies along it is cut away.
        /// Нормаль плоскости; то, что лежит по её направлению, отсекается.
        /// </summary>
        public Vector3 Direction { get; set; }
    }
}
