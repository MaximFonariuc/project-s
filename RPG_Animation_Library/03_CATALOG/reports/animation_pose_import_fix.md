# Animation pose import incident

Date: `2026-08-14`

## Symptom

All five Phase 9 clips produced only a small character-position change in Unity. The initial Unity gate reported PASS because it verified FBX identity, frame timing and root trajectory, but did not require non-constant body-pose curves.

## Root causes

1. `export_fbx_blender.py` switched the target armature to `REST` immediately before FBX baking. Blender therefore exported the root trajectory while every mapped body rotation remained constant.
2. After pose baking was restored, Unity rejected the original `TPose.FBX` as a copied Avatar reference for the Blender-generated animation-only FBX dialect. The hierarchy and rest dimensions were semantically the same, but Unity measured 9-11 mm reference-position differences on calves and feet.

## Correction

- FBX baking now evaluates the armature in `POSE` mode.
- A rest-pose prefix is exported at FBX frame `0`; the authored clip occupies frames `1..N`, and the Unity importer explicitly excludes the prefix.
- `ProjectS_CLazy_AnimationLibraryAvatar.fbx` is generated from the target T-pose through the same Blender FBX settings as the animation clips and is the sole copied Avatar for this import profile.
- The adjacent import contract pins both FBX and Avatar SHA-256 values.
- Blender validation now compares non-constant mapped-bone motion against Phase 7 evidence.
- Unity validation now requires non-constant pose curves and the exact authored clip range.

## Final evidence

| Clip | Blender animated bones | Unity non-constant pose curves | Sampled bones changed at midpoint | Midpoint rotation delta |
|---|---:|---:|---:|---:|
| `ForwardRoll` | 22/22 | 77 | 16/16 | 698.38 degrees |
| `ForwardStep` | 22/22 | 77 | 16/16 | 208.92 degrees |
| `ForwardLeapingAttack` | 22/22 | 77 | 16/16 | 571.35 degrees |
| `RisingStrike` | 22/22 | 77 | 16/16 | 786.98 degrees |
| `a000_080000` | 22/22 | 77 | 16/16 | 372.79 degrees |

Unity `6000.0.80f1` reports Phase 9 PASS for all five clips. The final import produced no compile errors, Console errors, warnings, or copied-Avatar rig mismatch messages.

This closes the mechanical pose-import defect. It does not promote `Probable` or `Unknown` semantic catalog labels to `Verified`.
