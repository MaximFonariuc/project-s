# Project S — Combat Action Model

## Ответственность

`PlayerCombatActionGraph` описывает доступные действия игрока и разрешённые переходы между ними. Он находится в `_App/Core`, потому что команды, moveset, наземные и воздушные ветки являются правилами конкретной игры.

Граф не читает устройства ввода, не запускает Animator, не хранит прогресс атаки и не меняет состояние персонажа. Его вход — семантическая команда, окружение и подтверждённый `CombatActionFrame`; выход — конкретный `PlayerCombatAction`.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class PlayerCombatCommandId {
        +Value string
    }
    class PlayerCombatActionId {
        +Value string
    }
    class PlayerCombatAnimationId {
        +Value string
    }
    class PlayerCombatAction {
        +Id PlayerCombatActionId
        +Environment PlayerCombatEnvironment
        +Presentation PlayerCombatPresentation
        +Definition CombatActionDefinition
    }
    class PlayerCombatActionGraph {
        +TryResolveEntry(command, environment, action) bool
        +TryResolveTransition(source, command, environment, frame, action) bool
    }
    class PlayerCombatEntryRule {
        +CommandId PlayerCombatCommandId
        +TargetActionId PlayerCombatActionId
    }
    class PlayerCombatTransitionRule {
        +SourceActionId PlayerCombatActionId
        +CommandId PlayerCombatCommandId
        +TargetActionId PlayerCombatActionId
        +Window PlayerCombatTransitionWindow
        +Condition PlayerCombatTransitionCondition
    }
    class CombatActionOutcome {
        +ExecutionId AttackExecutionId
        +ActionId PlayerCombatActionId
        +ConfirmedHitCount int
        +HasConfirmedHit bool
        +LastConfirmedTarget GameObject
    }
    class CombatActionDefinition {
        +HitboxWindow
        +HitDefinition CombatHitDefinition
        +ChainWindow
        +CancelWindow
        +RecoveryStart float
        +CompletionTime float
        +MovementIntent
        +Motion CombatMotionDefinition
    }
    class PlayerCombatActionProfile {
        <<ScriptableObject authoring>>
        +CreateActionGraph()
    }
    class PlayerCombatController {
        <<runtime owner>>
    }
    class IPlayerInput {
        <<semantic input port>>
        +CombatActionRequested
    }
    class ICombatHitEvents {
        <<confirmed gameplay events>>
        +HitResolved(CombatHitResult)
    }

    PlayerCombatActionGraph *-- PlayerCombatAction
    PlayerCombatActionGraph *-- PlayerCombatEntryRule
    PlayerCombatActionGraph *-- PlayerCombatTransitionRule
    PlayerCombatAction --> PlayerCombatActionId
    PlayerCombatAction --> PlayerCombatAnimationId : presentation
    PlayerCombatAction --> CombatActionDefinition
    PlayerCombatEntryRule --> PlayerCombatCommandId
    PlayerCombatTransitionRule --> PlayerCombatCommandId
    PlayerCombatController *-- CombatActionOutcome
    ICombatHitEvents --> PlayerCombatController : matching execution only
    PlayerCombatActionProfile ..> PlayerCombatActionGraph : builds
    IPlayerInput --> PlayerCombatController : command
    PlayerCombatController --> PlayerCombatActionGraph : resolves
```

## Команда, действие и presentation

- `PlayerCombatCommandId` отвечает на вопрос «что запросил игрок»: `attack.light`, `attack.heavy`, `attack.launcher`, `skill.special` и будущие `item.use`.
- `PlayerCombatActionId` идентифицирует узел moveset: `combo.0`, `combo.1`, `air.combo.0`.
- `PlayerCombatAnimationId` выбирает named animation slot (`attack.heavy`, `skill.special`) и не определяет gameplay transition.
- `PlayerCombatPresentation` сообщает animation adapter, как отобразить уже выбранное действие: combo index либо named `PlayerCombatAnimationId`.
- `CombatActionDefinition` хранит immutable timeline и impact: hitbox, damage, stagger, knockback, reaction id, chain/cancel, recovery, movement, invulnerability, commit, completion и cooldown.
- `CombatActionExecutor` удерживает отдельный damage-immunity lease только внутри authored invulnerability window. Он не восстанавливает сохранённый boolean, поэтому не может снять иммунитет, которым одновременно владеет dodge или другой gameplay flow.

Одна команда может иметь несколько entry/transition rules, если целевые actions принадлежат непересекающимся environment masks. Поэтому `attack.light` может выбирать отдельные наземную и воздушную ветки без проверки платформы или устройства в gameplay-коде. Неоднозначные правила с пересекающимися masks отклоняются при построении graph.

## Результат action и условия перехода

`CombatActionOutcome` — runtime-состояние только текущего action execution. Controller
начинает его вместе с `CombatAttackExecution`, принимает исключительно
`CombatHitResult` с совпадающими `AttackExecutionId` и `PlayerCombatActionId` и
сбрасывает outcome при completion, interruption, disable и перед следующим action.
Animator и Animation Events не могут подтвердить попадание.
Помимо счётчика outcome удерживает actor root последней подтверждённой цели. Ссылка
живёт только до перехода и может быть передана следующему motion action; она не
становится глобальной выбранной целью и не меняет lock-on.

`PlayerCombatTransitionCondition` задаёт дополнительное условие graph transition:

- `Any` сохраняет прежнее поведение и является serialization-safe значением по умолчанию;
- `OnHit` открывается после первого подтверждённого попадания текущего execution;
- `OnWhiff` открывается только после полного закрытия authored hitbox-window, если
  подтверждённых попаданий не было.

Graph отклоняет `OnHit` для source action без hitbox-window и `OnWhiff`, если
выбранный chain/cancel window успевает закрыться до разрешения результата промаха.
Два перехода одной команды могут перекрывать environment только тогда, когда их
условия взаимоисключающие (`OnHit` и `OnWhiff`); `Any` пересекается с обоими и
поэтому не скрывает более конкретное правило порядком элементов в списке.

Условие относится именно к переходу текущего action. Если команда не прошла
transition condition, но дожила в buffer до completion и имеет отдельное entry rule,
она по-прежнему может начаться как новое самостоятельное action. Для команды,
которая должна существовать только как hit-confirm branch, entry rule не создаётся.

## Authoring и prototype fallback

`PlayerCombatActionProfile` содержит только graph authoring: actions, entries и transitions. Параллельного legacy catalog больше нет. Пока profile не назначен, composition создаёт небольшой prototype graph:

- default input входит в первый combo step;
- следующий combo step требует `ChainWindow`;
- переход к skill требует `CancelWindow`;
- skill может вернуться к первой combo action через `CancelWindow`;
- prototype создаёт ограниченный запас combo nodes, а animation adapter определяет реально доступное число через `ComboCount`; поэтому graph строится до `Enable/ApplyLoadout`, не теряет будущие steps и безопасно поддерживает skill-only loadout.

ScriptableObject хранит только authoring data. Очередь input, текущая action, cooldown timestamps и progress timeline принадлежат scoped runtime objects.
Существующие transitions после добавления outcome-условий остаются `Any`, поэтому
serialized profile не требует migration и не меняет текущий moveset без явного tuning.

### Текущая конфигурация Player

`Assets/_Game/Settings/Combat/Player/PlayerCombatActionProfile.asset` назначен на
`Assets/_Game/Prefabs/Character/Player.prefab` и является штатным authoring-профилем игрока.
Он содержит две environment-specific ветки команды `attack.light` и одну
named action:

- три наземных actions `combo.ground.0 → 1 → 2`;
- три воздушных actions `combo.air.0 → 1 → 2`;
- самостоятельную наземную `attack.heavy`, доступную entry и из cancel window
  любого наземного light-combo step;
- наземную `skill.special`, доступную отдельным entry и из cancel window любого
  наземного light-combo step; её cancel window возвращает в `combo.ground.0`.
- hit-confirm ветку `combo.ground.0/1 → attack.launcher → movement.air-chase →
  combo.air.0`. У `attack.launcher` нет entry rule: команда `attack.launcher`
  принимается только в cancel window попавшего light action. Переход из launcher
  также требует его подтверждённого попадания; air chase затем принимает новый
  `attack.light` в chain window и переносит launcher target в воздушную combo как
  приоритетный motion-context, не меняя глобальный lock-on.

Grounded и Airborne entries не конфликтуют: `PlayerCombatActionGraph` выбирает
ветку по подтверждённому movement mode персонажа. Поэтому `attack.heavy` и
`skill.special` отклоняются в воздухе без проверки Animator или устройства ввода.

Input buffer настроен на четыре команды и при переполнении отбрасывает действие с
наименьшим приоритетом. `attack.light` использует default priority `0`,
`attack.heavy` — `10`, `attack.launcher` — `15`, `skill.special` — `20`; named commands обновляют одну
совпавшую запись вместо накопления дублей. Planar и vertical combat motion описываются
в action definition и исполняются через character motor с разрешением столкновений.
Текущие timings, damage и distances являются стартовыми tuning values. Воздушная
ветка ограничивает падение до `-1.5 m/s` только внутри combat-motion window; после
окна gravity снова полностью принадлежит character motor. Gameplay graph не хранит
ссылки на клипы: animation adapter передаёт ground/air combo index либо
`PlayerCombatAnimationId` и ожидаемую длительность в общий Animator contract.
Текущий `attack.heavy` отображается существующим `BaseAttack4`, а
`skill.special` — `Base Layer.Attack.Skill`; hit/cancel/cooldown остаются
gameplay-authoring data. Launcher переиспользует `BaseAttack3`, а air chase —
`Locomotion/Jump/Jump/JumpRunning`; структура `XController` при этом не меняется.
Heavy и special могут переходить друг в друга только
через собственные cancel windows.

## Зависимости

Разрешено:

```text
PlayerInputAdapter -> PlayerCombatCommandId
PlayerCombatController -> PlayerCombatActionGraph
PlayerCombatController -> ICombatHitEvents
PlayerCombatActionGraph -> CombatActionDefinition
PlayerCombatActionProfile -> PlayerCombatActionGraph (build only)
```

Запрещено:

```text
PlayerCombatActionGraph -> Unity Input System / Animator / Character / Cinemachine
PlayerCombatActionProfile -> runtime progress
CombatActionOutcome -> Animator / Animation Events
_App/Modules -> Project S combat ids or graph
```

Граф остаётся game-specific. Обобщать его в `_App/Modules` следует только после появления второго реального consumer с совпадающей стабильной границей.
