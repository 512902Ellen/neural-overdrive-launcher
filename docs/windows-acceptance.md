# Windows MVP acceptance

Use a Windows 11 x64 desktop with the local build and PowerShell 7. Use a separate Windows user session or test VM for cold startup, client absence and launcher termination tests. Keep any active Codex work session available.

## Run the modes

From the project root:

```powershell
$launcher = '.\.local\mvp\win-x64\CodexBootAnimation.exe'
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
| Launcher closes unexpectedly | In the isolated session, close the animation with Alt+F4, then separately end only the `CodexBootAnimation.exe` test process after activation. The client and original Start entry remain usable. |

Use these failure checks with a warm client. Each creates a test copy under `.local/acceptance/` and uses its own launcher data directory.

```powershell
.\scripts\Test-Interactive.ps1 -Scenario ThemeFailure
.\scripts\Test-Interactive.ps1 -Scenario WebViewFailure
```

The first corrupts only the copied theme. The second points only the test launcher process at an empty WebView2 runtime folder. In each case, confirm the animation window closes and the native launch path continues. For the actual missing-runtime case, use an isolated Windows environment where Evergreen is already absent and compare quick with direct mode.

## Collect results

- Run `.\scripts\Build.ps1 -Restore -Test` for the .NET coordination and bridge checks and Node timeline simulations. They cover modes, rejection of invalid messages, startup and playback deadlines, Escape, reduced motion and simulated activation failures.
- Add `--diagnostics` or run the test script. Logs contain elapsed milliseconds and fixed stage names. Compare window showing to `theme-first-frame-rendered`, first render to `theme-completed`, and activation acceptance to `client-window-stable` and handoff. A draw callback estimates the first frame. Confirm the actual displayed frame visually.
- The launcher records its own CPU time and peak working set. For total CPU, GPU and memory, measure the launcher and its WebView2 children in Task Manager in an isolated preview. Record display resolution, scale, WebView2 version, cold or warm state, and sample count. Keep workstation evidence in ignored local storage.
- Exit status 0 covers completed or skipped preview, a stable client window, a closed launcher window, or a suppressed duplicate. Status 1 indicates a launcher error, 2 indicates invalid arguments, 3 indicates unavailable registration or failed activation, 4 indicates timeout or ambiguous windows, and 5 indicates a failed preview.
