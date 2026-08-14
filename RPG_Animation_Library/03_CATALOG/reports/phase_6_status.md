# Phase 6 skeleton profiles

Timestamp: `2026-08-14T18:01:08.791626Z`

Mechanical gate result: **PASS**.

| Profile | Role | Bones | Height (m) | Required bones | Hierarchy | Measurement |
|---|---|---:|---:|---|---|---|
| `ER_Player` | Source | 488 | 2.358126640 | PASS | PASS | PASS |
| `ProjectS_CLazy_Player` | Target | 60 | 1.815035701 | PASS | PASS | PASS |

The family retarget map contains `52` explicit deform-bone mappings.

Root motion ownership is explicit: armature object translation/rotation is transferred once, while Pelvis supplies local pose height and body rotation.
Service, control, cloth, SFX and weapon bones are outside this pilot boundary.
The target profile points at the same Humanoid Avatar used by the current Player prefab.
