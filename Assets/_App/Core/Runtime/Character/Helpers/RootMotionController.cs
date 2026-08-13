using UnityEngine;

namespace _App
{
    public enum RootMotionPolicy
    {
        Ignore,
        Translation,
        Rotation,
        Full
    }

    [RequireComponent(typeof(Animator))]
    public class RootMotionController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody _rigidbody;

        private Vector3 _deltaPosition;
        private Quaternion _deltaRotation = Quaternion.identity;
        private RootMotionPolicy _policy = RootMotionPolicy.Ignore;

        public RootMotionPolicy Policy => _policy;
        
        private void Awake()
        {
            if (_rigidbody == null)
            {
                _rigidbody = GetComponentInParent<Rigidbody>();
            }

            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            _animator.applyRootMotion = true;
        }

        private void OnAnimatorMove()
        {
            if (_policy is RootMotionPolicy.Translation or RootMotionPolicy.Full)
            {
                _deltaPosition += _animator.deltaPosition;
            }

            if (_policy is RootMotionPolicy.Rotation or RootMotionPolicy.Full)
            {
                _deltaRotation *= _animator.deltaRotation;
            }
        }

        public void SetPolicy(RootMotionPolicy policy)
        {
            if (_policy == policy)
            {
                return;
            }

            _policy = policy;
            ResetDeltas();
        }

        public bool RootMovementUpdate(out Vector3 position, out Quaternion rotation)
        {
            if (_rigidbody == null || _policy == RootMotionPolicy.Ignore ||
                (_deltaPosition.sqrMagnitude <= 0.0000001f &&
                 Quaternion.Angle(Quaternion.identity, _deltaRotation) <= 0.0001f))
            {
                position = default;
                rotation = default;
                return false;
            }

            position = _rigidbody.position + _deltaPosition;
            rotation = _rigidbody.rotation * _deltaRotation;
         
            ResetDeltas();
            
            return true;
        }

        private void OnDisable()
        {
            ResetDeltas();
        }

        private void ResetDeltas()
        {
            _deltaPosition = Vector3.zero;
            _deltaRotation = Quaternion.identity;
        }
    }
}
