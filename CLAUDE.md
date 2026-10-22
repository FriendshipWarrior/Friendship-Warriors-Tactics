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
        Grid/             # GridCoord, Tile, BattleGrid, BattleMapData, GridMetrics
          Pathfinding/    # Pathfinder, MovementRange, MovementProfile, Occupancy
          View/           # GridView, TileView (placeholder geometry + highlights)
        Camera/           # IsoCameraRig (namespace FriendshipWarriors.Cameras)
        Dev/              # GridSandbox and other test-scene drivers
        Core/ Battle/ Units/ UI/   # planned
      Editor/             # FriendshipWarriors.Editor.asmdef — TestMapFactory, future map tools
    Data/                 # ScriptableObject assets: jobs, abilities, items, maps
    Materials/            # Tile.mat (URP Lit, tinted per tile via MaterialPropertyBlock)
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
- After code changes, confirm the project compiles with no console errors before reporting work as done. If verification wasn't possible, say so.

### Editor gotchas (learned the hard way)

- New files on disk aren't compiled until an asset refresh: `script eval 'UnityEditor.AssetDatabase.Refresh();'`, then poll `script recompile_status`. Commands fail briefly during the domain reload.
- `EditorSceneManager.NewScene` unloads unreferenced assets, so load assets you intend to assign **after** creating the scene, or the reference saves as null.
- `capture game_view --save_path` writes under `Assets/`. Don't use it; take the inline image (`--max_resolution`) and decode it into the scratchpad.
- Pipeline-specific flags go after `--`; the CLI's own flags (`--caller`, `--skill`, `--format`) go before it.
- Play-mode checks: `editor set_auto_tick` + `editor.playmode set_run_in_background` first, then `job wait` on `Time.frameCount` to prove frames advance. Afterwards revert run-in-background and confirm `PlayerSettings.runInBackground` didn't get saved as changed.

## Design Decisions

Record decisions here as they are made (date — decision — rationale).

- 2026-10-22 — Tile heights are integer **steps** of 0.5 world units (FFT-style half steps); `Jump` is measured in steps and limits both climbing and dropping. — Matches FFT feel; keeps rules integer-only.
- 2026-10-22 — Movement is 4-directional, 1 Move per tile; deep water and void are impassable; enemies block, allies can be passed but not stopped on. — FFT baseline; terrain move costs can be added later in `Pathfinder`.
- 2026-10-22 — Camera: orthographic, 30° pitch, 45° + 90° steps, Q/E rotate, scroll zoom, WASD/arrows pan. — Classic FFT framing.
- 2026-10-22 — Placeholder geometry (column + inset cap per tile) built at runtime from `BattleMapData`. — Stand-in until the art direction is decided.
- 2026-10-22 — **Scope:** an FFT-style game (FFT look and mechanics) with original characters, story and job names. Modes planned: story campaign, quick play, and possibly random map generation.
- 2026-10-22 — **Art style:** 2D sprite characters on 3D tiles. Each unit is a 3D placeholder object with a sprite on a camera-facing child (rotates on the vertical axis only, so it stays upright). Units face the **4 grid directions** (FFT). Because the camera always sits on a 45° diagonal, only 2 poses need drawing (front 3/4 and back 3/4), mirrored to cover all 4 views.
- 2026-10-22 — **Platforms:** Steam on PC. Windows first, then SteamOS/Steam Deck (Linux), then macOS if it isn't too much work. **Controller support is required**, since the game must work on Steam Deck. Every interaction needs a gamepad path, and the UI must be readable at 1280x800.
- 2026-10-22 — **Map authoring: hybrid.** Grid data (`BattleMapData`) is the single source of truth for campaign, quick play and the random generator. A custom in-Unity editor paints it, a decoration/props layer sits on top, and custom backdrops are allowed later for set pieces. Fully modeled maps are out of scope (no 3D artist).
- Open questions: save system scope; walls hiding units (fade/cutaway).
