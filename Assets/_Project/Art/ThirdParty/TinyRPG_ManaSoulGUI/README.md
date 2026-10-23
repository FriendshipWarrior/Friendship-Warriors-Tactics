# Tiny RPG – Mana Soul GUI (placeholder UI)

- **Source:** https://tiopalada.itch.io/tiny-rpg-mana-soul-gui
- **Author:** Gabriel "tiopalada" Lima (https://tiopalada.itch.io)
- **License:** CC0 1.0 (public domain dedication); see `README.html`. No credit is required; we credit the author anyway in the main README.

The files here are the pack's PNGs exactly as downloaded (original names, unedited). Sprites are cut from them at runtime by `UiSkin` (frames of a fixed size, read left to right), so the files themselves never change. Import settings (readable, point filter, uncompressed, no mipmaps) are applied by `PixelUiImporter`; the skin asset (`Resources/UI/UiSkin.asset`) is built by *Friendship Warriors → Art → Build Pixel UI Skin* (`PixelUiBuilder`).

## What we use

| File | Frames | Used for |
|---|---|---|
| `20250420manaSoul9SlicesC-Sheet.png` | 1 of 96×96, 9-slice border 32 | Every window: menus, turn order, unit panel, banner, timed press, pause and title menus |
| `20250421manaSoulButtonB-Sheet.png` | 4 of 96×22: normal, selected, pressed, disabled; border 16 × 6 | Menu items (framed in the title and pause menus; only the selected item in battle menus) and, in its pressed state, the one-line strips (objective, hint) |
| `20250421horizontalCursorB-Sheet.png` | 5 of 37×14, looped | The feather pointer beside the selected menu item (mirrored to point right) |
| `20250421barC-Sheet.png` | 1 of 95×15, hollow inside (6, 4, 6, 3 px in) | HP, MP and CT gauges and the timed-press track; the fill is drawn in the hollow |

The panels' centres are 90% opaque by design, so the battlefield shows faintly through.

## Kept for later

- **Equipment slot frames** (`20250423*Frame-Sheet.png`: boots, chest, empty, helmet, neck, ring, pants, shield, weapon; 5 frames of 32×32): the future equipment / status screen.
- **Portrait frame** (`20250425portraitFrame-Sheet.png`, 48×72): a unit portrait in the status screen.
- **Headers and tabs** (`20250420manaSoulHeader*`, `20250420manaTab*`): page titles and tabs (Status / Abilities / Equipment).
- **Other panels** (`9Slices` A, B, D, E, F), **buttons** A and C, **cursors** (vertical A–C, horizontal A and C), **bars** A and B.
- **Buttons with icons** (close, exit, help, options, more, less, arrows), the **compass**, the **mouse cursors** and the **target cursor** (`20250421cursorTarget-Sheet.png`, a battlefield tile cursor).
