using UnityEngine;

namespace _App
{
    public class ForceZone : MonoBehaviour
    {
        [SerializeField] private Vector3 _windDirection = Vector3.up;
        [SerializeField] private float _windStrength = 20.0f;

        private void OnTriggerStay(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (!other.TryGetComponent(out Character character))
                return;
            
            Vector3 windForce = _windDirection.normalized * _windStrength;
            
            Vector3 worldUp = -character.GetGravityDirection();
            float upWindForceMagnitude = Vector3.Dot(windForce, worldUp);
            
            if (upWindForceMagnitude > 0.0f)
            {
                if (character.IsWalking() && upWindForceMagnitude - character.GetGravityMagnitude() > 0.0f)
                    character.PauseGroundConstraint();
            }
            
            character.AddForce(windForce, ForceMode.Acceleration);
        }
    }
}