# Project S — Combat Hit Stop и Gameplay Time

## Ответственность

Система создаёт короткую остановку или замедление gameplay после подтверждённого попадания. Настройка эффекта принадлежит конкретному `CombatHitDefinition`, а не оружию, Animator или target reaction. `CombatHitStopController` только адаптирует входящие и исходящие `CombatHitResult` к единому владельцу `Time.timeScale` — `GameplayTimeScaleController`.

Текущая граница решает следующие задачи:

- hit stop возникает только после принятого `Health` попадания;
- один и тот же путь работает для удара игрока и входящего урона игроку;
- duration измеряется unscaled time и завершается даже при `Time.timeScale == 0`;
- повторные запросы продлевают оставшееся время и сохраняют самый сильный freeze;
- исходный scale гарантированно восстанавливается при завершении, disable, dispose или выгрузке gameplay scope;
- прямые записи в `Time.timeScale` из Combat, Animation, Camera и VFX не требуются.

Система пока не управляет audio pitch, camera impulse, VFX policy и UI pause. Это отдельные presentation consumers.

## Дерево классов и зависимости

```mermaid
classDiagram
    direction LR

    class CombatActionAuthoring {
        +Damage int
        +StaggerDamage float
        +Knockback float
        +ReactionId string
        +HitStopDuration float
        +HitStopTimeScale float
    }
    class CombatHitDefinition {
        <<immutable action data>>
        +HitStop CombatHitStop
    }
    class CombatHitStop {
        <<immutable value>>
        +IsEnabled bool
        +Duration float
        +TimeScale float
    }
    class CombatHitResult {
        <<confirmed result>>
        +HitStop CombatHitStop
    }
    class ICombatHitEvents {
        +HitResolved event
    }
    class Health {
        +Damaged event
    }
    class CombatHitStopController {
        +Enable()
        +Disable()
    }
    class IGameplayTimeScaleRequests {
        <<request port>>
        +Request(request)
    }
    class IGameplayTimeScaleState {
        <<read port>>
        +IsActive bool
        +CurrentScale float
        +RemainingUnscaledTime float
    }
    class GameplayTimeScaleRequest {
        +Duration float
        +Scale float
    }
    class GameplayTimeScaleController {
        +Enable()
        +Disable()
        +Tick(unscaledDeltaTime)
        +Request(request)
    }
    class ITimeScaleBackend {
        <<Unity API port>>
        +TimeScale float
    }
    class UnityTimeScaleBackend
    class PlayerActorController {
        <<lifecycle / tick host>>
    }
    class PlayerHitStopComposition {
        <<VContainer composition root>>
    }

    CombatActionAuthoring ..> CombatHitDefinition : builds
    CombatHitDefinition *-- CombatHitStop
    CombatHitResult *-- CombatHitStop
    CombatHitStopController --> ICombatHitEvents : outgoing confirmed hit
    CombatHitStopController --> Health : incoming confirmed hit
    CombatHitStopController --> IGameplayTimeScaleRequests
    CombatHitStopController ..> GameplayTimeScaleRequest : creates
    GameplayTimeScaleController ..|> IGameplayTimeScaleRequests
    GameplayTimeScaleController ..|> IGameplayTimeScaleState
    GameplayTimeScaleController --> ITimeScaleBackend
    UnityTimeScaleBackend ..|> ITimeScaleBackend
    PlayerActorController --> CombatHitStopController : lifecycle
    PlayerActorController --> GameplayTimeScaleController : lifecycle / unscaled tick
    PlayerHitStopComposition ..> GameplayTimeScaleController : constructs
    PlayerHitStopComposition ..> CombatHitStopController : constructs
```

## Поток попадания

```mermaid
sequenceDiagram
    participant Action as CombatHitDefinition
    participant Resolver as CombatHitResolver
    participant Health
    participant Stop as CombatHitStopController
    participant Time as GameplayTimeScaleController
    participant Unity as UnityTimeScaleBackend

    Resolver->>Action: snapshot HitStop
    Resolver->>Health: TryApplyHit(result)
    Health-->>Resolver: accepted
    alt outgoing player hit
        Resolver-->>Stop: ICombatHitEvents.HitResolved(result)
    else incoming player hit
        Health-->>Stop: Damaged(result)
    end
    Stop->>Time: Request(duration, scale)
    Time->>Unity: Time.timeScale = baseline * scale
    loop PlayerActorController.Update
        Time->>Time: Tick(Time.unscaledDeltaTime)
    end
    Time->>Unity: restore baseline scale
```

`Weapon` не знает о hit stop. Он сообщает physics-кандидата; только принятый resolver/Health результат может создать freeze.

## Агрегация запросов

`GameplayTimeScaleController` поддерживает один агрегированный request:

- remaining duration становится максимумом текущего и нового duration;
- request scale становится минимумом текущего и нового scale;
- `0` означает полный freeze, `0.5` — половинную скорость, `1` — отсутствие замедления;
- завершение агрегированного окна единожды восстанавливает scale, захваченный при `Enable`.

Такая политика детерминирована, не создаёт коллекций в hot path и подходит для коротких overlapping hits. Она намеренно не моделирует последовательный стек slow-motion эффектов. Если появятся cutscene/menu/ability time domains с разными приоритетами, они должны расширить единого owner, а не писать в `Time.timeScale` параллельно.

## Time policy

| Система | Clock во время hit stop | Поведение |
|---|---|---|
| Combat action executor | `Time.deltaTime` | timeline и hit/cancel windows заморожены |
| Character physics | `FixedUpdate` / scaled time | simulation заморожена при scale `0` |
| Обычный Animator | scaled Unity time | текущая поза заморожена |
| Character reaction duration | scaled delta | hit-stun не истекает во время freeze |
| Combat input buffer | `Time.unscaledTime` | продолжает принимать input и истекает в реальном времени |
| Camera input/tracking | unscaled delta | камера остаётся отзывчивой |
| Camera presentation smoothing | scaled delta | gameplay-dependent smoothing заморожен |
| UI | Unity UI / unscaled presentation | остаётся доступным; отдельная pause policy пока отсутствует |

Эта политика не является побочным эффектом случайного выбора clock: `ThirdPersonController` передаёт в `PlayerActorController` одновременно scaled и unscaled delta time.

## Authoring и defaults

`CombatActionAuthoring` хранит `Hit Stop Duration` и `Hit Stop Time Scale` рядом с damage/stagger/knockback. Duration `0` отключает эффект. Prototype action по умолчанию использует `0.06s` и scale `0`, поэтому текущая сцена не требует нового asset или Inspector wiring.

Существующие serialized action assets, в которых новых полей ещё нет, безопасно получают disabled/default значение до явной настройки. Сцены, prefabs и Animator Controller системой не изменяются.

## Lifecycle и границы

- `GameplayTimeScaleController.Enable` захватывает baseline scale; `Disable` и `Dispose` всегда его восстанавливают.
- `CombatHitStopController.Enable/Disable` симметрично управляет двумя target-specific subscriptions.
- VContainer registrations находятся только в `PlayerHitStopComposition`; runtime-код не вызывает `Resolve`.
- `PlayerActorController` запускает time owner до gameplay consumers и отключает его последним.
- `UnityTimeScaleBackend` — единственная реализация, которая обращается к `UnityEngine.Time.timeScale`.
- Код остаётся в `_App/Core`: общая time-scale граница пока имеет только один реальный gameplay consumer. После появления cutscene/ability/global pause consumers можно отдельно оценить извлечение стабильного ядра в `_App/Modules`.
