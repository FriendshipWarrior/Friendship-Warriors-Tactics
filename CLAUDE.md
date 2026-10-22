# CLAUDE.md

Guidance for Claude Code when working in this repository.

## Project

**Friendship Warriors Tactics** — an isometric, turn-based tactics RPG in the spirit of *Final Fantasy Tactics*: height-mapped grid battlefields, a speed/charge-time turn order, a job/class system, facing-based combat, and a story campaign between battles.

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
- **Unity MCP:** _not yet configured._ Once connected, use it to inspect/modify scenes, prefabs, and components, read the Unity console, trigger recompiles, and run tests — rather than hand-editing serialized YAML. Until then, Claude writes C# and editor scripts, and the user performs Editor-side setup (scene wiring, assigning references) from explicit instructions.

## Project Layout (target)

Keep game code under one root so it can get its own assembly definitions:

```
Assets/
  _Project/
    Scripts/
      Runtime/            # FWT.Runtime.asmdef
        Core/             # bootstrapping, service locator, events, save/load
        Grid/             # tiles, heights, pathfinding, map data
        Battle/           # turn order (CT), battle state machine, actions, AI
        Units/            # stats, jobs, abilities, equipment, status effects
        Camera/           # isometric camera, rotation, zoom
        UI/
      Editor/             # FWT.Editor.asmdef — map editor tools, inspectors
    Data/                 # ScriptableObject assets: jobs, abilities, items, maps
    Prefabs/
    Scenes/
    Art/ Audio/ Materials/
  Tests/
    EditMode/             # FWT.Tests.EditMode.asmdef — pure logic tests
    PlayMode/             # FWT.Tests.PlayMode.asmdef
```

Root namespace: `FriendshipWarriors` (e.g. `FriendshipWarriors.Battle`). `Assets/Scenes/SampleScene.unity` is a template leftover (it's in Build Settings); replace it with a real scene, removing it from Build Settings in the Editor.

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

- New battle/grid/stat logic gets **EditMode tests** (Unity Test Framework / NUnit) — add `com.unity.test-framework` to the manifest when the first test lands.
- Run tests from the Editor Test Runner, via Unity MCP once available, or headless:
  `"/Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults Temp/results.xml`
  (headless runs fail if the project is already open in the Editor).
- After code changes, confirm the project compiles with no console errors before reporting work as done. If verification wasn't possible, say so.

## Design Decisions

Record decisions here as they are made (date — decision — rationale).

- _None yet. Open questions: map authoring approach (custom editor tool vs. tilemap vs. modeled meshes), art style (sprites on 3D tiles vs. full 3D), target platforms, save system scope._
