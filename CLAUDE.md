# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Friendship Warriors Tactics** — an isometric, turn-based tactics RPG in the spirit of *Final Fantasy Tactics*: height-mapped grid battlefields, a speed/charge-time turn order, a job/class system, facing-based combat, and a story campaign between battles.

**FFT is the source of truth** for mechanics and presentation. When a design choice departs from how FFT works, point it out and explain the tradeoff instead of quietly diverging. Content (characters, story, job names) is original.

**Long-term roadmap, in order:** quick play → story campaign → online co-op and versus. Online play means battle logic must stay deterministic and replayable from its inputs (seeded RNG, no Unity-time or frame-order dependence in rules) so a match can be re-simulated on each side from the players' commands.

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
          Abilities/      # AbilityDefinition, AbilityKind (action/reaction/support/movement), ReactionEffect, TurnTrade
          Status/         # StatusEffect, StatusRules (FFT durations and effects), StatusSet
          Equipment/      # EquipmentDefinition, EquipmentCatalog, slots (main/off hand, head, body, accessory), weapon & armor types
          View/           # UnitView (billboard sprite), SpriteViewMath, PlaceholderSprites
        Battle/           # TurnOrder (CT), TurnState (Move/Act/Wait), BattleController (turn flow; .Ai.cs partial runs AI turns)
          AI/             # AiPlanner, AiPlan, AiHints (objective-aware weights; pure, deterministic per seed)
          Abilities/      # AbilityCommand (job command menus + execution), AbilityRules, AbilityResolver, Knockback, Inventory
          Combat/         # CombatFormulas, AttackResolver, AttackRange, TimedPressRules, BattleRewards
          Magic/          # MagicFormulas, SpellArea, ChargingAction, SpellResolver (spells, Counter Magic)
          Reactions/      # ReactionResolver (Morale-chance reactions after damage)
          Objectives/     # BattleSetup (team sizes, rosters, objective), RosterEntry, BattleObjective, ObjectiveEvaluator, BattleSetupValidator
        UI/               # BattleHud (uGUI + TextMeshPro, built in code), MenuModel, MenuScreen (front-end menus), UiFactory
        Controls/         # BattleInput, MenuInput ("Menu" map: menus + Pause), TileCursor, CursorMath (gamepad + mouse/keyboard)
        Menus/            # MainMenuController (page stack), MenuBackdrop (orbiting random map behind the title)
        QuickPlay/        # RandomRoster (pure, seeded random teams), QuickPlayDirector (match setup, pause, battle end)
        Camera/           # IsoCameraRig (namespace FriendshipWarriors.Cameras)
        Dev/              # BattleSandbox and other test-scene drivers
        Core/             # planned
      Editor/             # FriendshipWarriors.Editor.asmdef — MapEditorWindow, MapPreview, TestMapFactory, TestSceneBuilder, StartingJobsBuilder, Scenes/GameScenesBuilder; Setups/BattleSetupBuilder (prebuilt setups)
    Data/                 # ScriptableObject assets: maps, props, terrain palette, generator settings, jobs, abilities, equipment (EquipmentCatalog); Setups/ (Battle Setup assets)
    Input/                # BattleControls.inputactions (control schemes: Keyboard&Mouse, Gamepad)
    Materials/            # Terrain.mat (URP Lit, cloned per terrain look), Highlight.mat (transparent unlit)
    Scenes/               # MainMenu.unity (first in Build Settings), QuickPlay.unity, Battle_Test.unity (dev sandbox)
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
- Rebuild `MainMenu` and `QuickPlay` (and the `EquipmentCatalog` asset) with **Friendship Warriors → Dev → Rebuild Game Scenes** (`GameScenesBuilder`). It also sets Build Settings to MainMenu, QuickPlay, Battle_Test; both builders go through `GameScenesBuilder.ApplyBuildSettings()`.
- To play the game end to end, open `MainMenu` and press Play. Quick Play logs its seed (`[QuickPlay] Seed …`); set `QuickPlayDirector._fixedSeed` to rebuild a reported match.
- When scripting a virtual gamepad, each `script eval` takes long enough that a held D-pad press triggers key-repeat (cursor and menus repeat after 0.3–0.35 s). Place the cursor with `TileCursor.SetCoord` and pick menu items via `MenuModel.Select`, and use the pad only for buttons.
- Gamepad paths can be tested without hardware: in Play mode, `InputSystem.AddDevice<Gamepad>()` and `InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(...))` through `script eval`, then remove the device.
- After code changes, confirm the project compiles with no console errors before reporting work as done. If verification wasn't possible, say so.

### Editor gotchas (learned the hard way)

- New files on disk aren't compiled until an asset refresh: `script eval 'UnityEditor.AssetDatabase.Refresh();'`, then poll `script recompile_status`. Commands fail briefly during the domain reload.
- `EditorSceneManager.NewScene` unloads unreferenced assets, so load assets you intend to assign **after** creating the scene, or the reference saves as null.
- `capture game_view --save_path` writes under `Assets/`. Don't use it; take the inline image (`--max_resolution`) and decode it into the scratchpad.
- In Play mode `Destroy` is deferred to the end of the frame: clear references to destroyed objects right away, or a same-frame rebuild reuses them.
- Pipeline-specific flags go after `--`; the CLI's own flags (`--caller`, `--skill`, `--format`) go before it.
- **Never edit or refresh scripts while Play mode is running** (or before a stop has finished): the domain reload nulls non-serialized references such as `UnitView.Unit` and floods the console with errors. Stop, confirm `isPlaying` is false, then edit.
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
- 2026-10-22 — **Combat (FFT):** weapon damage = Sword/Rod P.Atk×WP, Knife and Rifle (FFT bow) ⌊(P.Atk+Speed)/2⌋×WP, Staff M.Atk×WP, bare hands ⌊P.Atk×Morale/100⌋×P.Atk (FFT's Brave formula with Morale). Hit% = 100 × (100−C-Ev)% front only × (100−S-Ev)% front/side × (100−A-Ev)% always. Critical: 5% base, raises XA by 0…XA−1 (recalled; knockback not modelled). Melee reaches the 4 neighbours within 3h; rifles 1–5 tiles (FFT bows' height bonus and line of fire not modelled). Rewards per successful action: Prof 8 + 2×job level + level/4 (verified), allies get ¼; EXP keeps FFT's 10 + (target level − actor level) but as a percentage of what the actor's next level costs (decided 2026-10-22): a same-level target is worth a tenth of a level at any level, stronger targets more, weaker less; at least 1 EXP, at most one level, none at 99. Non-attack actions (heals, buffs, items) give the same-level share — 10% of the actor's own next level, whoever the target — so support roles level at the same pace (FFT uses the target's level; ours is the actor's). KO at 0 HP with a 3-turn death counter, then the unit is lost (recalled). Victory/defeat when one team has nobody standing.
- 2026-10-22 — **Timed press (intended departure):** a marker sweeps a bar (1.2 s, perfect at 0.8 s, ±0.3 s window). Accuracy 0–1 is how close the press is to perfect; the chance it grants scales with accuracy², capped below 100%: block up to 60% when an enemy attacks a Player-team unit (blocked = no damage), critical up to 5% + 45% when a Player-team unit attacks. Order: evasion, then block, then critical. Tunable in `TimedPressRules` on the BattleController.
- 2026-10-23 — **Enemy AI:** each team is Human- or AI-controlled (`BattleController` Team Control; default Player = Human, Enemy = AI). `AiPlanner` (pure C#) plans the whole turn: every reachable tile (including staying) × every enemy in Attack range there, scored by expected damage (hit% × damage, capped at the target's HP) + a knock-out bonus (hit% × target max HP) + side (+2) / back (+4) bonuses − 1.5 per enemy that could reach the tile next turn. Attacks from its own tile are followed by a retreat to a less threatened tile (attack-then-move, as FFT's AI often does); otherwise it moves then attacks. With no attack available it walks toward the nearest enemy by walking distance. It ends facing the nearest enemy. Ties are broken with the battle's seeded RNG, so plans are deterministic. FFT's actual AI logic isn't public — these weights are ours, tune freely. Not yet: AI turn trades (Rapid Fire / Double Time), abilities, healing allies, retreating when hurt. AI turns play back as paced steps (~0.55 s apart) with the camera following; only human-controlled sides get timed presses (Brace! when the AI attacks a human unit).
- 2026-10-23 — **First job abilities (FFT mechanics, our names — user to approve or rename):**

  | Ours | FFT original | Kind | Effect | Prof (FFT JP) |
  |---|---|---|---|---|
  | **Drill** command | Squire *Basic Skill* | | | |
  | Grit | Accumulate | action, self | P.Atk +1 until the battle ends, stacks | 300 |
  | Shoulder Charge | Dash | action, range 1 | P.Atk × 1–4 damage, may knock back | 80 |
  | Hurl Rivet | Throw Stone | action, range 1–4 | P.Atk × 1–2 damage, may knock back | 90 |
  | Light Pack | Move +1 | movement | Move +1 | 200 |
  | Diligence | Gained JP Up | support | +50% Prof | 200 |
  | **Field Kit** command | Chemist *Item* | | | |
  | Tonic / Strong Tonic / Grand Tonic | Potion / Hi-Potion / X-Potion | item | +30 / +70 / +150 HP | 30 / 200 / 300 |
  | Aether Vial / Aether Flask | Ether / Hi-Ether | item | +20 / +50 MP | 300 / 400 |
  | Panacea | Elixir | item | full HP and MP | 900 |
  | Revival Salts | Phoenix Down | item | revives a KO'd unit (death counter still running) with 1–20 HP | 90 |
  | Long Toss | Throw Item | support (innate to Corpsman) | items reach 4 tiles instead of 1 | 350 |

  Rules: items come from a per-team shared stock and always land; damage abilities are physical (evaded exactly like Attack); knockback pushes one tile straight away with FFT's chance attacker Brave / (attacker + target Brave), using Morale (50% when equal), blocked by higher ground, gaps, blocked or occupied tiles and the map edge, and may drop to lower ground (no fall damage: none found in FFT). Offensive abilities give level-based EXP, everything else support EXP; Prof as for Attack, with Diligence's bonus. **The timed press stays Attack-only** (FFT abilities have no equivalent; revisit if wanted). Unusable entries are greyed out with the reason (none left, not enough MP, no target).
  Unverified: Dash ×1–4 and Throw Stone ×1–2 multipliers (sources disagree; one gives Throw Stone ×1–4), Phoenix Down's 1–20 HP, Hi-Ether/Elixir JP costs, Dash's 3h vertical reach.
  Deferred: Maiden's Kiss, Soft, Holy Water (need Frog, Stone, Undead). Knockback fall damage isn't in FFT as far as we can find; left out unless the user wants it. (Heal, the other cures and Counter Tackle arrived with statuses and reactions, below.)
- 2026-10-23 — **Magic (FFT):** the Evoker's *Evocation* (Black Magic) and the Warder's *Warding* (White Magic). Spell list set by the user; names approved for now (fallback: FFT-style numbering, Fire 1/2/3, Raise 2):

  | Ours | FFT original | MP | Range | Area (radius / vertical) | Speed | Power | Prof (FFT JP) |
  |---|---|---|---|---|---|---|---|
  | Incendiary Charge / Blast / Barrage | Fire / Fira / Firaga | 6 / 12 / 24 | 4 | 1 / 1, 2, 3 | 25 / 20 / 15 | Q 14 / 18 / 24 | 50 / 200 / 500 |
  | Cryo Charge / Blast / Barrage | Blizzard / Blizzara / Blizzaga | as Fire | | | | | |
  | Voltaic Charge / Blast / Barrage | Thunder / Thundara / Thundaga | as Fire | | | | | |
  | Toxin Cloud | Poison | 6 | 4 | 1 / 1 | 33 | hit +160, Poison | 150 |
  | Mend / Greater Mend / Grand Mend | Cure / Cura / Curaga | 6 / 10 / 16 | 4 | 1 / 1, 2, 3 | 25 / 20 / 15 | Q 14 / 20 / 30 | 50 / 180 / 450 |
  | Vital Ward | Regen (the user's "Regain") | 8 | 3 | single | 34 | hit +170, Regen | 350 |
  | Plate Ward / Aether Ward | Protect / Shell | 6 | 3 | single | 34 | hit +200 | 70 / 70 |
  | Purge | Esuna | 18 | 3 | 1 / 2 | 34 | hit +190 | 300 |
  | Resuscitate | Raise | 10 | 4 | single | 25 | hit +180, revive at 50% max HP | 180 |
  | Full Resuscitation | Arise (Raise 2) | 20 | 4 | single | 10 | hit +160, revive at full HP | 500 |
  | Feedback Loop (reaction) | Counter Magic | | | | | | 800 |

  Rules: spells target a tile (empty ground is fine; height doesn't block aiming) and hit everyone in the area, friend and foe. Area = tiles within the radius (FFT effect 2 = radius 1, a cross of five), within vertical × 2 half-steps of the centre tile. Charged spells go on the CT clock: each tick the spell's CT rises by its speed and at 100 it resolves, before unit turns in the same tick (`TurnOrder.NextEvent`); the turn-order panel lists pending spells. The caster gets **Charging** (can't move, act or evade) until then. **MP is paid when casting starts** and casting ends the turn's options (Wait/facing, 80 CT). Knock-out, Stop, Sleep or Silence on the caster cancels the spell (MP lost). Damage/heal = M.Atk × Q × caster Resonance/100 × target Resonance/100 (Shell ×⅔ on damage, not heals); always lands. Status/cure/revive hit % = (M.Atk + power) × the two Resonances, capped at 100. Silence blocks spells (menu greys them out "silenced"; AI won't cast). **Purge cures every harmful status we have** (Poison, Blind, Silence, Sleep, Slow, Stop, Disable, Immobilize and Panic — FFT Esuna's list is shorter; user decision) and never buffs; Panic follows Morale, so Purge lifts it by raising Morale to 10 — **battle-only** (the in-battle Morale modifier, like FFT's temporary Brave changes; base Morale is untouched and the modifier resets when the battle ends; user decision, tested). Revives only reach knocked-out units whose death counter is still running; the living and the lost are not targets. The AI revives downed allies, never enemies. Elements (Fire/Ice/Lightning) are tagged only; no weaknesses yet. Feedback Loop: when an enemy's offensive spell hits the reactor, at Morale % it casts the same spell back at the caster's tile instantly, paying its MP, no rewards, no chains. Rewards per spell: offensive → level EXP vs the first enemy hit, support → support EXP; Prof as for actions. **The timed press stays Attack-only** (no press for spells). AI (`AiPlanner.SpellScore`): expected damage/heal/status value over everyone in the area, friendly fire ×1.5, charged spells discounted (harm ×0.8, help ×0.9), heals only count for allies missing 25%+, minus MP × 0.1; must beat the best attack; deterministic. **User decisions (2026-10-23):** the names stand for now; Feedback Loop stays; an interrupted spell's MP is lost; casting ends the turn; Purge's Panic cure is battle-only.
  Verified (Final Fantasy Wiki, Game8): Fire tiers' MP, range, effect, speed, Q and JP; the damage and status formulas; White Magic JP (Cure 50, Cura 180, Curaga 450, Regen 350, Protect 70, Shell 70, Esuna 300); Counter Magic 800 JP; Raise MP 10, range 4, effect 1, speed 25, JP 180 (PSX NA; 200 in other versions); Arise MP 20, full HP, hit (M.Atk + 160) × Faiths, JP 500 (600 in WotL). Recalled/unverified: Poison's speed and hit bonus, the buff and Esuna speeds (34) and hit bonuses, Cura/Curaga speeds and Q, Blizzard/Thunder mirroring Fire exactly, which statuses interrupt charging, MP paid at cast start, Counter Magic paying MP, Raise's 50% HP and +180 hit bonus, Arise's speed 10, range 4 and single-tile effect.
  Deferred: tier-4 spells, Flare, Death, Holy, Frog/Toad (and Maiden's Kiss), Reraise, Protectja/Shellja, elemental weaknesses, Magic Attack/Defense Up, Short Charge, AI use of items and job skills.
- 2026-10-22 — **Battle UI:** uGUI + TextMeshPro, built in code by `BattleHud` on a Screen Space Overlay canvas scaled from 1920x1080 (readable on Steam Deck's 1280x800). Menus are driven by `MenuModel` and the battle's own input (D-pad/stick/keys, A/B); the mouse can hover and click. Placeholder styling.
- 2026-10-22 — **Unit visuals:** a placeholder object with a camera-facing sprite (rotates on the vertical axis only). Two drawn poses per job (front ¾ and back ¾, facing screen-right) mirrored to cover all four views. Set `FrontSprite`/`BackSprite` on a job to replace the generated placeholder.
- 2026-10-23 — **Morale and Resonance (the user's replacements for Brave and Faith), 0–100**, each a permanent base plus in-battle swings that reset after battle. Generic units roll both in 45–74. Defaults (tunable constants):
  - **Morale** (Brave): reactions trigger at Morale %; knockback chance uses it in FFT's Brave formula; bare hands ⌊P.Atk×Morale/100⌋×P.Atk. *Our additions (user design):* melee weapons (Sword, Knife, Rod, Staff; not Rifle) deal × (Morale+130)/200 (1.0 at 70, 1.15 at 100, 0.65 at 0); all physical damage taken × (270−Morale)/200 (1.0 at 70, 0.85 at 100). Morale below 10 → **Panic** (FFT Chicken): the unit is driven by the AI to flee, can't act, takes ×1.5 physical damage, regains +1 Morale per turn and recovers at 10. Every standing teammate loses 5 Morale (battle only) when a unit is knocked out ("heavy losses", our addition). Permanent hooks: each Commendation +3 base Morale (`Unit.CommendationMoraleBonus`), `SufferDefeat()` −5 (`DefeatMoralePenalty`) — placeholder amounts to tune with the campaign. Stealing/disarming/capturing will use Morale when those abilities exist.
  - **Resonance** (Faith): FFT's magic factor caster Resonance/100 × target Resonance/100 (`CombatFormulas.MagicFactor`), ×⅔ with Shell; used by magic once it exists. High Resonance hits and is hit harder by magic; near 0 is nearly immune but useless as a caster. Gadgets will use it later.
- 2026-10-23 — **Status effects (FFT):** durations in clock ticks counted on the battle clock (`TurnOrder` ticks every unit's statuses each tick): Poison 36 (−max HP/16 at the end of the unit's turn), Regen 32 (+max HP/8 likewise), Protect 32 (physical ×⅔), Shell 32 (magic ×⅔), Haste 32 (CT gain Speed×3/2), Slow 32 (Speed/2), Stop 20 (no CT, no moving/acting, no evasion), Sleep 60 (no moving/acting, no evasion, ×1.5 physical damage taken, the turn passes; any damage wakes the unit), Blind (until cured; attacker's hit % halved), Silence (2 of the unit's own turns, counted down as each of its turns ends; will block spells once they exist), Disable 24 (can't act, can move), Immobilize 24 (can't move, can act), Panic (Morale-driven). Re-applying refreshes to the longer duration; Haste and Slow cancel each other; KO clears all statuses; battle end clears everything. Sources: Final Fantasy Wiki and Game8 FFT status pages.
  User decisions (2026-10-23): any damage wakes a sleeping unit; Poison starts gentle at max HP/16 (FFT is usually cited as ⅛; raise later if needed), Regen stays at FFT's ⅛; Silence lasts 2 of the unit's own turns (FFT: until cured); a KO clears every status; the death counter stays at 3 of the unit's own turns.
  Still unverified: whether Sleep freezes CT (Game8 says so for one version; we keep CT running and skip the turn), Disable letting the unit move (PSX Don't Act).
- 2026-10-23 — **Reactions (FFT, Morale-chance):** fire after damage from an action, never chain, and don't fire while asleep, stopped, disabled or panicking. Order in an attack: evasion → timed block (player skill) → critical → damage → reaction (Morale chance). Proposed names (user to approve):

  | Ours | FFT original | Job | Effect | Prof (FFT JP) |
  |---|---|---|---|---|
  | Counter Charge | Counter Tackle | Cadet | when hit by a physical attack in reach, hit back with P.Atk × 1–4, may knock back | 180 |
  | Self-Dose | Auto Potion | Corpsman | when hurt, use the team's weakest HP item on yourself | 400 |
  | Guard Stance | Parry (Weapon Guard) | Vanguard | passive: weapon evasion counts against front and side physical attacks | 200 |
  | Adrenaline | Speed Save | Marksman | when hurt, Speed +1 for the battle | 800 |
  | Mending Ward | Regenerator | Warder | when hurt, gain Regen | 400 |

  Evoker's Counter Magic (Feedback Loop) arrived with magic, below. Starter weapon W-Ev (recalled FFT values): saber 5, knife 5, rifle 0, rod 20, staff 15.
- 2026-10-23 — **Cures (FFT):** First Aid (Squire *Heal*, 150, cures Blind and Silence on self or adjacent), Antitoxin (*Antidote*, 70, Poison), Eyewash (*Eye Drops*, 80, Blind), Throat Lozenge (*Echo Herbs*, 120, Silence), Field Remedy (*Remedy*, 700, Poison/Blind/Silence/Sleep — cut down to the statuses we have). Remedy's JP cost is recalled.
- 2026-10-23 — **Enemy AI and statuses:** immobilized units don't move, disabled units approach without attacking, panicking units flee to the reachable tile farthest from the enemy, and attacks into a likely counter-strike are scored down by their expected counter damage.
- 2026-10-24 — **Battle setup and objectives.** A battle is described by a `BattleSetup` asset (*Create → Friendship Warriors → Battle Setup*): player and enemy team sizes (**standard / quick play is 5 v 5**, `BattleSetup.StandardTeamSize`; campaign and special levels may change them, 1–12), a roster per side (`RosterEntry`: job, optional display name, optional level, and a stable **key**), a default level, and a `BattleObjective`. A side fields min(team size, roster length, deployment tiles); `BattleSetupValidator` warns about short rosters, missing jobs, duplicate keys and objective keys that don't match a fielded unit. Objectives reference units by `Unit.Key` (not name or index) so campaign scripting and online play can name them stably.
  - **Victory conditions:** *Defeat all enemies* (standard) or *Defeat target* (one enemy, by key). **The target counts as defeated as soon as it is knocked out** (user decision); other enemies may still be standing. A target key that matches no enemy falls back to defeat-all (and the validator flags it).
  - **Protected units** (any number of player keys, on top of either victory condition): **the player loses only when a protected unit is permanently lost** — its 3-turn death counter runs out (user decision). Being knocked out and revived is fine.
  - **The player always loses when every Player unit is down.**
  - **Simultaneous events** (`ObjectiveEvaluator.Evaluate`, checked after every action and death-counter tick): **winning beats everything** (user decision, 2026-10-24). (1) The victory condition met → victory, even if in the same check a protected unit was permanently lost or the last Player unit fell (e.g. a counter-attack or area spell); (2) a protected unit lost → defeat; (3) all Player units down → defeat.
  - **Presentation:** the HUD shows the objective top left ("Defeat Commander Vael | Protect: Escort Lira"), the battle opens with an "Objective: …" banner, targets carry a red TARGET tag and protected units a green PROTECT tag, and the end banner gives the reason ("Victory! Commander Vael defeated", "Defeat... Escort Lira was lost").
  - **AI awareness** (`AiHints`): enemies weight attacks on protected player units ×1.5 (attacks only, not spells), and an enemy target plays cautiously (threat cost ×2 when choosing tiles). Tunable constants.
  - **User decisions (2026-10-24):** the target is defeated on KO; a protected unit only counts when permanently lost; winning beats everything when events coincide; the red TARGET and green PROTECT tags are approved; the AI weights (×1.5 on protected units, the target counting danger ×2) are approved.
  - Prebuilt setups (*Friendship Warriors → Data → Create Battle Setups*, `Data/Setups/`): **Standard (5 v 5)**, **Defeat the Commander** (enemy Vanguard "Commander Vael", Lv 7, key `commander`) and **Escort** (player Warder "Escort Lira", key `escort`, protected). Each side fields all six jobs between them. The test scene's `BattleSandbox` takes a setup; swap it to try each objective.
- 2026-10-22 — **Front end:** `MainMenu` is the first scene: title, then Quick Play, Campaign (greyed, not selectable yet), Options (greyed). `MenuModel` skips disabled items. `MainMenuController` keeps a page stack, so the planned Quick Play submenu (online, army building) is one more page: Quick Play goes straight into a match for now. Menus read a separate **"Menu"** action map (`MenuInput`), so they keep working while battle input is switched off.
- 2026-10-22 — **Quick play:** a random map and 5 random Player (human) vs 5 random Enemy (AI) units, all from one seed shown in the top-left corner and logged. Each unit gets a random job from the `JobCatalog`, the same level for everyone (5), random legal gear in every slot (no shield with a two-handed weapon), every ability of its job line (the job and its requirement jobs) learned, a random reaction, support and movement ability equipped, and a random secondary command from its line. Names are unique and drawn from a military-steampunk pool. Both teams get the same item stock. Master seed → map seed, then the player roster, then the enemy roster, so one seed rebuilds the whole match. Once the battle-objectives work lands, quick play should use the Standard 5 v 5 setup and its objective.
- 2026-10-22 — **Pause:** gamepad Start or keyboard P opens Resume / Quit to Main Menu, and B / Esc resumes. Pausing switches off `BattleController` and `BattleInput`, which freezes turn flow, AI turns and input. Battle timers run on a `BattleClock` that stops while the controller is disabled: an open timed press (window, marker sweep and timeout), the result hold, spell pauses and AI steps all freeze and resume exactly where they were. Presses made while paused don't count.
- 2026-10-22 — **Quick play battle end:** victory or defeat shows the result banner, then goes back to the main menu. Confirm (or a click) after 1 s returns, and so does a 6 s timeout. There's no Play Again choice: another match is Quick Play again, with a new seed.
- 2026-10-22 — **Campaign losses (planned, not built):** losing a campaign battle is **not** game over, and the campaign carries on after a loss. This is a deliberate difference from FFT, where losing a story battle is game over.
- Open questions: save system scope; AI difficulty levels; whether to add FFT's multi-layer tiles (bridges, walking under things).
