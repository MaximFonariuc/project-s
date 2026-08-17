# PSO2 NGS reference animation import

This import is a local animation reference set for Project S. Extracted game assets are excluded from Git and must not be redistributed.

## Source and tooling

- Installed game: `D:\Games\SteamGames\steamapps\common\PHANTASYSTARONLINE2_NA_STEAM\pso2_bin`.
- Extractor/exporter: Aqua Toolset release `8_16_2026`, commit `72796fbee78ee3bcab2ca32c86ee097b07510c16`.
- Female NGS base body: `200000`, archive hash `39637db7d70f4d2b289baa62a37f9010`.
- Male NGS base body: `100000`, archive hash `0bd5b01bbc2d17ac77c4eed77a3934f7`.
- Common movement archive: `46079d9779358f7e8215c78f32ed92b7`.
- Dual Blade archive: `ed40b9261c1c6a06fe43d3418daba31b`.

Six common swim sources (`swim_st`, `swim_idle`, `swim_f`, `swim_b`, `swim_l`, and `swim_r`) contain an all-zero scale sentinel on `body_root`. Aqua exports that value literally, which collapses the animated hierarchy during the Blender bake. The local bake script therefore replaces only completely zero `body_root` scale curves with unit scale before retargeting. `swim_ed` does not contain the sentinel and is left unchanged.

## Local Unity layout

```text
Assets/_Game/Animations/Reference/PSO2NGS/
├── PSO2NGS_SourceRig_Female200000.fbx
├── PSO2NGS_SourceRig_Male100000.fbx
├── Female200000/
│   ├── Common/PSO2NGS_Common_Locomotion.fbx
│   └── DualBlade/
├── Male100000/
│   ├── Common/PSO2NGS_Common_Locomotion.fbx
│   └── DualBlade/
└── Preview/
    ├── PSO2NGS_ReferenceCharacter_Female200000.prefab
    └── PSO2NGS_ReferenceCharacter_Male100000.prefab
```

Each body variant contains 55 common locomotion clips, 15 Dual Blade locomotion clips, and 36 Dual Blade combat candidates: 106 clips per body, 212 local clips total.

The reference assets use Generic Avatars intentionally. This preserves all 205 skeleton bones, including twist and weapon helper animation that Unity Humanoid import discards. Conversion or retargeting to the Project S gameplay rig is a separate step after the source motion has been approved.

Run `Tools/Animation/PSO2 NGS/Configure Reference FBX` to reapply import settings and regenerate both preview prefabs.

Open `Tools/Animation/PSO2 NGS/Open Reference Preview`, choose a body and pack, then spawn a reference target to play or scrub clips directly in the Scene view without an Animator Controller.

## Initial Dual Blade shortlist

The direct normal-attack sequences are the first candidates to review visually before defining gameplay combo timing:

| Purpose | Clip | Source duration |
|---|---|---:|
| Ground 1 | `pl_std_rw631_atk_n1` | 0.9333 s |
| Ground 2 | `pl_std_rw631_atk_n2` | 1.0667 s |
| Ground 3 | `pl_std_rw631_atk_n3` | 1.1667 s |
| Air 1 | `pl_std_rw631_atk_a1` | 0.9333 s |
| Air 2 | `pl_std_rw631_atk_a2` | 1.0667 s |
| Air 3 | `pl_std_rw631_atk_a3` | 1.1667 s |

## Validation snapshot

- Both source models import with valid Generic Avatars.
- Female model: 10 skinned renderers and 218 transforms.
- Male model: 8 skinned renderers and 216 transforms.
- All six animation packs contain no meshes and have matching male/female clip names.
- 436,720 curves and 5,191,224 keys scanned: no non-finite values and no zero-length clips.
- 208 of 212 clips contain motion. The remaining four are the same two intentionally static Dual Blade pose fragments on both bodies: `pl_std_rwiz2_10320_jump_rise` and `pl_std_rwiz2_10340_jump_fall`.
- All seven swim clips contain animated curves. Sampling them at the first and middle frames changes 115-137 transforms on each reference prefab.
- `pl_std_rw631_atk_n1` sampled successfully on both preview prefabs at 0.467 seconds.
- Repeated Unity import completes without Console errors, assertions, or warnings.
