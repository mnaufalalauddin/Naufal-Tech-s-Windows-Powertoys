using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys
{
    internal readonly record struct NativeCommandResult(
        int ExitCode,
        string StandardOutput,
        string StandardError,
        bool TimedOut,
        TimeSpan Duration)
    {
        public string CombinedOutput
        {
            get
            {
                if (string.IsNullOrWhiteSpace(StandardError))
                {
                    return StandardOutput.Trim();
                }

                if (string.IsNullOrWhiteSpace(StandardOutput))
                {
                    return StandardError.Trim();
                }

                return $"{StandardOutput.TrimEnd()}{Environment.NewLine}{StandardError.Trim()}";
            }
        }
    }

    internal sealed class NativeCommandRunner
    {
        public async Task<NativeCommandResult> RunAsync(
            string executable,
            IReadOnlyList<string> arguments,
            TimeSpan timeoutValue,
            CancellationToken cancellationToken = default,
            IProgress<string>? outputProgress = null,
            Encoding? outputEncoding = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Zero (including a sub-millisecond value rounded down by the timer)
            // must not launch a process that is already due to be cancelled.
            // Infinite is intentional only for callers with an outer operation gate.
            if (timeoutValue != Timeout.InfiniteTimeSpan && timeoutValue.TotalMilliseconds < 1)
                throw new ArgumentOutOfRangeException(nameof(timeoutValue), "Use a positive timeout of at least one millisecond.");
            // Validate/start timeout before launching, so invalid arguments or
            // an already-cancelled request cannot leave a child process running.
            using CancellationTokenSource timeout = new(timeoutValue);
            using CancellationTokenSource combined = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                timeout.Token);
            ProcessStartInfo startInfo = new()
            {
                FileName = executable,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            foreach (string argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
            if (outputEncoding is not null)
            {
                startInfo.StandardOutputEncoding = outputEncoding;
                startInfo.StandardErrorEncoding = outputEncoding;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
            {
                return new NativeCommandResult(
                    -1,
                    string.Empty,
                    "The Windows process could not be started.",
                    false,
                    stopwatch.Elapsed);
            }

            SafeOutputProgress observer = new(outputProgress);
            Task<string> outputTask = ReadOutputAsync(process.StandardOutput, observer, combined.Token);
            Task<string> errorTask = ReadOutputAsync(process.StandardError, observer, combined.Token);

            bool timedOut = false;
            try
            {
                await process.WaitForExitAsync(combined.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                TryKill(process);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                await Task.WhenAll(outputTask, errorTask);
                throw;
            }

            await Task.WhenAll(outputTask, errorTask);
            cancellationToken.ThrowIfCancellationRequested();
            timedOut |= timeout.IsCancellationRequested;
            stopwatch.Stop();
            string output = await outputTask;
            string error = await errorTask;
            if (observer.Failure is not null)
                error = AppendMessage(error, "Progress reporting failed; command output was retained: " + observer.Failure.Message);
            return new NativeCommandResult(
                timedOut ? -1 : process.ExitCode,
                output,
                timedOut ? AppendMessage(error, "The operation timed out.") : error,
                timedOut,
                stopwatch.Elapsed);
        }

        private static void TryKill(Process process)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(5000);
                }
            }
            catch
            {
                // The process may already have exited between checks.
            }
        }

        // A failed UI/log observer must not stop draining redirected pipes:
        // otherwise the child can block on a full pipe until its timeout.
        // Preserve the real command exit/result, and disclose delivery failure.
        private sealed class SafeOutputProgress(IProgress<string>? target) : IProgress<string>
        {
            private readonly object _gate = new();
            internal Exception? Failure { get; private set; }
            public void Report(string value)
            {
                lock (_gate)
                {
                    if (Failure is not null || target is null) return;
                    try { target.Report(value); }
                    catch (Exception exception) { Failure = exception; }
                }
            }
        }

        private static async Task<string> ReadOutputAsync(StreamReader reader, IProgress<string>? progress, CancellationToken cancellationToken)
        {
            StringBuilder output = new();
            char[] buffer = new char[1024];
            int count;
            try
            {
                while ((count = await reader.ReadAsync(buffer.AsMemory(), cancellationToken)) > 0)
                {
                    string chunk = new(buffer, 0, count);
                    output.Append(chunk);
                    progress?.Report(chunk);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { /* Retain partial output on timeout; caller still propagates explicit cancellation. */ }
            return output.ToString();
        }

        private static string AppendMessage(string existing, string message)
        {
            return string.IsNullOrWhiteSpace(existing)
                ? message
                : $"{existing.TrimEnd()}{Environment.NewLine}{message}";
        }
    }
}
