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

All generated clips use the valid shared Humanoid Avatar at
`Rig/FrankHumanoidAnimationRig.fbx`. The converter retains rotation tracks for
the mapped body, retains `root` and `pelvis` translation for body/root motion,
removes incompatible child-bone translation and scale tracks, and exports the
source frame sequence at at most 60 FPS without changing the frame count.

Before retargeting, every mapped Humanoid body rotation track is checked for
non-finite/zero quaternions and one-frame outliers. A single key is repaired when
it jumps by more than 35 degrees on both sides while its two neighbor poses
differ by less than 20 degrees. If three or more core bones independently prove
that an entire frame is corrupt, only body tracks that differ materially from
their neighbor interpolation are repaired. Ambiguous coordinated multi-bone
motion is preserved. The replacement is quaternion slerp between untouched
neighbors. Sustained fast attacks remain unchanged. Every
repair is stored in `FrankSourceKeyAuditReport.json` with its classification,
bone, source frame, time, and measured angles. A complete rebuild also mirrors
the same `sourceKeyCorrections` data into each generated pack catalog.

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

- `Project S/Animation/Frank/Prepare Legacy DualSwords Humanoid Metadata`
  (one time only for a checkout that still contains the old `FrankDualRig`
  `HumanDescription`; finish this command before running the next command)
- `Project S/Animation/Frank/Configure Humanoid Imports`
- `Project S/Animation/Frank/Validate Humanoid Library`

Validation output is written to `FrankHumanoidImportReport.json`. In addition
to the Humanoid Avatar/import contract, the validator scans every imported
curve key for invalid numeric/time data. Conservative muscle-curve candidates
are then sampled on the neutral `DefaultMale` Humanoid; the report separates
curve-only candidates from confirmed 3D bone-pose discontinuities.

The generated FBX files, `.meta` files, import tooling, catalogs, documentation,
and validation reports are versioned. The original Unreal source archives under
`Reference/Frank` remain local-only and ignored by Git.
