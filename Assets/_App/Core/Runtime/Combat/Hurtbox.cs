using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Hurtbox : MonoBehaviour, ICombatHitTarget
    {
        [SerializeField] private BodyColliderRig _rig;
        [SerializeField] private Health _health;
        [SerializeField] private BodyHitZone _zone = BodyHitZone.Root;
        [SerializeField] private string _customZoneId;
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField] private Collider _collider;

        public bool IsAlive => _rig != null ? _rig.IsAlive : _health != null && _health.IsAlive;
        public GameObject Owner => _rig != null ? _rig.Owner : _health != null ? _health.gameObject : gameObject;
        public BodyHitZone Zone => _zone;
        public string ZoneId => BodyColliderBoneDefinition.GetZoneId(_zone, _customZoneId);
        public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);

        int ICombatHitTarget.TargetId
        {
            get
            {
                GameObject owner = Owner;
                return owner != null
                    ? owner.transform.root.gameObject.GetInstanceID()
                    : transform.root.gameObject.GetInstanceID();
            }
        }

        bool ICombatHitTarget.IsAlive => IsAlive;
        GameObject ICombatHitTarget.Owner => Owner;
        string ICombatHitTarget.HitZoneId => ZoneId;
        float ICombatHitTarget.DamageMultiplier => DamageMultiplier;

        private void Awake()
        {
            ResolveReferences();

            if (_rig == null && _health == null)
            {
                Debug.LogError($"{nameof(Hurtbox)} on '{name}' requires a {nameof(BodyColliderRig)} or {nameof(Health)} owner.", this);
            }
        }

        private void OnValidate()
        {
            _damageMultiplier = Mathf.Max(0f, _damageMultiplier);
            ResolveReferences();
        }

        bool ICombatHitTarget.TryResolveHit(
            in CombatHitResult proposedHit,
            out CombatHitResult resolvedHit)
        {
            if (proposedHit.Damage <= 0 || _damageMultiplier <= 0f)
            {
                resolvedHit = default;
                return false;
            }

            if (_rig != null)
            {
                return _rig.TryResolveHit(in proposedHit, out resolvedHit);
            }

            if (_health != null && _health.TryApplyHit(in proposedHit))
            {
                resolvedHit = proposedHit;
                return true;
            }

            resolvedHit = default;
            return false;
        }

        public void SetColliderEnabled(bool enabled)
        {
            if (_collider != null)
            {
                _collider.enabled = enabled;
            }
        }

        public void Configure(
            BodyColliderRig rig,
            Health health,
            BodyHitZone zone,
            string customZoneId,
            float damageMultiplier,
            Collider collider)
        {
            _rig = rig;
            _health = health;
            _zone = zone;
            _customZoneId = customZoneId;
            _damageMultiplier = Mathf.Max(0f, damageMultiplier);
            _collider = collider;
        }

        private void ResolveReferences()
        {
            if (_rig == null)
            {
                _rig = GetComponentInParent<BodyColliderRig>();
            }

            if (_health == null)
            {
                _health = _rig != null ? _rig.GetComponent<Health>() : GetComponentInParent<Health>();
            }

            if (_collider == null)
            {
                _collider = GetComponent<Collider>();
            }
        }
    }
}
