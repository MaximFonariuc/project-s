# Project S — Combat Operation Lifetime

## Ответственность

`PlayerCombatOperationLifetime` является единым owner активного
`CancellationTokenSource` для attack pipeline либо weapon transition. Он
гарантирует общий порядок cancellation: остановить animation action, отменить
async continuation, abort обе возможные sessions и освободить token source.

```mermaid
classDiagram
    direction LR

    class PlayerCombatController {
        +ToggleWeaponAsync(target) UniTask~bool~
        +InterruptAttack()
        -CompleteAction(tokenSource)
    }
    class PlayerCombatOperationLifetime {
        -activeCancellation CancellationTokenSource
        +Begin() CancellationTokenSource
        +TryComplete(tokenSource) bool
        +Cancel(reason)
    }
    class IPlayerCombatAnimation {
        +Cancel()
    }
    class CombatActionSession {
        +Abort(reason)
    }
    class PlayerWeaponTransitionSession {
        +Abort()
    }
    class PlayerCombatComposition {
        <<VContainer composition root>>
    }

    PlayerCombatController --> PlayerCombatOperationLifetime : begin / complete / cancel
    PlayerCombatOperationLifetime --> IPlayerCombatAnimation : presentation cleanup
    PlayerCombatOperationLifetime --> CombatActionSession : attack abort
    PlayerCombatOperationLifetime --> PlayerWeaponTransitionSession : transition abort
    PlayerCombatComposition ..> PlayerCombatOperationLifetime : scoped construction
```

## Инварианты

- Одновременно существует не более одного активного operation token.
- `Begin` сначала отменяет предыдущую операцию с причиной `Cancelled`.
- Только owner текущего token source может выполнить completion cleanup;
  запоздавший `finally` старой UniTask получает `false` от `TryComplete`.
- `Cancel` вызывает обе session boundaries даже при отсутствии token source,
  чтобы частично начатая session не пережила disable/interruption.
- Вложенные `finally` гарантируют abort и dispose, даже если один callback
  cancellation/animation выбросил исключение.

Контроллер остаётся владельцем combat phase, input buffer, combo index и
возврата в `Ready`. Lifetime не выбирает actions и не регистрируется как
публичный service/interface: это конкретный scoped collaborator Player Combat.

