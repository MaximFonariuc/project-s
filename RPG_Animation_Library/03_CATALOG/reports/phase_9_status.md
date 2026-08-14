# Phase 9 Unity import pilot

Timestamp: `2026-08-14T18:34:12.7388946Z`

Unity `6000.0.80f1` import gate: **PASS** (5 clips).

| Animation | Unity asset | Clips | Root motion | Avatar | Import |
|---|---|---:|---|---|---|
| `a000_019270` | `Assets/_Game/Animations/Imported/FromSoftwarePilot/Dodge/Player_Dodge_ForwardRoll__RPGREF_6139DF2ED3414FD6A29783C4.fbx` | 1 | RootMotionPreservedScaled | CopyFromOther | PASS |
| `a000_020000` | `Assets/_Game/Animations/Imported/FromSoftwarePilot/Locomotion/Player_Locomotion_ForwardStep__RPGREF_7C4D02DD2CF66D4C4B1425CC.fbx` | 1 | RootMotionPreservedScaled | CopyFromOther | PASS |
| `a000_040590` | `Assets/_Game/Animations/Imported/FromSoftwarePilot/Attack/Player_Attack_ForwardLeapingAttack__RPGREF_7BF0E1BA1FB96046803D49F9.fbx` | 1 | RootMotionPreservedScaled | CopyFromOther | PASS |
| `a000_041150` | `Assets/_Game/Animations/Imported/FromSoftwarePilot/Attack/Player_Attack_RisingStrike__RPGREF_AD2D98A037757115F711E21D.fbx` | 1 | RootMotionPreservedScaled | CopyFromOther | PASS |
| `a000_080000` | `Assets/_Game/Animations/Imported/FromSoftwarePilot/Unknown/Player_Unknown_Unknown__RPGREF_0F567AEF0CF5F974E6D6AEE4.fbx` | 1 | InPlace | CopyFromOther | PASS |

The Editor importer verifies the adjacent contract and FBX SHA-256 before applying settings.
All assets use Humanoid Copy From Other Avatar with `Assets/_Game/Models/AnimationSources/CLazy/TPose.FBX`.
Clip settings, labels, frame ranges, root-motion policy and imported clip lengths were verified in Unity.
This is a bounded import/retarget pilot, not semantic approval of every source animation.
