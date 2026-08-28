# Project S — combat action availability

## Назначение

`PlayerCombatAvailability` отвечает на узкий вопрос: можно ли сейчас принять
боевую операцию и доступна ли конкретная action. Он объединяет уже существующие
live gates, проверку semantic animation presentation и cooldown timestamps,
которые раньше хранились непосредственно в `PlayerCombatController`.

Объект не запускает action, не потребляет input buffer, не выбирает graph edge,
не меняет combat phase и не управляет Animator. `PlayerCombatController` остаётся
orchestrator, `PlayerCombatActionGraph` — владельцем routes, а
`IPlayerCombatAnimation` — presentation port.

## Дерево классов и зависимости

```mermaid
classDiagram
    direction LR

    class PlayerCombatController {
        +TryExecute(command) bool
        +ToggleWeaponAsync(target) UniTask~bool~
    }
    class PlayerCombatAvailability {
        +CanEnterAction bool
        +CanSwitchWeapon bool
        +CanUseAction(action, weapon) bool
        +RecordUse(action)
        -lastUseTime Dictionary
    }
    class IPlayerCombatActor {
        +CanAttack bool
        +IsFalling bool
    }
    class ICharacterReactionState {
        +BlocksActions bool
    }
    class IPlayerCombatAnimation {
        +CanPlayAction(character, weapon, action) bool
    }
    class ICombatClock {
        +ScaledTime float
    }
    class PlayerCombatAction {
        +Id PlayerCombatActionId
        +Definition CombatActionDefinition
    }
    class PlayerCombatComposition {
        <<VContainer composition root>>
    }

    PlayerCombatController --> PlayerCombatAvailability
    PlayerCombatAvailability --> IPlayerCombatActor : live actor gate
    PlayerCombatAvailability --> ICharacterReactionState : live reaction gate
    PlayerCombatAvailability --> IPlayerCombatAnimation : presentation support
    PlayerCombatAvailability --> ICombatClock : cooldown time policy
    PlayerCombatAvailability --> PlayerCombatAction
    PlayerCombatComposition ..> PlayerCombatAvailability : scoped construction
```

Класс остаётся в `_App/Core`: его правила используют Project S actor, reaction,
weapon и combat-action contracts. Это не универсальный модуль и не кандидат на
`_App/Modules` в текущем виде.

## Правила

`CanEnterAction` вычисляется из актуального состояния при каждом обращении:

- reaction state не блокирует действия;
- actor допускает атаку в текущем movement mode.

`CanSwitchWeapon` сохраняет отдельную политику существующего поведения:

- reaction state не блокирует действия;
- actor не находится в воздухе.

Проверки `targetWeapon`, combat phase и lifecycle `enabled` остаются у публичного
controller boundary и не дублируются в availability.

`CanUseAction` проверяет:

1. action существует;
2. её cooldown уже истёк по `ICombatClock.ScaledTime`;
3. текущий character/weapon animation loadout поддерживает semantic
   presentation через `IPlayerCombatAnimation.CanPlayAction`.

Graph environment, transition window и hit outcome проверяются до этого в
`PlayerCombatActionGraph`. Presentation adapter сообщает только наличие слота и
не получает authority над gameplay rules.

## Cooldown ownership

Cooldown записывается после штатного завершения action либо принятого graph
transition. Cancellation, reaction interruption, disable и landing до этой
границы timestamp не создают — это сохраняет прежнюю семантику controller.

Dictionary принадлежит scoped `PlayerCombatAvailability` и не очищается при
обычном disable/enable controller. Он исчезает вместе с gameplay scope. Время
намеренно scaled: hit stop и gameplay pause не расходуют cooldown, как и раньше.

Эта модель пока не является системой ресурсов. Будущие stamina/mana costs,
attributes, modifiers и regeneration должны иметь собственного owner согласно
`Character.StatsAndResources.md`; availability сможет читать их узкий контракт,
но не будет хранить второй экземпляр ресурсов.
