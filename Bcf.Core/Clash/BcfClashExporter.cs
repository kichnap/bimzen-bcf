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

        /// <summary>Обновляемый архив; null — выгрузка пишет файл с нуля.</summary>
        private ExistingBcfArchive _existing;

        /// <summary>
        /// Ключи, уже выданные в этой выгрузке. Одна и та же пара элементов
        /// сталкивается дважды чаще, чем кажется: на живой проверке из 1391
        /// замечания таких оказалось 26. Без учёта повторов оба замечания
        /// получили бы один идентификатор и одну папку в архиве.
        /// </summary>
        private readonly HashSet<string> _usedKeys = new HashSet<string>(StringComparer.Ordinal);

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
            return Export(destination, null, settings, progress, cancellationToken);
        }

        /// <summary>
        /// Пишет архив, обновляя существующий.
        /// </summary>
        /// <param name="destination">Поток нового архива.</param>
        /// <param name="existingArchive">
        /// Обновляемый файл, открытый на чтение. Пишем всегда в новый поток,
        /// а не поверх исходного: прерванная запись поверх оставила бы
        /// пользователя без обеих версий файла.
        /// </param>
        /// <param name="settings">Настройки экспорта.</param>
        /// <param name="progress">Приёмник прогресса; может быть null.</param>
        /// <param name="cancellationToken">Токен отмены.</param>
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
                // Наружу исключение не выпускаем: в Navisworks необработанная
                // ошибка в обработчике команды роняет всё приложение
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
                    // Замечания, которых выгрузка не касалась: коллизия разобрана
                    // и в проверку больше не попадает — из файла она пропасть
                    // не должна
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

                // Смешивать версии в одном архиве нельзя, а молча переключить
                // выбранную пользователем версию значит соврать о том,
                // что мы записали
                throw new InvalidOperationException(
                    "Файл выгрузки записан в формате BCF " + existing.Version.ToVersionId() +
                    ", а выбрана версия " + settings.Version.ToVersionId() +
                    ". Выберите ту же версию или сохраните выгрузку в новый файл.");
            }

            foreach (string warning in existing.Warnings)
            {
                result.Warn("Обновляемый файл: " + warning);
            }

            return existing;
        }

        /// <summary>
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
                // Точку зрения, добавленную в приёмнике, перезапись потеряла бы
                if (_existing.CopyTopic(topic.Guid, writer))
                {
                    result.TopicsKept++;
                    result.Warn(
                        "Замечание «" + existing.Title + "» перенесено без изменений: " +
                        "в нём есть точки зрения из приёмника, которых выгрузка не хранит.");

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
        /// Решает судьбу замечания, которое уже есть в обновляемом файле:
        /// перенести как есть или переписать нашими данными.
        ///
        /// Вызывается до сборки замечания и до снятия кадра: в режиме
        /// «только добавить» повторная выгрузка не должна заново рисовать
        /// пять тысяч снимков ради того, чтобы их выбросить.
        /// </summary>
        /// <returns>true — замечание перенесено, своё строить не нужно.</returns>
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
                    "Замечание «" + existing.Title + "» перенесено без изменений: в нём есть " + foreign +
                    ", чего выгрузка не хранит и при перезаписи потеряла бы.");
            }

            result.TopicsKept++;

            return true;
        }

        /// <summary>
        /// Чужие данные замечания, которых наша модель не хранит. Null —
        /// замечание можно переписать без потерь.
        /// </summary>
        private static string ForeignData(BcfTopic existing)
        {
            return existing.UnsupportedData.Count > 0
                ? string.Join(", ", Enumerable.ToArray(existing.UnsupportedData))
                : null;
        }

        /// <summary>
        /// Есть ли в существующем замечании точки зрения, которых нет у нас.
        ///
        /// Проверяется после сборки замечания, а не до: своих точек зрения
        /// может быть много — по одной на каждую коллизию группы, — и их
        /// идентификаторы известны только когда группа уже разобрана.
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
        /// Переносит в наше замечание то, что могло измениться у приёмника.
        /// </summary>
        private static void Merge(BcfTopic fresh, BcfTopic existing, BcfExportSettings settings)
        {
            // Замечание создано тогда, а не сейчас: дата и автор создания
            // принадлежат первой выгрузке
            if (existing.CreationDate != default(DateTimeOffset)) fresh.CreationDate = existing.CreationDate;
            if (!string.IsNullOrWhiteSpace(existing.CreationAuthor)) fresh.CreationAuthor = existing.CreationAuthor;

            // Номер, выданный сервером, переживает любую перезапись
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
        /// Идентификатор нашей точки зрения — он выводится из замечания
        /// и, для точек зрения на отдельные коллизии, из ключа коллизии.
        /// </summary>
        private static Guid ViewpointGuidFor(Guid topicGuid, string discriminator = null)
        {
            string[] parts = string.IsNullOrEmpty(discriminator)
                ? new[] { "viewpoint", topicGuid.ToString("D") }
                : new[] { "viewpoint", topicGuid.ToString("D"), discriminator };

            return StableTopicKey.ToTopicGuid(StableTopicKey.Compute(parts));
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
                    // Ключ по идентификатору вида: переименование вида и переезд
                    // между папками не должны создавать второе замечание
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
                Guid = ViewpointGuidFor(topicGuid),
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

            // Якоря групп: первое замечание группы, на которое ссылаются
            // остальные. Живут в пределах проверки — группы тоже
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

                // Коллизия, не попавшая ни в одну группу, — сама себе группа,
                // и «имя группы» у неё это имя коллизии вида «Столкновение123».
                // Такие имена Navisworks раздаёт заново при пересоздании
                // проверки, поэтому ключ для них считается по элементам:
                // иначе замечание теряет себя ровно там, где его ищут
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

                // Решение принимается до снятия кадра: рисовать снимок,
                // который тут же будет выброшен, — самая дорогая ошибка,
                // какую может допустить повторная выгрузка
                if (KeepExisting(topicGuid, writer, settings, result)) return;

                if (settings.GroupNameAsLabel && !string.IsNullOrWhiteSpace(clashes[0].GroupName))
                {
                    // Имя группы не из справочника, и файл обязан объявить его
                    // сам — иначе строгая проверка не пропустит запись
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
                // Ошибка на одной коллизии не должна останавливать выгрузку:
                // записываем в отчёт, пропускаем, идём дальше
                result.ClashesSkippedByError++;
                result.Warn("Замечание '" + title + "' пропущено: " + ex.Message);
            }
        }

        /// <summary>
        /// Копит статистику по тому, откуда брались числовые идентификаторы.
        /// </summary>
        private static void CountIdSources(ClashItem clash, BcfExportResult result)
        {
            foreach (ClashElementInfo element in clash.Elements)
            {
                string source = string.IsNullOrWhiteSpace(element.ElementIdSource)
                    ? "не найден"
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
        /// Разводит ключи, совпавшие в пределах одной выгрузки.
        ///
        /// Две коллизии между одной и той же парой элементов — обычное дело:
        /// труба пересекает стену дважды. Ключ у них один, а замечания должны
        /// быть разными, иначе второе затрёт первое прямо в архиве.
        /// Порядок обхода Navisworks устойчив, поэтому и номер повтора
        /// устойчив от выгрузки к выгрузке.
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
        /// Ключ коллизии — имя проверки и идентификаторы её элементов.
        /// Ни имени коллизии, ни её номера: их Navisworks раздаёт заново.
        /// </summary>
        private static string ClashKey(ClashItem clash)
        {
            return StableTopicKey.ForClash(clash.TestName, clash.Elements.Select(e => e.IfcGuid ?? e.ElementId));
        }

        /// <summary>
        /// Связывает поштучные замечания одной группы через RelatedTopics.
        ///
        /// Связь односторонняя, звездой на первое замечание группы: архив
        /// пишется потоком, и когда приходит второе, первое уже записано.
        /// Обратная ссылка стоила бы держать все замечания в памяти до конца
        /// выгрузки — цена, несоразмерная удобству.
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
        /// Добавляет точку зрения на каждую коллизию группы.
        ///
        /// Это единственный способ сохранить пары: в плоском списке
        /// компонентов замечания элемент, участвующий в трёх коллизиях,
        /// лежит один раз, и разбить список обратно на пары нельзя даже
        /// в теории. В точке зрения — ровно два компонента одной коллизии.
        ///
        /// Снимки здесь не снимаются: секунда на кадр против пары килобайт
        /// XML — разница, из-за которой выгрузка группы из шестидесяти
        /// коллизий превратилась бы в минуту работы и десяток мегабайт.
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
        /// Идентификатор замечания: ранее выданный, если он известен, иначе
        /// детерминированный из ключа. Первое важнее второго — на сервере
        /// у топика может оказаться свой Guid, и повторная выгрузка обязана
        /// попасть в тот же топик, а не создать рядом второй.
        /// </summary>
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
