using System;
using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    internal sealed class GroundOrientationPolicy : IColliderFilter
    {
        private readonly Character _character;
        private readonly float _maxSlopeAngle;
        private readonly float _alignRate;
        private readonly float _rayOffset;
        private readonly LayerMask _groundMask;
        private readonly bool _drawRays;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public GroundOrientationPolicy(Character character, GroundOrientationSettings settings)
        {
            _character = character ?? throw new ArgumentNullException(nameof(character));

            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            _maxSlopeAngle = settings.MaxSlopeAngle;
            _alignRate = settings.AlignRate;
            _rayOffset = settings.RayOffset;
            _groundMask = settings.GroundMask;
            _drawRays = settings.DrawRays;
        }

        public void Enable()
        {
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void Disable()
        {
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
        }

        public bool Filter(Collider otherCollider)
        {
            return otherCollider == _character.GetCharacterMovement().Collider;
        }

        private void OnAfterSimulationUpdated(float deltaTime)
        {
            Vector3 normal = _character.IsWalking() ? ComputeAverageNormal() : Vector3.up;
            Quaternion rotation = _character.GetRotation();
            Vector3 characterUp = rotation * Vector3.up;
            Quaternion slopeRotation = Quaternion.FromToRotation(characterUp, normal);

            rotation = Quaternion.Slerp(
                rotation,
                slopeRotation * rotation,
                _alignRate * deltaTime);

            _character.SetRotation(rotation);
        }

        private Vector3 ComputeAverageNormal()
        {
            CharacterMovement movement = _character.GetCharacterMovement();
            Vector3 worldUp = Vector3.up;
            Vector3 origin = _character.GetPosition() + worldUp * (movement.Height * 0.5f);
            Vector3 average = Vector3.zero;
            int hitCount = 0;

            float x = -_rayOffset;
            for (int row = 0; row < 3; row++)
            {
                float z = -_rayOffset;
                for (int column = 0; column < 3; column++)
                {
                    int count = CollisionDetection.Raycast(
                        origin + new Vector3(x, 0.0f, z),
                        -worldUp,
                        movement.Height,
                        _groundMask,
                        QueryTriggerInteraction.Ignore,
                        out RaycastHit hit,
                        _hits,
                        this);

                    if (count > 0 && Vector3.Angle(hit.normal, worldUp) < _maxSlopeAngle)
                    {
                        average += hit.normal;
                        hitCount++;

                        if (_drawRays)
                        {
                            Debug.DrawRay(hit.point, hit.normal, Color.yellow);
                        }
                    }

                    z += _rayOffset;
                }

                x += _rayOffset;
            }

            average = hitCount > 0 ? average / hitCount : worldUp;
            if (_drawRays)
            {
                Debug.DrawRay(_character.GetPosition(), average * 2.0f, Color.green);
            }

            return average;
        }
    }
}
