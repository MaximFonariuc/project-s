# Project S — combo graph

## Цель

Комбо строится как ориентированный граф gameplay-действий, а не как счётчик
ударов и не как набор переходов внутри Animator. Узел хранит combat timeline,
окружение и presentation slot. Ребро отвечает на вопрос: какой semantic input,
в каком окне и при каком результате текущего удара разрешает следующий узел.

```mermaid
classDiagram
    class IPlayerInput {
        +CombatActionRequested(commandId)
    }
    class CombatInputBuffer {
        +Store(commandId) bool
        +TryConsume(predicate, commandId) bool
    }
    class PlayerCombatController {
        -currentAction
        -observedFrame
        +TryExecute(commandId) bool
    }
    class PlayerCombatActionGraph {
        +TryResolveEntry(command, environment, action) bool
        +TryResolveTransition(source, command, environment, frame, outcome, action) bool
    }
    class PlayerCombatAction {
        +Id
        +Environment
        +Presentation
        +Definition
    }
    class PlayerCombatTransitionRule {
        +SourceActionId
        +CommandId
        +TargetActionId
        +Window
        +Condition
        +AcceptanceWindow
    }
    class CombatActionDefinition {
        +HitboxWindow
        +ChainWindow
        +CancelWindow
        +DurationSeconds
    }
    class CombatActionOutcome {
        +HasConfirmedHit
        +LastConfirmedTarget
    }
    class IPlayerCombatAnimation {
        <<presentation>>
        +TryPlayComboStep(index)
        +TryPlayNamedAnimation(id)
    }

    IPlayerInput --> PlayerCombatController
    PlayerCombatController --> CombatInputBuffer
    PlayerCombatController --> PlayerCombatActionGraph
    PlayerCombatActionGraph --> PlayerCombatAction
    PlayerCombatActionGraph --> PlayerCombatTransitionRule
    PlayerCombatAction --> CombatActionDefinition
    PlayerCombatActionGraph --> CombatActionOutcome
    PlayerCombatController --> IPlayerCombatAnimation
```

## Правила разрешения перехода

Переход принимается только если одновременно выполняются условия:

1. совпали текущий action и semantic command;
2. открыт выбранный `Chain` либо `Cancel` window исходного action;
3. если задан `AcceptanceWindow`, normalized time попадает и в этот поддиапазон;
4. выполнено условие `Any`, `OnHit` либо `OnWhiff`;
5. target action поддерживает текущее окружение `Grounded/Airborne`;
6. target action доступен по cooldown и имеет animation presentation.

`AcceptanceWindow` позволяет одному вводу иметь быстрый и pause-вариант. Такие
ветки обязаны использовать непересекающиеся диапазоны. Профиль отклоняется при
перекрывающихся диапазонах, неоднозначном окружении, выключенном исходном окне
или выходе поддиапазона за границы `Chain/Cancel`.

## Семантические команды

Схема управления не попадает в combo graph. Keyboard/mouse, gamepad и mobile UI
выдают один набор команд:

- `attack.light` — быстрый основной удар и продолжение серии;
- `attack.heavy` — силовая ветка или finisher;
- `attack.launcher` — вертикальный launcher;
- `skill.special` — оружейный special;
- dodge остаётся locomotion-командой и прерывает combat только в разрешённом
  action timeline.

Новый input pattern добавляется новым semantic command и graph route. Gameplay
не читает физическую кнопку и не просит Animator решить, какой удар следующий.

## Первый playable moveset

Первым полируется `DualSwords`. До расширения XController используются его
существующие универсальные presentation slots.

| Контекст | Ввод | Результат |
| --- | --- | --- |
| Ground | `Light → Light → Light` | основная безопасная трёхударная серия |
| Ground | `Light/Light2 → Heavy` | ранний выход в тяжёлый finisher |
| Ground | `Heavy` | самостоятельный силовой удар |
| Ground, hit confirm | `Light/Light2 → Launcher → Light` | launcher, air chase и переход в aerial chain |
| Air | `Light → Light → Light` | воздушная серия с завершающим slam |
| Ground | `Special` | отдельный оружейный skill с cooldown |

После того как основная серия станет отзывчивой, первая дополнительная ветка —
`Light → pause → Light`. Она использует поздний `AcceptanceWindow`, поэтому не
мешает обычному rapid-input продолжению. Hold-, directional- и lock-on-specific
команды добавляются только когда для них выбраны клипы и подтверждена польза в
combat UX.
## DualSwords vertical slice

`DualSwords.overrideController` использует `XController` как неизменяемый base
graph и заменяет только уникальные Humanoid motion slots. Общая locomotion и
реакции не зависят от оружия.

| Presentation slot | Frank Humanoid clip | Gameplay role |
| --- | --- | --- |
| `BaseAttack1` | `Frank_RPG_Dual_Combo01_1_IP` | Light 1 |
| `BaseAttack2` | `Frank_RPG_Dual_Combo01_2_IP` | Light 2 |
| `BaseAttack3` | `Frank_RPG_Dual_Combo01_3_IP` | Light 3 |
| `BaseAttack4` | `Frank_RPG_Dual_Skill03_PowerAttack_IP` | Heavy finisher |
| `Launcher` | `Frank_RPG_Dual_Air_Combo_Start_ZeroHeight_IP` | hit-confirm launcher |
| `AirAttack1...3` | `Frank_RPG_Dual_Air_Combo_1...3_ZeroHeight_IP` | aerial chain |
| `Skill` | `Frank_RPG_Dual_Skill01_WhirlWind_IP` | weapon special |
| `RollFront/Back` | `Frank_RPG_Dual_Evade_F/B_IP` | dodge presentation |

Профиль использует длительности исходных 60 FPS клипов. Hitbox windows сверены
с пиками скорости обеих кистей на общем Frank Humanoid rig: Light 1 — около
`0.36`, Light 2 — `0.27`, Light 3 — `0.23`, Heavy — `0.48`, Launcher — `0.36`
normalized time. Поэтому hitbox, chain и cancel windows принадлежат gameplay
профилю, но соответствуют видимым фазам конкретного moveset.

Все выбранные combat и dodge клипы — `InPlace`. `Animator.applyRootMotion`
остаётся выключенным: translation, air chase и target correction выполняются
через `CharacterMotor` и проходят обычные collision sweeps. Draw/Putup также не
передают root delta в gameplay.

Player стартует с `EWeaponType.DualHand`; привязанный override выбирается
`PlayerAnimatorLoadout`. Пока финальный prefab двух мечей не создан,
`PlayerWeaponRig` использует существующие hand hitboxes как явный prototype
fallback, поэтому визуальный loadout не отключает подтверждение попаданий.


## Animation boundary

Animator отвечает за позу, blend и визуальный выход. Combo graph отвечает за
доступность следующего действия, buffering, hit-confirm и cancel. Все оружейные
Animator Override Controller должны сохранять одинаковые state/slot names;
конкретное оружие заменяет клипы, а его combat profile задаёт timings, damage,
motion и graph routes.

На текущем этапе XController предоставляет три ground light slot, три air light
slot, heavy, launcher mapping и special. Новые визуально уникальные ветки нельзя
подключать к уже занятому state: сначала требуется явно согласовать и добавить
универсальный presentation slot в базовый controller, после чего его смогут
override-ить все виды оружия.
