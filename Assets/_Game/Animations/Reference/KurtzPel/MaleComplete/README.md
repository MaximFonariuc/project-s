# KurtzPel male reference animation pack

This folder contains a local reference conversion from the installed copy of
KurtzPel at `D:\Games\SteamGames\steamapps\common\KurtzPel`.

The original pack remains isolated reference content. A selected subset is
wired into the Project S Player by the dedicated controller/prefab setup tool.

## Contents

- `Model/KurtzPel_MaleReference.fbx` — untextured male reference body with the
  original 86-bone skeleton.
- `Movement/` — 336 locomotion and character-state clips: common ninja motion,
  directional movement and all eight male personality variants.
- `Reactions/KurtzPel_Male_Reactions.fbx` — 26 hit, blow and stand reactions.
- `Misc/KurtzPel_Male_Misc.fbx` — 4 intro and flag-capture clips.
- `Combat/KurtzPel_BlazingFist.fbx` — 71 Blazing Fist clips, including ground
  and air combos, dash/avoid attacks, parry, holds, skills and rage skills.
- `KurtzPel_AnimationManifest.csv` — exact clip, group, frame count, source path
  and source frame rate for all 437 clips.
- `Preview/KurtzPel_MaleReference.prefab` — reference model with a valid Generic
  Avatar and no Animator Controller assigned.

The source game does not contain a general-purpose swimming, crawling or
climbing set in this player animation library, so no such clips were invented
or substituted.

## Unity import

The FBXs use a shared Generic Avatar copied from the reference model. Obvious
wait/run/walk/loop clips are marked as looping; attack, start, end and landing
clips preserve their non-looping form. Runtime root motion is disabled.

Reconfigure or validate the import through:

`Tools > Animation > KurtzPel > Configure Reference Pack`

Preview the clips through:

`Tools > Animation > KurtzPel > Open Animation Preview`

The preview window spawns the reference prefab into the current scene and
samples clips on that scene object. It does not create an Animator Controller
or modify the Project S Player.

Build the conventional Project S Animator Controller, update the Player prefab
and regenerate its Generic body colliders through:

`Tools > Animation > KurtzPel > Build Player Animator and Prefab`

See `Docs/Animation/KurtzPelAnimationAudit.md` for the full validation boundary,
timing correction and selected runtime motions.

## Usage restriction

These assets are extracted from KurtzPel and remain third-party copyrighted
content. Keep them local and use them only as temporary animation references.
Do not redistribute them or include them in a public/released build. Replace
them with original or properly licensed production assets.
