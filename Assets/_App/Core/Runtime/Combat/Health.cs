using System;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;

        private int _damageImmunityLeaseCount;
        private int _damageImmunityGeneration;

        public event Action<CombatHitResult> Damaged;
        public event Action<CombatHitResult> Died;
        public event Action Revived;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool IsDamageImmune => _damageImmunityLeaseCount > 0;

        private void Awake()
        {
            RestoreFull();
        }

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1, _maxHealth);
        }

        internal bool TryApplyHit(in CombatHitResult hit)
        {
            if (!IsAlive || IsDamageImmune || hit.Damage <= 0)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - hit.Damage);
            Damaged?.Invoke(hit);

            if (!IsAlive)
            {
                Died?.Invoke(hit);
            }

            return true;
        }

        public void RestoreFull()
        {
            bool wasDead = !IsAlive;
            CurrentHealth = _maxHealth;

            if (wasDead)
            {
                Revived?.Invoke();
            }
        }

        public IDisposable AcquireDamageImmunity()
        {
            _damageImmunityLeaseCount++;
            return new DamageImmunityLease(this, _damageImmunityGeneration);
        }

        private void OnDisable()
        {
            _damageImmunityGeneration++;
            _damageImmunityLeaseCount = 0;
        }

        private void ReleaseDamageImmunity(int generation)
        {
            if (generation != _damageImmunityGeneration || _damageImmunityLeaseCount <= 0)
            {
                return;
            }

            _damageImmunityLeaseCount--;
        }

        private sealed class DamageImmunityLease : IDisposable
        {
            private Health _owner;
            private readonly int _generation;

            public DamageImmunityLease(Health owner, int generation)
            {
                _owner = owner;
                _generation = generation;
            }

            public void Dispose()
            {
                Health owner = _owner;
                if (owner == null)
                {
                    return;
                }

                _owner = null;
                owner.ReleaseDamageImmunity(_generation);
            }
        }
    }
}
