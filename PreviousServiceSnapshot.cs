using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed record PreviousServiceSnapshot(string Name, int Start, int? Delayed, bool WasRunning)
{
    internal bool AlreadyRestored { get; init; }
    private const string ReceiptRoot = @"Software\Naufal Windows Tech\Powertoys\Migrations\PreviousServiceSnapshots";
    // Per-entry identity: changes to unrelated JSON entries must not resurrect a
    // snapshot already restored by the native app. The source is never modified.
    internal string Fingerprint => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
        FormattableString.Invariant($"{Name.ToUpperInvariant()}|{Start}|{Delayed?.ToString(CultureInfo.InvariantCulture) ?? "absent"}|{WasRunning}"))));

    internal static PreviousServiceSnapshot? ReadForRestore(string service)
    {
        string path = AppDataPaths.ResolveLegacyBackup("performance-lab-original.json");
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length > 8 * 1024 * 1024) throw new InvalidDataException("Previous service snapshot file exceeds the safety limit.");
            using JsonDocument document = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 32 });
            PreviousServiceSnapshot? saved = Parse(document.RootElement, service);
            if (saved is null) return null;
            using RegistryKey? receipts = Registry.CurrentUser.OpenSubKey(ReceiptRoot);
            return PrepareForRestore(saved, receipts?.GetValue(service) as string);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
        // Access-denied, malformed data and provider errors are NOT absence.
    }

    internal static bool IsConsumed(PreviousServiceSnapshot saved, string? receipt) => receipt == saved.Fingerprint;
    internal static PreviousServiceSnapshot PrepareForRestore(PreviousServiceSnapshot saved, string? receipt) =>
        saved with { AlreadyRestored = IsConsumed(saved, receipt) };

    internal void MarkRestored()
    {
        if (AlreadyRestored) return;
        using RegistryKey receipts = Registry.CurrentUser.CreateSubKey(ReceiptRoot, writable: true);
        receipts.SetValue(Name, Fingerprint, RegistryValueKind.String);
        receipts.Flush();
    }

    internal static PreviousServiceSnapshot? Parse(JsonElement root, string service)
    {
        if (root.ValueKind != JsonValueKind.Object) throw Invalid(service);
        JsonElement saved = default;
        int count = 0;
        foreach (JsonProperty property in root.EnumerateObject())
            if (property.Name.Equals("__KentangSvc_" + service, StringComparison.OrdinalIgnoreCase))
            { saved = property.Value; count++; }
        if (count == 0) return null;
        if (count != 1 || saved.ValueKind != JsonValueKind.Object) throw Invalid(service);
        if (!StringField(saved, "Name").Equals(service, StringComparison.OrdinalIgnoreCase) ||
            !StringField(saved, "RequestedName").Equals(service, StringComparison.OrdinalIgnoreCase)) throw Invalid(service);
        int? start = Dword(saved, "Start", service);
        int? delayed = Dword(saved, "DelayedAutoStart", service);
        JsonElement running = Field(saved, "WasRunning");
        if (start is not (2 or 3 or 4) || delayed is not (null or 0 or 1) ||
            running.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Invalid(service);
        return new(service, start.Value, delayed, running.GetBoolean());
    }

    private static int? Dword(JsonElement parent, string name, string service)
    {
        JsonElement saved = Field(parent, name), exists = Field(saved, "Exists");
        if (exists.ValueKind == JsonValueKind.False) return null;
        if (exists.ValueKind != JsonValueKind.True || StringField(saved, "Type") != "DWord") throw Invalid(service);
        JsonElement raw = Field(saved, "Value");
        if (raw.ValueKind != JsonValueKind.Number || !raw.TryGetInt32(out int value)) throw Invalid(service);
        return value;
    }

    private static string StringField(JsonElement parent, string name)
    {
        JsonElement value = Field(parent, name);
        return value.ValueKind == JsonValueKind.String ? value.GetString()! : throw Invalid(name);
    }

    private static JsonElement Field(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object) throw Invalid(name);
        int count = 0;
        JsonElement found = default;
        foreach (JsonProperty property in parent.EnumerateObject())
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) { found = property.Value; count++; }
        return count == 1 ? found : throw Invalid(name);
    }
    private static InvalidDataException Invalid(string label) => new("The previous service snapshot is incomplete or invalid: " + label + ". Restore was not started.");
}
