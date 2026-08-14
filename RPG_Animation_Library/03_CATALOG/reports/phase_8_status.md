# Phase 8 Unity FBX export pilot

Timestamp: `2026-08-14T18:31:19.516235Z`

Mechanical gate result: **PASS**.

| Animation | FBX bytes | Frames | Root motion | Export | Blender reimport |
|---|---:|---:|---|---|---|
| `a000_019270` | 571260 | 65 | RootMotionPreservedScaled | PASS | PASS |
| `a000_020000` | 500348 | 41 | RootMotionPreservedScaled | PASS | PASS |
| `a000_040590` | 641260 | 87 | RootMotionPreservedScaled | PASS | PASS |
| `a000_041150` | 603148 | 73 | RootMotionPreservedScaled | PASS | PASS |
| `a000_080000` | 908284 | 181 | InPlace | PASS | PASS |

Animation-only FBX files and adjacent `.animlib.json` contracts are under ignored `07_FBX/phase_8`.
Every contract carries LibraryID, source hash, pinned pipeline version, skeleton/retarget profiles, root-motion policy and export timestamp.
The export contains only the target armature/action; source rigs, meshes and materials are excluded.
Blender reimport validates armature count, mapped bones, frame timing and root trajectory before Unity receives the files.
