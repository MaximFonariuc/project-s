using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    public sealed class Hurtbox : MonoBehaviour, IDamageReceiver
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

        public bool TryReceiveDamage(DamageInfo damage)
        {
            return TryReceiveDamage(damage, out _);
        }

        public bool TryReceiveDamage(DamageInfo damage, out DamageInfo appliedDamage)
        {
            appliedDamage = damage;

            if (damage.Amount <= 0 || _damageMultiplier <= 0f)
            {
                return false;
            }

            int amount = Mathf.Max(1, Mathf.RoundToInt(damage.Amount * _damageMultiplier));
            appliedDamage = amount == damage.Amount
                ? damage
                : new DamageInfo(amount, damage.Point, damage.Direction, damage.Source);

            return _rig != null
                ? _rig.TryReceiveDamage(appliedDamage)
                : _health != null && _health.TryReceiveDamage(appliedDamage);
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
