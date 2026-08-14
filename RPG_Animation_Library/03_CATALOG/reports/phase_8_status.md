# Phase 8 Unity FBX export pilot

Timestamp: `2026-08-14T19:10:39.545261Z`

Mechanical gate result: **PASS**.

| Animation | FBX bytes | Frames | Animated bones | Root motion | Export | Blender reimport |
|---|---:|---:|---:|---|---|---|
| `a000_019270` | 639116 | 65 | 22/22 | RootMotionPreservedScaled | PASS | PASS |
| `a000_020000` | 546140 | 41 | 22/22 | RootMotionPreservedScaled | PASS | PASS |
| `a000_040590` | 726908 | 87 | 22/22 | RootMotionPreservedScaled | PASS | PASS |
| `a000_041150` | 675116 | 73 | 22/22 | RootMotionPreservedScaled | PASS | PASS |
| `a000_080000` | 1058236 | 181 | 22/22 | InPlace | PASS | PASS |

Animation-only FBX files and adjacent `.animlib.json` contracts are under ignored `07_FBX/phase_8`.
The copied Humanoid Avatar is normalized through the same Blender exporter at `Assets/_Game/Models/AnimationSources/CLazy/ProjectS_CLazy_AnimationLibraryAvatar.fbx`.
Every contract carries LibraryID, source hash, pinned pipeline version, skeleton/retarget profiles, root-motion policy and export timestamp.
The export contains only the target armature/action; source rigs, meshes and materials are excluded.
Blender reimport validates armature count, mapped bones, non-constant pose motion, frame timing and root trajectory before Unity receives the files.
