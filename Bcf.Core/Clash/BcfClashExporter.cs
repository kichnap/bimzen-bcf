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
    /// Exporting clashes into a BCF archive.
    ///
    /// Not one window and not one question to the user: progress goes through
    /// <see cref="IProgress{T}"/>, cancellation through a token, and an error
    /// comes back as part of the result. Otherwise the export could not be
    /// reused inside an agent that runs on a schedule with nobody watching.
    ///
    /// Выгрузка коллизий в архив BCF.
    ///
    /// Ни одного окна и ни одного вопроса пользователю: прогресс идёт через
    /// <see cref="IProgress{T}"/>, отмена — через токен, ошибка возвращается
    /// результатом. Иначе выгрузку нельзя было бы переиспользовать в агенте,
    /// который работает по расписанию и без человека.
    /// </summary>
    public class BcfClashExporter
    {
        private readonly IClashSource _source;
        private readonly ITopicGuidStore _topicGuids;
        private readonly ISavedViewpointSource _viewpoints;

        /// <summary>
        /// What is left of the snapshot budget: -1 means no limit, 0 means spent.
        /// Остаток лимита снимков: -1 — без ограничения, 0 — исчерпан.
        /// </summary>
        private int _snapshotBudget = -1;

        /// <summary>
        /// The archive being updated; null means the export writes a file from
        /// scratch.
        ///
        /// Обновляемый архив; null — выгрузка пишет файл с нуля.
        /// </summary>
        private ExistingBcfArchive _existing;

        /// <summary>
        /// The keys already issued during this export. The same pair of
        /// elements collides twice more often than one would think: in a live
        /// test of 1391 topics, 26 were such. Without counting the repeats both
        /// topics would get one identifier and one folder in the archive.
        ///
        /// Ключи, уже выданные в этой выгрузке. Одна и та же пара элементов
        /// сталкивается дважды чаще, чем кажется: на живой проверке из 1391
        /// замечания таких оказалось 26. Без учёта повторов оба замечания
        /// получили бы один идентификатор и одну папку в архиве.
        /// </summary>
        private readonly HashSet<string> _usedKeys = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Creates an exporter over the sources given.
        /// Создаёт выгрузку поверх заданных источников.
        /// </summary>
        /// <param name="source">The source of clashes.</param>
        /// <param name="topicGuids">
        /// The map of identifiers issued earlier. Without it every export leans
        /// on the deterministic key alone — enough until a server starts issuing
        /// identifiers of its own.
        ///
        /// Карта ранее выданных идентификаторов. Без неё каждая выгрузка
        /// опирается только на детерминированный ключ — этого достаточно, пока
        /// идентификаторы не начал выдавать сервер.
        /// </param>
        /// <param name="viewpoints">
        /// The source of saved views. Unset means only clashes are exported.
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
        /// Writes the archive into a stream.
        /// Пишет архив в поток.
        /// </summary>
        /// <param name="destination">
        /// The archive stream. On cancellation the caller deletes the partial
        /// result.
        ///
        /// Поток архива. Частичный результат при отмене удаляет вызывающий.
        /// </param>
        /// <param name="settings">The export settings.</param>
        /// <param name="progress">Where progress is reported; may be null.</param>
        /// <param name="cancellationToken">Cancels the export.</param>
        public BcfExportResult Export(
            Stream destination,
            BcfExportSettings settings,
            IProgress<BcfExportProgress> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            return Export(destination, null, settings, progress, cancellationToken);
        }

        /// <summary>
        /// Writes the archive, updating an existing one.
        /// Пишет архив, обновляя существующий.
        /// </summary>
        /// <param name="destination">The stream of the new archive.</param>
        /// <param name="existingArchive">
        /// The file being updated, opened for reading. Writing always goes into
        /// a new stream rather than over the original: an interrupted write over
        /// it would leave the user without either version of the file.
        ///
        /// Обновляемый файл, открытый на чтение. Пишем всегда в новый поток,
        /// а не поверх исходного: прерванная запись поверх оставила бы
        /// пользователя без обеих версий файла.
        /// </param>
        /// <param name="settings">The export settings.</param>
        /// <param name="progress">Where progress is reported; may be null.</param>
        /// <param name="cancellationToken">Cancels the export.</param>
        public BcfExportResult Export(
            Stream destination,
            Stream existingArchive,
            BcfExportSettings settings,
            IProgress<BcfExportProgress> progress = null,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var result = new BcfExportResult();

            try
            {
                Run(destination, existingArchive, settings, progress, result, cancellationToken);
                result.Succeeded = !result.Cancelled;
            }
            catch (OperationCanceledException)
            {
                result.Cancelled = true;
            }
            catch (Exception ex)
            {
                // No exception is let out: in Navisworks an unhandled error
                // inside a command handler brings the whole application down
                result.Error = ex;
                result.Succeeded = false;
            }

            return result;
        }

        private void Run(
            Stream destination,
            Stream existingArchive,
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
                // The views count towards the total: otherwise the bar hits
                // 100 % and stands there while the views are captured — and that
                // is the longest part
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

            using (_existing = OpenExisting(existingArchive, settings, result))
            using (BcfArchiveWriter writer = BcfArchiveWriter.Create(destination, WriteOptions(settings, document)))
            {
                foreach (ClashTestInfo test in tests)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    state.CurrentTest = test.Name;
                    Report(progress, state);

                    ExportTest(test, settings, statusFilter, builder, writer, snapshot, progress, state, result, cancellationToken);
                }

                ExportSavedViewpoints(viewpoints, builder, writer, settings, snapshot, progress, state, result, cancellationToken);

                if (_existing != null)
                {
                    // The topics the export did not touch: a clash has been
                    // resolved and no longer appears in the test — it must not
                    // disappear from the file
                    result.TopicsKept += _existing.CopyRemainingTopics(writer);
                    _existing.CopyExtraEntries(writer);
                }

                writer.Complete();
                result.WriteReport = writer.Report;

                foreach (string warning in writer.Report.Warnings)
                {
                    result.Warn(warning);
                }
            }

            _existing = null;
        }

        /// <summary>
        /// Opens the archive being updated. Null when there is nothing to
        /// update or nobody asked for it.
        ///
        /// Открывает обновляемый архив. Null — когда обновлять нечего или
        /// не просили.
        /// </summary>
        private static ExistingBcfArchive OpenExisting(
            Stream existingArchive, BcfExportSettings settings, BcfExportResult result)
        {
            if (settings.UpdateMode == BcfUpdateMode.Overwrite || existingArchive == null) return null;

            ExistingBcfArchive existing = ExistingBcfArchive.Open(existingArchive);

            if (existing.Version != settings.Version)
            {
                existing.Dispose();

                // Versions cannot be mixed inside one archive, and quietly
                // switching the version the user picked would be a lie about
                // what was written
                throw new InvalidOperationException(
                    "The export file is written in BCF " + existing.Version.ToVersionId() +
                    " while version " + settings.Version.ToVersionId() + " is selected" +
                    ". Pick the same version or save the export into a new file.");
            }

            foreach (string warning in existing.Warnings)
            {
                result.Warn("The file being updated: " + warning);
            }

            return existing;
        }

        /// <summary>
        /// Writes a topic — our own, or, when the file being updated already
        /// holds it, merged with what lies there.
        ///
        /// Записывает замечание — своё или, если оно уже есть в обновляемом
        /// файле, слитое с тем, что там лежит.
        /// </summary>
        private void Emit(
            BcfArchiveWriter writer,
            BcfTopic topic,
            BcfExportSettings settings,
            BcfExportResult result,
            BcfExportProgress state)
        {
            BcfTopic existing = _existing == null ? null : _existing.Find(topic.Guid);

            if (existing != null && HasForeignViewpoints(existing, topic))
            {
                // A viewpoint added in the receiving tool would be lost by a rewrite
                if (_existing.CopyTopic(topic.Guid, writer))
                {
                    result.TopicsKept++;
                    result.Warn(
                        "The topic '" + existing.Title + "' was carried over unchanged: " +
                        "it holds viewpoints from a receiving tool that the export does not keep.");

                    return;
                }
            }

            if (existing != null)
            {
                Merge(topic, existing, settings);
                _existing.MarkHandled(topic.Guid);
            }

            writer.WriteTopic(topic);

            result.TopicsCreated++;
            if (existing != null) result.TopicsUpdated++;

            state.TopicsWritten++;
        }

        /// <summary>
        /// Decides the fate of a topic the file being updated already holds:
        /// carry it over as it is, or rewrite it with our data.
        ///
        /// It is called before the topic is assembled and before a frame is
        /// captured: in the append-only mode a repeat export must not redraw
        /// five thousand snapshots only to throw them away.
        ///
        /// Решает судьбу замечания, которое уже есть в обновляемом файле:
        /// перенести как есть или переписать нашими данными.
        ///
        /// Вызывается до сборки замечания и до снятия кадра: в режиме «только
        /// добавить» повторная выгрузка не должна заново рисовать пять тысяч
        /// снимков ради того, чтобы их выбросить.
        /// </summary>
        /// <returns>True when the topic was carried over and ours need not be built.</returns>
        private bool KeepExisting(
            Guid topicGuid,
            BcfArchiveWriter writer,
            BcfExportSettings settings,
            BcfExportResult result)
        {
            if (_existing == null) return false;

            BcfTopic existing = _existing.Find(topicGuid);
            if (existing == null) return false;

            string foreign = settings.UpdateMode == BcfUpdateMode.AppendNew
                ? null
                : ForeignData(existing);

            if (settings.UpdateMode == BcfUpdateMode.UpdateAndAppend && foreign == null) return false;

            if (!_existing.CopyTopic(topicGuid, writer)) return false;

            if (foreign != null)
            {
                result.Warn(
                    "The topic '" + existing.Title + "' was carried over unchanged: it holds " + foreign +
                    ", which the export does not keep and would lose in a rewrite.");
            }

            result.TopicsKept++;

            return true;
        }

        /// <summary>
        /// The data of a topic that our model does not keep. Null means the
        /// topic can be rewritten without losses.
        ///
        /// Чужие данные замечания, которых модель не хранит. Null — замечание
        /// можно переписать без потерь.
        /// </summary>
        private static string ForeignData(BcfTopic existing)
        {
            return existing.UnsupportedData.Count > 0
                ? string.Join(", ", Enumerable.ToArray(existing.UnsupportedData))
                : null;
        }

        /// <summary>
        /// Whether the existing topic holds viewpoints we do not have.
        ///
        /// It is checked after the topic is assembled and not before: there can
        /// be many viewpoints of our own — one per clash of a group — and their
        /// identifiers are known only once the group has been worked through.
        ///
        /// Есть ли в существующем замечании точки зрения, которых нет у нас.
        ///
        /// Проверяется после сборки замечания, а не до: своих точек зрения
        /// может быть много — по одной на каждую коллизию группы, — и их
        /// идентификаторы известны, только когда группа уже разобрана.
        /// </summary>
        private static bool HasForeignViewpoints(BcfTopic existing, BcfTopic fresh)
        {
            var ours = new HashSet<Guid>();

            foreach (BcfViewpoint viewpoint in fresh.Viewpoints)
            {
                ours.Add(viewpoint.Guid);
            }

            foreach (BcfViewpoint viewpoint in existing.Viewpoints)
            {
                if (viewpoint.Guid != Guid.Empty && !ours.Contains(viewpoint.Guid)) return true;
            }

            return false;
        }

        /// <summary>
        /// Carries into our topic whatever may have changed at the receiving
        /// end.
        ///
        /// Переносит в наше замечание то, что могло измениться у приёмника.
        /// </summary>
        private static void Merge(BcfTopic fresh, BcfTopic existing, BcfExportSettings settings)
        {
            // The topic was created then and not now: the creation date and
            // author belong to the first export
            if (existing.CreationDate != default(DateTimeOffset)) fresh.CreationDate = existing.CreationDate;
            if (!string.IsNullOrWhiteSpace(existing.CreationAuthor)) fresh.CreationAuthor = existing.CreationAuthor;

            // The number a server issued survives any rewrite
            if (!string.IsNullOrWhiteSpace(existing.ServerAssignedId)) fresh.ServerAssignedId = existing.ServerAssignedId;

            if (settings.KeepReceiverChanges)
            {
                if (!string.IsNullOrWhiteSpace(existing.TopicStatus)) fresh.TopicStatus = existing.TopicStatus;
                if (!string.IsNullOrWhiteSpace(existing.AssignedTo)) fresh.AssignedTo = existing.AssignedTo;
                if (existing.DueDate.HasValue) fresh.DueDate = existing.DueDate;
            }

            MergeComments(fresh, existing);

            foreach (string link in existing.ReferenceLinks)
            {
                if (!fresh.ReferenceLinks.Contains(link)) fresh.ReferenceLinks.Add(link);
            }

            foreach (Guid related in existing.RelatedTopics)
            {
                if (!fresh.RelatedTopics.Contains(related)) fresh.RelatedTopics.Add(related);
            }

            fresh.ModifiedDate = settings.ExportTime ?? DateTimeOffset.Now;
            fresh.ModifiedAuthor = settings.Author;
        }

        /// <summary>
        /// The comments of a receiving tool are appended to ours and ordered by
        /// time: the conversation about a topic has to read top to bottom.
        ///
        /// Комментарии приёмника дописываются к нашим и идут по времени:
        /// переписка по замечанию должна читаться сверху вниз.
        /// </summary>
        private static void MergeComments(BcfTopic fresh, BcfTopic existing)
        {
            var known = new HashSet<Guid>();

            foreach (BcfComment comment in fresh.Comments)
            {
                known.Add(comment.Guid);
            }

            var merged = new List<BcfComment>(fresh.Comments);

            foreach (BcfComment comment in existing.Comments)
            {
                if (comment.Guid != Guid.Empty && !known.Add(comment.Guid)) continue;

                merged.Add(comment);
            }

            merged.Sort((left, right) => left.Date.CompareTo(right.Date));

            fresh.Comments.Clear();

            foreach (BcfComment comment in merged)
            {
                fresh.Comments.Add(comment);
            }
        }

        /// <summary>
        /// The identifier of our viewpoint — it is derived from the topic and,
        /// for viewpoints of a single clash, from the key of that clash.
        ///
        /// Идентификатор нашей точки зрения — выводится из замечания и, для
        /// точек зрения на отдельные коллизии, из ключа коллизии.
        /// </summary>
        private static Guid ViewpointGuidFor(Guid topicGuid, string discriminator = null)
        {
            string[] parts = string.IsNullOrEmpty(discriminator)
                ? new[] { "viewpoint", topicGuid.ToString("D") }
                : new[] { "viewpoint", topicGuid.ToString("D"), discriminator };

            return StableTopicKey.ToTopicGuid(StableTopicKey.Compute(parts));
        }

        /// <summary>
        /// The views the user selected. They are read before writing starts:
        /// their number is what lets the progress bar know the full extent of
        /// the work.
        ///
        /// Topics made from saved views go into the same archive as the clashes:
        /// a coordinator wants one file per export, not two.
        ///
        /// Виды, отобранные пользователем. Читаются до начала записи: их число
        /// нужно, чтобы индикатор прогресса знал полный объём работы.
        ///
        /// Замечания из сохранённых видов идут в тот же архив, что и коллизии:
        /// координатору нужен один файл на выгрузку, а не два.
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
                result.Warn("The saved views were not read: " + ex.Message);
                return empty;
            }

            if (all == null) return empty;

            var selected = new HashSet<string>(settings.SelectedViewpointIds ?? new List<string>(), StringComparer.Ordinal);

            // An empty selection means "all of them": the settings may have
            // come from an earlier version that had no view picker yet
            return selected.Count == 0
                ? all
                : all.Where(v => selected.Contains(v.Id)).ToList();
        }

        private void ExportSavedViewpoints(
            IReadOnlyList<SavedViewpointInfo> viewpoints,
            ClashTopicBuilder builder,
            BcfArchiveWriter writer,
            BcfExportSettings settings,
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
                    // The key rests on the view identifier: renaming a view or
                    // moving it between folders must not create a second topic
                    string key = StableTopicKey.Compute(new[] { "savedviewpoint", viewpoint.Id });
                    Guid guid = ResolveTopicGuid(key, result);

                    if (!KeepExisting(guid, writer, settings, result))
                    {
                        BcfTopic topic = builder.BuildFromViewpoint(guid, viewpoint);

                        BcfViewpoint bcfViewpoint = CreateSavedViewpoint(guid, viewpoint, snapshot, result, cancellationToken);
                        if (bcfViewpoint != null) topic.Viewpoints.Add(bcfViewpoint);

                        Emit(writer, topic, settings, result, state);

                        result.ViewpointTopicsCreated++;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result.ClashesSkippedByError++;
                    result.Warn("The view '" + viewpoint.FullName + "' was skipped: " + ex.Message);
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
                result.Warn("The camera of the view '" + viewpoint.FullName + "' was not obtained: " + ex.Message);
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
                Guid = ViewpointGuidFor(topicGuid),
                Camera = data.Camera,
                Snapshot = data.Snapshot,
                Index = 0,

                // A saved view may hide part of the model, but that is not
                // carried into the viewpoint: resolving the elements into IFC
                // identifiers would cost more than the export itself. What the
                // author saw is shown by the snapshot
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
            // Groups accumulate within one test rather than across the whole
            // export: this way topics reach the archive as the walk goes on and
            // not at the very end
            var groups = new Dictionary<string, List<ClashItem>>(StringComparer.Ordinal);
            var order = new List<string>();

            // Group anchors: the first topic of a group, the one the rest point
            // at. They live within a test — so do the groups
            var groupAnchors = new Dictionary<string, Guid>(StringComparer.Ordinal);

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

                CountIdSources(clash, result);

                if (settings.Grouping == ClashGroupingMode.ClashPerTopic)
                {
                    WriteTopic(
                        Unique(ClashKey(clash)),
                        null,
                        ClashTitle(clash),
                        new List<ClashItem> { clash },
                        builder, writer, settings, snapshot, result, state, groupAnchors, cancellationToken);
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

                // A clash that fell into no group is a group of its own, and its
                // "group name" is the clash name of the "Clash123" kind. Such
                // names are handed out afresh by Navisworks when a test is
                // rebuilt, so their key is counted over the elements instead:
                // otherwise a topic loses itself exactly where it is looked for
                bool grouped = !string.IsNullOrWhiteSpace(items[0].GroupName);

                string key = Unique(grouped ? StableTopicKey.ForGroup(test.Name, bucket) : ClashKey(items[0]));
                string legacyKey = grouped ? null : StableTopicKey.ForGroup(test.Name, bucket);

                WriteTopic(
                    key,
                    legacyKey,
                    bucket + " — " + test.Name,
                    items,
                    builder, writer, settings, snapshot, result, state, groupAnchors, cancellationToken);
            }

            Report(progress, state);
        }

        private void WriteTopic(
            string key,
            string legacyKey,
            string title,
            List<ClashItem> clashes,
            ClashTopicBuilder builder,
            BcfArchiveWriter writer,
            BcfExportSettings settings,
            SnapshotRequest snapshot,
            BcfExportResult result,
            BcfExportProgress state,
            Dictionary<string, Guid> groupAnchors,
            CancellationToken cancellationToken)
        {
            try
            {
                Guid topicGuid = ResolveTopicGuid(key, legacyKey, result);

                // The decision comes before the frame is captured: drawing a
                // snapshot that is thrown away a moment later is the most
                // expensive mistake a repeat export can make
                if (KeepExisting(topicGuid, writer, settings, result)) return;

                if (settings.GroupNameAsLabel && !string.IsNullOrWhiteSpace(clashes[0].GroupName))
                {
                    // A group name is not in the vocabulary, and the file has to
                    // declare it itself — otherwise the strict check stops the write
                    writer.DeclareLabel(clashes[0].GroupName.Trim());
                }

                BcfTopic topic = builder.Build(key, topicGuid, title, clashes);

                LinkToGroup(topic, clashes, settings, groupAnchors);

                BcfViewpoint viewpoint = CreateViewpoint(topic.Guid, clashes, snapshot, result, cancellationToken);
                if (viewpoint != null) topic.Viewpoints.Add(viewpoint);

                AddClashViewpoints(topic, clashes, settings, snapshot, result, cancellationToken);

                Emit(writer, topic, settings, result, state);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // An error on one clash must not stop the export: it goes into
                // the report, the clash is skipped, and the walk goes on
                result.ClashesSkippedByError++;
                result.Warn("The topic '" + title + "' was skipped: " + ex.Message);
            }
        }

        /// <summary>
        /// Gathers the figures on where the numeric identifiers came from.
        /// Копит статистику по тому, откуда брались числовые идентификаторы.
        /// </summary>
        private static void CountIdSources(ClashItem clash, BcfExportResult result)
        {
            foreach (ClashElementInfo element in clash.Elements)
            {
                string source = string.IsNullOrWhiteSpace(element.ElementIdSource)
                    ? "not found"
                    : element.ElementIdSource;

                int count;
                result.ElementIdSources.TryGetValue(source, out count);
                result.ElementIdSources[source] = count + 1;

                string origin = element.Origin.ToString();

                result.ElementIdOrigins.TryGetValue(origin, out count);
                result.ElementIdOrigins[origin] = count + 1;
            }
        }

        /// <summary>
        /// Pulls apart the keys that coincided within one export.
        ///
        /// Two clashes between the same pair of elements are an everyday thing:
        /// a pipe crosses a wall twice. Their key is one and the same, yet the
        /// topics have to differ, or the second wipes out the first right there
        /// in the archive. The order Navisworks walks in is stable, so the
        /// number of a repeat is stable from export to export as well.
        ///
        /// Разводит ключи, совпавшие в пределах одной выгрузки.
        ///
        /// Две коллизии между одной и той же парой элементов — обычное дело:
        /// труба пересекает стену дважды. Ключ у них один, а замечания должны
        /// быть разными, иначе второе затрёт первое прямо в архиве. Порядок
        /// обхода Navisworks устойчив, поэтому и номер повтора устойчив
        /// от выгрузки к выгрузке.
        /// </summary>
        private string Unique(string key)
        {
            if (_usedKeys.Add(key)) return key;

            for (int occurrence = 2; ; occurrence++)
            {
                string candidate = key + "#" + occurrence.ToString(CultureInfo.InvariantCulture);

                if (_usedKeys.Add(candidate)) return candidate;
            }
        }

        /// <summary>
        /// The key of a clash — the test name and the identifiers of its
        /// elements. Neither the clash name nor its number: Navisworks hands
        /// those out afresh.
        ///
        /// Ключ коллизии — имя проверки и идентификаторы её элементов.
        /// Ни имени коллизии, ни её номера: их Navisworks раздаёт заново.
        /// </summary>
        private static string ClashKey(ClashItem clash)
        {
            return StableTopicKey.ForClash(clash.TestName, clash.Elements.Select(e => e.IfcGuid ?? e.ElementId));
        }

        /// <summary>
        /// Ties the per-clash topics of one group together through
        /// RelatedTopics.
        ///
        /// The link runs one way, as a star onto the first topic of the group:
        /// the archive is written as a stream, and by the time the second topic
        /// arrives the first is already written. A back-reference would cost
        /// holding every topic in memory until the end of the export — a price
        /// out of all proportion to the convenience.
        ///
        /// Связывает поштучные замечания одной группы через RelatedTopics.
        ///
        /// Связь односторонняя, звездой на первое замечание группы: архив
        /// пишется потоком, и когда приходит второе, первое уже записано.
        /// Обратная ссылка стоила бы того, чтобы держать все замечания в памяти
        /// до конца выгрузки, — цена, несоразмерная удобству.
        /// </summary>
        private static void LinkToGroup(
            BcfTopic topic,
            List<ClashItem> clashes,
            BcfExportSettings settings,
            Dictionary<string, Guid> groupAnchors)
        {
            if (!settings.LinkGroupTopics || groupAnchors == null) return;
            if (clashes.Count != 1) return;

            string group = clashes[0].GroupName;
            if (string.IsNullOrWhiteSpace(group)) return;

            Guid anchor;

            if (!groupAnchors.TryGetValue(group, out anchor))
            {
                groupAnchors.Add(group, topic.Guid);
                return;
            }

            if (anchor != topic.Guid && !topic.RelatedTopics.Contains(anchor)) topic.RelatedTopics.Add(anchor);
        }

        /// <summary>
        /// Adds a viewpoint per clash of a group.
        ///
        /// This is the only way to preserve the pairs: in the flat component
        /// list of a topic an element taking part in three clashes lies once,
        /// and splitting that list back into pairs is impossible even in theory.
        /// A viewpoint holds exactly the two components of one clash.
        ///
        /// No snapshots are captured here: a second per frame against a couple
        /// of kilobytes of XML — the difference that would turn the export of a
        /// group of sixty clashes into a minute of work and ten megabytes.
        ///
        /// Добавляет точку зрения на каждую коллизию группы.
        ///
        /// Это единственный способ сохранить пары: в плоском списке компонентов
        /// замечания элемент, участвующий в трёх коллизиях, лежит один раз,
        /// и разбить список обратно на пары нельзя даже в теории. В точке зрения
        /// — ровно два компонента одной коллизии.
        ///
        /// Снимки здесь не снимаются: секунда на кадр против пары килобайт XML —
        /// разница, из-за которой выгрузка группы из шестидесяти коллизий
        /// превратилась бы в минуту работы и десяток мегабайт.
        /// </summary>
        private void AddClashViewpoints(
            BcfTopic topic,
            List<ClashItem> clashes,
            BcfExportSettings settings,
            SnapshotRequest snapshot,
            BcfExportResult result,
            CancellationToken cancellationToken)
        {
            if (!settings.ViewpointPerClash || clashes.Count < 2) return;

            SnapshotRequest request = WithoutSnapshot(snapshot);
            int index = 1;

            foreach (ClashItem clash in clashes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                BcfViewpoint viewpoint = CreateViewpoint(
                    topic.Guid,
                    new List<ClashItem> { clash },
                    request,
                    result,
                    cancellationToken,
                    ClashKey(clash),
                    index);

                if (viewpoint == null) continue;

                topic.Viewpoints.Add(viewpoint);
                index++;
            }
        }

        /// <summary>
        /// The topic identifier: the one issued earlier when it is known,
        /// otherwise a deterministic one derived from the key. The first matters
        /// more than the second — on a server a topic may have an identifier of
        /// its own, and a repeat export has to land in that same topic rather
        /// than create a second one beside it.
        ///
        /// Идентификатор замечания: ранее выданный, если он известен, иначе
        /// детерминированный из ключа. Первое важнее второго — на сервере
        /// у замечания может оказаться свой идентификатор, и повторная выгрузка
        /// обязана попасть в то же замечание, а не создать рядом второе.
        /// </summary>
        /// <param name="key">The stable key of this topic.</param>
        /// <param name="result">Where a reuse is counted.</param>
        private Guid ResolveTopicGuid(string key, BcfExportResult result)
        {
            return ResolveTopicGuid(key, null, result);
        }

        /// <summary>
        /// The topic identifier: the one issued earlier when it is known,
        /// otherwise a deterministic one derived from the key.
        ///
        /// Идентификатор замечания: ранее выданный, если он известен, иначе
        /// детерминированный из ключа.
        /// </summary>
        /// <param name="key">The stable key of this topic.</param>
        /// <param name="legacyKey">
        /// The key this topic was counted by in earlier versions. If an
        /// identifier was issued for it, that identifier moves to the new key:
        /// changing the rule must not turn into a full set of duplicates at
        /// the receiving end.
        ///
        /// Ключ, которым это замечание считалось в прежних версиях. Если
        /// идентификатор выдан на него, он переносится на новый ключ: смена
        /// правила счёта не должна оборачиваться полным комплектом дублей
        /// в приёмнике.
        /// </param>
        /// <param name="result">Where a reuse is counted.</param>
        private Guid ResolveTopicGuid(string key, string legacyKey, BcfExportResult result)
        {
            Guid guid;

            if (_topicGuids.TryGet(key, out guid))
            {
                result.TopicsReused++;
                return guid;
            }

            if (!string.IsNullOrEmpty(legacyKey) && _topicGuids.TryGet(legacyKey, out guid))
            {
                _topicGuids.Remember(key, guid);
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
            CancellationToken cancellationToken,
            string discriminator = null,
            int index = 0)
        {
            ClashItem source = clashes[0];

            // Snapshots are the slowest part of the export; the limit comes
            // from the settings, and once it is spent the viewpoint is still
            // wanted — without a picture
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
                result.Warn("The viewpoint for '" + source.DisplayName + "' was not obtained: " + ex.Message);
                return null;
            }

            if (data == null || data.Camera == null) return null;

            if (!string.IsNullOrWhiteSpace(data.Warning)) result.Warn(data.Warning);

            var viewpoint = new BcfViewpoint
            {
                // The viewpoint identifier is derived from the topic identifier:
                // a repeat export has to arrive at the same one
                Guid = ViewpointGuidFor(topicGuid, discriminator),
                Camera = data.Camera,
                Snapshot = data.Snapshot,
                Index = index
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

            // Visibility is given as explicit lists rather than as the
            // Navisworks translucent dimming: elsewhere that dimming is shown
            // differently
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
        /// The project identifier out of the document path — deterministic, so
        /// that repeat exports of one file land in one project.
        ///
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

            // Ungrouped clashes are not tipped into one heap: each gets a topic
            // of its own, or they would be lost inside a common group
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
            // Progress on every clash means thousands of marshalling hops onto
            // the UI thread
            if (state.ProcessedClashes % 25 == 0) Report(progress, state);
        }
    }
}
