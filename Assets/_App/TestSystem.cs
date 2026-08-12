using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _App
{
    public class TestSystem : MonoSingleton<TestSystem>
    {
        [Header("CombatController")] 
        [SerializeField] private EWeaponType _weaponType;
        
        private readonly List<ITestable> _testables = new List<ITestable>();
        
        public void SetTestableItem(ITestable testables)
        {
            _testables.Add(testables);
        }
        
        public void SetTestableItems(List<ITestable> testables)
        {
            _testables.AddRange(testables);
        }

        private T Get<T>() where T : ITestable
        {
            if (_testables.IsNullOrEmpty())
            {
                return default;
            }
            foreach (var uiView in _testables)
            {
                if (uiView is T view)
                {
                    return view;
                }
            }

            return default;
        }
        
        private void Update()
        {
#if UNITY_EDITOR
            if (Input.GetKey(KeyCode.Q) && Input.GetKeyUp(KeyCode.Alpha1))
            {
                Get<CombatControllerBase>().SwitchAnimations(ECharacterType.Player, _weaponType).Forget();
            }
            if (Input.GetKey(KeyCode.Q) && Input.GetKeyUp(KeyCode.Alpha2))
            {
                var player = Get<ThirdPersonController>().gameObject;
                var ragdollBodies = player.GetComponentsInChildren<Rigidbody>();

                SetRagdollState(true, ragdollBodies);
            }
#endif
        }
        
        public void SetRagdollState(bool enabled, Rigidbody[] ragdollBodies)
        {
            foreach (var rb in ragdollBodies)
            {
                if (rb == null) continue;
                rb.isKinematic = !enabled;

                Collider col = rb.GetComponent<Collider>();
                if (col != null)
                    col.enabled = enabled;
            }

            var animatorController = Get<AnimationController>();
            if (animatorController != null)
                animatorController.Animator.enabled = !enabled;
        }
    }
}