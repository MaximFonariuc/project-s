# Project S — Combat Hit Resolution

## Ответственность

Система превращает физическое пересечение weapon hitbox с `Hurtbox` в один подтверждённый `CombatHitResult`. Правила удара принадлежат выполняемому combat action, а не prefab оружия. Поэтому один и тот же `Weapon` может использоваться разными moveset без изменения компонента.

Граница решает четыре задачи:

- связывает попадание с конкретным `AttackExecutionId` и `PlayerCombatActionId`;
- запрещает повторно поразить одного actor несколькими hurtbox в рамках одного execution;
- применяет multiplier зоны к базовому damage;
- публикует событие только после того, как `Health` действительно принял удар.

Reaction и poise/stagger обрабатываются отдельным target-local consumer, описанным в `Docs/Architecture/Combat.Reactions.md`. Hit stop передаётся как immutable часть результата и обрабатывается отдельным consumer из `Docs/Architecture/Combat.HitStop.md`. VFX, audio и camera impulse пока не реализуются и не должны возвращать эти обязанности в `Weapon`.

## Дерево классов

```mermaid
classDiagram
    class CombatActionDefinition {
        +HitDefinition CombatHitDefinition
        +HitboxWindow NormalizedActionWindow
    }
    class CombatHitDefinition {
        +Damage int
        +StaggerDamage float
        +Knockback float
        +ReactionId CombatReactionId
        +HitStop CombatHitStop
    }
    class CombatAttackExecution {
        +Id AttackExecutionId
        +ActionId PlayerCombatActionId
        +IsActive bool
        +Begin(...)
        +TryRegisterTarget(targetId) bool
        +End()
    }
    class Weapon {
        <<MonoBehaviour / physics adapter>>
        +EnableHitbox(execution, resolver)
        +DisableHitbox()
    }
    class ICombatHitResolver {
        <<interface>>
        +TryResolve(execution, candidate, result) bool
    }
    class CombatHitResolver {
        +HitResolved event
    }
    class CombatHitCandidate {
        +Target ICombatHitTarget
        +Point Vector3
        +Direction Vector3
    }
    class ICombatHitTarget {
        <<interface>>
        +TargetId int
        +DamageMultiplier float
        +TryApplyHit(result) bool
    }
    class Hurtbox {
        <<MonoBehaviour / zone adapter>>
        +ZoneId string
        +DamageMultiplier float
    }
    class BodyColliderRig {
        <<MonoBehaviour / actor gateway>>
    }
    class Health {
        <<MonoBehaviour / vitality owner>>
        +CurrentHealth int
        +Damaged event
        +Died event
    }
    class CombatHitResult {
        <<immutable confirmed event>>
        +ExecutionId AttackExecutionId
        +ActionId PlayerCombatActionId
        +Damage int
        +StaggerDamage float
        +Knockback float
        +ReactionId CombatReactionId
        +HitStop CombatHitStop
        +Point Vector3
        +Direction Vector3
        +Source GameObject
        +Target GameObject
        +HitZoneId string
    }
    class ICombatHitEvents {
        <<public read port>>
        +HitResolved event
    }

    CombatActionDefinition *-- CombatHitDefinition
    CombatAttackExecution --> CombatActionDefinition : snapshots impact
    Weapon --> ICombatHitResolver : reports candidate
    Weapon ..> CombatHitCandidate : creates
    CombatHitCandidate --> ICombatHitTarget
    Hurtbox ..|> ICombatHitTarget
    CombatHitResolver ..|> ICombatHitResolver
    CombatHitResolver ..|> ICombatHitEvents
    CombatHitResolver --> CombatAttackExecution : filters target per swing
    CombatHitResolver ..> CombatHitResult : creates after validation
    Hurtbox --> BodyColliderRig : forwards confirmed hit
    BodyColliderRig --> Health : applies damage
```

## Поток попадания

```mermaid
sequenceDiagram
    participant Combat as PlayerCombatController
    participant Execution as CombatAttackExecution
    participant Weapon
    participant Hurtbox
    participant Resolver as CombatHitResolver
    participant Health
    participant Events as ICombatHitEvents

    Combat->>Execution: Begin(action id, impact, source)
    Combat->>Weapon: EnableHitbox(execution, resolver)
    Weapon->>Hurtbox: physics overlap
    Weapon->>Resolver: TryResolve(candidate)
    Resolver->>Execution: TryRegisterTarget(actor root id)
    Resolver->>Resolver: scale damage by zone multiplier
    Resolver->>Hurtbox: TryApplyHit(result)
    Hurtbox->>Health: TryApplyHit(result)
    Health-->>Resolver: accepted / rejected
    alt accepted
        Resolver-->>Events: HitResolved(result)
    end
    Combat->>Weapon: DisableHitbox()
    Combat->>Execution: End()
```

`Health.Damaged`, `Health.Died` и глобальный для gameplay scope `ICombatHitEvents.HitResolved` получают один и тот же immutable result. Событие resolver означает подтверждённое попадание; overlap, иммунитет или уже поражённая в этом execution цель событие не создают.

## Ownership и зависимости

```text
CombatActionAuthoring -> CombatActionDefinition -> CombatHitDefinition
PlayerCombatController -> CombatAttackExecution -> IPlayerWeaponRig
Weapon -> ICombatHitResolver -> ICombatHitTarget
Hurtbox -> BodyColliderRig -> Health
ICombatHitEvents -> hit stop / future VFX / audio adapters
Health.Damaged -> target-local reaction / incoming hit stop
```

- `PlayerCombatController` владеет lifecycle execution: новый id на каждое фактически запущенное action, завершение при completion, chain transition, interruption, cancellation и disable.
- `CombatAttackExecution` владеет множеством уже поражённых actor root. Оно не находится в `Weapon`, поэтому смена collider или несколько зон тела не меняют правило одного попадания.
- `Weapon` принадлежит scene/prefab и знает только Physics, активный execution и resolver. Он не хранит damage, cooldown, combo или reaction rules.
- `Hurtbox` предоставляет zone metadata и путь к владельцу здоровья. Он не вычисляет итог атаки.
- `Health` — единственный owner текущего здоровья и immunity check.
- `CombatHitResolver` имеет scoped lifecycle и регистрируется только в `PlayerCombatComposition`. Публичный read-port `ICombatHitEvents` не разрешает внешним системам создавать попадания.

## Расширение

- Новый тип оружия должен давать `CombatHitCandidate` тому же resolver; правила damage остаются в action profile.
- Projectile или area attack могут иметь собственный physics adapter и отдельный `CombatAttackExecution`, не меняя `Health` и `Hurtbox`.
- Stagger/poise consumer читает `StaggerDamage` и `ReactionId` из подтверждённого result.
- Knockback consumer интерпретирует `Knockback` вместе с нормализованным `Direction`, но применяет движение через предназначенный motor intent.
- Исходящий hit stop подписывается на `ICombatHitEvents`, входящий — на target-local `Health.Damaged`. Future VFX, audio и camera adapters также потребляют подтверждённый result и не участвуют в расчёте damage.

Система остаётся в `_App/Core`: `PlayerCombatActionId`, damage rules и ожидаемые reactions являются правилами Project S. Извлечение в `_App/Modules` возможно только после второго реального consumer и стабилизации game-agnostic контракта.
