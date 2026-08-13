# Player combat action timeline

This tree owns the timing of player combat actions independently from the
temporary animation clips. Animation selects and blends a pose; the action
timeline decides when gameplay windows open, when an action commits and when it
is complete.

## Class diagram

```mermaid
classDiagram
    class PlayerCombatController
    class PlayerCombatActionProfile {
        <<ScriptableObject authoring>>
        +CreateCatalog() PlayerCombatActionCatalog
    }
    class PlayerCombatActionCatalog {
        +GetCombo(index) CombatActionDefinition
        +TryGetSkill(type, definition) bool
        +GetWeaponTransition() CombatActionDefinition
    }
    class CombatActionDefinition {
        +HitboxWindow
        +ChainWindow
        +CancelWindow
        +MovementConstraintWindow
        +InvulnerabilityWindow
        +CompletionTime
        +CommitTime
        +MovementIntent
        +RootMotionPolicy
    }
    class CombatActionExecutor {
        +ExecuteAsync(definition, duration, observer, token)
        +Abort()
        +MovementIntent
    }
    class CombatActionFrame {
        +NormalizedTime
        +IsHitboxOpen
        +CanChain
        +CanCancel
        +IsMovementConstrained
        +IsInvulnerable
        +CommitReached
        +IsComplete
    }
    class ICombatClock
    class IRootMotionPolicyTarget
    class IDamageImmunityTarget
    class PlayerActorAdapter
    class RootMotionController
    class IPlayerCombatAnimation

    PlayerCombatActionProfile --> PlayerCombatActionCatalog
    PlayerCombatActionCatalog --> CombatActionDefinition
    PlayerCombatController --> PlayerCombatActionCatalog
    PlayerCombatController --> CombatActionExecutor
    PlayerCombatController --> IPlayerCombatAnimation
    CombatActionExecutor --> CombatActionDefinition
    CombatActionExecutor --> CombatActionFrame
    CombatActionExecutor --> ICombatClock
    CombatActionExecutor --> IRootMotionPolicyTarget
    CombatActionExecutor --> IDamageImmunityTarget
    IRootMotionPolicyTarget <|.. PlayerActorAdapter
    IDamageImmunityTarget <|.. PlayerActorAdapter
    PlayerActorAdapter --> RootMotionController
```

## Dependency and ownership rules

- `PlayerCombatController` selects a semantic combo, skill or weapon action. It
  does not count animation frames and does not read Animator state.
- `CombatActionExecutor` is the sole owner of elapsed action time. It evaluates
  inclusive normalized windows using the injected combat clock.
- `IPlayerCombatAnimation` returns presentation duration only. Clip names and
  Animator states are not action identifiers and may be replaced freely.
- `PlayerCombatActionCatalog` contains immutable runtime definitions.
  `PlayerCombatActionProfile` is optional immutable authoring data that creates
  that catalog at composition time.
- Root motion is selected per action and the previous policy is restored on
  completion, interruption, cancellation and disable.
- Hitbox state is derived from the current action frame. The weapon rig remains
  the owner of the physical hitbox.
- Movement constraints may apply to the full action or to a configured window.
  Outside that window the executor exposes unrestricted movement intent.
- Invulnerability is an explicit action window evaluated by the executor;
  `Health` remains the owner of whether incoming damage is accepted.
- Input buffering remains a separate concern. Project S adapts the reusable
  `ModularInput.InputBuffer<TAction>` and consumes a buffered combo only in a
  chain window and a buffered skill only in a cancel window.

## Temporary animation policy

The current clips use prototype defaults matching the previous behavior:

- hitbox: `0.20 .. 0.55`;
- combo/cancel: `0.50 .. 0.75`;
- attack completion: `0.75`;
- weapon commit and completion: `0.50`;
- special skill cooldown: `1.25s`;
- root motion: `Ignore`.
- movement constraints: full action;
- invulnerability: disabled.

These values are normalized and are not stored in animation assets. Assign a
`PlayerCombatActionProfile` when tuning begins; replacing FBX, clips or the
Animator Controller does not require changes to gameplay code.

This tree intentionally remains in `_App/Core`: DMC-like cancel semantics are
still game-specific. Only a stable, project-neutral timeline should later be
extracted into `_App/Modules`.
