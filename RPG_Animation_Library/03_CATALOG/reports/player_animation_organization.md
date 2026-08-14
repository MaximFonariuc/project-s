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
│   └── Mounted/TAE_MountControl/               # 13 clips
└── 90_NeedsReview/
    └── ID_Banks/<NNNxxx>/             # 584 clips without reliable semantics
```

## Classification policy

1. Preserve the four curated semantic records and their confidence.
2. Use only explicit TAE discriminators: attack window, projectile spawn, dodge flag, goods consumption, mount control or death/despawn flags.
3. Do not infer a gameplay action from animation ID ranges alone. Unresolved clips stay `NeedsReview` and are grouped into deterministic ID banks.

An evidence-only folder identifies a technical TAE event, not a final gameplay-ready name. For example, `TAE_DodgeFlag` proves that Elden Ring marks an interval as dodging, but it does not prove direction, weapon family or the intended Project S move.

Filename format:

```text
<AnimationID>__<ConservativeType>__<LibraryID>.fbx
```

Examples:

- `a000_019270__Dodge_ForwardRoll__RPGREF_....fbx`
- `a000_060500__Dodge_TAE__RPGREF_....fbx`
- `a000_013000__NeedsReview__RPGREF_....fbx`

The adjacent `.animlib.json` contains `CatalogType`, `ClassificationSource`, `Evidence`, `ReviewStatus` and `IDBank`. The same values are indexed in `Database/player_animation_catalog.csv` for filtering outside Unity.

## Rebuild

```powershell
py -3 RPG_Animation_Library/scripts/organize_player_animation_catalog.py `
  --config RPG_Animation_Library/config/pipeline.json
```

Add `--apply-phase8` to synchronize generated Phase 8 artifacts. After running the Unity menu `Tools/Project S/Animation Library/Organize Player Animations`, use `--sync-phase9` to update the staging manifest, then run the Phase 9 validator and finalizer.

Latest result: organization receipt **PASS**, Unity Phase 9 import validation **984/984 PASS**.
