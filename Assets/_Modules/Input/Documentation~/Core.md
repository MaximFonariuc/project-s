# Modular Input — Core

## Ответственность

Core определяет язык ввода, независимый от конкретного SDK: стабильные идентификаторы action/map, типизированное значение, фазу, источник и устройство. Он маршрутизирует несколько источников и оставляет активным последнее актуальное значение каждого action.

Core не знает о `InputActionAsset`, Player, UI, DI container, сохранениях или конкретных control paths.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class IInputReader {
        <<interface>>
        +SignalChanged
        +TryReadValue(actionId, value) bool
    }
    class IInputMapController {
        <<interface>>
        +SetMapEnabled(mapId, enabled) bool
        +DisableAllMaps()
    }
    class IInputBindingOverrides {
        <<interface>>
        +ApplyBindingOverride(actionId, index, path) bool
        +RemoveBindingOverride(actionId, index) bool
        +SaveBindingOverrides() string
        +LoadBindingOverrides(json, removeExisting) bool
    }
    class IInputSource {
        <<interface>>
        +SourceId
        +SignalEmitted
        +Start()
        +Stop()
    }
    class IInputService {
        <<interface>>
        +AttachSource(source) bool
        +DetachSource(sourceId) bool
        +Start()
        +Stop()
    }
    class InputRouter {
        -sources
        -sourceStates
        -currentSignals
    }
    class InputSignal {
        +SourceId
        +MapId
        +ActionId
        +Value
        +Phase
        +DeviceKind
        +Time
        +Frame
    }
    class InputValue {
        +Kind
        +AsButton() bool
        +AsAxis() float
        +AsVector2() Vector2
        +AsVector3() Vector3
        +AsQuaternion() Quaternion
    }
    class InputActionId
    class InputMapId
    class InputSourceId

    IInputReader <|-- IInputService
    IInputMapController <|-- IInputService
    IInputBindingOverrides <|-- IInputService
    IInputMapController <|-- IInputSource
    IInputService <|.. InputRouter
    InputRouter o-- IInputSource
    InputRouter --> InputSignal
    InputSignal --> InputValue
    InputSignal --> InputActionId
    InputSignal --> InputMapId
    InputSignal --> InputSourceId
```

## Зависимости

```mermaid
flowchart LR
    Consumer["Game/UI consumer"] --> Reader["IInputReader"]
    Router["InputRouter"] --> Source["IInputSource"]
    Router -.implements.-> Reader
    Core["ModularInput.Core"] --> UnityEngine["UnityEngine value types"]
    Core -.no dependency.-> UnityInput["Unity Input System"]
    Core -.no dependency.-> ProjectS["Project S"]
    Core -.no dependency.-> DI["VContainer"]
```

- `InputActionId` и `InputMapId` используют GUID. Переименование action/map в authoring asset не ломает потребителя.
- `InputSignal` — завершённый факт от источника; он содержит время и кадр, поэтому combat buffer не обязан угадывать порядок `Update`.
- `InputRouter` является владельцем подписок на sources и очищает их при `Stop`/`Dispose`.
- Несколько источников можно подключать динамически. Для одного action побеждает последний активный source; отмена одного source восстанавливает ещё активный сигнал другого.
- Binding persistence наружу представлен строкой JSON. Где и как её хранить, решает Save-модуль; Input не зависит от него.

## Правила расширения

- Новый backend реализует `IInputSource`; Core менять не нужно.
- Новый тип gameplay-команды создаётся в адаптере конкретной игры, а не в Core.
- Новый action не требует нового класса в модуле: source публикует его GUID автоматически.
- Новая политика объединения источников добавляется отдельной реализацией `IInputService`, не условными ветками внутри gameplay.
