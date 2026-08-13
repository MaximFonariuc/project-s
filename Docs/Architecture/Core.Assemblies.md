# Project S Core — границы assemblies

`Assets/_App/Core` содержит только код конкретной игры и разделён на две assembly: runtime и editor tooling. Граница нужна не ради количества проектов в IDE, а для проверки допустимого направления зависимостей самим Unity compiler.

## Физическая структура

```text
Assets/_App/Core/
├── Runtime/                         # ProjectS.Core.Runtime
│   ├── Character/                    # simulation, locomotion, traversal, movement policies
│   ├── Combat/
│   ├── Common/
│   ├── Composition/
│   ├── Diagnostics/
│   ├── Enemy/
│   ├── Enums/
│   ├── Environment/
│   ├── IK/
│   ├── Input/
│   ├── Settings/CharacterFeatures/   # immutable feature authoring data
│   └── TargetLock/
└── Editor/                          # ProjectS.Core.Editor
```

`ProjectS.Core.Runtime` остаётся одной feature-oriented assembly. Дробить её на assembly для каждой папки запрещено, пока конкретная feature не получила стабильный публичный контракт и независимый lifecycle.

## Направление зависимостей

```mermaid
flowchart LR
    Game["_Game scenes and prefabs"] -. "serialized components" .-> Runtime["ProjectS.Core.Runtime"]
    Editor["ProjectS.Core.Editor"] --> Runtime

    Runtime --> InputCore["ModularInput.Core"]
    Runtime --> InputUnity["ModularInput.Unity"]
    Runtime --> StateCore["ModularStateMachine.Core"]
    Runtime --> StateDI["ModularStateMachine.VContainer"]

    Runtime --> UniTask["UniTask"]
    Runtime --> Cinemachine["Unity.Cinemachine"]
    Runtime --> InputSystem["Unity.InputSystem"]
    Runtime --> VContainer["VContainer"]

    InputCore -. "forbidden" .-> Runtime
    InputUnity -. "forbidden" .-> Runtime
    StateCore -. "forbidden" .-> Runtime
    StateDI -. "forbidden" .-> Runtime
```

`_App/Modules` не ссылается на `ProjectS.Core.Runtime`. Сторонние `_Modules` рассматриваются как внешние зависимости; адаптеры Project S к ним должны находиться в Core.

Editor assembly может зависеть от Runtime, но Runtime не зависит от `ProjectS.Core.Editor`. Исключение — узкий код под `#if UNITY_EDITOR` внутри того же partial runtime-типа, когда перенос в другую assembly технически разорвал бы partial class. Сейчас таким исключением является `CharacterMovement.Editor.cs`.

## Текущие зависимости Runtime

- `ModularInput.Core` и `ModularInput.Unity` — семантический input и Unity Input System adapter;
- `ModularStateMachine.Core` и `ModularStateMachine.VContainer` — locomotion state machine и DI-регистрация;
- `VContainer` — composition root и injection;
- `UniTask` — существующие асинхронные gameplay-операции;
- `Unity.Cinemachine` — текущая camera/target-lock интеграция;
- `Unity.InputSystem` — создание Unity input source в composition root.

Новые assembly references добавляются только при фактическом использовании. `overrideReferences` отключён, поэтому Unity engine modules и совместимые precompiled plugins остаются доступны штатно.

## Зафиксированный технический долг

- `Diagnostics/TestSystem`, `ITestable` и `MonoSingleton` сохранены, потому что `TestSystem` сериализован в `Assets/Scenes/Demo Scene.unity`. Новый gameplay-код не должен использовать эту singleton-границу.
- `Common` содержит уже используемые малые helpers, ранее лежавшие в общей папке `Utils`. Новые универсальные helpers без доказанного общего владельца сюда не добавляются.
- `Enemy` и старый `TargetLock` продолжают компилироваться для сохранения проекта, но не входят в текущий приоритет single-player locomotion.
- Наличие одной Core assembly не означает, что все классы являются публичным API. Внутренние типы следует делать `internal` по мере локального рефакторинга и проверки всех consumers.

## Инварианты

- Runtime не содержит editor-only инструментов и не ссылается на `ProjectS.Core.Editor`.
- Editor tooling не попадает в player build.
- `_App/Modules` не получает references на Core.
- Перемещение скриптов выполняется с сохранением `.meta` GUID, поэтому scene/prefab references не меняются.
- После изменения references Unity должна завершить компиляцию без новых ошибок.
