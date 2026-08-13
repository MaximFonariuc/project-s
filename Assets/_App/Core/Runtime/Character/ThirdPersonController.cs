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
        private CombatControllerBase _combatControllerBase;
        private AnimationController _animationController;
        private PlayerLocomotionController _locomotionController;
        private PlayerTargetingController _targetingController;
        private PlayerCameraController _cameraController;
        private Weapon _equippedWeapon;

        internal AbilitiesData AbilitiesData => _abilitiesData;
        internal Animator PlayerAnimator => _animator;
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
            PlayerCameraController cameraController)
        {
            _input = input;
            _abilityController = abilityController;
            _animationController = animationController;
            _locomotionController = locomotionController;
            _targetingController = targetingController;
            _cameraController = cameraController;
        }

        private void SetCallbacks()
        {
            _character.PhysicsVolumeChanged += _animationController.OnPhysicsVolumeChanged;

            _input.AttackRequested += Attack;
            _input.InteractRequested += Interact;
            _input.PauseRequested += Pause;
            _input.InverseGravityRequested += InverseGravity;
            _input.SkillRequested += Skill;
            _input.SwitchCombatStateRequested += SwitchCombatState;
            _locomotionController.DodgeAccepted += OnDodgeAccepted;

            if (_equippedWeapon != null)
            {
                _combatControllerBase.HitWindowOpened += OpenWeaponHitbox;
                _combatControllerBase.HitWindowClosed += CloseWeaponHitbox;
            }
        }

        private void RemoveCallbacks()
        {
            if (_input == null || _character == null || _animationController == null)
            {
                return;
            }

            _character.PhysicsVolumeChanged -= _animationController.OnPhysicsVolumeChanged;

            _input.AttackRequested -= Attack;
            _input.InteractRequested -= Interact;
            _input.PauseRequested -= Pause;
            _input.InverseGravityRequested -= InverseGravity;
            _input.SkillRequested -= Skill;
            _input.SwitchCombatStateRequested -= SwitchCombatState;
            _locomotionController.DodgeAccepted -= OnDodgeAccepted;

            if (_equippedWeapon != null)
            {
                _combatControllerBase.HitWindowOpened -= OpenWeaponHitbox;
                _combatControllerBase.HitWindowClosed -= CloseWeaponHitbox;
                _equippedWeapon.DisableHitbox();
            }
        }

        #region Monobehavior

        protected virtual void Awake()
        {
            if (_input == null || _abilityController == null || _animationController == null ||
                _locomotionController == null || _targetingController == null || _cameraController == null)
            {
                Debug.LogError($"{nameof(ThirdPersonController)} requires its gameplay dependencies from the composition root.", this);
                enabled = false;
                return;
            }

            _character = GetComponent<Character>();
            InitControllers();
            SetCallbacks();
        }

        protected virtual void OnDestroy()
        {
            RemoveCallbacks();
        }

        private void InitControllers()
        {
            _combatControllerBase = new PlayerCombatController(_characterType, _animationController, _skillsData);
            _equippedWeapon = _weapon != null ? _weapon.GetComponent<Weapon>() : null;
        }

        private void OnEnable()
        {
            _abilityController?.EnableAllAbilities();
            _locomotionController?.Enable();
            _targetingController?.Enable();
            _cameraController?.Enable();
            _input?.Enable();
        }

        private void OnDisable()
        {
            _input?.Disable();
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
            bool attacking = _combatControllerBase.IsComboAttack || _combatControllerBase.IsSkillAttack;
            bool switching = _combatControllerBase.IsSwitching;
            bool faceMoveDirection = attacking &&
                                     _character.RotationMode == ERotationMode.OrientRotationToMovement;

            _locomotionController.SetMovementIntent(new PlayerMovementIntent(
                attacking ? 0.0f : 1.0f,
                !attacking && !switching,
                !_combatControllerBase.IsSkillAttack && !switching,
                faceMoveDirection));
        }

        #endregion

        #region Actions

        private void Attack()
        {
            if (!_character.CanAttack() || _combatControllerBase.IsSwitching)
            {
                return;
            }

            _combatControllerBase.TryAttack();
            UpdateMovementIntent();
        }

        private void OpenWeaponHitbox()
        {
            if (_combatControllerBase.CurrentWeapon is EWeaponType.None or EWeaponType.Unarmed)
            {
                return;
            }

            _equippedWeapon.EnableHitbox();
        }

        private void CloseWeaponHitbox()
        {
            _equippedWeapon.DisableHitbox();
        }

        private void Interact()
        {
            if (!CheckInteract())
            {
                return;
            }

            if (_combatControllerBase.CurrentWeapon != EWeaponType.Unarmed)
            {
                _character.Pause(true);
                _combatControllerBase?.ToggleWeaponAsync(EWeaponType.TwoHanded, () =>
                {
                    WeaponHandler();
                    _character.Pause(false);
                    var ladderClimbAbility = _abilityController.GetAbility<LadderClimbAbility>();
                    ladderClimbAbility?.Climb().Forget();
                });
                UpdateMovementIntent();
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

        private void Skill(ESkillType skillNumber)
        {
            if (!_character.CanAttack() || _combatControllerBase.IsSwitching)
            {
                return;
            }

            _combatControllerBase.TryAttack(skillNumber);
            UpdateMovementIntent();
        }

        private void OnDodgeAccepted()
        {
            _combatControllerBase.InterruptCombo();
            UpdateMovementIntent();
        }

        private void SwitchCombatState()
        {
            if (_character.IsFalling())
            {
                return;
            }

            _combatControllerBase?.ToggleWeaponAsync(EWeaponType.TwoHanded, WeaponHandler).Forget();
            UpdateMovementIntent();
        }
        
        private void WeaponHandler()
        {
            _weapon.SetParent(_combatControllerBase.CurrentWeapon != EWeaponType.Unarmed ? _weaponHand : _weaponHolder);
            _weapon.localPosition = Vector3.zero;
            _weapon.localRotation = Quaternion.identity;
        }

        #endregion
    }
}
