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
  at 32 pixels per world unit. The game shows unit art 25% larger (`UnitView.SpriteScale` = 1.25), so a unit is
  about 1⅓ tiles tall. The scale is applied on the sprite's transform, so draw at the normal pixel size.
- **The KO frame lies along the ground**, with the head on the left.
- **Timing** (seconds per frame): idle 0.55, walk 0.15, attack 0.12, cast 0.25 (looped while charging a spell),
  hurt 0.35. Idle and cast loop. Attack and hurt play once and then return to idle.
- **Walk frames** play while a unit walks its route tile by tile (4 frames, looped).

Frames are 40 × 40 rather than 32 × 40 so that the lying KO pose fits inside one frame.

## Layer order

The parts are stacked per pose, from the bottom layer to the top:

| Front ¾                              | Back ¾                               |
|--------------------------------------|--------------------------------------|
| Shield (far arm, behind the body)    | Weapon (far side, behind the body)   |
| Body (skin, undersuit, boots)        | Body                                 |
| Body gear (clothing / armor / robe)  | Body gear                            |
| Head                                 | Head                                 |
| Face (eyes, nose, mouth)             | Face (leave empty: no face from behind) |
| Hair                                 | Hair                                 |
| Headgear (hat / helmet)              | Headgear                             |
| Weapon (near hand, in front)         | Shield (now nearest the camera)      |

The top opaque pixel wins. A part can hide what's under it by painting over it, but it can't cut a hole in a lower
layer.

## Style guide

The generated parts follow the look of FFT's battle sprites. FFT was used as a **visual reference only**: no pixels,
colours or shapes were copied, traced or imported from it, and its sprite rips must never be added to this repo. Keep
to these rules when drawing by hand so new art matches.

- **Chibi proportions.** About 2.4 heads tall: the head is about 15 px high and 14 px wide (rows 4–18 of the frame),
  the torso about 10 px (shoulders around row 18, hip around row 27), and the legs short, ending in chunky boots
  (rows 35–39, about 5 px wide with a toe pointing toward the facing side). Hands are round 3 × 3 mittens. Arms and
  legs are 3 px thick.
- **Front ¾ faces screen-right.** The face sits on the right half of the head. The near eye is big (2 × 3 with a
  white catch-light in its top corner) and the far eye narrow (1 × 3). The nose is a single shadow pixel and the mouth
  2 px. Hair hangs behind the ear on the far side.
- **Light comes from the upper left.** Every piece is shaded as a volume: the upper-left part in the light shade, the
  middle in mid, and the lower-right part in shadow. The highlight shade is only for the brightest spot of big round
  pieces (head, hair, helmet, pauldrons, metal).
- **Four shades plus an outline per material.** Each material ramp runs highlight, light, mid, shadow, outline.
  Lighter shades drift warm (toward yellow) and darker shades drift cool (toward purple), so shadows look coloured,
  not grey.
- **Coloured outlines, not black.** Outline each piece with its own material's darkest (outline) shade: skin outlined
  in the skin outline colour, a green tunic in the job outline colour, and so on. Only things outside the ramps (eyes,
  lenses, glows) use the general outline `#1E1622`.
- **Pieces overlapping the same material get a dividing line.** The near arm in front of the torso, a pauldron over
  the breastplate and the near leg over the far one all have a line in the material's outline shade where they
  overlap.
- **Far limbs are a step darker.** The far arm, far leg, far boot and far sleeve use the light and shadow shades
  only, so the near side reads in front.
- **Hair is big clumps.** Draw a few large locks with pointed tips (bangs over the forehead, jagged ends at the
  nape) and a couple of shadow-shade strand lines between them, rather than a smooth cap.

## Key colours

Paint with the palette in `../Palettes/PaperDollKeys.gpl` (the same colours are in `PaperDollKeys.png`). Only an exact
RGB match is recoloured. A colour that's one step off stays as you painted it, so keep the palette locked while you
paint. Every ramp has five shades, in this order: highlight, light, mid, shadow, outline.

| Ramp                  | Highlight | Light     | Mid       | Shadow    | Outline   | Becomes |
|-----------------------|-----------|-----------|-----------|-----------|-----------|---------|
| **Skin** (key)        | `#F4E0B4` | `#EFC998` | `#EAB286` | `#A46355` | `#593231` | One of 5 skin tones, picked from the unit's name |
| **Hair** (key)        | `#CBB17D` | `#B08754` | `#96623A` | `#692D21` | `#391514` | One of 6 hair colours, picked from the name |
| **Job primary** (key) | `#BDC39B` | `#99A676` | `#76885A` | `#475F3B` | `#253421` | The job's `PlaceholderColor` as the mid shade |
| **Team accent** (key) | `#8FCEF0` | `#67ACE9` | `#4E8AE2` | `#2A3A9E` | `#1C1B56` | Player blue or Enemy red |
| Metal                 | `#C9D3DB` | `#B2BCC8` | `#9EA6B6` | `#6D6E7F` | `#3C3C45` | Never recoloured |
| Leather               | `#C0A97B` | `#A17C51` | `#825636` | `#5B281F` | `#311313` | Never recoloured |
| Undersuit             | `#A59EB2` | `#7A768B` | `#545264` | `#3C3846` | `#211F26` | Never recoloured |
| Brass                 | `#E6DA82` | `#D9BB59` | `#CC9A3E` | `#8F4A1F` | `#4E2215` | Never recoloured |
| Copper                | `#E2B781` | `#D48E57` | `#C6683C` | `#8B241E` | `#4B141A` | Never recoloured |

The recoloured ramps are built in code from one mid colour (`ColorRamp.From`), with the same warm-light, cool-shadow
drift, so a unit's outline follows its own skin, hair, job and team colours.

These single colours are never recoloured: general outline `#1E1622`, lens `#96E6FF`, lens dark `#3C5A6E`, eye
`#281C2C`, eye white `#FAF8F0`, mouth `#B05C50`, aether glow `#82DCFF`, glow core `#E8FAFF` and muzzle flash
`#FFE878`.

The kit is brass-and-leather steampunk military: field caps with goggles, plate with brass rivets, robes with copper
trim.

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
