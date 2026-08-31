# Combat style HUD

The HUD follows MVP Passive View and is optional for gameplay composition.

```mermaid
classDiagram
    class ICombatStyleState {
        <<model port>>
        +Snapshot CombatStyleSnapshot
        +Changed CombatStyleSnapshot
    }
    class CombatStylePresenter {
        <<plain C# presenter>>
        +Start()
        +Dispose()
    }
    class CombatStyleHudView {
        <<Unity view adapter>>
        +Render(snapshot, rankProgress)
    }
    class CombatStyleSettings
    class GameLifetimeScope {
        <<composition root>>
    }

    GameLifetimeScope --> CombatStylePresenter : scene lifecycle
    GameLifetimeScope --> CombatStyleHudView : serialized reference
    CombatStylePresenter --> ICombatStyleState
    CombatStylePresenter --> CombatStyleSettings : rank progress
    CombatStylePresenter --> CombatStyleHudView
```

- The presenter subscribes to the read-only style model and converts score to
  rank progress. It does not call combat commands or inspect scene actors.
- The view owns only TextMesh Pro, Image and CanvasGroup references. It formats
  numeric values with TMP overloads and never reads gameplay state directly.
- Absence of a HUD reference skips presenter composition; combat style remains
  operational in scenes that intentionally have no screen UI.
- VContainer scene scope owns subscription disposal. The hidden view does not
  block raycasts and cannot interfere with player input.
