using Microsoft.UI.Xaml.Controls;

namespace Naufal_Windows_Tech_s_Powertoys;

public sealed partial class MainWindow
{
    private LiveMetricChart? _cpuChart, _ramChart, _gpuChart, _networkChart;

    // Keep the navigation label stable while existing operation handlers update
    // status. The scheduler still owns all running/queued task state.
    private string TaskStatusMessage
    {
        set => ToolTipService.SetToolTip(TaskStatusButton,
            value.StartsWith("TASKS: ", System.StringComparison.Ordinal)
                ? "Task Monitoring: " + value[7..] : value);
    }

    private void InitializeLiveCharts()
    {
        _cpuChart = new(0);
        _ramChart = new(1);
        _gpuChart = new(2);
        _networkChart = new(3, network: true);
        CpuGraphHost.Children.Add(_cpuChart.Root);
        RamGraphHost.Children.Add(_ramChart.Root);
        GpuGraphHost.Children.Add(_gpuChart.Root);
        NetworkGraphHost.Children.Add(_networkChart.Root);
    }

    private void AppendLiveCharts(SystemSnapshot? snapshot)
    {
        double time = _sessionTimer.Elapsed.TotalSeconds;
        _cpuChart?.Add(time, snapshot?.CpuPercent);
        _ramChart?.Add(time, snapshot is { MemoryTotalGigabytes: > 0 } memory ? memory.MemoryPercent : null);
        _gpuChart?.Add(time, snapshot?.GpuPercent);
        _networkChart?.Add(time, snapshot?.ReceiveMegabitsPerSecond, snapshot?.SendMegabitsPerSecond);
    }

    private void RenderLiveCharts()
    {
        _cpuChart?.Render();
        _ramChart?.Render();
        _gpuChart?.Render();
        _networkChart?.Render();
    }
}
