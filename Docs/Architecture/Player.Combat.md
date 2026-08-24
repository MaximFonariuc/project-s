# Project S — Player Combat Runtime

## Ответственность

`PlayerCombatController` координирует выполнение уже определённых combat actions: принимает семантические команды, обращается к action graph, запускает timeline executor, управляет buffer/cooldown и публикует подтверждённое состояние. Animator и weapon rig остаются presentation/scene adapters.

Combat не управляет motor напрямую. Он выдаёт только `PlayerMovementIntent`, который читает locomotion.

## Текущие combo branches

### Ground

Одна команда `attack.light` входит в `combo.ground.0`. Следующие команды сначала
попадают в `CombatInputBuffer` и могут перейти только `0 → 1 → 2` внутри
`ChainWindow` текущего action. После третьего action chain transition отсутствует:
новая цепочка начинается только отдельным вводом, который остаётся жив на
completion boundary.

| Step | Duration | Hitbox | Chain | Cancel | Damage | Motor distance |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1.20 s | 0.28–0.50 | 0.50–0.76 | 0.58–0.90 | 10 | 0.55 m |
| 1 | 1.40 s | 0.30–0.50 | 0.52–0.78 | 0.60–0.90 | 12 | 0.75 m |
| 2 | 1.50 s | 0.35–0.55 | disabled | 0.64–0.92 | 15 | 0.70 m |

`attack.heavy` не является четвёртым combo step. Это самостоятельная наземная
action с duration `1.32 s`, hitbox `0.24–0.45`, damage `24` и motor distance
`0.90 m`; она отображается через существующий `BaseAttack4` slot.

Это стартовые tuning values для текущего набора клипов. Damage/hitbox/movement
остаются gameplay data и не вычисляются из Animator. Presentation получает лишь
ожидаемую длительность action и подстраивает скорость активного override-клипа.

### Air

Во время `Falling` та же команда `attack.light` входит в `combo.air.0` и может
продолжиться только `0 → 1 → 2`. Воздушный finisher завершает цепочку.

| Step | Duration | Hitbox | Chain | Cancel | Damage | Motor distance |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 0 | 1.583 s | 0.12–0.31 | 0.32–0.46 | 0.50–0.84 | 10 | 0.35 m |
| 1 | 1.500 s | 0.10–0.32 | 0.33–0.46 | 0.50–0.84 | 12 | 0.35 m |
| 2 | 1.917 s | 0.11–0.35 | disabled | 0.62–0.90 | 22 | 0.45 m |

Начало chain windows выбрано по измеренному pose seam retargeted clips, а не по
произвольному общему значению. `MinimumVelocity(-1.5 m/s)` создаёт ограниченный
air hang только до `0.58 / 0.58 / 0.55` normalized time, не выключая gravity на
весь action. Planar и vertical displacement по-прежнему исполняет motor; Animator
root motion отключён.

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
    class ICharacterReactionState {
        +BlocksActions bool
    }
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
    PlayerCombatController --> ICharacterReactionState : entry gate
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

Сначала buffered command проверяется как transition текущего action. Если его
окно не открылось, но команда не истекла к завершению timeline, controller
проверяет её как новый graph entry в актуальном environment. Так recovery input
не теряется, при этом недопустимый ground/air action и cooldown по-прежнему
отсекаются gameplay graph до запуска presentation.

Для ground/air combo presentation вызов содержит отдельный semantic track,
`ComboIndex` и
`CombatActionDefinition.DurationSeconds`. Это не переносит authority в Animator:
длительность остаётся метаданными ожидаемого playback. Текущий XController
воспроизводит authored clip speed и не выводит gameplay timeline из normalized
time Animator.

Для named presentation controller передаёт отдельный `PlayerCombatAnimationId`
и ту же ожидаемую gameplay-длительность. Наземный `attack.heavy` отображается
существующим `Attack/BaseAttack4`, а `skill.special` — `Attack/Skill`. ПКМ/RT
выдают semantic command тяжёлой атаки; клавиша `3`/D-pad Down — special,
независимо от control scheme.

Hitbox открывается только по `CombatActionFrame` и закрывается при completion, cancellation, interruption и disable. Каждое фактически запущенное action получает отдельный `CombatAttackExecution`, поэтому повторные hurtbox одной цели фильтруются на уровне атаки, а не оружия. Weapon transition применяет loadout только после commit point. Cooldown хранится по `PlayerCombatActionId`; animation lookup использует отдельный `PlayerCombatAnimationId`.

Gameplay timeline использует scaled delta time, поэтому execution, hitbox/cancel windows, обычный Animator и character physics замораживаются во время hit stop. Input buffer намеренно использует unscaled clock: команды продолжают приниматься и истекают в реальном времени. Полная time policy описана в `Docs/Architecture/Combat.HitStop.md`.

## Lifecycle

- `Enable` запускает state machine в `Ready` и симметрично подписывает semantic input, dodge и state events.
- `Disable` отменяет UniTask action, aborts executor, очищает buffer, закрывает hitbox, снимает callbacks и останавливает state machine.
- State machine и её states создаются scoped-регистрациями VContainer в `PlayerCombatComposition`; controller не вызывает `Resolve` и не владеет container.
- Animator, graph authoring и input buffer не являются владельцами combat phase.
- Входящая реакция не входит в combat state machine. `PlayerActorController` прерывает executing/recovery/weapon-switch action по подтверждённому reaction state, а `PlayerCombatController` читает только `ICharacterReactionState.BlocksActions`, чтобы не принять новый action во время hit-stun/knockdown/airborne/death. Владельцем этих состояний остаётся `CharacterReactionController`.

`IPlayerCombatActor.CanAttack` допускает только обычные walking/falling movement
modes и по-прежнему блокирует crouch и dodge. Поэтому воздушная ветка доступна
после реального перехода locomotion в `Falling`, а climbing/custom modes не обходят
actor gate.
