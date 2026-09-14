# Native header layout regression host

This WinUI test application links the **actual** `MainWindow.xaml`, application
styles, `MainWindow.HeaderLayout.cs`, scaling and localization implementations.
It does not compile any Windows repair, first-run, hardware or mutation service.
Every unrelated dashboard action is a test-only no-op. It runs as the current
user without Administrator privileges and stores its preferences under its own
build output, never in the installed application's AppData folder.

After restoring the project once, run:

```powershell
dotnet restore .\Tests\HeaderLayout\HeaderLayout.Tests.csproj -p:Platform=x64
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NoBuild -StartupScale 25
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NoBuild -StartupScale 200
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NoBuild -StartupTheme Dark
```

For the installer-equivalent **Native AOT** path, use an x64 Visual Studio
Developer PowerShell (MSVC C++ linker and Windows SDK required):

```powershell
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NativeAot -StartupTheme Dark
& .\Tests\HeaderLayout\Test-HeaderLayout.ps1 -NoBuild -NativeAot -StartupTheme Light -StartupScale 25
```

This publishes only the backend-free test host. Its project explicitly stages
linked XBF files and the app PRI index needed for unpackaged XAML loading.

The window cycles through 1920/1280/800/480-pixel widths, Light/Dark and
en/de/id/ar, applying 25–200% forward and backward. The 512 layout cases assert
that the language selector and two recovery buttons remain inside the window,
have nonzero size, and that Text Scaling has a minimum 32-DIP target with its
center present in WinUI's native hit-test results. Eight native flyouts are
opened to verify the complete menu and readable text.

The theme regression (14 September 2026) also checks effective clock, language
label, ComboBox and item foreground contrast on each pass (at least 4.5:1,
including alpha compositing). It verifies that default header foregrounds have
no app-written local overrides and that explicit white text on primary buttons
and authored muted colors survive theme round trips. `-StartupTheme Dark`
reproduced the original white-on-light clock at only 1.04:1 before the fix.
Both startup themes use isolated test preferences, never the real AppData.

The follow-up button regression also checks all **28 enabled authored dashboard
buttons**, including their rendered TextBlock labels. Hidden WinUI template
buttons are not treated as authored dashboard actions. Separate-window tests
create implicit, explicit-style and local-color catalog buttons in both themes;
they verify theme round trips, re-enabling and primary/neutral style changes.
Style-owned foreground/background/border values must remain free of local
snapshot overrides. Disabled text is intentionally not held to enabled contrast.

After a successful matrix, RenderTargetBitmap writes `theme-preview-dark.png`
and `theme-preview-light.png` beside the test executable. These are actual renders
of the isolated UI, **not** screenshots of a running repair or live metrics.
The native host has no system monitor/chart backend; initializing labels and
the static clock are expected. Do not use these previews as product screenshots.

The report is written beside `HeaderLayout.Tests.exe` as
`header-layout-results.txt`. The runner rejects failed, missing or stale reports
and stops only its own backend-free test process if it exceeds 60 seconds.
Visual appearance and actual mouse/keyboard input in the final elevated binary
are separate acceptance checks; these tests must not be described as exercising
all application features or all 23 languages interactively.
