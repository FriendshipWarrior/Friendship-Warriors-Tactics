# Tiny RPG – Green Wilderness (terrain and props)

- **Source:** https://tiopalada.itch.io/tiny-rpg-green-wilderness (v1.5, bought by the project owner)
- **Author:** Gabriel "tiopalada" Lima (https://tiopalada.itch.io), the artist behind Tiny Tactics, so terrain and units share one style.
- **License:** [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/); see `README.HTML` (the pack's own notice). **Attribution is required**: the main README's *Credits* section gives it, and the game's credits screen must too once it exists. Commercial use and redistribution are allowed.
- **Artist's request:** don't feed the assets to AI models or use them for crypto. We don't: the files are only sliced into tiles and sprites by our own code (`PixelTerrainBuilder`) and placed on the battlefield.

Only the 32×32 tileset is kept here (`20240303tilesetWilderness32x32.png`, exactly as downloaded). The pack's 48×48 version and RPG Maker sheets aren't used.

## What we use

The sheet is a 22 × 19 grid of 32×32 cells (column, row from the top left). It's top-down art: grass and tall grass are drawn as overlays on the orange dirt, and the dark red-brown strips under raised dirt are cliff faces.

| Cells | Used for |
|---|---|
| (1,1), (0,4), (1,4), (2,4) | Grass tops (full grass cells laid over dirt), mirrored for more variety |
| (1,6), (12,6) | Dirt tops |
| (12,1) | Water tops (the lake's middle) |
| (1,9), (2,9) | Cliff faces on the sides of grass, dirt, sand and water columns. One cell is 2h tall and repeats down tall columns at fixed world heights. |
| (1,2), rows 13–22 | Grass lip: the grass edge hanging over the top of grass cliffs |
| (1,8), rows 22–31 | Dirt lip: the dirt edge at the top of dirt cliffs |
| Trees, bushes, rocks, mushrooms, flowers | Props (`Data/Props/Pine`, `Oak`, `Shrub`, `Mossy Rock`, `Mushroom`, `Wildflowers`), upright sprites at the units' pixel density |

Not used yet: tall grass (we have no tall-grass terrain), the edge and corner pieces (blob autotiles) for smooth grass/dirt/lake borders, bridges, fallen logs, the sign and cave entrances.

The pack has no stone or sand; those tops (and stone walls) are drawn by `PixelTerrainBuilder` in the pack's palette.

Rebuild with *Friendship Warriors → Art → Build Pixel Terrain* (also run by *Rebuild Battle Test Scene*). It writes `Art/Terrain/TerrainAtlas.png` and `Data/TerrainAtlas.asset`; this folder is never changed. Import settings (point filter, uncompressed, readable, no mipmaps) come from `PixelTerrainImporter`.
