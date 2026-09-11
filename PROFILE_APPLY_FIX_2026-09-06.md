# Performance Profile Apply — 6 September 2026

## Scope and evidence

Focused follow-up to the Balanced and Competitive Gaming failure screenshots.
This does not supersede the outstanding whole-program parity/UI/VM validation
listed in `PROGRAM_AUDIT_2026-09-05.md`. Neither a profile Apply/Restore nor a
network adapter Enable/Disable was executed during this work.

### Balanced: missing registry override was treated as missing power setting

The screenshot names processor minimum setting
`893dee8e-2bef-41e0-89c6-b55d0929964c` in Balanced scheme
`381b4222-f694-41f0-9685-ff5bb260df2e`. Read-only checks found no explicit
per-scheme registry override, but `powercfg /query` and the native Windows
power API both read **AC 5 / DC 5**. The preflight implementation queried only
the override tree and therefore rejected a valid setting.

`NativePowerPolicyReader` now supplies the same effective values to extended
profile snapshots, apply/read-back and live profile verification. AC and DC
errors remain distinct, include the native error code, and fail closed; failed
reads do not become default zero values. Registry-based factory-default lookup
is a separate operation and has not been conflated with current effective state.
The API contract is documented by Microsoft for
[PowerReadACValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreadacvalueindex)
and [PowerReadDCValueIndex](https://learn.microsoft.com/en-us/windows/win32/api/powrprof/nf-powrprof-powerreaddcvalueindex).

### RSC: misleading `failed with result 0`

The old return validator accepted only VARIANT types 3 and 19, but printed the
unsigned union field as an error code for **every** other type. An unset or
unsupported output could therefore produce `failed with result 0`, preventing
the mandatory adapter state read-back and affecting both Apply and rollback.
The screenshot does not record the actual VARIANT type; it is not evidence
that a numeric zero return itself failed, or that the full rollback succeeded.

The new path:

- Checks the declared CIM_UINT32 schema and handles supported integer types.
- Rejects explicit nonzero errors with the exact code and VARIANT/CIM types.
- Distinguishes NULL/EMPTY from numeric zero and never interprets unused union
  bits as an error code. After a successful native call, an unset numeric return
  may proceed **only to mandatory state verification**, not directly to success.
- Re-queries every target adapter and compares IPv4 and IPv6 to the exact target.
  Missing adapters, mixed/mismatched protocol states, or unreadable state still
  fail. Diagnostics show adapter, expected/actual values and method result.
- Uses the same verified path for rollback; a zero or unset output alone never
  grants rollback success.

Microsoft documents the RSC method signatures for
[Enable](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/enable-msft-netadapterrscsettingdata)
and [Disable](https://learn.microsoft.com/en-us/windows/win32/fwp/wmi/netadaptercimprov/disable-msft-netadapterrscsettingdata).
Read-only local CIM metadata confirms UInt32. The native output **signature**
contains VARIANT=1 (NULL), CIM=19; that is a schema template, not an observed
return from executing Enable/Disable. The precise execution-time return on
this PC and end-to-end profile behavior still require a controlled Apply test.

## Verification

- Debug x64: 0 errors, 0 warnings.
- 717 synthetic regression assertions passed (615 prior baseline + 24 power
  reader cases + 78 RSC cases), including AC/DC errors, successful zero values,
  missing/unsupported RSC returns, failed methods and failed state verification
  for Apply/rollback targets. Assertion counts are not end-to-end feature counts.
- 5 publish-stage assertions passed.
- Shared power API probe read Balanced: minimum 5/5, maximum 100/100,
  increment policy 0/0, minimum cores 100/100, maximum cores 100/100 (AC/DC).
- Native read-only RSC probe: Wi-Fi and Ethernet 2 reported IPv4=True and
  IPv6=True. Input construction and output metadata for both methods validated;
  **no adapter method was executed**. This current-state observation does not
  prove the historical rollback of CPU/BCD/network settings was complete.
- Initial WMI access inside the sandbox was denied; the read-only probe succeeded
  outside the sandbox. One test invocation through `dotnet <test.dll>` failed
  the child-process harness because `Environment.ProcessPath` then referred to
  dotnet rather than the test executable. Re-running the actual test executable
  passed all 717 assertions and the native probe; it was not an app regression.

Useful commands from the project root:

```powershell
dotnet run --project Tests\ProfileVerification\ProfileVerification.Tests.csproj -c Debug --no-restore -- --power-probe
& '.\Tests\ProfileVerification\bin\Debug\net10.0-windows\ProfileVerification.Tests.exe' --rsc-probe
dotnet build "Naufal Tech's Windows Powertoys.csproj" -c Debug -p:Platform=x64 --no-restore
.\Tests\ParityAudit\Test-PublishStage.ps1
.\build-installer.ps1 -NoRestore
```

Do not run the test DLL directly for this suite: its child-process pipe tests
expect the test executable to be `Environment.ProcessPath`.

## Artifacts

Native AOT and Inno Setup compilation succeeded; the completed staging manifest
and artifact hashes were checked after compilation.

- Publish folder: `artifacts/publish/win-x64-20260906-001404-265`.
- EXE: `Naufal Windows Powertoys.exe`, 18,698,240 bytes.
  SHA-256: `232999F2D8E1BB2150AF0C831A571E4D322FF206270812564A715219AC739D59`.
- Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-7.8.0-x64.exe`,
  36,582,707 bytes.
  SHA-256: `A8F571E494648789883FF5DC0DE3BB539C78E7147B4B7F21687D6B26FBB8D77F`.
- Both: FileVersion `7.8.0.0`, CompanyName `Naufal Tech's Softwares`, NotSigned.
- The previous same-name Setup was replaced; historical publish folders remain.
  Distribute Setup or the entire publish folder, not the EXE alone.
- Neither the new GUI EXE nor Setup was executed in this focused pass; startup,
  installation/upgrade and real profile mutations are not claimed verified.
