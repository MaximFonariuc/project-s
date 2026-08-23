# Frank Dual Swords

Source: `Reference/Frank/Frank_Dual_Sword`.

This directory contains the high-fidelity Unity port of the dual-sword animation
set. The master assets intentionally use a **Generic** rig. Frank has 94 bones,
including animated weapon, segmented-sword and secondary chains that Unity
discards when the same FBX is imported directly as Humanoid.

## Contents

- `Rig/FrankDualRig.fbx` — stable reference rig and shared Generic Avatar;
- `Animations/InPlace` — 54 clips intended for motor-driven displacement;
- `Animations/RootMotion` — 94 clips with authored root displacement;
- `FrankDualSwordAnimationCatalog.json` — frame count, duration and measured
  root delta for every clip.

All clips are sampled at 60 FPS and retain the complete 94-bone hierarchy.
Unity imports them through the Avatar from `FrankDualRig.fbx`. In-place and
root-motion FBXs remain separate so gameplay can select its movement policy
without modifying the source animation.

`InPlace` means planar displacement is removed by the source pack. Some air
clips still contain authored vertical displacement; their `ZeroHeight`
variants remove it as well.

## Humanoid retargeting

Do not change these master FBXs to Humanoid. Create derived retargeted clips for
Humanoid characters and keep weapon/secondary animation as an explicit
additional pass. This preserves the original source and avoids silently losing
the custom sword curves.

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
