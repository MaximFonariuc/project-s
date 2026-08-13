using System;

namespace _App
{
    public interface ICharacterMovementPolicyLifecycle
    {
        void Enable();
        void Disable();
    }

    internal sealed class CharacterMovementPolicies :
        ICharacterMovementPolicyLifecycle,
        IDisposable
    {
        private readonly FlyingMovementPolicy _flying;
        private readonly GravityAlignmentPolicy _gravityAlignment;
        private readonly GroundOrientationPolicy _groundOrientation;
        private readonly LandingForcePolicy _landingForce;
        private readonly SwimmingMovementPolicy _swimming;

        private bool _enabled;

        public CharacterMovementPolicies(
            Character character,
            AnimationController animationController,
            CharacterFeatureSet features)
        {
            if (character == null)
            {
                throw new ArgumentNullException(nameof(character));
            }

            if (animationController == null)
            {
                throw new ArgumentNullException(nameof(animationController));
            }

            if (features == null)
            {
                throw new ArgumentNullException(nameof(features));
            }

            FlyingSettings flying = features.GetEnabled<FlyingSettings>();
            if (flying != null)
            {
                _flying = new FlyingMovementPolicy(character, flying);
            }

            if (features.GetEnabled<GravityAlignmentSettings>() != null)
            {
                _gravityAlignment = new GravityAlignmentPolicy(character);
            }

            GroundOrientationSettings groundOrientation = features.GetEnabled<GroundOrientationSettings>();
            if (groundOrientation != null)
            {
                _groundOrientation = new GroundOrientationPolicy(character, groundOrientation);
            }

            LandingForceSettings landingForce = features.GetEnabled<LandingForceSettings>();
            if (landingForce != null)
            {
                _landingForce = new LandingForcePolicy(character, landingForce);
            }

            SwimmingSettings swimming = features.GetEnabled<SwimmingSettings>();
            if (swimming != null)
            {
                _swimming = new SwimmingMovementPolicy(character, animationController, swimming);
            }
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _flying?.Enable();
            _gravityAlignment?.Enable();
            _groundOrientation?.Enable();
            _landingForce?.Enable();
            _swimming?.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _swimming?.Disable();
            _landingForce?.Disable();
            _groundOrientation?.Disable();
            _gravityAlignment?.Disable();
            _flying?.Disable();
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }
    }
}
