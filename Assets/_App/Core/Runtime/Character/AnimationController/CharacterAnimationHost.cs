using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Project S/Character/Character Animation Host")]
    public sealed class CharacterAnimationHost : MonoBehaviour
    {
        private static readonly CharacterLookFrame NeutralLookFrame =
            new CharacterLookFrame(0.0f, 0.0f);

        [SerializeField] private Animator _animator;

        private XControllerCharacterAnimationDriver _driver;
        private PlayerAnimationCoordinator _animation;
        private bool _initializationFailed;

        public Animator Animator => _animator;

        private void Awake()
        {
            TryGetAnimation(out _);
        }

        private void Update()
        {
            if (_driver != null)
            {
                _driver.ApplyLook(in NeutralLookFrame, Time.deltaTime);
            }
        }

        private void OnDisable()
        {
            _animation?.Reset();
        }

        private void OnDestroy()
        {
            _driver?.Dispose();
            _animation = null;
            _driver = null;
        }

        private void Reset()
        {
            ResolveAnimator();
        }

        private void OnValidate()
        {
            ResolveAnimator();
        }

        public void Configure(Animator animator)
        {
            _animator = animator;
        }

        internal bool TryGetAnimation(out PlayerAnimationCoordinator animation)
        {
            if (_animation != null)
            {
                animation = _animation;
                return true;
            }

            if (_initializationFailed)
            {
                animation = null;
                return false;
            }

            ResolveAnimator();
            if (_animator == null)
            {
                Debug.LogError(
                    $"{nameof(CharacterAnimationHost)} requires an Animator.",
                    this);
                _initializationFailed = true;
                enabled = false;
                animation = null;
                return false;
            }

            try
            {
                _driver = new XControllerCharacterAnimationDriver(_animator);
                _animation = new PlayerAnimationCoordinator(_driver);
                animation = _animation;
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                _driver?.Dispose();
                _driver = null;
                _animation = null;
                _initializationFailed = true;
                enabled = false;
                animation = null;
                return false;
            }
        }

        private void ResolveAnimator()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>(true);
            }
        }
    }
}
