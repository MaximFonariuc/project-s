using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    public partial class Character
    {
        protected virtual void SetPhysicsVolume(PhysicsVolume newPhysicsVolume)
        {
            if (newPhysicsVolume == _physicsVolume)
                return;

            OnPhysicsVolumeChanged(newPhysicsVolume);

            _physicsVolume = newPhysicsVolume;
        }

        protected virtual void OnPhysicsVolumeChanged(PhysicsVolume newPhysicsVolume)
        {
            if (newPhysicsVolume && newPhysicsVolume.WaterVolume)
            {
                SetMovementMode(EMovementMode.Swimming);
            }
            else if (IsInWaterPhysicsVolume() && newPhysicsVolume == null)
            {
                if (IsSwimming())
                {
                    SetMovementMode(EMovementMode.Falling);
                }
            }

            PhysicsVolumeChanged?.Invoke(newPhysicsVolume);
        }

        protected virtual void UpdatePhysicsVolume(PhysicsVolume newPhysicsVolume)
        {
            Vector3 characterCenter = CharacterMovement.WorldCenter;

            if (newPhysicsVolume && newPhysicsVolume.BoxCollider.ClosestPoint(characterCenter) == characterCenter)
            {
                SetPhysicsVolume(newPhysicsVolume);
            }
            else
            {
                SetPhysicsVolume(null);
            }
        }

        protected virtual void AddPhysicsVolume(Collider other)
        {
            _physicsVolumes.Add(other);
        }

        protected virtual void RemovePhysicsVolume(Collider other)
        {
            _physicsVolumes.Remove(other);
        }

        protected virtual void UpdatePhysicsVolumes()
        {
            UpdatePhysicsVolume(_physicsVolumes.GetHighestPriority());
        }

        protected virtual bool IsInWaterPhysicsVolume()
        {
            return _physicsVolume && _physicsVolume.WaterVolume;
        }

    }
}
