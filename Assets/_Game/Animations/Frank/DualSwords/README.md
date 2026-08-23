# Frank Dual Swords

Source: `Reference/Frank/Frank_Dual_Sword`.

This directory contains the Unity port of the dual-sword animation set. The
entire pack uses one **Humanoid** contract. `FrankDualRigAvatar` is the validated
shared Avatar; every animation FBX imports as Humanoid with `Copy From Other
Avatar`, so retargeting does not depend on a separately generated mapping per
clip.

## Contents

- `Rig/FrankDualRig.fbx` — stable reference rig and shared Humanoid Avatar;
- `Animations/InPlace` — 54 clips intended for motor-driven displacement;
- `Animations/RootMotion` — 94 clips with authored root displacement;
- `FrankDualSwordAnimationCatalog.json` — frame count, duration and measured
  root delta for every clip.

All clips are sampled at 60 FPS and import through the Avatar from
`FrankDualRig.fbx`. In-place and root-motion FBXs remain separate so gameplay
can select its movement policy without modifying the source animation.

`InPlace` means planar displacement is removed by the source pack. Some air
clips still contain authored vertical displacement; their `ZeroHeight`
variants remove it as well.

## Runtime slots

Stable Animator slots may use derived `.anim` assets with gameplay-facing names
such as `AirAttack1`. Those assets remain Humanoid and can be replaced by an
Animator Override Controller without exposing Frank filenames to gameplay code.
Weapon and secondary-bone animation still require an explicit presentation pass
when the target character does not contain the corresponding source bones.

## Mesh status

The character and weapon meshes in the source pack are UE4.21 editor-only
SkeletalMesh assets. UEViewer can read their skeletons and animations but fails
while decoding their editor mesh render data. Export these two assets to FBX
through a compatible Unreal Editor before adding them here:

- `Frank_Dual_Sword/Skeletal_Meshes/Frank_Dual.uasset`;
- `Frank_SlashPack_Weapons/Skeletal_Meshes/SK_Frank_Dual_Weapon.uasset`.

The exported meshes must keep the original bone names and bind pose so they can
reuse `FrankDualRigAvatar` and every clip in this directory.

The current pack includes one death animation (`Frank_RPG_Dual_Die`). Additional
death variants can be added later without changing the rig or current clip API.
