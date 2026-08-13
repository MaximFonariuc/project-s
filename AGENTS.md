# Project S — правила разработки

## 1. Текущая цель

Project S на текущем этапе — single-player character-action/slasher в духе `DmC: Devil May Cry` 2013 года.

Главный приоритет — отзывчивое управление и качество базового combat loop:

- camera-relative movement;
- прыжок, уклонение и воздушное управление;
- наземные и воздушные комбинации;
- input buffering и cancel windows;
- hitbox/hurtbox, hit reaction, knockback и hit stop;
- target assistance/lock-on;
- один или несколько простых противников для проверки боя.

Не входят в текущий scope: multiplayer, FishNet, backend, Nakama, matchmaking, сложные production saves, live services, platform SDK и инфраструктура «на будущее».

## 2. Работа агента

- До изменений проверить наличие Unity-проекта, версию из `ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`, `packages-lock.json`, ближайший `AGENTS.md`, asmdef, сцены и существующий style.
- Не создавать implementation plan и автоматизированные тесты без явного запроса пользователя.
- Выполнять только запрошенный scope. Не добавлять соседние механики, абстракции, пакеты и настройки «заодно».
- Не устанавливать пакет, пока задача решается штатными средствами Unity без существенной потери качества.
- Перед добавлением зависимости проверить Unity/IL2CPP-совместимость, лицензию, активность проекта и отсутствие установленного аналога.
- Фиксировать package version либо git tag/commit; плавающие `main`/`master` запрещены.
- Не редактировать `.unity`, `.prefab`, `.asset`, Animator Controller и Project Settings вслепую. Использовать Unity Editor/MCP и проверять Console.
- Сохранять пользовательские изменения; не форматировать и не переписывать несвязанный код.
- Не считать задачу завершённой при новых compile errors, Console exceptions или нарушенном lifecycle затронутого объекта.

## 3. Минимальный стек

После создания Unity-проекта использовать:

- Unity `6000.0.80f1`;
- Universal Render Pipeline;
- Unity Input System package;
- Cinemachine;
- Animator/Mecanim;
- VContainer как единственный DI container, когда появляется реальная композиция зависимостей.

UniTask, MessagePipe, PrimeTween, Addressables, localization, ZLinq и другие библиотеки пока не являются частью базы. Подключать их только под конкретную задачу отдельным решением.

## 4. Структура проекта

Сохранять следующие корневые области внутри `Assets`:

```text
Assets/
├── _App/                         # весь first-party код
│   ├── Core/                     # логика конкретно Project S
│   │   ├── Runtime/
│   │   │   ├── Character/
│   │   │   ├── Combat/
│   │   │   ├── Enemies/
│   │   │   ├── Camera/
│   │   │   ├── Input/
│   │   │   ├── UI/
│   │   │   ├── Common/
│   │   │   └── Composition/
│   │   ├── Editor/
│   │   └── Tests/                # только по явному запросу
│   └── Modules/                  # наши независимые архитектурные модули
│       └── <ModuleName>/
│           ├── Runtime/
│           └── Editor/
├── _Game/                        # игровые и authoring assets, без first-party кода
│   ├── Animations/
│   ├── Audio/
│   ├── Materials/
│   ├── Prefabs/
│   ├── Scenes/
│   └── Settings/
└── _Modules/                     # сторонние/vendor расширения
```

- `_App` — единственное штатное место для скриптов, которые разрабатываются для Project S или его first-party модулей. Новые first-party скрипты не создавать в `_Game`, `_Modules`, корне `Assets` и случайных папках.
- `_App/Core` хранит код конкретной игры: Player, Character, Combat, Enemy, Camera, UI, composition и связанные editor-инструменты. Этот код может знать о Project S и его контенте.
- `_App/Modules` хранит разработанные нами переиспользуемые архитектурные решения: Input, StateMachine и следующие модули со стабильной независимой ответственностью. Модуль не ссылается на `_App/Core` и assets из `_Game`.
- Направление first-party зависимостей: `_App/Core -> _App/Modules`. Обратная зависимость запрещена; модули не зависят друг от друга без явного контракта и обоснованной интеграционной assembly.
- `_Game` хранит сцены, prefabs, animations, materials, audio, VFX и другие assets конкретной игры. First-party код здесь запрещён; допустимы только generated/vendor companion scripts, которые нельзя безопасно отделить.
- `_Modules` хранит только сторонние/imported/vendor расширения. Наши Input, StateMachine, gameplay и editor-tools сюда не помещать. Стороннюю структуру не переписывать; интеграционные адаптеры к ней находятся в `_App`.
- `_Game` может использовать компоненты из `_App/Core`, `_App/Modules` и сторонних `_Modules` на сценах/prefabs, но runtime-код не должен получать assets через неявный поиск по папкам.
- Существующие корневые `Resources`, `Scenes`, `Settings`, TextMesh Pro и vendor/imported folders не переносить автоматически. Реорганизация существующих assets выполняется только отдельным запросом с проверкой Unity references.
- Организовывать код по gameplay feature, а не создавать полный набор `Domain/Application/Infrastructure` для каждой папки.
- `Common` содержит только доказанно общие малые типы. Общий `Utils` запрещён.
- Editor-код хранить в соответствующем `Editor` и не включать в runtime assemblies.
- Не создавать asmdef на каждую папку. Добавлять assembly boundary, когда она даёт независимую компиляцию, явное направление зависимостей или готовит стабильный модуль к извлечению.
- Generated и imported vendor content не смешивать с first-party кодом. Любое осознанное изменение vendor-кода документировать как локальный patch, чтобы обновление зависимости не затёрло его незаметно.

## 5. Архитектурный подход

- Начинать с простого feature-oriented monolith. Извлекать слои и интерфейсы только вокруг реальной сменяемой границы или сложной чистой логики.
- `MonoBehaviour` отвечает за Unity lifecycle, scene references и адаптацию Unity API, но не должен становиться хранилищем всей логики персонажа.
- У изменяемого состояния один owner. Не дублировать combat state в controller, Animator, ScriptableObject и UI одновременно.
- Явная state machine предпочтительнее набора несогласованных boolean flags.
- Разделять simulation state и presentation state там, где animation smoothing, anticipation или hit stop могут временно расходиться с логикой.
- Не вводить интерфейс для каждого класса. Интерфейс нужен для I/O, времени, input source, platform API либо действительно сменяемой реализации.
- Не использовать global mutable static state, singleton MonoBehaviour, service locator и ScriptableObject как глобальный runtime container.
- ScriptableObject хранит immutable authoring data; runtime state принадлежит созданному экземпляру с явным lifecycle.
- Не связывать gameplay features через поиск объектов в сцене. Использовать явную ссылку, фабрику, композицию или небольшой контракт.

### Gameplay и UI models

- Не применять MVC, MVP или MVVM как глобальную архитектуру всей игры.
- Gameplay строится как feature-oriented controllers/state machines с Unity presentation adapters: input adapter передаёт команды владельцу gameplay state, а Animator/VFX/Audio отображают подтверждённый результат.
- Для UI по умолчанию использовать MVP Passive View.
- Model в MVP — gameplay/application state и доступные операции, а не копия данных внутри UI.
- Presenter — plain C# object: получает зависимости через VContainer, владеет логикой экрана и subscriptions, принимает пользовательские действия от View и обновляет View.
- View — MonoBehaviour либо UI Toolkit adapter: содержит serialized/visual references, сообщает typed UI actions и не вызывает gameplay services напрямую.
- View не читает Model напрямую и не содержит бизнес-правила. Presenter не управляет Animator/Cinemachine/gameplay objects в обход предназначенного adapter/service.
- Lifecycle Presenter совпадает с lifecycle screen/scene scope; при закрытии освобождаются все subscriptions и callbacks.
- Не создавать Presenter для каждого gameplay GameObject и для пассивного визуального элемента без логики.
- MVVM/data binding применять только отдельным решением для действительно сложного data-driven экрана. Reactive framework и общий binding layer не добавлять заранее.

## 6. VContainer

- Регистрации находятся только в Composition Root/LifetimeScope.
- Plain C# objects используют constructor injection.
- Injection в MonoBehaviour допустим для объектов, которыми владеет Unity или VContainer.
- `Resolve()`, `IObjectResolver`, `IContainerBuilder` и `LifetimeScope` вне Composition запрещены как Service Locator.
- Локальные зависимости View/Prefab предпочтительно задавать явными serialized references; не помещать каждый компонент в DI.
- На текущем этапе достаточно application/root scope и gameplay scene scope. Не создавать иерархию scope без различающегося lifecycle.
- Scope владеет созданными им subscriptions, factories и disposable resources и освобождает их при завершении сцены.

## 7. Input

- Gameplay-код получает семантические действия, а не читает `Keyboard.current`, `Gamepad.current` или конкретные bindings.
- Unity Input System скрывать за тонким adapter, выдающим input snapshot/commands.
- Разделять device polling и consumption gameplay-команд.
- Боевой input buffer хранит действие, время/кадр получения и срок допустимости.
- Повторный ввод, удержание, buffering и cancel обрабатываются явно, а не зависят от случайного порядка `Update`.
- Gameplay и UI используют разные action maps; переключение карты имеет одного owner.
- Поддерживать keyboard/mouse и gamepad с начала разработки, не разбрасывая проверки control scheme по gameplay-коду.

## 8. Персонаж и combat

- Player motor, combat state и visual/animation adapter имеют разные ответственности, но могут оставаться в одном feature до появления причины разделять assembly/package.
- Движение относительно камеры преобразуется в одном месте. Camera transform не передаётся глубоко в combat logic.
- Combat state machine определяет допустимые действия, transitions, buffering и cancel windows.
- Attack definition описывает authoring data: timings, damage, movement, hit reaction, cancel rules и ссылки на presentation; она не хранит runtime progress атаки.
- Damage применяется через явный hit result один раз. Повторное пересечение одного attack hitbox с одной целью фильтруется.
- Hitbox сообщает кандидата на попадание; combat resolver определяет итог damage, reaction и knockback.
- Не выдавать damage, награду или изменение gameplay state только из animation callback.
- Hit stop централизован и имеет owner. После завершения гарантированно восстанавливать time state, Animator и связанные эффекты.
- Gameplay, который зависит от времени, получает выбранную time policy явно; не смешивать scaled и unscaled time случайно.
- Root motion использовать осознанно для конкретных атак. Не смешивать неконтролируемое root motion и motor movement в одном кадре.
- Combo counter и style rank отображают подтверждённые combat events и не управляют самим боем.

## 9. Animation и камера

- Animator является presentation adapter, а не единственным владельцем gameplay state.
- Animator parameters задаются из одного adapter; не изменять одни параметры из нескольких несвязанных компонентов.
- Animation Events разрешены для узких presentation hooks. Критичные combat transitions должны иметь проверяемый fallback и явного owner.
- Идентификаторы Animator parameters кэшировать; строковые имена не вычислять в hot path.
- Cinemachine остаётся в Camera/Presentation. Combat logic может запросить camera intent, но не управляет Cinemachine components напрямую.
- Camera rig поддерживает свободную камеру, target assistance и combat framing как отдельные режимы.
- Camera shake/impulse не влияет на gameplay aim и physics queries.
- При hit stop отдельно определить поведение камеры, input buffering, VFX и UI.

## 10. Производительность и Unity lifecycle

- Сначала измерять Profiler/Memory Profiler, затем оптимизировать.
- Не выполнять `Find*`, dependency resolve и повторяющийся `GetComponent` в `Update`/`FixedUpdate`.
- Кэшировать обязательные component references при создании; отсутствие обязательной ссылки выявлять явно.
- Не создавать LINQ, boxing, closures и временные коллекции в подтверждённом hot path.
- Physics queries имеют явный layer mask и обрабатывают переполнение buffer; `NonAlloc` применять там, где запрос действительно частый.
- Pooling вводить после обнаружения churn. Возвращаемый объект сбрасывает mutable state, subscriptions, particles, Animator и physics state.
- Каждый event subscription, coroutine, cancellation source и временный resource имеет owner и путь освобождения.
- Не создавать отдельный `Update` для большого числа однотипных объектов, если профилирование подтвердило необходимость агрегированного tick.

## 11. Извлечение в _App/Modules и UPM-пакеты

Не проектировать module/package API заранее. Код можно переносить из `_App/Core` в `_App/Modules`, когда одновременно выполнено следующее:

- ответственность и публичная граница стабилизировались в реальном использовании;
- модуль не ссылается на контент и код конкретной игры;
- зависимости направлены внутрь и явно определены;
- lifecycle, configuration и ownership понятны без знания сцены проекта;
- извлечение уменьшает связанность или даёт реальное повторное использование.

- `_App/Modules/<ModuleName>` сначала остаётся обычным first-party модулем внутри `Assets`. Не добавлять `package.json` и не называть его UPM package без необходимости отдельного versioning/distribution.
- В настоящий UPM package модуль выносится отдельным решением, когда его нужно независимо версионировать, переиспользовать между проектами или распространять.
- First-party module/package не ссылается на `_App/Core`, `_Game` и Project S. Project S может ссылаться на module/package.
- `_Modules` не является промежуточной стадией для нашего кода: это каталог уже импортированных сторонних расширений.
- Не выносить незрелый Player/Combat feature только ради структуры.

## 12. Проверка изменений

- Минимум: нет новых compile errors и Console exceptions в затронутом flow.
- Проверить повторный вход в состояние, disable/destroy объекта и перезагрузку gameplay scene.
- Для input проверить press, hold, rapid repeat, смену action map и gamepad.
- Для движения проверить низкий/высокий frame rate, склон, край поверхности и столкновение.
- Для combat проверить промах, одно попадание, несколько целей, interruption, cancel, смерть цели и уничтожение attacker во время атаки.
- Для animation проверить отсутствующий state/clip, transition interruption и восстановление после hit stop.
- Для камеры проверить потерю/смену цели, тесное пространство, препятствие и резкое изменение направления.
- Автоматизированные тесты и отдельный test scaffold создавать только по явному запросу пользователя.
- В отчёте отделять фактически выполненную проверку от того, что проверить не удалось.
