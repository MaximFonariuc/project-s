using System;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterInteractionProbe : IPlayerInteractionProbe
    {
        private const int InteractionLayerMask = 1;

        private readonly Character _character;

        public CharacterInteractionProbe(Character character)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
        }

        public bool HasInteractable()
        {
            Collider[] overlappedColliders = _character.CharacterMovement.OverlapTest(
                InteractionLayerMask,
                QueryTriggerInteraction.Collide,
                out int overlapCount);

            return overlapCount > 0 &&
                   overlappedColliders[0] != null &&
                   overlappedColliders[0].TryGetComponent(out IInteractable _);
        }
    }
}
