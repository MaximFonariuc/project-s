# Project S — Player Combat Runtime

## Ответственность

`PlayerCombatController` координирует выполнение уже определённых combat actions: принимает семантические команды, обращается к action graph, запускает timeline executor, управляет buffer/cooldown и публикует подтверждённое состояние. Animator и weapon rig остаются presentation/scene adapters.

Combat не управляет motor напрямую. Он выдаёт только `PlayerMovementIntent`, который читает locomotion.

## Дерево классов и зависимости

```mermaid
classDiagram
    direction LR

    class IPlayerCombatCommands {
        +TryExecute(commandId) bool
        +ToggleWeaponAsync(weapon) UniTask~bool~
        +InterruptAttack()
    }
    class IPlayerCombatState {
        +Phase PlayerCombatPhase
        +CurrentWeapon EWeaponType
        +ComboIndex int
        +IsHitWindowOpen bool
        +MovementIntent PlayerMovementIntent
    }
    class PlayerCombatController
    class IStateMachine~PlayerCombatPhase~
    class PlayerCombatReadyState
    class PlayerCombatExecutingState {
        +Action PlayerCombatAction
    }
    class PlayerCombatRecoveryState {
        +Action PlayerCombatAction
    }
    class PlayerCombatWeaponSwitchState {
        +TargetWeapon EWeaponType
    }
    class PlayerCombatInterruptedState
    class PlayerCombatActionGraph
    class CombatActionExecutor
    class CombatInputBuffer
    class IPlayerCombatAnimation
    class IPlayerWeaponRig
    class IPlayerCombatActor
    class ICombatClock
    class PlayerCombatComposition {
        <<VContainer composition root>>
    }

    IPlayerCombatCommands <|.. PlayerCombatController
    IPlayerCombatState <|.. PlayerCombatController
    PlayerCombatController --> IStateMachine~PlayerCombatPhase~
    PlayerCombatController --> PlayerCombatActionGraph
    PlayerCombatController --> CombatActionExecutor
    PlayerCombatController --> CombatInputBuffer
    PlayerCombatController --> IPlayerCombatAnimation
    PlayerCombatController --> IPlayerWeaponRig
    PlayerCombatController --> IPlayerCombatActor
    PlayerCombatController --> ICombatClock
    IStateMachine~PlayerCombatPhase~ *-- PlayerCombatReadyState
    IStateMachine~PlayerCombatPhase~ *-- PlayerCombatExecutingState
    IStateMachine~PlayerCombatPhase~ *-- PlayerCombatRecoveryState
    IStateMachine~PlayerCombatPhase~ *-- PlayerCombatWeaponSwitchState
    IStateMachine~PlayerCombatPhase~ *-- PlayerCombatInterruptedState
    PlayerCombatComposition ..> PlayerCombatController : constructs
```

## State machine

```mermaid
stateDiagram-v2
    [*] --> Ready
    Ready --> Executing: graph entry accepted
    Executing --> Executing: chain/cancel selects next action
    Executing --> Recovery: RecoveryStart reached
    Recovery --> Executing: buffered transition accepted
    Executing --> Interrupted: dodge/interruption
    Recovery --> Interrupted: dodge/interruption
    Interrupted --> Ready
    Executing --> Ready: action completed / failed
    Recovery --> Ready: action completed / failed
    Ready --> WeaponSwitch: switch accepted
    WeaponSwitch --> Ready: complete / cancel / failure
```

`IStateMachine<PlayerCombatPhase>` — единственный owner combat phase. Controller больше не хранит собственный mutable phase и не содержит отдельный ручной `CanChangePhase`. `ExecutingState` и `RecoveryState` получают текущий `PlayerCombatAction` как context, а `WeaponSwitchState` — целевое оружие.

`RecoveryStart` является частью gameplay timeline, а не animation event. Поэтому transition в recovery остаётся определённым даже при смене Animator Controller или клипа.

## Выполнение action

```mermaid
sequenceDiagram
    participant Input as IPlayerInput
    participant Combat as PlayerCombatController
    participant Graph as PlayerCombatActionGraph
    participant Machine as Combat StateMachine
    participant Executor as CombatActionExecutor
    participant View as Animation / Weapon adapters

    Input->>Combat: CombatActionRequested(command)
    Combat->>Graph: TryResolveEntry(command, environment)
    Graph-->>Combat: PlayerCombatAction
    Combat->>View: play presentation
    Combat->>Machine: Ready -> Executing(action)
    Combat->>Executor: ExecuteAsync(definition)
    Executor-->>Combat: CombatActionFrame
    Combat->>Machine: Executing -> Recovery(action)
    Combat->>Graph: resolve buffered transition
    Combat->>Machine: Recovery -> Executing(next action)
```

Hitbox открывается только по `CombatActionFrame` и закрывается при completion, cancellation, interruption и disable. Weapon transition применяет loadout только после commit point. Cooldown хранится по `PlayerCombatActionId`; animation lookup использует отдельный `PlayerCombatAnimationId`.

## Lifecycle

- `Enable` запускает state machine в `Ready` и симметрично подписывает semantic input, dodge и state events.
- `Disable` отменяет UniTask action, aborts executor, очищает buffer, закрывает hitbox, снимает callbacks и останавливает state machine.
- State machine и её states создаются scoped-регистрациями VContainer в `PlayerCombatComposition`; controller не вызывает `Resolve` и не владеет container.
- Animator, graph authoring и input buffer не являются владельцами combat phase.

Текущий `IPlayerCombatActor.CanAttack` сохраняет проверенное наземное ограничение. Graph уже поддерживает `Grounded/Airborne`, но включение воздушных атак требует отдельного изменения actor gate и реальных air clips, а не скрытого обхода текущей locomotion policy.
