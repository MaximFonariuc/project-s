# Player combat action timeline

Combat timing is independent from animation presentation.

```mermaid
classDiagram
    class PlayerCombatActionProfile {
        <<ScriptableObject authoring>>
        +Actions
        +Entries
        +Transitions
    }
    class PlayerCombatLoadoutProfile
    class PlayerCombatMovesetCatalog
    class PlayerCombatActionGraph
    class CombatActionDefinition {
        +DurationSeconds
        +HitboxWindow
        +CancelWindow
        +RecoveryStart
        +CompletionTime
        +Motion
    }
    class CombatActionExecutor {
        +ExecuteAsync(definition, duration, target, observer, cancellationToken)
        +Advance(scaledDeltaTime)
        +Observe()
        +CurrentFrame CombatActionFrame
    }
    class CombatActionSession {
        +ExecuteAsync(action, weapon, target, observer, token)
        +Abort(reason)
    }
    class CombatActionFrame
    class CombatMotionController
    class IDamageImmunityTarget
    class ICharacterActionMotionMotor
    class IPlayerCombatTargetingQuery
    class CharacterMotor

    PlayerCombatLoadoutProfile --> PlayerCombatMovesetCatalog
    PlayerCombatMovesetCatalog --> PlayerCombatActionProfile
    PlayerCombatActionProfile --> PlayerCombatActionGraph
    PlayerCombatActionGraph --> CombatActionDefinition
    CombatActionExecutor --> CombatActionDefinition
    CombatActionExecutor --> CombatActionFrame
    CombatActionExecutor --> CombatMotionController
    CombatActionExecutor --> IDamageImmunityTarget : window lease
    CombatActionSession --> CombatActionExecutor : owns one attack run
    CombatMotionController --> ICharacterActionMotionMotor
    CombatMotionController --> IPlayerCombatTargetingQuery : one query per action
    ICharacterActionMotionMotor <|.. CharacterMotor
```

- `DurationSeconds` is the full gameplay time basis. Normalized hit, cancel,
  recovery, invulnerability and motion windows are evaluated against it.
- `CombatActionExecutor` owns elapsed time and cleanup, but does not poll Unity
  time or schedule its own frame loop. `PlayerActorController` advances combat
  exactly once before locomotion command resolution, then resolves combat
  commands after locomotion has had the opportunity to accept a dodge against
  the current authored cancel policy.
  The executor does not read Animator state or clip length.
- A tick is split deliberately: `Advance` first creates the authoritative
  `CombatActionFrame`, input callbacks captured since the previous tick are then
  consumed against that frame, and `Observe` finally resolves recovery,
  transition, commit and completion. This prevents callback/`UniTask` scheduling
  order from changing combat results.
- `CombatActionSession` surrounds one graph action with its execution id,
  confirmed outcome and hitbox lifecycle. The executor remains the lower-level
  timeline/motion mechanism; session end reason explains why the action stopped.
- `Allow Jump` and `Allow Dodge` are action policies, not free-form movement
  flags: locomotion receives them only while the authored `CancelWindow` is
  open. Startup, hit-confirm and committed recovery therefore cannot be
  bypassed accidentally.
- Startup is transactional: if a motion or immunity adapter fails while an
  execution is beginning, `Abort` rolls the partial state back and leaves no
  stale motion intent or active execution id.
- `PlayerCombatActionProfile.OnValidate` builds the complete immutable graph in
  the Editor, so duplicate ids, invalid transitions, timelines and impact data
  fail at authoring time instead of first appearing during gameplay composition.
- Graph compilation also walks every entry and transition. An orphaned action
  that cannot be reached from any command entry is rejected instead of remaining
  as silent, untestable authoring data.
- An action profile is weapon-specific and owns no runtime state, input-buffer
  policy or draw/sheathe timing. `PlayerCombatLoadoutProfile` owns those shared
  policies and maps each weapon to its profile and optional Animator override.
- Standard `hit.launcher` and `hit.slam` ids validate the sign of vertical
  knockback. Custom reaction ids remain open and are classified by the target
  reaction system at runtime.
- Hit filtering is once per target and action by default. A multi-hit action can
  opt into a bounded repeat count and scaled-time interval; hit stop freezes that
  interval instead of generating extra contacts while gameplay time is paused.
- Each invulnerability window owns a disposable lease. Overlapping combat and
  dodge windows compose without one action clearing another owner's immunity.
- `CombatMotionController` creates collision-safe velocity intents from an
  authored distance and normalized window. Animation-derived trajectory assets
  are not used. Ground attacks may acquire one soft-assist target inside an
  authored cone; this does not mutate hard lock-on or camera state.
- `CharacterMotor` and `CharacterMovement` remain the only authorities for
  position, collision, slopes and grounding.
- The current Dual Swords profile contains independent grounded and airborne
  `attack.light` branches, grounded named `attack.heavy` and `skill.special`
  actions, a direct grounded launcher entry, and a hit-confirmed
  `attack.launcher -> movement.air-chase` route. Every ground-light step exposes
  the same heavy and hit-confirmed launcher cancels; environment-specific
  entries share semantic commands without device checks.
- Air actions use motor-owned `MinimumVelocity` and planar intents for a bounded
  air hang. Animator root motion never becomes position authority.
