using UnityEngine;

namespace _App
{
    public class OneWayPlatform : MonoBehaviour
    {
        [SerializeField] private Collider _platformCollider;

        private void OnTriggerEnter(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;

            Character character = other.GetComponent<Character>();
            if (character)
                character.IgnoreCollision(_platformCollider);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("Player"))
                return;
            
            Character character = other.GetComponent<Character>();
            if (character)
                character.IgnoreCollision(_platformCollider, false);
        }
    }
}
