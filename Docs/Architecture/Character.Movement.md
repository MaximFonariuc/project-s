# Character Movement

## Назначение

`CharacterMovement` — низкоуровневый кинематический motor на базе `Rigidbody` и `CapsuleCollider`.
Он принимает world-space velocity, выполняет collision/depenetration, определяет опору, учитывает moving
platforms и публикует подтверждённый результат движения. Семантический input, locomotion states, combat,
Animator и Cinemachine в это дерево не входят.

Публичный API и serialized-поля исходного компонента сохранены. Это позволяет сценам, prefab references,
`Character` и `CharacterMotor` продолжать использовать тот же Unity-компонент после внутреннего рефакторинга.

## Дерево классов

```mermaid
classDiagram
    class CharacterMovement {
        <<MonoBehaviour / Facade>>
        +Move(velocity, deltaTime) CollisionFlags
        +FindGround(position) FindGroundResult
        +OverlapTest(...) Collider[]
        +AddForce(force, mode)
        +LaunchCharacter(velocity, ...)
        +AttachTo(platform)
    }

    class CharacterCapsuleShape {
        <<runtime state>>
        +Resize(radius, height, collider)
        +UpdateRotation(rotation)
    }

    class CharacterCollisionFilter {
        <<policy state>>
        +ShouldFilter(collider, callback) bool
        +SetIgnored(collider, ignored)
        +SetIgnored(rigidbody, ignored)
    }

    class CharacterPhysicsQueries {
        <<physics adapter>>
        +OverlapCapsule(...) int
        +Raycast(...) bool
        +CapsuleCast(...) bool
        +BoxCast(...) bool
    }

    class CharacterCollisionBuffer {
        <<frame state>>
        +Count int
        +Flags CollisionFlags
        +Add(result, platform)
        +Clear()
    }

    class CharacterGroundingState {
        <<runtime state owner>>
        +CurrentGround FindGroundResult
        +FoundGround FindGroundResult
        +IsGrounded bool
        +RecordLanding(velocity)
        +UpdateCurrent(result)
    }

    class CharacterMovingPlatformTracker {
        <<runtime state owner>>
        +State MovingPlatform
        +AttachTo(platform)
        +RefreshAttachment(...)
        +CalculateVelocity(...) Vector3
    }

    class CharacterForceAccumulator {
        <<runtime state owner>>
        +Add(force, mode, mass)
        +Launch(...)
        +Consume(velocity, deltaTime) Vector3
    }

    class Character {
        <<gameplay movement modes>>
    }

    class CharacterMotor {
        <<locomotion adapter>>
    }

    CharacterMotor --> Character : semantic locomotion commands
    Character --> CharacterMovement : velocity and movement mode
    CharacterMovement *-- CharacterCapsuleShape
    CharacterMovement *-- CharacterCollisionFilter
    CharacterMovement *-- CharacterPhysicsQueries
    CharacterMovement *-- CharacterCollisionBuffer
    CharacterMovement *-- CharacterGroundingState
    CharacterMovement *-- CharacterMovingPlatformTracker
    CharacterMovement *-- CharacterForceAccumulator
    CharacterPhysicsQueries --> CharacterCollisionFilter
```

## Файлы solver

`CharacterMovement` остаётся одной runtime-сущностью, но численная реализация разделена по ответственности:

- `CharacterMovement.cs` — serialized configuration, Unity lifecycle, публичный фасад и orchestration одного move;
- `CharacterMovement.Collision.cs` — collision queries, depenetration, slide solver и формирование `CollisionResult`;
- `CharacterMovement.Grounding.cs` — ground sweeps, perch/step rules и ground height constraint;
- `CharacterMovement.Platforms.cs` — platform displacement и dynamic-body interaction;
- `CharacterMovement.Math.cs` — geometric normals и velocity math;
- `CharacterMovement.Editor.cs` — только editor visualization, полностью исключённая из player build.

Это `partial`-граница одной реализации, а не отдельные модули. Методы solver имеют общий набор инвариантов
одного movement tick, поэтому интерфейсы между каждым алгоритмом добавили бы связность и передачу mutable
context вместо реальной сменяемости.

## Владение состоянием

- `CharacterMovement` владеет Unity references, serialized configuration, position/rotation и итоговой velocity.
- `CharacterCapsuleShape` — единственный owner локальной и повёрнутой геометрии capsule.
- `CharacterCollisionFilter` — единственный owner runtime ignore sets.
- `CharacterCollisionBuffer` — единственный owner frame-local collisions и `CollisionFlags`.
- `CharacterGroundingState` — единственный owner current/previous/candidate ground и landing transition.
- `CharacterMovingPlatformTracker` — единственный owner platform attachment и platform deltas.
- `CharacterForceAccumulator` — единственный owner pending force/impulse/launch до следующего `Move`.
- `Advanced` и другие serialized данные остаются authoring configuration, но не становятся global state.

Все runtime collaborators создаются самим `CharacterMovement`: у них тот же lifecycle, нет scene references и
нет причины регистрировать их в VContainer. DI применяется выше, на границе `ICharacterMotor`/locomotion, где
реализация действительно сменяема.

## Зависимости и правила использования

Разрешённое направление:

```text
Input -> PlayerLocomotionController -> ICharacterMotor -> CharacterMotor
                                                     -> Character -> CharacterMovement
```

- Gameplay передаёт velocity/intents через `Character`/`ICharacterMotor`, а не вызывает внутренний solver.
- `CharacterMovement` не читает Input System и не знает о locomotion/combat states.
- Animation и camera adapters читают подтверждённое состояние верхнего locomotion-слоя.
- Combat не получает доступ к внутренним collaborators. Для рывка/knockback используются ограниченные
  команды motor/`Character`, а не изменение grounding/platform state.
- `Internal/*` не является публичным extension API. Новый behaviour добавляется через существующие callbacks
  (`ColliderFilter`, `CollisionBehaviour`, `CollisionResponse`) либо отдельное архитектурное решение.

## Lifecycle одного шага

```mermaid
sequenceDiagram
    participant C as Character
    participant M as CharacterMovement
    participant P as PlatformTracker
    participant S as Collision/Ground Solver
    participant G as GroundingState

    C->>M: Move(velocity, fixedDeltaTime)
    M->>M: refresh pose/capsule and consume forces
    M->>P: calculate platform delta
    M->>S: depenetrate, sweep, slide, step
    S-->>M: position + collision results
    M->>S: find/adjust ground
    M->>G: commit current ground
    M->>P: refresh attachment
    M->>M: resolve dynamic bodies and commit Rigidbody pose
    M-->>C: CollisionFlags + events
```

## Инварианты

- Симуляция выполняется только через `Move` в fixed-step владельца `Character`.
- Physics queries используют заранее выделенные buffers и один collision filter.
- Serialized field names не менять без `FormerlySerializedAs` и проверки scene/prefab migration.
- `Rigidbody` остаётся kinematic, `useGravity` выключен; gravity интегрирует `Character`.
- Platform displacement добавляется к character displacement; при сходе сохранённая скорость платформы
  добавляется один раз, а при входе velocity персонажа не вычитает platform velocity.
- Ground height correction обновляет измеренную дистанцию дельтой позиции; landing не обнуляет
  `GroundDistance`, когда blocking contact не позволяет выполнить всю коррекцию вниз.
- `FindGroundResult`, `CollisionResult` и `MovingPlatform` — snapshot/value types, не owners состояния.
- `UnityEditor` API разрешён только в `CharacterMovement.Editor.cs` под `UNITY_EDITOR`.
- `Internal`-классы не регистрировать в VContainer и не выдавать наружу.

## Ручная проверка после изменений

- idle на ровной поверхности: grounded, стабильная ground distance;
- run и остановка, wall collision, узкое препятствие;
- jump/fall/landing и повторный прыжок;
- slope limit, edge/perch и step offset;
- dodge при высоком и низком frame rate;
- moving platform: вход, движение/поворот, сход и impart velocity;
- force/impulse/launch, disable/enable и повторный вход в gameplay scene;
- отсутствие новых compile errors и runtime exceptions.
