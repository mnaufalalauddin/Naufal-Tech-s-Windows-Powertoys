using System.Text.Json;

namespace Naufal_Windows_Tech_s_Powertoys;

internal static class FirstRunWizardPolicy
{
    public static bool ShouldShow(string json, int currentSchema)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.TryGetProperty("Suppress", out var suppress) && suppress.ValueKind == JsonValueKind.True)
                return false;
            int schema = root.TryGetProperty("Schema", out var version) && version.TryGetInt32(out int value) ? value : 0;
            bool completed = root.TryGetProperty("Completed", out var completion) && completion.ValueKind == JsonValueKind.True;
            return schema < currentSchema || !completed;
        }
        catch (System.Exception) { return true; }
    }
}
