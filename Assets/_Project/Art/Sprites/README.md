# Unit sprites: the paper doll

Units without a hand-drawn job sprite are drawn as a **paper doll**. A unit's sprite is a stack of part sheets
(body, head, face, hair, headgear, body gear, weapon, shield). The parts are painted in a few **key colours**, and each
unit gets its own colours for those keys: its job colour, its team colour, and a skin tone, hair colour and hairstyle
picked from its name. Change a unit's gear and its sprite changes too.

The part PNGs in `Parts/` are the source of truth. **Friendship Warriors → Art → Generate Paper-Doll Parts** drew the
first versions in code. They are a starting point for editing by hand. Running the generator again only fills in
missing files, so your edits are safe. **Regenerate Paper-Doll Parts (Overwrite)…** asks before it replaces anything.

## The sheet grid

Every part is one **320 × 240 PNG**: 8 columns × 6 rows of **40 × 40 frames**. The coordinates below are pixels from
the top-left corner, as Aseprite shows them.

|              | Front ¾ (x 0–159)                     | Back ¾ (x 160–319)                     |
|--------------|---------------------------------------|----------------------------------------|
| **Idle**     | y 0–39: 2 frames at x 0, 40           | 2 frames at x 160, 200                 |
| **Walk**     | y 40–79: 4 frames at x 0, 40, 80, 120 | 4 frames at x 160, 200, 240, 280       |
| **Attack**   | y 80–119: 3 frames at x 0, 40, 80     | 3 frames at x 160, 200, 240            |
| **Cast**     | y 120–159: 2 frames at x 0, 40        | 2 frames at x 160, 200                 |
| **Hurt**     | y 160–199: 1 frame at x 0             | 1 frame at x 160                       |
| **KO**       | y 200–239: 1 frame at x 0 (lying)     | 1 frame at x 160 (lying)               |

Frame `n` of a row starts at `x = n × 40` (front) or `x = 160 + n × 40` (back), and at `y = row × 40`. Unused cells
stay transparent.

- **Both poses face screen-right.** The game mirrors them for the other two camera views.
- **Feet stand on row 39** of the frame, centred around x 20. The sprite's pivot is the bottom-centre of the frame,
  at 32 pixels per world unit, so a unit is about 1¼ tiles tall.
- **The KO frame lies along the ground**, with the head on the left.
- **Timing** (seconds per frame): idle 0.55, walk 0.15, attack 0.12, cast 0.25 (looped while charging a spell),
  hurt 0.35. Idle and cast loop. Attack and hurt play once and then return to idle.
- **Walk frames aren't shown in the game yet.** Units move instantly, so the walk frames are only drawn ahead of time.

Frames are 40 × 40 rather than 32 × 40 so that the lying KO pose fits inside one frame.

## Layer order

The parts are stacked per pose, from the bottom layer to the top:

| Front ¾                              | Back ¾                               |
|--------------------------------------|--------------------------------------|
| Shield (far arm, behind the body)    | Weapon (far side, behind the body)   |
| Body (skin, undersuit, boots)        | Body                                 |
| Body gear (clothing / armor / robe)  | Body gear                            |
| Head                                 | Head                                 |
| Face (eyes, brows, mouth)            | Face (leave empty: no face from behind) |
| Hair                                 | Hair                                 |
| Headgear (hat / helmet)              | Headgear                             |
| Weapon (near hand, in front)         | Shield (now nearest the camera)      |

The top opaque pixel wins. A part can hide what's under it by painting over it, but it can't cut a hole in a lower
layer.

## Key colours

Paint with the palette in `../Palettes/PaperDollKeys.gpl` (the same colours are in `PaperDollKeys.png`). Only an exact
RGB match is recoloured. A colour that's one step off stays as you painted it, so keep the palette locked while you
paint.

| Ramp                   | Light     | Mid       | Dark      | Becomes                                   |
|------------------------|-----------|-----------|-----------|-------------------------------------------|
| **Skin** (key)         | `#F7CEA6` | `#DEA678` | `#B07452` | One of 5 skin tones, picked from the unit's name |
| **Hair** (key)         | `#825634` | `#603C24` | `#40281A` | One of 6 hair colours, picked from the name. Brows use Hair dark |
| **Job primary** (key)  | `#7E8C60` | `#606C46` | `#404830` | The job's `PlaceholderColor` (mid), with a lighter and a darker shade |
| **Team accent** (key)  | `#5A96E6` | `#3460B4` |           | Player blue or Enemy red                  |

These colours are never recoloured: outline `#16121C`, metal `#CED4DE` `#969EAC` `#606676`, leather `#8A5C3A`
`#603E28`, brass light `#F0C860`, brass `#C4963A`, copper `#CC6E3C`, lens `#96E6FF`, lens dark `#3C5A6E`, eye
`#1E1824`, eye white `#FAF8F0`, mouth `#965046`, undersuit `#4E4E5C` `#343440`, aether glow `#82DCFF`, glow core
`#E8FAFF` and muzzle flash `#FFE878`.

The house style is a 1 px dark outline around each part, 2–3 tone shading with light from the upper left, and
brass-and-leather steampunk military kit.

## Getting started in Aseprite

1. Open a part PNG straight from `Assets/_Project/Art/Sprites/Parts/…` (**File → Open**). Aseprite reads PNGs
   directly, so no `.aseprite` file is needed.
2. Load the key palette: **Palette → Load Palette** (the ☰ menu above the palette) and pick
   `Assets/_Project/Art/Palettes/PaperDollKeys.gpl`.
3. Optional: **View → Grid → Grid Settings**, set to 40 × 40, to see the frame cells.
4. Paint. **Keep the canvas at 320 × 240 and keep every frame in its cell.** Don't resize the canvas or move frames.
5. **File → Save As** (or Export As) **PNG** to the same path. Unity reimports it when you switch back to the
   editor.

Tips:

- Check both poses and every animation row. The front and back blocks are separate drawings.
- The face part is front only. Leave its back cells empty.
- Leave pixels transparent where a part has nothing. Don't fill them with a background colour.

## Adding a new part and connecting it to an item

Parts are chosen by **type** first:

- headgear and body gear by the item's `ArmorType` (Hat, Helmet, Clothing, Armor, Robe)
- weapons by `WeaponType`
- any shield uses the Buckler part

A unit with nothing in a slot draws nothing there. A type with no part also draws nothing, so the unit falls back to
its body and undersuit.

To give one item its own look:

1. Copy the closest existing part, for example `Parts/Headgear/headgear_hat_field_cap.png` to
   `Parts/Headgear/headgear_hat_officer_cap.png`, and paint it. Parts must be 320 × 240. The importer sets the right
   settings for any PNG under `Parts/` automatically: readable, Point filter, uncompressed, no mipmaps.
2. Select `Assets/_Project/Resources/PaperDoll/SpritePartCatalog.asset`.
3. Under **Item Overrides**, add an entry, and drag in the item (an `EquipmentDefinition`) and the new PNG.

Item overrides win over the type default. To change the default for a whole type instead, swap the PNG in that type's
row of the catalog. Running the generator again updates those rows to point at the generated files again, but keeps
the item overrides.

A new hairstyle is a new PNG added to **Hair Styles**. Units pick their hairstyle from their name, so adding a style
can change which hairstyle existing units get.

## Replacing a whole job sprite

To draw a job by hand instead of using the paper doll, set **Front Sprite** and **Back Sprite** on its
`JobDefinition` (in `Data/Jobs`). Use one front ¾ and one back ¾ sprite, both facing screen-right, with the pivot at
the feet. A hand-drawn job sprite always wins over the paper doll. The order is: job sprite, then paper doll, then the
generated placeholder (when no catalog exists).

Hand-drawn sprites are single frames. They aren't animated or recoloured per unit. A knocked-out unit is turned on its
side and tinted grey.
