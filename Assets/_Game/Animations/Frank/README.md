# Frank Humanoid animation library

The Frank body-animation library is imported as Unity Humanoid motion and is
organized by source weapon set, motion policy, and gameplay category.

## Imported sets

| Set | Clips |
| --- | ---: |
| Assassin / Ninja | 132 |
| Damage reactions | 173 |
| Dual swords | 148 |
| Stealth kills | 53 |
| Sword Two (female) | 175 |
| Two-handed axe | 98 |
| Two-handed greatsword | 75 |
| Two-handed katana | 149 |
| Two-handed spear | 84 |
| Warrior (sword and shield) | 191 |
| Whip (female) | 96 |
| **Complete library** | **1374** |

The rebuild pipeline now owns all 1374 clips, including the original 148-clip
`DualSwords` set.

## Layout

Newer sets use `Animations/InPlace/<Category>` and
`Animations/RootMotion/<Category>`. `DualSwords` retains its existing flat
`Animations/InPlace` and `Animations/RootMotion` paths so Unity references stay
stable. Categories describe intended gameplay use,
for example `Locomotion`, `Attacks`, `Skills`, `Defense`, `Evasion`, `Air`,
`Reactions`, and `Death`. A generated JSON catalog beside each set preserves the
source package, duration data, bone coverage, and conversion warnings.

Frank is not one skeleton. The source library contains multiple distinct bind
poses, so applying every PSA to `FrankDualRig` is invalid. The converter now
reconstructs a compact Humanoid body rig from the bind transforms embedded in
each PSA. A clean, animation-free Avatar is generated in `Rig/Source` for every
unique skeleton fingerprint, and each catalog records the matching
`rigAssetPath`. Unity imports animation FBXs with `Copy From Other Avatar` using
that exact bind rig. Creating an Avatar from an animated FBX is forbidden: it
produces a different Humanoid solve and can rotate feet and other limbs away
from the authored pose.

The converter retains authored rotation, translation, and (when present) scale
tracks for every mapped body bone. It rejects non-finite locations and invalid
quaternions instead of trying to repair them. No pose key is clamped, smoothed,
interpolated, or replaced. `q/-q` quaternion signs may be normalized because
they encode exactly the same rotation and independent linear FBX curves would
otherwise take the long interpolation path. The complete integrity summary is
stored in `FrankSourceKeyAuditReport.json`; the expected source-transform
mutation count is zero.

Some UE4.23 assets are stored at unusual source sample rates (around 780 or
1140 FPS). They are quaternion/linear resampled to a maximum of 60 FPS while
preserving the source duration. This is a rate conversion, not a pose-key repair;
the source and output rates, frame counts, and duration are recorded per clip in
the generated catalogs.

Unity's Humanoid conversion produced 11 isolated muscle-space discontinuities
in seven clips even though their source bone rotations were smooth. Those exact
imported muscle keys are replaced by interpolation between their untouched
neighbors in `FrankHumanoidRetargetPostprocessor`; no broad spike heuristic and
no source-transform mutation is allowed. The validator and the `Render Reported
Pose Spikes` contact-sheet command provide the before/after QA boundary for this
explicit correction list.

The PSA source can encode the same rotation as either `q` or `-q`. Because the
pinned Blender importer writes quaternion components to independent linear
curves, every bone track is normalized to one interpolation hemisphere before
export. This preserves authored poses while preventing long-path spins between
equivalent keys. Unity imports the resulting fast Humanoid motion with X4
oversampling; X1/X2 proved insufficient for the fastest leg rotations during
the diagnostic A/B pass.

Mannequin duplicates and weapon-only skeletal animation tracks are not body
Humanoid clips and are deliberately excluded. Weapon meshes and their own
animation tracks should be imported as a separate equipment task.

## Rebuild and validation

Run the local source conversion from the repository root:

```powershell
& 'Assets/_App/Core/Editor/Animation/FrankImport/ImportFrankAnimations.ps1'
```

Then use these Unity menu commands:

- `Project S/Animation/Frank/Configure Humanoid Imports`
- `Project S/Animation/Frank/Validate Humanoid Library`
- `Project S/Animation/Frank/Render Reported Pose Spikes`

Validation output is written to `FrankHumanoidImportReport.json`. In addition
to the Humanoid Avatar/import contract, the validator scans every imported
curve key for invalid numeric/time data. Conservative muscle-curve candidates
are then sampled on the neutral `DefaultMale` Humanoid; the report separates
curve-only candidates from confirmed 3D bone-pose discontinuities.

The generated FBX files, `.meta` files, import tooling, catalogs, documentation,
and validation reports are versioned. The original Unreal source archives under
`Reference/FrankAnim` remain local-only and ignored by Git.
