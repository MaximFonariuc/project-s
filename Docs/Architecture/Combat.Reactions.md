# Project S — Character Reaction и Poise

## Ответственность

`CharacterReactionController` владеет реакцией конкретного получателя урона. Он слушает только локальные события `Health`, расходует poise, выбирает состояние реакции, прерывает текущее действие и передаёт импульс motor через ограниченный порт. Исходящий `CombatHitResolver`, `Weapon`, Animator и `Character` не владеют reaction state.

Сейчас система поддерживает:

- накопление `StaggerDamage`, задержку и скорость восстановления poise;
- `Stable`, `HitStun`, `Knockdown`, `Airborne` и `Dead`;
- повторное попадание во время активной реакции;
- блокировку player movement intent и прерывание текущей атаки/смены оружия;
- направленный knockback через `ICharacterImpactMotor`;
- возврат из временной реакции и явное восстановление после `Health.RestoreFull()`;
- опциональное отображение состояний через `ICharacterAnimationDriver`.

Hit stop, invulnerability frames, get-up phase, ragdoll и финальные reaction clips не входят в этот блок.

## Дерево классов и зависимости

```mermaid
classDiagram
    direction LR

    class Health {
        <<MonoBehaviour / vitality owner>>
        +Damaged event
        +Died event
        +Revived event
        +RestoreFull()
    }
    class CharacterReactionController {
        +CurrentState CharacterReactionStateId?
        +Snapshot CharacterReactionSnapshot
        +CurrentPoise float
        +BlocksActions bool
        +Enable()
        +Disable()
        +Tick(deltaTime)
    }
    class ICharacterReactionState {
        <<public read port>>
        +StateChanged event
        +ReactionStarted event
        +Snapshot CharacterReactionSnapshot
    }
    class CharacterPoise {
        +ApplyStagger(amount) bool
        +Tick(deltaTime)
        +RestoreFull()
    }
    class IStateMachine~CharacterReactionStateId~
    class CharacterStableState
    class CharacterHitStunState
    class CharacterKnockdownState
    class CharacterAirborneReactionState
    class CharacterDeadState
    class ICharacterImpactMotor {
        <<restricted movement port>>
        +IsGrounded bool
        +Up Vector3
        +ApplyKnockback(worldVelocity)
    }
    class CharacterMotor {
        <<Character adapter>>
    }
    class ICharacterReactionInterruption {
        <<action interruption port>>
        +InterruptCurrentAction()
    }
    class PlayerCombatReactionInterruption
    class IPlayerCombatCommands
    class ICharacterReactionPresentation {
        <<presentation port>>
        +Present(snapshot)
        +Reset()
    }
    class CharacterReactionAnimationAdapter
    class ICharacterAnimationDriver
    class CharacterReactionProfile {
        <<optional authoring data>>
    }
    class PlayerReactionComposition {
        <<VContainer composition root>>
    }

    CharacterReactionController ..|> ICharacterReactionState
    CharacterReactionController --> Health : target-local events
    CharacterReactionController --> CharacterPoise
    CharacterReactionController --> IStateMachine~CharacterReactionStateId~
    CharacterReactionController --> ICharacterImpactMotor
    CharacterReactionController --> ICharacterReactionInterruption
    CharacterReactionController --> ICharacterReactionPresentation
    IStateMachine~CharacterReactionStateId~ *-- CharacterStableState
    IStateMachine~CharacterReactionStateId~ *-- CharacterHitStunState
    IStateMachine~CharacterReactionStateId~ *-- CharacterKnockdownState
    IStateMachine~CharacterReactionStateId~ *-- CharacterAirborneReactionState
    IStateMachine~CharacterReactionStateId~ *-- CharacterDeadState
    CharacterMotor ..|> ICharacterImpactMotor
    PlayerCombatReactionInterruption ..|> ICharacterReactionInterruption
    PlayerCombatReactionInterruption --> IPlayerCombatCommands
    CharacterReactionAnimationAdapter ..|> ICharacterReactionPresentation
    CharacterReactionAnimationAdapter --> ICharacterAnimationDriver
    CharacterReactionAnimationAdapter --> CharacterReactionProfile : optional mapping
    PlayerReactionComposition ..> CharacterReactionController : constructs
```

## State machine

```mermaid
stateDiagram-v2
    [*] --> Stable
    Stable --> HitStun: poise broken
    Stable --> Knockdown: grounded strong knockback
    Stable --> Airborne: target not grounded
    HitStun --> HitStun: repeated hit
    HitStun --> Knockdown: stronger repeated hit
    HitStun --> Airborne: leaves ground
    Knockdown --> HitStun: repeated hit
    Knockdown --> Knockdown: repeated heavy hit
    Knockdown --> Airborne: leaves ground
    Airborne --> HitStun: grounded repeated hit
    Airborne --> Knockdown: grounded heavy hit
    Airborne --> Airborne: repeated air hit
    HitStun --> Stable: duration elapsed
    Knockdown --> Stable: duration elapsed
    Airborne --> Stable: duration elapsed
    Stable --> Dead: Health.Died
    HitStun --> Dead: Health.Died
    Knockdown --> Dead: Health.Died
    Airborne --> Dead: Health.Died
    Dead --> Stable: Health.Revived
```

Ненулевой knockback принудительно создаёт реакцию даже до полного расхода poise. На земле направление проецируется на плоскость персонажа; вертикальная составляющая текущей скорости сохраняется. Порог сильного импульса выбирает `Knockdown`. Если motor сообщает airborne, приоритет имеет `Airborne`.

## Поток подтверждённого попадания

```mermaid
sequenceDiagram
    participant Resolver as CombatHitResolver
    participant Health
    participant Reaction as CharacterReactionController
    participant Poise as CharacterPoise
    participant Combat as ICharacterReactionInterruption
    participant Motor as ICharacterImpactMotor
    participant View as ICharacterReactionPresentation

    Resolver->>Health: TryApplyHit(CombatHitResult)
    Health-->>Reaction: Damaged(result)
    Reaction->>Poise: ApplyStagger(StaggerDamage)
    alt poise broken or knockback present
        Reaction->>Combat: InterruptCurrentAction()
        Reaction->>Motor: ApplyKnockback(direction * force)
        Reaction->>Reaction: change reaction state
        Reaction->>View: Present(snapshot)
    end
    opt lethal result
        Health-->>Reaction: Died(result)
        Reaction->>Reaction: change to Dead
    end
```

`ICombatHitEvents` намеренно не используется для владения реакцией цели: это scoped broadcast исходящего resolver. Подписка непосредственно на `Health` гарантирует, что controller получает только события своего actor и пригоден для будущей отдельной composition противников.

## Authoring и presentation

`CharacterReactionProfile` опционален. При отсутствии asset используются безопасные prototype defaults: poise `10`, hit-stun `0.25s`, knockdown `0.8s`, airborne `0.45s`. Ни одного Animator state система в таком режиме не требует.

Когда финальные клипы появятся, profile связывает пару `ReactionId + State` с backend-neutral animation state. Пустой `ReactionId` является fallback для состояния. `CharacterReactionAnimationAdapter` только вызывает `ICharacterAnimationDriver`; он не завершает gameplay reaction по длине клипа и не меняет state machine. После возврата в `Stable` обычную позу снова отображает locomotion presentation.

## Lifecycle и границы

- `PlayerReactionComposition` создаёт settings, poise, state machine, ports и controller в gameplay scope. Gameplay-классы не вызывают `Resolve`.
- `Enable` подписывает `Health`, запускает state machine и синхронизируется с текущим alive/dead состоянием.
- `Disable` симметрично снимает подписки, останавливает machine, сбрасывает runtime state и presentation.
- `PlayerActorController` тикает reaction перед locomotion и при `BlocksActions` передаёт `PlayerMovementIntent.Locked`.
- `Health` остаётся единственным владельцем hit points; `CharacterPoise` — единственным владельцем poise; state machine — единственным владельцем reaction phase.
- Reaction не вызывает `Character` напрямую. Единственный путь физического воздействия — `ICharacterImpactMotor`, реализованный существующим `CharacterMotor`.
- Система остаётся в `_App/Core`: timings, состояния и связь с combat rules пока проверены только в Project S. Извлечение в `_App/Modules` преждевременно до появления второго реального consumer.
