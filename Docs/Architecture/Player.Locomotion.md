# Project S — Player Locomotion

## Ответственность

Player Locomotion владеет интерпретацией семантического input и текущим locomotion-состоянием игрока. Физическое ядро `CharacterMovement` сохранено: оно выполняет движение capsule, ground/slope checks, step offset, depenetration, столкновения и moving-platform correction.

Внутренняя модель физического ядра описана отдельно в [Character.Movement.md](Character.Movement.md).

Граница специально остаётся внутри `_App`, пока не стабилизированы публичный API, настройки и traversal-сценарии. Сейчас это не UPM-пакет и не `_Modules/CharacterMotion`.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class IPlayerInput {
        <<input port>>
        +Move Vector2
        +WalkChanged
        +SprintChanged
        +JumpChanged
        +DodgeRequested
    }
    class PlayerLocomotionController {
        +CurrentState PlayerLocomotionStateId
        +Enable()
        +Disable()
        +SetMovementIntent(intent)
        +Tick(deltaTime)
        +TryInvertGravity()
    }
    class PlayerMovementIntent {
        +TranslationScale float
        +AllowJump bool
        +AllowDodge bool
        +FaceMoveDirection bool
    }
    class ICharacterMotor {
        <<motor port>>
        +IsFalling bool
        +IsDodging bool
        +Velocity Vector3
        +SetMovementDirection(direction)
        +PressJump()
        +TryDodge(direction, backwards)
        +SetSpeedMode(mode)
    }
    class CharacterMotor {
        <<adapter>>
    }
    class Character {
        <<simulation policy>>
    }
    class CharacterMovement {
        <<collision motor>>
    }
    class IPlayerMovementReference {
        <<camera basis port>>
        +ToWorldDirection(input, up, cameraRelative)
    }
    class CameraRelativeMovementReference {
        <<adapter>>
    }
    class ICameraOrientation {
        <<camera read port>>
        +Forward Vector3
        +Right Vector3
    }
    class IStateMachine~PlayerLocomotionStateId~
    class PlayerLocomotionState {
        <<abstract>>
        +Enter()
        +Tick(deltaTime)
        +Exit()
    }
    class PlayerIdleState
    class PlayerRunState
    class PlayerJumpState
    class PlayerFallState
    class PlayerDodgeState
    class IPlayerLocomotionPresentation {
        <<presentation port>>
        +EnterState(state, dodgeBackwards)
        +SetSpeedMode(mode)
        +Tick()
    }
    class PlayerLocomotionAnimationAdapter
    class AnimationController
    class GameLifetimeScope {
        <<composition root>>
    }

    PlayerLocomotionController --> IPlayerInput
    PlayerLocomotionController --> ICharacterMotor
    PlayerLocomotionController --> IPlayerMovementReference
    PlayerLocomotionController --> IStateMachine~PlayerLocomotionStateId~
    PlayerLocomotionController --> IPlayerLocomotionPresentation
    ICharacterMotor <|.. CharacterMotor
    CharacterMotor --> Character
    Character --> CharacterMovement
    IPlayerMovementReference <|.. CameraRelativeMovementReference
    CameraRelativeMovementReference --> ICameraOrientation
    IPlayerLocomotionPresentation <|.. PlayerLocomotionAnimationAdapter
    PlayerLocomotionAnimationAdapter --> AnimationController
    PlayerLocomotionState <|-- PlayerIdleState
    PlayerLocomotionState <|-- PlayerRunState
    PlayerLocomotionState <|-- PlayerJumpState
    PlayerLocomotionState <|-- PlayerFallState
    PlayerLocomotionState <|-- PlayerDodgeState
    GameLifetimeScope ..> PlayerLocomotionController : constructs
    GameLifetimeScope ..> CharacterMotor : constructs
    GameLifetimeScope ..> IStateMachine~PlayerLocomotionStateId~ : constructs
```

## Состояния

```mermaid
stateDiagram-v2
    [*] --> Idle
    Idle --> Run: effective move input
    Run --> Idle: no effective input
    Idle --> Jump: confirmed upward movement
    Run --> Jump: confirmed upward movement
    Jump --> Fall: vertical speed becomes downward
    Fall --> Jump: additional jump
    Fall --> Idle: landed without input
    Fall --> Run: landed with input
    Idle --> Dodge: accepted dodge
    Run --> Dodge: accepted dodge
    Dodge --> Idle: completed without input
    Dodge --> Run: completed with input
    Dodge --> Fall: left ground
```

`Jump` начинается не по нажатию кнопки, а после подтверждения motor: `Character` должен перейти в `Falling` и получить восходящую скорость. По той же причине `Dodge` становится активным только после перехода `Character` в custom movement mode. State machine не дублирует физическое состояние заранее.

`Walk` и `Sprint` являются взаимоисключающими speed modes, а не отдельными locomotion states. Единственный runtime-owner скорости — `CharacterMotor`; старые `WalkAbility` и `SprintAbility` используются только как источник уже настроенных значений скорости.

## Направление зависимостей

```mermaid
flowchart LR
    Input["IPlayerInput"] --> Controller["PlayerLocomotionController"]
    Combat["Combat"] -->|"PlayerMovementIntent"| Controller
    CameraBasis["IPlayerMovementReference"] --> Controller
    Controller --> Machine["Locomotion State Machine"]
    Machine --> MotorPort["ICharacterMotor"]
    Motor["CharacterMotor"] -.implements.-> MotorPort
    Motor --> Character
    Character --> Collision["CharacterMovement"]
    Machine --> Presentation["IPlayerLocomotionPresentation"]
    AnimationAdapter["Animation Adapter"] -.implements.-> Presentation
```

- Gameplay input не знает о клавиатуре, gamepad или bindings.
- Combat не вызывает `Character.SetMovementDirection`, `Jump` или `Dodge`. Он передаёт ограниченный intent: масштаб translation, разрешение jump/dodge и запрос разворота по input.
- Camera участвует только как basis для camera-relative направления через отдельный adapter; locomotion не зависит от Cinemachine API.
- Animation получает подтверждённое состояние. Продолжительность dodge задаётся gameplay-конфигурацией, а не длиной animation clip.
- `Character` симулируется с `Time.fixedDeltaTime`; presentation обновляется в frame `Update`.

## Lifecycle и ownership

`GameLifetimeScope` регистрирует один scoped-набор motor/controller/runtime/state machine для активного Player. `ThirdPersonController` остаётся Unity-host для serialized references и временным координатором traversal; Combat и Camera используют отдельные scoped controllers.

`PlayerLocomotionController.Enable` только подписывает semantic input. Первый motor snapshot и запуск state machine происходят в первом `Tick`, после завершения `Awake` Unity-компонентов. `Disable` симметрично снимает callbacks, освобождает jump/glide, сбрасывает speed mode и останавливает state machine.

## Текущая граница

В core locomotion входят `Idle / Run / Jump / Fall / Dodge`, camera-relative ground/air input, walk/run/sprint и variable jump/glide forwarding. Swimming и ladder traversal пока сохраняют существующие abilities и не объявлены частью стабильного API. Их следует мигрировать отдельным решением после стабилизации базового движения.

Кандидат на `_Modules/CharacterMotion` появляется только после того, как:

- `ICharacterMotor` перестанет зависеть от legacy ability-конфигурации;
- traversal и root motion получат явные контракты;
- параметры движения будут собраны в стабильную authoring model;
- модуль можно будет использовать без `ThirdPersonController`, Combat и assets Project S.
