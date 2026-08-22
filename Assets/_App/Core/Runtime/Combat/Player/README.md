# Player combat presentation

This tree translates an accepted combat action into a semantic Animator action.
It does not decide action legality, damage or cancel timing.

```mermaid
classDiagram
    class PlayerCombatController
    class IPlayerCombatAnimation {
        <<interface>>
        +TryPlayComboStep(index, expectedDuration, playback) bool
        +Cancel()
        +Release()
    }
    class PlayerCombatAnimationAdapter
    class PlayerAnimationCoordinator
    class AnimationActionRequest
    class PlayerWeaponRig
    class Weapon

    PlayerCombatController --> IPlayerCombatAnimation
    IPlayerCombatAnimation <|.. PlayerCombatAnimationAdapter
    PlayerCombatAnimationAdapter --> PlayerAnimationCoordinator
    PlayerCombatAnimationAdapter --> AnimationActionRequest
    PlayerCombatController --> PlayerWeaponRig
    PlayerWeaponRig --> Weapon
```

- The adapter exposes four combo steps for every compatible animation loadout
  and maps them to semantic action `LightAttack`, variants `0...3`.
- The Animator Controller maps those variants to `BaseAttack1...4`; gameplay
  code has no clip reference.
- The fourth prototype step is the current heavy finisher: its confirmed hit
  requests `hit.heavy` and crosses the reaction profile's knockdown threshold.
- Gameplay passes the action duration to presentation. The Animator driver
  derives the local `ActionSpeed` from the active base/override clip, so both
  controller types finish on the same normalized gameplay timeline.
- `PlayerWeaponRig` enables two hand hitboxes only during the confirmed hit
  window. `Weapon` reports collision candidates to `CombatHitResolver`.
- `Cancel()` sends an explicit Animator cancel trigger when gameplay interrupts
  an action. Normal completion lets the controller use its authored exit
  transition.
- Weapon transitions and the ground light combo are implemented. Named skills,
  heavy branches and air attacks remain deferred.
