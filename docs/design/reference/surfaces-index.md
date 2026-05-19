# Surfaces × Concepts Parity Matrix

Reads the canonical list of concepts (rows) against the surfaces that
ship today (columns). `✅` = concept is exposed; `–` = intentionally
omitted; `❓` = should probably appear but currently does not (= work
item). Update this table when you add a concept, a surface, or
implement an `❓` cell.

| Concept                    | Tray flyout | Tray Permissions flyout | Permissions page | Onboarding wizard | Mission Control |
|----------------------------|:----------:|:----------------------:|:----------------:|:-----------------:|:---------------:|
| **States**                 |            |                        |                  |                   |                 |
| Gateway connection         | ✅ header   | –                      | ❓                | ✅                 | ✅               |
| Node mode (master switch)  | –          | ✅                      | ✅                | ✅                 | ✅               |
| Pairing state              | ✅ header   | –                      | –                | ✅                 | ✅               |
| Sessions metric            | ✅          | –                      | –                | –                 | ✅               |
| Usage metric               | ✅          | –                      | –                | –                 | ✅               |
| **Capabilities**           |            |                        |                  |                   |                 |
| Browser control            | –          | ✅                      | ✅                | ✅                 | –               |
| Camera                     | –          | ✅                      | ✅                | ✅                 | –               |
| Canvas                     | ✅ action   | ✅                      | ✅                | ✅                 | –               |
| Screen capture             | –          | ✅                      | ✅                | ✅                 | –               |
| Location                   | –          | ✅                      | ✅                | ✅                 | –               |
| Text-to-speech             | ✅ Voice    | ✅                      | ✅                | –                 | –               |
| Speech-to-text             | –          | ✅                      | ✅                | –                 | –               |
| **Actions**                |            |                        |                  |                   |                 |
| Dashboard                  | ✅          | –                      | –                | –                 | –               |
| Chat                       | ✅          | –                      | –                | –                 | –               |
| Quick Send…                | ✅          | –                      | –                | –                 | –               |
| Reconfigure…               | ✅          | –                      | –                | –                 | –               |
| Companion Settings…        | ✅          | –                      | (parent)         | –                 | –               |
| About                      | ✅          | –                      | –                | –                 | –               |
| Close                      | ✅          | –                      | –                | –                 | –               |

## How to read this

- A `✅` cell means the surface renders the concept *using its concept
  file* (label, icon, copy). If the surface deviates, file a bug.
- A `❓` cell is a parity gap — a candidate work item for that surface.
- Move a cell to `–` only after writing a one-line rationale below.

### Rationale for `–` cells

- *Sessions / Usage on Permissions page* — Permissions is about what
  the node will do, not what has happened. Metrics live in their own
  surfaces (tray, Mission Control).
