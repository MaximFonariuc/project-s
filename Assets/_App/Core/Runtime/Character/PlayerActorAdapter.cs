using System;
using UnityEngine;

namespace _App
{
    internal sealed class PlayerActorAdapter :
        IPlayerActorTransform,
        IPlayerTargetingActor,
        IPlayerCombatActor
    {
        private readonly Character _character;

        public Transform Root => _character.transform;
        public Vector3 Position => _character.transform.position;
        public Vector3 Forward => _character.transform.forward;
        public bool CanAttack => _character.CanAttack();
        public bool IsFalling => _character.IsFalling();
        public ERotationMode RotationMode => _character.RotationMode;

        public PlayerActorAdapter(Character character)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
        }

        public void SetRotationMode(ERotationMode rotationMode)
        {
            _character.SetRotationMode(rotationMode);
        }
    }
}
