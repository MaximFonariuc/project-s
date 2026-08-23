using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Project S/Combat/Rigidbody Character Impact Motor")]
    public sealed class RigidbodyCharacterImpactMotor : MonoBehaviour, ICharacterImpactMotor
    {
        [SerializeField] private Rigidbody _body;
        [SerializeField, Min(0.01f)] private float _groundProbeDistance = 0.12f;

        public bool IsGrounded =>
            _body != null &&
            _body.SweepTest(
                -Up,
                out _,
                _groundProbeDistance,
                QueryTriggerInteraction.Ignore);

        public Vector3 Up => transform.up;
        public Vector3 Forward => transform.forward;

        private void Awake()
        {
            ResolveBody();
        }

        private void Reset()
        {
            ResolveBody();
        }

        private void OnValidate()
        {
            _groundProbeDistance = Mathf.Max(0.01f, _groundProbeDistance);
            ResolveBody();
        }

        public void ApplyKnockback(Vector3 worldVelocity)
        {
            if (_body == null || _body.isKinematic || worldVelocity.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            _body.AddForce(worldVelocity, ForceMode.VelocityChange);
        }

        public void Configure(Rigidbody body)
        {
            _body = body;
        }

        private void ResolveBody()
        {
            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }
        }
    }
}
