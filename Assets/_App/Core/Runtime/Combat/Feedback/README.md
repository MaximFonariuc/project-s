# Combat feedback

This tree renders confirmed combat results without owning damage, reactions or
combat transitions.

```mermaid
classDiagram
    class CombatHitResolver
    class ICombatHitEvents
    class CombatFeedbackController
    class CombatFeedbackProfile {
        <<ScriptableObject authoring>>
    }
    class CombatFeedbackSettings {
        <<immutable runtime data>>
    }
    class CombatImpactPresentation
    class ICameraImpactFeedback
    class CinemachineCameraRig
    class ParticleSystem
    class AudioSource
    class Weapon
    class WeaponTrailRenderer

    CombatHitResolver ..|> ICombatHitEvents
    CombatFeedbackController --> ICombatHitEvents : confirmed outgoing hits
    CombatFeedbackController --> Health : confirmed incoming hits
    CombatFeedbackProfile ..> CombatFeedbackSettings : compiles once
    CombatFeedbackController --> CombatFeedbackSettings
    CombatFeedbackController --> CombatImpactPresentation
    CombatImpactPresentation --> ICameraImpactFeedback
    ICameraImpactFeedback <|.. CinemachineCameraRig
    CombatImpactPresentation --> ParticleSystem
    CombatImpactPresentation --> AudioSource
    Weapon --> WeaponTrailRenderer : gameplay hit-window intent
```

- `CombatHitResult` is the only trigger. A hitbox overlap, Animator state or
  Animation Event cannot produce feedback before gameplay confirms the hit.
- `CombatFeedbackController` filters outgoing events to the local actor and also
  observes its incoming `Health.Damaged` event. It owns subscriptions only; it
  does not change damage, hit stop, poise or reaction state.
- `CombatFeedbackProfile` maps open reaction ids to camera strength, impact
  particles and an optional spatial audio clip. Unknown ids use one explicit
  fallback entry. The profile compiles to immutable `CombatFeedbackSettings`
  during VContainer composition.
- `CombatImpactPresentation` owns one world-space particle emitter and a bounded
  pool of four `AudioSource` instances for the gameplay scope. Repeated impacts
  do not instantiate or destroy transient GameObjects.
- `CinemachineCameraRig` is the Cinemachine adapter. It owns the runtime impulse
  source/listener and uses the largest overlapping impulse, preventing rapid
  multi-hit attacks from stacking into uncontrolled camera displacement.
- Hit stop remains a separate gameplay-time concern. Particles, audio and the
  camera impulse are triggered from the same confirmed result but never request
  or release time scale themselves.
- The gameplay camera and its Cinemachine brain advance in unscaled time, so
  aiming and the impact impulse remain responsive during hit stop. Impact
  particles intentionally use scaled time: the newly emitted streaks hold on
  the contact frame and resume when gameplay time is restored.
- `WeaponTrailRenderer` is a bounded runtime ribbon sampled from the authored
  blade collider. `Weapon` starts and ends sampling with its gameplay hitbox
  window, so cancel, reaction, weapon switch and disable cannot leave a trail
  emitting. Existing samples fade on scaled time and therefore hold naturally
  during hit stop. The mesh, material and sample buffers are allocated once per
  equipped blade and released with the weapon lifecycle.
