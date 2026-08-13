using System;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageReceiver
    {
        [SerializeField, Min(1)] private int _maxHealth = 100;

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;

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

        public bool TryReceiveDamage(DamageInfo damage)
        {
            if (!IsAlive || IsDamageImmune || damage.Amount <= 0)
            {
                return false;
            }

            CurrentHealth = Mathf.Max(0, CurrentHealth - damage.Amount);
            Damaged?.Invoke(damage);

            if (!IsAlive)
            {
                Died?.Invoke(damage);
            }

            return true;
        }

        public void RestoreFull()
        {
            CurrentHealth = _maxHealth;
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
