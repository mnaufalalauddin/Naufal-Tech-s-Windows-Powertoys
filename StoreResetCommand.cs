using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Naufal_Windows_Tech_s_Powertoys;

// Narrow bridge to the Windows Appx cmdlet used by the reference repair.
// The Windows App SDK reset API can be present but return E_NOTIMPL.
internal static partial class StoreResetCommand
{
    internal const string CompletedMarker = "WPT_STORE_RESET_COMPLETED";

    internal static string BuildScript(string packageFullName)
    {
        if (!StorePackagePattern().IsMatch(packageFullName))
            throw new ArgumentException("Only a Microsoft Store main-package full name may be reset.", nameof(packageFullName));
        return $$"""
            $ErrorActionPreference = 'Stop'
            $ProgressPreference = 'SilentlyContinue'
            [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
            try {
                $null = Get-Command -Name 'Appx\Reset-AppxPackage' -ErrorAction Stop
                $store = @(Appx\Get-AppxPackage -Name 'Microsoft.WindowsStore' -ErrorAction Stop | Where-Object { $_.PackageFullName -eq '{{packageFullName}}' })
                if ($store.Count -ne 1) { throw 'The selected Store package is no longer registered for the current user. Analyze again before retrying.' }
                Appx\Reset-AppxPackage -Package $store[0].PackageFullName -Confirm:$false -ErrorAction Stop
                # This marker confirms command completion, not package health.
                # Registration is restored in stage 5 and verified in stage 9.
                [Console]::WriteLine('{{CompletedMarker}}')
                exit 0
            }
            catch {
                [Console]::Error.WriteLine(('Reset-AppxPackage: {0} (HRESULT 0x{1:X8})' -f $_.Exception.Message, $_.Exception.HResult))
                exit 1
            }
            """;
    }

    internal static void ValidateResult(NativeCommandResult result)
    {
        if (result.TimedOut)
            throw new TimeoutException("Store reset timed out; completion is not confirmed. Do not start another package repair until it finishes.");
        if (result.ExitCode != 0 || !result.StandardOutput.Split('\r', '\n').Any(line => line == CompletedMarker))
            throw new InvalidOperationException($"Reset-AppxPackage did not confirm completion (exit {result.ExitCode}). {result.CombinedOutput}".Trim());
    }

    [GeneratedRegex(@"\AMicrosoft\.WindowsStore_\d+\.\d+\.\d+\.\d+_(?:x64|x86|arm64|arm|neutral)__8wekyb3d8bbwe\z", RegexOptions.CultureInvariant)]
    private static partial Regex StorePackagePattern();
}
