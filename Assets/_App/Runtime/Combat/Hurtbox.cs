using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Hurtbox : MonoBehaviour, IDamageReceiver
    {
        [SerializeField] private Health _health;

        public bool IsAlive => _health != null && _health.IsAlive;
        public GameObject Owner => _health != null ? _health.gameObject : gameObject;

        private void Awake()
        {
            if (_health == null)
            {
                _health = GetComponentInParent<Health>();
            }

            if (_health == null)
            {
                Debug.LogError($"{nameof(Hurtbox)} on '{name}' requires a {nameof(Health)} owner.", this);
            }
        }

        public bool TryReceiveDamage(DamageInfo damage)
        {
            return _health != null && _health.TryReceiveDamage(damage);
        }
    }
}
