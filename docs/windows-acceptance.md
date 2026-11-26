# Windows launcher acceptance

Use Windows 11 x64 and PowerShell 7. Use a separate Windows user session or test VM for cold startup, client absence and launcher termination tests. Keep active Codex work sessions available.

## Accept the portable package

1. Compare the ZIP hash against its `.sha256` sidecar. Extract the whole archive to a writable directory whose path includes a space and a non-ASCII character. Open PowerShell outside the source tree and launch the extracted EXE by its absolute path.
2. Run `--settings`. Check the title, theme preview and launcher version. Check startup with the default Quick mode, English and system reduced motion enabled. Resize the window at normal and enlarged display scaling. Use Tab, arrow keys and Space to select controls and activate buttons.
3. Select 简体中文 and Cinematic. Save, close, then reopen settings to verify both choices persist. Preview the animation and confirm ChatGPT stays in place. Reset the form and save, then reopen to check the defaults. Close settings and verify its process exits.
4. If you want desktop shortcuts, select **Add Launch Codex** and **Add Launcher Settings**. Try both links, then select **Remove launcher shortcuts** and verify each launcher link is gone. If another item has either name, confirm it stays in place. Leave the official ChatGPT shortcut available.
5. With settings closed, move the entire folder and reopen it from the new location. Verify the data moved with the executable and replay the preview. Recreate any launcher shortcuts from the new location.
6. In a disposable copy, replace `.launcher-data/settings.json` with invalid JSON. Check that settings opens with safe defaults and that Save writes a usable file. Try preview in a folder where launcher data cannot be written, then verify Direct works through Windows.

## Run the modes

From the project root:

```powershell
$launcher = '.\.local\neural-overdrive-launcher\win-x64\NeuralOverdriveLauncher.exe'
& $launcher --settings
& $launcher --preview
& $launcher --preview --full
& $launcher --quick
& $launcher --full
& $launcher --direct
```

Run one command at a time. For repeatable waits and diagnostic output, use the test script:

```powershell
.\scripts\Test-Interactive.ps1 -Scenario Preview -Mode Full -Duplicate
.\scripts\Test-Interactive.ps1 -Scenario Warm -Mode Quick
.\scripts\Test-Interactive.ps1 -Scenario Warm -Mode Direct
```

Pass `-LauncherPath` to the script to exercise the extracted EXE. For Phase 2 comparisons, run the retained v0.1.0 EXE directly.

In the isolated Windows environment, establish that ChatGPT is closed before the cold check. Use Start to verify the native baseline in a separate run. The test script launches the MVP and waits for its result.

```powershell
.\scripts\Test-Interactive.ps1 -Scenario Cold -Mode Quick
.\scripts\Test-Interactive.ps1 -Scenario Cold -Mode Direct
```

Exercise each Cold command from a fresh cold state. The logs distinguish an accepted activation from a stable candidate window. Record which app surface actually opens and when it responds to user input.

## Check the behavior

| Case | Expected observation |
| --- | --- |
| 5-second and 9-second preview | A borderless window appears. Compare the wake signal, neural lattice, shockwave, tunnel and final title against the reference. The simulated telemetry label remains visible. The window closes at completion. |
| Esc | Press Esc once during both the dark initialization screen and active animation, while the launcher is focused. The window dismisses promptly. In launch modes, the independently starting client remains available. |
| Reduced motion | With Windows **Accessibility → Visual effects → Animation effects** off, start a preview. It shows the static final frame briefly. Repeat after restoring the preference to confirm movement returns. |
| Concurrent launches | The duplicate run records `duplicate-suppressed`. One animation plays. Repeat with the warm client if checking duplicate activation. |
| Warm and minimized client | Run quick and direct separately. The existing app remains open. If Windows grants foreground access, the matching window takes focus or restores. Use Start or the taskbar if Windows keeps it in the background. |
| User switches apps | Switch to another app during a warm animation and stay there until it ends. The launcher leaves that app in the foreground. |
| Cold startup | Check that the client opens through Windows, including when the animation has already closed. Check the resulting ChatGPT or Codex surface by observation. |
| Missing client | In an isolated test session without the supported package registered, run the launcher. Expect status 3 and an explanation when the launcher retains the interaction. |
| Client startup timeout or ambiguous windows | In a test session where the client stays without an eligible window or presents indistinguishable candidates, expect status 4 and the animation to be gone. The client can still open later. The automated suite simulates a stalled client when a real timeout is unavailable. |
| Launcher closes unexpectedly | In the isolated session, close the animation with Alt+F4, then separately end only the `NeuralOverdriveLauncher.exe` test process after activation. The client and original Start entry remain usable. |
| Focus, screen and DPI | Compare both settings languages with keyboard focus visible. Test settings and animation on each monitor and drag settings between differently scaled monitors. Check layout, close controls and native handoff. |

Use these failure checks with a warm client. Each creates a test copy under `.local/acceptance/` and uses its own launcher data directory.

```powershell
.\scripts\Test-Interactive.ps1 -Scenario ThemeFailure
.\scripts\Test-Interactive.ps1 -Scenario WebViewFailure
```

The first corrupts only the copied theme. The second points only the test launcher process at an empty WebView2 runtime folder. In each case, confirm the animation window closes and the native launch path continues. For the actual missing-runtime case, use an isolated Windows environment where Evergreen is already absent and compare quick with direct mode.

## Collect results

- Run `.\scripts\Build.ps1 -Restore -Test` for .NET checks and Node timeline simulations. They cover timing, the bridge, failure recovery, launch precedence, JSON persistence and recovery, and real Windows Shell Links in a temporary directory. The desktop shortcut buttons require explicit user interaction.
- `tests/Launcher.Windows.Tests/` drives the actual Windows settings window in a fresh copy. Build it with the pinned SDK, then run it with a disposable `NeuralOverdriveLauncher.exe` path and an ignored screenshot output directory. Use an interactive session because it exercises keyboard input in the launcher window.
- Add `--diagnostics` or run the test script. Logs contain elapsed milliseconds and fixed stage names. Compare window showing to `theme-first-frame-rendered`, first render to `theme-completed`, and activation acceptance to `client-window-stable` and handoff. A draw callback estimates the first frame. Confirm the actual displayed frame visually.
- The launcher records its own CPU time and peak working set. For total CPU, GPU and memory, measure the launcher and its WebView2 children in Task Manager in an isolated preview. Record display resolution, scale, WebView2 version, cold or warm state, and sample count. Keep workstation evidence in ignored local storage.
- Exit status 0 covers completed or skipped preview, a stable client window, a closed launcher window, or a suppressed duplicate. Status 1 indicates a launcher error, 2 indicates invalid arguments, 3 indicates unavailable registration or failed activation, 4 indicates timeout or ambiguous windows, and 5 indicates a failed preview.

## Coverage remaining before release

Record direct observations and simulated results separately. Retest an isolated cold client start and its actual destination surface; absent client registration; actual absent Evergreen; an actual client deadline or ambiguous windows; minimized-window restore; switching to another app mid-animation; physical Escape; the Windows reduced-motion setting; launcher termination; owner visual comparison; and multi-monitor mixed-DPI behavior. Repeat portable tests on a clean Windows 11 x64 machine and any Windows configuration intended for support. The launcher uses the ordinary Windows app entry. A direct route to the Codex workspace remains unverified.
