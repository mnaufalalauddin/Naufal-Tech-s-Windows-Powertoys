using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed record TaskActivityEntry(
        long Sequence,
        string Title,
        string State,
        string Resources,
        DateTimeOffset StartedAt,
        DateTimeOffset? CompletedAt,
        string Detail)
    {
        public TimeSpan Elapsed(DateTimeOffset now) =>
            (CompletedAt ?? now) - StartedAt;
    }

    /// <summary>
    /// Owns task history and the reference-compatible resource-lock scheduler.
    /// Tasks without resources can run together. Conflicting tasks are queued
    /// in sequence order, and duplicate active task ids are rejected.
    /// </summary>
    internal sealed class TaskActivityService
    {
        private readonly object _sync = new();
        private readonly List<TaskActivityEntry> _entries = new();
        private readonly List<ManagedTask> _queue = new();
        private readonly Dictionary<string, ManagedTask> _activeById =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ManagedTask> _locks =
            new(StringComparer.OrdinalIgnoreCase);
        private long _sequence;
        private long? _legacyActiveSequence;

        public bool HasRunningTask
        {
            get
            {
                lock (_sync)
                {
                    return _activeById.Values.Any(task => task.State == "RUNNING") ||
                        _legacyActiveSequence.HasValue;
                }
            }
        }

        public bool HasActiveTask
        {
            get
            {
                lock (_sync)
                {
                    return _activeById.Count > 0 || _legacyActiveSequence.HasValue;
                }
            }
        }

        public string HeaderStatus
        {
            get
            {
                lock (_sync)
                {
                    int running = _activeById.Values.Count(task => task.State == "RUNNING");
                    int queued = _activeById.Values.Count(task => task.State == "QUEUED");
                    return running == 0 && queued == 0
                        ? "TASKS: IDLE"
                        : $"TASKS: {running} RUNNING | QUEUE {queued}";
                }
            }
        }

        /// <summary>
        /// Acquires logical resources without blocking the UI. A null lease means
        /// the same task id is already running or waiting in the queue.
        /// </summary>
        public Task<TaskActivityLease?> AcquireAsync(
            string id,
            string title,
            IEnumerable<string>? resources,
            string initialDetail = "Starting...")
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(id);
            ArgumentException.ThrowIfNullOrWhiteSpace(title);

            Task<TaskActivityLease?>? queuedTask = null;
            TaskActivityLease? grantedLease = null;

            lock (_sync)
            {
                if (_activeById.ContainsKey(id))
                {
                    return Task.FromResult<TaskActivityLease?>(null);
                }

                ManagedTask task = new(
                    id,
                    title,
                    NormalizeResources(resources),
                    ++_sequence,
                    NormalizeActiveDetail(initialDetail, title));
                _activeById.Add(task.Id, task);
                AddOrReplaceEntryLocked(task.ToEntry());
                TrimHistoryLocked();

                if (HasConflictLocked(task))
                {
                    task.State = "QUEUED";
                    task.QueuedAt = DateTimeOffset.Now;
                    task.Detail = BuildWaitingDetailLocked(task);
                    AddOrReplaceEntryLocked(task.ToEntry());
                    _queue.Add(task);
                    queuedTask = task.Granted.Task;
                }
                else
                {
                    grantedLease = GrantLocked(task);
                }
            }

            return queuedTask ?? Task.FromResult(grantedLease);
        }

        public void ObserveStatus(string? rawStatus)
        {
            string status = rawStatus?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(status) ||
                !status.StartsWith("TASKS", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            lock (_sync)
            {
                // Managed tasks own their state. Dashboard label changes must not
                // create duplicate activity records for the same operation.
                if (_activeById.Count > 0)
                {
                    return;
                }

                if (status.Equals("TASKS: IDLE", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteLegacyLocked("COMPLETED", "Returned to idle.");
                    return;
                }

                if (status.Equals("TASKS: COMPLETE", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteLegacyLocked("COMPLETED", "Completed and returned control to the application.");
                    return;
                }

                if (status.Equals("TASKS: WARNING", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteLegacyLocked("WARNING", "Completed with one or more warnings.");
                    return;
                }

                if (status.Equals("TASKS: FAILED", StringComparison.OrdinalIgnoreCase))
                {
                    CompleteLegacyLocked("FAILED", "The operation reported a failure.");
                    return;
                }

                string title = NormalizeTitle(status);
                if (_legacyActiveSequence.HasValue &&
                    FindEntryLocked(_legacyActiveSequence.Value) is TaskActivityEntry current &&
                    current.State == "RUNNING" &&
                    string.Equals(current.Title, title, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                CompleteLegacyLocked(
                    "COMPLETED",
                    "The task advanced to another application stage.");
                long sequence = ++_sequence;
                AddOrReplaceEntryLocked(new TaskActivityEntry(
                    sequence,
                    title,
                    "RUNNING",
                    ClassifyResources(title),
                    DateTimeOffset.Now,
                    null,
                    $"Running: {title}"));
                _legacyActiveSequence = sequence;
                TrimHistoryLocked();
            }
        }

        // Keep the diagnostic history separate from the running-only monitor.
        public IReadOnlyList<TaskActivityEntry> RunningSnapshot()
        {
            lock (_sync)
            {
                return _entries
                    .Where(entry => entry.State == "RUNNING" && entry.CompletedAt is null)
                    .OrderByDescending(entry => entry.Sequence)
                    .ToArray();
            }
        }

        public IReadOnlyList<TaskActivityEntry> Snapshot()
        {
            lock (_sync)
            {
                HashSet<long> recentCompleted = _entries
                    .Where(entry => entry.State is not ("RUNNING" or "QUEUED"))
                    .OrderByDescending(entry => entry.Sequence)
                    .Take(30).Select(entry => entry.Sequence).ToHashSet();
                return _entries
                    .Where(entry => entry.State is "RUNNING" or "QUEUED" ||
                        recentCompleted.Contains(entry.Sequence))
                    .OrderByDescending(entry => entry.Sequence)
                    .ToArray();
            }
        }

        private TaskActivityLease GrantLocked(ManagedTask task)
        {
            bool wasQueued = task.State == "QUEUED";
            task.State = "RUNNING";
            task.StartedAt = DateTimeOffset.Now;
            task.Detail = wasQueued
                ? $"Starting: {task.Title}"
                : NormalizeActiveDetail(task.Detail, task.Title);
            foreach (string resource in task.Resources)
            {
                _locks[resource] = task;
            }

            AddOrReplaceEntryLocked(task.ToEntry());
            TaskActivityLease lease = new(this, task);
            task.Granted.TrySetResult(lease);
            return lease;
        }

        private void Complete(ManagedTask task, string state, string detail)
        {
            lock (_sync)
            {
                if (!_activeById.TryGetValue(task.Id, out ManagedTask? current) ||
                    !ReferenceEquals(current, task))
                {
                    return;
                }

                task.State = state;
                task.Detail = NormalizeTerminalDetail(state, detail);
                task.CompletedAt = DateTimeOffset.Now;
                foreach (string resource in task.Resources)
                {
                    if (_locks.TryGetValue(resource, out ManagedTask? owner) &&
                        ReferenceEquals(owner, task))
                    {
                        _locks.Remove(resource);
                    }
                }

                _activeById.Remove(task.Id);
                AddOrReplaceEntryLocked(task.ToEntry());
                GrantQueuedTasksLocked();
                TrimHistoryLocked();
            }
        }

        private void GrantQueuedTasksLocked()
        {
            while (true)
            {
                ManagedTask? next = _queue
                    .OrderBy(task => task.Sequence)
                    .FirstOrDefault(task => !HasConflictLocked(task));
                if (next is null)
                {
                    return;
                }

                _queue.Remove(next);
                GrantLocked(next);

                foreach (ManagedTask queued in _queue)
                {
                    queued.Detail = BuildWaitingDetailLocked(queued);
                    AddOrReplaceEntryLocked(queued.ToEntry());
                }
            }
        }

        private bool HasConflictLocked(ManagedTask task) =>
            task.Resources.Any(resource => _locks.ContainsKey(resource)) ||
            _queue.Any(earlier => earlier.Sequence < task.Sequence &&
                earlier.Resources.Intersect(task.Resources, StringComparer.OrdinalIgnoreCase).Any());

        private string BuildWaitingDetailLocked(ManagedTask task)
        {
            string[] owners = task.Resources
                .Where(resource => _locks.TryGetValue(resource, out _))
                .Select(resource => _locks[resource].Title)
                .Concat(_queue.Where(earlier => earlier.Sequence < task.Sequence &&
                    earlier.Resources.Intersect(task.Resources, StringComparer.OrdinalIgnoreCase).Any())
                    .Select(earlier => earlier.Title))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return owners.Length == 0
                ? "Waiting: queued for available resources."
                : "Waiting for: " + string.Join(", ", owners);
        }

        private void CompleteLegacyLocked(string state, string detail)
        {
            if (!_legacyActiveSequence.HasValue)
            {
                return;
            }

            TaskActivityEntry? active = FindEntryLocked(_legacyActiveSequence.Value);
            if (active is not null && active.State == "RUNNING")
            {
                AddOrReplaceEntryLocked(active with
                {
                    State = state,
                    CompletedAt = DateTimeOffset.Now,
                    Detail = NormalizeTerminalDetail(state, detail)
                });
            }
            _legacyActiveSequence = null;
        }

        private TaskActivityEntry? FindEntryLocked(long sequence) =>
            _entries.FirstOrDefault(entry => entry.Sequence == sequence);

        private void AddOrReplaceEntryLocked(TaskActivityEntry entry)
        {
            int existingIndex = _entries.FindIndex(item => item.Sequence == entry.Sequence);
            if (existingIndex >= 0)
            {
                _entries[existingIndex] = entry;
            }
            else
            {
                _entries.Add(entry);
            }
        }

        private void TrimHistoryLocked()
        {
            const int retainedEntries = 60;
            if (_entries.Count <= retainedEntries)
            {
                return;
            }

            HashSet<long> activeSequences = _activeById.Values
                .Select(task => task.Sequence)
                .ToHashSet();
            if (_legacyActiveSequence.HasValue)
            {
                activeSequences.Add(_legacyActiveSequence.Value);
            }

            foreach (TaskActivityEntry item in _entries
                .OrderBy(entry => entry.Sequence)
                .Where(entry => !activeSequences.Contains(entry.Sequence))
                .Take(Math.Max(0, _entries.Count - retainedEntries))
                .ToArray())
            {
                _entries.RemoveAll(entry => entry.Sequence == item.Sequence);
            }
        }

        private static string[] NormalizeResources(IEnumerable<string>? resources) =>
            (resources ?? Array.Empty<string>())
                .Where(resource => !string.IsNullOrWhiteSpace(resource))
                .Select(resource => resource.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

        private static readonly string[] ActiveDetailVerbs =
        {
            "Applying and verifying", "Running verified",
            "Starting", "Waiting", "Checking", "Analyzing", "Reading",
            "Collecting", "Detecting", "Downloading", "Installing", "Creating",
            "Opening", "Launching", "Running", "Applying", "Restoring",
            "Repairing", "Verifying", "Saving", "Stopping", "Removing",
            "Enabling", "Disabling"
        };

        private static string NormalizeActiveDetail(string? detail, string title)
        {
            string text = detail?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text) ||
                text.Equals("Starting...", StringComparison.OrdinalIgnoreCase) ||
                text.Equals("In progress...", StringComparison.OrdinalIgnoreCase))
            {
                return $"Starting: {title}";
            }

            foreach (string verb in ActiveDetailVerbs)
            {
                if (text.StartsWith(verb + ":", StringComparison.OrdinalIgnoreCase))
                {
                    return verb + text[verb.Length..];
                }
                if (text.StartsWith(verb + " ", StringComparison.OrdinalIgnoreCase))
                {
                    return $"{verb}: {text[(verb.Length + 1)..]}";
                }
            }

            return $"Running: {text}";
        }

        private static string NormalizeTerminalDetail(string state, string? detail)
        {
            string verb = state.ToUpperInvariant() switch
            {
                "COMPLETED" => "Completed",
                "WARNING" => "Completed with warnings",
                "FAILED" => "Failed",
                "INTERRUPTED" => "Interrupted",
                _ => "Finished"
            };
            string text = detail?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(text)
                ? verb + "."
                : $"{verb}: {text}";
        }

        private static string NormalizeTitle(string status)
        {
            int separator = status.IndexOf(':');
            string value = separator >= 0 ? status[(separator + 1)..] : status[5..];
            value = value.Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                return "Application task";
            }

            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(
                value.ToLowerInvariant());
        }

        private static string ClassifyResources(string title)
        {
            string value = title.ToUpperInvariant();
            if (value.Contains("REPORT", StringComparison.Ordinal) ||
                value.Contains("LICENSE", StringComparison.Ordinal) ||
                value.Contains("READING", StringComparison.Ordinal) ||
                value.Contains("INVENTORY", StringComparison.Ordinal) ||
                value.Contains("CHECK", StringComparison.Ordinal) ||
                value.Contains("PREVIEW", StringComparison.Ordinal) ||
                value.Contains("LEGACY PANELS", StringComparison.Ordinal))
            {
                return "Read-only / independent";
            }

            return "System mutation (serialized)";
        }

        internal sealed class ManagedTask
        {
            public ManagedTask(
                string id,
                string title,
                string[] resources,
                long sequence,
                string detail)
            {
                Id = id;
                Title = title;
                Resources = resources;
                Sequence = sequence;
                Detail = detail;
                Granted = new TaskCompletionSource<TaskActivityLease?>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public string Id { get; }
            public string Title { get; }
            public string[] Resources { get; }
            public long Sequence { get; }
            public TaskCompletionSource<TaskActivityLease?> Granted { get; }
            public string State { get; set; } = "PENDING";
            public string Detail { get; set; }
            public DateTimeOffset? QueuedAt { get; set; }
            public DateTimeOffset? StartedAt { get; set; }
            public DateTimeOffset? CompletedAt { get; set; }

            public TaskActivityEntry ToEntry() => new(
                Sequence,
                Title,
                State,
                Resources.Length == 0
                    ? "Read-only / independent"
                    : string.Join(", ", Resources),
                StartedAt ?? QueuedAt ?? DateTimeOffset.Now,
                CompletedAt,
                Detail);
        }

        internal sealed class TaskActivityLease : IDisposable
        {
            private readonly TaskActivityService _owner;
            private readonly ManagedTask _task;
            private bool _completed;

            internal TaskActivityLease(TaskActivityService owner, ManagedTask task)
            {
                _owner = owner;
                _task = task;
            }

            public void UpdateDetail(string detail)
            {
                lock (_owner._sync)
                {
                    if (_completed ||
                        !_owner._activeById.TryGetValue(_task.Id, out ManagedTask? current) ||
                        !ReferenceEquals(current, _task))
                    {
                        return;
                    }

                    _task.Detail = NormalizeActiveDetail(detail, _task.Title);
                    _owner.AddOrReplaceEntryLocked(_task.ToEntry());
                }
            }

            public void Complete(string state, string detail)
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                _owner.Complete(_task, state, detail);
            }

            public void Dispose() => Complete("INTERRUPTED", "Task ended without an explicit completion result.");
        }
    }
}
