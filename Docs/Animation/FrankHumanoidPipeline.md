# Frank Humanoid animation pipeline

## Contract

Frank FBX files under `Assets/_Game/Animations/Frank/<Pack>/...` are source
assets. They are imported as `Generic` so Unity exposes the original bone
motion without passing it through the broken Frank Humanoid avatar.

Runtime-ready clips are generated under the mirrored path
`Assets/_Game/Animations/Frank/Humanoid/<Pack>/...`. These files are standalone
Humanoid `AnimationClip` assets and can be used by the project's Humanoid
characters and Animator Override Controllers.

The source FBX and the generated clip have a one-to-one path mapping:

```text
Frank/DualSwords/Animations/InPlace/Attack.fbx
    -> Frank/Humanoid/DualSwords/Animations/InPlace/Attack.anim
```

Gameplay and Animator controllers must reference the generated `.anim`, never
the Generic source FBX.

## Why direct Humanoid import is not used

The source transforms are coherent when sampled as Generic animation, but the
Frank reference avatar converts several ordinary limb rotations into invalid
Humanoid muscle values. In affected clips lower-leg twist exceeded the normal
Humanoid range and produced the visibly inverted knees and feet. Reusing that
avatar, changing `avatarSetup`, or repeatedly reimporting the same FBX cannot
repair the conversion.

`FrankHumanoidClipBaker` therefore samples the untouched Generic skeleton,
reconstructs the pose on the project's validated `DefaultMale` Humanoid avatar,
and writes the resulting HumanPose channels to a separate clip. This keeps the
source archive reversible and makes the playable representation deterministic.

## Pose conversion

For every source frame the baker:

1. restores the source and target rest poses;
2. samples the Generic Frank clip;
3. aligns the target body, limb and finger chains to the sampled joint
   directions;
4. reads the result through `HumanPoseHandler` on the validated target Avatar;
5. scales pelvis translation by the source/target body-height ratio;
6. replaces non-finite values, clamps every muscle to `[-1, 1]`, preserves
   quaternion continuity and limits an isolated muscle change to 18 units per
   second;
7. reduces redundant linear keys and writes Humanoid muscle plus root curves.

The limiter is a corruption guard, not general animation smoothing. It permits
fast combat poses while preventing a one-frame 180-degree retargeting flip from
reintroducing the leg defect.

Loop metadata is inferred only for idle, walk, run, sprint and explicitly named
loop clips. Attacks, combos, skills, reactions, deaths, evasions and steps are
non-looping.

## Editor workflow

Use the commands under `Project S/Animation/Frank`:

- `Bake Selected Humanoid Clips` for iteration on selected source FBXs;
- `Bake Entire Humanoid Library In Background` for a non-blocking full rebuild;
- `Bake Entire Humanoid Library` only when a blocking batch is acceptable.

The full library contains 1374 source clips at the time this document was
written. Generated `.anim` files are tracked through Git LFS because their
serialized curve data is too large for the normal GitHub pack.

## Integration and validation

Animator controllers keep stable semantic state and clip-slot names. Weapon
variation is supplied through `AnimatorOverrideController`; Frank overrides
resolve to the generated Humanoid assets. Combat timing, hit windows and root
motion ownership remain gameplay data and are not inferred from animation
events.

Validation must establish all of the following before a generated clip is used:

- every source FBX has exactly one mirrored output clip;
- the source importer is Generic and the output reports `humanMotion=true`;
- clip length and frame rate are valid;
- all muscle/root keys are finite;
- muscle values remain inside `[-1, 1]` and consecutive muscle changes remain
  within the baker's corruption limit;
- the clip can be sampled on `DefaultMale` with a valid Humanoid Avatar;
- affected Animator Override Controllers no longer reference Generic Frank FBX
  clips;
- Unity Console has no new errors after import and controller validation.

The generated folder is derived data. Corrective changes belong in the baker or
the Generic source conversion and are then regenerated; generated curves should
not be hand-edited independently.
