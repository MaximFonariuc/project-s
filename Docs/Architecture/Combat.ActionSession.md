# Project S — Combat Action Session

## Назначение

`CombatActionSession` является runtime-owner одной выполняемой боевой action.
Она объединяет данные, которые обязаны начинаться и завершаться на одной
lifecycle-границе: текущую action и frame, уникальный attack execution, outcome,
hitbox window и timeline execution.

Session не выбирает следующую action, не меняет combat phase и не знает правил
combo graph. Эти решения остаются у `PlayerCombatActionPipeline`. Animator также не
становится владельцем gameplay-состояния.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class PlayerCombatController {
        <<command / lifecycle coordinator>>
        +Tick(scaledDeltaTime)
        +InterruptAttack()
    }
    class PlayerCombatActionPipeline {
        <<graph / chain coordinator>>
        -ObserveCombatFrame(frame) CombatActionEndReason
    }
    class CombatActionSession {
        <<single action owner>>
        +Action PlayerCombatAction
        +CurrentFrame CombatActionFrame
        +Outcome CombatActionOutcome
        +IsHitWindowOpen bool
        +LastEndReason CombatActionEndReason
        +ExecuteAsync(action, weapon, target, observer, token)
        +Abort(reason)
        +TryRecord(hit) bool
    }
    class PlayerCombatOperationLifetime {
        +Begin() CancellationTokenSource
        +TryComplete(tokenSource) bool
        +Cancel(reason)
    }
    class CombatActionExecutor {
        <<timeline / motion / immunity>>
        +Advance(scaledDeltaTime)
        +Observe()
        +Abort()
    }
    class CombatAttackExecution {
        <<one-hit filtering context>>
    }
    class CombatActionOutcome {
        <<confirmed result>>
        +HasConfirmedHit bool
        +LastConfirmedTarget GameObject
    }
    class IPlayerWeaponRig {
        +OpenHitbox(execution)
        +CloseHitbox()
    }
    class IPlayerCombatActor {
        +Root Transform
    }
    class ICombatHitEvents {
        +HitResolved
    }
    class PlayerCombatActionGraph

    PlayerCombatController --> PlayerCombatActionPipeline : starts
    PlayerCombatActionPipeline --> CombatActionSession : starts / observes
    PlayerCombatController --> PlayerCombatOperationLifetime : operation lifecycle
    PlayerCombatOperationLifetime --> CombatActionSession : aborts
    PlayerCombatActionPipeline --> PlayerCombatActionGraph : entry / transition rules
    PlayerCombatController --> ICombatHitEvents : forwards confirmed hit
    CombatActionSession --> CombatActionExecutor : timeline lifecycle
    CombatActionSession *-- CombatAttackExecution
    CombatActionSession *-- CombatActionOutcome
    CombatActionSession --> IPlayerWeaponRig : hitbox window
    CombatActionSession --> IPlayerCombatActor : execution source
```

## Lifecycle

```mermaid
sequenceDiagram
    participant Combat as PlayerCombatController
    participant Pipeline as PlayerCombatActionPipeline
    participant Session as CombatActionSession
    participant Executor as CombatActionExecutor
    participant Rig as IPlayerWeaponRig
    participant Graph as PlayerCombatActionGraph

    Combat->>Pipeline: ExecuteAsync(action, weapon, token)
    Pipeline->>Session: ExecuteAsync(action, weapon, target, frameHandler, token)
    Session->>Session: begin execution + outcome
    Session->>Executor: ExecuteAsync(definition)
    loop gameplay tick
        Combat->>Executor: Advance(scaledDeltaTime)
        Combat->>Executor: Observe()
        Executor->>Session: Observe(frame)
        Session->>Rig: open / close authored hitbox window
        Session->>Pipeline: frameHandler(frame)
        Pipeline->>Graph: resolve transition
    end
    Session->>Rig: CloseHitbox()
    Session->>Session: end execution + outcome
    Session-->>Pipeline: result + reason + confirmed target
```

Начало session транзакционно: частично созданные execution/outcome закрываются,
если actor или adapter отклоняет запуск. `finally` завершает session при обычном
completion, graph transition, landing, cancellation и исключении. Поэтому
hitbox и per-action hit filtering не могут пережить свою action.

Execution и outcome также отклоняют повторный `Begin`, пока предыдущий lifecycle
не завершён. Action, command, animation и reaction ids принимаются только в
каноническом виде без начальных/конечных пробелов: невидимая опечатка в
authoring profile обнаруживается при компиляции graph, а не превращается в
несопоставимый runtime key.

`CombatActionEndReason` описывает причину завершения без набора несогласованных
boolean flags:

- `Completed` — timeline дошёл до completion boundary;
- `Transitioned` — graph принял следующую action;
- `Landed` — airborne-only action потеряла допустимый environment;
- `Reaction`, `Interrupted`, `Cancelled`, `Disabled` — внешние lifecycle boundaries;
- `Failed` — запуск или выполнение завершились исключением.

Последняя подтверждённая цель копируется в immutable result до очистки outcome.
Это позволяет переносить цель из launcher в air chase, не оставляя mutable
outcome жить между actions.

## Защита коротких окон от frame hitch

Обычный кадр использует текущее normalized time. Если один большой delta целиком
пересёк короткое hitbox, chain или cancel window, `NormalizedActionWindow`
отмечает это как fully-crossed interval и executor выдаёт один catch-up frame.

- hitbox открывается ровно на один rendering frame, чтобы late-frame swept query
  прошёл от предыдущей до текущей позы оружия;
- completion откладывается до следующего tick, иначе session закрыл бы hitbox до
  `PlayerWeaponLoadout.LateUpdate`;
- принятый buffered transition также фиксируется, но применяется на следующем
  tick после swept sample;
- `OnWhiff` не принимается на catch-up frame: buffered command переоценивается
  на следующем tick, когда late-frame query уже подтвердил hit либо истинный
  промах;
- invulnerability и movement windows не продлеваются: catch-up предназначен
  только для событий, которые иначе полностью потерялись бы при frame hitch.

Это не заменяет разумную ширину authoring windows, но не позволяет единичному
длинному кадру бесследно удалить удар или уже buffered combo input.

## Границы зависимостей

- Session находится в `_App/Core`: она использует Project S action, actor и
  weapon contracts и пока не является кандидатом в `_App/Modules`.
- `PlayerCombatController` не открывает hitbox и не создаёт execution id.
- `CombatActionExecutor` не знает о combo graph, weapon rig и confirmed hits.
- Weapon draw/sheathe не создаёт damage execution и намеренно не входит в
  `CombatActionSession`; это отдельный equipment transition на том же
  сериализованном timeline executor.
- Session и `PlayerCombatOperationLifetime` создаются `PlayerCombatComposition`
  в gameplay scope через VContainer. Общая cancellation boundary описана в
  `Docs/Architecture/Combat.OperationLifetime.md`.
