# Task Monitoring and live performance graphs

Date: 12 September 2026 (Asia/Jakarta). Product version: 8.0.0.0.

## Changes

- The main navigation button and task window are named **Task Monitoring**.
  Operation handlers now update its tooltip without replacing the button name.
- The window reads `TaskActivityService.RunningSnapshot()`. Completed, failed,
  warning, interrupted and queued records do not appear. Queued work is still
  admitted by the existing scheduler and appears once running. Diagnostic
  history remains available through the original `Snapshot()` API.
- Refresh remains 500 ms and stops when the task window closes. An empty view
  says “No tasks are currently running.”
- CPU, RAM, GPU 3D and network cards contain native WinUI line/area graphs with
  gridlines and a 60-second time axis. The existing one-second sampler feeds
  them; no extra worker or polling timer was introduced.
- Percentage axes are fixed at 0–100%. Network uses one shared automatic Mbps
  scale, solid receive and dashed transmit traces. The graph is inspired by
  Task Manager, not a promise of identical counter aggregation: GPU retains the
  application's existing combined 3D-engine counter, and network retains its
  existing active non-loopback adapter aggregate.
- Missing readings remain gaps. Initial CPU/network counter warmup does not
  become a zero sample. Non-finite/negative readings are rejected; gaps longer
  than 2.5 seconds are not interpolated. Valid zero readings remain visible.
- History retains at most 60 seconds and 121 samples. Coordinate projection is
  bounded and handles empty/invalid layouts. Plot geometry is rebuilt on layout
  size changes and explicit light/dark/scaling changes.
- Existing numeric values, gaming configuration details, catalog menus,
  scheduler resource locks and separate catalog progress windows are retained.
- New monitoring and axis strings are supplied for all 23 languages, including
  formatted-count tests and round trips back to English.

## Verification

- Debug build: succeeded, zero warnings and zero errors.
- Functional suite: **2,955 assertions passed**, including 56 new monitoring /
  history assertions. Tests cover terminal-state removal, queue promotion,
  retained history, invalid samples, time gaps, bounds and network axes.
- Localization suite: **97,662 assertions passed across 23 languages**.
- Existing catalog button/routing/progress audit: **80 assertions passed**.
- New monitoring source-wiring audit: **26 assertions passed**.
- Publish icon validation: **91 assertions passed**.
- Native AOT publish and Inno Setup compilation completed successfully.
- Launched the exact published executable below through a process-qualified
  path. The earlier unqualified launch resolved to the installed old build and
  was explicitly excluded as evidence. The user closed that old build before
  the new build was launched.
- Observed the new Task Monitoring button and four live graphs in light mode.
  Observed changing values/graphs at startup and after two minutes; history
  filled the 60-second plot and network scale responded to traffic peaks. The
  process remained responsive. Computer-use screenshot inspection helped
  distinguish the installed build from the new artifact.
- The desktop helper reported lower integrity than this Administrator app.
  Automated clicks did not open the monitor, so native empty/running task-window
  interaction and the full theme/scaling matrix are **not certified** by this
  smoke test. These remain manual UI acceptance checks, not claimed passes.
- No repair, tweak, installation or uninstall was executed during verification.
  No Git commit or push was performed. The newly built application remains
  available for the user's manual inspection; Setup was built, not installed.

## Artifacts

Native AOT stage: `artifacts/publish/win-x64-20260912-161601-278`.

Application SHA-256:
`13EFC3AF010B57E75C1A14FD9C6A3BFB1BB8D500AAE497B17039842529EDBB8A`

Setup: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Size: 38,120,782 bytes.

Setup SHA-256:
`114911E2A50300C0746FA85650D425BEE4C73D0EBB5ED3912FA7401D50C5CB97`

Application file version: `8.0.0.0`; company: `Naufal Tech's Ltd.`.
Application signing status remains `NotSigned`.

## Manual acceptance checks still required

1. Open Task Monitoring when idle: only the empty message, no completed rows.
2. Start a read-only report and observe its running row; the row disappears
   after completion. Its report/log must remain accessible independently.
3. With a safe test fixture, queue two conflicting tasks: only the running one
   appears, then the next appears on promotion. Failures are removed too.
4. Switch light/dark and all eight scale settings; inspect graph strokes, labels
   and numeric cards at both normal and narrow window sizes.
5. Switch languages while monitoring is open, including Arabic/Urdu and back to
   English. Verify text refresh and the graph's left-to-right time axis.

WinUI drawing reference: [Microsoft — Shapes](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/shapes).
