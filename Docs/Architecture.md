# Project S — текущая архитектурная база

Project S остаётся feature-oriented monolith. MVC/MVVM не используются как архитектура gameplay; состояние принадлежит gameplay-компонентам и plain C# controllers/state machines, а Unity-компоненты служат lifecycle и presentation adapters. Для сложных UI-экранов по умолчанию применяется MVP Passive View.

## Выделенные сущности и владельцы состояния

- **Player Actor** — существующий `ThirdPersonController` координирует персонажа, способности, бой, input и camera intent. Его следует постепенно уменьшать по мере появления устойчивых feature boundaries.
- **Character Motor** — существующие `Character` и `CharacterMovement`; это уже работающая база движения, она не переписывается без отдельной задачи.
- **Input Source** — `IInputHandler`/`InputHandler`; один scoped-экземпляр создаётся в `GameLifetimeScope` и скрывает Unity Input System от gameplay-кода.
- **Combatant vitality** — `Health` является единственным владельцем текущего здоровья и публикует подтверждённые события damage/death.
- **Damage boundary** — `Hurtbox` принимает попадание и передаёт его владельцу `Health`; `Weapon` формирует `DamageInfo` и фильтрует повторное попадание одной цели в рамках одного окна hitbox.
- **Enemy Agent** — `EnemyController` и `EnemyStateMachine`; получение урона переводит агента в hit/dead state.
- **Animation presentation** — `AnimationController` отображает подтверждённое gameplay-состояние, но не владеет здоровьем и уроном.
- **Camera presentation** — Cinemachine остаётся presentation-слоем; gameplay сообщает intent через существующий controller.
- **Authoring data** — существующие `AbilitiesData` и `AnimationsData` остаются ScriptableObject-конфигурацией, а runtime progress хранится в экземплярах controllers/abilities.

## Composition

`GameLifetimeScope` — composition root gameplay-сцены. На первом шаге он владеет только input adapter и инъекцией `ThirdPersonController`. Новые регистрации добавляются лишь для зависимостей с реальным общим lifecycle; локальные ссылки prefab/scene остаются serialized references.

`Assets/Scenes/Demo Scene.unity` сохраняется как reference-сцена без переноса её содержимого. Рабочая сцена находится в `Assets/_Game/Scenes/PrototypeArena.unity` и является стартовой сценой разработки.
