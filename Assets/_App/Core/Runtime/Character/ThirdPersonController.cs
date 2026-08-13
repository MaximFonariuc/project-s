using Cysharp.Threading.Tasks;
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

        private Character _character;
        private IPlayerInput _input;
        private AbilityController _abilityController;
        private AnimationController _animationController;
        private PlayerLocomotionController _locomotionController;
        private PlayerTargetingController _targetingController;
        private PlayerCameraController _cameraController;
        private PlayerCombatController _combatController;

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
            AbilityController abilityController,
            AnimationController animationController,
            PlayerLocomotionController locomotionController,
            PlayerTargetingController targetingController,
            PlayerCameraController cameraController,
            PlayerCombatController combatController)
        {
            _input = input;
            _abilityController = abilityController;
            _animationController = animationController;
            _locomotionController = locomotionController;
            _targetingController = targetingController;
            _cameraController = cameraController;
            _combatController = combatController;
        }

        private void SetCallbacks()
        {
            _character.PhysicsVolumeChanged += _animationController.OnPhysicsVolumeChanged;

            _input.InteractRequested += Interact;
            _input.PauseRequested += Pause;
            _input.InverseGravityRequested += InverseGravity;
        }

        private void RemoveCallbacks()
        {
            if (_input == null || _character == null || _animationController == null)
            {
                return;
            }

            _character.PhysicsVolumeChanged -= _animationController.OnPhysicsVolumeChanged;

            _input.InteractRequested -= Interact;
            _input.PauseRequested -= Pause;
            _input.InverseGravityRequested -= InverseGravity;
        }

        #region Monobehavior

        protected virtual void Awake()
        {
            if (_input == null || _abilityController == null || _animationController == null ||
                _locomotionController == null || _targetingController == null || _cameraController == null ||
                _combatController == null)
            {
                Debug.LogError($"{nameof(ThirdPersonController)} requires its gameplay dependencies from the composition root.", this);
                enabled = false;
                return;
            }

            _character = GetComponent<Character>();
            SetCallbacks();
        }

        protected virtual void OnDestroy()
        {
            RemoveCallbacks();
        }

        private void OnEnable()
        {
            _abilityController?.EnableAllAbilities();
            _locomotionController?.Enable();
            _targetingController?.Enable();
            _cameraController?.Enable();
            _combatController?.Enable();
            _input?.Enable();
        }

        private void OnDisable()
        {
            _input?.Disable();
            _combatController?.Disable();
            _cameraController?.Disable();
            _targetingController?.Disable();
            _locomotionController?.Disable();
            _abilityController?.DisableAllAbilities();
        }

        protected virtual void Update()
        {
            HandleTraversalInput();
            UpdateMovementIntent();
            _locomotionController?.Tick(Time.deltaTime);
            _targetingController?.Tick();
        }

        protected virtual void LateUpdate()
        {
            _cameraController?.LateTick(Time.unscaledDeltaTime, Time.deltaTime);
        }

        #endregion

        #region Movement Inputs

        private void HandleTraversalInput()
        {
            if (_character.IsClimbing() && _input.Move.sqrMagnitude > 0.01f)
            {
                var ladder = _abilityController.GetAbility<LadderClimbAbility>();

                ladder?.OnClimbInput(_input.Move);
            }
        }

        private void UpdateMovementIntent()
        {
            _locomotionController.SetMovementIntent(_combatController.MovementIntent);
        }

        #endregion

        #region Actions

        private void Interact()
        {
            if (!CheckInteract())
            {
                return;
            }

            if (_combatController.CurrentWeapon != EWeaponType.Unarmed)
            {
                SheatheAndClimbAsync().Forget();
            }
            else
            {
                var ladderClimbAbility = _abilityController.GetAbility<LadderClimbAbility>();
                ladderClimbAbility?.Climb().Forget();
            }

            bool CheckInteract()
            {
                var overlappedColliders = _character.CharacterMovement.OverlapTest(1, QueryTriggerInteraction.Collide, out int overlapCount);
                return overlapCount != 0 && overlappedColliders[0].TryGetComponent(out IInteractable _);
            }
        }

        private void Pause()
        {
            _character.Pause(!_character.IsPaused);
        }

        public void InverseGravity()
        {
            if (!_locomotionController.TryInvertGravity())
            {
                return;
            }

            _cameraController.ToggleGravityOrientation();
        }

        private async UniTaskVoid SheatheAndClimbAsync()
        {
            _character.Pause(true);
            try
            {
                if (!await _combatController.ToggleWeaponAsync(EWeaponType.Unarmed))
                {
                    return;
                }

                LadderClimbAbility ladder = _abilityController.GetAbility<LadderClimbAbility>();
                if (ladder != null)
                {
                    ladder.Climb().Forget();
                }
            }
            finally
            {
                if (_character != null)
                {
                    _character.Pause(false);
                }
            }
        }

        #endregion
    }
}
