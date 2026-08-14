using System;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;

        public event Action<CombatHitResult> Damaged;
        public event Action<CombatHitResult> Died;
        public event Action Revived;

        public int MaxHealth => _maxHealth;
        public int CurrentHealth { get; private set; }
        public bool IsAlive => CurrentHealth > 0;
        public bool IsDamageImmune { get; private set; }

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

        public void SetDamageImmune(bool immune)
        {
            IsDamageImmune = immune;
        }

        private void OnDisable()
        {
            IsDamageImmune = false;
        }
    }
}
