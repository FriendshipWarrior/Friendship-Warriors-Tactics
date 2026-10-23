# Friendship Warriors Tactics

An isometric, turn-based tactics RPG in the spirit of **Final Fantasy Tactics**, built in Unity for Steam (Windows, SteamOS / Steam Deck, macOS). Fantasy with steampunk elements and a strict military flavor: soldiers rise from Cadet and Corpsman into specialist careers, fight on height-mapped battlefields, and earn Commendations along the way.

FFT is the reference for mechanics and presentation. Characters, story and job names are original, and every intended departure from FFT is recorded in [`CLAUDE.md`](CLAUDE.md).

> **Status:** early development. The battlefield, units, turn order, combat, the first job abilities and enemy AI are playable in a test scene with placeholder art. No story or campaign yet.

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

**Battlefield**
- FFT-style tiles: heights in half steps (1h, 1.5h, …), slopes, water depth, terrain types, deployment zones for each side, and props like trees and rocks.
- FFT movement: Move and Jump, leaping gaps of Jump ÷ 2 tiles, and leaping over enemies standing at least 2h lower. Enemies block, allies can be passed.
- The whole map is drawn as one mesh with real slopes, which keeps it cheap enough for the Steam Deck.
- An isometric camera that rotates in 90° steps, switches between FFT's low and high tilt, zooms and pans.

**Units and jobs**
- FFT's stat model (hidden raw stats × job multipliers, level-up growth), without Brave and Faith.
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
- Knock-outs with FFT's 3-turn countdown, victory and defeat.
- Enemy AI that weighs damage, knock-outs and attack angle, retreats after attacking, and faces your nearest unit.

**Tools**
- An in-Unity map editor with a live 3D preview and map checks.
- A seeded random map generator for quick play.
- Gamepad, keyboard and mouse supported everywhere.

## Screenshots

| | |
|---|---|
| ![Timed press: the Strike! bar](docs/images/timed-press.png) | ![Units shown as sprites that face the camera](docs/images/units.png) |
| **Timed press:** press as the marker crosses the gold zone. | **Units:** flat sprites with front and back poses, mirrored by camera angle. |
| ![A randomly generated map](docs/images/random-map.png) | ![The map editor](docs/images/map-editor.png) |
| **Random maps:** hills, plateaus, ponds, chasms and ramps from a seed. | **Map editor:** paint on a grid, preview in 3D, fix the reported problems. |

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
| New random map (test scene) | Select | R |

## Getting started

**Requirements**
- Unity **6000.6.7f1** (Unity 6.6) with the Universal Render Pipeline. Unity Hub picks the version from `ProjectSettings/ProjectVersion.txt`.
- [Git LFS](https://git-lfs.com). Images are stored with LFS.

**Run it**
1. Clone the repository (run `git lfs install` once first if you haven't).
2. Open the folder in Unity Hub and let the first import finish.
3. Open `Assets/_Project/Scenes/Battle_Test.unity` and press **Play**.

Your team is the blue markers and the enemy AI controls the red ones. Pick a unit's Move, Act or Wait from the command menu. Press R or Select to try a random map.

## Tools

All of these are under the **Friendship Warriors** menu in the Unity Editor.

| Tool | What it does |
|---|---|
| **Map Editor** | Paint heights, terrain, slopes, water, props and deployment zones. Live 3D preview, map checks, undo, and a random-map generator. Double-clicking a map asset also opens it. |
| **Dev → Rebuild Battle Test Scene** | Rebuilds the test scene and its placeholder assets from code. |
| **Data → Create Starting Jobs / Create Starter Equipment** | Creates the job, ability and equipment assets. Existing assets are left alone. |

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
- **Knock-outs:** at 0 HP a unit has 3 turns to be revived before it is lost.
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
      Battle/          Turn order, turn flow, combat, enemy AI
      Controls/        Input, tile cursor
      Camera/          Isometric camera
      UI/              Battle HUD (uGUI + TextMeshPro, built in code)
    Scripts/Editor/    Map editor, scene and data builders
    Data/              Maps, jobs, abilities, equipment, props, palettes, generator settings
    Input/             Input actions (gamepad + keyboard/mouse)
    Scenes/            Battle_Test
  Tests/EditMode/      Unit tests (NUnit)
docs/images/           README screenshots
CLAUDE.md              Design decisions, conventions and project notes
```

## Development

- **Rules are plain C#, separate from presentation**, so they're deterministic and tested in EditMode tests. Run them from the Unity Test Runner (Window → General → Test Runner).
- **Content is data:** jobs, abilities, equipment, maps and terrain looks are ScriptableObject assets. To use real art, set it on the asset; no code changes needed.
- **Placeholder art throughout:** colored tiles and generated sprites, until the real sprites are drawn.
- **Workflow:** feature branches and pull requests into `main`, with commit messages in Conventional Commits style.
- **Design record:** [`CLAUDE.md`](CLAUDE.md) records every design decision. It notes which FFT rules are confirmed, which are approximations, and where we deliberately differ.

## Roadmap

- [x] Battlefield: tiles, slopes, leaps, props, map editor, random maps
- [x] Units: FFT stats, six jobs, progression, facing
- [x] Turns: CT turn order, Move / Act / Wait, turn trades
- [x] Combat: FFT formulas, five-slot gear, timed press, knock-outs
- [x] Enemy AI
- [x] First job abilities: Cadet *Drill* and Corpsman *Field Kit* items
- [ ] Status effects, reactions, and the abilities that need them
- [ ] Magic (Evoker and Warder spells, charge times)
- [ ] Our replacement for Brave and Faith
- [ ] Character sprites and terrain art
- [ ] Battle prep, party management, campaign and story
- [ ] Save system
- [ ] Steam builds (Windows, SteamOS, macOS)

---

Built by Dustin Hurst with [Claude Code](https://claude.com/claude-code).
