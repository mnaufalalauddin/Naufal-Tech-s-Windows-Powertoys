# Performance profile regression tests

This standalone .NET 10 console project links the production verifier and
read-only collectors. It does not start WinUI or invoke any Apply/Restore path.
The parent WinUI project excludes this directory from application build items.

Run from the repository root:

```powershell
dotnet restore .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --configfile .\Tests\ProfileVerification\NuGet.Config
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore
```

The suite started with 73 profile assertions and has expanded to cover catalog,
repair, task, restore, WMI, app-management, and other regressions. The latest
recorded development checkpoint contains 2,655 functional assertions. Consult
the root SOURCE_PACKAGE.md for verification of this exported snapshot.

The original profile coverage includes all three 23-check profiles, AC/DC
mismatches, missing/read-denied values, partial matches, stale historical plan
IDs, per-adapter protocol states, and the LIVE RSC summary.

Optional read-only Windows probe:

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore -- --probe
```

The probe reads WMI, registry, power settings and BCD. It also validates the WMI
Enable/Disable method signatures and constructs their input objects, but never
executes those methods. A process without Administrator access may report BCD
as unavailable and therefore not certify the profile. That is not a test failure
or proof that the BCD values are wrong. Sandbox WMI denial is handled explicitly.
Unexpected harness errors are printed to stderr with exit code 1, not left as
unhandled application exceptions.

These tests do not validate actual adapter mutations or transaction rollback.
Use disposable administrative Windows VM snapshots for those operations.

## First-run WMI verification

`WmiPrerequisiteTests` adds synthetic Windows 10/11, invalid data, access denial,
timeout and retry tests. Normal test runs never install WMIC or execute setup.
To run only the production, read-only OS/RAM probes on a machine:

```powershell
dotnet run --project .\Tests\ProfileVerification\ProfileVerification.Tests.csproj --no-restore -- --wizard-wmi-read-probe
```

This command uses the same native WMI reader as the wizard, without WinUI,
PowerShell/WMIC subprocesses, service changes or package installation. Failure
inside a restricted sandbox must not be mistaken for a corrupt host WMI provider.
