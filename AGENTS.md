# Repository Guidelines

## Project Structure & Module Organization
`PSVR2Toolkit.sln` is the root solution for all projects.

- `projects/psvr2_openvr_driver_ex/`: main native OpenVR driver extension (C++).
- `projects/libcustomshare/`: shared native library used by the driver.
- `projects/PSVR2Toolkit.IPC/`: .NET Standard IPC client library (`netstandard2.0`).
- `projects/PSVR2Toolkit.App/`: WPF helper app (`net472`).
- `projects/UnifiedTelemetry.*_stub/`: compatibility/export stubs.
- `projects/shared/`: cross-project protocol headers (for example `ipc_protocol.h`).
- `include/`: public C API headers.
- `assets/`: icons and visual assets.

## Build, Test, and Development Commands
Use a Visual Studio Developer PowerShell on Windows.

- `nuget restore .`  
  Restores NuGet dependencies for the solution.
- `msbuild /m /p:Configuration=ReleaseCI .`  
  CI-equivalent release build.
- `msbuild /m /p:Configuration=Debug .`  
  Local debug build for development.
- `msbuild /m /t:Clean,Build /p:Configuration=Release .`  
  Clean rebuild before packaging or release validation.

Artifacts are typically emitted to each project output directory unless `OutDir` is set.

## Coding Style & Naming Conventions
- C++: 2-space indentation, braces on the same line for functions/blocks, namespace-scoped code in `psvr2_toolkit`.
- C#: 4-space indentation, K&R braces, nullable reference types where already used.
- Naming follows existing code: C++ types/functions in `PascalCase`, members often `m_`-prefixed; constants commonly `k_` or all-caps macros.
- Keep headers and IPC structs stable and backward-aware; protocol changes must be versioned.

## Testing Guidelines
There is no dedicated unit test project yet. Treat build verification as the minimum gate:

- Build `Debug` locally before opening a PR.
- Build `ReleaseCI` before merge to mirror GitHub Actions (`.github/workflows/msbuild.yml`).
- For IPC/protocol edits, validate handshake and gaze/trigger paths manually against the toolkit app or a consuming client.

## Commit & Pull Request Guidelines
Recent history uses short, imperative commit subjects (for example: `Implement ...`, `Cleanup ...`, `Update ...`) and references issues/PRs like `(#32)` when applicable.

- Keep subject lines concise and action-oriented.
- Group related changes in one commit; avoid mixing refactors with behavior changes.
- PRs should include: what changed, why, risk/compatibility notes, and manual validation steps.
- Link relevant issues and attach screenshots/log snippets for UI/runtime behavior changes.
