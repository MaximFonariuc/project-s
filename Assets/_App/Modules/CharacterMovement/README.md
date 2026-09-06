# Modular Character Movement

Кинематический контроллер персонажа с собственным решателем коллизий. Модуль отвечает только за перемещение капсулы в физическом мире: депенетрацию, collide-and-slide, определение и удержание земли, шаг вверх, движущиеся платформы и plane constraints. Он не знает о Project S, игроке, бое, вводе, анимации и содержимом `_Game`.

Модуль состоит из одной assembly `ModularCharacterMovement`, зависящей только от Unity engine modules. Ссылка на `ProjectS.Core.Runtime` запрещена и является инвариантом.

## Происхождение

Поведенческая модель написана по образцу `UCharacterMovementComponent` из Unreal Engine, поэтому внутри сохранён словарь UE: `KINDA_SMALL_NUMBER`, `SWEEP_EDGE_REJECT_DISTANCE`, `MAX_WALKABLE_SLOPE_LIMIT`. Это собственный код проекта, а не импортированный сторонний ассет.

Практическое следствие: неочевидные места читаются по аналогии с UE и часто оказываются точными портами его ветвей, а не ошибками. Например, финальное `_velocity.ProjectedOnPlane(up).normalized * _velocity.magnitude` в `PerformMovement` — это ветка `MaintainHorizontalGroundVelocity == false`, сохраняющая величину скорости при уплощении в горизонталь.

Стиль внутри файлов (табы, `UPPER_SNAKE` константы) отличается от остального проекта и сохраняется как исторический слой.

## Состав

- `CharacterMovement.cs` — публичный API, поля, lifecycle;
- `CharacterMovement.Collision.cs` — депенетрация, `PerformMovement`, `SlideAlongSurface`, step up;
- `CharacterMovement.Grounding.cs` — поиск земли, ground constraint, perch;
- `CharacterMovement.Math.cs` — геометрия, нормали, `IsFinite`;
- `CharacterMovement.Platforms.cs` — движущиеся платформы;
- `CharacterMovement.Editor.cs` — editor-only часть того же partial-типа под `#if UNITY_EDITOR`;
- `Internal/` — владельцы состояния: grounding, силы, платформы, буферы, фильтр коллизий, physics queries;
- `Struct/` — `CollisionResult`, `FindGroundResult`, `MovingPlatform`, `Advanced`;
- `Enums/` — коды поведения коллизий, депенетрации, местоположения удара, plane constraint, поведения склона;
- `SlopeLimitBehaviour.cs` — authoring-компонент для локального переопределения предела склона;
- `CharacterMovementMath.cs` — внутренние вектор-хелперы, чтобы модуль не зависел от `Common/Extensions.cs` в Core.

## Гарантии

- Буферы physics queries выделяются один раз и растут удвоением при насыщении; аллокаций за кадр нет.
- Все запросы используют `NonAlloc` и явный layer mask.
- Итерации скольжения и депенетрации ограничены `Advanced.MaxMovementIterations` и `Advanced.MaxDepenetrationIterations`.
- `Move` отклоняет нефинитные velocity и deltaTime, сбрасывает накопленные силы и сообщает об этом один раз.

## Известные ограничения

- Публичная поверхность шире фактического использования Project S: часть API (`SimpleMove`, `AddExplosionForce`, `SetPlaneConstraint`, crouch) существует как расширение и не вызывается игрой.
- Переполнение буфера в `OverlapTest` с переданным извне массивом обрезается молча, без признака насыщения.
- Автоматизированных тестов нет.
