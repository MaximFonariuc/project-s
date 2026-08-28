# Project S — текущая архитектурная база

Project S остаётся feature-oriented monolith. MVC/MVVM не используются как архитектура gameplay; состояние принадлежит gameplay-компонентам и plain C# controllers/state machines, а Unity-компоненты служат lifecycle и presentation adapters. Для сложных UI-экранов по умолчанию применяется MVP Passive View.

## Физические границы проекта

- `Assets/_App/Core` — first-party код, который знает о Project S: gameplay, presentation adapters, composition и editor tooling.
- `Assets/_App/Modules` — наши независимые архитектурные модули. Сейчас здесь находятся `Input`, `StateMachine` и `Animation`; они не ссылаются на `Core` и `_Game`.
- `Assets/_Game` — сцены, prefabs, модели, анимации, материалы, настройки и другой игровой контент без first-party скриптов.
- `Assets/_Modules` — импортированные сторонние расширения. Интеграционный код Project S остаётся в `_App`, а vendor-код не изменяется без явно задокументированного patch.

Допустимое направление зависимостей: `_App/Core -> _App/Modules`. `_Game` связывает компоненты и content через Unity serialization; `_Modules` рассматриваются как внешние зависимости.

Физическое направление ссылок закреплено assemblies `ProjectS.Core.Runtime` и `ProjectS.Core.Editor`. Их дерево, разрешённые package references и сохранённый технический долг описаны в `Docs/Architecture/Core.Assemblies.md`.

## Выделенные сущности и владельцы состояния

- **Player Actor** — `ThirdPersonController` остаётся Unity-host для serialized references и делегирует lifecycle/frame ticks scoped-координатору `PlayerActorController`. Координатор также согласует подтверждённые reaction transitions с Combat, Traversal и Locomotion, не дублируя их state. `PlayerActorAdapter` изолирует Camera, Targeting, Combat и Traversal от большого legacy-класса `Character` через consumer-specific порты.
- **Player Locomotion** — `PlayerLocomotionController` владеет semantic movement intent и машиной `Idle / Run / Jump / Fall / Dodge`, а также публикует единый immutable `PlayerLocomotionSnapshot`. `PlayerDodgeAction` владеет motor displacement и нормализованным i-frame window через общий immunity lease. `CharacterMotor` является портом к существующим `Character` и `CharacterMovement`, не переписывая проверенную collision physics.
- **Player Input** — gameplay зависит от project-specific порта `IPlayerInput`. `PlayerInputAdapter` переводит сигналы независимого `IInputService` в семантические команды игрока, а `UnityInputSource` остаётся инфраструктурной деталью composition root.
- **Combatant vitality** — текущий `Health` является временным prototype-owner hit points, публикует подтверждённые события damage/death и агрегирует краткоживущие immunity leases. Целевая модель разделяет Attributes, Derived Stats, Resources, Modifiers, Costs/Regeneration и узкий Life State; решение зафиксировано в `Docs/Architecture/Character.StatsAndResources.md` и пока не реализуется.
- **Combat hit resolution** — `Weapon` сообщает только physics-кандидата; scoped `CombatHitResolver` связывает его с action execution, применяет multiplier `Hurtbox`, фильтрует повторную цель и публикует подтверждённый `CombatHitResult`. `Health` остаётся владельцем здоровья.
- **Player Combat** — scoped `IStateMachine<PlayerCombatPhase>` владеет фазой `Ready / Executing / Recovery / WeaponSwitch / Interrupted`; `PlayerCombatController` координирует semantic commands, action graph, input buffer, cooldown и подтверждённый weapon state. `CombatActionSession` владеет одной выполняемой атакой, execution/outcome и hitbox lifecycle, а `PlayerWeaponTransitionSession` — временной sheathe/draw-последовательностью, commit-point и восстановлением loadout при отмене. Outcome принимает только подтверждённые hits совпадающего execution и позволяет graph различать `Any / OnHit / OnWhiff`, не передавая authority Animator. Внешние consumers используют раздельные `IPlayerCombatCommands` и `IPlayerCombatState`.
- **Animation presentation** — `PlayerAnimationCoordinator` является единственным владельцем arbitration конечных animation actions и playback speed для Locomotion, Combat, Reaction и Traversal. `PlayerCharacterAnimationPresenter` переводит подтверждённый locomotion snapshot в backend-neutral параметры driver. Обычный Animator Controller владеет clips/blend trees/transitions, но не gameplay-состоянием.
- **Player Targeting** — `PlayerTargetingController` владеет lock-on state и выбором цели. Camera и будущий Combat читают его через `IPlayerTargetingState`, не управляя target selection.
- **Player Camera** — `PlayerCameraController` владеет free-look/target-lock режимом, zoom, shoulder и framing state. `CinemachineCameraRig` является заменяемым presentation adapter, а locomotion читает только `ICameraOrientation`.
- **Player Traversal** — `PlayerTraversalController` владеет interaction/climb workflow и согласованием gravity inversion между locomotion и camera. `PlayerSimulationGate` объединяет persistent pause request и временные traversal locks, оставаясь единственным владельцем вызова `Character.Pause` в player flow.
- **Character Simulation** — `Character` остаётся совместимым Unity facade и владельцем movement mode/fixed-step, но runtime state прыжка, реестр physics volumes и cancellable simulation loop имеют отдельных внутренних owners. Collision solving остаётся в `CharacterMovement`; детали и class tree описаны в `Docs/Architecture/Character.Simulation.md`.
- **Character Features** — `CharacterFeatureSet` является только authoring-каталогом включённых настроек. Общая runtime ability-система, generic lookup и factories удалены; поведение принадлежит Locomotion, Traversal и Movement Policies.
- **Character Movement Policies** — scoped owner подключает опциональные fly/swim/gravity/ground/landing rules к явным simulation events `Character` и симметрично освобождает subscriptions.
- **Authoring data** — `CharacterFeatureSet` и gameplay-профили остаются ScriptableObject-конфигурацией, а runtime progress хранится только в экземплярах controllers/actions/policies. Animation clips и transitions принадлежат Animator Controller, а не отдельному SO-каталогу.

## Composition

`GameLifetimeScope` — composition root gameplay-сцены. Он регистрирует input boundary и делегирует player wiring в `PlayerComposition`; Camera, Targeting, Combat, Locomotion и Traversal имеют отдельные composition-блоки внутри той же Composition-области. Регистрации не просачиваются в gameplay-код, а локальные prefab/scene ссылки остаются serialized references.

Input выделен в независимый first-party module boundary. Его class trees и зависимости описаны в `Assets/_App/Modules/Input/Documentation~`, а Project S adapter — в `Docs/Architecture/Input.Player.md`.

Общее синхронное ядро конечных машин состояний выделено в `Assets/_App/Modules/StateMachine`. Core не зависит от Unity или DI container; VContainer используется отдельной integration assembly только в composition root. Диаграммы, lifecycle и package boundary описаны в `Assets/_App/Modules/StateMachine/Documentation~`.

Player Locomotion, его class tree, state graph и ограничения зависимостей описаны в `Docs/Architecture/Player.Locomotion.md`.

Player Animation presentation, immutable locomotion snapshot и граница Mecanim backend описаны в `Docs/Architecture/Player.Animation.md`.

Player Camera и Player Targeting описаны раздельно в `Docs/Architecture/Player.Camera.md` и `Docs/Architecture/Player.Targeting.md`.

Player Combat и его state machine описаны в `Docs/Architecture/Player.Combat.md`; semantic commands, moveset graph и Grounded/Airborne branches — в `Docs/Architecture/Combat.ActionModel.md`; отдельное дерево input buffer и его policies — в `Docs/Architecture/Combat.InputBuffer.md`.

Character Reaction, poise, hit-stun/knockdown/airborne/death state machine и её границы с Health, Combat, Locomotion и Animation описаны в `Docs/Architecture/Combat.Reactions.md`.

Combat Hit Stop, единый owner `Time.timeScale`, правила агрегации freeze-запросов и поведение scaled/unscaled systems описаны в `Docs/Architecture/Combat.HitStop.md`.

Player Traversal, interaction flow и ownership локальной simulation pause описаны в `Docs/Architecture/Player.Traversal.md`.

Authoring-модель character features и serialization migration описаны в `Docs/Architecture/Character.Features.md`.

Опциональные movement policies, их class tree и simulation subscriptions описаны в `Docs/Architecture/Character.MovementPolicies.md`.

Anti-corruption boundary между feature controllers и legacy `Character`, а также дерево player composition описаны в `Docs/Architecture/Player.ActorBoundary.md`.

Внутренняя декомпозиция `Character`, fixed-step lifecycle и разрешённые зависимости описаны в `Docs/Architecture/Character.Simulation.md`.

Будущая система характеристик и ресурсов, временный статус `Health` и правила её последующей миграции описаны в `Docs/Architecture/Character.StatsAndResources.md`.

Animation layer arbitration и gameplay interruption между Reaction, Traversal, Combat и Locomotion централизованно согласованы. Следующий state scope следует выбирать из реального gameplay-приоритета, а не расширять общий player coordinator заранее.

`Assets/Scenes/Demo Scene.unity` сохраняется как reference-сцена без переноса её содержимого. Рабочая сцена находится в `Assets/_Game/Scenes/PrototypeArena.unity` и является стартовой сценой разработки.
