# Codex Execution Spec — FromSoftware Animation Reference Pipeline

> **Purpose:** Build a reproducible local pipeline that inventories, previews, converts, retargets, validates, and imports animation reference assets from **Player Animation Repositories Collection**, **ELDEN VINS**, and optionally a local **Elden Ring** installation into **Unity**.
>
> **Scope:** internal, non-commercial prototype/reference use only.
>
> **Execution rule:** do **not** bulk-process thousands of files until the pilot pipeline passes all validation gates.

---

## 0. How Codex must use this document

This file is an **execution specification**, not background reading.

Codex must:

1. Read this file completely before changing the repository.
2. Inspect the current machine/repository before assuming paths, tool versions, or installed dependencies.
3. Work phase-by-phase.
4. Never modify source archives in `01_RAW/`.
5. Prefer scripts and deterministic batch operations over manual one-off steps.
6. Produce logs/manifests for every transformation.
7. Stop a batch phase if validation fails.
8. Never invent an animation's semantic name when it is not known.
9. Preserve original animation IDs and source paths even after friendly renaming.
10. Keep the pipeline reversible: every generated file must be traceable back to its source.

### Default Codex instruction

When this file is handed to Codex, the recommended first instruction is:

```text
Read CODEX_FromSoftware_Animation_Pipeline.md completely.
Inspect the repository and local tools.
Execute Phase 0 and Phase 1 only.
Do not start bulk conversion.
Create the required directory structure, manifests, scripts, and a status report.
If a required external tool is missing, document the exact blocker and prepare the integration point instead of guessing.
```

---

# 1. Goal

Create a searchable local animation reference library for:

- Player
- Humanoid mobs
- Humanoid bosses
- Large humanoid bosses
- Custom/non-humanoid creatures
- One-handed sword
- Sword + shield
- Greatsword
- Katana
- Dual blades
- Twinblade
- Spear
- Halberd
- Axe / greataxe
- Hammer
- Fist / claw
- Bow
- Staff
- Magic / casting
- Dodges
- Parries
- Hit reactions
- Knockdowns
- Deaths
- Grabs
- Ripostes
- Executions
- Aerial attacks
- Traversal/reference locomotion

The final system must let a developer answer questions such as:

```text
Show all Boss + Greatsword + Heavy Attack animations with verified Root Motion.
```

or:

```text
Show all Katana attacks sourced from PlayerRepo and preview them on the Unity test avatar.
```

---

# 2. Non-goals

Do **not** attempt to:

- publish or redistribute extracted source assets;
- make the resulting asset library production-license-clean;
- recreate Elden Ring gameplay logic;
- copy Elden Ring VFX systems;
- copy Elden Ring AI;
- rebuild TAE/HKS behavior exactly inside Unity;
- convert the entire source collection before the pilot is validated;
- force every creature onto Unity Humanoid;
- discard source metadata after conversion.

This is a **reference and prototyping pipeline**.

---

# 3. Source priority

Process sources in this order.

## Priority A — Player Animation Repositories Collection

Use first because many animations are already retargeted to the Elden Ring player skeleton.

Expected advantages:

- large quantity of player-compatible HKX animations;
- preserved animation IDs;
- multiple character/moveset collections;
- much lower retarget cost than raw NPC/boss animations.

## Priority B — ELDEN VINS

Use as a secondary source for:

- player combat movesets;
- combo-oriented animations;
- magic/casting references;
- modified combat actions;
- event timing references.

Do not assume the mod remains permanently available online. If the archive already exists locally, treat the local copy as the source of truth for this pipeline.

## Priority C — Vanilla Elden Ring

Use only when needed for:

- source skeletons;
- source models;
- original animation IDs;
- NPC/boss animation archives;
- reference comparison in DSAnimStudio.

Do not dump the entire game into the working dataset unless required.

---

# 4. Tool roles

The exact installed versions must be detected and recorded by Codex at runtime.

## 4.1 WitchyBND / compatible FromSoftware unpacker

Role:

- decompress `.dcx`;
- unpack supported BND/ANIBND containers;
- preserve archive structure;
- optionally repack when required for inspection workflows.

Do not use it as an assumed universal HKX-to-FBX converter.

## 4.2 DSAnimStudio

Role:

- open Elden Ring animation archives;
- preview animations;
- inspect animation IDs;
- inspect TAE event timing;
- identify unknown animation clips;
- compare player and NPC/boss animation behavior.

DSAnimStudio is primarily an **inspection/metadata tool** in this pipeline.

## 4.3 Blender

Role:

- receive source animation data through the selected HKX bridge/add-on;
- store source armatures;
- retarget animation;
- bake animation;
- normalize axes/scale;
- preserve or extract root motion;
- export FBX.

## 4.4 HKX → Blender bridge

Preferred architecture:

```text
HKX
 ↓
FromSoftware-aware Blender importer/add-on
 ↓
Blender Action + source armature
```

Candidate implementations may include a maintained FromSoftware Blender add-on such as Soulstruct tooling.

**Codex must not hardcode a candidate as working until a pilot import has been verified on actual Elden Ring HKX files.**

Validation requirements are defined later.

## 4.5 Unity

Role:

- import FBX;
- configure Humanoid or Generic rigs;
- create/assign Avatar;
- preserve additional weapon bones;
- preview clips;
- validate root motion;
- expose clips through an Animation Browser;
- drive the gameplay prototype.

---

# 5. Repository layout

Codex must create or normalize the project to this structure:

```text
RPG_Animation_Library/
│
├─ CODEX_FromSoftware_Animation_Pipeline.md
│
├─ README.md
│
├─ .gitignore
│
├─ config/
│  ├─ pipeline.json
│  ├─ skeleton_profiles/
│  ├─ retarget_maps/
│  └─ unity_import_profiles/
│
├─ 00_Tools/
│  └─ README.md
│
├─ 01_RAW/
│  ├─ PlayerRepo/
│  ├─ EldenVins/
│  └─ EldenRing_Vanilla/
│
├─ 02_UNPACKED/
│  ├─ PlayerRepo/
│  ├─ EldenVins/
│  └─ EldenRing_Vanilla/
│
├─ 03_CATALOG/
│  ├─ manifests/
│  ├─ previews/
│  ├─ reports/
│  └─ unknown/
│
├─ 04_CONVERTED/
│  ├─ PlayerRepo/
│  ├─ EldenVins/
│  └─ EldenRing_Vanilla/
│
├─ 05_RETARGETED/
│
├─ 06_CLEAN/
│
├─ 07_FBX/
│
├─ 08_UNITY/
│
├─ Database/
│  ├─ animations.csv
│  ├─ characters.csv
│  ├─ skeletons.csv
│  ├─ sources.csv
│  └─ pipeline_runs.csv
│
├─ logs/
│
├─ scripts/
│  ├─ inventory_sources.py
│  ├─ unpack_sources.py
│  ├─ index_animations.py
│  ├─ build_catalog.py
│  ├─ convert_batch.py
│  ├─ validate_conversion.py
│  ├─ retarget_batch.py
│  ├─ validate_exports.py
│  └─ generate_report.py
│
└─ tests/
   ├─ fixtures/
   ├─ expected/
   └─ reports/
```

---

# 6. Immutable source policy

`01_RAW/` is immutable.

Codex must:

- never rename files in `01_RAW/`;
- never overwrite them;
- never unpack in-place;
- never convert in-place;
- never write generated metadata inside source folders.

For every source file, calculate:

```text
SHA-256
size
relative source path
source package
scan timestamp
```

This makes all derived assets traceable.

---

# 7. Configuration

Create `config/pipeline.json`.

Minimum structure:

```json
{
  "schema_version": 1,
  "project_root": ".",
  "paths": {
    "raw": "01_RAW",
    "unpacked": "02_UNPACKED",
    "catalog": "03_CATALOG",
    "converted": "04_CONVERTED",
    "retargeted": "05_RETARGETED",
    "clean": "06_CLEAN",
    "fbx": "07_FBX",
    "database": "Database",
    "logs": "logs"
  },
  "tools": {
    "witchybnd": null,
    "dsanimstudio": null,
    "blender": null,
    "hkx_blender_bridge": null,
    "unity_editor": null
  },
  "conversion": {
    "pilot_only": true,
    "max_batch_size": 20
  }
}
```

Tool paths may initially be `null`.

Never fabricate a path.

---

# 8. Canonical animation record

Every animation must have a single metadata record.

Recommended schema:

```text
LibraryID
Source
SourcePackageVersion
SourceGame
SourceCharacter
CharacterType
OriginalPath
SourceHash
AnimationID
OriginalFileName
FriendlyName
SemanticConfidence
WeaponFamily
ActionType
ActionVariant
Direction
Loop
DurationSeconds
FrameRate
RootMotionStatus
RootTranslation
RootYaw
SkeletonProfile
RetargetProfile
ConversionStatus
RetargetStatus
UnityImportStatus
PairGroupID
TAEReference
PreviewPath
Notes
CreatedAt
UpdatedAt
```

## Enumerations

### CharacterType

```text
Player
HumanoidMob
HumanoidBoss
LargeHumanoid
Creature
Unknown
```

### WeaponFamily

```text
Unarmed
Sword1H
SwordShield
Greatsword
Katana
DualBlade
Twinblade
Spear
Halberd
Axe
Greataxe
Hammer
Fist
Claw
Bow
Crossbow
Staff
Magic
Other
Unknown
```

### ActionType

```text
Idle
Locomotion
Traversal
Turn
Dodge
Block
Parry
AttackLight
AttackHeavy
AttackCharged
AttackRunning
AttackJump
AttackAerial
Launcher
Combo
Skill
Cast
Charge
Release
Hit
Knockdown
GetUp
Death
Grab
Execution
Interaction
Transition
Unknown
```

### SemanticConfidence

```text
Verified
Probable
Unknown
```

Codex must never promote an inferred semantic name to `Verified` without an actual preview or reliable source mapping.

---

# 9. Naming strategy

Never replace original IDs.

Store both original and friendly identifiers.

Example:

```text
Original:
a000_003039.hkx

Database:
AnimationID = a000_003039

Friendly export:
ERREF_Malenia_Katana_AttackLight_003039.fbx
```

If semantic meaning is unknown:

```text
ERREF_Malenia_Unknown_003039.fbx
```

Do **not** guess:

```text
Malenia_Super_Cool_Slash_03.fbx
```

unless confirmed.

---

# 10. Phase 0 — Environment discovery

## Objective

Determine what is already available before modifying anything.

## Codex tasks

1. Inspect repository root.
2. Detect OS.
3. Detect Python version.
4. Detect Blender installation/version.
5. Detect Unity project and Unity Editor version if present.
6. Detect WitchyBND/unpacker.
7. Detect DSAnimStudio.
8. Detect the chosen HKX Blender bridge/add-on.
9. Detect source directories/files.
10. Write:

```text
03_CATALOG/reports/environment.md
```

## Required report

Include:

```text
OS:
Python:
Blender:
Unity:
WitchyBND:
DSAnimStudio:
HKX Blender bridge:
PlayerRepo present:
ELDEN VINS present:
Vanilla ER present:
Blockers:
```

## Acceptance gate P0

PASS when:

- repository paths are known;
- no path was guessed;
- required directories exist;
- missing tools are explicitly listed.

Do not proceed to mass unpacking if this phase is incomplete.

---

# 11. Phase 1 — Source inventory

Implement:

```text
scripts/inventory_sources.py
```

## Input

```text
01_RAW/**
```

## Output

```text
03_CATALOG/manifests/raw_files.csv
03_CATALOG/manifests/raw_files.json
```

Each record must include:

```text
Source
RelativePath
Extension
SizeBytes
SHA256
ModifiedTime
```

The script must be:

- recursive;
- deterministic;
- safe to rerun;
- read-only against RAW.

## Acceptance gate P1

PASS when:

- every RAW file is represented;
- no RAW file was changed;
- duplicate hashes can be queried;
- counts per extension/source are reported.

---

# 12. Phase 2 — Controlled unpack

Implement:

```text
scripts/unpack_sources.py
```

## Behavior

Process only known container formats.

Conceptual flow:

```text
*.dcx
  ↓
decompress

*.anibnd / supported BND
  ↓
unpack

result
  ↓
02_UNPACKED/<source>/...
```

Preserve relative structure.

Generate:

```text
Database/pipeline_runs.csv
logs/unpack_<timestamp>.log
```

For every operation record:

```text
input hash
input path
tool/version
command
output path
status
duration
```

## Failure policy

If one archive fails:

- log it;
- mark it failed;
- continue only if failure is isolated;
- do not silently skip it.

---

# 13. Phase 3 — Index HKX and TAE

Implement:

```text
scripts/index_animations.py
```

Scan:

```text
01_RAW/
02_UNPACKED/
```

for:

```text
*.hkx
*.tae
*.anibnd
*.anibnd.dcx
```

Populate/update:

```text
Database/animations.csv
```

Initially it is acceptable for many semantic fields to be:

```text
Unknown
```

The purpose of this phase is identity and traceability, not perfect classification.

---

# 14. Phase 4 — Preview and semantic identification

Use DSAnimStudio where applicable.

For each pilot clip:

1. locate its source archive/character;
2. open in DSAnimStudio;
3. preview animation;
4. record source animation ID;
5. classify:
   - CharacterType
   - WeaponFamily
   - ActionType
6. set `SemanticConfidence`;
7. inspect associated TAE timeline;
8. record relevant event timing.

Do not manually identify thousands of clips before the rest of the pipeline is proven.

---

# 15. TAE metadata policy

HKX and TAE serve different roles:

```text
HKX = skeletal movement
TAE = action/event timeline
```

Preserve TAE references even if Unity will not consume them directly.

Useful normalized event classes:

```text
AttackWindowStart
AttackWindowEnd
CancelWindowStart
CancelWindowEnd
ChargeStart
Release
ProjectileSpawn
WeaponTrailStart
WeaponTrailEnd
Footstep
SFX
VFX
GrabSync
ExecutionImpact
RecoveryStart
RecoveryEnd
Unknown
```

Store raw source event identity as well as normalized meaning.

Do not destroy raw timing information during normalization.

---

# 16. Phase 5 — HKX → Blender pilot

This is a **hard gate**.

Do not process the full library.

Select 5–10 representative clips:

```text
1 idle
1 locomotion
1 light attack
1 heavy attack
1 root-motion lunge or dodge
1 turn
1 casting animation
1 boss animation if available
optional paired animation
```

## Validation dimensions

Each imported clip must be tested for:

### Pose integrity

PASS:

- no T-pose;
- no exploding bones;
- no obvious hierarchy corruption.

### Duration

PASS:

- duration matches source preview within expected technical tolerance.

### Frame timing

PASS:

- no systematic speedup/slowdown.

### Coordinate system

PASS:

- one deterministic transform converts source orientation to Blender/Unity orientation.

### Scale

PASS:

- source scale is stable between clips from the same skeleton family.

### Root translation

PASS:

- translation exists where expected;
- is not doubled;
- is not silently removed.

### Root yaw

PASS:

- root turning behaves correctly.

### Repeatability

PASS:

- re-importing the same source using the same profile gives the same result.

If any of these fail, the HKX bridge is not approved.

---

# 17. Skeleton profiles

Do not use one skeleton strategy for everything.

Create profiles under:

```text
config/skeleton_profiles/
```

Minimum:

```text
ER_Player.json
Humanoid_Boss.json
Large_Humanoid.json
Creature_Custom.json
```

Each profile should describe:

```text
profile id
source family
armature name
root bone
pelvis bone
major humanoid bones
weapon bones
scale convention
forward axis
up axis
Unity rig type
notes
```

---

# 18. Retarget maps

Create mappings under:

```text
config/retarget_maps/
```

Example conceptual map:

```json
{
  "source_profile": "ER_Player",
  "target_profile": "Unity_Master_Humanoid",
  "bones": {
    "Pelvis": "Hips",
    "Spine": "Spine",
    "Spine1": "Chest",
    "Neck": "Neck",
    "Head": "Head",
    "UpperArm_L": "LeftUpperArm",
    "Forearm_L": "LeftLowerArm",
    "Hand_L": "LeftHand",
    "UpperArm_R": "RightUpperArm",
    "Forearm_R": "RightLowerArm",
    "Hand_R": "RightHand"
  }
}
```

Actual source bone names must come from inspected rigs.

Do not fabricate mapping keys before inspection.

---

# 19. Retarget strategy

The unit of retarget configuration is a **skeleton family**, not an animation.

Correct:

```text
Malenia source skeleton
       ↓
one validated retarget profile
       ↓
all selected Malenia clips
```

Incorrect:

```text
clip 001 → manual fix
clip 002 → manual fix
clip 003 → manual fix
...
```

The latter cannot scale.

---

# 20. Master rigs

Recommended targets:

## Unity_Master_Humanoid

For:

- player;
- normal humanoid mobs;
- humanoid bosses where proportions are reasonable.

## Unity_Large_Humanoid

For:

- oversized humanoids where a separate retarget profile produces better reference quality.

## Unity_Generic_Creature

Not one universal rig.

Each creature family can use a Generic Unity rig where required.

---

# 21. Weapon and attachment bones

Do not delete non-Humanoid bones merely because Unity Humanoid does not require them.

Preserve when present:

```text
Weapon_R
Weapon_L
Shield
Bow
Arrow
Staff
WeaponSheath
WeaponBack
WeaponHip
```

Unity Humanoid can coexist with extra transform bones.

Validate:

- weapon grip;
- hand alignment;
- sheath transitions;
- bow hand relationship;
- staff orientation.

---

# 22. Root motion policy

Root motion is first-class metadata.

For every clip classify:

```text
Unknown
NoRootMotion
HasRootMotion
VerifiedRootMotion
BrokenRootMotion
```

For root-motion clips preserve/check:

```text
Root Translation X
Root Translation Y
Root Translation Z
Root Yaw
```

Recommended prototype usage:

```text
Locomotion:
controller-driven or motion-system driven

Combat attack / lunge / dodge / execution:
often root-motion driven
```

Never globally strip root motion during conversion.

---

# 23. Blender batch retarget

Implement:

```text
scripts/retarget_batch.py
```

Preferred architecture:

```text
Python launcher
  ↓
Blender headless
  ↓
load source armature/action
  ↓
load target rig
  ↓
apply retarget profile
  ↓
bake target action
  ↓
validate root
  ↓
export FBX
  ↓
write metadata/log
```

Pseudo-command:

```text
blender --background template.blend --python blender/retarget_job.py -- job.json
```

Do not hardcode this exact CLI until the local Blender/add-on workflow is confirmed.

---

# 24. Export contract

Every FBX export must have:

```text
LibraryID
SourceHash
PipelineVersion
SkeletonProfile
RetargetProfile
RootMotionStatus
ExportTimestamp
```

either embedded where practical or stored in adjacent metadata:

```text
ERREF_0001842.fbx
ERREF_0001842.json
```

Example:

```json
{
  "library_id": "ERREF_0001842",
  "source_hash": "...",
  "animation_id": "a000_003039",
  "skeleton_profile": "ER_Player",
  "retarget_profile": "ER_Player_to_Unity_Master",
  "root_motion": "VerifiedRootMotion",
  "pipeline_version": "0.1.0"
}
```

---

# 25. Paired animations

Treat these as synchronized groups:

- grabs;
- backstabs;
- ripostes;
- executions;
- throws;
- boss grabs.

Do not export them as unrelated files only.

Structure:

```text
07_FBX/Paired/Execution_001/
├─ attacker.fbx
├─ attacker.json
├─ victim.fbx
├─ victim.json
└─ pair.json
```

`pair.json` should include:

```text
PairGroupID
AttackerLibraryID
VictimLibraryID
RelativeRootTransform
SyncStart
ImpactTime
ReleaseTime
Notes
```

---

# 26. Magic and casting

Animation pipeline:

```text
skeletal cast HKX
     ↓
body animation
```

It does **not** automatically give Unity:

- spell projectile;
- particle VFX;
- trails;
- decals;
- damage logic.

Use TAE timing only as reference metadata.

Normalize important moments such as:

```text
Charge
SpawnProjectile
Release
Recover
```

Unity spell systems remain independent.

---

# 27. Unity destination layout

Recommended:

```text
Assets/
└─ AnimationLibrary/
   ├─ Player/
   │  ├─ Locomotion/
   │  ├─ Traversal/
   │  └─ Interaction/
   │
   ├─ Weapons/
   │  ├─ Sword1H/
   │  ├─ SwordShield/
   │  ├─ Greatsword/
   │  ├─ Katana/
   │  ├─ DualBlade/
   │  ├─ Twinblade/
   │  ├─ Spear/
   │  ├─ Halberd/
   │  ├─ Axe/
   │  ├─ Greataxe/
   │  ├─ Hammer/
   │  ├─ Fist/
   │  ├─ Claw/
   │  ├─ Bow/
   │  └─ Staff/
   │
   ├─ Magic/
   ├─ Defense/
   ├─ Reactions/
   ├─ Boss/
   └─ Executions/
```

Physical folder placement is secondary to metadata.

Do not rely on folders as the only catalog.

---

# 28. Unity import automation

Create an `AssetPostprocessor` or import pipeline.

It should read adjacent metadata and set:

```text
Animation Type = Humanoid or Generic
Avatar source
Loop Time
Root Transform Rotation options
Root Transform Position options
Apply Root Motion policy metadata
Asset labels
LibraryID
WeaponFamily
ActionType
Source
```

Do not make all FBX clips Humanoid automatically.

Creature rigs remain Generic where appropriate.

---

# 29. Unity Animation Browser

Build a custom Editor Window.

Minimum filters:

```text
Source
SourceCharacter
CharacterType
WeaponFamily
ActionType
RootMotionStatus
SkeletonProfile
RetargetStatus
QAStatus
```

Minimum UI:

```text
┌───────────────────────────────────────┐
│ Animation Reference Browser           │
├───────────────────────────────────────┤
│ Source        [All ▼]                 │
│ Character     [All ▼]                 │
│ Type          [Boss ▼]                │
│ Weapon        [Greatsword ▼]          │
│ Action        [AttackHeavy ▼]         │
│ Root Motion   [Verified ▼]            │
├───────────────────────────────────────┤
│ Search: [_________________________]   │
├───────────────────────────────────────┤
│ result list                           │
│                                       │
│ ERREF_00128 ...                       │
│ ERREF_00415 ...                       │
│ ERREF_00812 ...                       │
├───────────────────────────────────────┤
│              3D Preview               │
└───────────────────────────────────────┘
```

Desired actions:

- Play/Pause
- scrub timeline
- loop
- root-motion visualization
- switch test avatar
- show source metadata
- open source path
- copy LibraryID

---

# 30. Validation scripts

Implement:

```text
scripts/validate_conversion.py
scripts/validate_exports.py
```

Automatable checks should include when technically possible:

```text
file exists
file non-empty
expected animation count
duration > 0
bone count > minimum
target root exists
hips/pelvis exists
NaN transforms absent
extreme scale absent
root track detected
output hash
metadata exists
```

Visual QA remains required for representative clips.

---

# 31. Pipeline run ledger

Maintain:

```text
Database/pipeline_runs.csv
```

Schema:

```text
RunID
Timestamp
Phase
Tool
ToolVersion
InputCount
SuccessCount
FailureCount
ConfigHash
PipelineVersion
LogPath
Notes
```

Every batch operation must produce a row.

---

# 32. Required scripts

Codex should gradually implement these.

## `inventory_sources.py`

Read-only inventory + SHA-256.

## `unpack_sources.py`

Wrapper around the selected unpacker.

## `index_animations.py`

Index HKX/TAE and create/update records.

## `build_catalog.py`

Merge manifests and metadata.

## `convert_batch.py`

Run the approved HKX→Blender conversion only for selected LibraryIDs.

## `validate_conversion.py`

Technical validation of converted clips.

## `retarget_batch.py`

Prepare/run Blender batch jobs.

## `validate_exports.py`

Validate FBX + metadata.

## `generate_report.py`

Generate Markdown summary such as:

```text
Total animations
By source
By character
By weapon
By action
Unknown count
Converted count
Retargeted count
Unity-ready count
Failure count
```

---

# 33. Idempotency requirements

All scripts must be safe to rerun.

If:

```text
source hash unchanged
AND config hash unchanged
AND pipeline version unchanged
AND output already PASS
```

then skip processing unless `--force` is specified.

Avoid repeated expensive conversions.

---

# 34. Logging requirements

Use human-readable log files plus structured metadata.

Minimum per failure:

```text
LibraryID
source path
phase
tool
command/action
exception/error text
timestamp
```

Never swallow errors.

---

# 35. Batch size policy

Initial configuration:

```text
pilot_only = true
max_batch_size = 20
```

Do not increase until the pilot gate is explicitly passed.

Recommended progression:

```text
10 clips
   ↓ PASS
50 clips
   ↓ PASS
250 clips
   ↓ PASS
full selected category
```

Do not jump directly from 10 to the entire repository.

---

# 36. Pilot dataset

The first pilot should intentionally cover edge cases.

Recommended:

| Clip | Why |
|---|---|
| Idle | baseline skeleton |
| Walk/Run | cyclic locomotion |
| Light attack | normal combat |
| Heavy attack | stronger limb/root movement |
| Dodge/Lunge | root translation |
| Turn | root yaw |
| Cast | upper-body/hand timing |
| Humanoid boss attack | different proportions |
| Optional grab pair | synchronization |

Prefer PlayerRepo for the first 5–7 clips.

Use a boss clip only after player clips are stable.

---

# 37. Pilot acceptance criteria

The pilot is PASS only when all are true:

- [ ] RAW inputs unchanged.
- [ ] Every test clip has a metadata record.
- [ ] Original animation ID preserved.
- [ ] Source hash preserved.
- [ ] DSAnimStudio/source preview available or semantics remain explicitly Unknown.
- [ ] HKX import produces stable pose.
- [ ] Duration is correct.
- [ ] Animation speed is correct.
- [ ] Axis correction is deterministic.
- [ ] Scale correction is deterministic.
- [ ] Root translation is preserved when present.
- [ ] Root yaw is preserved when present.
- [ ] Retarget works for the main humanoid profile.
- [ ] FBX imports into Unity.
- [ ] Unity avatar plays without major corruption.
- [ ] Extra weapon bones are preserved where needed.
- [ ] Re-running the job gives reproducible output.
- [ ] Failed jobs are logged.
- [ ] Batch processing remains disabled until this checklist passes.

---

# 38. Definition of Done for one animation

An animation is `READY_REFERENCE` when:

```text
source identity known
source hash known
original ID known
semantic category assigned or explicitly Unknown
source skeleton profile assigned
conversion PASS
root motion status recorded
retarget PASS if needed
FBX export PASS
Unity import PASS
metadata present
searchable in Animation Browser
```

This does not mean production-ready quality.

---

# 39. Status states

Use explicit states.

## ConversionStatus

```text
NotStarted
Queued
Running
Pass
Failed
Blocked
```

## RetargetStatus

```text
NotRequired
NotStarted
Pass
Failed
NeedsManualReview
```

## UnityImportStatus

```text
NotStarted
Pass
Failed
```

## QAStatus

```text
Unreviewed
TechnicalPass
VisualPass
Rejected
```

---

# 40. Reporting

At the end of every phase, generate:

```text
03_CATALOG/reports/phase_<N>_status.md
```

The report must include:

```text
What was attempted
What changed
Files created
Counts
Failures
Known blockers
Next safe action
```

Codex should not report vague statements such as:

```text
Everything looks good.
```

Use measurable results.

---

# 41. Recommended implementation order

```text
Phase 0
Environment discovery
    ↓
Phase 1
RAW inventory
    ↓
Phase 2
Controlled unpack
    ↓
Phase 3
HKX/TAE index
    ↓
Phase 4
Preview + metadata pilot
    ↓
Phase 5
HKX → Blender pilot
    ↓
Phase 6
Skeleton profile
    ↓
Phase 7
Retarget pilot
    ↓
Phase 8
FBX export
    ↓
Phase 9
Unity import
    ↓
Phase 10
Animation Browser
    ↓
Phase 11
50-clip batch
    ↓
Phase 12
250-clip batch
    ↓
Phase 13
Category-scale processing
```

---

# 42. What Codex must not do autonomously

Unless explicitly instructed by the user, Codex must not:

- delete RAW data;
- overwrite source archives;
- rename source files;
- bulk-convert the entire library;
- download unrelated mod packs;
- modify a local Elden Ring installation;
- repack/install mods into the game;
- assume an unknown clip's meaning;
- discard TAE metadata;
- collapse all skeletons into one rig;
- strip all root motion;
- create hand-authored fixes for hundreds of clips before identifying a systematic mapping issue.

---

# 43. Troubleshooting decision tree

## Animation becomes T-pose

Check in order:

```text
wrong source skeleton?
↓
unsupported HKX variant?
↓
armature hierarchy mismatch?
↓
retarget attempted without source rig?
↓
wrong action binding?
```

Do not solve a skeleton mismatch by merely renaming the HKX file.

---

## Character moves twice

Likely:

```text
animation root translation
+
Unity/controller displacement
```

Choose one authority for that action.

---

## Character does not move during a lunge

Check:

```text
root track imported?
root transform baked?
root motion stripped?
Unity root settings baking into pose?
```

---

## Animation rotated 90° or 180°

Fix in one shared source/export profile.

Do not rotate each FBX manually.

---

## Hands do not match weapon

Check:

```text
source weapon bone
weapon socket
retargeted hand rotations
weapon model pivot
IK/post-process layer
```

---

## Boss clip explodes on player skeleton

Use a source boss skeleton and retarget mapping.

Do not treat raw boss HKX as player-compatible unless the repository specifically provides it already retargeted.

---

# 44. Suggested code quality rules

For Python:

- Python 3.11+ if compatible with local tooling;
- `pathlib`, not string path concatenation;
- type hints;
- structured dataclasses/models for records;
- clear CLI using `argparse` or equivalent;
- dry-run mode for destructive/expensive operations;
- deterministic output;
- unit tests for parsers and manifests.

For Unity C#:

- separate Editor tooling from runtime code;
- use `ScriptableObject` or generated catalog assets for indexed metadata;
- do not scan the entire filesystem every Editor repaint;
- cache catalog data;
- support incremental refresh.

---

# 45. Git policy

Recommended `.gitignore`:

```gitignore
01_RAW/
02_UNPACKED/
04_CONVERTED/
05_RETARGETED/
06_CLEAN/
07_FBX/
logs/*.log

# Keep manifests/config/code
!03_CATALOG/manifests/
!Database/
!config/
!scripts/
!tests/
```

Adjust depending on repository size and whether FBX references should be versioned.

Do not commit large source-derived files unless intentionally using Git LFS and the user has approved it.

---

# 46. First implementation milestone

The first meaningful milestone is **not** “all animations imported”.

It is:

```text
10 representative animations
        ↓
fully traceable
        ↓
previewed
        ↓
converted
        ↓
retargeted where required
        ↓
root motion verified
        ↓
FBX exported
        ↓
Unity imported
        ↓
searchable in a minimal Animation Browser
```

Only after that milestone passes should the project scale.

---

# 47. Deliverables Codex should create for the first run

After executing Phase 0 + Phase 1, expected deliverables:

```text
README.md
config/pipeline.json
00_Tools/README.md
03_CATALOG/manifests/raw_files.csv
03_CATALOG/manifests/raw_files.json
03_CATALOG/reports/environment.md
03_CATALOG/reports/phase_1_status.md
Database/pipeline_runs.csv
scripts/inventory_sources.py
tests/...
```

The final Codex response should state:

```text
1. What was created
2. What sources were found
3. What tools were detected
4. What tools are missing
5. File counts by source/type
6. Any blockers
7. Exact next recommended phase
```

---

# 48. Suggested README summary

Codex may generate a short root `README.md` containing:

```text
# RPG Animation Reference Library

Internal prototype pipeline for cataloging and retargeting FromSoftware-compatible animation references into Unity.

Primary pipeline specification:
CODEX_FromSoftware_Animation_Pipeline.md

Do not modify 01_RAW manually.

Start:
python scripts/inventory_sources.py --config config/pipeline.json

Current status:
See 03_CATALOG/reports/
```

---

# 49. Final architectural model

```text
                       SOURCES
                          │
            ┌─────────────┼─────────────┐
            │             │             │
       Player Repo    ELDEN VINS    Vanilla ER
            │             │             │
            └─────────────┼─────────────┘
                          ↓
                   Immutable RAW
                          ↓
                    Inventory/Hash
                          ↓
                    Unpack DCX/BND
                          ↓
                    HKX + TAE + rigs
                          ↓
              ┌───────────┴───────────┐
              ↓                       ↓
        DSAnimStudio             Animation DB
      Preview / TAE                Metadata
              │                       │
              └───────────┬───────────┘
                          ↓
                    HKX → Blender
                          ↓
                  Source Skeleton
                          ↓
                  Retarget Profile
                          ↓
                  Master/Target Rig
                          ↓
                   Root Motion QA
                          ↓
                      FBX + JSON
                          ↓
                         Unity
                          ↓
                 Import Automation
                          ↓
                 Animation Browser
                          ↓
          ┌───────────────┼───────────────┐
          ↓               ↓               ↓
        PLAYER           MOBS            BOSSES
```

---

# 50. Immediate instruction to Codex

Start here:

```text
Execute only Phase 0 and Phase 1.

1. Inspect the current repository and machine.
2. Create the directory structure if missing.
3. Create config/pipeline.json without inventing tool paths.
4. Implement scripts/inventory_sources.py.
5. Inventory 01_RAW read-only.
6. Calculate SHA-256 for each source file.
7. Generate raw_files.csv and raw_files.json.
8. Generate environment.md and phase_1_status.md.
9. Add tests for inventory behavior.
10. Do not unpack or convert anything yet.
11. Report exact blockers and next actions.
```

Once Phase 0 + Phase 1 pass, proceed to the controlled unpack/index phase in a separate Codex task.
