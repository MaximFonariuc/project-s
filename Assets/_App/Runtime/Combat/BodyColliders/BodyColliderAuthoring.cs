using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project S/Combat/Body Collider Authoring")]
    public sealed class BodyColliderAuthoring : MonoBehaviour
    {
        [SerializeField] private BodyRigType _rigType = BodyRigType.Auto;
        [SerializeField] private Animator _animator;
        [SerializeField] private Health _health;
        [SerializeField] private BodyColliderProfile _profile;
        [SerializeField] private List<SkinnedMeshRenderer> _sourceMeshes = new List<SkinnedMeshRenderer>();
        [SerializeField] private List<BodyColliderBoneDefinition> _genericBones = new List<BodyColliderBoneDefinition>();

        public BodyRigType RigType => _rigType;
        public Animator Animator => _animator;
        public Health Health => _health;
        public BodyColliderProfile Profile => _profile;
        public IReadOnlyList<SkinnedMeshRenderer> SourceMeshes => _sourceMeshes;
        public IReadOnlyList<BodyColliderBoneDefinition> GenericBones => _genericBones;

        public void ResolveMissingReferences()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }
        }

        private void Reset()
        {
            ResolveMissingReferences();
        }

        private void OnValidate()
        {
            ResolveMissingReferences();
        }
    }
}
