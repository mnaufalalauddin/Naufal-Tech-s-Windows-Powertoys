using System;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal sealed record MaintenanceProgressUpdate(
        int StageIndex,
        int StageCount,
        string StageName,
        string Detail,
        double? StagePercent = null,
        string Status = "RUNNING");

    // Backend-owned status: a UI transition alone can never certify a stage.
    internal sealed class MaintenanceStageTracker : IProgress<MaintenanceProgressUpdate>
    {
        private readonly IProgress<MaintenanceProgressUpdate>? _target;
        private readonly Func<int> _warnings;
        private MaintenanceProgressUpdate? _active;
        private int _warningBaseline;
        private bool _stageFinished;
        private bool _completed;
        private readonly object _gate = new();
        public MaintenanceStageTracker(IProgress<MaintenanceProgressUpdate>? target, Func<int> warnings)
        { _target = target; _warnings = warnings; _warningBaseline = warnings(); }
        public void Report(MaintenanceProgressUpdate update)
        {
            lock (_gate)
            {
            if (_completed || update.StageIndex < 1 || update.StageIndex > update.StageCount ||
                (_active is not null && (update.StageIndex < _active.StageIndex ||
                    (update.StageIndex == _active.StageIndex && _stageFinished)))) return;
            if (_active is not null && update.StageIndex != _active.StageIndex)
            {
                FinishStage(true);
                _warningBaseline = _warnings();
                _stageFinished = false;
            }
            _active = update;
            if (update.Status != "RUNNING") _stageFinished = true;
            _target?.Report(update);
            }
        }
        public void Skip(string detail)
        {
            lock (_gate)
            {
            if (_completed || _stageFinished) return;
            if (_active is null) return;
            _stageFinished = true;
            _target?.Report(_active with { Status = "SKIPPED", Detail = detail, StagePercent = 100 });
            }
        }
        public void FinishStage(bool success)
        {
            lock (_gate)
            {
            if (_active is null || _stageFinished) return;
            _stageFinished = true;
            _target?.Report(_active with { Status = !success ? "FAILED" : _warnings() > _warningBaseline ? "WARNING" : "PASS", StagePercent = success ? 100 : _active.StagePercent });
            }
        }
        public void Complete(bool success)
        {
            lock (_gate)
            {
            if (_completed) return;
            FinishStage(success);
            _completed = true;
            if (!success && _active is not null)
                for (int index = _active.StageIndex + 1; index <= _active.StageCount; index++)
                    _target?.Report(new(index, _active.StageCount, "Not reached", "A previous stage failed.", null, "SKIPPED"));
            }
        }
    }

    internal static class MaintenanceProgress
    {
        public static void StartStage(
            IProgress<MaintenanceProgressUpdate>? progress,
            int index,
            int count,
            string name,
            string? detail = null)
        {
            progress?.Report(new MaintenanceProgressUpdate(
                index,
                count,
                name,
                string.IsNullOrWhiteSpace(detail) ? name : detail));
        }
    }
}
