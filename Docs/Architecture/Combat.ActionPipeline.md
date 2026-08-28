# Project S — Combat Action Pipeline

## Boundary

`PlayerCombatActionPipeline` owns the execution of an already accepted player
action and the selection of its next action. It is a scoped Core service, not a
reusable module and not an interface. Its boundary exists because graph
traversal, buffering, action-session observation and target carry form one
cohesive lifecycle that is independent from input subscriptions and weapon
switching.

```mermaid
classDiagram
    class PlayerCombatController {
        +TryExecute(commandId) bool
        +Tick(scaledDeltaTime)
        +ToggleWeaponAsync(weapon)
    }
    class PlayerCombatActionPipeline {
        +TryAccept(command, phase, weapon, action) bool
        +ExecuteAsync(action, weapon, token)
        +TryRecord(hit)
        +Reset()
        +ComboIndex int
        +IsHitWindowOpen bool
    }
    class PlayerCombatActionGraph {
        +TryResolveEntry(command, environment, action) bool
        +TryResolveTransition(source, command, frame, outcome, action) bool
    }
    class CombatInputBuffer
    class CombatActionSession {
        +Action PlayerCombatAction
        +CurrentFrame CombatActionFrame
        +Outcome CombatActionOutcome
        +ExecuteAsync(action, weapon, target, observer, token)
        +TryRecord(hit)
    }
    class CombatActionExecutor
    class PlayerCombatAvailability
    class IPlayerCombatAnimation
    class IPlayerCombatActor
    class IStateMachine~PlayerCombatPhase~

    PlayerCombatController --> PlayerCombatActionPipeline : commands / lifecycle
    PlayerCombatController --> CombatActionExecutor : Advance then Observe
    PlayerCombatActionPipeline --> PlayerCombatActionGraph : entry / transition
    PlayerCombatActionPipeline --> CombatInputBuffer : contextual commands
    PlayerCombatActionPipeline --> CombatActionSession : one action at a time
    PlayerCombatActionPipeline --> CombatActionExecutor : current frame
    PlayerCombatActionPipeline --> PlayerCombatAvailability : admission / cooldown
    PlayerCombatActionPipeline --> IPlayerCombatAnimation : semantic action
    PlayerCombatActionPipeline --> IPlayerCombatActor : current environment
    PlayerCombatActionPipeline --> IStateMachine~PlayerCombatPhase~ : executing / recovery
```

## Ownership

- `PlayerCombatController` owns input/event subscriptions, the pre-tick command
  inbox, the shared operation cancellation boundary, persistent weapon state and
  weapon transitions.
- `PlayerCombatActionPipeline` owns the buffered transition candidate, combo
  index, graph entry/transition selection, action-to-action loop, landing exit
  and confirmed-target carry between launcher, air chase and air combo.
- `CombatActionSession` remains the owner of one active execution, its hitbox,
  outcome and combat motion. The pipeline does not duplicate those values.
- `CombatActionExecutor` remains the only timeline clock. The controller calls
  `Advance`, consumes queued commands through the pipeline, then calls
  `Observe`; the async pipeline never advances time itself.
- `PlayerCombatOperationLifetime` still owns cancellation across both the action
  pipeline and weapon-transition session. A late async completion cannot release
  a newer operation.

## Command and chain flow

```mermaid
sequenceDiagram
    participant Input as PlayerCombatController
    participant Pipeline as PlayerCombatActionPipeline
    participant Graph as PlayerCombatActionGraph
    participant Session as CombatActionSession
    participant Machine as Combat StateMachine

    Input->>Pipeline: TryAccept(command, phase, weapon)
    alt Ready
        Pipeline->>Graph: TryResolveEntry(command, environment)
        Graph-->>Pipeline: initial action
        Input->>Pipeline: ExecuteAsync(action, weapon, token)
    else Executing or Recovery
        Pipeline->>Pipeline: store command with source action/time
    end
    Pipeline->>Machine: Executing(action)
    Pipeline->>Session: ExecuteAsync(action, target, frame observer)
    Session-->>Pipeline: frame / outcome / last confirmed target
    Pipeline->>Graph: resolve buffered transition
    Pipeline->>Machine: Recovery or Executing(next action)
```

The equipped weapon is captured when the pipeline starts. Weapon switching is
not legal during execution, so every action in one chain uses the same graph and
animation loadout context. `Reset` clears only pipeline-owned transient state;
session cancellation and presentation cancellation are performed by the shared
operation lifetime before reset.

When an airborne-only action detects a grounded actor, landing wins over a
buffered transition. The session returns `Landed`, the pipeline cancels the
finite animation and completion returns the controller to `Ready`. A late
buffered command that did not match a transition may still resolve as a fresh
entry after the current session finishes.
