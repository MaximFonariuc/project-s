using UnityEngine;

namespace _App
{
    public interface IColliderFilter
    {
        bool Filter(Collider otherCollider);
    }
}