# Project S — Combat Motion

## Ответственность

Combat Motion управляет перемещением владельца во время конкретного combat action. Он решает только три задачи:

- независимо управляет планарной скоростью, вертикальной скоростью и одноразовым вертикальным импульсом в нормализованном окне action timeline;
- при включённой опции направляет движение и поворот к текущей lock-on цели;
- передаёт результат в character motor, который применяет его внутри fixed-step до collision-resolved `CharacterMovement.Move`.

Обычный input locomotion продолжает формировать `PlayerMovementIntent`. В активном motion window combat action может заменить планарную скорость либо сохранить её, а также сохранить, заменить или ограничить снизу вертикальную скорость. Gravity продолжает рассчитываться `Character`, после чего action policy применяется в одном fixed-step adapter. Grounding, slopes и collision response остаются собственностью `Character`/`CharacterMovement`.

## Дерево классов и зависимости

```mermaid
classDiagram
    direction LR

    class CombatActionAuthoring {
        +UseCombatMotion bool
        +CombatMotionStart float
        +CombatMotionEnd float
        +OverridePlanarVelocity bool
        +CombatMotionDistance float
        +VerticalMotionMode CharacterActionVerticalMode
        +CombatVerticalVelocity float
        +CombatVerticalImpulse float
        +FaceCombatTarget bool
        +TargetStoppingDistance float
    }
    class CombatActionDefinition {
        <<immutable action data>>
        +Motion CombatMotionDefinition
        +RootMotionPolicy RootMotionPolicy
    }
    class CombatMotionDefinition {
        <<immutable value>>
        +Window NormalizedActionWindow
        +OverridePlanarVelocity bool
        +PlanarDistance float
        +VerticalMode CharacterActionVerticalMode
        +VerticalVelocity float
        +VerticalImpulse float
        +FaceTarget bool
        +TargetStoppingDistance float
    }
    class CombatActionExecutor {
        +ExecuteAsync(definition, duration)
        +Abort()
    }
    class CombatMotionController {
        +Begin(definition, actionDuration)
        +Advance(normalizedTime)
        +End()
    }
    class IPlayerTargetingState {
        <<read port>>
        +IsTargetLocked bool
        +CurrentTarget Transform
        +AimPosition Vector3
    }
    class ICharacterActionMotionMotor {
        <<character port>>
        +Position Vector3
        +Forward Vector3
        +Up Vector3
        +SetActionMotion(intent)
        +QueueActionVerticalImpulse(velocity, override)
        +ClearActionMotion()
    }
    class CharacterActionMotionIntent {
        <<immutable value>>
        +PlanarMode CharacterActionPlanarMode
        +PlanarVelocity Vector3
        +VerticalMode CharacterActionVerticalMode
        +VerticalVelocity float
        +FacingDirection Vector3
    }
    class CharacterMotor {
        +OnAfterSimulationUpdated(deltaTime)
    }
    class Character {
        +AfterSimulationUpdated event
        +SetVelocity(velocity)
        +SetRotation(rotation)
    }
    class CharacterMovement {
        +Move(fixedDeltaTime)
    }
    class PlayerCombatComposition {
        <<VContainer composition root>>
    }
    class PlayerLocomotionComposition {
        <<VContainer composition root>>
    }

    CombatActionAuthoring ..> CombatMotionDefinition : builds
    CombatActionDefinition *-- CombatMotionDefinition
    CombatActionExecutor --> CombatMotionController : timeline lifecycle
    CombatMotionController --> IPlayerTargetingState : optional target assist
    CombatMotionController --> ICharacterActionMotionMotor : semantic motion request
    CharacterMotor ..|> ICharacterActionMotionMotor
    ICharacterActionMotionMotor ..> CharacterActionMotionIntent
    CharacterMotor --> Character : fixed-step adapter
    Character --> CharacterMotor : AfterSimulationUpdated
    Character --> CharacterMovement : collision simulation
    PlayerCombatComposition ..> CombatMotionController : registers
    PlayerLocomotionComposition ..> CharacterMotor : registers once for all ports
```

`ICharacterActionMotionMotor` — нейтральная сменяемая граница Character, которую может использовать combat или другой краткоживущий action. Combat не знает о `Rigidbody`, `CharacterMovement`, friction, ground probing и Unity lifecycle. `CharacterMotor` реализует эту границу рядом с остальными адаптерами движения, но остаётся единственным scoped instance благодаря одной VContainer registration.

## Поток выполнения

```mermaid
sequenceDiagram
    participant Action as CombatActionExecutor
    participant Motion as CombatMotionController
    participant Target as IPlayerTargetingState
    participant Motor as CharacterMotor
    participant Character
    participant Physics as CharacterMovement

    Action->>Motion: Begin(definition, animationDuration)
    loop scaled action timeline
        Action->>Motion: Advance(normalizedTime)
        Motion->>Target: read current lock-on state
        opt first frame inside window
            Motion->>Motor: QueueActionVerticalImpulse(velocity, override)
        end
        Motion->>Motor: SetActionMotion(intent)
    end
    Character-->>Motor: AfterSimulationUpdated(fixedDeltaTime)
    Motor->>Character: apply planar and vertical policies
    Motor->>Character: consume pending vertical impulse once
    Motor->>Character: rotate towards requested facing
    Character->>Physics: Move(fixedDeltaTime)
    Action->>Motion: End()
    Motion->>Motor: ClearActionMotion()
```

Timeline использует scaled `ICombatClock`: во время hit stop окно и скорость атаки не продолжают жить в реальном времени. Physics применяет сохранённый intent только на fixed-step; при полном freeze fixed simulation не выполняется.

## Authoring и правила

- `Override Planar Velocity` явно выбирает владельца горизонтальной скорости. Когда опция выключена, сохраняется результат обычной locomotion; когда включена, `Combat Motion Distance` преобразуется в скорость как `distance / motion window duration`.
- `Vertical Motion Mode = PreserveVelocity` оставляет gravity/jump velocity без изменений.
- `OverrideVelocity` задаёт точную `Combat Vertical Velocity` на каждом fixed-step окна. Значение `0` полностью удерживает высоту, положительное поднимает персонажа, отрицательное задаёт контролируемое снижение.
- `MinimumVelocity` вычисляет `max(current, configured)`: значение `0` сохраняет существующий подъём, но не позволяет начать падение; отрицательное значение, например `-2`, создаёт ограниченное медленное падение.
- `Combat Vertical Impulse` — знаковая одноразовая скорость на первом fixed-step окна. Положительное значение запускает персонажа вверх, отрицательное поддерживает slam вниз. `Override Vertical Velocity On Impulse` выбирает замену текущей вертикальной скорости либо сложение с ней.
- Положительная итоговая vertical velocity на земле использует существующий character jump boundary: motor снимает ground constraint и переводит movement mode в `Falling`, поэтому подъём не теряется при ground projection.
- `Face Combat Target` использует только подтверждённую lock-on цель. Без цели сохраняется движение вперёд по направлению персонажа.
- `Target Stopping Distance` останавливает поступательное движение рядом с lock-on целью, но позволяет продолжить разворот к ней.
- Окно motion обязано завершаться не позже `Completion Time`.
- Translational root motion (`Translation`, `Full`) нельзя объединять с planar override, vertical policy или vertical impulse одного action.
- Rotational root motion (`Rotation`, `Full`) нельзя объединять с `Face Combat Target` одного action.
- Новые vertical-поля имеют безопасные defaults: `PreserveVelocity` и impulse `0`. Prototype combo сохраняет окно `0.0–0.3` и планарную дистанцию `0.75`; prototype skill — `1.0`; weapon transition не использует combat motion.
- Финальные воздушные actions и clips намеренно не создаются: текущий prototype graph содержит только `Grounded` actions. Любой authoring action с environment `Airborne` уже может использовать launch, suspension и controlled fall через эту же модель.

## Lifecycle и отказоустойчивость

- `CombatActionExecutor` начинает и завершает motion в том же `try/finally`, где восстанавливает root motion и damage immunity.
- `Abort`, interruption, cancellation и disable игрока всегда вызывают `CombatMotionController.End`.
- Вертикальный impulse сначала ставится в очередь `CharacterMotor` и потребляется один раз только внутри fixed-step. Cancellation/disable до этого шага очищает очередь и не оставляет отложенный launch.
- `CharacterMotor.Dispose` очищает intent, pending impulse и снимает fixed-step subscription.
- Пустая или потерянная цель безопасно заменяется текущим forward персонажа.
- Scene, prefab, Animator Controller и animation clips для этой границы не изменяются.

## Почему это остаётся в Core

Алгоритм уже отделён портом `ICharacterActionMotionMotor`, но его data model пока напрямую использует `CombatActionDefinition`, `IPlayerTargetingState` и правила Project S. Реального второго consumer ещё нет, поэтому перенос в `_App/Modules` сейчас создал бы преждевременный package API. Кандидатом на модуль станет только стабильное ядро motion profile/window после применения к нескольким типам действий или персонажей без ссылок на Project S.
