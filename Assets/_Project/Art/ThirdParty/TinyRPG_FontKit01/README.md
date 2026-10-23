# Tiny RPG – Font Kit I (placeholder fonts)

- **Source:** https://tiopalada.itch.io/tiny-rpg-font-kit-i
- **Author:** Gabriel "tiopalada" Lima (https://tiopalada.art)
- **License:** CC0 1.0 (public domain dedication); see `license.html`. No credit is required; we credit the author anyway in the main README.

The files here are the pack exactly as downloaded (TTFs and their preview sheets, original names, unedited).

## Native sizes

The fonts' outlines sit on a grid of **2 font units per pixel**, so a font's native pixel size is its units per em ÷ 2. TextMeshPro font assets are rasterised at exactly that size and drawn at whole multiples of it (with the UI canvas at a whole-number scale, see `PixelUiScale`), so every font pixel covers whole screen pixels.

| Font | Units per em | Native size | Cap height | Used for |
|---|---|---|---|---|
| `TinyRPG-FineFantasyStrategies.ttf` | 24 | 12 px | 8 px | Body text and menus (bitmap atlas, point sampled) |
| `TinyRPG-BrilliantStrength.ttf` | 28 | 14 px | 11 px | Titles, banners, Victory / Defeat (bitmap atlas) |
| `TinyRPG-FineFantasyStrategiesItalicNumbers.ttf` | 24 | 12 px | 8 px | Floating damage numbers, the death counter and the status / role tags over units (SDF atlas sampled at 48 px, so they can carry a dark outline; drawn at whole-pixel sizes) |
| `TinyRPG-BadgeFont.ttf` | 26 | 13 px | 9 px | Not used yet |

Font assets live in `Assets/_Project/Data/UI/Fonts/` and are built by *Friendship Warriors → Art → Build Pixel UI Skin* (`PixelUiBuilder`; *Rebuild Font Atlases* regenerates them). Each covers ASCII plus the Latin-1 letters the fonts draw.
