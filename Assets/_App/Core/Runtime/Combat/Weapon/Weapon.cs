using UnityEngine;

namespace _App
{
    public sealed class Weapon : MonoBehaviour
    {
        [SerializeField] private Collider _hitbox;

        private CombatAttackExecution _execution;
        private ICombatHitResolver _resolver;

        private void Awake()
        {
            if (_hitbox == null)
            {
                _hitbox = GetComponent<Collider>();
            }

            DisableHitbox();
        }

        internal void EnableHitbox(
            CombatAttackExecution execution,
            ICombatHitResolver resolver)
        {
            _execution = execution != null && execution.IsActive
                ? execution
                : null;
            _resolver = resolver;

            if (_hitbox != null && _execution != null && _resolver != null)
            {
                _hitbox.enabled = true;
            }
        }

        internal void DisableHitbox()
        {
            if (_hitbox != null)
            {
                _hitbox.enabled = false;
            }

            _execution = null;
            _resolver = null;
        }

        private void OnDisable()
        {
            DisableHitbox();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_execution == null || !_execution.IsActive || _resolver == null)
            {
                return;
            }

            Hurtbox hurtbox = other.GetComponentInParent<Hurtbox>();
            if (hurtbox == null || !hurtbox.IsAlive)
            {
                return;
            }

            GameObject owner = hurtbox.Owner;
            if (owner == null)
            {
                return;
            }

            Vector3 hitPoint = other.ClosestPoint(_hitbox.bounds.center);
            Vector3 direction = owner.transform.position - _execution.Source.transform.position;
            var candidate = new CombatHitCandidate(hurtbox, hitPoint, direction);
            _resolver.TryResolve(_execution, in candidate, out _);
        }
    }
}
