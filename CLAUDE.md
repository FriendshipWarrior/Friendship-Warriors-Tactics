# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Friendship Warriors Tactics** — an isometric, turn-based tactics RPG in the spirit of *Final Fantasy Tactics*: height-mapped grid battlefields, a speed/charge-time turn order, a job/class system, facing-based combat, and a story campaign between battles.

**FFT is the source of truth** for mechanics and presentation. When a design choice departs from how FFT works, point it out and explain the tradeoff instead of quietly diverging. Content (characters, story, job names) is original.

**Team & budget:** just the user and Claude, no budget. Prefer approaches one person can maintain; all art is placeholder until the user draws sprites, so art must be swappable through data without code changes.

**Roles:** Claude is the primary developer for all code and game logic (C# scripts, systems architecture, editor tooling, tests). The user owns creative direction, art/audio, design decisions, and anything done by hand in the Unity Editor. When a design question has no clear answer in this file, ask rather than invent — then record the decision under *Design Decisions* below.

## Tech Stack

- **Engine:** Unity `6000.6.7f1` (Unity 6) — see `ProjectSettings/ProjectVersion.txt`
- **Render pipeline:** URP 17.6 (`Assets/Settings/` holds PC and Mobile RP assets)
- **Input:** Input System 1.20 (`Assets/InputSystem_Actions.inputactions`) — do not use the legacy `Input` class
- **UI:** uGUI 2.6 / UI Toolkit available
- **Other packages:** Unity AI Assistant (pre-release), AI Inference, `com.unity.pipeline` (experimental)
- **Language:** C# (Unity's supported version; no `Nullable` reference types unless enabled project-wide)
- **IDE integrations:** Rider and Visual Studio packages installed

## Repository & Git

- **Remote:** `https://github.com/FriendshipWarrior/Friendship-Warriors-Tactics` (the remote is currently empty — nothing has been pushed yet)
- **Workflow:** git and `gh` CLI only, driven from Claude Code (no GitKraken).
- **Default branch:** `main`. Do feature work on branches (`feat/…`, `fix/…`, `chore/…`) and merge via PR.
- **README:** `README.md` is the GitHub front page (features, controls, getting started, tools, roadmap). Update it in the same PR whenever a feature lands, a control changes or a roadmap item moves. Screenshots live in `docs/images/` (PNG, via Git LFS).
- **Commits:** Conventional Commits style (`feat:`, `fix:`, `chore:`, `refactor:`, `test:`, `docs:`). Only commit or push when the user asks.
- **Never commit** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Obj/`, `Build(s)/` — already covered by `.gitignore`.
- **Always commit `.meta` files** alongside their assets, and move/rename assets in a way that keeps the `.meta` (GUID) with them — a lost `.meta` breaks every reference to that asset.
- Unity serializes scenes/prefabs as YAML (`.gitattributes` configures this). Avoid hand-editing `.unity`/`.prefab`/`.asset` files unless the change is trivial and well understood; prefer having the Editor (or Unity MCP) make those changes.

### Account isolation (important)

This repo belongs to GitHub account **`FriendshipWarrior`**. Everything else on this machine uses the work account **`dustinh-angel`**. Keep them separate — never change global config to make this repo work.

- **Commit identity:** set repo-locally in `.git/config` — `Dustin Hurst <1971018+FriendshipWarrior@users.noreply.github.com>`. Never use `git config --global` here.
- **Push/pull:** SSH via host alias `github-fwt` (in `~/.ssh/config`, key `~/.ssh/id_ed25519_fwt`, `IdentitiesOnly yes`). Remote is `git@github-fwt:FriendshipWarrior/Friendship-Warriors-Tactics.git`. Other repos keep using HTTPS + Keychain.
- **`gh` CLI:** the active `gh` account must stay `dustinh-angel`. For this repo, pass the token per command instead of switching accounts:
  `GH_TOKEN="$(gh auth token --user FriendshipWarrior)" gh pr create ...`
  Never run `gh auth switch` to `FriendshipWarrior`.
- Before committing, sanity-check with `git config user.email` (expect the noreply address above).

## Tooling / MCP

- **GitHub:** via `gh` CLI using the per-command `GH_TOKEN` pattern (see *Account isolation*). A GitHub MCP server is optional.
- **Unity CLI:** installed at `~/.unity/bin/unity` (may not be on PATH in agent shells — use the full path). The project's `com.unity.pipeline` package lets it drive the open Editor live.
- **Unity MCP:** registered as `unity-editor` (Claude Code *local* scope, this project only) → `~/.unity/bin/unity mcp --project-path <this repo>`. The `unity@unity-agent-plugin` Claude Code plugin supplies the Unity skills (`unity:unity-cli` etc.).
- **Driving the Editor:** when the Editor is open, make scene/prefab/component/asset changes through the MCP tools or `unity command …` (e.g. `unity command script eval --caller plugin --skill <skill> '<C#>'`), never by hand-editing `.unity`/`.prefab`/`.asset` YAML. Check with `unity status --project-path <repo>` first; if the Editor is in Safe Mode (compile errors), fix the C# errors first.
- If the Editor isn't open, ask the user to open it rather than starting a separate headless Editor on the same project.

## Project Layout

Game code lives under one root with its own assembly definitions (folders marked *planned* don't exist yet):

```
Assets/
  _Project/
    Scripts/
      Runtime/            # FriendshipWarriors.Runtime.asmdef
        Grid/             # GridCoord, Tile, TileShape, BattleGrid, BattleMapData, GridMetrics
          Pathfinding/    # Pathfinder, MovementRange, MovementProfile, Occupancy
          Props/          # PropDefinition, PropPlacement (decoration layer)
          Validation/     # MapValidator, MapIssue (shared by the map editor and generator)
          Generation/     # MapGenerator, MapGeneratorSettings (seeded random maps)
          View/           # GridView, BattlefieldMeshBuilder, TerrainPalette (presentation)
        Units/            # Unit, JobDefinition, stats/EXP/Prof/Commendations, facing rules, UnitFactory
          Abilities/      # AbilityDefinition, AbilityKind (action/reaction/support/movement), TurnTrade
          Equipment/      # EquipmentDefinition, slots (main/off hand, head, body, accessory), weapon & armor types
          View/           # UnitView (billboard sprite), SpriteViewMath, PlaceholderSprites
        Battle/           # TurnOrder (CT), TurnState (Move/Act/Wait), BattleController (turn flow; .Ai.cs partial runs AI turns)
          AI/             # AiPlanner, AiPlan (pure, deterministic per seed)
          Combat/         # CombatFormulas, AttackResolver, AttackRange, TimedPressRules, BattleRewards
        UI/               # BattleHud (uGUI + TextMeshPro, built in code), MenuModel
        Controls/         # BattleInput, TileCursor, CursorMath (gamepad + mouse/keyboard)
        Camera/           # IsoCameraRig (namespace FriendshipWarriors.Cameras)
        Dev/              # BattleSandbox and other test-scene drivers
        Core/             # planned
      Editor/             # FriendshipWarriors.Editor.asmdef — MapEditorWindow, MapPreview, TestMapFactory, TestSceneBuilder, StartingJobsBuilder
    Data/                 # ScriptableObject assets: maps, props, terrain palette, generator settings (later jobs, abilities, items)
    Input/                # BattleControls.inputactions (control schemes: Keyboard&Mouse, Gamepad)
    Materials/            # Terrain.mat (URP Lit, cloned per terrain look), Highlight.mat (transparent unlit)
    Scenes/               # Battle_Test.unity (the only scene in Build Settings)
    Prefabs/ Art/ Audio/  # planned
  Tests/
    EditMode/             # FriendshipWarriors.Tests.EditMode.asmdef — pure logic tests
    PlayMode/             # planned
```

Root namespace: `FriendshipWarriors`; namespaces follow folders (e.g. `FriendshipWarriors.Grid.Pathfinding`). Avoid a namespace named `Camera` — it shadows `UnityEngine.Camera`.

## Architecture Principles

- **Separate simulation from presentation.** Battle rules (grid, pathfinding, turn order, damage, hit chance, AI) live in plain C# classes with no `MonoBehaviour` dependency, so they are deterministic and unit-testable in EditMode tests. MonoBehaviours are thin views/controllers that render state and forward input.
- **Data-driven content.** Jobs, abilities, items, status effects, and maps are `ScriptableObject` assets, not hard-coded. Formulas reference data; designers add content without code changes.
- **Grid model:** logical tile grid with per-tile height and terrain type; world position is derived from grid coords. Movement/jump/range checks use grid coords + height, never physics raycasts.
- **Turn order:** charge-time (CT) model — each unit accumulates Speed per tick and acts at a threshold; charged abilities resolve on their own CT. Keep it deterministic and seedable (inject an RNG, never call `UnityEngine.Random` from battle logic).
- **Battle flow** is an explicit state machine (e.g., SelectUnit → Move → Act → Facing → EndTurn), with commands as objects so they can be previewed, validated, and (later) replayed or undone.
- **Isometric camera:** a 3D scene with an orthographic camera at a fixed iso angle, supporting 90° rotation steps and zoom. Tile picking must account for height and camera rotation.
- **Events over references:** systems communicate through C# events / a lightweight event bus rather than `FindObjectOfType` or singletons reaching across systems.

## Coding Conventions

- PascalCase for types, methods, properties; `_camelCase` for private fields; `camelCase` for locals/parameters.
- `[SerializeField] private` over public fields for Inspector exposure.
- No `GameObject.Find`, `FindObjectOfType`, or string-based `SendMessage` in gameplay code.
- Avoid per-frame allocations in `Update` (LINQ, closures, boxing) on hot paths.
- One public type per file; filename matches the type.
- Comments explain *why* (rules, formula sources, non-obvious Unity behavior), not *what*.

## Testing & Verification

- New battle/grid/stat logic gets **EditMode tests** (Unity Test Framework / NUnit). `TestGrids.FromRows(...)` builds small maps from strings for pathfinding tests.
- Run tests in the open Editor: `unity command test run --caller plugin --skill unity-cli -- --mode EditMode --filter FriendshipWarriors` (headless `unity test` refuses a project that's open in an Editor).
- For visual changes, play the `Battle_Test` scene and capture the Game view (see *Editor gotchas*).
- **Map editor:** *Friendship Warriors → Map Editor*, or double-click a Battle Map asset. Paints height (in halves), terrain, slopes, water depth, props and deployment zones on a top-down grid; live 3D preview; validation via `MapValidator` (also used by the random generator). Left-click paints, right-click copies the tile's value (erases for props/deployment), each stroke is one undo step. When testing it from scripts, work on a copy of a map: the window is on the user's screen and real clicks edit the open map.
- **Random maps:** `MapGenerator.Generate(settings, seed, map)` fills a `BattleMapData` (works at runtime for quick play). Deterministic per seed, validated with `MapValidator`, retried on errors. Tune via `MapGeneratorSettings` assets (`Data/Generation/MapGenerator_Default.asset`). In the map editor use the Generate row; in `Battle_Test` press R / Select (the seed is logged so maps can be reproduced).
- Rebuild the test scene and its placeholder assets from code with **Friendship Warriors → Dev → Rebuild Battle Test Scene** (`TestSceneBuilder.Rebuild()`); don't hand-wire it.
- When scripting a virtual gamepad, each `script eval` takes long enough that a held D-pad press triggers key-repeat (cursor and menus repeat after 0.3–0.35 s). Place the cursor with `TileCursor.SetCoord` and pick menu items via `MenuModel.Select`, and use the pad only for buttons.
- Gamepad paths can be tested without hardware: in Play mode, `InputSystem.AddDevice<Gamepad>()` and `InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(...))` through `script eval`, then remove the device.
- After code changes, confirm the project compiles with no console errors before reporting work as done. If verification wasn't possible, say so.

### Editor gotchas (learned the hard way)

- New files on disk aren't compiled until an asset refresh: `script eval 'UnityEditor.AssetDatabase.Refresh();'`, then poll `script recompile_status`. Commands fail briefly during the domain reload.
- `EditorSceneManager.NewScene` unloads unreferenced assets, so load assets you intend to assign **after** creating the scene, or the reference saves as null.
- `capture game_view --save_path` writes under `Assets/`. Don't use it; take the inline image (`--max_resolution`) and decode it into the scratchpad.
- In Play mode `Destroy` is deferred to the end of the frame: clear references to destroyed objects right away, or a same-frame rebuild reuses them.
- Pipeline-specific flags go after `--`; the CLI's own flags (`--caller`, `--skill`, `--format`) go before it.
- Play-mode checks: `editor set_auto_tick` + `editor.playmode set_run_in_background` first, then `job wait` on `Time.frameCount` to prove frames advance. Afterwards revert run-in-background and confirm `PlayerSettings.runInBackground` didn't get saved as changed.

## Design Decisions

Record decisions here as they are made (date — decision — rationale).

- 2026-10-22 — **Heights:** tiles store height in half-h steps (FFT shows heights like 2.5h); 1h = 0.5 world units. `Jump` is in h, as in FFT. Tiles follow FFT's tile data: height, slope type + slope height, water depth, surface type.
- 2026-10-22 — **Movement (FFT rules, confirmed with the user):**
  - Orthogonal steps cost 1 Move. Jump limits the height difference both climbing up and stepping down.
  - Heights come in halves (1, 1.5, 2...). A sloped tile's height is the middle of its slope (a ramp from 1h to 2h is 1.5h); midpoints that would fall between halves round down.
  - A unit can leap a gap of Jump / 2 tiles, paying Move as if flat (Jump caps at 7, so at most a 3-tile gap). A gap is void or any tile lower than the take-off tile. An enemy is an obstacle: leaping over one needs the take-off tile to be **at least 2h higher** than the enemy's tile (e.g. at 3h next to an enemy at 1h, Jump 3 hops over). The 2h is tunable via `MovementRules.EnemyLeapClearance`. Same-height or higher tiles, and blocked tiles, can't be leapt.
  - Enemies block walking; allies can be passed but not stopped on.
  - Water depth doesn't affect movement. A unit in water of **depth 2 or more can't act** (`Tile.AllowsActions`; enforce it when the battle system lands). Swim-type abilities that lift this come later.
- 2026-10-22 — **Battlefield rendering:** one combined mesh with real slopes (cheap on Steam Deck), terrain looks from `TerrainPalette` (colors now, textures later), translucent highlight overlays. Props are a decoration layer; blocking props make their tile impassable.
- 2026-10-22 — **Camera (FFT):** orthographic, always diagonal, 90° rotation steps plus a low/high tilt toggle (30°/45°, approximated). Walls hiding units are handled the FFT way, by rotating or tilting the camera; no wall fading.
- 2026-10-22 — **Controls:** one tile cursor for every device. D-pad/left stick/WASD/arrows move it along grid axes (input turned 45°, FFT-style) with key repeat, and the camera follows. Mouse hover places it. Confirm = A/Enter/Space/click, Cancel = B/Esc/Backspace/right-click, rotate = LB/RB or Q/E, tilt = Y or T, zoom = triggers, scroll or -/=, pan = right stick or middle-drag.
- 2026-10-22 — **Scope:** an FFT-style game (FFT look and mechanics) with original characters, story and job names. Modes planned: story campaign, quick play, and possibly random map generation.
- 2026-10-22 — **Art style:** 2D sprite characters on 3D tiles. Each unit is a 3D placeholder object with a sprite on a camera-facing child (rotates on the vertical axis only, so it stays upright). Units face the **4 grid directions** (FFT). Because the camera always sits on a 45° diagonal, only 2 poses need drawing (front 3/4 and back 3/4), mirrored to cover all 4 views.
- 2026-10-22 — **Platforms:** Steam on PC. Windows first, then SteamOS/Steam Deck (Linux), then macOS if it isn't too much work. **Controller support is required**, since the game must work on Steam Deck. Every interaction needs a gamepad path, and the UI must be readable at 1280x800.
- 2026-10-22 — **Map authoring: hybrid.** Grid data (`BattleMapData`) is the single source of truth for campaign, quick play and the random generator. A custom in-Unity editor paints it, a decoration/props layer sits on top, and custom backdrops are allowed later for set pieces. Fully modeled maps are out of scope (no 3D artist).
- 2026-10-22 — **Theme:** fantasy with steampunk elements; strict military roles and names.
- 2026-10-22 — **Stats (FFT, minus Brave/Faith):** HP, MP, Speed, Physical Attack, Magic Attack, Move, Jump, class evasion. FFT's model: hidden raw stats × job multiplier / 1,638,400; level-up adds raw / (growth constant + level) using the current job's constants. Brave and Faith are left out until we design our own; spells use a flat multiplier where FFT used Faith. Idea to revisit: the timed block/crit press could replace FFT's Brave-based reaction chance.
- 2026-10-22 — **Jobs (6):** Cadet (FFT Squire) → Vanguard (Knight, melee) and Marksman (Archer, ranged); Corpsman (Chemist) → Evoker (Black Mage) and Warder (White Mage). Advanced jobs unlock at job level 2 in the base job. Multipliers and growth constants are FFT's (verified); Move/Jump/class evasion are recalled FFT values. Name pool kept for later specialist jobs: Ironclad, Cuirassier, Sentinel, Aethermancer, Grenadier, Artificer, Arcane Artillerist.
- 2026-10-22 — **Progression (intended departures from FFT):** EXP per level grows exponentially (`ExperienceCurve`: 100 × 1.06^(level−1); about 500k EXP to reach the level-99 cap) instead of FFT's flat 100. FFT's JP is called **Proficiency (Prof)** and uses FFT's job level thresholds (200 … 3000). Reaching max job level masters a job (until abilities exist) and awards a **Commendation**; Commendations can also come from field challenges (not defined yet).
- 2026-10-22 — **Facing (FFT):** units face the 4 grid directions and choose a facing at the end of their turn. Facing changes evasion, not damage: front = class + shield + accessory evasion, side = shield + accessory, back = accessory only. Exact diagonals count as side (our approximation).
- 2026-10-22 — **Timed defense/offense (intended departure):** in combat, a well-timed button press gives a *chance* to block (when attacked) or land a critical hit (when attacking). Design details come with the combat system.
- 2026-10-22 — **Turn order (FFT CT):** every clock tick each unit's CT rises by its Speed; at 100 it takes a turn (highest CT first, ties to roster order). A turn costs 60 CT, +20 if it moved, +20 if it acted; CT after a turn is capped at 60 (recalled, unverified). Everyone starts at CT 0. Charged abilities (spells with their own CT) come with combat.
- 2026-10-22 — **Turns (FFT):** command menu Move / Act / Wait; Move and Act once each, in either order (Move after Act allowed); Wait or using both ends the turn with a facing choice. No undoing a move once confirmed. Until enemy AI exists, the player controls every team.
- 2026-10-22 — **Turn trades (intended departure):** abilities can let a unit give up its Move to Act twice (*Rapid Fire*, Marksman support ability) or give up its Act to Move twice (*Double Time*, Vanguard movement ability). A traded turn still costs the full 100 CT. Names, jobs and Prof costs (400 / 300) are placeholders to revisit.
- 2026-10-22 — **Ability slots (FFT):** primary action set (current job's command), secondary action set (another job with a learned action ability), reaction (the user's "defence" slot), support (passive) and movement. Abilities are learned by spending Prof earned in the job that teaches them; job level counts all Prof earned, spent or not. Job command names: Cadet *Drill*, Corpsman *Field Kit*, Vanguard *Assault*, Marksman *Marksmanship*, Evoker *Evocation*, Warder *Warding*.
- 2026-10-22 — **Equipment (FFT):** five slots — main hand (weapon), off hand (shield; a second weapon once a dual-wield ability exists), head, body, accessory. Two-handed weapons (rifles) leave the off hand empty. Jobs limit weapon types, shield use and armor types like their FFT counterparts (e.g. Vanguard: swords, shields, helmets, armor, robes). Head/body gear add flat max HP/MP; shields give S-Ev, accessories A-Ev. Starter items mirror FFT starting gear (bonus values recalled).
- 2026-10-22 — **Combat (FFT):** weapon damage = Sword/Rod P.Atk×WP, Knife and Rifle (FFT bow) ⌊(P.Atk+Speed)/2⌋×WP, Staff M.Atk×WP, bare hands ⌊P.Atk×Brave/100⌋×P.Atk with Brave standing in at 70. Hit% = 100 × (100−C-Ev)% front only × (100−S-Ev)% front/side × (100−A-Ev)% always. Critical: 5% base, raises XA by 0…XA−1 (recalled; knockback not modelled). Melee reaches the 4 neighbours within 3h; rifles 1–5 tiles (FFT bows' height bonus and line of fire not modelled). Rewards per successful action: Prof 8 + 2×job level + level/4 (verified), allies get ¼; EXP keeps FFT's 10 + (target level − actor level) but as a percentage of what the actor's next level costs (decided 2026-10-22): a same-level target is worth a tenth of a level at any level, stronger targets more, weaker less; at least 1 EXP, at most one level, none at 99. Non-attack actions (heals, buffs, items) give the same-level share — 10% of the actor's own next level, whoever the target — so support roles level at the same pace (FFT uses the target's level; ours is the actor's). KO at 0 HP with a 3-turn death counter, then the unit is lost (recalled). Victory/defeat when one team has nobody standing.
- 2026-10-22 — **Timed press (intended departure):** a marker sweeps a bar (1.2 s, perfect at 0.8 s, ±0.3 s window). Accuracy 0–1 is how close the press is to perfect; the chance it grants scales with accuracy², capped below 100%: block up to 60% when an enemy attacks a Player-team unit (blocked = no damage), critical up to 5% + 45% when a Player-team unit attacks. Order: evasion, then block, then critical. Tunable in `TimedPressRules` on the BattleController.
- 2026-10-23 — **Enemy AI:** each team is Human- or AI-controlled (`BattleController` Team Control; default Player = Human, Enemy = AI). `AiPlanner` (pure C#) plans the whole turn: every reachable tile (including staying) × every enemy in Attack range there, scored by expected damage (hit% × damage, capped at the target's HP) + a knock-out bonus (hit% × target max HP) + side (+2) / back (+4) bonuses − 1.5 per enemy that could reach the tile next turn. Attacks from its own tile are followed by a retreat to a less threatened tile (attack-then-move, as FFT's AI often does); otherwise it moves then attacks. With no attack available it walks toward the nearest enemy by walking distance. It ends facing the nearest enemy. Ties are broken with the battle's seeded RNG, so plans are deterministic. FFT's actual AI logic isn't public — these weights are ours, tune freely. Not yet: AI turn trades (Rapid Fire / Double Time), abilities, healing allies, retreating when hurt. AI turns play back as paced steps (~0.55 s apart) with the camera following; only human-controlled sides get timed presses (Brace! when the AI attacks a human unit).
- 2026-10-22 — **Battle UI:** uGUI + TextMeshPro, built in code by `BattleHud` on a Screen Space Overlay canvas scaled from 1920x1080 (readable on Steam Deck's 1280x800). Menus are driven by `MenuModel` and the battle's own input (D-pad/stick/keys, A/B); the mouse can hover and click. Placeholder styling.
- 2026-10-22 — **Unit visuals:** a placeholder object with a camera-facing sprite (rotates on the vertical axis only). Two drawn poses per job (front ¾ and back ¾, facing screen-right) mirrored to cover all four views. Set `FrontSprite`/`BackSprite` on a job to replace the generated placeholder.
- Open questions: save system scope; AI difficulty levels; whether to add FFT's multi-layer tiles (bridges, walking under things).
