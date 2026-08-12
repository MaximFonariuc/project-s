using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    public class Weapon : MonoBehaviour
    {
        [SerializeField] protected int _damage = 10;
        [SerializeField] protected Collider _hitbox;

        private readonly HashSet<int> _damagedOwners = new HashSet<int>();

        protected virtual void Awake()
        {
            if (_hitbox == null)
            {
                _hitbox = GetComponent<Collider>();
            }

            DisableHitbox();
        }

        public virtual void EnableHitbox()
        {
            _damagedOwners.Clear();

            if (_hitbox != null)
            {
                _hitbox.enabled = true;
            }
        }

        public virtual void DisableHitbox()
        {
            if (_hitbox != null)
            {
                _hitbox.enabled = false;
            }

            _damagedOwners.Clear();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            Hurtbox hurtbox = other.GetComponentInParent<Hurtbox>();
            if (hurtbox == null || !hurtbox.IsAlive)
            {
                return;
            }

            GameObject owner = hurtbox.Owner;
            if (owner.transform.root == transform.root || !_damagedOwners.Add(owner.GetInstanceID()))
            {
                return;
            }

            Vector3 hitPoint = other.ClosestPoint(_hitbox.bounds.center);
            Vector3 direction = owner.transform.position - transform.root.position;
            DamageInfo damage = new DamageInfo(_damage, hitPoint, direction.normalized, transform.root.gameObject);

            if (hurtbox.TryReceiveDamage(damage))
            {
                OnDamageApplied(hurtbox, damage);
            }
        }

        protected virtual void OnDamageApplied(Hurtbox hurtbox, DamageInfo damage) { }
    }
}
