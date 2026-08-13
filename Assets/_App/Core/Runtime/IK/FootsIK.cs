using UnityEngine;

namespace _App
{
    [RequireComponent(typeof(Animator))]
    public class FootIK : MonoBehaviour
    {
        [Header("Main")]
        [SerializeField, Range(0, 1)] private float _weight = 1f;
    
        [Header("Settings")]
        [SerializeField] private float _maxStep = 0.5f;
        [SerializeField] private float _footRadius = 0.15f;
        [SerializeField] private LayerMask _ground = 1;
        [SerializeField] private float _groundOffset = 0f;
    
        [Header("Speed")]
        [SerializeField] private float _hipsPositionSpeed = 1f;
        [SerializeField] private float _feetPositionSpeed = 2f;
        [SerializeField] private float _feetRotationSpeed = 90;
    
        [Header("Weight")]
        [SerializeField, Range(0, 1)] private float _hipsWeight = 0.75f;
        [SerializeField, Range(0, 1)] private float _footPositionWeight = 1f;
        [SerializeField, Range(0, 1)] private float _footRotationWeight = 1f;
        [SerializeField] private bool _showDebug = true;

        private Vector3 _likPosition, _rikPosition, _lNormal, _rNormal;
        private Quaternion _likRotation, _rikRotation, _lastLeftRotation, _lastRightRotation;
        private float _lastRFootHeight, _lastLFootHeight;

        private Animator _animator;
        private float _velocity;
        private float _falloffWeight;
        private float _lastHeight;
        private Vector3 _lastPosition;
        private bool _lGrounded, _rGrounded, _isGrounded;

        void Start()
        {
            _animator = GetComponent<Animator>();
        }

        public void EnableIK(bool enable)
        {
            _weight = enable ? 1f : 0f;
        }
        
        private void FixedUpdate()
        {
            if (_weight == 0 || !_animator)
            {
                return;
            }

            Vector3 speed = (_lastPosition - _animator.transform.position) / Time.fixedDeltaTime;
            _velocity = Mathf.Clamp(speed.magnitude, 1, speed.magnitude);
            _lastPosition = _animator.transform.position;

            FeetSolver(HumanBodyBones.LeftFoot, ref _likPosition, ref _lNormal, ref _likRotation, ref _lGrounded);
            FeetSolver(HumanBodyBones.RightFoot, ref _rikPosition, ref _rNormal, ref _rikRotation, ref _rGrounded);

            GetGrounded();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_weight == 0 || !_animator) { return; }

            MovePelvisHeight();

            MoveIK(AvatarIKGoal.LeftFoot, _likPosition, _lNormal, _likRotation, ref _lastLFootHeight, ref _lastLeftRotation);

            MoveIK(AvatarIKGoal.RightFoot, _rikPosition, _rNormal, _rikRotation, ref _lastRFootHeight, ref _lastRightRotation);
        }

        private void MovePelvisHeight()
        {
            float leftOffset = _likPosition.y - _animator.transform.position.y;
            float rightOffset = _rikPosition.y - _animator.transform.position.y;
            float totalOffset = (leftOffset < rightOffset) ? leftOffset : rightOffset;

            Vector3 newPosition = _animator.bodyPosition;
            float newHeight = totalOffset * (_hipsWeight * _falloffWeight);
            _lastHeight = Mathf.MoveTowards(_lastHeight, newHeight, _hipsPositionSpeed * Time.deltaTime);
            newPosition.y += _lastHeight + _groundOffset;

            _animator.bodyPosition = newPosition;
        }

        void MoveIK(AvatarIKGoal foot, Vector3 ikPosition, Vector3 normal, Quaternion ikRotation, ref float lastHeight, ref Quaternion lastRotation)
        {
            Vector3 position = _animator.GetIKPosition(foot);
            Quaternion rotation = _animator.GetIKRotation(foot);

            position = _animator.transform.InverseTransformPoint(position);
            ikPosition = _animator.transform.InverseTransformPoint(ikPosition);
            lastHeight = Mathf.MoveTowards(lastHeight, ikPosition.y, _feetPositionSpeed * Time.deltaTime);
            position.y += lastHeight;

            position = _animator.transform.TransformPoint(position);
            position += normal * _groundOffset;

            Quaternion relative = Quaternion.Inverse(ikRotation * rotation) * rotation;
            lastRotation = Quaternion.RotateTowards(lastRotation, Quaternion.Inverse(relative), _feetRotationSpeed * Time.deltaTime);

            rotation *= lastRotation;

            _animator.SetIKPosition(foot, position);
            _animator.SetIKPositionWeight(foot, _footPositionWeight * _falloffWeight);
            _animator.SetIKRotation(foot, rotation);
            _animator.SetIKRotationWeight(foot, _footRotationWeight * _falloffWeight);
        }

        void GetGrounded()
        {
            _isGrounded = _lGrounded || _rGrounded;
            _falloffWeight = LerpValue(_falloffWeight, _isGrounded ? 1f : 0f, 1f, 10f, Time.fixedDeltaTime) * _weight;
        }
    
        public float LerpValue(float current, float desired, float increaseSpeed, float decreaseSpeed, float deltaTime)
        {
            if (Mathf.Approximately(current, desired))
            {
                return desired;
            }

            return current < desired ? 
                Mathf.MoveTowards(current, desired, (increaseSpeed * _velocity) * deltaTime) : 
                Mathf.MoveTowards(current, desired, (decreaseSpeed * _velocity) * deltaTime);
        }

        private void FeetSolver(HumanBodyBones foot, ref Vector3 ikPosition, ref Vector3 normal, ref Quaternion ikRotation, ref bool grounded)
        {
            Vector3 position = _animator.GetBoneTransform(foot).position;
            position.y = _animator.transform.position.y + _maxStep;

            position -= normal * _groundOffset;
            float feetHeight = _maxStep;

            if (_showDebug)
            {
                Debug.DrawLine(position, position + Vector3.down * (_maxStep * 2), Color.yellow);
            }

            if (Physics.SphereCast(position, _footRadius, Vector3.down, out var hit, _maxStep * 2, _ground))
            {
                feetHeight = _animator.transform.position.y - hit.point.y;
                ikPosition = hit.point;

                normal = hit.normal;
                if (_showDebug)
                {
                    Debug.DrawRay(hit.point, hit.normal, Color.blue);
                }

                Vector3 axis = Vector3.Cross(Vector3.up, hit.normal);
                float angle = Vector3.Angle(Vector3.up, hit.normal);
                ikRotation = Quaternion.AngleAxis(angle, axis);
            }

            grounded = feetHeight < _maxStep;

            if (!grounded)
            {
                ikPosition.y = _animator.transform.position.y - _maxStep;
                ikRotation = Quaternion.identity;
            }
        }
    }
}