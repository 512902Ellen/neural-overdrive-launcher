# Project roadmap

Each phase ends with a review and a handoff. The next phase begins when the project lead or owner authorizes it. Dates and the final Windows support matrix will follow integration testing.

| Phase | Scope | Review outcome |
| --- | --- | --- |
| **1 · Setup and feasibility** | Preserve NEURAL OVERDRIVE `v0.3.0`, audit the development environment and Windows app entry, compare host options, and document the launch design. | Approved by the project lead. |
| **2 · MVP** | Build the Windows x64 `v0.1.0` launcher with 9-second, 5-second, direct and preview modes, Esc, reduced motion, timeout handling and native activation. | Build and automated checks delivered. [Windows acceptance](windows-acceptance.md) covers visual fidelity, warm and isolated cold start, focus and failure recovery. Owner review is pending. |
| **3 · Product features** | Add mode preferences, a desktop shortcut, portable distribution, user-scoped install and uninstall if needed, and a simple local theme format. Refine error messages, display handling, accessibility, and handoff behavior. | An owner-approved release candidate is usable on a clean test machine. |
| **4 · Integration and quality review** | Test supported Windows versions, scaling and mixed-DPI displays, application updates, cold and warm launches, and offline animation. Measure performance and verify failure paths, privacy, dependency notices, and install/uninstall. | Supported scenarios and known limitations documented with test evidence. |
| **5 · GitHub release** | Confirm license and branding, review repository contents and history, record the version, and prepare the approved `v1.0.0` tag and GitHub Release. Provide Windows downloads, checksums, usage instructions, and captured demo assets. | Owner approves publication and verifies the released artifacts. |
| **6 · Maintenance** | Triage issues, monitor Windows and ChatGPT desktop compatibility, maintain dependencies, and evaluate theme contributions. | Publish maintenance releases with verified fixes and release notes. |

## Repository as it grows

The launcher code lives under `src/`, automated checks under `tests/`, and build and Windows acceptance tools under `scripts/`. The approved reference and launcher theme are under `themes/`. Add `assets/` for shared visuals and `examples/` for approved demos when needed.

## Release preparation

- Start with a Windows x64 portable ZIP built from a self-contained launcher. Document the WebView2 requirement, and document the native launch fallback for systems where animation cannot run. Evaluate an installer once installation and uninstall behavior are ready to validate. Publish ARM64 only after equivalent tests pass.
- Use launcher prereleases such as `0.1.0` and `1.0.0-rc.1` when appropriate. Publish SHA-256 checksums with stable releases and record the dependency versions used to build each package.
- Expand the README with requirements, screenshots or a short GIF, a video link if available, download and setup, mode controls, direct launch and recovery, theme instructions, known limitations, and contribution and license information. Capture the launcher demo with simulated telemetry and an approved clean screen.
- Add Windows CI after the first implementation. Build and test on pull requests and stage release packages for review from version-tagged source. Verify foreground activation, clean-machine installation, multi-display behavior, and performance in interactive Windows sessions.
- Inventory shipped dependencies and theme assets and include their required notices. Review the working project name, app icon, wordmark treatment, and unofficial statement before release. Use project-owned artwork and review any third-party marks or assets before distribution.
