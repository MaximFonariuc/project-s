# Project S — текущая архитектурная база

Project S остаётся feature-oriented monolith. MVC/MVVM не используются как архитектура gameplay; состояние принадлежит gameplay-компонентам и plain C# controllers/state machines, а Unity-компоненты служат lifecycle и presentation adapters. Для сложных UI-экранов по умолчанию применяется MVP Passive View.

## Физические границы проекта

- `Assets/_App/Core` — first-party код, который знает о Project S: gameplay, presentation adapters, composition и editor tooling.
- `Assets/_App/Modules` — наши независимые архитектурные модули. Сейчас здесь находятся `Input` и `StateMachine`; они не ссылаются на `Core` и `_Game`.
- `Assets/_Game` — сцены, prefabs, модели, анимации, материалы, настройки и другой игровой контент без first-party скриптов.
- `Assets/_Modules` — импортированные сторонние расширения. Интеграционный код Project S остаётся в `_App`, а vendor-код не изменяется без явно задокументированного patch.

Допустимое направление зависимостей: `_App/Core -> _App/Modules`. `_Game` связывает компоненты и content через Unity serialization; `_Modules` рассматриваются как внешние зависимости.

## Выделенные сущности и владельцы состояния

- **Player Actor** — `ThirdPersonController` остаётся Unity-host для serialized references и временным координатором ещё не выделенных Combat/Camera/Traversal features. Он больше не владеет базовым locomotion input.
- **Player Locomotion** — `PlayerLocomotionController` владеет semantic movement intent и машиной `Idle / Run / Jump / Fall / Dodge`. `CharacterMotor` является портом к существующим `Character` и `CharacterMovement`, не переписывая проверенную collision physics.
- **Player Input** — gameplay зависит от project-specific порта `IPlayerInput`. `PlayerInputAdapter` переводит сигналы независимого `IInputService` в семантические команды игрока, а `UnityInputSource` остаётся инфраструктурной деталью composition root.
- **Combatant vitality** — `Health` является единственным владельцем текущего здоровья и публикует подтверждённые события damage/death.
- **Damage boundary** — `Hurtbox` принимает попадание и передаёт его владельцу `Health`; `Weapon` формирует `DamageInfo` и фильтрует повторное попадание одной цели в рамках одного окна hitbox.
- **Animation presentation** — `AnimationController` отображает подтверждённое gameplay-состояние, но не владеет здоровьем и уроном.
- **Camera presentation** — Cinemachine остаётся presentation-слоем; gameplay сообщает intent через существующий controller.
- **Authoring data** — существующие `AbilitiesData` и `AnimationsData` остаются ScriptableObject-конфигурацией, а runtime progress хранится в экземплярах controllers/abilities.

## Composition

`GameLifetimeScope` — composition root gameplay-сцены. Он владеет scoped input adapter, Player Locomotion, его state machine и инъекцией `ThirdPersonController`. Локальные ссылки prefab/scene остаются serialized references.

Input выделен в независимый first-party module boundary. Его class trees и зависимости описаны в `Assets/_App/Modules/Input/Documentation~`, а Project S adapter — в `Docs/Architecture/Input.Player.md`.

Общее синхронное ядро конечных машин состояний выделено в `Assets/_App/Modules/StateMachine`. Core не зависит от Unity или DI container; VContainer используется отдельной integration assembly только в composition root. Диаграммы, lifecycle и package boundary описаны в `Assets/_App/Modules/StateMachine/Documentation~`.

Player Locomotion, его class tree, state graph и ограничения зависимостей описаны в `Docs/Architecture/Player.Locomotion.md`.

`Assets/Scenes/Demo Scene.unity` сохраняется как reference-сцена без переноса её содержимого. Рабочая сцена находится в `Assets/_Game/Scenes/PrototypeArena.unity` и является стартовой сценой разработки.
