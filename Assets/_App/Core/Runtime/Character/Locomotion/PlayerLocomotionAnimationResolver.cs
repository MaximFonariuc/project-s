using System;
using UnityEngine;

namespace _App
{
    internal enum PlayerLocomotionAnimationStateId
    {
        Grounded,
        JumpWalk,
        JumpRun,
        JumpSprint,
        Fall,
        LandSoft,
        LandMedium,
        LandHard,
        DodgeForward,
        DodgeBackward
    }

    internal sealed class PlayerLocomotionAnimationPolicy
    {
        public float MinimumAirborneSeconds { get; }
        public float MediumLandingSpeed { get; }
        public float HardLandingSpeed { get; }
        public float MinimumLandingSeconds { get; }
        public float SoftLandingSeconds { get; }
        public float MediumLandingSeconds { get; }
        public float HardLandingSeconds { get; }
        public float MovementInterruptThreshold { get; }

        public PlayerLocomotionAnimationPolicy(
            float minimumAirborneSeconds,
            float mediumLandingSpeed,
            float hardLandingSpeed,
            float minimumLandingSeconds,
            float softLandingSeconds,
            float mediumLandingSeconds,
            float hardLandingSeconds,
            float movementInterruptThreshold)
        {
            MinimumAirborneSeconds = Mathf.Max(0.0f, minimumAirborneSeconds);
            MediumLandingSpeed = Mathf.Max(0.01f, mediumLandingSpeed);
            HardLandingSpeed = Mathf.Max(MediumLandingSpeed + 0.01f, hardLandingSpeed);
            MinimumLandingSeconds = Mathf.Max(0.0f, minimumLandingSeconds);
            SoftLandingSeconds = Mathf.Max(MinimumLandingSeconds, softLandingSeconds);
            MediumLandingSeconds = Mathf.Max(MinimumLandingSeconds, mediumLandingSeconds);
            HardLandingSeconds = Mathf.Max(MinimumLandingSeconds, hardLandingSeconds);
            MovementInterruptThreshold = Mathf.Clamp01(movementInterruptThreshold);
        }

        public static PlayerLocomotionAnimationPolicy CreateDefaults()
        {
            return new PlayerLocomotionAnimationPolicy(
                0.08f,
                7.0f,
                12.0f,
                0.08f,
                0.28f,
                0.45f,
                0.70f,
                0.12f);
        }

        public float GetLandingSeconds(PlayerLocomotionAnimationStateId state)
        {
            return state switch
            {
                PlayerLocomotionAnimationStateId.LandSoft => SoftLandingSeconds,
                PlayerLocomotionAnimationStateId.LandMedium => MediumLandingSeconds,
                PlayerLocomotionAnimationStateId.LandHard => HardLandingSeconds,
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
            };
        }
    }

    internal sealed class PlayerLocomotionAnimationResolver
    {
        private readonly PlayerLocomotionAnimationPolicy _policy;

        private PlayerLocomotionAnimationStateId _currentState;
        private PlayerLocomotionAnimationStateId _landingState;
        private float _airborneSeconds;
        private float _maximumDownwardSpeed;
        private float _landingSeconds;
        private bool _wasAirborne;
        private bool _isLanding;
        private bool _isInitialized;

        public PlayerLocomotionAnimationResolver(PlayerLocomotionAnimationPolicy policy)
        {
            _policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }

        public bool TryResolve(
            in PlayerLocomotionSnapshot snapshot,
            float deltaTime,
            out PlayerLocomotionAnimationStateId state)
        {
            if (!snapshot.IsValid)
            {
                Reset();
                state = default;
                return false;
            }

            PlayerLocomotionAnimationStateId nextState = ResolveState(
                snapshot,
                Mathf.Max(0.0f, deltaTime));
            bool changed = !_isInitialized || _currentState != nextState;
            _currentState = nextState;
            _isInitialized = true;
            state = nextState;
            return changed;
        }

        public void Reset()
        {
            _currentState = default;
            _landingState = default;
            _airborneSeconds = 0.0f;
            _maximumDownwardSpeed = 0.0f;
            _landingSeconds = 0.0f;
            _wasAirborne = false;
            _isLanding = false;
            _isInitialized = false;
        }

        private PlayerLocomotionAnimationStateId ResolveState(
            in PlayerLocomotionSnapshot snapshot,
            float deltaTime)
        {
            if (snapshot.State == PlayerLocomotionStateId.Dodge)
            {
                CancelLanding();
                return snapshot.DodgeBackwards
                    ? PlayerLocomotionAnimationStateId.DodgeBackward
                    : PlayerLocomotionAnimationStateId.DodgeForward;
            }

            bool isAirborne = snapshot.Mode == PlayerLocomotionMode.Airborne ||
                              snapshot.State is PlayerLocomotionStateId.Jump or PlayerLocomotionStateId.Fall;
            if (isAirborne)
            {
                TrackAirborne(snapshot.VerticalSpeed, deltaTime);
                CancelLanding();
                return snapshot.State == PlayerLocomotionStateId.Jump
                    ? ResolveJump(snapshot)
                    : PlayerLocomotionAnimationStateId.Fall;
            }

            if (snapshot.Mode != PlayerLocomotionMode.Grounded)
            {
                ResetAirborne();
                CancelLanding();
                return PlayerLocomotionAnimationStateId.Grounded;
            }

            if (_wasAirborne)
            {
                BeginLandingIfNeeded();
                ResetAirborne();
            }

            if (!_isLanding)
            {
                return PlayerLocomotionAnimationStateId.Grounded;
            }

            _landingSeconds += deltaTime;
            bool canInterrupt = _landingSeconds >= _policy.MinimumLandingSeconds;
            bool movementRequested = snapshot.State == PlayerLocomotionStateId.Run ||
                                     snapshot.NormalizedSpeed >= _policy.MovementInterruptThreshold;
            if ((canInterrupt && movementRequested) ||
                _landingSeconds >= _policy.GetLandingSeconds(_landingState))
            {
                CancelLanding();
                return PlayerLocomotionAnimationStateId.Grounded;
            }

            return _landingState;
        }

        private void TrackAirborne(float verticalSpeed, float deltaTime)
        {
            if (!_wasAirborne)
            {
                _airborneSeconds = 0.0f;
                _maximumDownwardSpeed = 0.0f;
            }

            _wasAirborne = true;
            _airborneSeconds += deltaTime;
            _maximumDownwardSpeed = Mathf.Max(_maximumDownwardSpeed, -verticalSpeed);
        }

        private void BeginLandingIfNeeded()
        {
            if (_airborneSeconds < _policy.MinimumAirborneSeconds)
            {
                return;
            }

            _landingState = _maximumDownwardSpeed >= _policy.HardLandingSpeed
                ? PlayerLocomotionAnimationStateId.LandHard
                : _maximumDownwardSpeed >= _policy.MediumLandingSpeed
                    ? PlayerLocomotionAnimationStateId.LandMedium
                    : PlayerLocomotionAnimationStateId.LandSoft;
            _landingSeconds = 0.0f;
            _isLanding = true;
        }

        private static PlayerLocomotionAnimationStateId ResolveJump(
            in PlayerLocomotionSnapshot snapshot)
        {
            if (snapshot.NormalizedSpeed < 0.2f || snapshot.SpeedMode == PlayerSpeedMode.Walk)
            {
                return PlayerLocomotionAnimationStateId.JumpWalk;
            }

            return snapshot.SpeedMode == PlayerSpeedMode.Sprint
                ? PlayerLocomotionAnimationStateId.JumpSprint
                : PlayerLocomotionAnimationStateId.JumpRun;
        }

        private void ResetAirborne()
        {
            _airborneSeconds = 0.0f;
            _maximumDownwardSpeed = 0.0f;
            _wasAirborne = false;
        }

        private void CancelLanding()
        {
            _landingSeconds = 0.0f;
            _isLanding = false;
        }
    }
}
