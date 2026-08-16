# Player animation catalog organization

The canonical Unity player library contains **984** technically validated full-body clips. Its browsable layout is generated from `Database/player_animation_catalog.json`; Unity moves assets through `AssetDatabase` so FBX and metadata GUIDs remain stable.

```text
Assets/_Game/Animations/Imported/FromSoftwarePlayer/
├── 01_Combat/
│   ├── Attacks/
│   │   ├── Reviewed/                 # 2 visually reviewed attacks
│   │   └── TAE_AttackWindow/         # 3 clips with AttackBehavior windows
│   └── Projectiles/
│       └── TAE_ProjectileSpawn/      # 5 clips with BulletBehavior events
├── 02_Defense/
│   └── Dodges/
│       ├── Reviewed/                 # 1 reviewed forward roll
│       └── TAE_DodgeFlag/            # 127 additional FlagType=8 clips
├── 03_Movement/
│   └── Locomotion/Reviewed/          # 1 reviewed forward step
├── 04_Items/
│   └── ConsumeGoods/TAE_ConsumeCurrentGoods/  # 110 clips
├── 05_Reactions/
│   └── Death_Despawn/TAE_DeathFlags/          # 138 clips
├── 06_Interactions/
│   ├── Mounted/
│   │   ├── TAE_MountControl/                   # 13 clips
│   │   └── TAE_RideTurning/                    # 49 clips
│   └── Ladder/TAE_LadderCollision/             # 35 clips
├── 07_Equipment/
│   └── WeaponStyleSwitch/TAE_WeaponEvents/     # 24 clips
├── 08_Magic/
│   └── Cast/TAE_CastHighlightedMagic/          # 2 clips
└── 90_NeedsReview/
    ├── 01_ByWeapon/                             # 42 exclusive behavior matches
    │   ├── Axe/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    │   ├── Bow/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    │   ├── DuelingShield/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    │   ├── LightGreatsword/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    │   ├── Spear/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    │   └── Torch/<InPlace|RootMotion>/ID_Banks/<NNNxxx>/
    ├── 02_FootstepCandidates/ID_Banks/<NNNxxx>/ # 57 clips
    ├── 03_RootMotion/ID_Banks/<NNNxxx>/         # 138 clips
    ├── 04_InPlace/ID_Banks/<NNNxxx>/            # 225 clips
    └── 05_MissingTAE/ID_Banks/<NNNxxx>/         # 12 clips
```

## Classification policy

1. Preserve the four curated semantic records and their confidence.
2. Use only explicit TAE discriminators: attack window, projectile spawn, dodge flag, goods consumption, mount/ride control, ladder collision, weapon configuration, magic cast or death/despawn flags.
3. For otherwise unresolved clips, accept a weapon family only when every matching `CustomManualSelectorGenerator` name in `c0000.behbnd.dcx` contains the same explicit weapon token. Shared or mixed behavior names remain unclassified.
4. Treat a footstep event only as a motion candidate, not proof of locomotion.
5. Split unresolved clips by technical root-translation evidence into `RootMotion` and `InPlace`; missing TAE entries remain separately visible.
6. Do not infer a gameplay action or weapon from animation ID ranges alone. Unresolved clips stay `NeedsReview` and are grouped into deterministic ID banks.

An evidence-only folder identifies a technical source fact, not a final gameplay-ready name. For example, `TAE_DodgeFlag` proves that Elden Ring marks an interval as dodging, but it does not prove direction or the intended Project S move. A behavior weapon folder proves that the animation is selected exclusively by the named weapon family in the inspected player behavior graph; it still does not identify the exact attack or action.

The behavior graph maps **873 of 984** imported animation IDs to one or more selector names. The conservative intersection rule yields **61** exclusive weapon matches in the complete catalog: DuelingShield 25, Torch 18, Spear 8, Axe 4, LightGreatsword 4 and Bow 2. Nineteen of those clips already have stronger curated or TAE categories, so **42** are physically separated inside `90_NeedsReview/01_ByWeapon`. Seven shared/mixed cases are deliberately left unknown. `eventnameid.txt` describes behavior events but cannot establish this animation-to-weapon relation by itself.

Filename format:

```text
<AnimationID>__<ConservativeType>__<LibraryID>.fbx
```

Examples:

- `a000_019270__Dodge_ForwardRoll__RPGREF_....fbx`
- `a000_060500__Dodge_TAE__RPGREF_....fbx`
- `a000_013000__NeedsReview__RPGREF_....fbx`

The adjacent `.animlib.json` contains `CatalogType`, `ClassificationSource`, `Evidence`, `ReviewStatus`, `IDBank`, `WeaponFamily`, `WeaponClassificationSource` and `WeaponEvidence`. The same values are indexed in `Database/player_animation_catalog.csv` for filtering outside Unity. Raw behavior evidence is stored separately in `Database/player_animation_behavior_evidence.csv` and `.json`.

## Rebuild

```powershell
pwsh -NoProfile -File RPG_Animation_Library/scripts/extract_behavior_animation_evidence.ps1 `
  -Config RPG_Animation_Library/config/pipeline.json

py -3 RPG_Animation_Library/scripts/organize_player_animation_catalog.py `
  --config RPG_Animation_Library/config/pipeline.json
```

The extractor requires PowerShell 7 (`pwsh`) because the Havoc parser targets modern .NET. Add `--apply-phase8` to synchronize generated Phase 8 artifacts. After running the Unity menu `Tools/Project S/Animation Library/Organize Player Animations`, use `--sync-phase9` to update the staging manifest, then run the Phase 9 validator and finalizer.

Latest result: organization receipt **PASS**, Unity Phase 9 import validation **984/984 PASS**.
