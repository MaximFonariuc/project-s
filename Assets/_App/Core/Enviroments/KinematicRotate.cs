using UnityEngine;

namespace _App
{
    
    [RequireComponent(typeof(Rigidbody))]
    public class KinematicRotate : MonoBehaviour
    {

        [SerializeField] private float _rotationSpeed = 30.0f;
        
        [SerializeField] private Vector3 _rotationAxis = Vector3.up;
        
        private Rigidbody _rigidbody;
        private float _angle;
        
        public float RotationSpeed
        {
            get => _rotationSpeed;
            set => _rotationSpeed = value;
        }

        public float Angle
        {
            get => _angle;
            set => _angle = MathLib.ClampAngle(value, 0.0f, 360.0f);
        }



        public void OnValidate()
        {
            RotationSpeed = _rotationSpeed;
            _rotationAxis = _rotationAxis.normalized;
        }

        public void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.isKinematic = true;
        }

        public void FixedUpdate()
        {
            Angle += RotationSpeed * Time.deltaTime;

            Quaternion rotation = Quaternion.AngleAxis(RotationSpeed * Time.deltaTime, _rotationAxis.normalized);
            _rigidbody.MoveRotation(_rigidbody.rotation * rotation);
        }
    }
}
