using UnityEngine;

namespace ModularCharacterMovement
{
    public sealed class SlopeLimitBehaviour : MonoBehaviour
    {
        [SerializeField] private ESlopeBehaviourType _slopeBehaviour = ESlopeBehaviourType.Default;
        [SerializeField] private float _slopeLimit;

        private float _slopeLimitCos;

        public ESlopeBehaviourType WalkableSlopeBehaviour
        {
            get => _slopeBehaviour;
            set => _slopeBehaviour = value;
        }

        public float SlopeLimit
        {
            get => _slopeLimit;

            set
            {
                _slopeLimit = Mathf.Clamp(value, 0.0f, 89.0f);
                
                _slopeLimitCos = Mathf.Cos(_slopeLimit * Mathf.Deg2Rad);
            }
        }

        public float SlopeLimitCos
        {
            get => _slopeLimitCos;

            set
            {
                _slopeLimitCos = Mathf.Clamp01(value);

                _slopeLimit = Mathf.Clamp(Mathf.Acos(_slopeLimitCos) * Mathf.Rad2Deg, 0.0f, 89.0f);
            }
        }
        
        private void OnValidate()
        {
            SlopeLimit = _slopeLimit;
        }
    }
}
