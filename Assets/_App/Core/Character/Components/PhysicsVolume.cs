using UnityEngine;

namespace _App
{
    [RequireComponent(typeof(BoxCollider))]
    public class PhysicsVolume : MonoBehaviour
    {
        [SerializeField] private int _priority;
        [SerializeField] private float _friction;
        [SerializeField] private float _maxFallSpeed;
        [SerializeField] private bool _waterVolume;
        
        private BoxCollider _collider;

        public BoxCollider BoxCollider
        {
            get
            {
                if (_collider == null)
                    _collider = GetComponent<BoxCollider>();

                return _collider;
            }
        }

        public int Priority
        {
            get => _priority;
            private set => _priority = value;
        }

        public float Friction
        {
            get => _friction;
            private set => _friction = Mathf.Max(0.0f, value);
        }

        public float MaxFallSpeed
        {
            get => _maxFallSpeed;
            private set => _maxFallSpeed = Mathf.Max(0.0f, value);
        }

        public bool WaterVolume
        {
            get => _waterVolume;
            private set => _waterVolume = value;
        }
        
        protected virtual void OnReset()
        {
            Priority = 0;
            Friction = 0.5f;
            MaxFallSpeed = 40.0f;
            WaterVolume = true;
        }

        protected virtual void OnOnValidate()
        {
            Friction = _friction;
            MaxFallSpeed = _maxFallSpeed;
        }

        protected virtual void OnAwake()
        {
            BoxCollider.isTrigger = true;
        }

        private void Reset()
        {
            OnReset();
        }

        private void OnValidate()
        {
            OnOnValidate();
        }

        private void Awake()
        {
            OnAwake();
        }
    }
}
