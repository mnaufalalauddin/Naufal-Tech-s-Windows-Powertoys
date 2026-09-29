using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Naufal_Windows_Tech_s_Powertoys;

internal sealed class WindowsSecurityControlsService : IToolToggleService
{
    private const string BackupRoot = @"Software\Naufal Windows Tech\Powertoys\Backups\SecurityControls";
    private const string UacPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string ExplorerPolicy = @"SOFTWARE\Policies\Microsoft\Windows\System";
    private const string EdgePolicy = @"SOFTWARE\Policies\Microsoft\Edge";
    private readonly NativeCommandRunner _runner = new();

    private static readonly IReadOnlyList<ToolToggleDefinition> Definitions = new[]
    {
        Security("Firewall", "Windows Firewall",
            "Controls firewall state for Domain, Private and Public profiles. The firewall service is retained; disabling profiles is not treated as a reason to remove shared networking services.", restart: false),
        Security("SmartScreenWindows", "Microsoft Defender SmartScreen - Windows",
            "Controls Windows shell/app SmartScreen policy. This is separate from Microsoft Edge SmartScreen.", restart: false),
        Security("SmartScreenEdge", "Microsoft Defender SmartScreen - Edge",
            "Controls the Microsoft Edge SmartScreen policy independently from Windows shell SmartScreen.", restart: false),
        Security("UAC", "User Account Control (UAC)",
            "Controls EnableLUA. OFF is actual UAC disablement, not only notification behavior, and requires restart before the effective security model changes.", restart: true)
    };

    public IReadOnlyList<ToolToggleDefinition> GetDefinitions() => Definitions;

    public async Task<ToolToggleState> ReadStateAsync(ToolToggleDefinition definition)
    {
        try
        {
            return definition.Id switch
            {
                "Firewall" => await ReadFirewallAsync(),
                "SmartScreenWindows" => ReadDword(ExplorerPolicy, "EnableSmartScreen") switch
                {
                    0 => State(false, "Windows EnableSmartScreen=0"),
                    1 => State(true, "Windows EnableSmartScreen=1"),
                    _ => State(true, "Windows EnableSmartScreen=<not forced>; Windows default/user choice")
                },
                "SmartScreenEdge" => ReadDword(EdgePolicy, "SmartScreenEnabled") switch
                {
                    0 => State(false, "Edge SmartScreenEnabled=0"),
                    1 => State(true, "Edge SmartScreenEnabled=1"),
                    _ => State(true, "Edge SmartScreenEnabled=<not forced>; Edge default/user choice")
                },
                "UAC" => ReadDword(UacPath, "EnableLUA") switch
                {
                    0 => State(false, "EnableLUA=0"),
                    1 => State(true, "EnableLUA=1"),
                    int value => new ToolToggleState(false, false, $"EnableLUA={value}", "Unexpected UAC value."),
                    _ => new ToolToggleState(false, false, "EnableLUA=<absent>", "UAC effective state could not be established.")
                },
                _ => throw new InvalidOperationException($"Unknown security control: {definition.Id}")
            };
        }
        catch (Exception exception)
        {
            return new(false, false, "Unable to read", exception.Message);
        }
    }

    public async Task<ToolToggleOperationResult> SetStateAsync(ToolToggleDefinition definition, bool targetOn)
    {
        if (!WindowsPrivilegeService.IsAdministrator())
        {
            ToolToggleState denied = await ReadStateAsync(definition);
            return new(false, false, "Administrator rights are required.", denied);
        }

        ToolToggleState before = await ReadStateAsync(definition);
        if (!before.IsAvailable) return new(false, false, before.Error, before);
        try
        {
            await CaptureAsync(definition);
            switch (definition.Id)
            {
                case "Firewall":
                    await RequireNetshAsync("advfirewall", "set", "allprofiles", "state", targetOn ? "on" : "off");
                    break;
                case "SmartScreenWindows":
                    WriteDword(ExplorerPolicy, "EnableSmartScreen", targetOn ? 1 : 0);
                    break;
                case "SmartScreenEdge":
                    WriteDword(EdgePolicy, "SmartScreenEnabled", targetOn ? 1 : 0);
                    break;
                case "UAC":
                    WriteDword(UacPath, "EnableLUA", targetOn ? 1 : 0);
                    break;
                default:
                    throw new InvalidOperationException($"Unknown security control: {definition.Id}");
            }

            ToolToggleState after = await ReadStateAsync(definition);
            bool matches = after.IsAvailable && after.IsOn == targetOn;
            bool pending = matches && definition.Id == "UAC";
            if (pending)
                after = after with { EffectiveState = ToolEffectiveState.PendingReboot, ActualValue = after.ActualValue + "; pending reboot" };
            return new(matches, matches && !pending,
                matches
                    ? pending ? "UAC configuration was written and read back; effective state requires restart."
                              : $"{definition.Name} state was changed and read back."
                    : $"Read-back did not match the requested state. Actual: {after.ActualValue}",
                after, VerificationPending: pending, RebootRequired: pending);
        }
        catch (Exception exception)
        {
            ToolToggleState state = await ReadStateAsync(definition);
            return new(false, false, exception.Message, state);
        }
    }

    public async Task<ToolToggleOperationResult> RestoreOriginalAsync(ToolToggleDefinition definition)
    {
        if (!WindowsPrivilegeService.IsAdministrator())
        {
            ToolToggleState denied = await ReadStateAsync(definition);
            return new(false, false, "Administrator rights are required.", denied);
        }

        using RegistryKey? backup = Registry.CurrentUser.OpenSubKey(BackupRoot + "\\" + definition.Id);
        if (backup is null)
        {
            ToolToggleState state = await ReadStateAsync(definition);
            return new(false, false, "No captured pre-change state exists.", state, OriginalBackupMissing: true);
        }

        try
        {
            if (definition.Id == "Firewall")
            {
                foreach (string profile in new[] { "domainprofile", "privateprofile", "publicprofile" })
                {
                    int state = Convert.ToInt32(backup.GetValue(profile, -1), CultureInfo.InvariantCulture);
                    if (state is 0 or 1) await RequireNetshAsync("advfirewall", "set", profile, "state", state == 1 ? "on" : "off");
                }
            }
            else
            {
                (string path, string name) = Target(definition.Id);
                bool existed = Convert.ToInt32(backup.GetValue("Existed", 0), CultureInfo.InvariantCulture) == 1;
                using RegistryKey key = Registry.LocalMachine.CreateSubKey(path, writable: true);
                if (existed) key.SetValue(name, Convert.ToInt32(backup.GetValue("Value", 0), CultureInfo.InvariantCulture), RegistryValueKind.DWord);
                else key.DeleteValue(name, throwOnMissingValue: false);
            }

            ToolToggleState after = await ReadStateAsync(definition);
            bool pending = definition.Id == "UAC";
            Registry.CurrentUser.DeleteSubKeyTree(BackupRoot + "\\" + definition.Id, throwOnMissingSubKey: false);
            if (pending) after = after with { EffectiveState = ToolEffectiveState.PendingReboot, ActualValue = after.ActualValue + "; original configuration restored; pending reboot" };
            return new(true, !pending,
                pending ? "Original UAC configuration was restored; effective state requires restart."
                        : $"Original {definition.Name} state was restored and read back.",
                after, VerificationPending: pending, RebootRequired: pending);
        }
        catch (Exception exception)
        {
            ToolToggleState state = await ReadStateAsync(definition);
            return new(false, false, exception.Message, state);
        }
    }

    public Task<ToolToggleOperationResult> RestoreWindowsDefaultAsync(ToolToggleDefinition definition) =>
        Task.FromResult(new ToolToggleOperationResult(false, false,
            "Use Restore saved state. Organization policy and Windows defaults can differ, so this control does not guess a universal default.",
            new ToolToggleState(false, true, "No universal default")));

    private async Task CaptureAsync(ToolToggleDefinition definition)
    {
        string path = BackupRoot + "\\" + definition.Id;
        using RegistryKey backup = Registry.CurrentUser.CreateSubKey(path, writable: true);
        if (Convert.ToInt32(backup.GetValue("Captured", 0), CultureInfo.InvariantCulture) == 1) return;
        if (definition.Id == "Firewall")
        {
            ToolToggleState state = await ReadFirewallAsync();
            Dictionary<string, bool> profiles = ParseFirewallProfiles(state.ActualValue);
            foreach ((string profile, bool enabled) in profiles)
                backup.SetValue(profile, enabled ? 1 : 0, RegistryValueKind.DWord);
        }
        else
        {
            (string targetPath, string name) = Target(definition.Id);
            int? value = ReadDword(targetPath, name);
            backup.SetValue("Existed", value.HasValue ? 1 : 0, RegistryValueKind.DWord);
            if (value.HasValue) backup.SetValue("Value", value.Value, RegistryValueKind.DWord);
        }
        backup.SetValue("Captured", 1, RegistryValueKind.DWord);
    }

    private async Task<ToolToggleState> ReadFirewallAsync()
    {
        NativeCommandResult result = await _runner.RunAsync("netsh.exe",
            new[] { "advfirewall", "show", "allprofiles", "state" }, TimeSpan.FromSeconds(15));
        if (result.ExitCode != 0 || result.TimedOut)
            return new(false, false, "Firewall state unavailable", result.StandardError);
        Dictionary<string, bool> profiles = ParseFirewallProfiles(result.StandardOutput);
        if (profiles.Count != 3)
            return new(false, false, result.StandardOutput.Trim(), "Could not establish all three firewall profile states.");
        bool allOn = profiles.Values.All(value => value);
        return State(allOn, string.Join("; ", profiles.Select(pair => pair.Key + "=" + (pair.Value ? "ON" : "OFF"))));
    }

    internal static Dictionary<string, bool> ParseFirewallProfiles(string output)
    {
        Dictionary<string, bool> result = new(StringComparer.OrdinalIgnoreCase);
        string current = "";
        foreach (string raw in output.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.StartsWith("Domain Profile", StringComparison.OrdinalIgnoreCase)) current = "domainprofile";
            else if (line.StartsWith("Private Profile", StringComparison.OrdinalIgnoreCase)) current = "privateprofile";
            else if (line.StartsWith("Public Profile", StringComparison.OrdinalIgnoreCase)) current = "publicprofile";
            else if (current.Length > 0 && line.StartsWith("State", StringComparison.OrdinalIgnoreCase))
            {
                string value = line.Substring(5).Trim();
                if (value.Equals("ON", StringComparison.OrdinalIgnoreCase) || value.Equals("OFF", StringComparison.OrdinalIgnoreCase))
                    result[current] = value.Equals("ON", StringComparison.OrdinalIgnoreCase);
            }
        }
        return result;
    }

    private async Task RequireNetshAsync(params string[] args)
    {
        NativeCommandResult result = await _runner.RunAsync("netsh.exe", args, TimeSpan.FromSeconds(20));
        if (result.ExitCode != 0 || result.TimedOut)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError);
    }

    private static ToolToggleState State(bool enabled, string actual) =>
        new(enabled, true, actual, EffectiveState: enabled ? ToolEffectiveState.Active : ToolEffectiveState.Inactive);

    private static ToolToggleDefinition Security(string id, string name, string description, bool restart) =>
        new(id, "Windows Security", name, description, true, restart, ToolToggleTier.Advanced,
            Warning: "Disabling this protection materially reduces Windows security. No performance gain is assumed.",
            IsFeatureSwitch: true,
            CanonicalActionId: "security.control." + id.ToLowerInvariant(),
            Impact: ToolActionImpact.AdvancedSecurityMitigation);

    private static (string Path, string Name) Target(string id) => id switch
    {
        "SmartScreenWindows" => (ExplorerPolicy, "EnableSmartScreen"),
        "SmartScreenEdge" => (EdgePolicy, "SmartScreenEnabled"),
        "UAC" => (UacPath, "EnableLUA"),
        _ => throw new InvalidOperationException($"No registry target for {id}.")
    };

    private static int? ReadDword(string path, string name)
    {
        using RegistryKey? key = Registry.LocalMachine.OpenSubKey(path, writable: false);
        object? value = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return value is null ? null : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    private static void WriteDword(string path, string name, int value)
    {
        using RegistryKey key = Registry.LocalMachine.CreateSubKey(path, writable: true);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }
}
