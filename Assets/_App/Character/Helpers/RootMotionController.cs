using UnityEngine;

namespace _App
{
    [RequireComponent(typeof(Animator))]
    public class RootMotionController : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private Animator _animator;
        [SerializeField] private Rigidbody _rigidbody;

        private Vector3 _deltaPosition;
        private Quaternion _deltaRotation = Quaternion.identity;
        
        private void Start()
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
            _deltaPosition += _animator.deltaPosition;
            _deltaRotation = _animator.deltaRotation;
        }

        public bool RootMovementUpdate(out Vector3 position, out Quaternion rotation)
        {
            if (_rigidbody == null)
            {
                position = default;
                rotation = default;
                return false;
            }

            position = _rigidbody.position + _deltaPosition;
            rotation = _rigidbody.rotation * _deltaRotation;
         
            _deltaPosition = Vector3.zero;
            _deltaRotation = Quaternion.identity;
            
            return true;
        }
    }
}