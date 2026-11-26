# Changelog

Launcher release versions follow [Semantic Versioning](https://semver.org/) once the public interface is defined. Visual reference revisions have their own version history.

## Unreleased

### Launcher 0.2.0

- Add a separate `--settings` entry with English and Simplified Chinese, a built-in theme preview, and controls for startup mode and reduced motion.
- Save versioned preferences next to the executable with atomic replacement and safe defaults. Explicit launch options take precedence.
- Add user-operated creation and removal of launcher desktop shortcuts and an independent multiresolution product icon.
- Package a self-contained Windows x64 portable ZIP with a SHA-256 sidecar. Preserve the Windows launch fallback and bounded WebView2 playback.

### Launcher 0.1.0

- Add a Windows 11 x64 MVP with 5-second, 9-second, direct and animation-only launch options.
- Host the NEURAL OVERDRIVE theme in a borderless WebView2 window with Escape, a static reduced-motion frame and bounded playback.
- Discover the registered ChatGPT desktop app, use Windows app activation when needed and attempt a foreground handoff for a verified window.
- Continue Windows startup through renderer failures and suppress concurrent launcher invocations within a user session.
- Add the pinned project-local toolchain, automated checks and a Windows acceptance guide.

### Project setup

- Set up the project structure, collaboration rules, and roadmap.
- Archive the approved NEURAL OVERDRIVE `v0.3.0` visual reference.
- Complete the Phase 1 feasibility audit and record the proposed Windows launcher design.
