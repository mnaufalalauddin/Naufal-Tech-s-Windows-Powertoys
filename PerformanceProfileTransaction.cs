using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class PerformanceProfileTransaction
{
    internal static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "WindowsPowerToys", "PerformanceProfile_LastTransaction.json");

    internal static string Save(string path, string profile, bool success, double duration,
        bool restart, string? target, IReadOnlyDictionary<string, string> known,
        bool rollbackAttempted, bool? rollbackSucceeded, ProfileVerificationResult? verification, string? error)
    {
        string? temporary = null;
        try
        {
            string fullPath = Path.GetFullPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = true }))
                {
                    json.WriteStartObject();
                    json.WriteNumber("SchemaVersion", 2);
                    json.WriteString("Timestamp", DateTimeOffset.Now);
                    json.WriteString("Profile", profile);
                    json.WriteBoolean("Success", success);
                    json.WriteNumber("DurationSeconds", Math.Round(duration, 3));
                    json.WriteBoolean("RestartRequired", restart);
                    json.WriteString("TargetPowerGuid", target);
                    json.WriteStartObject("KnownPowerGuids");
                    foreach (var entry in known)
                        if (Array.IndexOf(PerformanceProfileVerification.Profiles, entry.Key) >= 0 && Guid.TryParse(entry.Value, out Guid guid))
                            json.WriteString(entry.Key, guid.ToString());
                    json.WriteEndObject();
                    json.WriteBoolean("RollbackAttempted", rollbackAttempted);
                    if (rollbackSucceeded.HasValue) json.WriteBoolean("RollbackSucceeded", rollbackSucceeded.Value);
                    else json.WriteNull("RollbackSucceeded");
                    json.WriteString("Error", error);
                    json.WriteString("VerificationProfile", verification?.Profile);
                    json.WriteNumber("Matched", verification?.Matched ?? 0);
                    json.WriteNumber("Total", verification?.Total ?? 0);
                    json.WriteStartArray("Checks");
                    foreach (var check in verification?.Checks ?? Array.Empty<ProfileVerificationCheck>())
                    {
                        json.WriteStartObject();
                        json.WriteString("Name", check.Name); json.WriteString("Expected", check.Expected);
                        json.WriteString("Actual", check.Actual); json.WriteBoolean("Pass", check.Pass);
                        json.WriteEndObject();
                    }
                    json.WriteEndArray(); json.WriteEndObject(); json.Flush();
                }
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, fullPath, overwrite: true);
            return "";
        }
        catch (Exception exception) { return "Transaction record could not be saved: " + exception.Message; }
        finally { if (temporary is not null && File.Exists(temporary)) { try { File.Delete(temporary); } catch { } } }
    }

    internal static string Format(ProfileVerificationResult? verification)
    {
        if (verification is null) return "Verification details unavailable.";
        StringBuilder text = new();
        text.AppendLine($"{verification.Profile}: {verification.Matched}/{verification.Total}");
        foreach (var check in verification.Checks)
            text.AppendLine($"[{(check.Pass ? "PASS" : "FAIL")}] {check.Name}: expected {check.Expected}; actual {check.Actual}");
        return text.ToString().TrimEnd();
    }
}
