# Project S — Player Traversal

## Ответственность

`PlayerTraversalController` владеет application flow перемещений вне базовой locomotion state machine: взаимодействием с лестницей, передачей climb input, снятием оружия перед входом на лестницу и согласованным разворотом gravity/camera orientation. Начало traversal разрешено только в `Combat.Ready` и при стабильной reaction state. Он не читает Unity devices, не выполняет movement simulation и не управляет Cinemachine или Animator напрямую.

Локальная команда Pause вынесена в `PlayerSimulationPauseController`. Это пауза только симуляции `Character`, а не глобальная пауза игры, UI или времени. Такое название намеренно не обещает будущую pause-menu архитектуру.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class IPlayerTraversalCommands {
        <<command port>>
        +TryInteract() bool
        +TryInvertGravity() bool
    }
    class PlayerTraversalController {
        +Enable()
        +Disable()
        +Tick()
        +Interrupt()
    }
    class IPlayerInput {
        <<semantic input port>>
        +Move Vector2
        +InteractRequested
        +InverseGravityRequested
        +PauseRequested
    }
    class IPlayerTraversalActor {
        <<actor state port>>
        +IsClimbing bool
    }
    class IPlayerInteractionProbe {
        <<interaction query port>>
        +HasInteractable() bool
    }
    class CharacterInteractionProbe {
        <<legacy physics adapter>>
    }
    class IPlayerTraversalActions {
        <<action port>>
        +IsClimbAvailable bool
        +ApplyClimbInput(input)
        +TryBeginClimb() bool
        +Interrupt()
    }
    class PlayerTraversalActions {
        <<action owner>>
        +Enable()
        +Disable()
    }
    class LadderTraversalAction
    class PlayerAnimationCoordinator
    class LadderTraversalSettings {
        <<authoring>>
    }
    class IPlayerCombatCommands
    class IPlayerCombatState
    class ICharacterReactionState {
        +BlocksActions bool
    }
    class IPlayerLocomotionCommands {
        +TryInvertGravity() bool
    }
    class IPlayerCameraCommands {
        +ToggleGravityOrientation()
    }
    class IPlayerSimulationLock {
        +AcquirePause() IDisposable
    }
    class IPlayerSimulationPause {
        +IsPauseRequested bool
        +SetPaused(paused)
    }
    class PlayerSimulationGate {
        <<pause owner>>
    }
    class PlayerSimulationPauseController
    class Character
    class ThirdPersonController {
        <<Unity lifecycle host>>
    }

    IPlayerTraversalCommands <|.. PlayerTraversalController
    PlayerTraversalController --> IPlayerInput
    PlayerTraversalController --> IPlayerTraversalActor
    PlayerTraversalController --> IPlayerInteractionProbe
    PlayerTraversalController --> IPlayerTraversalActions
    PlayerTraversalController --> IPlayerCombatCommands
    PlayerTraversalController --> IPlayerCombatState
    PlayerTraversalController --> ICharacterReactionState : entry gate
    PlayerTraversalController --> IPlayerLocomotionCommands
    PlayerTraversalController --> IPlayerCameraCommands
    PlayerTraversalController --> IPlayerSimulationLock
    IPlayerTraversalActions <|.. PlayerTraversalActions
    PlayerTraversalActions o-- LadderTraversalAction
    LadderTraversalAction --> PlayerAnimationCoordinator : traversal requests
    PlayerTraversalActions ..> LadderTraversalSettings : reads once
    IPlayerInteractionProbe <|.. CharacterInteractionProbe
    CharacterInteractionProbe --> Character
    IPlayerSimulationLock <|.. PlayerSimulationGate
    IPlayerSimulationPause <|.. PlayerSimulationGate
    PlayerSimulationGate --> Character
    PlayerSimulationPauseController --> IPlayerInput
    PlayerSimulationPauseController --> IPlayerSimulationPause
    ThirdPersonController --> PlayerTraversalController : Tick / lifecycle
    ThirdPersonController --> PlayerSimulationPauseController : lifecycle
```

## Interaction flow

```mermaid
sequenceDiagram
    participant Input as IPlayerInput
    participant Traversal as PlayerTraversalController
    participant Probe as IPlayerInteractionProbe
    participant Gate as PlayerSimulationGate
    participant Combat as IPlayerCombatCommands
    participant Actions as IPlayerTraversalActions

    Input->>Traversal: InteractRequested
    Traversal->>Traversal: require Stable reaction + Combat.Ready
    Traversal->>Probe: HasInteractable()
    alt weapon is sheathed
        Traversal->>Actions: TryBeginClimb()
    else weapon is equipped
        Traversal->>Gate: AcquirePause()
        Traversal->>Combat: ToggleWeaponAsync(Unarmed)
        Combat-->>Traversal: confirmed
        Traversal->>Actions: TryBeginClimb()
        Traversal->>Gate: Dispose pause lease
    end
```

Повторный Interact во время sheathe блокируется владельцем workflow. Reaction interruption инвалидирует ожидающую sheathe-операцию, освобождает pause lease, отменяет ladder UniTask и восстанавливает movement mode/constraints. После этого создаётся новый lifecycle token, поэтому traversal остаётся доступным после возврата reaction в `Stable`. Disable выполняет тот же cleanup без повторного запуска lifecycle; завершившийся позже async continuation не запускает climb и не меняет состояние нового lifecycle.

`CharacterInteractionProbe` сохраняет текущую физическую семантику: `CharacterMovement.OverlapTest`, первый collider и `IInteractable`. Это legacy adapter, а не стабильная универсальная interaction system. Layer mask и выбор лучшего кандидата должны стать отдельной authoring policy только вместе с реальными типами взаимодействий.

`PlayerTraversalController` делегирует конкретную механику в `IPlayerTraversalActions`. `PlayerTraversalActions` создаёт и владеет `LadderTraversalAction`, читая `LadderTraversalSettings` один раз при composition. Общего ability runtime и lookup по concrete type нет. `Interrupt` и `Disable` отменяют активные async ladder operations; отменённый step освобождает только собственный token и не может уничтожить token новой операции.

## Simulation pause ownership

`PlayerSimulationGate` — единственная новая точка, которая вызывает `Character.Pause` в player flow. Постоянный пользовательский pause request и временные locks хранятся раздельно:

- `PlayerSimulationPauseController` меняет persistent request;
- traversal получает disposable lease на время sheathe;
- фактическая симуляция возобновляется только когда request снят и все leases освобождены.

При обычном `Disable` pause controller снимает persistent request до разрушения Unity-компонентов, а traversal освобождает свои leases. `PlayerSimulationGate.Dispose` только инвалидирует scoped state и не вызывает `Character.Pause`: к моменту container teardown Unity уже может уничтожить `CharacterMovement`.

Это предотвращает ситуацию, когда завершение traversal случайно снимает паузу, установленную другим владельцем. Глобальный pause menu в будущем должен иметь отдельный owner времени и action maps; текущий gate не следует расширять до глобального service locator.

## Gravity inversion

Traversal вызывает `IPlayerLocomotionCommands.TryInvertGravity`. Только после подтверждения motor он отправляет `ToggleGravityOrientation` через camera command port. Camera и locomotion не получают concrete-ссылки друг на друга, а orientation presentation не расходится с gameplay gravity при отклонённой команде.

## ThirdPersonController и lifecycle

`ThirdPersonController` теперь содержит serialized authoring references, порядок `Enable/Disable`, frame ticks и compatibility entry point `InverseGravity`. Он больше не:

- подписывается на Interact/Pause/InverseGravity input;
- выполняет physics interaction query;
- знает о `Character` и climbing state;
- владеет async sheathe-and-climb workflow;
- соединяет `PhysicsVolumeChanged` с animation напрямую.

Последняя связь вынесена в `PlayerPhysicsVolumeAnimationAdapter`, который симметрично подписывается и отписывается вместе с lifecycle host.

`PlayerTraversalComposition` регистрирует probe, traversal controller, pause controller и один scoped `PlayerSimulationGate`. Scene/prefab wiring не требуется.

## Ladder presentation binding

`LadderTraversalAction` играет восемь слотов: вход сверху спереди и сзади, вход снизу,
idle, подъём, спуск, выход сверху и снизу. Они замаплены в `PlayerAnimatorActions` на
действие `LadderTraversal` с вариантами 0..7, а `XControllerAnimationContract.TraversalStatePaths`
задаёт соответствующие состояния `Base Layer.Ladder.*`. Длительности клипов авторятся в
`LadderTraversalSettings` и передаются в `CrossFade` как duration override — константы
длин в коде не хранятся.

`CanClimb` отказывает, если хотя бы один из восьми слотов не поддерживается бэкендом,
и один раз пишет предупреждение. Это защита: лазание захватывает movement mode,
rotation mode и ground constraint с первого кадра, поэтому отсутствующая презентация
должна отклонить действие до захвата, а не посреди него. Любая иная ошибка внутри
climb-потоков возвращает состояние через `AbortClimb`.

Проверено в play mode: вход снизу, переход в `Ladder_Idle` и подъём отрабатывают,
персонаж поднимается по лестнице с проигрыванием клипов.

**Незакрытое:** выход сверху не завершается. Фаза подъёма exit-последовательности
отрабатывает и поднимает персонажа на `topY + STEP_HEIGHT * 1.5`, но movement mode
остаётся `Custom/Climbing`, а `_climbingState` — `Grabbed`. Причина в самой
exit-последовательности, а не в привязке анимации: `Step` передаёт в
`PlayExitAndRelease` собственный step-token, время жизни которого короче
последовательности. До починки выход сверху считать неработающим.
