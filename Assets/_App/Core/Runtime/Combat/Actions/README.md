# Player combat action timeline

Combat timing is independent from animation presentation.

```mermaid
classDiagram
    class PlayerCombatActionProfile {
        <<ScriptableObject authoring>>
    }
    class PlayerCombatActionGraph
    class CombatActionDefinition {
        +DurationSeconds
        +HitboxWindow
        +CancelWindow
        +RecoveryStart
        +CompletionTime
        +Motion
    }
    class CombatActionExecutor
    class CombatActionFrame
    class CombatMotionController
    class IDamageImmunityTarget
    class ICharacterActionMotionMotor
    class CharacterMotor

    PlayerCombatActionProfile --> PlayerCombatActionGraph
    PlayerCombatActionGraph --> CombatActionDefinition
    CombatActionExecutor --> CombatActionDefinition
    CombatActionExecutor --> CombatActionFrame
    CombatActionExecutor --> CombatMotionController
    CombatActionExecutor --> IDamageImmunityTarget : window lease
    CombatMotionController --> ICharacterActionMotionMotor
    ICharacterActionMotionMotor <|.. CharacterMotor
```

- `DurationSeconds` is the full gameplay time basis. Normalized hit, cancel,
  recovery, invulnerability and motion windows are evaluated against it.
- `CombatActionExecutor` owns elapsed time and cleanup. It does not read
  Animator state or clip length.
- Each invulnerability window owns a disposable lease. Overlapping combat and
  dodge windows compose without one action clearing another owner's immunity.
- `CombatMotionController` creates collision-safe velocity intents from an
  authored distance and normalized window. Animation-derived trajectory assets
  are not used.
- `CharacterMotor` and `CharacterMovement` remain the only authorities for
  position, collision, slopes and grounding.
- The current profile contains one grounded unarmed action and one
  `attack.light` entry. Combo transitions, air actions and skills are deferred.
