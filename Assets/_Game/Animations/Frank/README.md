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

Validation output is written to `FrankHumanoidImportReport.json`.

The generated FBX files and their `.meta` files are intentionally local-only and
ignored by Git. Import tooling, catalogs, this documentation, and the validation
report remain versioned so another authorized local source copy can reproduce
the library.
