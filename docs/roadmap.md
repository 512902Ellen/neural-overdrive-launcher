# Project roadmap

Each phase ends with a review and a handoff. The next phase begins when the project lead or owner authorizes it. Dates and the final Windows support matrix will follow integration testing.

| Phase | Scope | Review outcome |
| --- | --- | --- |
| **1 · Setup and feasibility** | Preserve NEURAL OVERDRIVE `v0.3.0`, audit the development environment and Windows app entry, compare host options, and document the launch design. | Approved by the project lead. |
| **2 · MVP** | Build the Windows x64 `v0.1.0` launcher with 9-second, 5-second, direct and preview modes, Esc, reduced motion, timeout handling and native activation. | Project lead approved the MVP. The owner used it and reported no obvious issues. [Windows acceptance](windows-acceptance.md) carries the outstanding scenarios forward. |
| **3 · Product features** | Add the bilingual settings entry, versioned preferences, built-in theme metadata, independent artwork, desktop shortcuts and a self-contained Windows x64 portable ZIP for `v0.2.0`. | Local build and review package prepared. Project lead review, owner experience acceptance and clean-machine validation remain pending. |
| **First GitHub release** | Prepare `v0.2.0-beta`, privacy and dependency checks, bilingual documentation, license and notices, Windows download, checksum and demo media. Carry outstanding compatibility scenarios into the [beta notes](releases/v0.2.0-beta.md). | Owner confirms the publication destination, branding and license before release. |
| **Maintenance** | Triage issues, verify remaining Windows and ChatGPT compatibility, maintain dependencies, and evaluate theme contributions. | Publish maintenance releases with verified fixes and release notes. |

## Repository as it grows

The launcher code lives under `src/`, automated checks under `tests/`, and build and Windows acceptance tools under `scripts/`. The approved reference and launcher theme are under `themes/`. Add `assets/` for shared visuals and `examples/` for approved demos when needed.

## Release preparation

- Start with a Windows x64 portable ZIP built from a self-contained launcher. Document the WebView2 requirement, and document the native launch fallback for systems where animation cannot run. Evaluate an installer once installation and uninstall behavior are ready to validate. Publish ARM64 only after equivalent tests pass.
- Use launcher prereleases such as `0.1.0` and `1.0.0-rc.1` when appropriate. Publish SHA-256 checksums with stable releases and record the dependency versions used to build each package.
- Expand the README with requirements, screenshots or a short GIF, a video link if available, download and setup, mode controls, direct launch and recovery, theme instructions, known limitations, and contribution and license information. Capture the launcher demo with simulated telemetry and an approved clean screen.
- Add Windows CI after the first implementation. Build and test on pull requests and stage release packages for review from version-tagged source. Verify foreground activation, clean-machine installation, multi-display behavior, and performance in interactive Windows sessions.
- Inventory shipped dependencies and theme assets and include their required notices. Review the working project name, app icon, wordmark treatment, and unofficial statement before release. Use project-owned artwork and review any third-party marks or assets before distribution.
