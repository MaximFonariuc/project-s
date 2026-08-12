# Project S — Player Input adapter

## Ответственность

Project S не получает raw controls. `PlayerInputAdapter` переводит стабильные GUID действий из generic Input-модуля в команды уровня игры: attack, dodge, jump, конкретный skill. Это anti-corruption layer между переиспользуемой инфраструктурой и gameplay.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class IPlayerInput {
        <<Project S port>>
        +Move Vector2
        +Look Vector2
        +Zoom Vector2
        +AttackRequested
        +JumpChanged
        +SkillRequested
        +Enable()
        +Disable()
    }
    class PlayerInputAdapter {
        -IInputService input
        +ActiveDevice
    }
    class PlayerInputIds {
        <<Project S configuration>>
        +PlayerMap
        +Move
        +Attack
        +Dodge
        +SkillBoneCrash
        +SkillFlashStance
    }
    class IInputService {
        <<Modular Input port>>
    }
    class ThirdPersonController {
        <<Gameplay consumer>>
    }
    class GameLifetimeScope {
        <<Composition Root>>
    }
    class UnityInputSource {
        <<Modular Input adapter>>
    }

    IPlayerInput <|.. PlayerInputAdapter
    PlayerInputAdapter --> IInputService
    PlayerInputAdapter --> PlayerInputIds
    ThirdPersonController --> IPlayerInput
    GameLifetimeScope ..> PlayerInputAdapter : constructs
    GameLifetimeScope ..> UnityInputSource : constructs
```

## Направление зависимостей

```mermaid
flowchart LR
    Controller["ThirdPersonController"] --> Port["IPlayerInput"]
    Adapter["PlayerInputAdapter"] -.implements.-> Port
    Adapter --> CorePort["IInputService"]
    Composition["GameLifetimeScope"] --> Adapter
    Composition --> UnitySource["UnityInputSource"]
    UnitySource --> Asset["ProjectSInput.inputactions"]
```

- Gameplay зависит только от `IPlayerInput` и не знает о devices, bindings или Unity callback context.
- `PlayerInputIds` — project configuration. GUID остаются стабильными после rename action/map.
- `GameLifetimeScope` — единственное место, где встречаются VContainer, Unity source и project asset.
- UI prompts могут подписаться на `ActiveDeviceChanged`, не выполняя device polling.

## От чего отказались

- сгенерированный `InputSystem.cs` удалён: добавление action больше не требует wrapper regeneration;
- `InputHandler` и изменяемые delegate-properties удалены;
- gameplay больше не создаёт input через `new` и не имеет fallback вне composition root;
- skill больше не определяется через `context.control.name`;
- `PlayerInput` MonoBehaviour не дублирует lifecycle action asset.
- неиспользуемые prototype controllers с прямым `UnityEngine.Input` удалены; editor `TestSystem` больше не выполняет device polling.
