# Friendship Warriors Tactics

An isometric, turn-based tactics RPG in the spirit of **Final Fantasy Tactics**, built in Unity for Steam (Windows, SteamOS / Steam Deck, macOS). Fantasy with steampunk elements and a strict military flavor: soldiers rise from Cadet and Corpsman into specialist careers, fight on height-mapped battlefields, and earn Commendations along the way.

FFT is the reference for mechanics and presentation. Characters, story and job names are original, and every intended departure from FFT is recorded in [`CLAUDE.md`](CLAUDE.md).

> **Status:** early development. Start from the main menu and play **Quick Play**: a random map with two random 5-unit teams, you against the AI. The battlefield, units, turn order, combat, the first job abilities, magic and enemy AI are all in, with placeholder art. No story or campaign yet.

![Battle with the command menu, turn order and unit details](docs/images/battle-hud.png)

---

## Contents

- [Features](#features)
- [Screenshots](#screenshots)
- [Controls](#controls)
- [Getting started](#getting-started)
- [Tools](#tools)
- [How the game works](#how-the-game-works)
- [Project structure](#project-structure)
- [Development](#development)
- [Roadmap](#roadmap)

---

## Features

**Main menu and Quick Play**
- A title screen with Quick Play, plus Campaign and Options (greyed out until they exist). Works with a gamepad, a keyboard or a mouse.
- **Quick Play:** a random map and 5 random units on each side, you against the AI. Every unit has a random job, the same level, random gear its job can use, its job line's abilities learned, and random reaction, support, movement and secondary abilities. Both sides get the same item stock.
- **Formation** (FFT): before the first turn, arrange your units on your side's blue deployment tiles while the enemy stands in view. Pick a unit, pick a tile (picking another unit's tile swaps them), choose its facing, then *Begin Battle* from the Formation menu. Beginning straight away keeps the starting spots.
- One seed builds the whole match. It's shown in the corner, so a match can be reported and played again.
- Pause any time, formation included (Start / P), to resume or quit to the main menu. Winning or losing returns you to the main menu.

**Battlefield**
- FFT-style tiles: heights in half steps (1h, 1.5h, …), slopes, water depth, terrain types, deployment zones for each side, and props like trees and rocks.
- FFT movement: Move and Jump, leaping gaps of Jump ÷ 2 tiles, and leaping over enemies standing at least 2h lower. Enemies block, allies can be passed.
- Units walk their route tile by tile, as in FFT: they follow slopes, hop up and down ledges, arc over gaps, and turn with each step. A ground shadow stays on the surface below them as they arc. Knockback pushes them. Hold A / Enter to hurry a walk.
- The whole map is drawn as one mesh with real slopes, which keeps it cheap enough for the Steam Deck.
- An isometric camera that rotates in 90° steps, switches between FFT's low and high tilt, zooms and pans.

**Units and jobs**
- FFT's stat model (hidden raw stats × job multipliers, level-up growth), with our own **Morale** and **Resonance** in place of FFT's Brave and Faith. Morale drives reactions, melee punch and toughness, and below 10 a unit panics and flees. Resonance scales magic both ways.
- Six jobs in two trees. Advanced jobs unlock at job level 2 in their base job.
- Five equipment slots: two hands, head, body and accessory. Each job is limited to the weapons and armor its FFT counterpart could use.
- Ability slots as in FFT: primary and secondary commands, reaction, support and movement.
- First abilities: the Cadet's **Drill** (FFT Squire: Grit, Shoulder Charge, Hurl Rivet, Light Pack, Diligence) and the Corpsman's **Field Kit** items (FFT Chemist: tonics, aether vials, Panacea, Revival Salts), drawn from a shared team stock. Corpsmen throw items up to 4 tiles.
- Progression: EXP up to level 99 on a growing curve, Proficiency (our version of FFT's job points), and Commendations for mastering a job.

**Turns and combat**
- FFT's speed-based turn order (CT). The command menu has Move, Act and Wait, in either order.
- Turn trades (our addition): *Rapid Fire* gives up the Move to Act twice; *Double Time* gives up the Act to Move twice.
- FFT's damage and hit formulas. Attacking from the side or back strips away evasion.
- **Timed press** (our addition): press as a marker crosses the bar for a chance at a critical hit when attacking, or a block when defending. The closer to perfect, the better the chance, but it never reaches 100%.
- Knock-outs with FFT's 3-turn countdown.
- **Battle setups and objectives:** a standard match is 5 v 5 and won by defeating every enemy. Campaign and special battles can change the team sizes and the objective: defeat one named enemy, or win while keeping a protected character from being lost for good. The objective is shown on screen, with TARGET and PROTECT tags over the units involved, and enemies play to it.
- **Status effects** on FFT's battle clock: Poison, Regen, Protect, Shell, Haste, Slow, Stop, Sleep, Blind, Silence, Disable, Immobilize and Panic.
- **Magic** on FFT's charge-time clock: the Evoker's *Evocation* (fire, ice and lightning in three tiers, Toxin Cloud) and the Warder's *Warding* (three tiers of Mend, Vital Ward, Plate Ward, Aether Ward, Purge, and the revives Resuscitate and Full Resuscitation). Spells target a tile, hit everyone in their area, cost MP, and resolve on the turn order after a charge, during which the caster can't dodge. Knock-outs, Stop, Sleep and Silence cut a spell off. Damage and hit rates scale with caster and target Resonance.
- **Reactions** that trigger at Morale %: Counter Charge, Self-Dose, Guard Stance, Adrenaline and Mending Ward, and the Evoker's Feedback Loop (Counter Magic). Plus cures: First Aid and the Corpsman's Antitoxin, Eyewash, Throat Lozenge and Field Remedy.
- Enemy AI that weighs damage, knock-outs and attack angle, retreats after attacking, faces your nearest unit, and casts spells: area damage away from its own side, heals for hurt allies.

**Unit sprites**
- **Paper-doll sprites:** each unit is put together from part sheets (body, head, face, hair, headgear, body gear, weapon, shield) and recoloured with its job colour, its team colour and a skin tone, hair colour and hairstyle picked from its name. Gear changes show on the sprite.
- Idle, walk, attack, cast (while charging a spell), hurt and knocked-out animations, in front and back poses.
- The parts are plain PNGs on a fixed grid, made to be edited by hand in Aseprite. See the [art guide](Assets/_Project/Art/Sprites/README.md).

**Drops (FFT crystals and chests)**
- A lost unit leaves a **glowing steel rod** (our crystal) or a **treasure chest** on its tile. Any unit, on either side, takes it by ending its move there.
- A steel rod lets the taker **learn the fallen unit's abilities** (free, from jobs it has unlocked) or **restore HP and MP** in full.
- A chest holds one piece of the fallen unit's gear (a monster's holds an item matched to its level). Gear found is listed as **spoils** when the battle ends.
- The enemy AI goes for drops too: rods when it's hurt, chests when nothing better is in reach.

**Battle feedback**
- FFT-style floating numbers that bounce up over the unit: white damage, green healing, blue MP, gold criticals with *Critical!*, and *Miss*, *Guarded* and status names (*Poison*, *Regen lifted*...). Several at once on one unit are staggered and stacked; an area spell shows on everyone together, and reactions (a counter-strike, a Self-Dose) follow a beat after the hit.
- Struck units blink white and jolt; healed units glow; criticals shake the camera.
- Knocked-out units flash as they drop, dim as their 3-turn counter runs down, and show the count over them. A lost unit fades away, leaving its steel rod or treasure chest behind.
- Sound hooks for hits, criticals, misses, blocks, heals, spells, knock-outs, timed presses and menus. Until real audio exists they play generated placeholder blips; drop clips into `Resources/Audio/SoundCues` to replace them.
- Everything runs on the battle clock, so pausing freezes it mid-bounce.

**Tools**
- An in-Unity map editor with a live 3D preview and map checks.
- A seeded random map generator for quick play.
- Gamepad, keyboard and mouse supported everywhere.

## Screenshots

| | |
|---|---|
| ![The main menu](docs/images/main-menu.png) | ![A quick play match](docs/images/quick-play.png) |
| **Main menu:** Quick Play now; Campaign and Options to come. | **Quick play:** random map, random teams, and the seed in the corner. |
| ![Timed press: the Strike! bar](docs/images/timed-press.png) | ![Paper-doll units in battle: one casting, one knocked out](docs/images/paper-doll-battle.png) |
| **Timed press:** press as the marker crosses the gold zone. | **Units:** paper-doll sprites that face the camera. Here an Evoker charges a spell next to a knocked-out Corpsman. |
| ![A randomly generated map](docs/images/random-map.png) | ![The map editor](docs/images/map-editor.png) |
| **Random maps:** hills, plateaus, ponds, chasms and ramps from a seed. | **Map editor:** paint on a grid, preview in 3D, fix the reported problems. |
| ![A Cadet leaping a chasm mid-walk](docs/images/walk-leap.png) | |
| **Walking:** units walk their route tile by tile. Here a Cadet leaps a chasm after walking up a ramp and hopping onto a ledge. | |
| ![The formation menu before a battle, enemies already in place](docs/images/formation-menu.png) | ![Arranging units on the blue deployment tiles](docs/images/formation.png) |
| **Formation:** the objective, the enemy's starting spots, and the Formation menu. | **Arranging units:** blue tiles are where your side may start. |
| ![An area spell: four damage numbers and white hit flashes](docs/images/battle-feedback-area.png) | ![A hit followed by a Self-Dose heal](docs/images/battle-feedback-reaction.png) |
| **Battle feedback:** an area spell hits four enemies at once. | **Reactions follow the hit:** 14 damage, then the green Self-Dose heal a beat later. |
| ![A Cadet on a fallen Corpsman's steel rod, choosing to learn or restore](docs/images/drops-steel-rod.png) | ![A Vanguard opening a treasure chest](docs/images/drops-chest.png) |
| **Steel rod:** learn the fallen unit's abilities, or restore HP and MP. | **Treasure chest:** a piece of the fallen unit's gear, kept as spoils. |

![Every job in its starting gear on both teams: idle, walk, attack, cast, hurt and KO frames, front and back](docs/images/paper-doll.png)

**Paper dolls:** every job in typical gear, Player team on top and Enemy team below. From left to right: idle, walk, attack, cast, hurt and KO frames in the front pose, then idle, attack, cast and KO frames in the back pose.

## Controls

| Action | Gamepad | Keyboard / mouse |
|---|---|---|
| Move cursor / menus | D-pad or left stick | WASD or arrows, mouse hover |
| Confirm | A | Enter, Space, left click |
| Cancel / back | B | Esc, Backspace, right click |
| Rotate camera | LB / RB | Q / E |
| Tilt camera | Y | T |
| Zoom | LT / RT | Scroll wheel, − / = |
| Pan | Right stick | Middle-drag |
| Timed press | A | Enter / Space |
| Hurry a walk (hold) | A | Enter / Space |
| Formation menu (Begin Battle) | B | Esc, Backspace, right click |
| Pause | Start | P |
| New random map (test scene) | Select | R |

## Getting started

**Requirements**
- Unity **6000.6.7f1** (Unity 6.6) with the Universal Render Pipeline. Unity Hub picks the version from `ProjectSettings/ProjectVersion.txt`.
- [Git LFS](https://git-lfs.com). Images are stored with LFS.

**Run it**
1. Clone the repository (run `git lfs install` once first if you haven't).
2. Open the folder in Unity Hub and let the first import finish.
3. Open `Assets/_Project/Scenes/MainMenu.unity`, press **Play**, and choose **Quick Play**.

Your team is the blue markers and the enemy AI controls the red ones. Pick a unit's Move, Act or Wait from the command menu. Press Start or P to pause. When the battle ends you're back at the main menu.

`Battle_Test.unity` is the developer sandbox: fixed test teams, and R or Select rolls a random map. To try another objective, select **Battle Sandbox** in the scene and swap its Battle Setup.

## Tools

All of these are under the **Friendship Warriors** menu in the Unity Editor.

| Tool | What it does |
|---|---|
| **Map Editor** | Paint heights, terrain, slopes, water, props and deployment zones. Live 3D preview, map checks, undo, and a random-map generator. Double-clicking a map asset also opens it. |
| **Dev → Rebuild Game Scenes** | Rebuilds the MainMenu and QuickPlay scenes from code and sets Build Settings (MainMenu, QuickPlay, Battle_Test). |
| **Dev → Rebuild Battle Test Scene** | Rebuilds the test scene and its placeholder assets from code. |
| **Data → Create Battle Setups** | Creates the prebuilt battle setups: Standard (5 v 5), Defeat the Commander and Escort. |
| **Data → Create Starting Jobs / Create Starter Equipment** | Creates the job, ability and equipment assets. Existing assets are left alone. |
| **Art → Generate Paper-Doll Parts** | Draws any missing paper-doll part PNGs and the key palette, and updates the part catalog. Existing PNGs are left alone. **Regenerate (Overwrite)…** replaces them after asking. |
| **Art → Save Paper-Doll Preview…** | Saves a contact sheet of every job on both teams, to check the parts read clearly. |

## How the game works

<details>
<summary><b>Jobs</b></summary>

| Job | FFT counterpart | Focus | Weapons | Unlock |
|---|---|---|---|---|
| Cadet | Squire | Base soldier | Sword, knife | — |
| Corpsman | Chemist | Field medic, path to magic | Knife | — |
| Vanguard | Knight | Melee front line | Sword + shield | Cadet job level 2 |
| Marksman | Archer | Ranged | Rifle (two-handed) | Cadet job level 2 |
| Evoker | Black Mage | Offensive magic | Rod | Corpsman job level 2 |
| Warder | White Mage | Healing and protection | Staff | Corpsman job level 2 |

Stat multipliers and growth rates are FFT's for the matching job.
</details>

<details>
<summary><b>Turns</b></summary>

Every clock tick, each unit's CT (charge time) rises by its Speed. At 100 the unit takes a turn. A turn costs 60 CT, plus 20 if the unit moved and 20 if it acted. Move and Act can come in either order, and Wait ends the turn with a choice of facing.
</details>

<details>
<summary><b>Combat</b></summary>

- **Damage:** each weapon type has FFT's formula. Swords do P.Atk × WP; knives and rifles do ⌊(P.Atk + Speed) / 2⌋ × WP.
- **Hit chance:** class evasion only works from the front, shield evasion from the front or side, and accessory evasion from any direction.
- **Criticals:** a 5% base chance, raised by a well-timed press.
- **Blocks:** only from a timed press while defending.
- **Magic:** M.Atk × spell power × caster Resonance/100 × target Resonance/100 (Shell cuts damage to ⅔). Status spells hit (M.Atk + spell power) × the same Resonances %. A spell's CT rises by its speed each tick and it resolves at 100, before units' turns.
- **Knock-outs:** at 0 HP a unit has 3 turns to be revived before it is lost.
</details>

<details>
<summary><b>Objectives</b></summary>

- **Defeat all enemies:** the standard objective.
- **Defeat a target:** win as soon as that enemy is knocked out.
- **Protect:** lose if a protected character is lost for good (their 3-turn countdown runs out). Being knocked out and revived in time is fine.
- You always lose if your whole team is down. If you meet the objective in the same action that loses a protected character or your last unit, you still win.
</details>

<details>
<summary><b>Progression</b></summary>

- **EXP per action:** a percentage of what your next level costs. Same-level targets give 10%; stronger targets more, weaker targets less.
- **Heals and other support actions:** always give 10%.
- **Proficiency:** follows FFT's job point formula, and allies get a quarter. Prof raises job levels and is spent to learn abilities.
- **Commendations:** mastering a job earns one.
</details>

## Project structure

```
Assets/
  _Project/
    Scripts/Runtime/   Game code (FriendshipWarriors.* namespaces)
      Grid/            Tiles, maps, pathfinding, map checks, random generation, mesh view
      Units/           Units, jobs, stats, abilities, equipment, sprite views
      Battle/          Formation phase, turn order, turn flow, combat, magic, enemy AI, drops, battle setups and objectives
      Feedback/        Floating numbers, hit flashes, knock-out countdown, rod and chest markers (presentation only)
      Audio/           Sound cues, placeholder blips, the sound player
      Menus/           Main menu
      QuickPlay/       Random teams and quick-play matches
      Controls/        Input, tile cursor
      Camera/          Isometric camera
      UI/              Battle HUD and menus (uGUI + TextMeshPro, built in code)
    Scripts/Editor/    Map editor, scene and data builders, paper-doll part generator
    Data/              Maps, jobs, abilities, equipment, battle setups, props, palettes, generator settings
    Art/               Paper-doll part PNGs, key palette, art guide (Sprites/README.md), shaders
    Resources/         Sprite part catalog, sound cue set, hit-flash material (loaded at runtime)
    Input/             Input actions (gamepad + keyboard/mouse)
    Scenes/            MainMenu, QuickPlay, Battle_Test (dev sandbox)
  Tests/EditMode/      Unit tests (NUnit)
docs/images/           README screenshots
CLAUDE.md              Design decisions, conventions and project notes
```

## Development

- **Rules are plain C#, separate from presentation**, so they're deterministic and tested in EditMode tests. Run them from the Unity Test Runner (Window → General → Test Runner).
- **Content is data:** jobs, abilities, equipment, maps and terrain looks are ScriptableObject assets. To use real art, set it on the asset; no code changes needed.
- **Placeholder art throughout:** colored tiles, and paper-doll unit sprites drawn by code as a starting point for hand editing.
- **Workflow:** feature branches and pull requests into `main`, with commit messages in Conventional Commits style.
- **Design record:** [`CLAUDE.md`](CLAUDE.md) records every design decision. It notes which FFT rules are confirmed, which are approximations, and where we deliberately differ.

## Roadmap

- [x] Battlefield: tiles, slopes, leaps, props, map editor, random maps
- [x] Units: FFT stats, six jobs, progression, facing
- [x] Turns: CT turn order, Move / Act / Wait, turn trades
- [x] Combat: FFT formulas, five-slot gear, timed press, knock-outs
- [x] Enemy AI
- [x] First job abilities: Cadet *Drill* and Corpsman *Field Kit* items
- [x] Status effects, reactions, cures
- [x] Morale and Resonance (our Brave and Faith)
- [x] Magic (Evoker and Warder spells, charge times)
- [x] Battle setups and objectives (5 v 5 standard, defeat a target, protect a character)
- [x] Main menu, Quick Play (random 5 v 5 from a seed), pause menu
- [x] Paper-doll unit sprites (first drafts drawn by code, ready for hand editing)
- [x] Animated movement: units walk their path tile by tile, as in FFT, with walk frames, hops and leaps
- [x] Formation phase before battle (FFT): place units, swap, choose facings
- [x] Battle feedback: floating numbers, hit flashes, knock-out countdown, sound hooks (placeholder blips)
- [x] Drops (FFT): lost units leave a steel rod (learn abilities or restore) or a treasure chest (gear, kept as spoils)
- [ ] Hand-drawn character art and terrain art, real sound effects and music
- [ ] Battle prep, party management, campaign and story. Losing a campaign battle won't be game over: the campaign carries on, unlike FFT.
- [ ] Quick Play options: online matches, army building
- [ ] Save system
- [ ] Steam builds (Windows, SteamOS, macOS)

**Game modes, in order:** quick play, then the story campaign, then online co-op and versus. Battle rules are kept deterministic so online matches can be re-simulated from each player's commands.

---

Built by Dustin Hurst with [Claude Code](https://claude.com/claude-code).
