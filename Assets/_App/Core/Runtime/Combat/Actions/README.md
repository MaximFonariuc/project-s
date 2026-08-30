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
  time or schedule its own frame loop. `PlayerCombatController.Tick` supplies
  scaled delta time exactly once per gameplay tick. The executor does not read
  Animator state or clip length.
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
- An action profile is weapon-specific and owns no runtime state, input-buffer
  policy or draw/sheathe timing. `PlayerCombatLoadoutProfile` owns those shared
  policies and maps each weapon to its profile and optional Animator override.
- Standard `hit.launcher` and `hit.slam` ids validate the sign of vertical
  knockback. Custom reaction ids remain open and are classified by the target
  reaction system at runtime.
- Each invulnerability window owns a disposable lease. Overlapping combat and
  dodge windows compose without one action clearing another owner's immunity.
- `CombatMotionController` creates collision-safe velocity intents from an
  authored distance and normalized window. Animation-derived trajectory assets
  are not used. Ground attacks may acquire one soft-assist target inside an
  authored cone; this does not mutate hard lock-on or camera state.
- `CharacterMotor` and `CharacterMovement` remain the only authorities for
  position, collision, slopes and grounding.
- The current profile contains independent grounded and airborne
  `attack.light` branches, grounded named `attack.heavy` and `skill.special`
  actions, and a hit-confirmed `attack.launcher -> movement.air-chase` route.
  Environment-specific entries share semantic commands without device checks.
- Air actions use motor-owned `MinimumVelocity` and planar intents for a bounded
  air hang. Animator root motion never becomes position authority.
