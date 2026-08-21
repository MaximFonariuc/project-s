# KurtzPel animation audit and runtime selection

## Complete audit boundary

The imported reference pack contains 437 uniquely named Generic clips in 13
FBX packs:

| Group | Clips | Runtime status |
| --- | ---: | --- |
| Movement and character states | 336 | fully measured; selected subset used |
| Reactions | 26 | fully measured; runtime mapping deferred |
| Miscellaneous | 4 | fully measured; not runtime gameplay content |
| Blazing Fist combat | 71 | fully measured; first light attack selected |

Two independent checks are available:

- `KurtzPelReferenceAnimationImporter` cross-checks all clips against
  `KurtzPel_AnimationManifest.csv`, validates unique names, timing metadata,
  curves, shared Avatar and importer policy.
- `KurtzPelAnimationQualityAudit` samples every clip at 32 normalized positions
  on the shared rig and records length, FPS, loop policy, curve/transform count,
  authored root/motion curves, start/end pose RMS, root/pelvis travel and foot
  contact phases.

Run the second check through
`Tools > Animation > KurtzPel > Run Full Quality Audit`. Its complete row-level
output is `KurtzPelAnimationQuality.csv`; the generated summary is
`KurtzPelAnimationQuality.md`.

## Measured result

- Total clips: `437`.
- Clips with authored root/motion curves: `0`.
- Clips requiring review flags: `18`.
- Loop-pose discontinuities after importer correction: `0`.

The remaining flags are not import failures:

- 15 directional/special walk clips have ambiguous contact phases under a
  simple minimum-foot-height heuristic. None is used by the current forward
  locomotion tree.
- `CM_C_Recovery` and `CC_Eltheca_Intro` are deliberately long clips.
- `GT_CM_C_Skill_HellPunch_Ready_Stop` is a deliberate one-frame hold pose.

The audit initially detected `CC_Ninja_CombatWait_Wait` as a discontinuous
loop. Its name ends in `Wait` but the motion is finite, so the importer now
classifies this exact clip as non-looping. Reimport removed the false loop
without editing source curves.

## Timing and transition correction

All source PSA sequences are resampled to a common 60 FPS timeline before the
group FBXs are written. Source duration is preserved within one sample.

The selected locomotion clips have different natural contact phases:

| Clip | Left contact | Right contact | Controller cycle offset |
| --- | ---: | ---: | ---: |
| `HM_ENFJ_Walk` | 0.1875 | 0.6875 | 0.00 |
| `HM_ENFJ_Run` | 0.5000 | 0.03125 | 0.32 |
| `HM_ENFJ_Run_Fast` | 0.65625 | 0.1875 | 0.47 |

`PlayerKurtzPel.controller` uses those offsets and keeps Idle outside the
moving Blend Tree. This prevents both arbitrary locomotion entry phase and the
visible foot-contact jump that occurred when the three cycles were blended
without phase alignment.

Dodge is the deliberate speed exception: state speed is fitted to the
motor-owned `0.48 s` window. Combat duration remains gameplay data and never
depends on clip length.

## Selected runtime clips

| State | Clip | Source group |
| --- | --- | --- |
| Idle | `HM_ENFJ_Wait` | Male ENFJ |
| Walk | `HM_ENFJ_Walk` | Male ENFJ |
| Run | `HM_ENFJ_Run` | Male ENFJ |
| Sprint | `HM_ENFJ_Run_Fast` | Male ENFJ |
| Jump anticipation/rise | `HM_ENFJ_Air_JumpUp` | Male ENFJ |
| Fall | `HM_ENFJ_Air_JumpDown` | Male ENFJ |
| Landing | `HM_ENFJ_Air_JumpLanding` | Male ENFJ |
| Forward dodge | `HM_ENFJ_Dash` | Male ENFJ |
| Backward dodge | `CC_C_Rage_Avoid` | Common Ninja |
| Unarmed light 1 | `HMGT_BasicCombo_01` | Blazing Fist |

Swimming and ladder traversal are intentionally unmapped.

## UMotion and source-edit policy

UMotion is available for genuinely defective source curves, contact correction
that cannot be solved by phase alignment, or authored additive/weapon layers.
It is not used as an automatic destructive post-process. The current selected
clips have valid timing, complete transform curves and no loop discontinuity;
their observed problems were controller structure, phase alignment and one
incorrect importer loop classification. Those were fixed at the responsible
boundaries, so no duplicate edited clips were created.

Any future UMotion correction must retain the untouched reference clip, write a
separate derived production clip and document exactly which transforms/frames
changed.

## Generic versus Humanoid

The current reference model and all clips share the same Generic skeleton, so
Generic preserves exact poses and extra weapon/twist bones. A temporary import
probe produced a valid Humanoid Avatar with 52 mapped human bones, confirming
that later migration is viable.

Conversion should occur with the real production Humanoid model, not before.
The runtime animation contract is rig-independent, so that migration replaces
content without changing input, gameplay state, combat timelines or motor
authority.
