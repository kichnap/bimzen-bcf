using System;
using System.Collections.Generic;
using Bcf.Core.Geometry;

namespace Bcf.Core.Model
{
    /// <summary>
    /// Точка зрения: камера, выделенные элементы, видимость и секущие плоскости.
    /// Ложится в архив двумя файлами — {guid}.bcfv и снимком.
    /// </summary>
    public class BcfViewpoint
    {
        public Guid Guid { get; set; }

        /// <summary>Камера: перспективная или ортогональная. В 3.0 обязательна.</summary>
        public BcfCamera Camera { get; set; }

        /// <summary>Элементы, которые приёмник должен выделить.</summary>
        public IList<BcfComponent> Selection { get; } = new List<BcfComponent>();

        /// <summary>
        /// Видимость. Формируется явными списками, а не прозрачным затемнением
        /// Navisworks: у сторонних приложений затемнение отображается иначе.
        /// </summary>
        public BcfVisibility Visibility { get; set; }

        public IList<BcfClippingPlane> ClippingPlanes { get; } = new List<BcfClippingPlane>();

        /// <summary>Порядок сортировки точек зрения внутри замечания.</summary>
        public int? Index { get; set; }

        /// <summary>
        /// Снимок в PNG. Держится в памяти ровно на время записи одного топика:
        /// экспорт идёт потоком, иначе пять тысяч коллизий не поместятся.
        /// </summary>
        public byte[] Snapshot { get; set; }

        /// <summary>
        /// Имя файла снимка в папке топика. Только ASCII — имена записей
        /// в архиве не должны зависеть от локали.
        /// </summary>
        public string SnapshotFileName { get; set; } = "snapshot.png";
    }

    /// <summary>Элемент модели в точке зрения.</summary>
    public class BcfComponent
    {
        public BcfComponent()
        {
        }

        public BcfComponent(string ifcGuid)
        {
            IfcGuid = ifcGuid;
        }

        /// <summary>22-символьный идентификатор. Без него приёмник не подсветит элемент.</summary>
        public string IfcGuid { get; set; }

        /// <summary>Система, из которой пришёл элемент (например, Navisworks).</summary>
        public string OriginatingSystem { get; set; }

        /// <summary>Идентификатор элемента в исходной системе — например, Revit Element Id.</summary>
        public string AuthoringToolId { get; set; }
    }

    /// <summary>Видимость элементов в точке зрения.</summary>
    public class BcfVisibility
    {
        /// <summary>Видно ли всё остальное по умолчанию.</summary>
        public bool DefaultVisibility { get; set; }

        /// <summary>Элементы, чья видимость отличается от умолчания.</summary>
        public IList<BcfComponent> Exceptions { get; } = new List<BcfComponent>();

        /// <summary>
        /// Подсказки по показу пространств и проёмов. В 3.0 лежат внутри Visibility,
        /// в 2.1 — на уровне Components; сериализаторы кладут их каждый по-своему.
        /// </summary>
        public BcfViewSetupHints Hints { get; set; }
    }

    /// <summary>Подсказки приёмнику о показе служебной геометрии.</summary>
    public class BcfViewSetupHints
    {
        public bool SpacesVisible { get; set; }

        public bool SpaceBoundariesVisible { get; set; }

        public bool OpeningsVisible { get; set; }
    }

    /// <summary>Секущая плоскость. Координаты — в метрах.</summary>
    public class BcfClippingPlane
    {
        public Vector3 Location { get; set; }

        public Vector3 Direction { get; set; }
    }
}
