# Frank Humanoid animation library

The Frank body-animation library is imported as Unity Humanoid motion and is
organized by source weapon set, motion policy, and gameplay category.

## Imported sets

| Set | New clips |
| --- | ---: |
| Assassin / Ninja | 132 |
| Damage reactions | 173 |
| Stealth kills | 53 |
| Sword Two (female) | 175 |
| Two-handed axe | 98 |
| Two-handed greatsword | 75 |
| Two-handed katana | 149 |
| Two-handed spear | 84 |
| Warrior (sword and shield) | 191 |
| Whip (female) | 96 |
| **Newly imported** | **1226** |

`DualSwords` remains the previously imported 148-clip Humanoid set. The complete
library therefore contains 1374 validated body-animation clips.

## Layout

Each set uses `Animations/InPlace/<Category>` and
`Animations/RootMotion/<Category>`. Categories describe intended gameplay use,
for example `Locomotion`, `Attacks`, `Skills`, `Defense`, `Evasion`, `Air`,
`Reactions`, and `Death`. A generated JSON catalog beside each set preserves the
source package, duration data, bone coverage, and conversion warnings.

All newly generated clips use the valid shared Humanoid Avatar at
`Rig/FrankHumanoidAnimationRig.fbx`. The converter retains rotation tracks for
the mapped body, retains `root` and `pelvis` translation for body/root motion,
removes incompatible child-bone translation and scale tracks, and resamples
source data to at most 60 FPS without changing clip duration.

Before retargeting, every mapped Humanoid body rotation track is checked for
non-finite/zero quaternions and one-frame outliers. A single key is repaired when
it jumps by more than 35 degrees on both sides while its two neighbor poses
differ by less than 20 degrees. A second conservative rule catches coherent
multi-bone corruption when at least two core bones jump by 20 degrees and their
neighbors remain within 5 degrees. If three or more core bones prove that an
entire frame is corrupt, only body tracks that differ materially from their
neighbor interpolation are repaired. The replacement is quaternion slerp
between untouched neighbors. Sustained fast attacks remain unchanged. Every
repair is stored in `FrankSourceKeyAuditReport.json` with its classification,
bone, source frame, time, and measured angles. A complete rebuild also mirrors
the same `sourceKeyCorrections` data into each generated pack catalog.

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

Validation output is written to `FrankHumanoidImportReport.json`. In addition
to the Humanoid Avatar/import contract, the validator scans every imported
curve key for invalid numeric/time data. Conservative muscle-curve candidates
are then sampled on the neutral `DefaultMale` Humanoid; the report separates
curve-only candidates from confirmed 3D bone-pose discontinuities.

The generated FBX files and their `.meta` files are intentionally local-only and
ignored by Git. Import tooling, catalogs, this documentation, and the validation
report remain versioned so another authorized local source copy can reproduce
the library.
