using Unity.Cinemachine;
using UnityEngine;
using VContainer;

namespace _App
{
    public class ThirdPersonController : MonoBehaviour, ITestable
    {
        [SerializeField] private ECharacterType _characterType;

        [Header("Cinemachine")] 
        [SerializeField] private CinemachineThirdPersonFollow _followCamera;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private float _followDistance = 5.0f;

        [Space(15.0f)] 
        [SerializeField] private float _followMinDistance = 2.0f;
        [SerializeField] private float _followMaxDistance = 10.0f;

        [Space(15.0f)] 
        [SerializeField] private bool _invertLook = true;
        [SerializeField] private Vector2 _mouseSensitivity = new Vector2(1.5f, 1.25f);

        [Space(15.0f)] 
        [SerializeField] private float _minPitch = -80.0f;
        [SerializeField] private float _maxPitch = 80.0f;

        [Header("Abilities")] 
        [SerializeField] private AbilitiesData _abilitiesData;

        [Header("CombatController")] 
        [SerializeField] private AnimationsData _skillsData;

        [Header("AnimationController")] 
        [SerializeField] private Animator _animator;

        [Header("WeaponHolder")] 
        [SerializeField] private Transform _weaponHolder;
        [SerializeField] private Transform _weaponHand;
        [SerializeField] private Transform _weapon;

        private IPlayerInput _input;
        private ICharacterAbilityLifecycle _abilityLifecycle;
        private PlayerPhysicsVolumeAnimationAdapter _physicsVolumeAnimation;
        private PlayerLocomotionController _locomotionController;
        private PlayerTargetingController _targetingController;
        private PlayerCameraController _cameraController;
        private PlayerCombatController _combatController;
        private PlayerTraversalController _traversalController;
        private PlayerSimulationPauseController _simulationPauseController;

        internal AbilitiesData AbilitiesData => _abilitiesData;
        internal Animator PlayerAnimator => _animator;
        internal ECharacterType CharacterType => _characterType;
        internal AnimationsData CombatAnimations => _skillsData;
        internal Transform WeaponHolder => _weaponHolder;
        internal Transform WeaponHand => _weaponHand;
        internal Transform WeaponTransform => _weapon;
        internal CinemachineThirdPersonFollow FollowCameraPrefab => _followCamera;
        internal Transform FollowTarget => _followTarget;
        internal PlayerCameraSettings CameraSettings => new PlayerCameraSettings(
            _followDistance,
            _followMinDistance,
            _followMaxDistance,
            _invertLook,
            _mouseSensitivity,
            _minPitch,
            _maxPitch);

        [Inject]
        public void Construct(
            IPlayerInput input,
            ICharacterAbilityLifecycle abilityLifecycle,
            PlayerPhysicsVolumeAnimationAdapter physicsVolumeAnimation,
            PlayerLocomotionController locomotionController,
            PlayerTargetingController targetingController,
            PlayerCameraController cameraController,
            PlayerCombatController combatController,
            PlayerTraversalController traversalController,
            PlayerSimulationPauseController simulationPauseController)
        {
            _input = input;
            _abilityLifecycle = abilityLifecycle;
            _physicsVolumeAnimation = physicsVolumeAnimation;
            _locomotionController = locomotionController;
            _targetingController = targetingController;
            _cameraController = cameraController;
            _combatController = combatController;
            _traversalController = traversalController;
            _simulationPauseController = simulationPauseController;
        }

        #region Monobehavior

        protected virtual void Awake()
        {
            if (_input == null || _abilityLifecycle == null || _physicsVolumeAnimation == null ||
                _locomotionController == null || _targetingController == null || _cameraController == null ||
                _combatController == null || _traversalController == null ||
                _simulationPauseController == null)
            {
                Debug.LogError($"{nameof(ThirdPersonController)} requires its gameplay dependencies from the composition root.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _physicsVolumeAnimation?.Enable();
            _abilityLifecycle?.Enable();
            _locomotionController?.Enable();
            _targetingController?.Enable();
            _cameraController?.Enable();
            _combatController?.Enable();
            _traversalController?.Enable();
            _simulationPauseController?.Enable();
            _input?.Enable();
        }

        private void OnDisable()
        {
            _input?.Disable();
            _simulationPauseController?.Disable();
            _traversalController?.Disable();
            _combatController?.Disable();
            _cameraController?.Disable();
            _targetingController?.Disable();
            _locomotionController?.Disable();
            _abilityLifecycle?.Disable();
            _physicsVolumeAnimation?.Disable();
        }

        protected virtual void Update()
        {
            _traversalController?.Tick();
            _locomotionController?.SetMovementIntent(_combatController.MovementIntent);
            _locomotionController?.Tick(Time.deltaTime);
            _targetingController?.Tick();
        }

        protected virtual void LateUpdate()
        {
            _cameraController?.LateTick(Time.unscaledDeltaTime, Time.deltaTime);
        }

        #endregion

        public void InverseGravity()
        {
            _traversalController?.TryInvertGravity();
        }
    }
}
