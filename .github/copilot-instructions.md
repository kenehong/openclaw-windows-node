# OpenClaw Windows Hub — Copilot Instructions

Windows companion suite for OpenClaw. .NET 10 / WinUI 3 monorepo. See `README.md` and `DEVELOPMENT.md` for full detail.

## Required validation (run after every change)

From repo root (`openclaw-windows-node/`):

```powershell
./build.ps1
dotnet test ./tests/OpenClaw.Shared.Tests/OpenClaw.Shared.Tests.csproj --no-restore
dotnet test ./tests/OpenClaw.Tray.Tests/OpenClaw.Tray.Tests.csproj --no-restore
```

Do not claim completion without reporting validation results. If a build/test is blocked by a locked output assembly (running tray exe), stop that process and rerun.

### First-run / worktree gotchas

- `dotnet test --no-restore` silently no-ops in a fresh worktree where the test `bin/` doesn't exist yet (reports "Build succeeded" then exits 0 with no tests run). On first run, omit `--no-restore` or `dotnet build` the test project first. Subsequent runs are fine with `--no-restore`.
- In linked git worktrees, set `OPENCLAW_REPO_ROOT` to the worktree path before running tests that discover the repo root, e.g. `$env:OPENCLAW_REPO_ROOT='D:\github\moltbot-windows-hub.<worktree>'`.
- Prefer isolated worktrees for PR validation. Use `git-wt`; note `wt.exe` may resolve to WorkTrunk instead of Windows Terminal — use the full Windows Terminal path when explicitly launching it.

### Running a single test

```powershell
dotnet test --filter "FullyQualifiedName~AgentActivityTests"
dotnet test --filter "Name=SpecificTestMethod"
```

### Build individual projects

```powershell
.\build.ps1 -CheckOnly                 # verify prereqs
.\build.ps1 -Project WinUI             # tray only
dotnet build src/OpenClaw.Tray.WinUI -r win-arm64        # ARM64
dotnet build src/OpenClaw.Tray.WinUI -r win-x64          # x64
dotnet build src/OpenClaw.Tray.WinUI -r win-x64 -p:PackageMsix=true   # MSIX (camera/mic consent)
dotnet build src/OpenClaw.CommandPalette -p:Platform=x64               # CmdPal (explicit platform required)
```

ARM64 hosts: both Tray and CommandPalette must be built for `arm64` — mixing breaks WebView2 and deep links.

Cross-platform builds of WinUI projects on Linux/macOS require `-p:EnableWindowsTargeting=true`. `OpenClaw.Shared` builds anywhere.

## Architecture (the parts that span files)

Monorepo projects under `src/`:

| Project | Role |
|---|---|
| `OpenClaw.Shared` | Gateway WebSocket client (`OpenClawGatewayClient.cs`), models, logging interface. Cross-platform. |
| `OpenClaw.Tray.WinUI` | Primary WinUI 3 tray app (Molty). Owns App.xaml.cs event routing, Services/ (settings, logging, hotkeys, deep links), Windows/ (Settings, WebChat, Status, Command Center), Helpers/ (icons). |
| `OpenClaw.Cli` | WebSocket validator that reuses tray settings from `%APPDATA%\OpenClawTray\settings.json`. |
| `OpenClaw.WinNode.Cli` | Node-mode CLI surface. |
| `OpenClaw.CommandPalette` | PowerToys Command Palette extension. Requires explicit `-p:Platform=x64|arm64`. |

Dependency direction: Tray.WinUI → Shared; CommandPalette → Shared; tests → respective targets.

### Gateway protocol (Shared/OpenClawGatewayClient.cs)

Connect → wait for `challenge` event → send token → receive `connected`. Reconnect with exponential backoff `1s,2s,4s,8s,15s,30s,60s` (max). Status surfaced via `StatusChanged`. Event types parsed: `agent` (stream=job|tool), `chat`, `health`, `session`, `usage`. Notifications classified first by structured fields (`type`/`category`/`notificationType`), then by keyword fallback.

### Tray app subsystems

- **Tray icon (Helpers/IconHelper.cs)** — GDI handle discipline: Create bitmap → `GetHicon` → `Icon.FromHandle` → `Clone` → `DestroyIcon(hIcon)`. Icons cached per state to avoid GDI churn (Windows process limit ~10k handles).
- **WebView2 chat (Windows/WebChatWindow.xaml.cs)** — Singleton; window is **hidden, not disposed** on close to preserve state. User data folder isolated at `%LOCALAPPDATA%\OpenClawTray\WebView2`. Navigation guard cancels nav outside allowed host.
- **Session tracking** — 5s poll via `RequestSessionsAsync`. Display selection: active main session wins; sticky to currently displayed session if still active; otherwise most recently active sub-session; 3s debounce against flipping.
- **Logging (Services/)** — File log at `%LOCALAPPDATA%\OpenClawTray\openclaw-tray.log`, rotated at 5 MB to `.log.old`, thread-safe. Auth tokens are never logged.
- **Exec policy (Node mode)** — `system.run` gated by `%LOCALAPPDATA%\OpenClawTray\exec-policy.json` (`{ defaultAction, rules[] }`), separate from gateway-side `~/.openclaw/exec-approvals.json`. Wrapper payloads (`cmd /c`, `powershell -Command`, `pwsh -EncodedCommand`, `bash -c`) are unwrapped and re-matched. Dangerous env overrides (`PATH`, `PATHEXT`, `NODE_OPTIONS`, `GIT_SSH_COMMAND`, `LD_*`, `DYLD_*`) are rejected.

### Node mode allowlist

The gateway enforces an explicit per-command allowlist under `gateway.nodes.allowCommands` in `~/.openclaw/openclaw.json`. Wildcards do **not** work. Privacy-sensitive commands (`screen.record`, `camera.snap`, `camera.clip`, `tts.speak`) are opt-in and must be added explicitly.

## Conventions specific to this repo

- **Korean-friendly agent comms** — keep responses short and clear (한글 OK), per `AGENTS.md`.
- **Test isolation for `SettingsManager`** — Tray tests must **not** use `new SettingsManager()` against the real `%APPDATA%\OpenClawTray\settings.json`. Pass a temp directory or set `OPENCLAW_TRAY_DATA_DIR` before the test process starts. Only exception: tests that intentionally validate real-user-settings behavior.
- **Solution file is `.slnx`** (`openclaw-windows-node.slnx`), not `.sln`.
- **CommandPalette deploy loop** — use `.\tools\cmdpal-dev.ps1 cycle` (remove → build → `Add-AppxPackage -Register` → reload). After deploy, run "Reload" in Command Palette.
- **Logs / diagnostics live under `%LOCALAPPDATA%\OpenClawTray\`** — `openclaw-tray.log`, `WebView2/`, `exec-policy.json`. Settings live under `%APPDATA%\OpenClawTray\settings.json`.
- **Activity Stream stores metadata only** — command name / status / duration. Never payloads, screenshots, recordings, or secrets.
- **Quick Send requires `operator.write` scope.** `missing scope: operator.write` → token scope problem (update token). `pairing required` / `NOT_PAIRED` → device approval problem (approve in gateway).
- Project-context wiki: `C:\Users\kehong\repo\work-iq-cli-workspace\docs\knowledge\wiki\projects\claw-windows.md`.

## Variant worktrees

Sibling directories `openclaw-windows-node-Companion-Var*`, `-Settings*`, `-RBrid` are independent git checkouts/worktrees of the same project for parallel feature work. The same validation rules apply in each. Don't cross-edit between them without intent.
