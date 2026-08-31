# Character reaction presentation

Reaction gameplay and animation presentation have separate owners.

```mermaid
classDiagram
    class CharacterReactionController
    class CharacterReactionSnapshot {
        +Kind CharacterReactionKind
        +State CharacterReactionStateId
        +Direction CharacterReactionDirection
    }
    class CharacterPoise
    class CharacterReactionStateMachineFactory
    class CharacterReactionProfile {
        <<gameplay ScriptableObject>>
    }
    class ICharacterImpactMotor
    class ICharacterReactionPresentation
    class CharacterReactionAnimationAdapter
    class PlayerAnimationCoordinator
    class CharacterReactionHost {
        <<standalone composition root>>
    }
    class CharacterAnimationHost {
        <<single animation owner>>
    }
    class RigidbodyCharacterImpactMotor
    class XController

    CharacterReactionStateMachineFactory --> CharacterReactionController : supplies machine
    CharacterReactionController --> CharacterReactionSnapshot : creates
    CharacterReactionController --> CharacterPoise
    CharacterReactionController --> CharacterReactionProfile
    CharacterReactionController --> ICharacterImpactMotor
    CharacterReactionController --> ICharacterReactionPresentation
    ICharacterReactionPresentation <|.. CharacterReactionAnimationAdapter
    CharacterReactionAnimationAdapter --> PlayerAnimationCoordinator
    PlayerAnimationCoordinator --> XController : ActionId 4
    CharacterReactionHost --> CharacterReactionController
    CharacterReactionHost --> CharacterAnimationHost
    CharacterAnimationHost --> PlayerAnimationCoordinator
    CharacterReactionHost --> RigidbodyCharacterImpactMotor
```

- `CharacterReactionController` owns stable, hit-stun, knockdown, get-up,
  airborne and dead gameplay states, their timers, poise and the source-side
  direction captured when a hit is confirmed. It also normalizes the open
  authoring reaction id into `Light`, `Heavy`, `Launcher` or `Slam` once per hit.
- `CharacterReactionAnimationAdapter` may request a semantic reaction slot but
  never changes reaction gameplay state.
- Reaction and death outrank combat and locomotion in
  `PlayerAnimationCoordinator`.
- Repeated hit, reset, revive, disable and scope teardown release presentation
  idempotently.
- `CharacterReactionStateMachineFactory` creates the same state topology for the
  VContainer-owned Player controller and standalone character hosts.
- `CharacterReactionHost` is a local reaction composition root for scene/prefab
  actors that do not yet have their own VContainer scope. It does not duplicate
  poise, timers or transition rules. Animator ownership belongs to the adjacent
  `CharacterAnimationHost`, allowing other presentation adapters to share the
  same driver without writing to Animator independently.
- `XController` maps semantic action id `4` to nine required slots:
  `Reaction/HitStun`, `Reaction/HitStun_Back`, `Reaction/HitStun_Left`,
  `Reaction/HitStun_Right`, `Reaction/Knockdown`, `Reaction/Airborne`,
  `Reaction/Dead_1`, `Reaction/Dead_2` and `Reaction/GetUp`. Weapon override
  controllers inherit the slots.
- Direction names describe the source side relative to the target. Gameplay
  computes that value once from the confirmed impulse and target orientation;
  rotation during the reaction does not select a different clip. Only hit-stun
  is directional today; other reaction states retain their common fallback.
- Known ids (`hit.light`, `hit.heavy`, `hit.launcher`, `hit.slam`) map explicitly
  to a closed runtime kind. Unknown ids fall back from signed vertical impact and
  planar knockback, so gameplay and presentation do not branch on arbitrary strings.
- Death selection uses the normalized kind: `Heavy` selects `Dead_2`; other
  kinds fall back to `Dead_1`.
- Reaction and death slots keep their authored playback speed. Gameplay still
  owns the reaction duration and cancels presentation on return to `Stable`;
  short hit-stun windows never accelerate the source clip.
- The current reaction states reference Humanoid proxy motions from the Knight
  and CLazy sets. Left/right use the same `LightHit` clip with Animator mirroring,
  while front/back use normal/alternate `LightHit` slots. The
  imported FBX clips are not modified and contain no gameplay Animation Events.
  They remain replaceable content slots; damage, death and collision ownership
  stay in gameplay code.
- Timed reaction states have no Animator-owned gameplay transitions. Knockdown
  completion explicitly enters `GetUp`; get-up completion cancels presentation
  back to locomotion. Both death states hold their final frame until revive.
- Vertical impact is signed: positive speed launches along actor up, while
  negative speed drives an airborne actor down. A downward impact becomes
  `Knockdown` only when the motor confirms landing and is not applied twice.
- `CombatTrainingTarget.prefab` duplicates the Player visual and generated body
  hurtboxes, but removes player input, locomotion, combat and weapon ownership.
  It adds `CombatTarget`, `CharacterReactionHost` and
  `RigidbodyCharacterImpactMotor` for end-to-end hit validation.
- The standalone grounded `attack.heavy` action is authored as `hit.heavy` with
  enough knockback to enter `Knockdown`; all three light-combo steps remain light.
  The `combo.air.2` finisher is authored as `hit.slam` and retains `Slam` while
  its negative vertical impact drives the target toward grounded knockdown.
