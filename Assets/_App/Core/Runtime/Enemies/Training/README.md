# Combat training attacker

This tree closes the incoming-hit side of the current combat vertical slice. It
is a deterministic test actor, not the foundation of the future enemy AI.

```mermaid
classDiagram
    class CombatTrainingAttacker {
        +TryStartAttack() bool
        +ConfigureTarget(targetRig)
        +Snapshot CombatTrainingAttackSnapshot
    }
    class CombatTrainingAttackSettings {
        <<serialized authoring>>
    }
    class CombatTrainingAttackTelegraph {
        <<presentation adapter>>
    }
    class CombatTrainingAttackerAnimationAdapter {
        <<presentation adapter>>
    }
    class CharacterAnimationHost {
        <<single animation owner>>
    }
    class ICombatAttackOutcomeReceiver {
        <<interface>>
        +OnAttackOutcome(hit)
    }
    class CombatAttackExecution
    class CombatHitResolver
    class BodyColliderRig
    class Hurtbox
    class Health
    class CharacterReactionHost
    class Rigidbody

    CombatTrainingAttacker *-- CombatTrainingAttackSettings
    CombatTrainingAttacker --> CombatAttackExecution
    CombatTrainingAttacker ..|> ICombatAttackOutcomeReceiver
    CombatTrainingAttacker --> CombatHitResolver
    CombatTrainingAttacker --> BodyColliderRig : explicit target
    BodyColliderRig *-- Hurtbox
    Hurtbox --> Health : confirmed hit
    CombatTrainingAttacker --> CharacterReactionHost : interruption gate
    CombatTrainingAttacker --> Rigidbody : yaw intent in FixedUpdate
    CombatTrainingAttackTelegraph --> CombatTrainingAttacker : read-only snapshot
    CombatTrainingAttackerAnimationAdapter --> CombatTrainingAttacker : read-only snapshot
    CombatTrainingAttackerAnimationAdapter --> CharacterAnimationHost
```

- The actor owns a fixed `Waiting -> Windup -> Active -> Recovery` loop plus a
  source-side `Active -> ParryStagger -> Waiting` branch. The configured
  one-second stagger is the deterministic punish window after a successful
  player parry.
  Target choice, navigation and tactical decisions intentionally remain outside
  this test boundary.
- A target `BodyColliderRig` is assigned explicitly by the scene. There is no
  tag lookup, global registry or `Find*` fallback.
- Waiting and windup rotate the Rigidbody toward that target at the authored
  `540 deg/s`; active, recovery and parry-stagger lock the committed attack
  direction. The upright body remains physics-owned and no transform rotation
  is written from `Update`.
- A contact must be both within `2.4 m` and inside the authored `100 degree`
  forward cone. A rear or side target does not consume the execution and takes
  no damage, so the visible swing, hit query and Player guard arc agree.
- The active window uses the same `CombatAttackExecution`, repeat filtering,
  `CombatHitResolver`, `Hurtbox` and `Health` path as player weapons. Damage
  immunity can reject a frame without consuming the execution, allowing the
  window to validate dodge timing correctly.
- Being hit, knocked down or killed interrupts the attack and closes its active
  execution. Revive restarts the initial delay.
- A confirmed `Parried` result closes the active execution synchronously before
  another target contact can resolve. `Blocked` continues into normal recovery.
- The telegraph reads a snapshot and renders the same forward attack sector:
  orange/red describes windup/active, while a pulsing cyan circle describes the
  parry punish window.
  It never applies damage or changes attack timing.
- The animation adapter enters the existing Humanoid `Attack/BaseAttack1` once
  at `Windup`. Its `0.45 / 0.32 / 0.90 s` gameplay phases place the active hit
  at normalized `0.27...0.46`, matching the current semantic clip window while
  keeping a deliberately readable windup. The driver stretches the complete
  clip over the same `1.67 s` action without moving gameplay authority into the
  Animator.
- On entry to `ParryStagger`, that adapter requests the existing
  `Reaction/HitStun` slot at higher priority. It adds no Animator states or
  transitions. Damage/death reactions may replace it through the same shared
  coordinator, and normal phase exit releases ownership without forcing a
  transition.
- `CombatActionId` belongs to the shared hit boundary. Player graph identifiers
  are converted only when a player action begins, so enemy/test attacks do not
  depend on player combo types.
