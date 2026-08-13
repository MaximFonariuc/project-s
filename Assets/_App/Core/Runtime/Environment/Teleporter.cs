using UnityEngine;

namespace _App
{
    public class Teleporter : MonoBehaviour
    {
        [SerializeField] private Teleporter _destination;

        [SerializeField] private bool _orientWithDestination;

        private bool _isTeleporterEnabled = true;

        private void OnTriggerEnter(Collider other)
        {
            if (_destination == null || !_isTeleporterEnabled)
                return;

            if (other.TryGetComponent(out Character character))
            {
                character.TeleportPosition(_destination.transform.position);
                
                _destination._isTeleporterEnabled = false;
                
                if (_orientWithDestination)
                {
                    Vector3 characterUp = character.GetUpVector();
                    Vector3 teleporterForward = _destination.transform.forward.ProjectedOnPlane(characterUp);

                    Quaternion newRotation = Quaternion.LookRotation(teleporterForward, character.GetUpVector());

                    character.TeleportRotation(newRotation);
                    
                    character.LaunchCharacter(teleporterForward * character.GetSpeed(), false, true);
                }
            }
        }

        private void OnTriggerExit(Collider other)
        {
            _isTeleporterEnabled = true;
        }
    }
}
