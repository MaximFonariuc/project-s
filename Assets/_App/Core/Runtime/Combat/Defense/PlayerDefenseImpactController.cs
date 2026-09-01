using System;
using UnityEngine;

namespace _App
{
    /// <summary>
    /// Converts confirmed defensive outcomes into bounded motor impulses.
    /// Defense decides the outcome; the character motor remains the sole
    /// position and collision authority.
    /// </summary>
    internal sealed class PlayerDefenseImpactController : IDisposable
    {
        private const float DirectionThresholdSqr = 0.0001f;

        private readonly IPlayerDefenseState _defense;
        private readonly IPlayerDefenseActor _actor;
        private readonly ICharacterImpactMotor _motor;
        private readonly PlayerDefenseSettings _settings;
        private bool _enabled;

        public PlayerDefenseImpactController(
            IPlayerDefenseState defense,
            IPlayerDefenseActor actor,
            ICharacterImpactMotor motor,
            PlayerDefenseSettings settings)
        {
            _defense = defense ?? throw new ArgumentNullException(nameof(defense));
            _actor = actor ?? throw new ArgumentNullException(nameof(actor));
            _motor = motor ?? throw new ArgumentNullException(nameof(motor));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _defense.Defended += OnDefended;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _defense.Defended -= OnDefended;
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }

        private void OnDefended(PlayerDefenseResult result)
        {
            if (!_enabled || result.Outcome != CombatHitOutcome.Blocked ||
                _settings.BlockPushbackSpeed <= 0.0f)
            {
                return;
            }

            Vector3 up = _motor.Up;
            if (up.sqrMagnitude <= DirectionThresholdSqr)
            {
                up = _actor.Up.sqrMagnitude > DirectionThresholdSqr
                    ? _actor.Up.normalized
                    : Vector3.up;
            }
            else
            {
                up.Normalize();
            }

            CombatHitResult hit = result.Hit;
            Vector3 away = Vector3.ProjectOnPlane(hit.Direction, up);
            if (away.sqrMagnitude <= DirectionThresholdSqr && hit.Source != null)
            {
                away = Vector3.ProjectOnPlane(
                    _actor.Position - hit.Source.transform.position,
                    up);
            }

            if (away.sqrMagnitude <= DirectionThresholdSqr)
            {
                away = Vector3.ProjectOnPlane(-_actor.Forward, up);
            }

            if (away.sqrMagnitude > DirectionThresholdSqr)
            {
                _motor.ApplyKnockback(
                    away.normalized * _settings.BlockPushbackSpeed);
            }
        }
    }
}
