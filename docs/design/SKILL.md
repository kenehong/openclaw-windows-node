---
name: openclaw-design
description: |
  Design reference for the OpenClaw Windows node UI. Two surface
  families, both WinUI 3 in `OpenClaw.Tray.WinUI`: **Tray** (system
  tray icon flyouts — chat flyout + right-click menu) and **App**
  (Companion Settings window pages — Permissions, Connection, Chat,
  …). This skill is the single entry point for finding canonical
  labels, icons, copy, tokens, and surface composition. Invoke for
  any UI work in the Windows node; do NOT invoke for generic
  XAML/Fluent questions (use `Windows-XAML-Skill` / `winuxe`).

  Triggers: `odr:`, `openclaw:`, `openclaw-design:`, or any mention
  of "openclaw design", "win node design", "openclaw permissions",
  "openclaw capabilities", "openclaw canvas concept", "openclaw
  iconography", "openclaw tokens", "openclaw tray flyout".
location: repo
version: 0.2.0
---

# Skill: OpenClaw Windows-node design reference

## Folder map

```
docs/design/
├── SKILL.md                              ← this file (entry + procedure)
└── reference/                            ← everything the skill reads
    ├── naming.md                         ← canonical product vocabulary
    ├── iconography.md                    ← concept → Segoe Fluent glyph
    ├── tokens.md                         ← Fluent 2 color/type/spacing/radius
    ├── surfaces-index.md                 ← concept × surface parity matrix
    ├── win11-settings.md                 ← external: Win 11 Settings + Fluent 2 links
    ├── settings-controls.md              ← external: SettingsCard slot reference
    ├── winui-gallery.md                  ← external: WinUI 3 Gallery
    ├── cdr-001-adopt-win11-settings.md   ← system-level CDR
    ├── concepts/                         ← ⭐ source of truth, one file per concept
    │   ├── _template.md, _decisions/, capabilities/, states/
    └── surfaces/                         ← how a screen composes concepts
        ├── tray-flyout.md, permissions-page.md
```

**Concept files own** label, icon, copy, states, resw key, code
anchors. **Surface files compose** concepts — never redefine them.
External-reference files (win11-settings, settings-controls,
winui-gallery, cdr-001) cite upstream authority — open only when
you need to justify a rule.

## Procedure

1. **Ask which surface** the user is editing — tray flyout, tray
   right-click menu, or a specific app page (file path if any).
2. **Read in order:**
   `reference/naming.md` → `reference/iconography.md` →
   `reference/tokens.md` → `reference/surfaces-index.md` →
   relevant `reference/concepts/**` → relevant
   `reference/surfaces/**`. Open the external-reference files
   (`reference/win11-settings.md`, `settings-controls.md`,
   `winui-gallery.md`, `cdr-001-adopt-win11-settings.md`) only
   when you need to justify a rule, look up a control's slot API,
   or cite the system CDR.
3. **Pull verbatim** from each needed concept file: canonical
   label, description, Segoe Fluent glyph, state variants, resw
   key, code anchors, Do/Don't list. Don't paraphrase.
4. **Model layout** on the nearest existing surface doc
   (`reference/surfaces/permissions-page.md` for app pages,
   `reference/surfaces/tray-flyout.md` for tray surfaces).
5. **Report drift candidates** found in code — hard-coded colors
   instead of `SystemFillColor*Brush`, literal Segoe glyphs not in
   `FluentIconCatalog`, copy diverging from resw, emoji on a WinUI
   surface, hand-rolled `Border + Grid` rows on an app page. Don't
   silently fix them — list with file:line.
6. **Missing concept?** Author the concept file first using
   `reference/concepts/_template.md` — never invent label/icon
   locally.

## Deliverable

Four sections:

1. **Concepts the surface should expose** — each linked to its
   `reference/concepts/**` file.
2. **Canonical labels, icons, copy** — verbatim from concept files.
3. **Suggested layout / row anatomy / token usage** — modeled on
   the nearest existing surface doc.
4. **Drift candidates** — with file:line citations.

## Hard rules

- **Concepts are immutable** without a CDR. Renaming "Canvas" →
  "Whiteboard" requires a file under
  `reference/concepts/_decisions/`. System-wide design decisions
  (e.g. "adopt Win 11 Settings") live in `reference/cdr-*.md`.
- **No new icon, color, or token** outside `reference/iconography.md`
  / `reference/tokens.md`. Add it to the catalog first, same PR.
- **Don't translate** canonical product terms (Canvas, Permissions,
  Companion Settings, Quick Send, Reconfigure, Node mode).
- **Don't duplicate** concept content inside a surface doc — link.
- **App pages use `CommunityToolkit.WinUI.Controls.SettingsCard` /
  `SettingsExpander`.** No hand-rolled `Border + Grid` rows on a
  new app page. See
  [`reference/settings-controls.md`](./reference/settings-controls.md).
- **Icons render as Segoe Fluent via `FluentIconCatalog` /
  `FontIcon`.** Emoji are documentation reference only — never a
  WinUI render target.
- **Labels canonical in English here; localized strings live in**
  `src/OpenClaw.Tray.WinUI/Strings/<locale>/Resources.resw`. Each
  concept file records its resw key under "code anchors".

## Platform compliance (delegated to winuxe)

Every XAML produced from this skill must also satisfy winuxe's
**5 Critical Rules**:

1. `HighContrastAdjustment = ApplicationHighContrastAdjustment.None`
   at app level in `App.xaml.cs`.
2. Every custom resource defines **Light, Dark, and HighContrast**
   variants.
3. No hardcoded colors — all colors and brushes use `{ThemeResource}`.
4. No raw `FontSize` / `FontWeight` — use system typography styles
   (`BodyTextBlockStyle`, `CaptionTextBlockStyle`, `TitleTextBlockStyle`, …).
5. **4 px grid** — every margin / padding / size is a multiple of 4.

See winuxe `guide.md`, `docs/code-review-checklist.md`, and
`UXE_Wiki/Accessibility/HighContrastAdjustment.md` for details.

## Relationship to other skills

`winuxe` / `Windows-XAML-Skill` cover Windows-platform-wide design
(Fluent, theme dictionaries, HC, semantic tokens). `openclaw-design`
is **narrower** — OpenClaw product concepts only. When both apply,
run this skill first to fix concept identity (label, icon, copy),
then the platform skill for XAML implementation details.
