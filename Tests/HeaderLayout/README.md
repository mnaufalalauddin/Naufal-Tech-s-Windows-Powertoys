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
```

The window cycles through 1920/1280/800/480-pixel widths, Light/Dark and
en/de/id/ar, applying 25–200% forward and backward. The 512 layout cases assert
that the language selector and two recovery buttons remain inside the window,
have nonzero size, and that Text Scaling has a minimum 32-DIP target with its
center present in WinUI's native hit-test results. Eight native flyouts are
opened to verify the complete menu and readable text.

The report is written beside `HeaderLayout.Tests.exe` as
`header-layout-results.txt`. The runner rejects failed, missing or stale reports
and stops only its own backend-free test process if it exceeds 60 seconds.
Visual appearance and actual mouse/keyboard input in the final elevated binary
are separate acceptance checks; these tests must not be described as exercising
all application features or all 23 languages interactively.
