using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project S/Combat/Body Collider Rig")]
    public sealed class BodyColliderRig : MonoBehaviour
    {
        [SerializeField] private Health _health;
        [SerializeField] private List<Hurtbox> _zones = new List<Hurtbox>();

        public bool IsAlive => _health != null && _health.IsAlive;
        public GameObject Owner => _health != null ? _health.gameObject : gameObject;
        public IReadOnlyList<Hurtbox> Zones => _zones;

        private void Awake()
        {
            ResolveHealth();

            if (_health == null)
            {
                Debug.LogError($"{nameof(BodyColliderRig)} on '{name}' requires a {nameof(Health)} owner.", this);
            }
        }

        internal bool TryResolveHit(
            in CombatHitResult proposedHit,
            out CombatHitResult resolvedHit)
        {
            if (_health != null && _health.TryApplyHit(in proposedHit))
            {
                resolvedHit = proposedHit;
                return true;
            }

            resolvedHit = default;
            return false;
        }

        public void SetHurtboxesEnabled(bool enabled)
        {
            for (int i = 0; i < _zones.Count; i++)
            {
                Hurtbox zone = _zones[i];
                if (zone != null)
                {
                    zone.SetColliderEnabled(enabled);
                }
            }
        }

        public void Configure(Health health, IReadOnlyList<Hurtbox> zones)
        {
            _health = health;
            _zones.Clear();

            if (zones == null)
            {
                return;
            }

            for (int i = 0; i < zones.Count; i++)
            {
                Hurtbox zone = zones[i];
                if (zone != null && !_zones.Contains(zone))
                {
                    _zones.Add(zone);
                }
            }
        }

        private void ResolveHealth()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }
        }
    }
}
