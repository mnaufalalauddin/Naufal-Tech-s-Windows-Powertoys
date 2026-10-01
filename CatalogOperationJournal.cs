using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

// One durable file per operation; never replace a registry/service snapshot.
// Missing completion after a crash means Unknown, never inferred success.
internal static class CatalogOperationJournal
{
    internal static string DirectoryPath => Path.Combine(AppDataPaths.LocalRoot, "Logs", "CatalogOperations");

    internal static async Task<ToolToggleOperationResult> RunAsync(string directory, ToolToggleDefinition definition,
        CatalogOperation operation, Func<Task<ToolToggleOperationResult>> execute)
    {
        string path = Path.Combine(directory, DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + "-" + Guid.NewGuid().ToString("N") + ".json");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        // If the initial record cannot be saved, abort before calling the mutator.
        await Task.Run(() => Save(path, definition, operation, started, null, null));
        ToolToggleOperationResult result;
        CatalogStateEpoch.Invalidate();
        try { result = await execute(); }
        catch (Exception exception)
        {
            try { await Task.Run(() => Save(path, definition, operation, started, null, exception.Message)); }
            catch (Exception logError) { throw new AggregateException("Operation failed and final journal could not be saved. Inspect Windows state before retrying.", exception, logError); }
            throw;
        }
        finally { CatalogStateEpoch.Invalidate(); }
        try { await Task.Run(() => Save(path, definition, operation, started, result, null)); }
        catch (Exception exception)
        {
            // Preserve the actual outcome: reporting failure does not undo writes.
            return result with { Message = result.Message + "\nWARNING: Completion journal could not be saved: " + exception.Message + "\nStarted journal: " + path };
        }
        return result with { Message = result.Message + "\nJournal: " + path };
    }

    internal static string Outcome(ToolToggleDefinition definition, ToolToggleOperationResult result) =>
        result.SkippedUnavailable && result.State.IsConfirmedUnavailable ? "NotApplicable" :
        result.AlreadyApplied ? "AlreadyApplied" :
        result.Success && result.Verified ? definition.RestartRecommended ? "RebootRequired" : "Applied" :
        result.Success ? "VerificationPending" :
        result.State.HasReadFailure ? "Unknown" :
        result.State.HasAppliedParts ? "PartiallyApplied" : "Failed";

    private static void Save(string path, ToolToggleDefinition definition, CatalogOperation operation, DateTimeOffset started,
        ToolToggleOperationResult? result, string? error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporary = path + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var json = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
                {
                    json.WriteStartObject();
                    json.WriteNumber("SchemaVersion", 1);
                    json.WriteString("ActionId", definition.Id);
                    json.WriteString("Name", definition.Name);
                    json.WriteString("Operation", operation.ToString());
                    json.WriteString("StartedUtc", started);
                    json.WriteString("UpdatedUtc", DateTimeOffset.UtcNow);
                    json.WriteString("Outcome", result is { } value ? Outcome(definition, value) : "Unknown");
                    json.WriteString("Phase", result.HasValue || error is not null ? "Completed" : "Started");
                    json.WriteBoolean("RestartSensitive", definition.RestartRecommended);
                    json.WriteBoolean("EffectiveStateAfterRebootVerified", false);
                    json.WriteString("Error", error);
                    if (result is { } r)
                    {
                        json.WriteBoolean("Success", r.Success); json.WriteBoolean("ConfigurationVerified", r.Verified);
                        json.WriteString("Message", r.Message);
                        json.WriteString("Before", r.BeforeState?.ActualValue);
                        json.WriteString("After", r.State.ActualValue);
                        json.WriteBoolean("ReadFailure", r.State.HasReadFailure);
                    }
                    json.WriteEndObject(); json.Flush();
                }
                stream.Flush(true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            // Cleanup must not replace the primary write/operation exception.
            try { if (File.Exists(temporary)) File.Delete(temporary); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
