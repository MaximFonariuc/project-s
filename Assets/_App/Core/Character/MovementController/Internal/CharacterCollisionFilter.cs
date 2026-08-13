using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    internal sealed class CharacterCollisionFilter
    {
        private readonly HashSet<Rigidbody> _ignoredRigidbodies = new HashSet<Rigidbody>();
        private readonly HashSet<Collider> _ignoredColliders = new HashSet<Collider>();

        private Rigidbody _selfRigidbody;
        private CapsuleCollider _selfCollider;

        public void Bind(Rigidbody selfRigidbody, CapsuleCollider selfCollider)
        {
            _selfRigidbody = selfRigidbody;
            _selfCollider = selfCollider;
        }

        public bool ShouldFilter(Collider otherCollider, CharacterMovement.ColliderFilterCallback externalFilter)
        {
            if (otherCollider == null)
                return true;

            if (otherCollider == _selfCollider || otherCollider.attachedRigidbody == _selfRigidbody)
                return true;

            if (_ignoredColliders.Contains(otherCollider))
                return true;

            Rigidbody attachedRigidbody = otherCollider.attachedRigidbody;
            if (attachedRigidbody != null && _ignoredRigidbodies.Contains(attachedRigidbody))
                return true;

            return externalFilter != null && externalFilter.Invoke(otherCollider);
        }

        public void SetIgnored(Collider otherCollider, bool ignored)
        {
            if (otherCollider == null)
                return;

            if (ignored)
                _ignoredColliders.Add(otherCollider);
            else
                _ignoredColliders.Remove(otherCollider);
        }

        public void SetIgnored(Rigidbody otherRigidbody, bool ignored)
        {
            if (otherRigidbody == null)
                return;

            if (ignored)
                _ignoredRigidbodies.Add(otherRigidbody);
            else
                _ignoredRigidbodies.Remove(otherRigidbody);
        }

        public void SetIgnoredByPhysics(Collider otherCollider, bool ignored)
        {
            if (_selfCollider != null && otherCollider != null)
                Physics.IgnoreCollision(_selfCollider, otherCollider, ignored);
        }
    }
}
