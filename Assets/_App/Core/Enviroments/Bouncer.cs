using UnityEngine;

namespace _App
{

    public class Bouncer : MonoBehaviour
    {
        [SerializeField] private float _launchImpulse = 15.0f;
        [SerializeField] private bool _overrideVerticalVelocity;
        [SerializeField] private bool _overrideLateralVelocity;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            if (!other.TryGetComponent(out Character character))
                return;
            
            character.PauseGroundConstraint();
            character.LaunchCharacter(transform.up * _launchImpulse, _overrideVerticalVelocity, _overrideLateralVelocity);
        }
    }
}
