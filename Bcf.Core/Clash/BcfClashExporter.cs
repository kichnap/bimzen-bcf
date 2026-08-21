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

        /// <summary>Остаток лимита снимков: -1 — без ограничения, 0 — исчерпан.</summary>
        private int _snapshotBudget = -1;

        /// <param name="source">Источник коллизий.</param>
        /// <param name="topicGuids">
        /// Карта ранее выданных идентификаторов. Без неё каждая выгрузка
        /// опирается только на детерминированный ключ — этого достаточно,
        /// пока идентификаторы не начал выдавать сервер.
        /// </param>
        public BcfClashExporter(IClashSource source, ITopicGuidStore topicGuids = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _topicGuids = topicGuids ?? new InMemoryTopicGuidStore();
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
            DateTimeOffset exportTime = DateTimeOffset.Now;

            var builder = new ClashTopicBuilder(settings, document, result, exportTime);
            var statusFilter = new HashSet<string>(settings.IncludedClashStatuses ?? new List<string>(), StringComparer.Ordinal);

            var state = new BcfExportProgress
            {
                TotalClashes = tests.Sum(t => t.ClashCount)
            };

            _snapshotBudget = settings.MaxSnapshots <= 0 ? -1 : settings.MaxSnapshots;

            var snapshot = new SnapshotRequest
            {
                Enabled = settings.IncludeSnapshots,
                Width = settings.SnapshotWidth,
                Height = settings.SnapshotHeight
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

                writer.Complete();
                result.WriteReport = writer.Report;

                foreach (string warning in writer.Report.Warnings)
                {
                    result.Warn(warning);
                }
            }
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
            SnapshotRequest request = snapshot;
            if (snapshot.Enabled && _snapshotBudget == 0)
            {
                request = new SnapshotRequest { Enabled = false, Width = snapshot.Width, Height = snapshot.Height };
            }

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
