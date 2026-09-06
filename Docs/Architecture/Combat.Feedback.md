# Combat Feedback

Combat Feedback отвечает за подтверждённую презентацию удара: импульс камеры, частицы и звук. Он не принимает боевых решений и не владеет ни здоровьем, ни реакцией, ни временем — только отображает уже подтверждённый `CombatHitResult`.

## Владельцы

- `CombatFeedbackController` — scoped owner подписок. Слушает три источника и сводит их к одному вызову `Present`:
  - `ICombatHitEvents.HitResolved` — исходящие попадания, отфильтрованные по локальному actor через `IsLocalSource`;
  - `Health.Damaged` — входящий урон;
  - `IPlayerDefenseState.Defended` — блок и парирование.
- `CombatFeedbackSettings` — runtime-модель профиля. Выбирает `CombatFeedbackDefinition` по исходу удара: `Blocked`, `Parried`, иначе по `CombatReactionId` из таблицы override, иначе `Default`.
- `CombatImpactPresentation` — единственный владелец рантайм-объекта `Combat Feedback (Runtime)`, создаваемого под actor. Держит один `ParticleSystem` и пул из четырёх `AudioSource`.
- `CombatFeedbackProfile` — authoring-данные ScriptableObject.

Регистрация находится в `PlayerCombatFeedbackComposition`; в gameplay-код она не просачивается.

## Презентация процедурная

Частицы не являются префабом. `CombatImpactPresentation` создаёт `ParticleSystem` в рантайме и генерирует для него material и texture, а профиль задаёт только параметры: количество, скорость, разброс, время жизни, размер и цвет. Поэтому новый вариант удара не требует нового VFX-ассета — достаточно строки в профиле.

Звук воспроизводится через `PlayOneShot` на свободном источнике из пула, с позиционированием в точку удара и случайным pitch в авторском диапазоне. Поле `AudioSource.clip` при этом остаётся пустым — это ожидаемо и не является признаком отсутствия звука.

Точка и направление удара берутся из `CombatHitResult`; нефинитные значения заменяются fallback-точкой, поэтому испорченный hit не ломает презентацию.

## Hit stop

Профиль может переопределить hit stop через `OverridesHitStop` и `HitStopOverride`. Флаг и значение обязаны описывать одну и ту же политику, иначе `CombatFeedbackDefinition` бросает исключение при построении. Владельцем `Time.timeScale` остаётся `CombatHitStopController`; правила описаны в `Docs/Architecture/Combat.HitStop.md`.

## Текущее наполнение

`PlayerCombatFeedbackProfile` описывает семь вариантов: `Default`, `Blocked`, `Parried` и override для `hit.light`, `hit.heavy`, `hit.launcher`, `hit.slam`. Каждый имеет свой цвет частиц, импульс камеры и звук.

Звуки лежат в `Assets/_Game/Audio/Combat` и являются **процедурно синтезированными плейсхолдерами**, а не финальным аудио:

| Клип | Назначение | Синтез |
| --- | --- | --- |
| `Impact_Light` | `Default`, `hit.light` | шумовой всплеск + нисходящий синус 210 Гц, LP 5.2 кГц |
| `Impact_Heavy` | `hit.heavy` | то же, 128 Гц, LP 3.2 кГц, длиннее спад |
| `Impact_Launcher` | `hit.launcher` | 175 Гц, LP 4.2 кГц |
| `Impact_Slam` | `hit.slam` | 92 Гц, LP 2.4 кГц, самый длинный |
| `Impact_Block` | `Blocked` | негармонические партиалы 430 Гц (1 / 2.76 / 5.40 / 8.93 / 13.34) + шумовой тик |
| `Impact_Parry` | `Parried` | то же, 620 Гц, ярче и звонче |

Все клипы моно 44.1 кГц, PCM, `DecompressOnLoad`. Заменяются на финальные без изменений в коде — достаточно переназначить `_audioClip` в профиле.

## Границы

- Feedback читает подтверждённый результат и никогда не вычисляет урон, реакцию или knockback.
- Контроллер не знает об Animator и не управляет камерой напрямую: импульс уходит через `ICameraImpactFeedback`.
- Профиль запрещает `ReactionId` у `Default`, `Blocked` и `Parried`: защитные исходы выбираются по outcome, а не по реакции.
- Дубликаты `ReactionId` в override отвергаются при построении настроек.
- Пул источников фиксирован; при исчерпании переиспользуется следующий по кругу, поэтому очень плотная серия ударов может обрезать самый старый звук.
