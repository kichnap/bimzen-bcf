using System;
using System.Collections.Generic;
using System.Threading;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// The second source of topics — saved views.
    ///
    /// Not everything a coordinator sees follows from clash logic: a device
    /// turned the wrong way round, a pipe crossing the middle of a room, an
    /// assembly built off-design. A person records such issues as a saved
    /// view, and in BCF they must travel alongside the clashes — the same
    /// file, the same export.
    ///
    /// The port is deliberately separate from <see cref="IClashSource"/>: the
    /// two sources differ in nature and in lifetime, and a consumer may want
    /// only one of them.
    ///
    /// Второй источник замечаний — сохранённые виды.
    ///
    /// Не всё, что видит координатор, следует из логики коллизий: прибор
    /// повёрнут не той стороной, труба идёт посреди помещения, узел собран
    /// не по проекту. Такие замечания человек фиксирует сохранённым видом,
    /// и в BCF они должны уходить наравне с коллизиями — в тот же файл, той же
    /// выгрузкой.
    ///
    /// Порт намеренно отделён от <see cref="IClashSource"/>: у источников
    /// разная природа и разный жизненный цикл, а потребителю может понадобиться
    /// только один из них.
    /// </summary>
    public interface ISavedViewpointSource
    {
        /// <summary>
        /// The saved views of the document, those inside folders included.
        /// Сохранённые виды документа, включая лежащие в папках.
        /// </summary>
        IReadOnlyList<SavedViewpointInfo> GetSavedViewpoints();

        /// <summary>
        /// The camera and the snapshot for a saved view.
        /// Камера и снимок для сохранённого вида.
        /// </summary>
        /// <param name="viewpoint">The saved view to look at.</param>
        /// <param name="snapshot">What exactly is asked for.</param>
        /// <param name="cancellationToken">Cancels a slow capture.</param>
        ClashViewpointData CreateViewpoint(SavedViewpointInfo viewpoint, SnapshotRequest snapshot, CancellationToken cancellationToken);
    }

    /// <summary>
    /// A saved view of the host document.
    /// Сохранённый вид документа хоста.
    /// </summary>
    public class SavedViewpointInfo
    {
        /// <summary>
        /// The identifier of the view inside the document. It survives a
        /// rename and a move between folders — the stable key rests on it.
        ///
        /// Идентификатор вида внутри документа. Переживает переименование
        /// и переезд между папками — на нём и держится устойчивый ключ.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The name of the saved view as a person sees it.
        /// Имя сохранённого вида, каким его видит человек.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The folder path down to the view. Empty for views at the root.
        /// Путь папок до вида. Пусто для видов в корне.
        /// </summary>
        public string FolderPath { get; set; }

        /// <summary>
        /// When the view was created, as far as the host knows.
        /// Когда вид был создан, насколько это известно хосту.
        /// </summary>
        public DateTimeOffset? CreatedDate { get; set; }

        /// <summary>
        /// The comments of the view — they are carried into the topic as they are.
        /// Комментарии вида — переносятся в замечание как есть.
        /// </summary>
        public IList<ClashCommentInfo> Comments { get; } = new List<ClashCommentInfo>();

        /// <summary>
        /// Whether the view hides part of the model. It decides what ends up in
        /// the snapshot.
        ///
        /// Прячет ли вид часть модели. От этого зависит, что окажется
        /// на снимке.
        /// </summary>
        public bool HasVisibilityOverrides { get; set; }

        /// <summary>
        /// A handle to the host object; this library never dereferences it.
        /// Ссылка на объект хоста; библиотека её не разыменовывает.
        /// </summary>
        public object SourceHandle { get; set; }

        /// <summary>
        /// The view name together with its folder — the way a person sees it in
        /// the tree.
        ///
        /// Имя вида вместе с папкой — так его видит человек в дереве.
        /// </summary>
        public string FullName
        {
            get
            {
                return string.IsNullOrWhiteSpace(FolderPath) ? Name : FolderPath + " / " + Name;
            }
        }
    }
}
