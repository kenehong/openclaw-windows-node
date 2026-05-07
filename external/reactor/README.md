# Vendored Microsoft.UI.Reactor

This directory contains a vendored snapshot of [`microsoft/microsoft-ui-reactor`](https://github.com/microsoft/microsoft-ui-reactor) plus the chat sample's `Chat.Model` and `Chat.UI` projects from `samples/apps/chat`. It is consumed by `OpenClaw.Tray.WinUI` to render the native chat UI (replacing the previous WebView2-hosted gateway web client).

## Why vendored?

`Microsoft.UI.Reactor` is not published to public NuGet, and the upstream repository is internal. Vendoring keeps the build hermetic (no internal feeds required) and lets CI build offline.

## Provenance

- Upstream: <https://github.com/microsoft/microsoft-ui-reactor>
- Snapshot date: 2026-05-05
- License: MIT (see `LICENSE`)

## Layout

```
external/reactor/
  src/
    Reactor/                       Core declarative UI framework
    Reactor.Analyzers/             Roslyn analyzers (netstandard2.0, bundled)
    Reactor.Localization.Generator/  Source generator for .resw → strongly-typed accessors
  samples/apps/chat/
    Chat.Model/                    Provider-neutral chat state, reducer, IChatDataProvider
    Chat.UI/                       Reusable Reactor chat components (Timeline, InputBar, etc.)
  Directory.Build.props            (vendored from upstream)
  Directory.Build.targets          (vendored from upstream)
```

## Local edits

A minimal set of edits has been applied so the vendored projects build cleanly inside this repo:

- **TFM**: `Reactor.csproj` bumped from `net9.0-windows10.0.22621.0` to `net10.0-windows10.0.22621.0` to match the rest of this repository (which targets net10).

No other source files have been modified. Keep edits minimal — when refreshing from upstream, re-apply only the TFM bump above.

## Refreshing from upstream

```powershell
# 1. Pull a fresh clone of the upstream repo somewhere outside this tree.
git clone https://github.com/microsoft/microsoft-ui-reactor.git D:\reactor-chat\reactor-chat

# 2. Re-run the vendor copy (mirrors src/Reactor*, samples/apps/chat/{Chat.Model,Chat.UI},
#    Directory.Build.{props,targets}, LICENSE). bin/obj are stripped.

# 3. Re-apply the TFM bump in src/Reactor/Reactor.csproj
#    (net9.0-windows10.0.22621.0 → net10.0-windows10.0.22621.0).

# 4. dotnet restore && ./build.ps1
```

## Reuse contract

`Chat.UI` only depends on `Chat.Model` plus Reactor/WinUI APIs — no app-specific transports or services. To wire the UI to the OpenClaw gateway, see `src/OpenClaw.Tray.WinUI/Chat/OpenClawChatDataProvider.cs`, which adapts `OpenClawGatewayClient` events into `ChatThread` / `ChatEvent` / `ChatTimelineState`.
