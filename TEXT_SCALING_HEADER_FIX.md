# Text Scaling: header overflow fix

Date: 12 September 2026, Asia/Jakarta. Version: 8.0.0.0.

## Report and cause

The user could no longer reach the Text Scaling button while cycling through
25%, 50%, 75% and subsequent scales. The supplied screenshot showed the final
button beyond the right edge of the application header.

The original header had fixed 180/270-DIP columns, fixed height, and an
unconstrained horizontal StackPanel for the language selector and both buttons.
Those widths did not account for native minimum control sizes and scaled text.
Even at baseline, the authored 160 + 38 + 38 widths, two 8-DIP gaps and two
10-DIP margins totalled 272 DIP inside a 270-DIP settings column. The fix removes
the fixed-width assumption rather than applying an arbitrary negative margin.

## Implementation

- MainWindow header height is Auto with a minimum, so larger fonts are not
  clipped vertically.
- Settings use Grid columns, including content-sized columns for the two
  recovery buttons. The clock wraps to a separate row on narrower layouts.
- Header bounds constrain the language selector, which has an explicit zero
  minimum width so it cannot displace the recovery buttons.
- Layout decisions use absolute selected scale and current available width,
  never the previous control width. Unmeasured/zero-width layout is ignored.
- Theme and Text Scaling targets have a 32-DIP minimum and readable glyphs.
  The scaling flyout retains all eight presets and Reset, with font size 14–28
  DIP independently of very small dashboard text.
- Ctrl+0 resets the application scale to 100%. The accelerator marks the event
  handled to avoid also invoking the button's flyout action.
- `MainWindow.HeaderLayout.cs` is shared verbatim with the native test host;
  it contains the real responsive logic and flyout creation/event handlers.
- Live graphs, Task Monitoring, 23 languages and catalog behavior are preserved.

## Evidence

- Debug build: 0 errors, 0 warnings.
- Functional suite: 3,249 assertions passed, including 294 new policy checks.
- Localization: 97,662 assertions passed across 23 languages.
- Catalog source wiring: 80 assertions passed; monitoring wiring: 26 passed.
- Native test host: initial full matrix passed 5,656 assertions over 512 layout
  cases and eight flyout openings. It links the actual application XAML,
  resources, header implementation, scaling and localization code.
- Repeated the full matrix with saved startup preferences at 25% and 200%:
  each run passed **5,667 native assertions, 512 layout cases and eight flyout
  openings**, including an additional initial-window bounds/hit-test check.
  The final run completed on 12 September 2026 after starting at 16:41:43 WIB.
- The native matrix covers 1920/1280/800/480-pixel windows, Light/Dark,
  English/German/Indonesian/Arabic, ascending then descending scales. It checks
  bounds, nonzero dimensions, minimum target size and the button center in
  WinUI's native hit-test results, not merely whether the element exists.
- Test hit-test coordinates are transformed into XamlRoot host coordinates;
  using the RTL root Grid's logical coordinates incorrectly tests a mirrored
  point and was corrected in the test harness.
- The test host runs without elevation, has inert system-action handlers and
  separate disposable preferences under its own build output. It does not
  instantiate repair/tweak/first-run/hardware services or touch real app settings.
- Native AOT publish and Setup compilation completed; 91 publish icon checks
  passed. Setup is built, not installed. No commit/push was performed.
- The tests are native layout and programmatic flyout tests, not a claim that
  real mouse/keyboard input or all catalog actions were exercised in the final
  elevated executable. Ctrl+0's actual keypress remains a manual smoke check.

## Artifacts

Native AOT stage: `artifacts/publish/win-x64-20260912-163827-913`.

Application SHA-256:
`D7C126DE617CEF81BAA39A4278B7BCC6D397CA2310308E2CD8EB74ABCAA6B6E6`

Installer: `artifacts/installer/Naufal-Windows-Powertoys-Setup-8.0.0-x64.exe`.
Size: 38,140,101 bytes.

Installer SHA-256:
`3F1C8777C2C845D1BACA2ECD513CA7F33A4A8AEB8E0903700D1391E80D3485D1`

Version remains 8.0.0.0; company remains Naufal Tech's Ltd. Signing status is
unchanged. Close the old app and install the new Setup to receive the fix;
existing installed files were not silently replaced during development.

## References

- [Microsoft: Layout panels](https://learn.microsoft.com/en-us/windows/apps/develop/ui/layout-panels)
  documents Auto content sizing and star columns.
- [Microsoft: Keyboard accelerators](https://learn.microsoft.com/en-us/windows/apps/develop/input/keyboard-accelerators)
  documents custom Invoked handling and suppressing the default control action.
