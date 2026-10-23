# Tiny Tactics – Battle Kit I (placeholder art)

- **Source:** https://tiopalada.itch.io/tiny-tactics---battle-kit-i
- **Author:** Gabriel "tiopalada" Lima (https://tiopalada.art)
- **License:** CC0 1.0 (public domain dedication); see `license.html`. No credit is required; we credit the author anyway in the main README.

The files here are the pack exactly as downloaded (original names, unedited). They are placeholders until the game has its own art.

## What we use

| Files | Used for |
|---|---|
| `20240427{fighter,mage,cleric}-*{SE,NE}.png` | Unit sheets (`Data/UnitSheets/TinyTactics_*.asset`). SE is the front ¾ pose, NE the back ¾ pose, both facing screen-right. Frames are 32×32 with the feet 4 px up. The walking strips have two rows: the top row strides (walk), the bottom row barely steps (we use it as idle). The **charging** files are filed the other way round in the pack (its NE file shows the front), so the sheet swaps them. |
| `20240429weapons-*attack{SE,NE}.png` | Weapon swings over the attack frames (`TinyTactics_Weapons.asset`): 48×48 frames centred on the unit's 32×32 frame. Iron Sword → swords, Hatchet → knives, Scepter → rods, Wooden Staff → staves. Iron Axe and Wooden Sword are unused; rifles have no art. |
| `20240420tinyTacticsTileset00.png` | Props cut from the tileset (tree, rock, bush, boulder, stump, log) and the terrain colours. The iso tiles themselves are not used: our battlefield is a 3D mesh. |

Job → character: Cadet, Vanguard, Marksman → fighter; Corpsman, Warder → cleric; Evoker → mage. Jobs that share a character differ by outfit colour (the job colour), teams by the accent colour (Player blue, Enemy red), and fighters by hair colour (picked from the unit's name).

Rebuild everything from these files with *Friendship Warriors → Art → Build Tiny Tactics Placeholders* (`TinyTacticsArtBuilder`). Import settings (point filter, uncompressed, readable, no mipmaps) are applied by `TinyTacticsImporter`.
