using System;
using System.Collections.Generic;
using System.Threading;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Второй источник замечаний — сохранённые виды.
    ///
    /// Не всё, что видит координатор, объясняется логикой коллизий: прибор
    /// повёрнут не той стороной, труба идёт посреди помещения, узел собран
    /// не по проекту. Такие замечания человек фиксирует сохранённым видом,
    /// и в BCF они должны уходить наравне с коллизиями — в тот же файл,
    /// той же выгрузкой.
    ///
    /// Порт отдельный от <see cref="IClashSource"/> намеренно: у источников
    /// разная природа и разный жизненный цикл, а потребитель может захотеть
    /// только один из них.
    /// </summary>
    public interface ISavedViewpointSource
    {
        /// <summary>Сохранённые виды документа, включая вложенные в папки.</summary>
        IReadOnlyList<SavedViewpointInfo> GetSavedViewpoints();

        /// <summary>Камера и снимок для сохранённого вида.</summary>
        ClashViewpointData CreateViewpoint(SavedViewpointInfo viewpoint, SnapshotRequest snapshot, CancellationToken cancellationToken);
    }

    /// <summary>Сохранённый вид Navisworks.</summary>
    public class SavedViewpointInfo
    {
        /// <summary>
        /// Идентификатор вида в документе. Переживает переименование
        /// и перемещение между папками — на нём и держится устойчивый ключ.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The name of the saved view as a person sees it.
        /// Имя сохранённого вида, каким его видит человек.
        /// </summary>
        public string Name { get; set; }

        /// <summary>Путь папок до вида: «Этаж 3 / ОВ». Пусто для видов в корне.</summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// When the view was created, as far as the host knows.
        /// Когда вид был создан, насколько это известно хосту.
        /// </summary>
        public DateTimeOffset? CreatedDate { get; set; }

        /// <summary>Комментарии вида — переносятся в замечание как есть.</summary>
        public IList<ClashCommentInfo> Comments { get; } = new List<ClashCommentInfo>();

        /// <summary>Прячет ли вид часть модели. Влияет на то, что окажется на снимке.</summary>
        public bool HasVisibilityOverrides { get; set; }

        /// <summary>Ссылка на объект хоста; Bcf.Core её не разыменовывает.</summary>
        public object SourceHandle { get; set; }

        /// <summary>Имя вида вместе с папкой — так его видит человек в дереве.</summary>
        public string FullName
        {
            get
            {
                return string.IsNullOrWhiteSpace(FolderPath) ? Name : FolderPath + " / " + Name;
            }
        }
    }
}
