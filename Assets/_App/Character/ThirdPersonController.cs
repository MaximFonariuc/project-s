using Cysharp.Threading.Tasks;
using ModularInput;
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
        private PlayerTargetLockSystem _targetLockSystem;
        private Weapon _equippedWeapon;

        private bool _activeCursor;

        [Inject]
        public void Construct(IPlayerInput input)
        {
            _input = input;
        }

        private void SetCallbacks()
        {
            _character.PhysicsVolumeChanged += _animationController.OnPhysicsVolumeChanged;

            _input.AttackRequested += Attack;
            _input.InteractRequested += Interact;
            _input.WalkChanged += Walk;
            _input.JumpChanged += Jump;
            _input.SprintChanged += Sprint;
            _input.PauseRequested += Pause;
            _input.InverseGravityRequested += InverseGravity;
            _input.CursorRequested += CursorChanged;
            _input.SkillRequested += Skill;
            _input.LockOnRequested += LockOn;
            _input.DodgeRequested += Dodge;
            _input.SwitchCombatStateRequested += SwitchCombatState;

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
            _input.WalkChanged -= Walk;
            _input.JumpChanged -= Jump;
            _input.SprintChanged -= Sprint;
            _input.PauseRequested -= Pause;
            _input.InverseGravityRequested -= InverseGravity;
            _input.CursorRequested -= CursorChanged;
            _input.SkillRequested -= Skill;
            _input.LockOnRequested -= LockOn;
            _input.DodgeRequested -= Dodge;
            _input.SwitchCombatStateRequested -= SwitchCombatState;

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
            if (_input == null)
            {
                Debug.LogError($"{nameof(ThirdPersonController)} requires {nameof(IPlayerInput)} from the composition root.", this);
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
            _animationController = new AnimationController(_character, _animator);
            _combatControllerBase = new PlayerCombatController(_characterType, _animationController, _skillsData);
            _abilityController = new AbilityController(_abilitiesData, _character, _animationController);
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
            _input?.Enable();
        }

        private void OnDisable()
        {
            _abilityController?.DisableAllAbilities();
            _input?.Disable();
        }

        protected virtual void Update()
        {
            HandleInput();
            _animationController?.Update();
        }

        protected virtual void LateUpdate()
        {
            UpdateCamera();
            UpdateHeadLook();
        }

        #endregion

        #region Movement&Camera Inputs

        protected virtual void HandleInput()
        {
            if (_character.IsClimbing() && _input.Move.sqrMagnitude > 0.01f)
            {
                var ladder = _abilityController.GetAbility<LadderClimbAbility>();
                
                ladder.OnClimbInput(_input.Move);
            }
            
            var movementDirection = GetMovementDirection();

            var attacking = _combatControllerBase.IsComboAttack || _combatControllerBase.IsSkillAttack;
            _character.SetMovementDirection(attacking ? Vector3.zero : movementDirection);

            if (_character.RotationMode == ERotationMode.OrientRotationToMovement && attacking)
            {
                if (movementDirection.sqrMagnitude > 0.01f)
                {
                    Quaternion targetRotation =
                        Quaternion.LookRotation(movementDirection.normalized, _character.GetUpVector());

                    float currentYaw = _character.transform.eulerAngles.y;
                    float targetYaw = targetRotation.eulerAngles.y;

                    float smoothYaw = Mathf.LerpAngle(
                        currentYaw,
                        targetYaw,
                        Time.deltaTime * 10
                    );

                    _character.SetYaw(smoothYaw);
                }
            }

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


        private Vector3 GetMovementDirection()
        {
            Vector3 movementDirection = Vector3.zero;

            movementDirection += Vector3.right * _input.Move.x;

            if (_character.RotationMode != ERotationMode.None)
            {
                movementDirection += Vector3.forward * _input.Move.y;

                if (_character.CameraTransform)
                {
                    movementDirection =
                        movementDirection.relativeTo(_character.CameraTransform, _character.GetUpVector());
                }
            }

            return movementDirection;
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

        private void Walk(InputSignalPhase phase)
        {
            if (_character.IsCrouched())
            {
                return;
            }

            var walkAbility = _abilityController.GetAbility<WalkAbility>();

            switch (phase)
            {
                case InputSignalPhase.Started:
                    walkAbility?.Walk();
                    _animationController.IsWalking = true;
                    break;

                case InputSignalPhase.Canceled:
                    _animationController.IsWalking = false;
                    walkAbility?.StopWalking();
                    break;
            }
        }

        private void Jump(InputSignalPhase phase)
        {
            if (_combatControllerBase.IsComboAttack || _combatControllerBase.IsSkillAttack || _combatControllerBase.IsSwitching ||
                _character.IsDodging())
            {
                return;
            }

            var glideAbility = _abilityController.GetAbility<GlideAbility>();

            switch (phase)
            {
                case InputSignalPhase.Started:
                    _character?.Jump();
                    break;

                case InputSignalPhase.Performed when !_character.IsSwimming():
                    glideAbility?.Glide();
                    break;

                case InputSignalPhase.Canceled:
                    glideAbility?.StopGliding();
                    _character?.StopJumping();
                    break;
            }
        }

        private void Sprint(InputSignalPhase phase)
        {
            if (_character.IsCrouched())
            {
                return;
            }

            var sprintAbility = _abilityController.GetAbility<SprintAbility>();

            switch (phase)
            {
                case InputSignalPhase.Started:
                    sprintAbility?.Sprint();
                    _animationController.IsSprinting = true;
                    break;

                case InputSignalPhase.Canceled:
                    _animationController.IsSprinting = false;
                    sprintAbility?.StopSprinting();
                    break;
            }
        }

        private void Pause()
        {
            _character.Pause(!_character.IsPaused);
        }

        public void InverseGravity()
        {
            if (!_character.IsFalling())
            {
                return;
            }

            _character.GravityScale *= -1.0f;
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
        }

        private void LockOn()
        {
            _targetLockSystem.AcquireTarget(_character.SetRotationMode);
        }

        private void Dodge()
        {
            if (_character.IsSwimming() || _character.IsFalling() || _combatControllerBase.IsSkillAttack ||
                _combatControllerBase.IsSwitching)
            {
                return;
            }

            var dodgeAbility = _abilityController.GetAbility<DodgeAbility>();
            if (dodgeAbility.IsDodgeAllowed())
            {
                _combatControllerBase.InterruptCombo();

                var movementDirection = GetMovementDirection();

                bool isDodgingBack = _input.Move.y < -0.5f &&
                                     _character.RotationMode == ERotationMode.OrientToViewAndMovement;

                if (movementDirection.sqrMagnitude > 0.01f && !isDodgingBack)
                {
                    Quaternion targetRotation =
                        Quaternion.LookRotation(movementDirection.normalized, _character.GetUpVector());
                    _character.SetYaw(targetRotation.eulerAngles.y);
                }

                dodgeAbility.Dodge(isDodgingBack ? AnimationHashes.RollBack : AnimationHashes.RollFront);
            }
        }

        private void SwitchCombatState()
        {
            if (_character.IsFalling())
            {
                return;
            }

            _combatControllerBase?.ToggleWeaponAsync(EWeaponType.TwoHanded, WeaponHandler).Forget();
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
