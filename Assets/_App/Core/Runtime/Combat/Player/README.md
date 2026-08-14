# Player combat presentation

This tree adapts confirmed Project S combat actions to animation presentation.
It does not decide whether an attack is legal, when damage is applied or when a
cancel window opens.

## Class diagram

```mermaid
classDiagram
    class PlayerCombatController {
        +TryExecute(command) bool
        +InterruptAttack()
    }
    class IPlayerCombatAnimation {
        <<interface>>
        +ApplyLoadout(character, weapon)
        +TryPlayComboStep(index, playback) bool
        +TryPlayNamedAnimation(id, playback) bool
        +TryPlayWeaponTransition(weapon, draw, playback) bool
        +Release()
    }
    class PlayerCombatAnimationAdapter
    class AnimationsData {
        <<ScriptableObject library>>
    }
    class CombatAnimationSet {
        <<ScriptableObject loadout>>
        +ControllerOverrides
        +ComboActions
        +TryGetNamedAction(id, presentation) bool
        +TryGetWeaponTransition(draw, presentation) bool
    }
    class CharacterAnimationActionPresentation {
        +Clip
        +BlendOutSeconds
        +CreateRequest() AnimationClipActionRequest
    }
    class PlayerAnimationCoordinator {
        +TryPlayAction(source, request, blendOut, lease) bool
    }
    class AnimationClipActionArbiter
    class CombatActionExecutor

    PlayerCombatController --> IPlayerCombatAnimation
    PlayerCombatController --> CombatActionExecutor
    IPlayerCombatAnimation <|.. PlayerCombatAnimationAdapter
    PlayerCombatAnimationAdapter --> AnimationsData
    PlayerCombatAnimationAdapter --> PlayerAnimationCoordinator
    PlayerAnimationCoordinator --> AnimationClipActionArbiter
    AnimationsData --> CombatAnimationSet
    CombatAnimationSet --> CharacterAnimationActionPresentation
```

## Dependencies and ownership

- `PlayerCombatController` and `CombatActionExecutor` own action authority,
  normalized gameplay windows, interruption and root-motion policy.
- `CombatAnimationSet` is immutable authoring data for one weapon loadout. Clip
  replacement and blend tuning do not change the combat action graph.
- `PlayerCombatAnimationAdapter` resolves a presentation and returns only its
  effective duration. It claims the `Combat` animation channel through
  `PlayerAnimationCoordinator` and never reads Animator state or clip progress.
- Combat keeps an `AnimationActionLease`. Consecutive combat clips can crossfade,
  but a later reaction/death claim invalidates the combat lease. Combat cleanup
  therefore cannot stop a higher-priority clip.
- Controller overrides are restricted to continuous loadout locomotion. Finite
  combo, skill and draw/sheathe clips are played directly and have no Animator
  placeholder states.
- AvatarMask is a presentation choice. Root motion remains an explicit gameplay
  policy applied by `CombatActionExecutor` through `IRootMotionPolicyTarget`.

## Current authoring

The player animation library references `Unarmed_CombatAnimationSet` and
`TwoHanded_CombatAnimationSet`. Both contain four combo actions and one named
`skill.special` action. The two-handed set additionally contains explicit draw
and sheathe actions plus nine continuous locomotion overrides.
