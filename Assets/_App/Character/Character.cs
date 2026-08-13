using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Unity.Cinemachine;
using UnityEngine;

namespace _App
{
    [RequireComponent(typeof(CharacterMovement))]
    public class Character : MonoBehaviour
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
        
        private readonly List<PhysicsVolume> _physicsVolumes = new List<PhysicsVolume>();
        private readonly bool _enableAutoSimulation = true;

        private CancellationTokenSource _cancellationTokenSource;
        
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

        private bool _isJumping;
        private float _jumpInputHoldTime;
        private float _jumpForceTimeRemaining;
        private int _jumpCurrentCount;

        private float _fallingTime;
        private PhysicsVolume _physicsVolume;
        public RootMotionController RootMotionController { get; private set; }

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
        public bool JumpApexNotify { get; set; }
        public bool JumpInputPressed { get; protected set; }
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
            get => _jumpInputHoldTime;
            protected set => _jumpInputHoldTime = Mathf.Max(0.0f, value);
        }
        
        public float JumpForceTimeRemaining
        {
            get => _jumpForceTimeRemaining;
            protected set => _jumpForceTimeRemaining = Mathf.Max(0.0f, value);
        }
        
        public int JumpCurrentCount
        {
            get => _jumpCurrentCount;
            protected set => _jumpCurrentCount = Mathf.Max(0, value);
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
        
        public Vector3 GetGravityVector()
        {
            return Gravity;
        }

        public Vector3 GetGravityDirection()
        {
            return Gravity.normalized;
        }

        public float GetGravityMagnitude()
        {
            return Gravity.magnitude;
        }

        protected void SetGravityVector(Vector3 newGravityVector)
        {
            _gravity = newGravityVector;
        }
        

        private void EnableAutoSimulation(bool enable)
        {
            if (enable)
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = new CancellationTokenSource();

                LateFixedUpdateAsync(_cancellationTokenSource.Token).Forget();
            }
            else
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource = null;
            }
        }

        private async UniTask LateFixedUpdateAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await UniTask.WaitForFixedUpdate(cancellationToken);

                OnLateFixedUpdate();
            }
        }
        
        protected virtual void CacheComponents()
        {
            RootMotionController = GetComponentInChildren<RootMotionController>();
            _transform = GetComponent<Transform>();
            _characterMovement = GetComponent<CharacterMovement>();
            _camera = Camera.main;
            CharacterMovement.PushForceScale = _pushForceScale;

            Mass = _mass;
        }

        protected virtual void SetPhysicsVolume(PhysicsVolume newPhysicsVolume)
        {
            if (newPhysicsVolume == _physicsVolume)
                return;
            
            OnPhysicsVolumeChanged(newPhysicsVolume);
            
            _physicsVolume = newPhysicsVolume;
        }

        protected virtual void OnPhysicsVolumeChanged(PhysicsVolume newPhysicsVolume)
        {
            if (newPhysicsVolume && newPhysicsVolume.WaterVolume)
            {
                SetMovementMode(EMovementMode.Swimming);
            }
            else if (IsInWaterPhysicsVolume() && newPhysicsVolume == null)
            {
                if (IsSwimming())
                {
                    SetMovementMode(EMovementMode.Falling);
                }
            }
            
            PhysicsVolumeChanged?.Invoke(newPhysicsVolume);
        }

        protected virtual void UpdatePhysicsVolume(PhysicsVolume newPhysicsVolume)
        {
            Vector3 characterCenter = CharacterMovement.WorldCenter;

            if (newPhysicsVolume && newPhysicsVolume.BoxCollider.ClosestPoint(characterCenter) == characterCenter)
            {
                SetPhysicsVolume(newPhysicsVolume);
            }
            else
            {
                SetPhysicsVolume(null);
            }
        }

        protected virtual void AddPhysicsVolume(Collider other)
        {
            if (other.TryGetComponent(out PhysicsVolume volume) && !_physicsVolumes.Contains(volume))
                _physicsVolumes.Insert(0, volume);
        }

        protected virtual void RemovePhysicsVolume(Collider other)
        {
            if (other.TryGetComponent(out PhysicsVolume volume) && _physicsVolumes.Contains(volume))
                _physicsVolumes.Remove(volume);
        }

        protected virtual void UpdatePhysicsVolumes()
        {
            PhysicsVolume volume = null;
            int maxPriority = int.MinValue;

            for (int i = 0, c = _physicsVolumes.Count; i < c; i++)
            {
                PhysicsVolume vol = _physicsVolumes[i];
                if (vol.Priority <= maxPriority)
                    continue;

                maxPriority = vol.Priority;
                volume = vol;
            }
            
            UpdatePhysicsVolume(volume);
        }

        protected virtual bool IsInWaterPhysicsVolume()
        {
            return _physicsVolume && _physicsVolume.WaterVolume;
        }

        public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
        {
            CharacterMovement.AddForce(force, forceMode);
        }

        public void LaunchCharacter(Vector3 launchVelocity, bool overrideVerticalVelocity = false,
            bool overrideLateralVelocity = false)
        {
            CharacterMovement.LaunchCharacter(launchVelocity, overrideVerticalVelocity, overrideLateralVelocity);
        }

        public void IgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            CharacterMovement.IgnoreCollision(otherCollider, ignore);
        }

        public void PauseGroundConstraint(float seconds = 0.1f)
        {
            CharacterMovement.PauseGroundConstraint(seconds);
        }

        public void EnableGroundConstraint(bool enable)
        {
            CharacterMovement.ConstrainToGround = enable;
        }

        public bool WasOnGround()
        {
            return CharacterMovement.WasOnGround;
        }

        private bool IsOnGround()
        {
            return CharacterMovement.IsOnGround;
        }

        private bool IsOnWalkableGround()
        {
            return CharacterMovement.IsOnWalkableGround;
        }

        public bool IsGrounded()
        {
            return CharacterMovement.IsGrounded;
        }

        public CharacterMovement GetCharacterMovement()
        {
            return CharacterMovement;
        }

        public Vector3 GetPosition()
        {
            return CharacterMovement.Position;
        }

        public void TeleportPosition(Vector3 newPosition, bool interpolating = true, bool updateGround = false)
        {
            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.None;
            }

            CharacterMovement.SetPosition(newPosition, updateGround);

            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public Quaternion GetRotation()
        {
            return CharacterMovement.Rotation;
        }

        public void SetRotation(Quaternion newRotation)
        {
            CharacterMovement.Rotation = newRotation;
        }

        public void TeleportRotation(Quaternion newRotation, bool interpolating = true)
        {
            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.None;
            }

            CharacterMovement.SetRotation(newRotation);

            if (interpolating)
            {
                CharacterMovement.Interpolation = RigidbodyInterpolation.Interpolate;
            }
        }

        public virtual Vector3 GetUpVector()
        {
            return Transform.up;
        }

        public virtual Vector3 GetRightVector()
        {
            return Transform.right;
        }

        public virtual Vector3 GetForwardVector()
        {
            return Transform.forward;
        }

        protected virtual void RotateTowards(Vector3 worldDirection, float deltaTime, bool updateYawOnly = true, float maxTurnAngle = 0f)
        {
            Vector3 characterUp = GetUpVector();

            if (updateYawOnly)
                worldDirection = Vector3.ProjectOnPlane(worldDirection, characterUp);

            if (worldDirection == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection, characterUp);
            CharacterMovement.Rotation = Quaternion.RotateTowards(Rotation, targetRotation, RotationRate * deltaTime);
        }
        
        protected virtual void HandleRotationToViewAndMovement(float deltaTime)
        {
            if (MovementMode == EMovementMode.Custom && CustomMovement == (int)ECustomMovementMode.Dodging)
            {
                return;
            }
            
            Vector3 characterUp = GetUpVector();
            bool shouldRemainVertical = IsWalking() || IsFalling();

            if (_movementDirection == Vector3.zero)
            {
                RotateTowards(CameraTransform.forward, deltaTime, shouldRemainVertical);
                return;
            }

            Vector3 movementDirection = Vector3.ProjectOnPlane(_movementDirection, characterUp).normalized;
            Vector3 cameraDirection = Vector3.ProjectOnPlane(CameraTransform.forward, characterUp).normalized;

            float angleBetween = Vector3.Angle(movementDirection, cameraDirection);

            if (angleBetween > 75f && angleBetween < 105f)
            {
                RotateTowards(movementDirection, deltaTime, shouldRemainVertical);
                return;
            }

            if (angleBetween > 105f)
            {
                Vector3 combinedDirection = Vector3.Slerp(cameraDirection, -movementDirection, 0.3f);
                RotateTowards(combinedDirection, deltaTime, shouldRemainVertical);
                return;
            }

            RotateTowards(movementDirection, deltaTime, shouldRemainVertical);
        }

        public Vector3 GetVelocity()
        {
            return CharacterMovement.Velocity;
        }

        public void SetVelocity(Vector3 newVelocity)
        {
            CharacterMovement.Velocity = newVelocity;
        }

        public float GetSpeed()
        {
            return CharacterMovement.Velocity.magnitude;
        }

        public Vector3 GetMovementDirection()
        {
            return _movementDirection;
        }

        public void SetMovementDirection(Vector3 movementDirection)
        {
            _movementDirection = movementDirection;
        }

        public void SetYaw(float value)
        {
            CharacterMovement.Rotation = Quaternion.Euler(0.0f, value, 0.0f);
        }

        public virtual void AddYawInput(float value)
        {
            _rotationInput.y += value;
        }

        protected virtual void AddPitchInput(float value)
        {
            _rotationInput.x += value;
        }

        protected virtual void AddRollInput(float value)
        {
            _rotationInput.z += value;
        }

        protected virtual void ConsumeRotationInput()
        {
            if (_rotationInput != Vector3.zero)
            {
                CharacterMovement.Rotation *= Quaternion.Euler(_rotationInput);

                _rotationInput = Vector3.zero;
            }
        }

        public void SetMovementMode(EMovementMode newMovementMode, int newCustomMode = 0)
        {
            if (newMovementMode == _movementMode)
            {
                if (newMovementMode != EMovementMode.Custom || newCustomMode == _customMovementMode)
                    return;
            }
            
            EMovementMode prevMovementMode = _movementMode;
            int prevCustomMode = _customMovementMode;

            _movementMode = newMovementMode;
            _customMovementMode = newCustomMode;

            _footIK?.EnableIK(!(newMovementMode == EMovementMode.Custom && newCustomMode == (int)ECustomMovementMode.Climbing));

            OnMovementModeChanged(prevMovementMode, prevCustomMode);
        }

        protected virtual void OnMovementModeChanged(EMovementMode prevMovementMode, int prevCustomMode)
        {
            switch (MovementMode)
            {
                case EMovementMode.None:
                    CharacterMovement.Velocity = Vector3.zero;
                    CharacterMovement.ClearAccumulatedForces();
                    break;
                
                case EMovementMode.Walking:
                    ResetJumpState();
                    if (prevMovementMode == EMovementMode.Flying || prevMovementMode == EMovementMode.Swimming)
                    {
                        CharacterMovement.ConstrainToGround = true;
                    }
                    if (prevMovementMode == EMovementMode.Falling)
                    {
                        OnLanded(CharacterMovement.LandedVelocity);
                    }
                    break;
                
                case EMovementMode.Falling:
                    if (prevMovementMode == EMovementMode.Flying || prevMovementMode == EMovementMode.Swimming)
                    {
                        CharacterMovement.ConstrainToGround = true;
                    }
                    break;
                
                case EMovementMode.Flying:
                case EMovementMode.Swimming:
                    ResetJumpState();
                    CharacterMovement.ConstrainToGround = false;
                    break;
            }

            if (!IsFalling())
            {
                _fallingTime = 0.0f;
            }
            
            InvokeMovementModeChangedEvent(prevMovementMode, prevCustomMode);
        }

        public virtual bool CanAttack()
        {
            return IsWalking() && !IsFalling() && !IsCrouched() && !IsDodging();
        }
        
        public virtual bool IsWalking()
        {
            return _movementMode == EMovementMode.Walking;
        }

        public virtual bool IsFalling()
        {
            return _movementMode == EMovementMode.Falling;
        }

        public virtual bool IsFlying()
        {
            return _movementMode == EMovementMode.Flying;
        }

        public virtual bool IsSwimming()
        {
            return _movementMode == EMovementMode.Swimming;
        }

        public virtual bool IsDodging()
        {
            return _movementMode == EMovementMode.Custom && _customMovementMode == (int)ECustomMovementMode.Dodging;
        }

        public virtual bool IsClimbing()
        {
            return _movementMode == EMovementMode.Custom && _customMovementMode == (int)ECustomMovementMode.Climbing;
        }

        public virtual float GetMaxSpeed()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:
                    
                    float baseSpeed = IsCrouched() ? MaxWalkSpeedCrouched : MaxWalkSpeed;
                    
                    Vector3 movementDirection = GetMovementDirection();
                    Vector3 forwardDirection = GetForwardVector();

                    if (Vector3.Dot(movementDirection.normalized, forwardDirection.normalized) < 0)
                    {
                        baseSpeed *= 0.75f;
                    }
                    
                    return baseSpeed;

                case EMovementMode.Falling:
                    return MaxWalkSpeed;

                case EMovementMode.Swimming:
                    return MaxSwimSpeed;

                case EMovementMode.Flying:
                    return MaxFlySpeed;

                default:
                    return 1.0f;
            }
        }

        public virtual float GetMinAnalogSpeed()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:
                case EMovementMode.Falling:
                    return MinAnalogWalkSpeed;

                default:
                    return 0.0f;
            }
        }

        public virtual float GetMaxAcceleration()
        {
            if (IsFalling())
                return MaxAcceleration * AirControl;

            return MaxAcceleration;
        }

        protected virtual float GetMaxBrakingDeceleration()
        {
            switch (_movementMode)
            {
                case EMovementMode.Walking:
                    return BrakingDecelerationWalking;

                case EMovementMode.Falling:
                    return CharacterMovement.IsOnGround ? 0.0f : BrakingDecelerationFalling;

                case EMovementMode.Swimming:
                    return BrakingDecelerationSwimming;

                case EMovementMode.Flying:
                    return BrakingDecelerationFlying;

                default:
                    return 0.0f;
            }
        }
        
        protected virtual float ComputeAnalogInputModifier(Vector3 desiredVelocity)
        {
            float maxSpeed = GetMaxSpeed();
            
            if (desiredVelocity.sqrMagnitude > 0.0f && maxSpeed > 0.00000001f)
            {
                return Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);
            }

            return 0.0f;
        }

        protected virtual Vector3 ApplyVelocityBraking(Vector3 velocity, float friction, float maxBrakingDeceleration, float deltaTime)
        {
            const float minTickTime = 0.000001f;
            if (velocity.IsZero() || deltaTime < minTickTime)
                return velocity;
            
            bool isZeroFriction = friction == 0.0f;
            bool isZeroBraking = maxBrakingDeceleration == 0.0f;
            if (isZeroFriction && isZeroBraking)
            {
                return velocity;
            }
            
            Vector3 oldVel = velocity;
            Vector3 revAccel = isZeroBraking ? Vector3.zero : -maxBrakingDeceleration * velocity.normalized;
            
            const float maxTimeStep = 1.0f / 33.0f;
            
            float remainingTime = deltaTime;
            while (remainingTime >= minTickTime)
            {
                float dt = remainingTime > maxTimeStep && !isZeroFriction
                    ? Mathf.Min(maxTimeStep, remainingTime * 0.5f)
                    : remainingTime;
                
                remainingTime -= dt;
                
                velocity += (-friction * velocity + revAccel) * dt;
                
                if (Vector3.Dot(velocity, oldVel) <= 0.0f)
                    return Vector3.zero;
            }
            
            float sqrSpeed = velocity.sqrMagnitude;
            if (sqrSpeed <= 0.00001f || (!isZeroBraking && sqrSpeed <= 0.1f))
                return Vector3.zero;

            return velocity;
        }

        protected virtual Vector3 CalcVelocity(Vector3 velocity, Vector3 desiredVelocity, float friction, bool isFluid, float deltaTime)
        {
            const float minTickTime = 0.000001f;
            if (deltaTime < minTickTime)
                return velocity;
            
            float desiredSpeed = desiredVelocity.magnitude;
            Vector3 desiredMoveDirection = desiredSpeed > 0.0f ? desiredVelocity / desiredSpeed : Vector3.zero;
            
            float analogInputModifier = ComputeAnalogInputModifier(desiredVelocity);
            Vector3 inputAcceleration = GetMaxAcceleration() * analogInputModifier * desiredMoveDirection;
            
            float actualMaxSpeed = Mathf.Max(GetMinAnalogSpeed(), GetMaxSpeed() * analogInputModifier);
            
            bool isZeroAcceleration = inputAcceleration.IsZero();
            bool isVelocityOverMax = velocity.IsExceeding(actualMaxSpeed);
            
            if (isZeroAcceleration || isVelocityOverMax)
            {
                Vector3 oldVelocity = velocity;
                
                float actualBrakingFriction = UseSeparateBrakingFriction ? BrakingFriction : friction;
                float actualBrakingAcceleration =
                    UseSeparateBrakingDeceleration ? BrakingDeceleration : GetMaxBrakingDeceleration();

                velocity = ApplyVelocityBraking(velocity, actualBrakingFriction, actualBrakingAcceleration, deltaTime);
                
                if (isVelocityOverMax && velocity.sqrMagnitude < actualMaxSpeed.Square() && Vector3.Dot(inputAcceleration, oldVelocity) > 0.0f)
                    velocity = oldVelocity.normalized * actualMaxSpeed;
            }
            else
            {
                Vector3 accelDir = inputAcceleration.normalized;
                float velMag = velocity.magnitude;

                velocity -= (velocity - accelDir * velMag) * Mathf.Min(friction * deltaTime, 1.0f);
            }
            
            if (isFluid)
                velocity *= 1.0f - Mathf.Min(friction * deltaTime, 1.0f);
            
            if (!isZeroAcceleration)
            {
                float newMaxSpeed = velocity.IsExceeding(actualMaxSpeed) ? velocity.magnitude : actualMaxSpeed;

                velocity += inputAcceleration * deltaTime;
                velocity = velocity.ClampedTo(newMaxSpeed);
            }

            if (RootMotionController?.RootMovementUpdate(out Vector3 targetPosition, out Quaternion targetRotation) ?? false)
            {
                CharacterMovement.SetPositionAndRotation(targetPosition, targetRotation, true);
            }

            return velocity;
        }

        protected virtual Vector3 ConstrainInputVector(Vector3 inputVector)
        {
            Vector3 worldUp = -GetGravityDirection();
            
            float inputVectorDotWorldUp = Vector3.Dot(inputVector, worldUp);
            if (!Mathf.Approximately(inputVectorDotWorldUp, 0.0f) && (IsWalking() || IsFalling()))
                inputVector = Vector3.ProjectOnPlane(inputVector, worldUp);

            return CharacterMovement.ConstrainVectorToPlane(inputVector);
        }

        protected virtual void CalcDesiredVelocity(float deltaTime)
        {
            Vector3 movementDirection = Vector3.ClampMagnitude(GetMovementDirection(), 1.0f);
            
            _desiredVelocity = ConstrainInputVector(movementDirection * GetMaxSpeed());
        }

        protected virtual Vector3 GetDesiredVelocity()
        {
            return _desiredVelocity;
        }

        protected float GetSignedSlopeAngle()
        {
            Vector3 movementDirection = GetMovementDirection();
            if (movementDirection.IsZero() || !IsOnGround())
                return 0.0f;

            Vector3 projMovementDirection =
                Vector3.ProjectOnPlane(movementDirection, CharacterMovement.GroundNormal).normalized;

            return Mathf.Asin(Vector3.Dot(projMovementDirection, -GetGravityDirection())) * Mathf.Rad2Deg;
        }

        protected virtual void ApplyDownwardsForce()
        {
            Rigidbody groundRigidbody = CharacterMovement.GroundRigidbody;
            if (!groundRigidbody || groundRigidbody.isKinematic)
                return;

            Vector3 downwardForce = Mass * GetGravityVector();
            groundRigidbody.AddForceAtPosition(downwardForce * StandingDownwardForceScale, GetPosition());
        }
        
        protected virtual void WalkingMovementMode(float deltaTime)
        {
            CharacterMovement.Velocity = CalcVelocity(CharacterMovement.Velocity, GetDesiredVelocity(), GroundFriction, false, deltaTime);
            
            if (ApplyStandingDownwardForce)
                ApplyDownwardsForce();
        }

        public virtual bool IsCrouched()
        {
            return _isCrouched;
        }

        public virtual void Crouch()
        {
            CrouchInputPressed = true;
        }
        
        public virtual void UnCrouch()
        {
            CrouchInputPressed = false;
        }

        protected virtual bool IsCrouchAllowed()
        {
            return CanEverCrouch && IsWalking();
        }
        
        protected virtual bool CanUnCrouch()
        {
            bool overlapped = CharacterMovement.CheckHeight(_unCrouchedHeight);
            return !overlapped;
        }
        
        protected virtual void CheckCrouchInput()
        {
            if (!_isCrouched && CrouchInputPressed && IsCrouchAllowed())
            {
                _isCrouched = true;
                
                OnCrouched();
            }
            else if (_isCrouched && (!CrouchInputPressed || !IsCrouchAllowed()))
            {
                if (!CanUnCrouch())
                    return;
                
                _isCrouched = false;
                
                OnUnCrouched();
            }
        }

        protected virtual void FallingMovementMode(float deltaTime)
        {
            Vector3 desiredVelocity = GetDesiredVelocity();
            
            Vector3 worldUp = -GetGravityDirection();

            if (IsOnGround() && !IsOnWalkableGround())
            {
                Vector3 groundNormal = CharacterMovement.GroundNormal;

                if (Vector3.Dot(desiredVelocity, groundNormal) < 0.0f)
                {
                    Vector3 groundNormal2D = Vector3.ProjectOnPlane(groundNormal, worldUp).normalized;
                    desiredVelocity = Vector3.ProjectOnPlane(desiredVelocity, groundNormal2D);
                }
                
                worldUp = Vector3.ProjectOnPlane(worldUp, groundNormal).normalized;
            }
            
            Vector3 verticalVelocity = Vector3.Project(CharacterMovement.Velocity, worldUp);
            Vector3 lateralVelocity = CharacterMovement.Velocity - verticalVelocity;
            
            lateralVelocity = CalcVelocity(lateralVelocity, desiredVelocity, FallingLateralFriction, false, deltaTime);
            
            verticalVelocity += Gravity * deltaTime;
            
            float actualFallSpeed = MaxFallSpeed;
            if (_physicsVolume)
                actualFallSpeed = _physicsVolume.MaxFallSpeed;

            if (Vector3.Dot(verticalVelocity, worldUp) < -actualFallSpeed)
                verticalVelocity = Vector3.ClampMagnitude(verticalVelocity, actualFallSpeed);
            
            CharacterMovement.Velocity = lateralVelocity + verticalVelocity;
            
            _fallingTime += deltaTime;
        }

        public virtual bool IsJumping()
        {
            return _isJumping;
        }
        
        public virtual void Jump()
        {
            JumpInputPressed = true;
        }

        public virtual void StopJumping()
        {
            JumpInputPressed = false;
            JumpInputHoldTime = 0.0f;
            
            ResetJumpState();
        }
        
        protected virtual void ResetJumpState()
        {
            if (!IsFalling())
                JumpCurrentCount = 0;

            JumpForceTimeRemaining = 0.0f;
            
            _isJumping = false;
        }
        
        public virtual bool IsJumpProvidingForce()
        {
            return JumpForceTimeRemaining > 0.0f;
        }

        protected virtual float GetMaxJumpHeight()
        {
            float gravityMagnitude = GetGravityMagnitude();
            if (gravityMagnitude > 0.0001f)
            {
                return JumpImpulse * JumpImpulse / (2.0f * gravityMagnitude);
            }
            
            return 0.0f;
        }
        
        public virtual float GetMaxJumpHeightWithJumpTime()
        {
            float maxJumpHeight = GetMaxJumpHeight();
            return maxJumpHeight + JumpImpulse * JumpMaxHoldTime;
        }
        
        protected virtual bool IsJumpAllowed()
        {
            if (!CanJumpWhileCrouching && IsCrouched())
                return false;

            return CanEverJump && (IsWalking() || IsFalling());
        }
        
        protected virtual bool CanJump()
        {
            bool isJumpAllowed = IsJumpAllowed();
            if (isJumpAllowed)
            {
                if (!_isJumping || JumpMaxHoldTime <= 0.0f)
                {
                    if (JumpCurrentCount == 0)
                    {
                        isJumpAllowed = JumpInputHoldTime <= JumpMaxPreGroundedTime;
                        
                        if (isJumpAllowed)
                            JumpInputHoldTime = 0.0f;
                    }
                    else
                    {
                        isJumpAllowed = JumpCurrentCount < JumpMaxCount && JumpInputHoldTime == 0.0f;
                    }
                }
                else
                {
                    bool jumpInputHeld = JumpInputPressed && JumpInputHoldTime < JumpMaxHoldTime;
                
                    isJumpAllowed = jumpInputHeld && (JumpCurrentCount < JumpMaxCount || (_isJumping && JumpCurrentCount == JumpMaxCount));
                }
            }

            return isJumpAllowed;
        }
        
        protected virtual bool DoJump()
        {
            Vector3 worldUp = -GetGravityDirection();
            
            if (CharacterMovement.IsConstrainedToPlane && 
                Mathf.Approximately(Vector3.Dot(CharacterMovement.GetPlaneConstraintNormal(), worldUp), 1.0f))
            {
                return false;
            }
            
            float verticalSpeed = Mathf.Max(Vector3.Dot(CharacterMovement.Velocity, worldUp), JumpImpulse);

            CharacterMovement.Velocity =
                Vector3.ProjectOnPlane(CharacterMovement.Velocity, worldUp) + worldUp * verticalSpeed;
            
            return true;
        }
        
        protected virtual void CheckJumpInput()
        {
            if (!JumpInputPressed)
                return;
            
            if (JumpCurrentCount == 0 && IsFalling() && FallingTime > JumpMaxPostGroundedTime)
                JumpCurrentCount++;

            bool didJump = CanJump() && DoJump();
            if (didJump)
            {
                if (!_isJumping)
                {
                    JumpCurrentCount++;
                    JumpForceTimeRemaining = JumpMaxHoldTime;
                    
                    CharacterMovement.PauseGroundConstraint();
                    SetMovementMode(EMovementMode.Falling);
                    
                    OnJumped();
                }
            }

            _isJumping = didJump;
        }
        
        protected virtual void UpdateJumpTimers(float deltaTime)
        {
            if (JumpInputPressed)
                JumpInputHoldTime += deltaTime;
            
            if (JumpForceTimeRemaining > 0.0f)
            {
                JumpForceTimeRemaining -= deltaTime;
                if (JumpForceTimeRemaining <= 0.0f)
                    ResetJumpState();
            }
        }
        
        protected virtual void NotifyJumpApex()
        {
            if (!JumpApexNotify)
                return;

            float verticalSpeed = Vector3.Dot(GetVelocity(), -GetGravityDirection());
            if (verticalSpeed >= 0.0f)
                return;

            JumpApexNotify = false;
            OnReachedJumpApex();
        }

        protected virtual void FlyingMovementMode(float deltaTime)
        {
            float actualFriction = IsInWaterPhysicsVolume() ? _physicsVolume.Friction : FlyingFriction;

            CharacterMovement.Velocity 
                = CalcVelocity(CharacterMovement.Velocity, GetDesiredVelocity(), actualFriction, true, deltaTime);
        }

        public virtual float CalcImmersionDepth()
        {
            float depth = 0.0f;

            if (IsInWaterPhysicsVolume())
            {
                float height = CharacterMovement.Height;
                if (height == 0.0f || Buoyancy == 0.0f)
                    depth = 1.0f;
                else
                {
                    Vector3 worldUp = -GetGravityDirection();
                    
                    Vector3 rayOrigin = GetPosition() + worldUp * height;
                    Vector3 rayDirection = -worldUp;

                    BoxCollider waterVolumeCollider = _physicsVolume.BoxCollider;
                    depth = !waterVolumeCollider.Raycast(new Ray(rayOrigin, rayDirection), out RaycastHit hitInfo, height)
                        ? 1.0f
                        : 1.0f - Mathf.InverseLerp(0.0f, height, hitInfo.distance);
                }
            }
            
            return depth;
        }

        protected virtual void SwimmingMovementMode(float deltaTime)
        {
            float depth = CalcImmersionDepth();
            float actualBuoyancy = Buoyancy * depth;
            
            Vector3 desiredVelocity = GetDesiredVelocity();
            Vector3 newVelocity = CharacterMovement.Velocity;
            
            Vector3 worldUp = -GetGravityDirection();
            float verticalSpeed = Vector3.Dot(newVelocity, worldUp);
            
            if (verticalSpeed > MaxSwimSpeed * 0.33f && actualBuoyancy > 0.0f)
            {
                verticalSpeed = Mathf.Max(MaxSwimSpeed * 0.33f, verticalSpeed * depth * depth);
                newVelocity = Vector3.ProjectOnPlane(newVelocity, worldUp) + worldUp * verticalSpeed;
            }
            else if (depth < 0.65f)
            {
                float verticalDesiredSpeed = Vector3.Dot(desiredVelocity, worldUp);
                
                desiredVelocity =
                    Vector3.ProjectOnPlane(desiredVelocity, worldUp) + worldUp * Mathf.Min(0.1f, verticalDesiredSpeed);
            }
            
            float actualFriction = IsInWaterPhysicsVolume()
                ? _physicsVolume.Friction * depth
                : SwimmingFriction * depth;

            newVelocity = CalcVelocity(newVelocity, desiredVelocity, actualFriction, true, deltaTime);
            
            newVelocity += Gravity * ((1.0f - actualBuoyancy) * deltaTime);
            
            CharacterMovement.Velocity = newVelocity;
        }

        protected virtual void CustomMovementMode(float deltaTime)
        {
            OnCustomMovementMode(deltaTime);
        }

        public ERotationMode GetRotationMode()
        {
            return _rotationMode;
        }

        public void SetRotationMode(ERotationMode rotationMode)
        {
            _rotationMode = rotationMode;
        }

        protected virtual void UpdateRotation(float deltaTime)
        {
            switch (_rotationMode)
            {
                case ERotationMode.None:
                    break;
                
                case ERotationMode.OrientRotationToMovement:
                {
                    bool shouldRemainVertical = IsWalking() || IsFalling();
                    RotateTowards(_movementDirection, deltaTime, shouldRemainVertical);
                    break;
                }
                
                case ERotationMode.OrientRotationToViewDirection when Camera != null:
                {
                    bool shouldRemainVertical = IsWalking() || IsFalling();
                    RotateTowards(CameraTransform.forward, deltaTime, shouldRemainVertical);
                    break;
                }
                
                case ERotationMode.OrientToViewAndMovement when Camera != null:
                {
                    HandleRotationToViewAndMovement(deltaTime);
                    break;
                }
                
                case ERotationMode.Custom:
                    CustomRotationMode(deltaTime);
                    break;
            }
        }

        
        protected virtual void CustomRotationMode(float deltaTime)
        {
            OnCustomRotationMode(deltaTime);
        }
        
        private void BeforeSimulationUpdate(float deltaTime)
        {
            if (IsWalking() && !IsGrounded())
                SetMovementMode(EMovementMode.Falling);
            
            if (IsFalling() && IsGrounded())
                SetMovementMode(EMovementMode.Walking);
            
            UpdatePhysicsVolumes();
            CheckCrouchInput();
            CheckJumpInput();
            UpdateJumpTimers(deltaTime);
            OnBeforeSimulationUpdate(deltaTime);
        }

        private void SimulationUpdate(float deltaTime)
        {
            CalcDesiredVelocity(deltaTime);
            
            switch (_movementMode)
            {
                case EMovementMode.None:
                    break;

                case EMovementMode.Walking:
                    WalkingMovementMode(deltaTime);
                    break;

                case EMovementMode.Falling:
                    FallingMovementMode(deltaTime);
                    break;

                case EMovementMode.Flying:
                    FlyingMovementMode(deltaTime);
                    break;

                case EMovementMode.Swimming:
                    SwimmingMovementMode(deltaTime);
                    break;

                case EMovementMode.Custom:
                    CustomMovementMode(deltaTime);
                    break;
            }
            
            UpdateRotation(deltaTime);
            
            ConsumeRotationInput();
        }
        
        private void AfterSimulationUpdate(float deltaTime)
        {
            NotifyJumpApex();
            
            OnAfterSimulationUpdate(deltaTime);
        }

        private void CharacterMovementUpdate(float deltaTime)
        {
            CharacterMovement.Move(deltaTime);
            
            OnCharacterMovementUpdated(deltaTime);
        }

        private void Simulate(float deltaTime)
        {
            if (IsPaused)
                return;
            
            BeforeSimulationUpdate(deltaTime);
            SimulationUpdate(deltaTime);
            AfterSimulationUpdate(deltaTime);
            CharacterMovementUpdate(deltaTime);
        }
        
        private void OnLateFixedUpdate()
        {
            Simulate(Time.fixedDeltaTime);
        }
        
        public void Pause(bool pause, bool clearState = true, bool useAutoUnpause = false, float pauseDuration = 0f)
        {
            IsPaused = pause;
            CharacterMovement.Collider.enabled = !IsPaused;
            
            if (clearState)
            {
                _movementDirection = Vector3.zero;
                _rotationInput = Vector3.zero;
                
                CharacterMovement.Velocity = Vector3.zero;
                CharacterMovement.ClearAccumulatedForces();
            }

            if (useAutoUnpause)
            {
                DelayUtility.InvokeAfterSeconds(pauseDuration, () => Pause(!pause, !clearState));
            }
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
            SetMovementMode(_startingMovementMode);
        }
        
        protected virtual void OnEnable()
        {
            CharacterMovement.Collided += OnCollided;
            CharacterMovement.FoundGround += OnFoundGround;
            
            if (_enableAutoSimulation)
                EnableAutoSimulation(true);
        }
        
        protected virtual void OnDisable()
        {
            CharacterMovement.Collided -= OnCollided;
            CharacterMovement.FoundGround -= OnFoundGround;
            
            if (_enableAutoSimulation)
                EnableAutoSimulation(false);
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
