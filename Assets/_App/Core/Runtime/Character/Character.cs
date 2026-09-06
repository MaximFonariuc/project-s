using UnityEngine;
using ModularCharacterMovement;

namespace _App
{
    [RequireComponent(typeof(CharacterMovement))]
    public partial class Character : MonoBehaviour
    {
        [Space(15f)]
        [SerializeField] private ERotationMode _rotationMode;
        [SerializeField] private float _rotationRate;

        [Space(15f)]
        [SerializeField] private EMovementMode _startingMovementMode;

        [Space(15f)]
        [SerializeField] private float _maxWalkSpeed;
        [SerializeField] private float _minAnalogWalkSpeed;
        [SerializeField] private float _maxAcceleration;
        [SerializeField] private float _brakingDecelerationWalking;
        [SerializeField] private float _groundFriction;

        [Space(15.0f)]
        [SerializeField] private bool _canEverCrouch;
        [SerializeField] private float _crouchedHeight;
        [SerializeField] private float _unCrouchedHeight;
        [SerializeField] private float _maxWalkSpeedCrouched;

        [Space(15f)]
        [SerializeField] private float _maxFallSpeed;
        [SerializeField] private float _brakingDecelerationFalling;
        [SerializeField] private float _fallingLateralFriction;
        [SerializeField, Range(0.0f, 1.0f)] private float _airControl;

        [Space(15.0f)]
        [SerializeField] private bool _canEverJump;
        [SerializeField] private bool _canJumpWhileCrouching;
        [SerializeField] private int _jumpMaxCount;
        [SerializeField] private float _jumpImpulse;
        [SerializeField] private float _jumpMaxHoldTime;
        [SerializeField] private float _jumpMaxPreGroundedTime;
        [SerializeField] private float _jumpMaxPostGroundedTime;

        [Space(15f)]
        [SerializeField] private float _maxFlySpeed;
        [SerializeField] private float _brakingDecelerationFlying;
        [SerializeField] private float _flyingFriction;

        [Space(15f)]
        [SerializeField] private float _maxSwimSpeed;
        [SerializeField] private float _brakingDecelerationSwimming;
        [SerializeField] private float _swimmingFriction;
        [SerializeField] private float _buoyancy;

        [Space(15f)]
        [SerializeField] private Vector3 _gravity;
        [SerializeField] private float _gravityScale;

        [Space(15f)]
        [SerializeField] private bool _applyStandingDownwardForce;

        [Space(15.0f)]
        [SerializeField] private float _mass;
        [SerializeField] private float _pushForceScale;
        [SerializeField] private float _standingDownwardForceScale;

        [Header("FootIK")]
        [SerializeField] private FootIK _footIK;

        private readonly CharacterPhysicsVolumeRegistry _physicsVolumes = new CharacterPhysicsVolumeRegistry();
        private readonly CharacterJumpState _jumpState = new CharacterJumpState();

        private CharacterSimulationLoop _simulationLoop;
        private int _enableGeneration;

        private Camera _camera;
        private Transform _transform;
        private CharacterMovement _characterMovement;
        private Transform _cameraTransform;

        private EMovementMode _movementMode = EMovementMode.None;

        private int _customMovementMode;

        private bool _useSeparateBrakingFriction;
        private float _brakingFriction;

        private bool _useSeparateBrakingDeceleration;
        private float _brakingDeceleration;

        private Vector3 _movementDirection = Vector3.zero;
        private Vector3 _rotationInput = Vector3.zero;
        private Vector3 _desiredVelocity = Vector3.zero;

        private bool _isCrouched;

        private float _fallingTime;
        private PhysicsVolume _physicsVolume;

        private bool CanEverCrouch => _canEverCrouch;

        private bool UseSeparateBrakingFriction => _useSeparateBrakingFriction;

        private float BrakingFriction => _brakingFriction;

        private bool UseSeparateBrakingDeceleration  => _useSeparateBrakingDeceleration;

        private float BrakingDeceleration => _brakingDeceleration;

        private Vector3 Gravity => _gravity * _gravityScale;
        private Quaternion Rotation => CharacterMovement.Rotation;
        private Transform Transform => _transform;

        public Camera Camera => _camera;
        public float FallingTime => _fallingTime;
        public CharacterMovement CharacterMovement => _characterMovement;
        public Vector3 Position => CharacterMovement.Position;
        public float Speed => CharacterMovement.Velocity.magnitude;
        public float Radius => CharacterMovement.Radius;
        public float Height => CharacterMovement.Height;
        public EMovementMode MovementMode => _movementMode;
        public int CustomMovement => _customMovementMode;
        public bool JumpApexNotify
        {
            get => _jumpState.ApexNotificationPending;
            set => _jumpState.ApexNotificationPending = value;
        }

        public bool JumpInputPressed
        {
            get => _jumpState.InputPressed;
            protected set => _jumpState.InputPressed = value;
        }
        public bool IsPaused { get; private set; }

        protected bool ApplyStandingDownwardForce => _applyStandingDownwardForce;
        protected Vector3 Velocity => CharacterMovement.Velocity;

        public Transform CameraTransform
        {
            get
            {
                if (_camera != null)
                    _cameraTransform = _camera.transform;

                return _cameraTransform;
            }
        }

        public float RotationRate
        {
            get => _rotationRate;
            set => _rotationRate = value;
        }

        public ERotationMode RotationMode => _rotationMode;

        public float MaxWalkSpeed
        {
            get => _maxWalkSpeed;
            set => _maxWalkSpeed = Mathf.Max(0.0f, value);
        }

        public float MinAnalogWalkSpeed
        {
            get => _minAnalogWalkSpeed;
            set => _minAnalogWalkSpeed = Mathf.Max(0.0f, value);
        }

        public float MaxAcceleration
        {
            get => _maxAcceleration;
            set => _maxAcceleration = Mathf.Max(0.0f, value);
        }

        public float BrakingDecelerationWalking
        {
            get => _brakingDecelerationWalking;
            set => _brakingDecelerationWalking = Mathf.Max(0.0f, value);
        }

        public float GroundFriction
        {
            get => _groundFriction;
            set => _groundFriction = Mathf.Max(0.0f, value);
        }


        public float CrouchedHeight
        {
            get => _crouchedHeight;
            set => _crouchedHeight = Mathf.Max(0.0f, value);
        }

        public float UnCrouchedHeight
        {
            get => _unCrouchedHeight;
            set => _unCrouchedHeight = Mathf.Max(0.0f, value);
        }

        public float MaxWalkSpeedCrouched
        {
            get => _maxWalkSpeedCrouched;
            protected set => _maxWalkSpeedCrouched = Mathf.Max(0.0f, value);
        }

        public bool CrouchInputPressed { get; protected set; }

        public float MaxFallSpeed
        {
            get => _maxFallSpeed;
            set => _maxFallSpeed = Mathf.Max(0.0f, value);
        }

        public float BrakingDecelerationFalling
        {
            get => _brakingDecelerationFalling;
            set => _brakingDecelerationFalling = Mathf.Max(0.0f, value);
        }

        public float FallingLateralFriction
        {
            get => _fallingLateralFriction;
            set => _fallingLateralFriction = Mathf.Max(0.0f, value);
        }


        public float AirControl
        {
            get => _airControl;
            set => _airControl = Mathf.Clamp01(value);
        }

        public bool CanEverJump
        {
            get => _canEverJump;
            set => _canEverJump = value;
        }

        public bool CanJumpWhileCrouching => _canJumpWhileCrouching;

        public int JumpMaxCount
        {
            get => _jumpMaxCount;
            set => _jumpMaxCount = Mathf.Max(1, value);
        }

        public float JumpImpulse
        {
            get => _jumpImpulse;
            set => _jumpImpulse = Mathf.Max(0.0f, value);
        }

        public float JumpMaxHoldTime
        {
            get => _jumpMaxHoldTime;
            set => _jumpMaxHoldTime = Mathf.Max(0.0f, value);
        }

        public float JumpMaxPreGroundedTime
        {
            get => _jumpMaxPreGroundedTime;
            set => _jumpMaxPreGroundedTime = Mathf.Max(0.0f, value);
        }

        public float JumpMaxPostGroundedTime
        {
            get => _jumpMaxPostGroundedTime;
            set => _jumpMaxPostGroundedTime = Mathf.Max(0.0f, value);
        }

        public float JumpInputHoldTime
        {
            get => _jumpState.InputHoldTime;
            protected set => _jumpState.InputHoldTime = value;
        }

        public float JumpForceTimeRemaining
        {
            get => _jumpState.ForceTimeRemaining;
            protected set => _jumpState.ForceTimeRemaining = value;
        }

        public int JumpCurrentCount
        {
            get => _jumpState.CurrentCount;
            protected set => _jumpState.CurrentCount = value;
        }

        public float MaxFlySpeed
        {
            get => _maxFlySpeed;
            set => _maxFlySpeed = Mathf.Max(0.0f, value);
        }

        public float BrakingDecelerationFlying
        {
            get => _brakingDecelerationFlying;
            set => _brakingDecelerationFlying = Mathf.Max(0.0f, value);
        }

        public float FlyingFriction
        {
            get => _flyingFriction;
            set => _flyingFriction = Mathf.Max(0.0f, value);
        }

        public float MaxSwimSpeed
        {
            get => _maxSwimSpeed;
            set => _maxSwimSpeed = Mathf.Max(0.0f, value);
        }

        public float BrakingDecelerationSwimming
        {
            get => _brakingDecelerationSwimming;
            set => _brakingDecelerationSwimming = Mathf.Max(0.0f, value);
        }

        public float SwimmingFriction
        {
            get => _swimmingFriction;
            set => _swimmingFriction = Mathf.Max(0.0f, value);
        }

        public float Buoyancy
        {
            get => _buoyancy;
            set => _buoyancy = Mathf.Max(0.0f, value);
        }

        public float GravityScale
        {
            get => _gravityScale;
            set => _gravityScale = value;
        }

        public bool EnablePhysicsInteraction
        {
            set
            {
                if (_characterMovement)
                    _characterMovement.EnablePhysicsInteraction = value;
            }
        }

        public bool ApplyPushForceToCharacters
        {
            set
            {
                if (_characterMovement)
                    _characterMovement.PhysicsInteractionAffectsCharacters = value;
            }
        }

        public float Mass
        {
            get => _mass;
            set
            {
                _mass = Mathf.Max(1e-07f, value);

                if (_characterMovement && _characterMovement.Rigidbody)
                    _characterMovement.Rigidbody.mass = _mass;
            }
        }

        public float PushForceScale
        {
            get => _pushForceScale;
            set
            {
                _pushForceScale = Mathf.Max(0.0f, value);

                if (_characterMovement)
                    _characterMovement.PushForceScale = _pushForceScale;
            }
        }

        public float StandingDownwardForceScale
        {
            get => _standingDownwardForceScale;
            set => _standingDownwardForceScale = Mathf.Max(0.0f, value);
        }

        public bool ImpartPlatformVelocity
        {
            set
            {
                if (_characterMovement)
                    _characterMovement.ImpartPlatformVelocity = value;
            }
        }

        public bool ImpartPlatformMovement
        {
            set
            {
                if (_characterMovement)
                    _characterMovement.ImpartPlatformMovement = value;
            }
        }

        public bool ImpartPlatformRotation
        {
            set
            {
                if (_characterMovement)
                    _characterMovement.ImpartPlatformRotation = value;
            }
        }

        public delegate void PhysicsVolumeChangedEventHandler(PhysicsVolume newPhysicsVolume);

        public delegate void MovementModeChangedEventHandler(EMovementMode prevMovementMode, int prevCustomMode);
        public delegate void CustomMovementModeUpdateEventHandler(float deltaTime);

        public delegate void CustomRotationModeUpdateEventHandler(float deltaTime);

        public delegate void BeforeSimulationUpdateEventHandler(float deltaTime);
        public delegate void AfterSimulationUpdateEventHandler(float deltaTime);
        public delegate void CharacterMovementUpdateEventHandler(float deltaTime);

        public delegate void CollidedEventHandler(ref CollisionResult collisionResult);
        public delegate void FoundGroundEventHandler(ref FindGroundResult foundGround);
        public delegate void LandedEventHandled(Vector3 landingVelocity);

        public delegate void CrouchedEventHandler();
        public delegate void UnCrouchedEventHandler();

        public delegate void JumpedEventHandler();
        public delegate void ReachedJumpApexEventHandler();

        public event PhysicsVolumeChangedEventHandler PhysicsVolumeChanged;

        public event MovementModeChangedEventHandler MovementModeChanged;

        public event CustomMovementModeUpdateEventHandler CustomMovementModeUpdated;

        public event CustomRotationModeUpdateEventHandler CustomRotationModeUpdated;

        public event BeforeSimulationUpdateEventHandler BeforeSimulationUpdated;

        public event AfterSimulationUpdateEventHandler AfterSimulationUpdated;

        public event CharacterMovementUpdateEventHandler CharacterMovementUpdated;

        public event CollidedEventHandler Collided;

        public event FoundGroundEventHandler FoundGround;

        public event LandedEventHandled Landed;

        public event CrouchedEventHandler Crouched;

        public event UnCrouchedEventHandler UnCrouched;

        public event JumpedEventHandler Jumped;

        public event ReachedJumpApexEventHandler ReachedJumpApex;

        protected virtual void OnCustomMovementMode(float deltaTime)
        {
            CustomMovementModeUpdated?.Invoke(deltaTime);
        }

        protected virtual void OnCustomRotationMode(float deltaTime)
        {
            CustomRotationModeUpdated?.Invoke(deltaTime);
        }

        protected virtual void OnBeforeSimulationUpdate(float deltaTime)
        {
            BeforeSimulationUpdated?.Invoke(deltaTime);
        }

        protected virtual void OnAfterSimulationUpdate(float deltaTime)
        {
            AfterSimulationUpdated?.Invoke(deltaTime);
        }

        protected virtual void OnCharacterMovementUpdated(float deltaTime)
        {
            CharacterMovementUpdated?.Invoke(deltaTime);
        }
        protected virtual void OnCollided(ref CollisionResult collisionResult)
        {
            Collided?.Invoke(ref collisionResult);
        }

        protected virtual void OnFoundGround(ref FindGroundResult foundGround)
        {
            FoundGround?.Invoke(ref foundGround);
        }

        protected virtual void OnLanded(Vector3 landingVelocity)
        {
            Landed?.Invoke(landingVelocity);
        }

        protected virtual void OnCrouched()
        {
            Crouched?.Invoke();
        }

        protected virtual void OnUnCrouched()
        {
            UnCrouched?.Invoke();
        }

        protected virtual void OnJumped()
        {
            Jumped?.Invoke();
        }

        protected virtual void OnReachedJumpApex()
        {
            ReachedJumpApex?.Invoke();
        }

        private void InvokeMovementModeChangedEvent(EMovementMode prevMovementMode, int prevCustomMode)
        {
            MovementModeChanged?.Invoke(prevMovementMode, prevCustomMode);
        }

        protected virtual void CacheComponents()
        {
            _transform = GetComponent<Transform>();
            _characterMovement = GetComponent<CharacterMovement>();
            _camera = Camera.main;
            CharacterMovement.PushForceScale = _pushForceScale;

            Mass = _mass;
        }

        protected virtual void Reset()
        {
            _rotationMode = ERotationMode.OrientRotationToMovement;
            _rotationRate = 540.0f;

            _startingMovementMode = EMovementMode.Walking;

            _maxWalkSpeed = 5.0f;
            _minAnalogWalkSpeed = 0.0f;
            _maxAcceleration = 20.0f;
            _brakingDecelerationWalking = 20.0f;
            _groundFriction = 8.0f;

            _canEverCrouch = true;
            _crouchedHeight = 1.25f;
            _unCrouchedHeight = 2.0f;
            _maxWalkSpeedCrouched = 3.0f;

            _maxFallSpeed = 40.0f;
            _brakingDecelerationFalling = 0.0f;
            _fallingLateralFriction = 0.3f;
            _airControl = 0.3f;

            _canEverJump = true;
            _canJumpWhileCrouching = true;
            _jumpMaxCount = 1;
            _jumpImpulse = 5.0f;
            _jumpMaxHoldTime = 0.0f;
            _jumpMaxPreGroundedTime = 0.0f;
            _jumpMaxPostGroundedTime = 0.0f;

            _maxFlySpeed = 10.0f;
            _brakingDecelerationFlying = 0.0f;
            _flyingFriction = 1.0f;

            _maxSwimSpeed = 3.0f;
            _brakingDecelerationSwimming = 0.0f;
            _swimmingFriction = 0.0f;
            _buoyancy = 1.0f;

            _gravity = new Vector3(0.0f, -9.81f, 0.0f);
            _gravityScale = 1.0f;

            _applyStandingDownwardForce = false;

            _mass = 1.0f;
            _pushForceScale = 1.0f;
            _standingDownwardForceScale = 1.0f;
        }

        protected virtual void OnValidate()
        {
            RotationRate = _rotationRate;

            MaxWalkSpeed = _maxWalkSpeed;
            MinAnalogWalkSpeed = _minAnalogWalkSpeed;
            MaxAcceleration = _maxAcceleration;
            BrakingDecelerationWalking = _brakingDecelerationWalking;
            GroundFriction = _groundFriction;

            CrouchedHeight = _crouchedHeight;
            UnCrouchedHeight = _unCrouchedHeight;
            MaxWalkSpeedCrouched = _maxWalkSpeedCrouched;

            MaxFallSpeed = _maxFallSpeed;
            BrakingDecelerationFalling = _brakingDecelerationFalling;
            FallingLateralFriction = _fallingLateralFriction;
            AirControl = _airControl;

            JumpMaxCount = _jumpMaxCount;
            JumpImpulse = _jumpImpulse;
            JumpMaxHoldTime = _jumpMaxHoldTime;
            JumpMaxPreGroundedTime = _jumpMaxPreGroundedTime;
            JumpMaxPostGroundedTime = _jumpMaxPostGroundedTime;

            MaxFlySpeed = _maxFlySpeed;
            BrakingDecelerationFlying = _brakingDecelerationFlying;
            FlyingFriction = _flyingFriction;

            MaxSwimSpeed = _maxSwimSpeed;
            BrakingDecelerationSwimming = _brakingDecelerationSwimming;
            SwimmingFriction = _swimmingFriction;
            Buoyancy = _buoyancy;

            GravityScale = _gravityScale;

            if (_characterMovement == null)
                _characterMovement = GetComponent<CharacterMovement>();

            Mass = _mass;
            PushForceScale = _pushForceScale;
            StandingDownwardForceScale = _standingDownwardForceScale;
        }
        protected virtual void Awake()
        {
            CacheComponents();
            _simulationLoop = new CharacterSimulationLoop(OnLateFixedUpdate);
            SetMovementMode(_startingMovementMode);
        }

        protected virtual void OnEnable()
        {
            _enableGeneration++;

            CharacterMovement.Collided += OnCollided;
            CharacterMovement.FoundGround += OnFoundGround;

            _simulationLoop?.Start();
        }

        protected virtual void OnDisable()
        {
            _enableGeneration++;

            CharacterMovement.Collided -= OnCollided;
            CharacterMovement.FoundGround -= OnFoundGround;

            _simulationLoop?.Stop();
        }

        protected virtual void Start()
        {
            if (_startingMovementMode == EMovementMode.Walking)
            {
                CharacterMovement.SetPosition(Transform.position, true);
            }
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            AddPhysicsVolume(other);
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            RemovePhysicsVolume(other);
        }
    }
}
