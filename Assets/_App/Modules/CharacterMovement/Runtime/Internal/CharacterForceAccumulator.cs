using UnityEngine;

namespace ModularCharacterMovement
{
    internal sealed class CharacterForceAccumulator
    {
        private Vector3 _forces;
        private Vector3 _impulses;
        private Vector3 _launchVelocity;
        private bool _hasLaunchVelocity;

        public void Clear()
        {
            _forces = Vector3.zero;
            _impulses = Vector3.zero;
            _launchVelocity = Vector3.zero;
            _hasLaunchVelocity = false;
        }

        public void Add(Vector3 force, ForceMode forceMode, float mass)
        {
            switch (forceMode)
            {
                case ForceMode.Force:
                    _forces += force / mass;
                    break;

                case ForceMode.Acceleration:
                    _forces += force;
                    break;

                case ForceMode.Impulse:
                    _impulses += force / mass;
                    break;

                case ForceMode.VelocityChange:
                    _impulses += force;
                    break;
            }
        }

        public void Launch(Vector3 launchVelocity, Vector3 currentVelocity, Vector3 characterUp,
            bool overrideVerticalVelocity, bool overrideLateralVelocity)
        {
            Vector3 finalVelocity = launchVelocity;

            if (!overrideLateralVelocity)
                finalVelocity += currentVelocity.ProjectedOnPlane(characterUp);

            if (!overrideVerticalVelocity)
                finalVelocity += currentVelocity.ProjectedOn(characterUp);

            _launchVelocity = finalVelocity;
            _hasLaunchVelocity = true;
        }

        public Vector3 Consume(Vector3 velocity, float deltaTime)
        {
            velocity += _forces * deltaTime;
            velocity += _impulses;

            if (_hasLaunchVelocity)
                velocity = _launchVelocity;

            Clear();
            return velocity;
        }
    }
}
