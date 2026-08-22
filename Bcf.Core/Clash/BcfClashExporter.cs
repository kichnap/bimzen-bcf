using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Bcf.Core.Conversion;
using Bcf.Core.Model;
using Bcf.Core.Serialization;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Экспорт коллизий в архив BCF.
    ///
    /// Ни одного окна и ни одного вопроса пользователю: прогресс идёт через
    /// <see cref="IProgress{T}"/>, отмена — через токен, ошибка возвращается
    /// результатом. Иначе экспорт нельзя переиспользовать в агенте, который
    /// работает по расписанию и без человека.
    /// </summary>
    public class BcfClashExporter
    {
        private readonly IClashSource _source;
        private readonly ITopicGuidStore _topicGuids;
        private readonly ISavedViewpointSource _viewpoints;

        /// <summary>Остаток лимита снимков: -1 — без ограничения, 0 — исчерпан.</summary>
        private int _snapshotBudget = -1;

        /// <param name="source">Источник коллизий.</param>
        /// <param name="topicGuids">
        /// Карта ранее выданных идентификаторов. Без неё каждая выгрузка
        /// опирается только на детерминированный ключ — этого достаточно,
        /// пока идентификаторы не начал выдавать сервер.
        /// </param>
        /// <param name="viewpoints">
        /// Источник сохранённых видов. Не задан — выгружаются только коллизии.
        /// </param>
        public BcfClashExporter(
            IClashSource source,
            ITopicGuidStore topicGuids = null,
            ISavedViewpointSource viewpoints = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _topicGuids = topicGuids ?? new InMemoryTopicGuidStore();
            _viewpoints = viewpoints;
        }

        /// <summary>
        /// Пишет архив в поток.
        /// </summary>
        /// <param name="destination">Поток архива. Частичный результат при отмене удаляет вызывающий.</param>
        /// <param name="settings">Настройки экспорта.</param>
        /// <param name="progress">Приёмник прогресса; может быть null.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
        public BcfExportResult Export(
            Stream destination,
            BcfExportSettings settings,
            IProgress<BcfExportProgress> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var result = new BcfExportResult();

            try
            {
                Run(destination, settings, progress, result, cancellationToken);
                result.Succeeded = !result.Cancelled;
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
            }
            catch (Exception ex)
            {
                // Наружу исключение не выпускаем: в Navisworks необработанная
                // ошибка в обработчике команды роняет всё приложение
                result.Error = ex;
                result.Succeeded = false;
            }

            return result;
        }

        private void Run(
            Stream destination,
            BcfExportSettings settings,
            IProgress<BcfExportProgress> progress,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            ClashDocumentInfo document = _source.GetDocument();
            IReadOnlyList<ClashTestInfo> tests = SelectTests(settings);
            DateTimeOffset exportTime = settings.ExportTime ?? DateTimeOffset.Now;

            var builder = new ClashTopicBuilder(settings, document, result, exportTime);
            var statusFilter = new HashSet<string>(settings.IncludedClashStatuses ?? new List<string>(), StringComparer.Ordinal);

            IReadOnlyList<SavedViewpointInfo> viewpoints = SelectViewpoints(settings, result);

            var state = new BcfExportProgress
            {
                // Виды входят в общий счёт: иначе индикатор упирается в 100 %
                // и стоит там, пока снимаются виды — а это самая долгая часть
                TotalClashes = tests.Sum(t => t.ClashCount) + viewpoints.Count
            };

            _snapshotBudget = settings.MaxSnapshots <= 0 ? -1 : settings.MaxSnapshots;

            var snapshot = new SnapshotRequest
            {
                Enabled = settings.IncludeSnapshots,
                Width = settings.SnapshotWidth,
                Height = settings.SnapshotHeight,
                Mode = settings.SnapshotMode,
                Isolation = settings.SnapshotIsolation,
                BoxMarginMeters = settings.SnapshotBoxMarginMeters,
                TimeBudgetSeconds = settings.SnapshotTimeBudgetSeconds,
                IncludeOverlay = settings.SnapshotIncludeOverlay
            };

            using (BcfArchiveWriter writer = BcfArchiveWriter.Create(destination, WriteOptions(settings, document)))
            {
                foreach (ClashTestInfo test in tests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    state.CurrentTest = test.Name;
                    Report(progress, state);

                    ExportTest(test, settings, statusFilter, builder, writer, snapshot, progress, state, result, cancellationToken);
                }

                ExportSavedViewpoints(viewpoints, builder, writer, snapshot, progress, state, result, cancellationToken);

                writer.Complete();
                result.WriteReport = writer.Report;

                foreach (string warning in writer.Report.Warnings)
                {
                    result.Warn(warning);
                }
            }
        }

        /// <summary>
        /// Замечания из сохранённых видов. Идут в тот же архив, что и коллизии:
        /// координатору нужен один файл на выгрузку, а не два.
        /// </summary>
        /// <summary>
        /// Виды, отобранные пользователем. Читаются до начала записи: их число
        /// нужно, чтобы индикатор прогресса знал полный объём работы.
        /// </summary>
        private IReadOnlyList<SavedViewpointInfo> SelectViewpoints(BcfExportSettings settings, BcfExportResult result)
        {
            var empty = new List<SavedViewpointInfo>();

            if (!settings.IncludeSavedViewpoints || _viewpoints == null) return empty;

            IReadOnlyList<SavedViewpointInfo> all;

            try
            {
                all = _viewpoints.GetSavedViewpoints();
            }
            catch (Exception ex)
            {
                result.Warn("Сохранённые виды не прочитаны: " + ex.Message);
                return empty;
            }

            if (all == null) return empty;

            var selected = new HashSet<string>(settings.SelectedViewpointIds ?? new List<string>(), StringComparer.Ordinal);

            // Пустой список выбранных означает «все»: настройки могли прийти
            // из прошлой версии, где выбора видов ещё не было
            return selected.Count == 0
                ? all
                : all.Where(v => selected.Contains(v.Id)).ToList();
        }

        private void ExportSavedViewpoints(
            IReadOnlyList<SavedViewpointInfo> viewpoints,
            ClashTopicBuilder builder,
            BcfArchiveWriter writer,
            SnapshotRequest snapshot,
            IProgress<BcfExportProgress> progress,
            BcfExportProgress state,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            foreach (SavedViewpointInfo viewpoint in viewpoints)
            {
                cancellationToken.ThrowIfCancellationRequested();

                state.CurrentTest = viewpoint.FullName;

                try
                {
                    // Ключ по идентификатору вида: переименование вида и переезд
                    // между папками не должны создавать второе замечание
                    string key = StableTopicKey.Compute(new[] { "savedviewpoint", viewpoint.Id });
                    Guid guid = ResolveTopicGuid(key, result);

                    BcfTopic topic = builder.BuildFromViewpoint(guid, viewpoint);

                    BcfViewpoint bcfViewpoint = CreateSavedViewpoint(guid, viewpoint, snapshot, result, cancellationToken);
                    if (bcfViewpoint != null) topic.Viewpoints.Add(bcfViewpoint);

                    writer.WriteTopic(topic);

                    result.TopicsCreated++;
                    result.ViewpointTopicsCreated++;
                    state.TopicsWritten++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.ClashesSkippedByError++;
                    result.Warn("Вид «" + viewpoint.FullName + "» пропущен: " + ex.Message);
                }

                state.ProcessedClashes++;
                Report(progress, state);
            }
        }

        private BcfViewpoint CreateSavedViewpoint(
            Guid topicGuid,
            SavedViewpointInfo viewpoint,
            SnapshotRequest snapshot,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            ClashViewpointData data;

            SnapshotRequest request = _snapshotBudget == 0 && snapshot.Enabled
                ? WithoutSnapshot(snapshot)
                : snapshot;

            try
            {
                data = _viewpoints.CreateViewpoint(viewpoint, request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Warn("Камера вида «" + viewpoint.FullName + "» не получена: " + ex.Message);
                return null;
            }

            if (data?.Camera == null) return null;

            if (!string.IsNullOrWhiteSpace(data.Warning)) result.Warn(data.Warning);

            if (data.Snapshot != null && data.Snapshot.Length > 0)
            {
                result.SnapshotsCaptured++;
                if (data.SnapshotIsEmpty) result.SnapshotsEmpty++;
                if (_snapshotBudget > 0) _snapshotBudget--;
            }

            return new BcfViewpoint
            {
                Guid = StableTopicKey.ToTopicGuid(StableTopicKey.Compute(new[] { "viewpoint", topicGuid.ToString("D") })),
                Camera = data.Camera,
                Snapshot = data.Snapshot,
                Index = 0,

                // Сохранённый вид может прятать часть модели, но в точку зрения
                // это не переносится: разрешение элементов в идентификаторы IFC
                // стоило бы дороже самой выгрузки. Что видел автор, показывает снимок
                Visibility = new BcfVisibility { DefaultVisibility = true }
            };
        }

        private static SnapshotRequest WithoutSnapshot(SnapshotRequest snapshot)
        {
            return new SnapshotRequest
            {
                Enabled = false,
                Width = snapshot.Width,
                Height = snapshot.Height,
                Mode = snapshot.Mode,
                Isolation = snapshot.Isolation,
                BoxMarginMeters = snapshot.BoxMarginMeters,
                TimeBudgetSeconds = snapshot.TimeBudgetSeconds,
                IncludeOverlay = snapshot.IncludeOverlay
            };
        }

        private void ExportTest(
            ClashTestInfo test,
            BcfExportSettings settings,
            HashSet<string> statusFilter,
            ClashTopicBuilder builder,
            BcfArchiveWriter writer,
            SnapshotRequest snapshot,
            IProgress<BcfExportProgress> progress,
            BcfExportProgress state,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            // Группы накапливаются в пределах одной проверки, а не всей выгрузки:
            // так замечания уходят в архив по мере обхода, а не в самом конце
            var groups = new Dictionary<string, List<ClashItem>>(StringComparer.Ordinal);
            var order = new List<string>();

            foreach (ClashItem clash in _source.EnumerateClashes(test, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                state.ProcessedClashes++;

                if (statusFilter.Count > 0 && !statusFilter.Contains(clash.Status ?? string.Empty))
                {
                    result.ClashesSkippedByStatus++;
                    ReportEvery(progress, state);
                    continue;
                }

                result.ClashesProcessed++;
                result.ElementsWithoutGuid += clash.Elements.Count(e => e.Origin == ElementIdOrigin.None);

                if (settings.Grouping == ClashGroupingMode.ClashPerTopic)
                {
                    WriteTopic(
                        StableTopicKey.ForClash(clash.TestName, clash.Elements.Select(e => e.IfcGuid ?? e.ElementId)),
                        ClashTitle(clash),
                        new List<ClashItem> { clash },
                        builder, writer, snapshot, result, state, cancellationToken);
                }
                else
                {
                    string bucket = GroupName(clash, settings.Grouping);

                    List<ClashItem> items;
                    if (!groups.TryGetValue(bucket, out items))
                    {
                        items = new List<ClashItem>();
                        groups.Add(bucket, items);
                        order.Add(bucket);
                    }

                    items.Add(clash);
                }

                ReportEvery(progress, state);
            }

            foreach (string bucket in order)
            {
                cancellationToken.ThrowIfCancellationRequested();

                List<ClashItem> items = groups[bucket];

                WriteTopic(
                    StableTopicKey.ForGroup(test.Name, bucket),
                    bucket + " — " + test.Name,
                    items,
                    builder, writer, snapshot, result, state, cancellationToken);
            }

            Report(progress, state);
        }

        private void WriteTopic(
            string key,
            string title,
            List<ClashItem> clashes,
            ClashTopicBuilder builder,
            BcfArchiveWriter writer,
            SnapshotRequest snapshot,
            BcfExportResult result,
            BcfExportProgress state,
            CancellationToken cancellationToken)
        {
            try
            {
                BcfTopic topic = builder.Build(key, ResolveTopicGuid(key, result), title, clashes);

                BcfViewpoint viewpoint = CreateViewpoint(topic.Guid, clashes, snapshot, result, cancellationToken);
                if (viewpoint != null) topic.Viewpoints.Add(viewpoint);

                writer.WriteTopic(topic);

                result.TopicsCreated++;
                state.TopicsWritten++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Ошибка на одной коллизии не должна останавливать выгрузку:
                // записываем в отчёт, пропускаем, идём дальше
                result.ClashesSkippedByError++;
                result.Warn("Замечание '" + title + "' пропущено: " + ex.Message);
            }
        }

        /// <summary>
        /// Идентификатор замечания: ранее выданный, если он известен, иначе
        /// детерминированный из ключа. Первое важнее второго — на сервере
        /// у топика может оказаться свой Guid, и повторная выгрузка обязана
        /// попасть в тот же топик, а не создать рядом второй.
        /// </summary>
        private Guid ResolveTopicGuid(string key, BcfExportResult result)
        {
            Guid guid;

            if (_topicGuids.TryGet(key, out guid))
            {
                result.TopicsReused++;
                return guid;
            }

            guid = StableTopicKey.ToTopicGuid(key);
            _topicGuids.Remember(key, guid);

            return guid;
        }

        private BcfViewpoint CreateViewpoint(
            Guid topicGuid,
            List<ClashItem> clashes,
            SnapshotRequest snapshot,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            ClashItem source = clashes[0];

            // Снимки самая медленная часть экспорта; лимит задаётся настройками
            // и после его исчерпания вид всё равно нужен — без картинки
            SnapshotRequest request = snapshot.Enabled && _snapshotBudget == 0
                ? WithoutSnapshot(snapshot)
                : snapshot;

            ClashViewpointData data;

            try
            {
                data = _source.CreateViewpoint(source, request, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                result.Warn("Точка зрения для '" + source.DisplayName + "' не получена: " + ex.Message);
                return null;
            }

            if (data == null || data.Camera == null) return null;

            if (!string.IsNullOrWhiteSpace(data.Warning)) result.Warn(data.Warning);

            var viewpoint = new BcfViewpoint
            {
                // Идентификатор точки зрения выводится из идентификатора замечания:
                // при повторной выгрузке он должен получиться тем же
                Guid = StableTopicKey.ToTopicGuid(StableTopicKey.Compute(new[] { "viewpoint", topicGuid.ToString("D") })),
                Camera = data.Camera,
                Snapshot = data.Snapshot,
                Index = 0
            };

            if (data.Snapshot != null && data.Snapshot.Length > 0)
            {
                result.SnapshotsCaptured++;
                if (data.SnapshotIsEmpty) result.SnapshotsEmpty++;
                if (_snapshotBudget > 0) _snapshotBudget--;
            }

            foreach (BcfComponent component in ClashTopicBuilder.Components(clashes))
            {
                viewpoint.Selection.Add(component);
            }

            foreach (BcfClippingPlane plane in data.ClippingPlanes)
            {
                viewpoint.ClippingPlanes.Add(plane);
            }

            // Видимость задаётся явными списками, а не прозрачным затемнением
            // Navisworks: в других приложениях затемнение отображается иначе
            viewpoint.Visibility = new BcfVisibility { DefaultVisibility = true };

            return viewpoint;
        }

        private IReadOnlyList<ClashTestInfo> SelectTests(BcfExportSettings settings)
        {
            IReadOnlyList<ClashTestInfo> all = _source.GetTests();

            if (settings.SelectedTestIds == null || settings.SelectedTestIds.Count == 0) return all;

            var selected = new HashSet<string>(settings.SelectedTestIds, StringComparer.Ordinal);

            return all.Where(t => selected.Contains(t.Id)).ToList();
        }

        private static BcfWriteOptions WriteOptions(BcfExportSettings settings, ClashDocumentInfo document)
        {
            return new BcfWriteOptions
            {
                Version = settings.Version,
                EntryTimestamp = settings.ExportTime,
                Author = settings.Author,
                IncludeSnapshots = settings.IncludeSnapshots,
                Project = new BcfProject
                {
                    ProjectId = string.IsNullOrWhiteSpace(settings.ProjectId)
                        ? ProjectIdFromPath(document)
                        : settings.ProjectId,
                    Name = string.IsNullOrWhiteSpace(settings.ProjectName) ? document.Title : settings.ProjectName
                }
            };
        }

        /// <summary>
        /// Идентификатор проекта из пути документа — детерминированный, чтобы
        /// повторные выгрузки одного файла попадали в один проект.
        /// </summary>
        private static string ProjectIdFromPath(ClashDocumentInfo document)
        {
            string material = string.IsNullOrWhiteSpace(document.FilePath) ? document.Title : document.FilePath;

            return StableTopicKey.FormatGuid(
                StableTopicKey.ToTopicGuid(StableTopicKey.Compute(new[] { "project", material ?? "unnamed" })));
        }

        private static string GroupName(ClashItem clash, ClashGroupingMode mode)
        {
            if (mode == ClashGroupingMode.LevelPerTopic)
            {
                return string.IsNullOrWhiteSpace(clash.LevelName) ? "Без уровня" : clash.LevelName;
            }

            // Несгруппированные коллизии не сваливаются в одну кучу: у каждой
            // своё замечание, иначе они потеряются внутри общей группы
            if (!string.IsNullOrWhiteSpace(clash.GroupName)) return clash.GroupName;

            return string.IsNullOrWhiteSpace(clash.DisplayName) ? "Коллизия" : clash.DisplayName;
        }

        private static string ClashTitle(ClashItem clash)
        {
            string name = string.IsNullOrWhiteSpace(clash.DisplayName) ? "Коллизия" : clash.DisplayName;

            return string.IsNullOrWhiteSpace(clash.TestName) ? name : name + " — " + clash.TestName;
        }

        private static void Report(IProgress<BcfExportProgress> progress, BcfExportProgress state)
        {
            progress?.Report(state);
        }

        private static void ReportEvery(IProgress<BcfExportProgress> progress, BcfExportProgress state)
        {
            // Прогресс на каждой коллизии — это тысячи маршалингов в UI-поток
            if (state.ProcessedClashes % 25 == 0) Report(progress, state);
        }
    }
}
