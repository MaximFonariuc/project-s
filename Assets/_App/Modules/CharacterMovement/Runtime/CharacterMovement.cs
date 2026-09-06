using System;
using UnityEngine;

namespace ModularCharacterMovement
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed partial class CharacterMovement : MonoBehaviour
    {
        private const float KINDA_SMALL_NUMBER = 0.0001f;
        private const float HEMISPHERE_LIMIT = 0.01f;

        private const int INITIAL_COLLISION_CAPACITY = 16;
        private const int INITIAL_OVERLAP_CAPACITY = 16;

        private const float SWEEP_EDGE_REJECT_DISTANCE = 0.0015f;

        private const float MIN_GROUND_DISTANCE = 0.004f;
        private const float MAX_GROUND_DISTANCE = 0.009f;
        private const float AVG_GROUND_DISTANCE = (MIN_GROUND_DISTANCE + MAX_GROUND_DISTANCE) * 0.5f;

        private const float MIN_WALKABLE_SLOPE_LIMIT = 1.000000f;
        private const float MAX_WALKABLE_SLOPE_LIMIT = 0.017452f;

        private const float PENETRATION_OFFSET = 0.00125f;

        private const float CONTACT_OFFSET = 0.01f;
        private const float SMALL_CONTACT_OFFSET = 0.001f;

        [Space(15f)]
        [SerializeField] private EPlaneConstraintType _planeConstraint;

        [Space(15f)]
        [SerializeField] private float _radius;
        [SerializeField] private float _height;

        [Space(15f)]
        [SerializeField] private float _slopeLimit;
        [SerializeField] private float _stepOffset;
        [SerializeField] private float _perchOffset;
        [SerializeField] private float _perchAdditionalHeight;

        [Space(15f)]
        [SerializeField] private bool _slopeLimitOverride;

        [SerializeField] private bool _useFlatTop;
        [SerializeField] private bool _useFlatBaseForGroundChecks;

        [Space(15f)]
        [SerializeField] private LayerMask _collisionLayers = 1;
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;

        [Space(15f)]
        [SerializeField] private Advanced _advanced;

        private Transform _transform;
        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private readonly CharacterCapsuleShape _capsule = new CharacterCapsuleShape();
        private readonly CharacterCollisionFilter _collisionFilter = new CharacterCollisionFilter();
        private readonly CharacterCollisionBuffer _collisions =
            new CharacterCollisionBuffer(INITIAL_COLLISION_CAPACITY);
        private readonly CharacterForceAccumulator _forces = new CharacterForceAccumulator();
        private CharacterPhysicsQueries _queries;

        private float _minSlopeLimit;

        private bool _detectCollisions = true;

        private bool _reportedRejectedMove;

        private readonly CharacterGroundingState _grounding = new CharacterGroundingState();

        private Vector3 _constraintPlaneNormal;

        private Vector3 _characterUp;

        private Vector3 _velocity;

        private float _pushForceScale = 1.0f;

        private readonly CharacterMovingPlatformTracker _platforms = new CharacterMovingPlatformTracker();

        public Transform Transform
        {
            get
            {
#if UNITY_EDITOR
                if (_transform == null)
                    _transform = GetComponent<Transform>();
#endif

                return _transform;
            }
        }

        public Rigidbody Rigidbody
        {
            get
            {
#if UNITY_EDITOR
                if (_rigidbody == null)
                    _rigidbody = GetComponent<Rigidbody>();
#endif

                return _rigidbody;
            }
        }

        public RigidbodyInterpolation Interpolation
        {
            get => Rigidbody.interpolation;
            set => Rigidbody.interpolation = value;
        }

        public Collider Collider
        {
            get
            {
#if UNITY_EDITOR
                if (_capsuleCollider == null)
                    _capsuleCollider = GetComponent<CapsuleCollider>();
#endif

                return _capsuleCollider;
            }
        }

        public Vector3 Position => GetPosition();

        public Quaternion Rotation
        {
            get => GetRotation();
            set => SetRotation(value);
        }

        public Vector3 WorldCenter => Position + Rotation * _capsule.Center;

        public Vector3 UpdatedPosition { get; private set; }

        public Quaternion UpdatedRotation { get; private set; }

        public ref Vector3 Velocity => ref _velocity;

        public float Speed => _velocity.magnitude;

        public float ForwardSpeed => _velocity.Dot(Transform.forward);

        public float SidewaysSpeed => _velocity.Dot(Transform.right);

        public float Radius
        {
            get => _radius;
            set => SetDimensions(value, _height);
        }

        public float Height
        {
            get => _height;
            set => SetDimensions(_radius, value);
        }

        public float SlopeLimit
        {
            get => _slopeLimit;

            set
            {
                _slopeLimit = Mathf.Clamp(value, 0.0f, 89.0f);
                _minSlopeLimit = Mathf.Cos((_slopeLimit + 0.01f) * Mathf.Deg2Rad);
            }
        }

        public float StepOffset
        {
            get => _stepOffset;
            set => _stepOffset = Mathf.Max(0.0f, value);
        }

        public float PerchOffset
        {
            get => _perchOffset;
            set => _perchOffset = Mathf.Clamp(value, 0.0f, _radius);
        }

        public float PerchAdditionalHeight
        {
            get => _perchAdditionalHeight;
            set => _perchAdditionalHeight = Mathf.Max(0.0f, value);
        }

        public bool SlopeLimitOverride
        {
            get => _slopeLimitOverride;
            set => _slopeLimitOverride = value;
        }

        public bool UseFlatTop
        {
            get => _useFlatTop;
            set => _useFlatTop = value;
        }

        public bool UseFlatBaseForGroundChecks
        {
            get => _useFlatBaseForGroundChecks;
            set => _useFlatBaseForGroundChecks = value;
        }

        public LayerMask CollisionLayers
        {
            get => _collisionLayers;
            set => _collisionLayers = value;
        }

        public QueryTriggerInteraction TriggerInteraction  => _triggerInteraction;


        public CollisionFlags CollisionFlags => _collisions.Flags;

        public bool IsConstrainedToPlane => _planeConstraint != EPlaneConstraintType.None;

        public bool ConstrainToGround
        {
            get => _grounding.ConstrainToGround;
            set => _grounding.ConstrainToGround = value;
        }

        public bool DetectCollisions => _detectCollisions;
        public bool IsConstrainedToGround => _grounding.IsConstrained;

        public bool IsGroundConstraintPaused => _grounding.IsConstraintPaused;

        public float UnconstrainedTimer => _grounding.UnconstrainedTimer;

        public bool WasOnGround => _grounding.WasOnGround;

        public bool IsOnGround => _grounding.IsOnGround;

        public bool WasOnWalkableGround => _grounding.WasOnWalkableGround;

        public bool IsOnWalkableGround => _grounding.IsOnWalkableGround;

        public bool WasGrounded => _grounding.WasGrounded;

        public bool IsGrounded => _grounding.IsGrounded;

        public float GroundDistance => _grounding.CurrentGround.GroundDistance;

        public Vector3 GroundPoint => _grounding.CurrentGround.Point;

        public Vector3 GroundNormal => _grounding.CurrentGround.Normal;

        public Vector3 GroundSurfaceNormal => _grounding.CurrentGround.SurfaceNormal;

        public Collider GroundCollider => _grounding.CurrentGround.Collider;

        public Transform GroundTransform => _grounding.CurrentGround.Transform;

        public Rigidbody GroundRigidbody => _grounding.CurrentGround.Rigidbody;

        public FindGroundResult CurrentGround => _grounding.CurrentGround;

        public MovingPlatform MovingPlatform => _platforms.State;

        public Vector3 LandedVelocity => _grounding.LandedVelocity;

        public bool FastPlatformMove { get; set; }

        public bool ImpartPlatformMovement
        {
            get => _advanced.ImpartPlatformMovement;
            set => _advanced.ImpartPlatformMovement = value;
        }

        public bool ImpartPlatformRotation
        {
            get => _advanced.ImpartPlatformRotation;
            set => _advanced.ImpartPlatformRotation = value;
        }

        public bool ImpartPlatformVelocity
        {
            get => _advanced.ImpartPlatformVelocity;
            set => _advanced.ImpartPlatformVelocity = value;
        }

        public bool EnablePhysicsInteraction
        {
            get => _advanced.EnablePhysicsInteraction;
            set => _advanced.EnablePhysicsInteraction = value;
        }

        public bool PhysicsInteractionAffectsCharacters
        {
            get => _advanced.AllowPushCharacters;
            set => _advanced.AllowPushCharacters = value;
        }

        public float PushForceScale
        {
            get => _pushForceScale;
            set => _pushForceScale = Mathf.Max(0.0f, value);
        }

        public delegate bool ColliderFilterCallback(Collider collider);
        public delegate ECollisionBehaviour CollisionBehaviourCallback(Collider collider);
        public delegate void CollisionResponseCallback(ref CollisionResult inCollisionResult, ref Vector3 characterImpulse, ref Vector3 otherImpulse);

        public ColliderFilterCallback ColliderFilter { get; set; }
        public CollisionBehaviourCallback CollisionBehaviour { get; set; }
        public CollisionResponseCallback CollisionResponse { get; set; }

        public delegate void CollidedEventHandler(ref CollisionResult collisionResult);
        public delegate void FoundGroundEventHandler(ref FindGroundResult foundGround);

        public event CollidedEventHandler Collided;
        public event FoundGroundEventHandler FoundGround;

        private void OnCollided()
        {
            if (Collided == null)
                return;

            for (int i = 0; i < _collisions.Count; i++)
                Collided.Invoke(ref _collisions[i]);
        }

        private void OnFoundGround()
        {
            FoundGround?.Invoke(ref _grounding.CurrentGround);
        }




        public void SetDimensions(float characterRadius, float characterHeight)
        {
            _radius = Mathf.Max(characterRadius, 0.0f);
            _height = Mathf.Max(characterHeight, _radius * 2.0f);

#if UNITY_EDITOR
            if (_capsuleCollider == null)
                _capsuleCollider = GetComponent<CapsuleCollider>();
#endif

            _capsule.Resize(_radius, _height, _capsuleCollider);
        }

        public void SetHeight(float characterHeight)
        {
            _height = Mathf.Max(characterHeight, _radius * 2.0f);

#if UNITY_EDITOR
            if (_capsuleCollider == null)
                _capsuleCollider = GetComponent<CapsuleCollider>();
#endif

            _capsule.Resize(_radius, _height, _capsuleCollider);
        }

        private void CacheComponents()
        {
            _transform = GetComponent<Transform>();

            _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody)
            {
                _rigidbody.linearDamping = 0.0f;
                _rigidbody.angularDamping = 0.0f;

                _rigidbody.useGravity = false;
                _rigidbody.isKinematic = true;
            }

            _capsuleCollider = GetComponent<CapsuleCollider>();

            _collisionFilter.Bind(_rigidbody, _capsuleCollider);
            _queries = new CharacterPhysicsQueries(
                INITIAL_COLLISION_CAPACITY,
                INITIAL_OVERLAP_CAPACITY,
                _collisionFilter);
        }

        public Vector3 GetPlaneConstraintNormal()
        {
            return _constraintPlaneNormal;
        }

        public void SetPlaneConstraint(EPlaneConstraintType constrainAxis, Vector3 planeNormal)
        {
            _planeConstraint = constrainAxis;

            switch (_planeConstraint)
            {
                case EPlaneConstraintType.None:
                    {
                        _constraintPlaneNormal = Vector3.zero;

                        if (_rigidbody)
                            _rigidbody.constraints = RigidbodyConstraints.None;

                        break;
                    }

                case EPlaneConstraintType.ConstrainXAxis:
                    {
                        _constraintPlaneNormal = Vector3.right;

                        if (_rigidbody)
                            _rigidbody.constraints = RigidbodyConstraints.FreezePositionX;

                        break;
                    }

                case EPlaneConstraintType.ConstrainYAxis:
                    {
                        _constraintPlaneNormal = Vector3.up;

                        if (_rigidbody)
                            _rigidbody.constraints = RigidbodyConstraints.FreezePositionY;

                        break;
                    }

                case EPlaneConstraintType.ConstrainZAxis:
                    {
                        _constraintPlaneNormal = Vector3.forward;

                        if (_rigidbody)
                            _rigidbody.constraints = RigidbodyConstraints.FreezePositionZ;

                        break;
                    }

                case EPlaneConstraintType.Custom:
                    {
                        _constraintPlaneNormal = planeNormal;

                        if (_rigidbody)
                            _rigidbody.constraints = RigidbodyConstraints.None;

                        break;
                    }

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Vector3 ConstrainDirectionToPlane(Vector3 direction)
        {
            return ConstrainVectorToPlane(direction).normalized;
        }

        public Vector3 ConstrainVectorToPlane(Vector3 vector)
        {
            return IsConstrainedToPlane ? vector.ProjectedOnPlane(_constraintPlaneNormal) : vector;
        }

        public void SetPosition(Vector3 newPosition, bool updateGround = false)
        {
            UpdatedPosition = newPosition;

            if (updateGround)
            {
                FindGround(UpdatedPosition, out FindGroundResult groundResult);
                {
                    UpdateCurrentGround(ref groundResult);

                    AdjustGroundHeight();

                    UpdateCurrentPlatform();
                }
            }

            Rigidbody.position = UpdatedPosition;
            Transform.position = UpdatedPosition;
        }

        public Vector3 GetPosition()
        {
            return Transform.position;
        }

        public Vector3 GetFootPosition()
        {
            return Transform.position - Transform.up * AVG_GROUND_DISTANCE;
        }

        public void SetRotation(Quaternion newRotation)
        {
            UpdatedRotation = newRotation;

            Rigidbody.rotation = UpdatedRotation;
            Transform.rotation = UpdatedRotation;
        }

        public Quaternion GetRotation()
        {
            return Transform.rotation;
        }

        public void SetPositionAndRotation(Vector3 newPosition, Quaternion newRotation, bool updateGround = false)
        {
            UpdatedPosition = newPosition;
            UpdatedRotation = newRotation;

            if (updateGround)
            {
                FindGround(UpdatedPosition, out FindGroundResult groundResult);
                {
                    UpdateCurrentGround(ref groundResult);

                    AdjustGroundHeight();

                    UpdateCurrentPlatform();
                }
            }

            Rigidbody.position = UpdatedPosition;
            Rigidbody.rotation = UpdatedRotation;

            Transform.SetPositionAndRotation(UpdatedPosition, UpdatedRotation);
        }

        public void RotateTowards(Vector3 worldDirection, float maxDegreesDelta, bool updateYawOnly = true)
        {
            Vector3 characterUp = Transform.up;

            if (updateYawOnly)
                worldDirection = worldDirection.ProjectedOnPlane(characterUp);

            if (worldDirection == Vector3.zero)
                return;

            Quaternion targetRotation = Quaternion.LookRotation(worldDirection, characterUp);

            Rotation = Quaternion.RotateTowards(Rotation, targetRotation, maxDegreesDelta);
        }

        private void UpdateCachedFields()
        {
            _grounding.BeginMove();

            UpdatedPosition = Transform.position;
            UpdatedRotation = Transform.rotation;

            _characterUp = UpdatedRotation * Vector3.up;

            _capsule.UpdateRotation(UpdatedRotation);

            ResetCollisionFlags();
        }

        public void ClearAccumulatedForces()
        {
            _forces.Clear();
        }

        private void ReportRejectedMove(string rejectedInput)
        {
            if (_reportedRejectedMove)
                return;

            _reportedRejectedMove = true;
            Debug.LogError(
                $"{nameof(CharacterMovement)} rejected a non-finite move ({rejectedInput}). " +
                "Velocity and accumulated forces were reset. Further occurrences are not reported.",
                this);
        }

        public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
        {
            _forces.Add(force, forceMode, Rigidbody.mass);
        }

        public void AddExplosionForce(float strength, Vector3 origin, float radius, float upwardModifier, ForceMode forceMode = ForceMode.Force)
        {
            Vector3 delta = WorldCenter - origin;
            float deltaMagnitude = delta.magnitude;
            if (deltaMagnitude > radius)
                return;

            Vector3 explosionDirection = delta.normalized;
            float attenuation = 1.0f - Mathf.Clamp01(deltaMagnitude / radius);
            Vector3 force = explosionDirection * (strength * attenuation);

            if (upwardModifier != 0.0f)
            {
                force += Vector3.up * (upwardModifier * attenuation);
            }

            AddForce(force, forceMode);
        }

        public void LaunchCharacter(Vector3 launchVelocity, bool overrideVerticalVelocity = false, bool overrideLateralVelocity = false)
        {
            _forces.Launch(launchVelocity, _velocity, Transform.up, overrideVerticalVelocity,
                overrideLateralVelocity);
        }

        private void UpdateVelocity(Vector3 newVelocity, float deltaTime)
        {
            _velocity = newVelocity;
            _velocity = _forces.Consume(_velocity, deltaTime);

            _velocity = ConstrainVectorToPlane(_velocity);
        }

        public CollisionFlags Move(Vector3 newVelocity, float deltaTime)
        {
            if (!IsFinite(newVelocity))
            {
                ReportRejectedMove($"velocity {newVelocity.ToString("F4")}");
                _velocity = Vector3.zero;
                _forces.Clear();
                return CollisionFlags;
            }

            if (!IsFinite(deltaTime) || deltaTime < 0.0f)
            {
                ReportRejectedMove($"deltaTime {deltaTime}");
                return CollisionFlags;
            }

            UpdateCachedFields();

            ClearCollisionResults();

            UpdateVelocity(newVelocity, deltaTime);

            UpdatePlatformMovement(deltaTime);

            PerformMovement(deltaTime);

            if (IsGrounded || _grounding.HasLanded)
            {
                FindGround(UpdatedPosition, out FindGroundResult foundGround);
                _grounding.FoundGround = foundGround;
            }

            UpdateCurrentGround(ref _grounding.FoundGround);
            _grounding.TickConstraint(deltaTime);

            AdjustGroundHeight();

            UpdateCurrentPlatform();

            ResolveDynamicCollisions();

            SetPositionAndRotation(UpdatedPosition, UpdatedRotation);

            OnCollided();

            if (!WasOnWalkableGround && IsOnGround)
                OnFoundGround();

            return CollisionFlags;
        }

        public CollisionFlags Move(float deltaTime)
        {
            return Move(_velocity, deltaTime);
        }

        public CollisionFlags SimpleMove(Vector3 desiredVelocity, float maxSpeed, float acceleration,
            float deceleration, float friction, float brakingFriction, Vector3 gravity, bool onlyHorizontal, float deltaTime)
        {
            if (IsGrounded)
            {
                Velocity = CalcVelocity(Velocity, desiredVelocity, maxSpeed, acceleration, deceleration, friction,
                    brakingFriction, deltaTime);
            }
            else
            {
                Vector3 worldUp = -1.0f * gravity.normalized;
                Vector3 v = onlyHorizontal ? Velocity.ProjectedOnPlane(worldUp) : Velocity;

                if (onlyHorizontal)
                    desiredVelocity = desiredVelocity.ProjectedOnPlane(worldUp);

                if (IsOnGround)
                {
                    Vector3 actualGroundNormal = GroundNormal;
                    if (desiredVelocity.Dot(actualGroundNormal) < 0.0f)
                    {
                        actualGroundNormal = actualGroundNormal.ProjectedOnPlane(worldUp).normalized;
                        desiredVelocity = desiredVelocity.ProjectedOnPlane(actualGroundNormal);
                    }
                }

                v = CalcVelocity(v, desiredVelocity, maxSpeed, acceleration, deceleration, friction, brakingFriction, deltaTime);

                if (onlyHorizontal)
                    Velocity += Vector3.ProjectOnPlane(v - Velocity, worldUp);
                else
                    Velocity += v - Velocity;

                Velocity += gravity * deltaTime;
            }

            return Move(deltaTime);
        }

        [ContextMenu("Init Collision Layers from Collision Matrix")]
        private void InitCollisionMask()
        {
            int layer = gameObject.layer;

            _collisionLayers = 0;
            for (int i = 0; i < 32; i++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, i))
                    _collisionLayers |= 1 << i;
            }
        }

        public void SetState(Vector3 inPosition, Quaternion inRotation, Vector3 inVelocity,
            bool inConstrainedToGround, float inUnconstrainedTimer, bool inHitGround, bool inIsWalkable)
        {
            _velocity = inVelocity;

            _grounding.Restore(inConstrainedToGround, inUnconstrainedTimer, inHitGround, inIsWalkable);

            SetPositionAndRotation(inPosition, inRotation, IsGrounded);
        }

        private void Reset()
        {
            SetDimensions(0.5f, 2.0f);
            SetPlaneConstraint(EPlaneConstraintType.None, Vector3.zero);

            _slopeLimit = 45.0f;
            _stepOffset = 0.45f;
            _perchOffset = 0.5f;
            _perchAdditionalHeight = 0.4f;

            _triggerInteraction = QueryTriggerInteraction.Ignore;

            _advanced.Reset();

            _grounding.ConstrainToGround = true;

            _pushForceScale = 1.0f;
        }

        private void OnValidate()
        {
            SetDimensions(_radius, _height);
            SetPlaneConstraint(_planeConstraint, _constraintPlaneNormal);

            SlopeLimit = _slopeLimit;
            StepOffset = _stepOffset;
            PerchOffset = _perchOffset;
            PerchAdditionalHeight = _perchAdditionalHeight;

            _advanced.Sanitize();
        }

        private void Awake()
        {
            _advanced.Sanitize();
            CacheComponents();

            SetDimensions(_radius, _height);
            SetPlaneConstraint(_planeConstraint, _constraintPlaneNormal);
        }

        private void OnEnable()
        {
            UpdatedPosition = Transform.position;
            UpdatedRotation = Transform.rotation;

            UpdateCachedFields();
        }

    }
}
