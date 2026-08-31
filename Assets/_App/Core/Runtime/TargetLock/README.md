# Player targeting

Targeting owns target selection. Camera, combat motion and animation consume its
confirmed state through narrow read-only contracts.

```mermaid
classDiagram
    class PlayerTargetingController {
        +ToggleLock() bool
        +SwitchTarget(direction) bool
        +TryGetAttackAssistTarget(...) bool
    }
    class IPlayerTargetingState {
        <<interface>>
        +IsTargetLocked bool
        +CurrentTarget Transform
        +AimPosition Vector3
    }
    class IPlayerTargetingCommands {
        <<interface>>
    }
    class IPlayerCombatTargetingQuery {
        <<interface>>
    }
    class IPlayerInput
    class IPlayerTargetingActor
    class ICameraOrientation
    class CombatTarget
    class ICombatTarget {
        <<interface>>
        +TargetId int
        +IsTargetable bool
        +TargetRoot Transform
        +AimPosition Vector3
    }
    class PlayerCameraController
    class CombatMotionController

    PlayerTargetingController ..|> IPlayerTargetingState
    PlayerTargetingController ..|> IPlayerTargetingCommands
    PlayerTargetingController ..|> IPlayerCombatTargetingQuery
    PlayerTargetingController --> IPlayerInput
    PlayerTargetingController --> IPlayerTargetingActor
    PlayerTargetingController --> ICameraOrientation
    CombatTarget ..|> ICombatTarget
    PlayerCameraController --> IPlayerTargetingState
    CombatMotionController --> IPlayerTargetingState
    CombatMotionController --> IPlayerCombatTargetingQuery
```

- Hard lock is explicit player state. It owns the selected target and the
  temporary actor rotation mode, restoring the previous mode when lock is lost.
- Hard lock tolerates a brief line-of-sight interruption, measured in unscaled
  time, so a thin pillar does not cause camera flicker. Sustained occlusion
  clears the target and restores the actor's previous rotation mode.
- Acquisition and target switching score visible `ICombatTarget` instances in
  camera space. Multiple colliders belonging to one target are deduplicated by
  `TargetId`.
- Attack assist is a separate one-shot query inside an authored distance and
  cone. It never mutates hard lock, camera state or the actor rotation policy.
- Combat motion caches a confirmed target contract once per action. A dead,
  disabled or destroyed carried target is rejected without component searches
  in the frame loop; policies that allow fallback may then use hard lock or the
  one-shot attack-assist target instead of chasing a stale transform.
- `CombatTarget` owns scene references for the root, aim point and optional
  health gate. A dead or disabled target is not targetable.
- Physics queries use the `Enemy` layer, bounded reusable buffers and explicit
  line-of-sight checks. Gameplay code never searches folders or scene names.
- All command and query entry points are inert outside the controller lifecycle.
  `ClearTarget()` remains available during teardown so rotation ownership is
  always released.
