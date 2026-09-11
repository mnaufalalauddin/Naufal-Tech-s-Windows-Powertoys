[CmdletBinding()]
param(
    [string]$PublishDirectory,
    [string]$InstallerFile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$assetDirectory = Join-Path $projectRoot 'Assets'
$installerScript = Join-Path $projectRoot 'Installer\NaufalWindowsPowertoys.iss'
$identitySource = Join-Path $projectRoot 'ShellAppIdentity.cs'
$applicationSource = Join-Path $projectRoot 'App.xaml.cs'
$mainWindowSource = Join-Path $projectRoot 'MainWindow.xaml.cs'
$toolWindowSource = Join-Path $projectRoot 'ToolWindow.cs'
$iconBytes = [IO.File]::ReadAllBytes((Join-Path $assetDirectory 'NaufalWindowsPowertoys.ico'))
$stream = [IO.MemoryStream]::new($iconBytes, $false)
$reader = [IO.BinaryReader]::new($stream)
$frameHashes = [Collections.Generic.List[string]]::new()
$sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
$assertions = 0

function Assert-Icon([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw $Message }
    $script:assertions++
}

try {
    Assert-Icon ($reader.ReadUInt16() -eq 0) 'Invalid ICO reserved field.'
    Assert-Icon ($reader.ReadUInt16() -eq 1) 'Asset is not an ICO.'
    Assert-Icon ($reader.ReadUInt16() -eq $sizes.Count) 'Incorrect ICO frame count.'
    for ($index = 0; $index -lt $sizes.Count; $index++) {
        $stream.Position = 6 + 16 * $index
        $width = [int]$reader.ReadByte()
        $height = [int]$reader.ReadByte()
        if ($width -eq 0) { $width = 256 }
        if ($height -eq 0) { $height = 256 }
        [void]$reader.ReadUInt16()
        Assert-Icon ($reader.ReadUInt16() -eq 1) 'Invalid ICO planes.'
        Assert-Icon ($reader.ReadUInt16() -eq 32) 'Expected 32-bit icon.'
        $length = $reader.ReadUInt32()
        $offset = $reader.ReadUInt32()
        Assert-Icon ($length -gt 0 -and $offset -ge (6 + 16 * $sizes.Count) -and
            ([long]$offset + $length) -le $iconBytes.Length) 'Invalid ICO payload bounds.'
        Assert-Icon ($width -eq $sizes[$index] -and $height -eq $width) 'Unexpected ICO dimensions.'
        $stream.Position = $offset
        $payload = $reader.ReadBytes($length)
        $sha = [Security.Cryptography.SHA256]::Create()
        try { $frameHashes.Add([Convert]::ToBase64String($sha.ComputeHash($payload))) }
        finally { $sha.Dispose() }
        $imageStream = [IO.MemoryStream]::new($payload, $false)
        $decoded = [Drawing.Image]::FromStream($imageStream)
        try { Assert-Icon ($decoded.Width -eq $width -and $decoded.Height -eq $height) 'ICO image decode mismatch.' }
        finally { $decoded.Dispose(); $imageStream.Dispose() }
    }
}
finally { $reader.Dispose(); $stream.Dispose() }

$assets = @(
    @('Square44x44Logo.scale-200.png', 88, 88),
    @('Square44x44Logo.targetsize-24_altform-unplated.png', 24, 24),
    @('Square150x150Logo.scale-200.png', 300, 300),
    @('StoreLogo.png', 50, 50),
    @('Wide310x150Logo.scale-200.png', 620, 300),
    @('SplashScreen.scale-200.png', 1240, 600),
    @('LockScreenLogo.scale-200.png', 48, 48)
)
foreach ($asset in $assets) {
    $decoded = [Drawing.Image]::FromFile((Join-Path $assetDirectory $asset[0]))
    try { Assert-Icon ($decoded.Width -eq $asset[1] -and $decoded.Height -eq $asset[2]) "Wrong asset dimensions: $($asset[0])" }
    finally { $decoded.Dispose() }
}

$installerText = Get-Content -LiteralPath $installerScript -Raw
$identityText = Get-Content -LiteralPath $identitySource -Raw
$applicationText = Get-Content -LiteralPath $applicationSource -Raw
$mainWindowText = Get-Content -LiteralPath $mainWindowSource -Raw
$toolWindowText = Get-Content -LiteralPath $toolWindowSource -Raw
$productionCsFiles = @(Get-ChildItem -LiteralPath $projectRoot -Recurse -File -Filter '*.cs' |
    Where-Object { $_.FullName -notmatch '\\(bin|obj|artifacts|Tests)\\' })
$topLevelWindowTypes = @(
    $productionCsFiles | ForEach-Object {
        Select-String -LiteralPath $_.FullName -Pattern '\b(?:partial\s+)?class\s+\w+\s*:\s*Window\b' |
            ForEach-Object { $_.Matches.Value }
    }
)
Assert-Icon ($topLevelWindowTypes.Count -eq 2) 'A top-level window was added without an icon audit update.'
Assert-Icon ((@($topLevelWindowTypes | Where-Object { $_ -match '\bMainWindow\s*:\s*Window\b' }).Count) -eq 1) 'MainWindow is missing from the top-level window audit.'
Assert-Icon ((@($topLevelWindowTypes | Where-Object { $_ -match '\bToolWindow\s*:\s*Window\b' }).Count) -eq 1) 'ToolWindow is missing from the top-level window audit.'
Assert-Icon ($mainWindowText -match 'AppWindowIcon\.Apply\(AppWindow\)') 'Main window does not apply the branding icon.'
Assert-Icon ($toolWindowText -match 'AppWindowIcon\.Apply\(AppWindow\)') 'Tool windows do not apply the branding icon.'
Assert-Icon ($identityText -match 'AppUserModelId\s*=\s*"NaufalTechs\.WindowsPowertoys"') 'Taskbar identity changed or missing.'
$identityCallIndex = $applicationText.IndexOf('ShellAppIdentity.ApplyToCurrentProcess();', [StringComparison]::Ordinal)
$mainCreationIndex = $applicationText.IndexOf('_window = new MainWindow();', [StringComparison]::Ordinal)
Assert-Icon ($identityCallIndex -ge 0 -and $mainCreationIndex -gt $identityCallIndex) 'Taskbar identity must be applied before UI creation.'
Assert-Icon ($installerText -match '#define AppUserModelId "NaufalTechs\.WindowsPowertoys"') 'Installer taskbar identity missing.'
Assert-Icon ($installerText -match 'UninstallDisplayIcon=\{#AppIconFile\}') 'Uninstaller does not use the branding ICO.'
$startShortcuts = @($installerText -split "`r?`n" | Where-Object { $_ -match '^Name: "\{autoprograms\}' })
$desktopShortcuts = @($installerText -split "`r?`n" | Where-Object { $_ -match '^Name: "\{autodesktop\}' })
$shortcuts = @($startShortcuts + $desktopShortcuts)
Assert-Icon ($startShortcuts.Count -eq 1) 'Start shortcut missing.'
Assert-Icon ($desktopShortcuts.Count -eq 1) 'Desktop shortcut missing.'
foreach ($shortcutLine in $shortcuts) {
    Assert-Icon ($shortcutLine -match 'IconFilename: "\{#AppIconFile\}"; IconIndex: 0; AppUserModelID: "\{#AppUserModelId\}"') 'Shortcut has incomplete icon/taskbar metadata.'
}

if ($PublishDirectory -or $InstallerFile) {
    # Map PE resources as DATA only. Never start the application or installer.
    if (-not ('IconResourceAudit' -as [type])) {
        Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
public static class IconResourceAudit {
    private delegate bool EnumName(IntPtr module, IntPtr type, IntPtr name, IntPtr parameter);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern IntPtr LoadLibraryExW(string name, IntPtr file, uint flags);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern bool EnumResourceNamesW(IntPtr module, IntPtr type, EnumName callback, IntPtr parameter);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern IntPtr FindResourceW(IntPtr module, IntPtr name, IntPtr type);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern uint SizeofResource(IntPtr module, IntPtr info);
    [DllImport("kernel32.dll", SetLastError=true)] private static extern IntPtr LoadResource(IntPtr module, IntPtr info);
    [DllImport("kernel32.dll")] private static extern IntPtr LockResource(IntPtr resource);
    [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr module);
    public static string[] ReadIconHashes(string path) {
        IntPtr module = LoadLibraryExW(path, IntPtr.Zero, 0x22);
        if (module == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try {
            var hashes = new List<string>();
            Exception failure = null;
            EnumName callback = (m, t, n, p) => {
                try {
                    IntPtr info = FindResourceW(m, n, t);
                    uint size = SizeofResource(m, info);
                    IntPtr data = LockResource(LoadResource(m, info));
                    if (data == IntPtr.Zero || size == 0) throw new InvalidOperationException("Unreadable icon resource.");
                    byte[] bytes = new byte[checked((int)size)];
                    Marshal.Copy(data, bytes, 0, bytes.Length);
                    using (var sha = SHA256.Create()) hashes.Add(Convert.ToBase64String(sha.ComputeHash(bytes)));
                    return true;
                } catch (Exception e) { failure = e; return false; }
            };
            bool ok = EnumResourceNamesW(module, (IntPtr)3, callback, IntPtr.Zero);
            GC.KeepAlive(callback);
            if (failure != null) throw failure;
            if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
            return hashes.ToArray();
        } finally { FreeLibrary(module); }
    }
}
'@
    }
    $binaries = @()
    if ($PublishDirectory) {
        $binaries += Join-Path $PublishDirectory 'Naufal Windows Powertoys.exe'
        $publishedIcon = Join-Path $PublishDirectory 'Assets\NaufalWindowsPowertoys.ico'
        Assert-Icon ((Get-FileHash -LiteralPath $publishedIcon).Hash -eq
            (Get-FileHash -LiteralPath (Join-Path $assetDirectory 'NaufalWindowsPowertoys.ico')).Hash) 'Published window icon is stale/missing.'
        foreach ($asset in $assets) {
            Assert-Icon ((Get-FileHash -LiteralPath (Join-Path $PublishDirectory "Assets\$($asset[0])")).Hash -eq
                (Get-FileHash -LiteralPath (Join-Path $assetDirectory $asset[0])).Hash) "Published package asset is stale: $($asset[0])"
        }
    }
    if ($InstallerFile) { $binaries += $InstallerFile }
    foreach ($binary in $binaries) {
        $embeddedHashes = [IconResourceAudit]::ReadIconHashes((Resolve-Path -LiteralPath $binary).Path)
        foreach ($hash in $frameHashes) {
            Assert-Icon ($embeddedHashes -contains $hash) "Binary missing a supplied icon resolution: $binary"
        }
    }
}
Write-Output "PASS: $assertions icon assertions. No application or installer was launched."
