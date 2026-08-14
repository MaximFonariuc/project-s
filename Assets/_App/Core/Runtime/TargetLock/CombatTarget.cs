using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project S/Targeting/Combat Target")]
    public sealed class CombatTarget : MonoBehaviour, ICombatTarget
    {
        [SerializeField] private Transform _targetRoot;
        [SerializeField] private Transform _aimPoint;
        [SerializeField] private Collider _aimCollider;
        [SerializeField] private Health _health;

        public int TargetId => TargetRoot.GetInstanceID();
        public bool IsTargetable =>
            isActiveAndEnabled &&
            TargetRoot != null &&
            (_health == null || _health.IsAlive);
        public Transform TargetRoot => _targetRoot != null ? _targetRoot : transform;
        public Vector3 AimPosition => _aimPoint != null
            ? _aimPoint.position
            : _aimCollider != null
                ? _aimCollider.bounds.center
                : TargetRoot.position;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void ResolveReferences()
        {
            if (_targetRoot == null)
            {
                _targetRoot = transform;
            }

            if (_aimCollider == null)
            {
                _aimCollider = GetComponent<Collider>();
            }

            if (_health == null)
            {
                _health = GetComponentInParent<Health>();
            }
        }
    }
}
