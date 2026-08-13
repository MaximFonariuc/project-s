using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterPhysicsVolumeRegistry
    {
        private readonly List<PhysicsVolume> _volumes = new List<PhysicsVolume>();

        public void Add(Collider other)
        {
            if (other != null && other.TryGetComponent(out PhysicsVolume volume) && !_volumes.Contains(volume))
                _volumes.Insert(0, volume);
        }

        public void Remove(Collider other)
        {
            if (other != null && other.TryGetComponent(out PhysicsVolume volume))
                _volumes.Remove(volume);
        }

        public PhysicsVolume GetHighestPriority()
        {
            PhysicsVolume result = null;
            int highestPriority = int.MinValue;

            for (int index = 0; index < _volumes.Count; index++)
            {
                PhysicsVolume volume = _volumes[index];
                if (volume == null)
                {
                    _volumes.RemoveAt(index);
                    index--;
                    continue;
                }

                if (volume.Priority <= highestPriority)
                    continue;

                highestPriority = volume.Priority;
                result = volume;
            }

            return result;
        }
    }
}
