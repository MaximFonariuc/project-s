# Modular State Machine

Независимая конечная машина состояний для runtime-систем Project S. Ядро управляет только состояниями, разрешёнными переходами и lifecycle. Оно не знает о Unity, VContainer, Player, Enemy, Animator или конкретных правилах игры.

Модуль разделён на две assembly:

- `ModularStateMachine.Core` — container-agnostic модель без UnityEngine и сторонних зависимостей;
- `ModularStateMachine.VContainer` — небольшие registration extensions для composition root.

Документация:

- `Documentation~/Core.md` — контракты, дерево классов, lifecycle и гарантии;
- `Documentation~/VContainer.md` — внедрение состояний и направление зависимостей;
- `Documentation~/PackageBoundary.md` — граница будущего UPM-пакета.

Модуль намеренно не содержит игровые состояния. Locomotion, Combat, Enemy AI и Game Flow определяют собственные enum идентификаторов, состояния и графы переходов в своих feature assemblies.
