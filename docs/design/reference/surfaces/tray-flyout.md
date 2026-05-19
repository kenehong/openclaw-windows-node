# Surface: Tray Flyout (`TrayMenuWindow`)

The Windows-only quick menu that opens from the tray icon. Width
320 px, height up to ~400 px (scrolls), `OverlayCornerRadius` 8 px,
acrylic backdrop applied in code-behind via `BackdropHelper`. Outer
1 px border uses `SurfaceStrokeColorDefaultBrush`.

This document is **composition only** — every label, icon, copy
string is owned by a concept file.

## Layout (top to bottom)

1. **Gateway header row** — `concepts/states/gateway-connection.md`
2. *(divider)*
3. **Sessions metric row** — `concepts/states/sessions.md` *(TBD)*
4. **Usage metric row** — `concepts/states/usage.md` *(TBD)*
5. *(divider)*
6. **Permissions** sub-menu (chevron) — `concepts/categories/permissions.md` *(TBD)*
7. **Dashboard** — see iconography "Dashboard"
8. **Chat**
9. **Canvas** — `concepts/capabilities/canvas.md` (action variant)
10. **Voice** — currently the TTS short label; see `text-to-speech.md`
11. **Quick Send…**
12. **Reconfigure…**
13. *(divider)*
14. **Companion Settings…** (trailing `Ctrl+Alt+;` hint)
15. **About**
16. **Close**

## Row anatomy

`[16 px icon] [label, BodyTextBlockStyle] [trailing slot]`

- Icon column: 24 px wide (icon centered).
- Trailing slot is used by Permissions for the chevron and by
  Companion Settings for the keyboard-accelerator caption.
- Sub-menus are opened as **separate flyout windows**, not nested
  popups (`AddFlyoutMenuItem` → `flyoutWindow.AddMenuItem` …).

## Divider rule

Exactly three dividers, separating four regions:

1. Header / metrics
2. Permissions (and other status sub-menus)
3. Primary actions (Dashboard … Reconfigure)
4. App lifecycle (Companion Settings … Close)

## Reference screenshots

Place under `docs/design/assets/tray-flyout-{light,dark,hc}.png`. The
PR that adds those screenshots must also update this section.

## Code anchors

- Window XAML: `src/OpenClaw.Tray.WinUI/Windows/TrayMenuWindow.xaml`
- Code-behind: `src/OpenClaw.Tray.WinUI/Windows/TrayMenuWindow.xaml.cs`
- Population (the *actual* list of rows): `src/OpenClaw.Tray.WinUI/App.xaml.cs`
  — search for `menu.AddMenuItem("Dashboard"` to find the start of
  the action block.
- Row helpers: `AddMenuItem`, `AddFlyoutMenuItem`,
  `AddMenuItemWithHint`, `AddMenuItemWithTrailingElement`.

## Do / Don't

- ✅ All glyphs go through `FluentIconCatalog.Build(...)`.
- ✅ Trailing `Ctrl+…` hints are right-aligned captions.
- ❌ Don't add a row whose label is not in `naming.md`.
- ❌ Don't reorder regions without updating this surface doc and
  `surfaces-index.md`.
