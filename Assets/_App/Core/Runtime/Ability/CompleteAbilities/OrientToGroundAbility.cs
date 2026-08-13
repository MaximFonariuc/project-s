using UnityEngine;

namespace _App
{
    public class OrientToGroundAbility : IAbility, IColliderFilter
    {
        private readonly Character _character;
        private readonly float _maxSlopeAngle;
        private readonly float _alignRate;
        private readonly float _rayOffset;
        private readonly LayerMask _groundMask;
        private readonly bool _drawRays;
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        
        public OrientToGroundAbility(Character character, float maxSlopeAngle, float alignRate, float rayOffset, LayerMask groundMask, bool drawRays)
        {
            _character = character;
            _maxSlopeAngle = maxSlopeAngle;
            _alignRate = alignRate;
            _rayOffset = rayOffset;
            _groundMask = groundMask;
            _drawRays = drawRays;
        }

        public bool Filter(Collider otherCollider)
        {
            CharacterMovement characterMovement = _character.GetCharacterMovement();
            if (otherCollider == characterMovement.Collider)
                return true;

            return false;
        }
        
        private Vector3 ComputeAverageNormal()
        {
            CharacterMovement characterMovement = _character.GetCharacterMovement();

            Vector3 worldUp = Vector3.up;            
            Vector3 castOrigin = _character.GetPosition() + worldUp * (characterMovement.Height * 0.5f);

            Vector3 castDirection = -worldUp;
            float castDistance = characterMovement.Height;
            LayerMask castLayerMask = _groundMask;

            Vector3 avgNormal = Vector3.zero;
            
            float x = -_rayOffset;
            float z = -_rayOffset;

            int hitCount = 0;
            
            for (int i = 0; i < 3; i++)
            {
                z = -_rayOffset;

                for (int j = 0; j < 3; j++)
                {
                    bool hit = CollisionDetection.Raycast(castOrigin + new Vector3(x, 0.0f, z), castDirection,
                        castDistance, castLayerMask, QueryTriggerInteraction.Ignore, out RaycastHit hitResult, _hits, this) > 0;
                    
                    if (hit)
                    {
                        float angle = Vector3.Angle(hitResult.normal, worldUp);
                        if (angle < _maxSlopeAngle)
                        {
                            avgNormal += hitResult.normal;

                            if (_drawRays)
                                Debug.DrawRay(hitResult.point, hitResult.normal, Color.yellow);

                            hitCount ++;
                        }    
                    }

                    z += _rayOffset;
                }

                x += _rayOffset;
            }

            if (hitCount > 0)
                avgNormal /= hitCount;
            else
                avgNormal = worldUp;

            if (_drawRays)
                Debug.DrawRay(_character.GetPosition(), avgNormal * 2f, Color.green);
            
            return avgNormal;
        }
        
        private void OnAfterSimulationUpdated(float deltaTime)
        {
            Vector3 avgNormal = _character.IsWalking() ? ComputeAverageNormal() : Vector3.up;

            Quaternion characterRotation = _character.GetRotation();
            Vector3 characterUp = characterRotation * Vector3.up;

            Quaternion slopeRotation = Quaternion.FromToRotation(characterUp, avgNormal);
            characterRotation = Quaternion.Slerp(characterRotation, slopeRotation * characterRotation, _alignRate * deltaTime);

            _character.SetRotation(characterRotation);
        }

        public void OnEnable()
        {
            _character.AfterSimulationUpdated += OnAfterSimulationUpdated;
        }

        public void OnDisable()
        {
            _character.AfterSimulationUpdated -= OnAfterSimulationUpdated;
        }
    }
}