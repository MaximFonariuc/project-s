# Unity Project Context

<!-- unity-onboarding:generated:start -->

## Project Summary

- Project root: `D:/Git/SoloProjects/project-s`
- Project: single-player character-action/slasher prototype focused on the player combat loop.
- Last analyzed: 2026-08-16
- Last analyzed commit: `d80e84513e1ccd71ae287f45a86fcd37ab817514`

## Confirmed Environment

- Unity version: `6000.0.80f1` (`2dfd32957da2`).
- Render pipeline: Universal Render Pipeline `17.0.4`.
- Input system: Unity Input System `1.19.0` behind the first-party `ModularInput` abstraction; both input backends are enabled in Project Settings.
- Active target: `StandaloneWindows64` in the connected Editor.

## Important Packages And Frameworks

| Area | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Animation | Mecanim, Playables, UMotion Editor, and Animation Converter are present. | Confirmed | `Packages/manifest.json`, `Assets/_App/Modules/Animation`, `Assets/_Modules/UMotionEditor`, `Assets/_Modules/AnimationConverter` |
| Camera | Cinemachine `3.1.7`. | Confirmed | `Packages/manifest.json` |
| Composition | VContainer `1.19.0`; registrations are grouped under Core/Runtime/Composition. | Confirmed | `Packages/packages-lock.json`, `Assets/_App/Core/Runtime/Composition` |
| Async | UniTask `2.5.10` is installed and referenced by the Core runtime assembly. | Confirmed | `Packages/packages-lock.json`, `Assets/_App/Core/Runtime/ProjectS.Core.Runtime.asmdef` |
| Unity MCP | CoplayDev Unity MCP `v10.1.2`; one matching Editor instance is connected. | Confirmed | `Packages/manifest.json`, `mcpforunity://instances` |
| Networking | No networking package or active multiplayer architecture was found; multiplayer is explicitly out of scope. | Confirmed | `Packages/manifest.json`, `AGENTS.md` |

## Directory Structure

| Path | Purpose | Confidence | Evidence |
| --- | --- | --- | --- |
| `Assets/_App/Core/Runtime` | Project S gameplay and composition code. | Confirmed | `AGENTS.md`, namespaces and asmdef |
| `Assets/_App/Core/Editor` | Project-specific editor tooling, including animation-library import/validation. | Confirmed | `AGENTS.md`, `Assets/_App/Core/Editor/AnimationLibrary` |
| `Assets/_App/Modules` | First-party reusable Input, Animation, and StateMachine modules. | Confirmed | asmdefs and module documentation |
| `Assets/_Game` | Scenes, prefabs, models, settings, and gameplay animation assets. | Confirmed | `AGENTS.md`, asset inventory |
| `Assets/_Modules` | Imported/vendor editor extensions and content. | Confirmed | `AGENTS.md`, UMotion and Animation Converter files |

## Assembly Boundaries

| Assembly | Responsibility | Key references | Notes |
| --- | --- | --- | --- |
| `ProjectS.Core.Runtime` | Project-specific gameplay and presentation integration. | Modular Input/Animation/StateMachine, VContainer, Input System, Cinemachine, UniTask | Runtime; depends inward on modules. |
| `ProjectS.Core.Editor` | Project-specific editor tools. | `ProjectS.Core.Runtime` | Editor-only. |
| `ModularAnimation.Core` | Backend-neutral animation contracts and semantic values. | None | Runtime, no Unity engine references. |
| `ModularAnimation.Unity` | Mecanim/Playables implementation. | `ModularAnimation.Core` | Runtime; no Core dependency. |
| `ModularAnimation.Unity.Editor` | Animation profile authoring and validation. | Modular Animation assemblies | Editor-only. |
| `ModularInput.Core` / `ModularInput.Unity` | Semantic input router/buffer and Unity Input System adapter. | Unity adapter depends on Core and Input System. | Runtime; no Project S dependency. |
| `ModularStateMachine.Core` / `ModularStateMachine.VContainer` | State-machine primitives and DI integration. | VContainer integration depends on Core. | Runtime; no Project S dependency. |

## Scenes And Startup Flow

- Build scenes: `Assets/_Game/Scenes/PrototypeArena.unity` (enabled, index 0).
- Likely startup scene: `PrototypeArena` (confirmed active in the connected Editor and the only build scene).
- Scene loading flow: no separate boot scene was identified.

## Architecture

| Pattern | Finding | Confidence | Evidence |
| --- | --- | --- | --- |
| Feature-oriented gameplay | Core is organized around Character, Combat, Camera, Input, TargetLock, and related features. | Confirmed | `AGENTS.md`, `Assets/_App/Core/Runtime` |
| Explicit composition | VContainer registrations live under Composition and use scoped lifetimes. | Confirmed | `Assets/_App/Core/Runtime/Composition` |
| Animation authority | Gameplay owns state/timing; a Mecanim/Playables adapter presents confirmed state. Finite actions use clip lanes and an arbiter. | Confirmed | `Assets/_App/Modules/Animation/README.md` |
| Animation import boundary | Adjacent JSON metadata drives deterministic Humanoid/Generic FBX import and validation for external libraries. | Confirmed | `Assets/_App/Core/Editor/AnimationLibrary/README.md` |
| State machines | Reusable first-party state-machine module is present and referenced by Core. | Confirmed | asmdefs, `Assets/_App/Modules/StateMachine` |

## Coding Conventions

- Namespace style: `_App` for Project S runtime code; `ProjectS.Core.Editor.*` for current editor tooling; module-specific namespaces such as `ModularAnimation`.
- Serialized fields: `[SerializeField] private` with `_camelCase`; migration attributes are used when serialized names change.
- Composition: constructor injection for plain objects and VContainer registrations in composition helpers.
- Comments/docs: XML docs are selective; architecture and ownership decisions are recorded in Markdown.

## Testing And Validation

- Unity Test Framework `1.6.0` is installed.
- The connected Unity MCP exposes EditMode and PlayMode test discovery, but existing discovered entries are placeholder-named and were not run during onboarding.
- Project instructions prohibit creating automated tests unless explicitly requested.
- Baseline Unity Console at analysis time: zero errors and zero warnings.

## Available Unity Tooling

| Capability | Status | Evidence |
| --- | --- | --- |
| `unity.connection.status` | available | `mcpforunity://instances`, `mcpforunity://editor/state` |
| `unity.editor.version` | available | `mcpforunity://project/info` |
| `unity.console.read` | available | Unity MCP `read_console` |
| `unity.scene.list` | available | Unity MCP `manage_scene` |
| `unity.scene.inspect` | available | Unity MCP scene and GameObject resources |
| `unity.buildsettings.read` | available | `manage_scene(action=get_build_settings)` |
| `unity.gameobject.inspect` | available | GameObject resources and tools |
| `unity.asset.search` | available | Unity MCP `manage_asset` |
| `unity.package.read` | available | repository manifests and Unity MCP package tools |
| `unity.tests.list` | available | `mcpforunity://tests` |
| `unity.tests.run` | available | Unity MCP `run_tests` |
| `unity.playmode.read` | available | `mcpforunity://editor/state` |
| `unity.profiler.read` | available | Unity MCP profiler and rendering-stat capabilities |

## Important Constraints

- Preserve the current dirty worktree and do not rewrite unrelated animation/combat work.
- Do not edit scenes, prefabs, Animator Controllers, ScriptableObjects, or Project Settings blindly; use the connected Editor and check Console afterward.
- Keep gameplay authority outside Animator state and animation events.
- Do not add packages when Unity and the already-installed animation tools are sufficient.
- Keep third-party/license-restricted animation provenance explicit.

## Unknowns And Confidence

- No current CI or documented command-line build pipeline was found.
- The target hardware and animation memory/frame-time budgets are not documented.
- The imported external animation libraries have source-specific license constraints and must not be assumed production-cleared.

## Source Files Inspected

- `AGENTS.md`
- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/ProjectSettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- first-party `.asmdef` files under `Assets/_App`
- `Assets/_App/Modules/Animation/README.md`
- `Assets/_App/Core/Editor/AnimationLibrary/README.md`
- representative files under `Assets/_App/Core/Runtime/Composition`, `Assets/_App/Modules/Input`, and `Assets/_App/Core/Runtime/Character`
- Unity MCP project, editor-state, test, build-settings, asset-search, and Console snapshots

<!-- unity-onboarding:generated:end -->
