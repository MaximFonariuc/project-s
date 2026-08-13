# Modular State Machine — Core

## Ответственность

`ModularStateMachine.Core` предоставляет синхронную конечную машину состояний для покадровых gameplay-систем. Машина:

- получает уже созданные экземпляры состояний;
- связывает каждый конкретный тип состояния с уникальным enum ID;
- разрешает только явно зарегистрированные переходы;
- проверяет guards;
- маршрутизирует `Tick` и `FixedTick` только текущему состоянию;
- блокирует переход из `Enter`/`Exit` другого перехода;
- не создаёт и не уничтожает состояния самостоятельно.

## Дерево классов

```mermaid
classDiagram
    direction LR

    class IStateNode {
        <<interface>>
        +Exit()
    }
    class IState {
        <<interface>>
        +Enter()
    }
    class IState~TContext~ {
        <<interface>>
        +Enter(context)
    }
    class IUpdateState {
        <<interface>>
        +Tick(deltaTime)
    }
    class IFixedUpdateState {
        <<interface>>
        +FixedTick(fixedDeltaTime)
    }
    class IStateTransitionGuard {
        <<interface>>
        +CanTransition bool
    }
    class IStateMachine~TStateId~ {
        <<interface>>
        +CurrentStateId TStateId?
        +Start~TState~()
        +ChangeState~TState~()
        +CanChangeState(target) bool
        +Tick(deltaTime)
        +FixedTick(fixedDeltaTime)
        +Stop()
    }
    class StateMachine~TStateId~ {
        -statesByType
        -registeredStateIds
        -transitions
        +StateChanged
    }
    class StateRegistration~TStateId~ {
        +StateId
        +StateType
        +State
    }
    class StateTransition~TStateId~ {
        +From
        +To
        +Guard
    }

    IStateNode <|-- IState
    IStateNode <|-- IState~TContext~
    IStateMachine~TStateId~ <|.. StateMachine~TStateId~
    StateMachine~TStateId~ o-- StateRegistration~TStateId~
    StateMachine~TStateId~ o-- StateTransition~TStateId~
    StateTransition~TStateId~ --> IStateTransitionGuard
    StateRegistration~TStateId~ --> IStateNode
```

`IState<TContext>` не наследует parameterless `IState`: контекстное состояние не обязано иметь фиктивный `Enter()`. Тип контекста проверяется компилятором в `ChangeState<TState, TContext>(context)`.

Иерархия ошибок отделяет неверную конфигурацию/команду от исключения внутри lifecycle состояния:

```mermaid
classDiagram
    class InvalidOperationException
    class StateMachineException
    class StateNotRegisteredException
    class StateTransitionNotAllowedException
    class StateTransitionGuardException
    class StateMachineReentrancyException

    InvalidOperationException <|-- StateMachineException
    StateMachineException <|-- StateNotRegisteredException
    StateMachineException <|-- StateTransitionNotAllowedException
    StateMachineException <|-- StateTransitionGuardException
    StateMachineException <|-- StateMachineReentrancyException
```

## Lifecycle перехода

```mermaid
sequenceDiagram
    participant C as Consumer
    participant M as StateMachine
    participant A as Current state
    participant G as Guard
    participant B as Target state

    C->>M: ChangeState<TTarget>()
    M->>M: resolve registration by typeof(TTarget)
    M->>M: verify explicit edge
    M->>G: CanTransition
    G-->>M: true
    M->>A: Exit()
    M->>B: Enter()
    M->>M: commit CurrentState and ID
    M-->>C: StateChanged(previous, current)
```

- `StateChanged` публикуется после завершения перехода и снятия reentrancy lock.
- Переход, запрошенный внутри `Enter` или `Exit`, отклоняется `StateMachineReentrancyException`.
- Переход из `Tick` разрешён. После него старое состояние должно немедленно завершить свой метод `Tick`.
- Self-transition возможен, только если соответствующее ребро явно зарегистрировано.
- При исключении в `Exit` или `Enter` машина переходит в stopped state: `CurrentState` и `CurrentStateId` очищаются. Для частично вошедшего target вызывается best-effort `Exit`, но предыдущий потенциально завершённый state не восстанавливается магически.
- При `Stop` текущий state получает `Exit`, после чего ссылка очищается даже при исключении.

## Guards и переходы

Guard — уже созданный объект с единственным read-only условием `CanTransition`. Guard не должен иметь side effects. Он может получать gameplay-зависимости через constructor injection вне Core.

Машина выполняет только явно запрошенные переходы. Автоматический выбор перехода и priority отсутствуют намеренно: без конкретной семантики они скрывают ownership решения. Приоритет нескольких gameplay intents должен определять владелец feature — например Combat Command Arbiter или Locomotion Controller — и отправлять машине один итоговый переход.

## Ownership

- Composition root создаёт состояния и машину.
- DI scope владеет состояниями и освобождает их зависимости.
- Машина владеет текущим ID, текущей ссылкой и процессом перехода.
- State владеет только runtime-данными своего состояния.
- `Dispose` машины останавливает lifecycle, но не вызывает `Dispose` состояний: их owner — внешний scope.

Не регистрировать один экземпляр машины как глобальный singleton для независимых actors. Каждый actor или feature получает собственную машину в соответствующем scope.
