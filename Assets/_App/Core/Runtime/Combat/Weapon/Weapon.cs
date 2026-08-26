using UnityEngine;

namespace _App
{
    public sealed class Weapon : MonoBehaviour, IWeaponHitboxSweepConsumer
    {
        [SerializeField] private Collider _hitbox;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction =
            QueryTriggerInteraction.Collide;
        [SerializeField, Range(1, 12)] private int _maxSweepSteps = 8;

        private CombatAttackExecution _execution;
        private ICombatHitResolver _resolver;
        private WeaponHitboxSweep _sweep;
        private bool _reportedSweepSaturation;

        internal bool IsHitboxActive =>
            _hitbox != null &&
            _hitbox.enabled &&
            _execution != null &&
            _execution.IsActive &&
            _resolver != null;

        private void Awake()
        {
            if (_hitbox == null)
            {
                _hitbox = GetComponent<Collider>();
            }

            if (_hitbox != null)
            {
                _sweep = new WeaponHitboxSweep(
                    _hitbox,
                    _targetLayers,
                    _triggerInteraction,
                    _maxSweepSteps);
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
                _reportedSweepSaturation = false;
                _sweep?.Begin();
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
            _sweep?.Reset();
        }

        internal void SampleSweptHitbox()
        {
            if (!IsHitboxActive || _sweep == null)
            {
                return;
            }

            if (_sweep.Sample(this) && !_reportedSweepSaturation)
            {
                _reportedSweepSaturation = true;
                Debug.LogWarning(
                    $"Weapon hitbox '{name}' filled its overlap buffer. " +
                    "Narrow the target layer mask or reduce collider density.",
                    this);
            }
        }

        private void OnDisable()
        {
            DisableHitbox();
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessHitboxCandidate(other);
        }

        void IWeaponHitboxSweepConsumer.ProcessHitboxCandidate(Collider other)
        {
            ProcessHitboxCandidate(other);
        }

        private void ProcessHitboxCandidate(Collider other)
        {
            if (!IsHitboxActive || other == null)
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
