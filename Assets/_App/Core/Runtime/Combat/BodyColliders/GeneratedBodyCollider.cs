using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class GeneratedBodyCollider : MonoBehaviour
    {
        [SerializeField, HideInInspector] private BodyColliderRig _rig;
        [SerializeField, HideInInspector] private string _generationKey;
        [SerializeField, Tooltip("Keep the current transform and collider dimensions when regenerating this rig.")]
        private bool _lockGeometry;

        public BodyColliderRig Rig => _rig;
        public string GenerationKey => _generationKey;
        public bool LockGeometry => _lockGeometry;

        public void Configure(BodyColliderRig rig, string generationKey)
        {
            _rig = rig;
            _generationKey = generationKey;
        }
    }
}
