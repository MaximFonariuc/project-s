# Phase 5 HKX to Blender pilot

Timestamp: `2026-08-14T15:59:29.476696Z`

Mechanical gate result: **PASS**.

Bridge approval: **limited to the tested Elden Ring `c0000/a00_hi` standard animation variant**. Full-library conversion remains prohibited.

## Pilot

| Animation | Frames | Pose | Duration | Root translation | Reopen | Repeatability |
|---|---:|---|---|---|---|---|
| `a000_019270` | 65 | PASS | PASS | PASS | PASS | PASS |
| `a000_020000` | 41 | PASS | PASS | PASS | PASS | NOT_SAMPLED |
| `a000_040590` | 87 | PASS | PASS | PASS | PASS | NOT_SAMPLED |
| `a000_041150` | 73 | PASS | PASS | PASS | PASS | NOT_SAMPLED |
| `a000_080000` | 181 | PASS | PASS | PASS | PASS | NOT_SAMPLED |

## Gate evidence

- Pose integrity: finite transforms, required body bones, minimum vertical span, and bounded distance from pelvis.
- Duration and frame timing: frame count/FPS match the Phase 4 evidence within one source frame.
- Coordinate system: artifacts reopen as `BlenderZUp_SourceNative`; no hidden Unity-axis transform is applied in Phase 5.
- Scale: the rest-skeleton height spread across all clips is `0.000000000` Blender units.
- Root translation: end-to-start root displacement matches the Phase 4 sampled evidence.
- Root yaw: finite normalized yaw delta is preserved and recorded per clip.
- Serialization: every `.blend` was reopened in a fresh Blender process and produced the same motion fingerprint.
- Repeatability: `a000_019270` was imported twice and produced an identical motion fingerprint.

## Generated artifacts

Native `.blend`, conversion JSON, and reopen-validation JSON files are under ignored `04_CONVERTED/phase_5`.
Their paths, sizes, SHA-256 hashes, fingerprints, and gate results are versioned in `Database/conversion_operations.csv` and `Database/phase_5_state.json`.

## Coverage limitations

- The pilot covers locomotion-like motion, dodge/root motion, two attacks, and one long unclassified motion.
- It does not yet cover a confirmed idle, turn-in-place, cast, boss rig, creature rig, or paired animation.
- Semantic confidence is independent from mechanical conversion success. Probable/Unknown clips are not approved for gameplay merely because their HKX imported correctly.
- Phase 4 already found two HKX entries that the active Havok registry cannot parse. This gate does not generalize to those variants.
- On this host Blender may fault in Intel `igxelpicd64.dll` during shutdown after writing complete outputs. A non-zero exit is accepted only after both `.blend` and metadata exist; the `.blend` must then pass a clean reopen validation.

## Next safe action

Create the explicit `ER_Player` skeleton profile from the measured source rig, then retarget only this pilot subset to the Project S target skeleton. Do not export the full library or mark the bridge globally approved.

Execution log: `logs/conversion_20260814T155929Z_b7e13817.log` (ignored by Git).
