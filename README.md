# Codex Boot Animation

Codex Boot Animation is an independent, unofficial Windows launcher project for the Codex experience in the ChatGPT desktop app. It is unaffiliated with and unendorsed by OpenAI.

Phase 2 delivers the `v0.1.0` Windows 11 x64 MVP for local testing. The built-in theme is **NEURAL OVERDRIVE** `v0.3.0`.

## Run the MVP

Run `.local/mvp/win-x64/CodexBootAnimation.exe` after building. Keep the executable and its neighboring files together in a writable directory. The self-contained build includes .NET and WPF. Animated modes use the machine's Evergreen WebView2 Runtime. Launch modes use the supported ChatGPT desktop app registration for the current Windows user.

| Arguments | Action |
| --- | --- |
| None, or `--quick` | Play the 5-second animation and open or hand off to ChatGPT. |
| `--full` | Play the 9-second animation and open or hand off to ChatGPT. |
| `--direct` | Open or hand off to ChatGPT without initializing WebView2. |
| `--preview` | Play the animation only. Combine with `--full` for 9 seconds. |
| `--diagnostics` | Write local timing and outcome events under `.launcher-data/logs` next to the executable. |
| `--help` | Show available options. |

Press **Esc** while the animation has focus to dismiss it. With reduced motion enabled, the theme briefly shows its final static frame. The console, engine modules, progress, and readiness text are simulated.

If the animation fails, launch modes continue through the Windows entry. The launcher can identify a candidate desktop window. Workspace readiness and the mode ChatGPT opens are determined by the client. Windows controls foreground access. If the animation closes and ChatGPT remains in the background, use its taskbar icon or **Start → ChatGPT**.

The [standalone reference HTML](themes/neural-overdrive/reference/codex-cyber-boot-v03.html) remains available for browser comparison with its original demo controls.

## Build from source

From the repository root in PowerShell 7 on Windows x64, run:

```powershell
.\scripts\Install-LocalSdk.ps1
.\scripts\Build.ps1 -Restore -Test
```

The first command downloads Microsoft .NET SDK `10.0.404` and checks the official SHA-512. The second restores the exact WebView2 SDK `1.0.3967.48` and self-contained .NET `10.0.15` dependencies from NuGet.org. SDK files, caches and build output stay in `.local/`. The test switch uses the pinned SDK and Node.js. Run `.\scripts\Build.ps1` for subsequent builds using the restored dependencies. [Microsoft SDK binaries](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-10.0.404-windows-x64-binaries-zip) · [WebView2 SDK](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.3967.48)

See [Windows acceptance testing](docs/windows-acceptance.md) for preview, native startup, failure recovery and diagnostics.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/Core/` | Launch options, coordination, message validation and deadlines. |
| `src/Launcher/` | WPF host, Windows app activation and window tracking. |
| `themes/neural-overdrive/` | Byte-preserved reference under `reference/` and launcher HTML under `runtime/`. |
| `scripts/`, `tests/` | SDK provisioning, reproducible build, automated checks and Windows acceptance. |
| `docs/` | Roadmap and manual acceptance guide. |
| `AGENTS.md` | Collaboration and engineering rules. |
| `CHANGELOG.md` | Project changes and release history. |

See the [roadmap](docs/roadmap.md) for development and release milestones.

## License

MIT is the proposed project license, pending owner confirmation. Check the published release for the final license and third-party notices.
