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

        private float _headLookX;
        private float _headLookY;

        private float _cameraTargetYaw;
        private float _cameraTargetPitch;
        private float _cameraRotationOnGravityFlip;

        private float _followDistanceSmoothVelocity;

        private Character _character;
        private IPlayerInput _input;
        private AbilityController _abilityController;
        private CombatControllerBase _combatControllerBase;
        private AnimationController _animationController;
        private PlayerLocomotionController _locomotionController;
        private PlayerTargetLockSystem _targetLockSystem;
        private Weapon _equippedWeapon;

        private bool _activeCursor;

        internal AbilitiesData AbilitiesData => _abilitiesData;
        internal Animator PlayerAnimator => _animator;

        [Inject]
        public void Construct(
            IPlayerInput input,
            AbilityController abilityController,
            AnimationController animationController,
            PlayerLocomotionController locomotionController)
        {
            _input = input;
            _abilityController = abilityController;
            _animationController = animationController;
            _locomotionController = locomotionController;
        }

        private void SetCallbacks()
        {
            _character.PhysicsVolumeChanged += _animationController.OnPhysicsVolumeChanged;

            _input.AttackRequested += Attack;
            _input.InteractRequested += Interact;
            _input.PauseRequested += Pause;
            _input.InverseGravityRequested += InverseGravity;
            _input.CursorRequested += CursorChanged;
            _input.SkillRequested += Skill;
            _input.LockOnRequested += LockOn;
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
            _input.CursorRequested -= CursorChanged;
            _input.SkillRequested -= Skill;
            _input.LockOnRequested -= LockOn;
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
                _locomotionController == null)
            {
                Debug.LogError($"{nameof(ThirdPersonController)} requires its gameplay dependencies from the composition root.", this);
                enabled = false;
                return;
            }

            _character = GetComponent<Character>();
            _followCamera = Instantiate(_followCamera);

            InitControllers();

            if (_followCamera)
            {
                _followCamera.GetComponent<CinemachineCamera>().Follow = _followTarget;
                _followCamera.CameraDistance = _followDistance;
            }

            SetCallbacks();
        }

        protected virtual void OnDestroy()
        {
            RemoveCallbacks();
        }

        private void InitControllers()
        {
            _combatControllerBase = new PlayerCombatController(_characterType, _animationController, _skillsData);
            _targetLockSystem = new PlayerTargetLockSystem(_character, _followCamera.GetComponent<CinemachineCamera>());
            _equippedWeapon = _weapon != null ? _weapon.GetComponent<Weapon>() : null;
        }

        protected virtual void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnEnable()
        {
            _abilityController?.EnableAllAbilities();
            _locomotionController?.Enable();
            _input?.Enable();
        }

        private void OnDisable()
        {
            _input?.Disable();
            _locomotionController?.Disable();
            _abilityController?.DisableAllAbilities();
        }

        protected virtual void Update()
        {
            HandleTraversalInput();
            UpdateMovementIntent();
            _locomotionController?.Tick(Time.deltaTime);
            HandleCameraInput();
        }

        protected virtual void LateUpdate()
        {
            UpdateCamera();
            UpdateHeadLook();
        }

        #endregion

        #region Movement&Camera Inputs

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

        private void HandleCameraInput()
        {
            if (_character.RotationMode != ERotationMode.None)
            {
                Vector2 lookInput = _input.Look * _mouseSensitivity;
                AddControlYawInput(_invertLook ? lookInput.x : -lookInput.x);
                AddControlPitchInput(_invertLook ? -lookInput.y : lookInput.y, _minPitch, _maxPitch);

                Vector2 zoomInput = _input.Zoom * _mouseSensitivity;
                AddControlZoomInput(zoomInput.y);
            }
            else
            {
                if (_input.Move.x != 0)
                {
                    _character.SetYaw(_input.Move.x > 0 ? 90.0f : -90.0f);
                }

                if (_character.CameraTransform)
                {
                    _character.CameraTransform.position = _character.transform.position + new Vector3(0, 2, -5);
                    _character.CameraTransform.rotation = Quaternion.Euler(10, 90, 0);
                }
            }
        }

        private void AddControlYawInput(float value, float minValue = -180.0f, float maxValue = 180.0f)
        {
            if (value == 0.0f)
            {
                return;
            }

            _cameraTargetYaw = MathLib.ClampAngle(_cameraTargetYaw + value, minValue, maxValue);
        }

        private void AddControlPitchInput(float value, float minValue = -80.0f, float maxValue = 80.0f)
        {
            if (value == 0.0f)
            {
                return;
            }

            _cameraTargetPitch = MathLib.ClampAngle(_cameraTargetPitch + value, minValue, maxValue);
        }

        private void AddControlZoomInput(float value)
        {
            if (value == 0.0f)
            {
                return;
            }

            _followDistance = Mathf.Clamp(_followDistance - value, _followMinDistance, _followMaxDistance);
        }

        private void UpdateCamera()
        {
            if (_activeCursor)
            {
                return;
            }

            if (_targetLockSystem.IsTargetLocked && _targetLockSystem.CurrentTarget != null)
            {
                float distanceToTarget =
                    Vector3.Distance(_followTarget.position, _targetLockSystem.CurrentTarget.position);

                if (distanceToTarget > TargetLockSystem.MAX_LOCK_DISTANCE)
                {
                    LockOn();
                }
                else
                {
                    Vector3 directionToTarget = _targetLockSystem.CurrentTarget.position - _followTarget.position;

                    Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);
                    Vector3 lookEuler = lookRotation.eulerAngles;

                    float targetYaw = lookEuler.y;
                    float targetPitch = lookEuler.x;

                    _cameraTargetYaw = targetYaw;
                    _cameraTargetPitch = targetPitch;
                }
            }

            _followTarget.rotation = Quaternion.Euler(_cameraTargetPitch, _cameraTargetYaw,
                _cameraRotationOnGravityFlip != 0 ? _cameraRotationOnGravityFlip : 0);

            _followCamera.CameraDistance =
                Mathf.SmoothDamp(_followCamera.CameraDistance, _followDistance, ref _followDistanceSmoothVelocity,
                    0.1f);
        }

        private void UpdateHeadLook()
        {
            const float interpolationSpeed = 5f;

            const float clampMin = -0.5f;
            const float clampMax = 0.5f;

            float targetX = 0f;
            float targetY = 0f;

            if (_targetLockSystem.IsTargetLocked || _targetLockSystem.CurrentTarget != null)
            {
                var head = _animator.GetBoneTransform(HumanBodyBones.Head);
                Vector3 eyePos = head != null ? head.position : transform.position;
                Vector3 directionToTarget = _targetLockSystem.CurrentTarget.position - eyePos;
                Vector3 localDirection = transform.InverseTransformDirection(directionToTarget.normalized);

                targetX = Mathf.Clamp(localDirection.x, clampMin, clampMax);
                targetY = Mathf.Clamp(localDirection.y, clampMin, clampMax);
            }

            _headLookX = Mathf.Lerp(_headLookX, targetX, Time.deltaTime * interpolationSpeed);
            _headLookY = Mathf.Lerp(_headLookY, targetY, Time.deltaTime * interpolationSpeed);

            _animationController.SetFloat(AnimationHashes.HeadLookX, _headLookX);
            _animationController.SetFloat(AnimationHashes.HeadLookY, _headLookY);
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

            _cameraRotationOnGravityFlip = _cameraRotationOnGravityFlip >= 0 ? -180 : 0;
            _invertLook = _cameraRotationOnGravityFlip >= 0;
        }

        private void CursorChanged()
        {
            _activeCursor = !_activeCursor;
            Cursor.lockState = _activeCursor ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = _activeCursor;
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

        private void LockOn()
        {
            _targetLockSystem.AcquireTarget(_character.SetRotationMode);
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
