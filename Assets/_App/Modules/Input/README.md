# Modular Input

Независимый runtime-модуль маршрутизации ввода. Он принимает сигналы от заменяемых источников, хранит актуальные значения действий, управляет action maps и предоставляет binding overrides. Модуль ничего не знает о Player, Combat, UI, VContainer и Project S.

Документация деревьев классов:

- `Documentation~/Core.md` — стабильная абстрактная модель и маршрутизация;
- `Documentation~/UnityAdapter.md` — адаптер Unity Input System и платформенные устройства;
- `Documentation~/PackageBoundary.md` — граница будущего UPM-пакета.

Project-specific преобразование действий в команды игрока описано отдельно в `Docs/Architecture/Input.Player.md`.
