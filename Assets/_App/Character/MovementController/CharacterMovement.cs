using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace _App
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CharacterMovement : MonoBehaviour
    {
        private const float KINDA_SMALL_NUMBER = 0.0001f;
        private const float HEMISPHERE_LIMIT = 0.01f;

        private const int MAX_COLLISION_COUNT = 16;
        private const int MAX_OVERLAP_COUNT = 16;

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
        [SerializeField] private Transform _rootTransform;

        [SerializeField] private Vector3 _rootTransformOffset = new Vector3(0, 0, 0);

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
        private Vector3 _capsuleCenter;
        private Vector3 _capsuleTopCenter;
        private Vector3 _capsuleBottomCenter;

        private readonly HashSet<Rigidbody> _ignoredRigidbodies = new HashSet<Rigidbody>();
        private readonly HashSet<Collider> _ignoredColliders = new HashSet<Collider>();

        private readonly RaycastHit[] _hits = new RaycastHit[MAX_COLLISION_COUNT];
        private readonly Collider[] _overlaps = new Collider[MAX_OVERLAP_COUNT];

        private int _collisionCount;
        private readonly CollisionResult[] _collisionResults = new CollisionResult[MAX_COLLISION_COUNT];

        private float _minSlopeLimit;

        private bool _detectCollisions = true;

        private bool _isConstrainedToGround = true;
        private float _unconstrainedTimer;

        private Vector3 _constraintPlaneNormal;

        private Vector3 _characterUp;

        private Vector3 _transformedCapsuleCenter;
        private Vector3 _transformedCapsuleTopCenter;
        private Vector3 _transformedCapsuleBottomCenter;

        private Vector3 _velocity;
        
        private Vector3 _pendingForces;
        private Vector3 _pendingImpulses;
        private Vector3 _pendingLaunchVelocity;

        private float _pushForceScale = 1.0f;

        private bool _hasLanded;

        private FindGroundResult _foundGround;
        private FindGroundResult _currentGround;

        private Rigidbody _parentPlatform;
        private MovingPlatform _movingPlatform;
        
        private Vector3 _lastVelocityOnMovingPlatform;

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

        public Transform RootTransform
        {
            get => _rootTransform;
            set => _rootTransform = value;
        }

        public Vector3 RootTransformOffset
        {
            get => _rootTransformOffset;
            set => _rootTransformOffset = value;
        }

        public Vector3 Position => GetPosition();

        public Quaternion Rotation
        {
            get => GetRotation();
            set => SetRotation(value);
        }
        
        public Vector3 WorldCenter => Position + Rotation * _capsuleCenter;

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
        
        
        public CollisionFlags CollisionFlags { get; private set; }

        public bool IsConstrainedToPlane => _planeConstraint != EPlaneConstraintType.None;

        public bool ConstrainToGround
        {
            get => _isConstrainedToGround;
            set => _isConstrainedToGround = value;
        }

        public bool DetectCollisions => _detectCollisions;
        public bool IsConstrainedToGround => _isConstrainedToGround && _unconstrainedTimer == 0.0f;

        public bool IsGroundConstraintPaused => _isConstrainedToGround && _unconstrainedTimer > 0.0f;

        public float UnconstrainedTimer => _unconstrainedTimer;

        public bool WasOnGround { get; private set; }

        public bool IsOnGround => _currentGround.HitGround;
        
        public bool WasOnWalkableGround { get; private set; }

        public bool IsOnWalkableGround => _currentGround.IsWalkableGround;

        public bool WasGrounded { get; private set; }

        public bool IsGrounded => IsOnWalkableGround && IsConstrainedToGround;

        public float GroundDistance => _currentGround.GroundDistance;

        public Vector3 GroundPoint => _currentGround.Point;

        public Vector3 GroundNormal => _currentGround.Normal;

        public Vector3 GroundSurfaceNormal => _currentGround.SurfaceNormal;

        public Collider GroundCollider => _currentGround.Collider;

        public Transform GroundTransform => _currentGround.Transform;

        public Rigidbody GroundRigidbody => _currentGround.Rigidbody;

        public FindGroundResult CurrentGround => _currentGround;

        public MovingPlatform MovingPlatform => _movingPlatform;
        
        public Vector3 LandedVelocity { get; private set; }

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

            for (int i = 0; i < _collisionCount; i++)
                Collided.Invoke(ref _collisionResults[i]);
        }

        private void OnFoundGround()
        {
            FoundGround?.Invoke(ref _currentGround);
        }

        private Vector3 FindOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            const float thickness = (CONTACT_OFFSET - SWEEP_EDGE_REJECT_DISTANCE) * 0.5f;

            Vector3 result = inHit.normal;
            
            Vector3 rayOrigin = inHit.point - sweepDirDenorm;
            
            float rayLength = sweepDirDenorm.magnitude * 2f;
            Vector3 rayDirection = sweepDirDenorm / sweepDirDenorm.magnitude;

            if (Raycast(rayOrigin, rayDirection, rayLength, _collisionLayers, out RaycastHit hitResult, thickness))
                result = hitResult.normal;

            return result;
        }

        private static Vector3 FindBoxOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            Transform localToWorld = inHit.transform;

            Vector3 localContactNormal = localToWorld.InverseTransformDirection(inHit.normal);
            Vector3 localTraceDirDenorm = localToWorld.InverseTransformDirection(sweepDirDenorm);

            Vector3 bestLocalNormal = localContactNormal;
            float bestOpposingDot = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (localContactNormal[i] > KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = 1.0f;
                    }
                }
                else if (localContactNormal[i] < -KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = -localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = -1.0f;
                    }
                }
            }

            return localToWorld.TransformDirection(bestLocalNormal);
        }

        private static Vector3 FindBoxOpposingNormal(Vector3 displacement, Vector3 hitNormal, Transform hitTransform)
        {
            Transform localToWorld = hitTransform;

            Vector3 localContactNormal = localToWorld.InverseTransformDirection(hitNormal);
            Vector3 localTraceDirDenorm = localToWorld.InverseTransformDirection(displacement);

            Vector3 bestLocalNormal = localContactNormal;
            float bestOpposingDot = float.MaxValue;

            for (int i = 0; i < 3; i++)
            {
                if (localContactNormal[i] > KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = 1.0f;
                    }
                }
                else if (localContactNormal[i] < -KINDA_SMALL_NUMBER)
                {
                    float traceDotFaceNormal = -localTraceDirDenorm[i];
                    if (traceDotFaceNormal < bestOpposingDot)
                    {
                        bestOpposingDot = traceDotFaceNormal;
                        bestLocalNormal = Vector3.zero;
                        bestLocalNormal[i] = -1.0f;
                    }
                }
            }

            return localToWorld.TransformDirection(bestLocalNormal);
        }

        private static Vector3 FindTerrainOpposingNormal(ref RaycastHit inHit)
        {
            TerrainCollider terrainCollider = inHit.collider as TerrainCollider;

            if (terrainCollider != null)
            {
                Vector3 localPoint = terrainCollider.transform.InverseTransformPoint(inHit.point);

                TerrainData terrainData = terrainCollider.terrainData;

                Vector3 interpolatedNormal = terrainData.GetInterpolatedNormal(localPoint.x / terrainData.size.x,
                    localPoint.z / terrainData.size.z);

                return interpolatedNormal;
            }

            return inHit.normal;
        }
        
        private Vector3 FindGeomOpposingNormal(Vector3 sweepDirDenorm, ref RaycastHit inHit)
        {
            if (inHit.collider is SphereCollider _ || inHit.collider is CapsuleCollider _)
            {
                return inHit.normal;
            }
            
            if (inHit.collider is BoxCollider _)
            {
                return FindBoxOpposingNormal(sweepDirDenorm, ref inHit);
            }
            
            if (inHit.collider is MeshCollider nonConvexMeshCollider && !nonConvexMeshCollider.convex)
            {
                Mesh sharedMesh = nonConvexMeshCollider.sharedMesh;
                if (sharedMesh && sharedMesh.isReadable && !_advanced.UseFastGeomNormalPath)
                    return MeshUtility.FindMeshOpposingNormal(sharedMesh, ref inHit);
                
                return FindOpposingNormal(sweepDirDenorm, ref inHit);
            }
            
            if (inHit.collider is MeshCollider convexMeshCollider && convexMeshCollider.convex)
            {
                return FindOpposingNormal(sweepDirDenorm, ref inHit);
            }

            
            if (inHit.collider is TerrainCollider && !_advanced.UseFastGeomNormalPath)
            {
                return FindTerrainOpposingNormal(ref inHit);
            }
            
            return inHit.normal;
        }

        
        public static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        public static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
        
        private static Vector3 ApplyVelocityBraking(Vector3 currentVelocity, float friction, float deceleration, float deltaTime)
        {
            bool isZeroFriction = friction == 0.0f;
            bool isZeroBraking = deceleration == 0.0f;

            if (isZeroFriction && isZeroBraking)
                return currentVelocity;
            
            Vector3 oldVel = currentVelocity;
            Vector3 revAcceleration = isZeroBraking ? Vector3.zero : -deceleration * currentVelocity.normalized;
            
            currentVelocity += (-friction * currentVelocity + revAcceleration) * deltaTime;
            
            if (Vector3.Dot(currentVelocity, oldVel) <= 0.0f)
                return Vector3.zero;
            
            float sqrSpeed = currentVelocity.sqrMagnitude;
            if (sqrSpeed <= 0.00001f || !isZeroBraking && sqrSpeed <= 0.01f)
                return Vector3.zero;

            return currentVelocity;
        }

        private static float ComputeAnalogInputModifier(Vector3 desiredVelocity, float maxSpeed)
        {
            if (maxSpeed > 0.0f && desiredVelocity.sqrMagnitude > 0.0f)
                return Mathf.Clamp01(desiredVelocity.magnitude / maxSpeed);

            return 0.0f;
        }

        private static Vector3 CalcVelocity(Vector3 currentVelocity, Vector3 desiredVelocity, float maxSpeed,
            float acceleration, float deceleration, float friction, float brakingFriction, float deltaTime)
        {
            float desiredSpeed = desiredVelocity.magnitude;
            Vector3 desiredMoveDirection = desiredSpeed > 0.0f ? desiredVelocity / desiredSpeed : Vector3.zero;
            
            float analogInputModifier = ComputeAnalogInputModifier(desiredVelocity, maxSpeed);
            Vector3 requestedAcceleration = acceleration * analogInputModifier * desiredMoveDirection;
            
            float actualMaxSpeed = Mathf.Max(0.0f, maxSpeed * analogInputModifier);

            bool isZeroAcceleration = requestedAcceleration.IsZero();
            bool isVelocityOverMax = currentVelocity.IsExceeding(actualMaxSpeed);

            if (isZeroAcceleration || isVelocityOverMax)
            {
                Vector3 oldVelocity = currentVelocity;
                
                currentVelocity = ApplyVelocityBraking(currentVelocity, brakingFriction, deceleration, deltaTime);
                
                if (isVelocityOverMax && currentVelocity.sqrMagnitude < actualMaxSpeed.Square() &&
                    Vector3.Dot(requestedAcceleration, oldVelocity) > 0.0f)
                    currentVelocity = oldVelocity.normalized * actualMaxSpeed;
            }
            else
            {
                currentVelocity -= (currentVelocity - desiredMoveDirection * currentVelocity.magnitude) * Mathf.Min(friction * deltaTime, 1.0f);
            }
            
            if (!isZeroAcceleration)
            {
                float newMaxSpeed = currentVelocity.IsExceeding(actualMaxSpeed) ? currentVelocity.magnitude : actualMaxSpeed;

                currentVelocity += requestedAcceleration * deltaTime;
                currentVelocity = currentVelocity.ClampedTo(newMaxSpeed);
            }
            
            return currentVelocity;
        }

        private static Vector3 GetRigidbodyVelocity(Rigidbody rigidbody, Vector3 worldPoint)
        {
            if (rigidbody == null)
                return Vector3.zero;

            return rigidbody.TryGetComponent(out CharacterMovement controller)
                ? controller.Velocity
                : rigidbody.GetPointVelocity(worldPoint);
        }

        private static bool IsWalkable(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.Walkable) != 0;
        }

        private static bool IsNotWalkable(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.NotWalkable) != 0;
        }

        private static bool CanPerchOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanPerchOn) != 0;
        }

        private static bool CanNotPerchOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotPerchOn) != 0;
        }

        private static bool CanStepOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanStepOn) != 0;
        }

        private static bool CanNotStepOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotStepOn) != 0;
        }

        private static bool CanRideOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanRideOn) != 0;
        }

        private static bool CanNotRideOn(ECollisionBehaviour behaviourFlags)
        {
            return (behaviourFlags & ECollisionBehaviour.CanNotRideOn) != 0;
        }

        private static void MakeCapsule(float radius, float height, out Vector3 center, out Vector3 bottomCenter, out Vector3 topCenter)
        {
            radius = Mathf.Max(radius, 0.0f);
            height = Mathf.Max(height, radius * 2.0f);

            center = height * 0.5f * Vector3.up;

            float sideHeight = height - radius * 2.0f;

            bottomCenter = center - sideHeight * 0.5f * Vector3.up;
            topCenter = center + sideHeight * 0.5f * Vector3.up;
        }

        public void SetDimensions(float characterRadius, float characterHeight)
        {
            _radius = Mathf.Max(characterRadius, 0.0f);
            _height = Mathf.Max(characterHeight, characterRadius * 2.0f);

            MakeCapsule(_radius, _height, out _capsuleCenter, out _capsuleBottomCenter, out _capsuleTopCenter);

#if UNITY_EDITOR
            if (_capsuleCollider == null)
                _capsuleCollider = GetComponent<CapsuleCollider>();
#endif

            if (_capsuleCollider)
            {
                _capsuleCollider.radius = _radius;
                _capsuleCollider.height = _height;
                _capsuleCollider.center = _capsuleCenter;
            }
        }

        public void SetHeight(float characterHeight)
        {
            _height = Mathf.Max(characterHeight, _radius * 2.0f);

            MakeCapsule(_radius, _height, out _capsuleCenter, out _capsuleBottomCenter, out _capsuleTopCenter);

#if UNITY_EDITOR
            if (_capsuleCollider == null)
                _capsuleCollider = GetComponent<CapsuleCollider>();
#endif

            if (_capsuleCollider)
            {
                _capsuleCollider.height = _height;
                _capsuleCollider.center = _capsuleCenter;
            }
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

        private void ResetCollisionFlags()
        {
            CollisionFlags = CollisionFlags.None;
        }

        private void UpdateCollisionFlags(EHitLocation hitLocation)
        {
            CollisionFlags |= (CollisionFlags) hitLocation;
        }

        private EHitLocation ComputeHitLocation(Vector3 inNormal)
        {
            float verticalComponent = inNormal.Dot(_characterUp);

            if (verticalComponent > HEMISPHERE_LIMIT)
                return EHitLocation.Below;

            return verticalComponent < -HEMISPHERE_LIMIT ? EHitLocation.Above : EHitLocation.Sides;
        }
        
        private bool IsWalkable(Collider inCollider, Vector3 inNormal)
        {
            if (ComputeHitLocation(inNormal) != EHitLocation.Below)
                return false;
            
            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(inCollider);

                if (IsWalkable(collisionBehaviour))
                    return Vector3.Dot(inNormal, _characterUp) > MAX_WALKABLE_SLOPE_LIMIT;

                if (IsNotWalkable(collisionBehaviour))
                    return Vector3.Dot(inNormal, _characterUp) > MIN_WALKABLE_SLOPE_LIMIT;
            }
            
            float actualSlopeLimit = _minSlopeLimit;

            if (_slopeLimitOverride && inCollider.TryGetComponent(out SlopeLimitBehaviour slopeLimitOverrideComponent))
            {
                switch (slopeLimitOverrideComponent.WalkableSlopeBehaviour)
                {
                    case ESlopeBehaviourType.Walkable:
                        actualSlopeLimit = MAX_WALKABLE_SLOPE_LIMIT;
                        break;

                    case ESlopeBehaviourType.NotWalkable:
                        actualSlopeLimit = MIN_WALKABLE_SLOPE_LIMIT;
                        break;

                    case ESlopeBehaviourType.Override:
                        actualSlopeLimit = slopeLimitOverrideComponent.SlopeLimitCos;
                        break;

                    case ESlopeBehaviourType.Default:
                        break;
                }
            }
            
            return Vector3.Dot(inNormal, _characterUp) > actualSlopeLimit;
        }

        private Vector3 ComputeBlockingNormal(Vector3 inNormal, bool isWalkable)
        {
            if ((IsGrounded || _hasLanded) && !isWalkable)
            {
                Vector3 actualGroundNormal = _hasLanded ? _foundGround.Normal : _currentGround.Normal;

                Vector3 forward = actualGroundNormal.PerpendicularTo(inNormal);
                Vector3 blockingNormal = forward.PerpendicularTo(_characterUp);

                if (Vector3.Dot(blockingNormal, inNormal) < 0.0f)
                    blockingNormal = -blockingNormal;

                if (!blockingNormal.IsZero())
                    inNormal = blockingNormal;

                return inNormal;
            }

            return inNormal;
        }

        private bool ShouldFilter(Collider otherCollider)
        {
            if (otherCollider == _capsuleCollider || otherCollider.attachedRigidbody == Rigidbody)
                return true;
            
            if (_ignoredColliders.Contains(otherCollider))
                return true;

            Rigidbody attachedRigidbody = otherCollider.attachedRigidbody;
            if (attachedRigidbody && _ignoredRigidbodies.Contains(attachedRigidbody))
                return true;
            
            return ColliderFilter != null && ColliderFilter.Invoke(otherCollider);
        }

        public void CapsuleIgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            if (otherCollider == null)
                return;

            Physics.IgnoreCollision(_capsuleCollider, otherCollider, ignore);
        }

        public void IgnoreCollision(Collider otherCollider, bool ignore = true)
        {
            if (otherCollider == null)
                return;

            if (ignore)
                _ignoredColliders.Add(otherCollider);
            else
                _ignoredColliders.Remove(otherCollider);
        }

        public void IgnoreCollision(Rigidbody otherRigidbody, bool ignore = true)
        {
            if (otherRigidbody == null)
                return;

            if (ignore)
                _ignoredRigidbodies.Add(otherRigidbody);
            else
                _ignoredRigidbodies.Remove(otherRigidbody);
        }

        private void ClearCollisionResults()
        {
            _collisionCount = 0;
        }

        private void AddCollisionResult(ref CollisionResult collisionResult)
        {
            UpdateCollisionFlags(collisionResult.HitLocation);

            if (collisionResult.Rigidbody)
            {
                if (collisionResult.Rigidbody == _movingPlatform.Platform)
                    return;
                
                for (int i = 0; i < _collisionCount; i++)
                {
                    if (collisionResult.Rigidbody == _collisionResults[i].Rigidbody)
                        return;
                }
            }

            if (_collisionCount < MAX_COLLISION_COUNT)
                _collisionResults[_collisionCount++] = collisionResult;
        }

        public int GetCollisionCount()
        {
            return _collisionCount;
        }

        public CollisionResult GetCollisionResult(int index)
        {
            return _collisionResults[index];
        }

        private bool ComputeInflatedMTD(Vector3 characterPosition, Quaternion characterRotation, float mtdInflation,
            Collider hitCollider, Transform hitTransform, out Vector3 mtdDirection, out float mtdDistance)
        {
            mtdDirection = Vector3.zero;
            mtdDistance = 0.0f;

            _capsuleCollider.radius = _radius + mtdInflation * 1.0f;
            _capsuleCollider.height = _height + mtdInflation * 2.0f;

            bool mtdResult = Physics.ComputePenetration(_capsuleCollider, characterPosition, characterRotation,
                hitCollider, hitTransform.position, hitTransform.rotation, out Vector3 recoverDirection, out float recoverDistance);

            if (mtdResult)
            {
                if (IsFinite(recoverDirection))
                {
                    mtdDirection = recoverDirection;
                    mtdDistance = Mathf.Max(Mathf.Abs(recoverDistance) - mtdInflation, 0.0f) + KINDA_SMALL_NUMBER;
                }
                else
                {
                    Debug.LogWarning("Warning: ComputeInflatedMTD_Internal: MTD returned NaN " + recoverDirection.ToString("F4"));
                }
            }

            _capsuleCollider.radius = _radius;
            _capsuleCollider.height = _height;

            return mtdResult;
        }

        private bool ComputeMTD(Vector3 characterPosition, Quaternion characterRotation, Collider hitCollider, Transform hitTransform, out Vector3 mtdDirection, out float mtdDistance)
        {
            const float smallMtdInflation = 0.0025f;
            const float largeMtdInflation = 0.0175f;

            if (ComputeInflatedMTD(characterPosition, characterRotation, smallMtdInflation, hitCollider, hitTransform, out mtdDirection, out mtdDistance) ||
                ComputeInflatedMTD(characterPosition, characterRotation, largeMtdInflation, hitCollider, hitTransform, out mtdDirection, out mtdDistance))
            {
                return true;
            }
            
            return false;
        }

        private void ResolveOverlaps(EDepenetrationBehaviour depenetrationBehaviour = EDepenetrationBehaviour.IgnoreNone)
        {
            if (!DetectCollisions)
                return;

            bool ignoreStatic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreStatic) != 0;
            bool ignoreDynamic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreDynamic) != 0;
            bool ignoreKinematic = (depenetrationBehaviour & EDepenetrationBehaviour.IgnoreKinematic) != 0;

            for (int i = 0; i < _advanced.MaxDepenetrationIterations; i++)
            {
                Vector3 top = UpdatedPosition + _transformedCapsuleTopCenter;
                Vector3 bottom = UpdatedPosition + _transformedCapsuleBottomCenter;

                int overlapCount = Physics.OverlapCapsuleNonAlloc(bottom, top, _radius, _overlaps, _collisionLayers, TriggerInteraction);
                if (overlapCount == 0)
                    break;

                for (int j = 0; j < overlapCount; j++)
                {
                    Collider overlappedCollider = _overlaps[j];

                    if (ShouldFilter(overlappedCollider))
                        continue;

                    Rigidbody attachedRigidbody = overlappedCollider.attachedRigidbody;

                    if (ignoreStatic && attachedRigidbody == null)
                        continue;

                    if (attachedRigidbody)
                    {
                        bool isKinematic = attachedRigidbody.isKinematic;

                        if (ignoreKinematic && isKinematic)
                            continue;

                        if (ignoreDynamic && !isKinematic)
                            continue;
                    }

                    if (ComputeMTD(UpdatedPosition, UpdatedRotation, overlappedCollider, overlappedCollider.transform, out Vector3 recoverDirection, out float recoverDistance))
                    {
                        recoverDirection = ConstrainDirectionToPlane(recoverDirection);

                        EHitLocation hitLocation = ComputeHitLocation(recoverDirection);

                        bool isWalkable = IsWalkable(overlappedCollider, recoverDirection);

                        Vector3 impactNormal = ComputeBlockingNormal(recoverDirection, isWalkable);

                        UpdatedPosition += impactNormal * (recoverDistance + PENETRATION_OFFSET);

                        if (_collisionCount < MAX_COLLISION_COUNT)
                        {
                            Vector3 point;

                            if (hitLocation == EHitLocation.Above)
                                point = UpdatedPosition + _transformedCapsuleTopCenter - recoverDirection * _radius;
                            else if (hitLocation == EHitLocation.Below)
                                point = UpdatedPosition + _transformedCapsuleBottomCenter - recoverDirection * _radius;
                            else
                                point = UpdatedPosition + _transformedCapsuleCenter - recoverDirection * _radius;

                            CollisionResult collisionResult = new CollisionResult
                            {
                                StartPenetrating = true,

                                HitLocation = hitLocation,
                                IsWalkable = isWalkable,

                                Position = UpdatedPosition,

                                Velocity = _velocity,
                                OtherVelocity = GetRigidbodyVelocity(attachedRigidbody, point),

                                Point = point,
                                Normal = impactNormal,

                                SurfaceNormal = impactNormal,

                                Collider = overlappedCollider
                            };

                            AddCollisionResult(ref collisionResult);
                        }
                    }
                }
            }
        }

        public int OverlapTest(Vector3 characterPosition, Quaternion characterRotation, float testRadius,
            float testHeight, int layerMask, Collider[] results, QueryTriggerInteraction queryTriggerInteraction)
        {
            MakeCapsule(testRadius, testHeight, out Vector3 _, out Vector3 bottomCenter, out Vector3 topCenter);

            Vector3 top = characterPosition + characterRotation * topCenter;
            Vector3 bottom = characterPosition + characterRotation * bottomCenter;

            int rawOverlapCount =
                Physics.OverlapCapsuleNonAlloc(bottom, top, testRadius, results, layerMask, queryTriggerInteraction);

            if (rawOverlapCount == 0)
                return 0;

            int filteredOverlapCount = rawOverlapCount;

            for (int i = 0; i < rawOverlapCount; i++)
            {
                Collider overlappedCollider = results[i];

                if (ShouldFilter(overlappedCollider))
                {
                    if (i < --filteredOverlapCount)
                        results[i] = results[filteredOverlapCount];
                }
            }

            return filteredOverlapCount;
        }

        public Collider[] OverlapTest(Vector3 characterPosition, Quaternion characterRotation, float testRadius,
            float testHeight, int layerMask, QueryTriggerInteraction queryTriggerInteraction, out int overlapCount)
        {
            overlapCount = OverlapTest(characterPosition, characterRotation, testRadius, testHeight, layerMask,
                _overlaps, queryTriggerInteraction);

            return _overlaps;
        }

        public Collider[] OverlapTest(int layerMask, QueryTriggerInteraction queryTriggerInteraction,
            out int overlapCount)
        {
            overlapCount = 
                OverlapTest(Position, Rotation, Radius, Height, layerMask, _overlaps, queryTriggerInteraction);

            return _overlaps;
        }

        public bool CheckCapsule()
        {
            IgnoreCollision(_movingPlatform.Platform);

            int overlapCount =
                OverlapTest(Position, Rotation, Radius, Height, CollisionLayers, _overlaps, TriggerInteraction);

            IgnoreCollision(_movingPlatform.Platform, false);

            return overlapCount > 0;
        }

        public bool CheckHeight(float testHeight)
        {
            IgnoreCollision(_movingPlatform.Platform);

            int overlapCount =
                OverlapTest(Position, Rotation, Radius, testHeight, CollisionLayers, _overlaps, TriggerInteraction);

            IgnoreCollision(_movingPlatform.Platform, false);

            return overlapCount > 0;
        }
        
        public bool IsWithinEdgeTolerance(Vector3 characterPosition, Vector3 inPoint, float testRadius)
        {
            float distFromCenterSq = (inPoint - characterPosition).ProjectedOnPlane(_characterUp).sqrMagnitude;

            float reducedRadius = Mathf.Max(SWEEP_EDGE_REJECT_DISTANCE + KINDA_SMALL_NUMBER,
                testRadius - SWEEP_EDGE_REJECT_DISTANCE);

            return distFromCenterSq < reducedRadius * reducedRadius;
        }

        private bool ShouldCheckForValidLandingSpot(ref CollisionResult inCollision)
        {
            if (inCollision.HitLocation == EHitLocation.Below && inCollision.Normal != inCollision.SurfaceNormal)
            {
                if (IsWithinEdgeTolerance(UpdatedPosition, inCollision.Point, _radius))
                    return true;
            }

            return false;
        }

        private bool IsValidLandingSpot(Vector3 characterPosition, ref CollisionResult inCollision)
        {
            if (!inCollision.IsWalkable)
                return false;
            
            if (inCollision.HitLocation != EHitLocation.Below)
                return false;
            
            if (!IsWithinEdgeTolerance(characterPosition, inCollision.Point, _radius))
            {
                inCollision.IsWalkable = false;

                return false;
            }

            FindGround(characterPosition, out FindGroundResult groundResult);
            {
                inCollision.IsWalkable = groundResult.IsWalkableGround;

                if (inCollision.IsWalkable)
                {
                    _foundGround = groundResult;

                    return true;
                }
            }
            
            return false;
        }
        
        public bool Raycast(Vector3 origin, Vector3 direction, float distance, int layerMask, out RaycastHit hitResult,
            float thickness = 0.0f)
        {
            hitResult = default;

            int rawHitCount = thickness == 0.0f
                ? Physics.RaycastNonAlloc(origin, direction, _hits, distance, layerMask, TriggerInteraction)
                : Physics.SphereCastNonAlloc(origin - direction * thickness, thickness, direction, _hits, distance, layerMask, TriggerInteraction);

            if (rawHitCount == 0)
                return false;
            
            float closestDistance = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (hit.distance <= 0.0f || ShouldFilter(hit.collider))
                    continue;

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex != -1)
            {
                hitResult = _hits[hitIndex];
                return true;
            }

            return false;
        }
        
        private bool CapsuleCast(Vector3 characterPosition, float castRadius, Vector3 castDirection, float castDistance,
            int layerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = false;

            Vector3 top = characterPosition + _transformedCapsuleTopCenter;
            Vector3 bottom = characterPosition + _transformedCapsuleBottomCenter;

            int rawHitCount = Physics.CapsuleCastNonAlloc(bottom, top, castRadius, castDirection, _hits, castDistance,
                layerMask, TriggerInteraction);

            if (rawHitCount == 0)
                return false;

            float closestDistance = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                if (hit.distance <= 0.0f)
                    startPenetrating = true;
                else if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex != -1)
            {
                hitResult = _hits[hitIndex];
                return true;
            }

            return false;
        }        

        private static void SortArray(RaycastHit[] array, int length)
        {
            for (int i = 1; i < length; i++)
            {
                RaycastHit key = array[i];
                int flag = 0;

                for (int j = i - 1; j >= 0 && flag != 1;)
                {
                    if (key.distance < array[j].distance)
                    {
                        array[j + 1] = array[j];
                        j--;
                        array[j + 1] = key;
                    }
                    else flag = 1;
                }
            }
        }
        
        private bool CapsuleCastEx(Vector3 characterPosition, float castRadius, Vector3 castDirection, float castDistance, int layerMask,
            out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, bool ignoreNonBlockingOverlaps = false)
        {
            hitResult = default;

            startPenetrating = default;
            recoverDirection = default;
            recoverDistance = default;

            Vector3 top = characterPosition + _transformedCapsuleTopCenter;
            Vector3 bottom = characterPosition + _transformedCapsuleBottomCenter;

            int rawHitCount =
                Physics.CapsuleCastNonAlloc(bottom, top, castRadius, castDirection, _hits, castDistance, layerMask, TriggerInteraction);

            if (rawHitCount == 0)
                return false;

            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                bool isOverlapping = hit.distance <= 0.0f;
                if (isOverlapping)
                {
                    if (ComputeMTD(characterPosition, UpdatedRotation, hit.collider, hit.collider.transform, out Vector3 mtdDirection, out float mtdDistance))
                    {
                        mtdDirection = ConstrainDirectionToPlane(mtdDirection);

                        EHitLocation hitLocation = ComputeHitLocation(mtdDirection);

                        Vector3 point;
                        if (hitLocation == EHitLocation.Above)
                            point = characterPosition + _transformedCapsuleTopCenter - mtdDirection * _radius;
                        else if (hitLocation == EHitLocation.Below)
                            point = characterPosition + _transformedCapsuleBottomCenter - mtdDirection * _radius;
                        else
                            point = characterPosition + _transformedCapsuleCenter - mtdDirection * _radius;

                        Vector3 impactNormal = ComputeBlockingNormal(mtdDirection, IsWalkable(hit.collider, mtdDirection));

                        hit.point = point;
                        hit.normal = impactNormal;
                        hit.distance = -mtdDistance;
                    }
                }
            }
            
            if (rawHitCount > 2)
            {
                SortArray(_hits, rawHitCount);
            }

            float mostOpposingDot = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                bool isOverlapping = hit.distance <= 0.0f && !hit.point.IsZero();
                if (isOverlapping)
                {
                    float movementDotNormal = Vector3.Dot(castDirection, hit.normal);

                    if (ignoreNonBlockingOverlaps)
                    {
                        bool isMovingOut = movementDotNormal > 0.0f;
                        if (isMovingOut)
                            continue;
                    }
                    
                    if (movementDotNormal < mostOpposingDot)
                    {
                        mostOpposingDot = movementDotNormal;
                        hitIndex = i;
                    }
                }
                else if (hitIndex == -1)
                {
                    hitIndex = i;
                    break;
                }
            }

            if (hitIndex >= 0)
            {
                hitResult = _hits[hitIndex];

                if (hitResult.distance <= 0.0f)
                {
                    startPenetrating = true;
                    recoverDirection = hitResult.normal;
                    recoverDistance = Mathf.Abs(hitResult.distance);
                }

                return true;
            }

            return false;
        }

        private bool SweepTest(Vector3 sweepOrigin, float sweepRadius, Vector3 sweepDirection, float sweepDistance,
            int sweepLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;

            bool innerCapsuleHit =
                CapsuleCast(sweepOrigin, sweepRadius, sweepDirection, sweepDistance + sweepRadius, sweepLayerMask,
                    out RaycastHit innerCapsuleHitResult, out startPenetrating) && innerCapsuleHitResult.distance <= sweepDistance;

            float outerCapsuleRadius = sweepRadius + CONTACT_OFFSET;

            bool outerCapsuleHit =
                CapsuleCast(sweepOrigin, outerCapsuleRadius, sweepDirection, sweepDistance + outerCapsuleRadius,
                    sweepLayerMask, out RaycastHit outerCapsuleHitResult, out _) && outerCapsuleHitResult.distance <= sweepDistance;

            bool foundBlockingHit = innerCapsuleHit || outerCapsuleHit;
            if (!foundBlockingHit)
                return false;

            if (!outerCapsuleHit)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else if (innerCapsuleHit && innerCapsuleHitResult.distance < outerCapsuleHitResult.distance)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else
            {
                hitResult = outerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);
            }
            
            return true;
        }

        private bool SweepTestEx(Vector3 sweepOrigin, float sweepRadius, Vector3 sweepDirection, float sweepDistance, int sweepLayerMask,
            out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, bool ignoreBlockingOverlaps = false)
        {
            hitResult = default;

            bool innerCapsuleHit =
                CapsuleCastEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance + sweepRadius, sweepLayerMask,
                out RaycastHit innerCapsuleHitResult, out startPenetrating, out recoverDirection, out recoverDistance, ignoreBlockingOverlaps) && innerCapsuleHitResult.distance <= sweepDistance;

            if (innerCapsuleHit && startPenetrating)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);

                return true;
            }

            float outerCapsuleRadius = sweepRadius + CONTACT_OFFSET;

            bool outerCapsuleHit =
                CapsuleCast(sweepOrigin, outerCapsuleRadius, sweepDirection, sweepDistance + outerCapsuleRadius, sweepLayerMask,
                out RaycastHit outerCapsuleHitResult, out _) && outerCapsuleHitResult.distance <= sweepDistance;

            bool foundBlockingHit = innerCapsuleHit || outerCapsuleHit;
            if (!foundBlockingHit)
                return false;

            if (!outerCapsuleHit)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else if (innerCapsuleHit && innerCapsuleHitResult.distance < outerCapsuleHitResult.distance)
            {
                hitResult = innerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - CONTACT_OFFSET);
            }
            else
            {
                hitResult = outerCapsuleHitResult;
                hitResult.distance = Mathf.Max(0.0f, hitResult.distance - SMALL_CONTACT_OFFSET);
            }

            return true;
        }

        private bool ResolvePenetration(Vector3 displacement, Vector3 proposedAdjustment)
        {
            Vector3 adjustment = ConstrainVectorToPlane(proposedAdjustment);
            if (adjustment.IsZero())
                return false;

            const float overlapInflation = 0.001f;

            if (!(OverlapTest(UpdatedPosition + adjustment, UpdatedRotation, _radius + overlapInflation, _height, _collisionLayers, _overlaps, TriggerInteraction) > 0))
            {
                UpdatedPosition += adjustment;

                return true;
            }

            Vector3 lastPosition = UpdatedPosition;
            
            bool hit = CapsuleCastEx(UpdatedPosition, _radius, adjustment.normalized, adjustment.magnitude, _collisionLayers,
                out RaycastHit sweepHitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance, true);

            if (!hit)
                UpdatedPosition += adjustment;
            else
                UpdatedPosition += adjustment.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);


            bool moved = UpdatedPosition != lastPosition;
            if (!moved && startPenetrating)
            {
                Vector3 secondMtd = recoverDirection * (recoverDistance + CONTACT_OFFSET + PENETRATION_OFFSET);
                Vector3 combinedMtd = adjustment + secondMtd;
                    
                if (secondMtd != adjustment && !combinedMtd.IsZero())
                {
                    lastPosition = UpdatedPosition;
                        
                    hit = CapsuleCastEx(UpdatedPosition, _radius, combinedMtd.normalized, combinedMtd.magnitude, 
                        _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                    if (!hit)
                        UpdatedPosition += combinedMtd;
                    else
                        UpdatedPosition += combinedMtd.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                    moved = UpdatedPosition != lastPosition;
                }
            }
            
            if (!moved)
            {
                Vector3 moveDelta = ConstrainVectorToPlane(displacement);
                if (!moveDelta.IsZero())
                {
                    lastPosition = UpdatedPosition;

                    Vector3 newAdjustment = adjustment + moveDelta;
                    hit = CapsuleCastEx(UpdatedPosition, _radius, newAdjustment.normalized, newAdjustment.magnitude, 
                        _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                    if (!hit)
                        UpdatedPosition += newAdjustment;
                    else
                        UpdatedPosition += newAdjustment.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                    moved = UpdatedPosition != lastPosition;
                    
                    if (!moved && Vector3.Dot(moveDelta, adjustment) > 0.0f)
                    {
                        lastPosition = UpdatedPosition;

                        hit = CapsuleCastEx(UpdatedPosition, _radius, moveDelta.normalized, moveDelta.magnitude,
                            _collisionLayers, out sweepHitResult, out _, out _, out _, true);

                        if (!hit)
                            UpdatedPosition += moveDelta;
                        else
                            UpdatedPosition += moveDelta.normalized * Mathf.Max(sweepHitResult.distance - SMALL_CONTACT_OFFSET, 0.0f);

                        moved = UpdatedPosition != lastPosition;
                    }
                }
            }

            return moved;
        }

        private bool MovementSweepTest(Vector3 characterPosition, Vector3 inVelocity, Vector3 displacement,
            out CollisionResult collisionResult)
        {
            collisionResult = default;

            Vector3 sweepOrigin = characterPosition;
            Vector3 sweepDirection = displacement.normalized;

            float sweepRadius = _radius;
            float sweepDistance = displacement.magnitude;

            int sweepLayerMask = _collisionLayers;
            
            bool hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask, 
                out RaycastHit hitResult, out bool startPenetrating, out Vector3 recoverDirection, out float recoverDistance);

            if (startPenetrating)
            {
                Vector3 requestedAdjustment = recoverDirection * (recoverDistance + CONTACT_OFFSET + PENETRATION_OFFSET);

                if (ResolvePenetration(displacement, requestedAdjustment))
                {
                    sweepOrigin = UpdatedPosition;
                    hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                        out hitResult, out startPenetrating, out _, out _);
                }
            }

            if (!hit)
                return false;

            EHitLocation hitLocation = ComputeHitLocation(hitResult.normal);

            Vector3 displacementToHit = sweepDirection * hitResult.distance;
            Vector3 remainingDisplacement = displacement - displacementToHit;

            Vector3 hitPosition = sweepOrigin + displacementToHit;

            Vector3 surfaceNormal = hitResult.normal;

            bool isWalkable = false;
            bool hitGround = hitLocation == EHitLocation.Below;
            
            if (hitGround)
            {
                surfaceNormal = FindGeomOpposingNormal(displacement, ref hitResult);

                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
            }

            collisionResult = new CollisionResult
            {
                StartPenetrating = startPenetrating,

                HitLocation = hitLocation,
                IsWalkable = isWalkable,

                Position = hitPosition,

                Velocity = inVelocity,
                OtherVelocity = GetRigidbodyVelocity(hitResult.rigidbody, hitResult.point),

                Point = hitResult.point,
                Normal = hitResult.normal,

                SurfaceNormal = surfaceNormal,

                DisplacementToHit = displacementToHit,
                RemainingDisplacement = remainingDisplacement,

                Collider = hitResult.collider,

                HitResult = hitResult
            };

            return true;
        }

        public bool MovementSweepTest(Vector3 characterPosition, Vector3 sweepDirection, float sweepDistance,
            out CollisionResult collisionResult)
        {
            return MovementSweepTest(characterPosition, Velocity, sweepDirection * sweepDistance, out collisionResult);
        }

        private Vector3 HandleSlopeBoosting(Vector3 slideResult, Vector3 displacement, Vector3 inNormal)
        {
            Vector3 result = slideResult;

            float yResult = Vector3.Dot(result, _characterUp);
            if (yResult > 0.0f)
            {
                float yLimit = Vector3.Dot(displacement, _characterUp);
                if (yResult - yLimit > KINDA_SMALL_NUMBER)
                {
                    if (yLimit > 0.0f)
                    {
                        float upPercent = yLimit / yResult;
                        result *= upPercent;
                    }
                    else
                    {
                        result = Vector3.zero;
                    }
                    
                    Vector3 lateralRemainder = (slideResult - result).ProjectedOnPlane(_characterUp);
                    Vector3 lateralNormal = inNormal.ProjectedOnPlane(_characterUp).normalized;
                    Vector3 adjust = lateralRemainder.ProjectedOnPlane(lateralNormal);

                    result += adjust;
                }
            }

            return result;
        }

        private Vector3 ComputeSlideVector(Vector3 displacement, Vector3 inNormal, bool isWalkable)
        {
            if (IsGrounded)
            {
                if (isWalkable)
                    displacement = displacement.TangentTo(inNormal, _characterUp);
                else
                {
                    Vector3 right = inNormal.PerpendicularTo(GroundNormal);
                    Vector3 up = right.PerpendicularTo(inNormal);

                    displacement = displacement.ProjectedOnPlane(inNormal);
                    displacement = displacement.TangentTo(up, _characterUp);
                }
            }
            else
            {
                if (isWalkable)
                {
                    if (_isConstrainedToGround)
                        displacement = displacement.ProjectedOnPlane(_characterUp);
                    
                    displacement = displacement.ProjectedOnPlane(inNormal);
                }
                else
                {
                    Vector3 slideResult = displacement.ProjectedOnPlane(inNormal);

                    if (_isConstrainedToGround)
                        slideResult = HandleSlopeBoosting(slideResult, displacement, inNormal);
                    
                    displacement = slideResult;
                }
            }

            return ConstrainVectorToPlane(displacement);
        }

        private int SlideAlongSurface(int iteration, Vector3 inputDisplacement, ref Vector3 inVelocity,
            ref Vector3 displacement, ref CollisionResult inHit, ref Vector3 prevNormal)
        {
            if (UseFlatTop && inHit.HitLocation == EHitLocation.Above)
            {
                Vector3 surfaceNormal = FindBoxOpposingNormal(displacement, inHit.Normal, inHit.Transform);

                if (inHit.Normal != surfaceNormal)
                {
                    inHit.Normal = surfaceNormal;
                    inHit.SurfaceNormal = surfaceNormal;
                }
            }

            inHit.Normal = ComputeBlockingNormal(inHit.Normal, inHit.IsWalkable);

            if (inHit.IsWalkable && IsConstrainedToGround)
            {
                inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, true);
                displacement = ComputeSlideVector(displacement, inHit.Normal, true);
            }
            else
            {
                if (iteration == 0)
                {
                    inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, inHit.IsWalkable);
                    displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);

                    iteration++;
                }
                else if (iteration == 1)
                {
                    Vector3 crease = prevNormal.PerpendicularTo(inHit.Normal);

                    Vector3 oVel = inputDisplacement.ProjectedOnPlane(crease);

                    Vector3 nVel = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                            nVel = nVel.ProjectedOnPlane(crease);

                    if (oVel.Dot(nVel) <= 0.0f || prevNormal.Dot(inHit.Normal) < 0.0f)
                    {
                        inVelocity = ConstrainVectorToPlane(inVelocity.ProjectedOn(crease));
                        displacement = ConstrainVectorToPlane(displacement.ProjectedOn(crease));

                        ++iteration;
                    }
                    else
                    {
                        inVelocity = ComputeSlideVector(inVelocity, inHit.Normal, inHit.IsWalkable);
                        displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    }
                }
                else
                {
                    inVelocity = Vector3.zero;
                    displacement = Vector3.zero;
                }

                prevNormal = inHit.Normal;
            }

            return iteration;
        }
        
        private void PerformMovement(float deltaTime)
        {
            EDepenetrationBehaviour depenetrationFlags = !EnablePhysicsInteraction
                ? EDepenetrationBehaviour.IgnoreDynamic
                : EDepenetrationBehaviour.IgnoreNone;

            ResolveOverlaps(depenetrationFlags);
            
            if (IsGrounded)
                _velocity = _velocity.ProjectedOnPlane(_characterUp);
            
            Vector3 displacement = _velocity * deltaTime;
            
            if (IsGrounded)
            {
                displacement = displacement.TangentTo(GroundNormal, _characterUp);
                displacement = ConstrainVectorToPlane(displacement);
            }
            
            Vector3 inputDisplacement = displacement;
            
            int iteration = 0;
            Vector3 prevNormal = default;

            for (int i = 0; i < _collisionCount; i++)
            {
                ref CollisionResult collisionResult = ref _collisionResults[i];

                bool opposesMovement = displacement.Dot(collisionResult.Normal) < 0.0f;
                if (!opposesMovement)
                    continue;
                
                if (IsConstrainedToGround && !IsOnWalkableGround)
                {
                    if (IsValidLandingSpot(UpdatedPosition, ref collisionResult))
                    {
                        _hasLanded = true;
                        LandedVelocity = collisionResult.Velocity;
                    }
                    else
                    {
                        if (collisionResult.HitLocation == EHitLocation.Below)
                        {
                            FindGround(UpdatedPosition, out FindGroundResult groundResult);

                            collisionResult.IsWalkable = groundResult.IsWalkableGround;
                            if (collisionResult.IsWalkable)
                            {
                                _foundGround = groundResult;

                                _hasLanded = true;
                                LandedVelocity = collisionResult.Velocity;
                            }
                        }
                    }
                    
                    if (!_hasLanded && collisionResult.HitLocation == EHitLocation.Below)
                    {
                        _foundGround.SetFromSweepResult(true, false, UpdatedPosition, collisionResult.Point,
                            collisionResult.Normal, collisionResult.SurfaceNormal, collisionResult.Collider,
                            collisionResult.HitResult.distance);
                    }
                }
                
                iteration = SlideAlongSurface(iteration, inputDisplacement, ref _velocity, ref displacement,
                    ref collisionResult, ref prevNormal);
            }
            
            int maxSlideCount = _advanced.MaxMovementIterations;
            while (DetectCollisions && maxSlideCount-- > 0 && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
            {
                bool collided = MovementSweepTest(UpdatedPosition, _velocity, displacement,
                    out CollisionResult collisionResult);

                if (!collided)
                    break;
                
                UpdatedPosition += collisionResult.DisplacementToHit;

                displacement = collisionResult.RemainingDisplacement;
                
                if (IsGrounded && !collisionResult.IsWalkable)
                {
                    if (CanStepUp(collisionResult.Collider) &&
                        StepUp(ref collisionResult, out CollisionResult stepResult))
                    {
                        UpdatedPosition = stepResult.Position;

                        displacement = Vector3.zero;
                        break;
                    }
                }
                
                if (IsConstrainedToGround && !IsOnWalkableGround)
                {
                    if (IsValidLandingSpot(UpdatedPosition, ref collisionResult))
                    {
                        _hasLanded = true;
                        LandedVelocity = collisionResult.Velocity;
                    }
                    else
                    {

                        if (ShouldCheckForValidLandingSpot(ref collisionResult))
                        {
                            FindGround(UpdatedPosition, out FindGroundResult groundResult);

                            collisionResult.IsWalkable = groundResult.IsWalkableGround;
                            if (collisionResult.IsWalkable)
                            {
                                _foundGround = groundResult;

                                _hasLanded = true;
                                LandedVelocity = collisionResult.Velocity;
                            }
                        }
                    }
                    
                    if (!_hasLanded && collisionResult.HitLocation == EHitLocation.Below)
                    {
                        float sweepDistance = collisionResult.HitResult.distance;
                        Vector3 surfaceNormal = collisionResult.SurfaceNormal;

                        _foundGround.SetFromSweepResult(true, false, UpdatedPosition, sweepDistance,
                            ref collisionResult.HitResult, surfaceNormal);
                    }
                }
                
                iteration = SlideAlongSurface(iteration, inputDisplacement, ref _velocity, ref displacement,
                    ref collisionResult, ref prevNormal);
                
                AddCollisionResult(ref collisionResult);
            }

            if (displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
                UpdatedPosition += displacement;

            if (IsGrounded || _hasLanded)
            {
                _velocity = _velocity.ProjectedOnPlane(_characterUp).normalized * _velocity.magnitude;
                _velocity = ConstrainVectorToPlane(_velocity);
            }
        }

        private bool CanPerchOn(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;
            
            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanPerchOn(collisionBehaviour))
                    return true;

                if (CanNotPerchOn(collisionBehaviour))
                    return false;
            }
            
            return true;
        }
        
        private float GetPerchRadiusThreshold()
        {
            return Mathf.Max(0.0f, _radius - PerchOffset);
        }

        private float GetValidPerchRadius(Collider otherCollider)
        {
            if (!CanPerchOn(otherCollider))
                return 0.0011f;
            
            return Mathf.Clamp(_perchOffset, 0.0011f, _radius);
        }

        private bool ShouldComputePerchResult(Vector3 characterPosition, ref RaycastHit inHit)
        {
            if (GetPerchRadiusThreshold() <= SWEEP_EDGE_REJECT_DISTANCE)
	        {
		        return false;
	        }

            float distFromCenterSq = (inHit.point - characterPosition).ProjectedOnPlane(_characterUp).sqrMagnitude;
            float standOnEdgeRadius = GetValidPerchRadius(inHit.collider);

            if (distFromCenterSq <= standOnEdgeRadius.Square())
            {
                return false;
            }

            return true;
        }

        private bool CapsuleCast(Vector3 point1, Vector3 point2, float castRadius, Vector3 castDirection,
            float castDistance, int castLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = false;

            int rawHitCount = Physics.CapsuleCastNonAlloc(point1, point2, castRadius, castDirection, _hits,
                castDistance, castLayerMask, TriggerInteraction);

            if (rawHitCount == 0)
                return false;

            float closestDistance = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                if (hit.distance <= 0.0f)
                    startPenetrating = true;
                else if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex != -1)
            {
                hitResult = _hits[hitIndex];
                return true;
            }

            return false;
        }

        private bool BoxCast(Vector3 center, Vector3 halfExtents, Quaternion orientation, Vector3 castDirection,
            float castDistance, int castLayerMask, out RaycastHit hitResult, out bool startPenetrating)
        {
            hitResult = default;
            startPenetrating = default;

            int rawHitCount = Physics.BoxCastNonAlloc(center, halfExtents, castDirection, _hits, orientation,
                castDistance, castLayerMask, TriggerInteraction);

            if (rawHitCount == 0)
                return false;

            float closestDistance = Mathf.Infinity;

            int hitIndex = -1;
            for (int i = 0; i < rawHitCount; i++)
            {
                ref RaycastHit hit = ref _hits[i];
                if (ShouldFilter(hit.collider))
                    continue;

                if (hit.distance <= 0.0f)
                    startPenetrating = true;
                else if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    hitIndex = i;
                }
            }

            if (hitIndex != -1)
            {
                hitResult = _hits[hitIndex];
                return true;
            }

            return false;
        }
        
        private bool GroundSweepTest(Vector3 characterPosition, float capsuleRadius, float capsuleHalfHeight,
            float sweepDistance, out RaycastHit hitResult, out bool startPenetrating)
        {
            bool foundBlockingHit;

            if (!UseFlatBaseForGroundChecks)
            {
                Vector3 characterCenter = characterPosition + _transformedCapsuleCenter;

                Vector3 point1 = characterCenter - _characterUp * (capsuleHalfHeight - capsuleRadius);
                Vector3 point2 = characterCenter + _characterUp * (capsuleHalfHeight - capsuleRadius);

                Vector3 sweepDirection = -1.0f * _characterUp;

                foundBlockingHit = CapsuleCast(point1, point2, capsuleRadius, sweepDirection, sweepDistance,
                    _collisionLayers, out hitResult, out startPenetrating);
            }
            else
            {
                Vector3 center = characterPosition + _transformedCapsuleCenter;
                Vector3 halfExtents = new Vector3(capsuleRadius * 0.707f, capsuleHalfHeight, capsuleRadius * 0.707f);
                
                Quaternion sweepOrientation = Rotation * Quaternion.Euler(0f, -Rotation.eulerAngles.y, 0f);
                Vector3 sweepDirection = -1.0f * _characterUp;

                LayerMask sweepLayerMask = _collisionLayers;

                foundBlockingHit = BoxCast(center, halfExtents, sweepOrientation * Quaternion.Euler(0.0f, 45.0f, 0.0f),
                    sweepDirection, sweepDistance, sweepLayerMask, out hitResult, out startPenetrating);

                if (!foundBlockingHit && !startPenetrating)
                {
                    foundBlockingHit = BoxCast(center, halfExtents, sweepOrientation, sweepDirection, sweepDistance,
                        sweepLayerMask, out hitResult, out startPenetrating);
                }
            }
            
            return foundBlockingHit;
        }
        
        public void ComputeGroundDistance(Vector3 characterPosition, float sweepRadius, float sweepDistance,
            float castDistance, out FindGroundResult outGroundResult)
        {
            outGroundResult = default;
            if (sweepDistance < castDistance)
                return;

            float characterRadius = _radius;
            float characterHeight = _height;
            float characterHalfHeight = characterHeight * 0.5f;

            bool foundGround = default;
            bool startPenetrating = default;
            
            if (sweepDistance > 0.0f && sweepRadius > 0.0f)
            {
                const float shrinkScale = 0.9f;
                float shrinkHeight = (characterHalfHeight - characterRadius) * (1.0f - shrinkScale);

                float capsuleRadius = sweepRadius;
                float capsuleHalfHeight = characterHalfHeight - shrinkHeight;

                float actualSweepDistance = sweepDistance + shrinkHeight;

                foundGround = GroundSweepTest(characterPosition, capsuleRadius, capsuleHalfHeight, actualSweepDistance,
                    out RaycastHit hitResult, out startPenetrating);

                if (foundGround || startPenetrating)
                {
                    if (startPenetrating || !IsWithinEdgeTolerance(characterPosition, hitResult.point, capsuleRadius))
                    {
                        const float shrinkScaleOverlap = 0.1f;
                        shrinkHeight = (characterHalfHeight - characterRadius) * (1.0f - shrinkScaleOverlap);

                        capsuleRadius = Mathf.Max(0.0011f, capsuleRadius - SWEEP_EDGE_REJECT_DISTANCE - KINDA_SMALL_NUMBER);
                        capsuleHalfHeight = Mathf.Max(capsuleRadius, characterHalfHeight - shrinkHeight);

                        actualSweepDistance = sweepDistance + shrinkHeight;

                        foundGround = GroundSweepTest(characterPosition, capsuleRadius, capsuleHalfHeight,
                            actualSweepDistance, out hitResult, out startPenetrating);
                    }

                    if (foundGround && !startPenetrating)
                    {
                        float maxPenetrationAdjust = Mathf.Max(MAX_GROUND_DISTANCE, characterRadius);
                        float sweepResult = Mathf.Max(-maxPenetrationAdjust, hitResult.distance - shrinkHeight);

                        Vector3 sweepDirection = -1.0f * _characterUp;
                        Vector3 hitPosition = characterPosition + sweepDirection * sweepResult;

                        Vector3 surfaceNormal = hitResult.normal;

                        bool isWalkable = false;
                        bool hitGround = sweepResult <= sweepDistance &&
                                         ComputeHitLocation(hitResult.normal) == EHitLocation.Below;
                        
                        if (hitGround)
                        {
                            if (UseFlatBaseForGroundChecks)
                                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
                            else
                            {
                                surfaceNormal = FindGeomOpposingNormal(sweepDirection * sweepDistance, ref hitResult);

                                isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
                            }
                        }

                        outGroundResult.SetFromSweepResult(hitGround, isWalkable, hitPosition, sweepResult,
                            ref hitResult, surfaceNormal);

                        if (outGroundResult.IsWalkableGround)
                            return;
                    }
                }
            }
            
            if (!foundGround && !startPenetrating)
                return;
            
            if (castDistance > 0.0f)
            {
                Vector3 rayOrigin = characterPosition + _transformedCapsuleCenter;
                Vector3 rayDirection = -1.0f * _characterUp;

                float shrinkHeight = characterHalfHeight;
                float rayLength = castDistance + shrinkHeight;

                foundGround = Raycast(rayOrigin, rayDirection, rayLength, _collisionLayers, out RaycastHit hitResult);

                if (foundGround && hitResult.distance > 0.0f)
                {
                    float maxPenetrationAdjust = Mathf.Max(MAX_GROUND_DISTANCE, characterRadius);
                    float castResult = Mathf.Max(-maxPenetrationAdjust, hitResult.distance - shrinkHeight);
                    
                    if (castResult <= castDistance && IsWalkable(hitResult.collider, hitResult.normal))
                    {
                        outGroundResult.SetFromRaycastResult(true, true, outGroundResult.Position,
                            outGroundResult.GroundDistance, castResult, ref hitResult);

                        return;
                    }
                }
            }
            
            outGroundResult.IsWalkable = false;
        }

        private bool ComputePerchResult(Vector3 characterPosition, float testRadius, float inMaxGroundDistance,
            ref RaycastHit inHit, out FindGroundResult perchGroundResult)
        {
            perchGroundResult = default;

            if (inMaxGroundDistance <= 0.0f)
                return false;
            
            float inHitAboveBase = Mathf.Max(0.0f, Vector3.Dot(inHit.point - characterPosition, _characterUp));
            float perchCastDist = Mathf.Max(0.0f, inMaxGroundDistance - inHitAboveBase);
            float perchSweepDist = Mathf.Max(0.0f, inMaxGroundDistance);

            float actualSweepDist = perchSweepDist + _radius;
            ComputeGroundDistance(characterPosition, testRadius, actualSweepDist, perchCastDist, out perchGroundResult);

            if (!perchGroundResult.IsWalkable)
                return false;
            if (inHitAboveBase + perchGroundResult.GroundDistance > inMaxGroundDistance)
            {
                perchGroundResult.IsWalkable = false;
                return false;
            }

            return true;
        }

        public void FindGround(Vector3 characterPosition, out FindGroundResult outGroundResult)
        {
            if (!_detectCollisions)
            {
                outGroundResult = default;
                return;
            }
            
            float heightCheckAdjust = IsGrounded ? MAX_GROUND_DISTANCE + KINDA_SMALL_NUMBER : -MAX_GROUND_DISTANCE;
            float sweepDistance = Mathf.Max(MAX_GROUND_DISTANCE, StepOffset + heightCheckAdjust);
            
            ComputeGroundDistance(characterPosition, _radius, sweepDistance, sweepDistance, out outGroundResult);

            if (outGroundResult.HitGround && !outGroundResult.IsRaycastResult)
            {
                Vector3 positionOnGround = outGroundResult.Position;

                if (ShouldComputePerchResult(positionOnGround, ref outGroundResult.HitResult))
                {
                    float maxPerchGroundDistance = sweepDistance;
                    if (IsGrounded)
                        maxPerchGroundDistance += PerchAdditionalHeight;

                    float validPerchRadius = GetValidPerchRadius(outGroundResult.Collider);

                    if (ComputePerchResult(positionOnGround, validPerchRadius, maxPerchGroundDistance,
                        ref outGroundResult.HitResult, out FindGroundResult perchGroundResult))
                    {

                        float moveUpDist = AVG_GROUND_DISTANCE - outGroundResult.GroundDistance;
                        if (moveUpDist + perchGroundResult.GroundDistance >= maxPerchGroundDistance)
                        {
                            outGroundResult.GroundDistance = AVG_GROUND_DISTANCE;
                        }
                        
                        if (!outGroundResult.IsWalkableGround)
                        {
                            float groundDistance = outGroundResult.GroundDistance;
                            float raycastDistance = Mathf.Max(MIN_GROUND_DISTANCE, groundDistance);

                            outGroundResult.SetFromRaycastResult(true, true, outGroundResult.Position, groundDistance,
                                raycastDistance, ref perchGroundResult.HitResult);
                        }
                    }
                    else
                    {
                        outGroundResult.IsWalkable = false;
                    }
                }
            }
        }

        private void AdjustGroundHeight()
        {
            if (!_currentGround.IsWalkableGround || !IsConstrainedToGround)
                return;

            float lastGroundDistance = _currentGround.GroundDistance;
            
            if (_currentGround.IsRaycastResult)
            {
                if (lastGroundDistance < MIN_GROUND_DISTANCE && _currentGround.RaycastDistance >= MIN_GROUND_DISTANCE)
                {
                    return;
                }

                lastGroundDistance = _currentGround.RaycastDistance;
            }
            
            if (lastGroundDistance < MIN_GROUND_DISTANCE || lastGroundDistance > MAX_GROUND_DISTANCE)
            {
                float initialY = Vector3.Dot(UpdatedPosition, _characterUp);
                float moveDistance = AVG_GROUND_DISTANCE - lastGroundDistance;

                Vector3 displacement = _characterUp * moveDistance;

                Vector3 sweepOrigin = UpdatedPosition;
                Vector3 sweepDirection = displacement.normalized;

                float sweepRadius = _radius;
                float sweepDistance = displacement.magnitude;

                int sweepLayerMask = _collisionLayers;

                bool hit = SweepTestEx(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                    out RaycastHit hitResult, out bool startPenetrating, out _, out _, true);

                if (!hit && !startPenetrating)
                {
                    UpdatedPosition += displacement;
                    _currentGround.GroundDistance += moveDistance;
                }
                else if (moveDistance > 0.0f)
                {
                    UpdatedPosition += sweepDirection * hitResult.distance;

                    float currentY = Vector3.Dot(UpdatedPosition, _characterUp);
                    _currentGround.GroundDistance += currentY - initialY;
                }
                else
                {
                    UpdatedPosition += sweepDirection * hitResult.distance;

                    float currentY = Vector3.Dot(UpdatedPosition, _characterUp);
                    _currentGround.GroundDistance = currentY - initialY;
                }
            }
            
            if (_rootTransform)
            {
                _rootTransform.localPosition =
                    _rootTransformOffset - new Vector3(0.0f, AVG_GROUND_DISTANCE, 0.0f);
            }
        }

        private bool CanStepUp(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;
            
            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanStepOn(collisionBehaviour))
                    return true;

                if (CanNotStepOn(collisionBehaviour))
                    return false;
            }
            
            return true;
        }

        private bool StepUp(ref CollisionResult inCollision, out CollisionResult stepResult)
        {
            stepResult = default;
            
            if (inCollision.HitLocation == EHitLocation.Above)
                return false;
            
            float characterInitialGroundPositionY = Vector3.Dot(inCollision.Position, _characterUp);
            float groundPointY = characterInitialGroundPositionY;
            
            float actualGroundDistance = Mathf.Max(0.0f, _currentGround.GetDistanceToGround());
            characterInitialGroundPositionY -= actualGroundDistance;

            float stepTravelUpHeight = Mathf.Max(0.0f, StepOffset - actualGroundDistance);
            float stepTravelDownHeight = StepOffset + MAX_GROUND_DISTANCE * 2.0f;

            bool hitVerticalFace =
                !IsWithinEdgeTolerance(inCollision.Position, inCollision.Point, _radius + CONTACT_OFFSET);

            if (!_currentGround.IsRaycastResult && !hitVerticalFace)
                groundPointY = Vector3.Dot(GroundPoint, _characterUp);
            else
                groundPointY -= _currentGround.GroundDistance;
            
            float initialImpactY = Vector3.Dot(inCollision.Point, _characterUp);
            if (initialImpactY <= characterInitialGroundPositionY)
                return false;
            
            Vector3 sweepOrigin = inCollision.Position;
            Vector3 sweepDirection = _characterUp;

            float sweepRadius = _radius;
            float sweepDistance = stepTravelUpHeight;

            int sweepLayerMask = _collisionLayers;

            bool foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out RaycastHit hitResult, out bool startPenetrating);

            if (startPenetrating)
                return false;

            if (!foundBlockingHit)
                sweepOrigin += sweepDirection * sweepDistance;
            else
                sweepOrigin += sweepDirection * hitResult.distance;
            
            Vector3 displacement = inCollision.RemainingDisplacement;
            Vector3 displacement2D = ConstrainVectorToPlane(Vector3.ProjectOnPlane(displacement, _characterUp));

            sweepDistance = displacement.magnitude;
            sweepDirection = displacement2D.normalized;            

            foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out hitResult, out startPenetrating);

            if (startPenetrating)
                return false;

            if (!foundBlockingHit)
                sweepOrigin += sweepDirection * sweepDistance;
            else
            {
                return false;
            }
            
            sweepDirection = -_characterUp;
            sweepDistance = stepTravelDownHeight;

            foundBlockingHit = SweepTest(sweepOrigin, sweepRadius, sweepDirection, sweepDistance, sweepLayerMask,
                out hitResult, out startPenetrating);

            if (!foundBlockingHit || startPenetrating)
                return false;
            
            float deltaY = Vector3.Dot(hitResult.point, _characterUp) - groundPointY;
            if (deltaY > StepOffset)
                return false;
            
            Vector3 positionOnStep = sweepOrigin + sweepDirection * hitResult.distance;

            if (OverlapTest(positionOnStep, UpdatedRotation, _radius, _height, _collisionLayers, _overlaps, TriggerInteraction) > 0)
                return false;
            
            Vector3 surfaceNormal = FindGeomOpposingNormal(sweepDirection * sweepDistance, ref hitResult);

            bool isWalkable = IsWalkable(hitResult.collider, surfaceNormal);
            if (!isWalkable)
            {
                bool normalTowardsMe = Vector3.Dot(displacement, surfaceNormal) < 0.0f;
                if (normalTowardsMe)
                    return false;
                
                if (Vector3.Dot(positionOnStep, _characterUp) > Vector3.Dot(inCollision.Position, _characterUp))
                    return false;
            }

            if (!IsWithinEdgeTolerance(positionOnStep, hitResult.point, _radius + CONTACT_OFFSET))
                return false;
            
            if (deltaY > 0.0f && !CanStepUp(hitResult.collider))
                return false;
            
            stepResult = new CollisionResult
            {
                Position = positionOnStep
            };

            return true;
        }

        public void PauseGroundConstraint(float unconstrainedTime = 0.1f)
        {
            _unconstrainedTimer = Mathf.Max(0.0f, unconstrainedTime);
        }

        private void UpdateCurrentGround(ref FindGroundResult inGroundResult)
        {
            WasOnGround = IsOnGround;

            WasOnWalkableGround = IsOnWalkableGround;

            WasGrounded = IsGrounded;

            _currentGround = inGroundResult;
        }

        private int SlideAlongSurface(int iteration, Vector3 inputDisplacement, ref Vector3 displacement,
            ref CollisionResult inHit, ref Vector3 prevNormal)
        {
            inHit.Normal = ComputeBlockingNormal(inHit.Normal, inHit.IsWalkable);

            if (inHit.IsWalkable && IsConstrainedToGround)
                displacement = ComputeSlideVector(displacement, inHit.Normal, true);
            else
            {
                if (iteration == 0)
                {
                    displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    iteration++;
                }
                else if (iteration == 1)
                {
                    Vector3 crease = prevNormal.PerpendicularTo(inHit.Normal);

                    Vector3 oVel = inputDisplacement.ProjectedOnPlane(crease);

                    Vector3 nVel = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    nVel = nVel.ProjectedOnPlane(crease);

                    if (oVel.Dot(nVel) <= 0.0f || prevNormal.Dot(inHit.Normal) < 0.0f)
                    {
                        displacement = ConstrainVectorToPlane(displacement.ProjectedOn(crease));
                        ++iteration;
                    }
                    else
                    {
                        displacement = ComputeSlideVector(displacement, inHit.Normal, inHit.IsWalkable);
                    }
                }
                else
                {
                    displacement = Vector3.zero;
                }

                prevNormal = inHit.Normal;
            }

            return iteration;
        }

        private void MoveAndSlide(Vector3 displacement)
        {
            Vector3 inputDisplacement = displacement;

            int iteration = default;
            Vector3 prevNormal = default;

            int maxSlideCount = _advanced.MaxMovementIterations;
            while (maxSlideCount-- > 0 && displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
            {
                bool collided = MovementSweepTest(UpdatedPosition, default, displacement, out CollisionResult collisionResult);
                if (!collided)
                    break;


                UpdatedPosition += collisionResult.DisplacementToHit;

                displacement = collisionResult.RemainingDisplacement;

                iteration = SlideAlongSurface(iteration, inputDisplacement, ref displacement, ref collisionResult, ref prevNormal);

                AddCollisionResult(ref collisionResult);
            }

            if (displacement.sqrMagnitude > _advanced.MinMoveDistanceSqr)
                UpdatedPosition += displacement;
        }

        private bool CanRideOn(Collider otherCollider)
        {
            if (otherCollider == null)
                return false;
            
            if (CollisionBehaviour != null)
            {
                ECollisionBehaviour collisionBehaviour = this.CollisionBehaviour.Invoke(otherCollider);

                if (CanRideOn(collisionBehaviour) && otherCollider.attachedRigidbody)
                    return true;

                if (CanNotRideOn(collisionBehaviour) && otherCollider.attachedRigidbody)
                    return false;
            }
            
            return otherCollider.attachedRigidbody;
        }

        private void IgnoreCurrentPlatform(bool ignore)
        {
            IgnoreCollision(_movingPlatform.Platform, ignore);
        }

        public void AttachTo(Rigidbody parent)
        {
            _parentPlatform = parent;
        }

        private void UpdateCurrentPlatform()
        {
            _lastVelocityOnMovingPlatform = Vector3.zero;
            
            _movingPlatform.LastPlatform = _movingPlatform.Platform;

            if (_parentPlatform)
                _movingPlatform.Platform = _parentPlatform;
            else if (IsGrounded && CanRideOn(GroundCollider))
                _movingPlatform.Platform = GroundCollider.attachedRigidbody;
            else
                _movingPlatform.Platform = null;

            if (_movingPlatform.Platform != null)
            {
                Transform platformTransform = _movingPlatform.Platform.transform;

                _movingPlatform.Position = UpdatedPosition;
                _movingPlatform.LocalPosition = platformTransform.InverseTransformPoint(UpdatedPosition);

                _movingPlatform.Rotation = UpdatedRotation;
                _movingPlatform.LocalRotation = Quaternion.Inverse(platformTransform.rotation) * UpdatedRotation;
                
                _lastVelocityOnMovingPlatform = Velocity;
            }
        }

        private void UpdatePlatformMovement(float deltaTime)
        {
            Vector3 lastPlatformVelocity = _movingPlatform.PlatformVelocity;

            if (!_movingPlatform.Platform)
            {
                _movingPlatform.PlatformVelocity = Vector3.zero;
            }
            else
            {
                Transform platformTransform = _movingPlatform.Platform.transform;

                Vector3 newPositionOnPlatform = platformTransform.TransformPoint(_movingPlatform.LocalPosition);
                Vector3 deltaPosition = newPositionOnPlatform - _movingPlatform.Position;

                _movingPlatform.DeltaPosition = deltaPosition;
                _movingPlatform.PlatformVelocity = deltaTime > 0.0f ? deltaPosition / deltaTime : Vector3.zero;

                if (ImpartPlatformRotation)
                {
                    Quaternion newRotationOnPlatform = platformTransform.rotation * _movingPlatform.LocalRotation;
                    Quaternion deltaRotation = newRotationOnPlatform * Quaternion.Inverse(_movingPlatform.Rotation);

                    _movingPlatform.DeltaRotation = deltaRotation;

                    Vector3 newForward = Vector3
                        .ProjectOnPlane(deltaRotation * UpdatedRotation * Vector3.forward, _characterUp).normalized;
                    
                    UpdatedRotation = Quaternion.LookRotation(newForward, _characterUp);
                }
            }

            if (ImpartPlatformMovement && _movingPlatform.PlatformVelocity.sqrMagnitude > 0.0f)
            {
                if (FastPlatformMove)
                    UpdatedPosition += _movingPlatform.PlatformVelocity * deltaTime;
                else
                {
                    IgnoreCurrentPlatform(true);

                    MoveAndSlide(_movingPlatform.PlatformVelocity * deltaTime);

                    IgnoreCurrentPlatform(false);
                }
            }

            if (ImpartPlatformVelocity && _movingPlatform.LastPlatform && _movingPlatform.Platform != _movingPlatform.LastPlatform)
            {
                _velocity -= _movingPlatform.PlatformVelocity;

                _velocity += lastPlatformVelocity;
            }
            
            if (ImpartPlatformVelocity && _movingPlatform.LastPlatform == null && _movingPlatform.Platform)
            {
                _velocity = _lastVelocityOnMovingPlatform - _movingPlatform.PlatformVelocity;
            }
        }

        private void ComputeDynamicCollisionResponse(ref CollisionResult inCollisionResult,
            out Vector3 characterImpulse, out Vector3 otherImpulse)
        {
            characterImpulse = default;
            otherImpulse = default;

            float massRatio = 0.0f;

            Rigidbody otherRigidbody = inCollisionResult.Rigidbody;
            if (!otherRigidbody.isKinematic || otherRigidbody.TryGetComponent(out CharacterMovement _))
            {
                float mass = Rigidbody.mass;
                massRatio = mass / (mass + inCollisionResult.Rigidbody.mass);
            }

            Vector3 normal = inCollisionResult.Normal;

            float velocityDotNormal = Vector3.Dot(inCollisionResult.Velocity, normal);
            float otherVelocityDotNormal = Vector3.Dot(inCollisionResult.OtherVelocity, normal);

            if (velocityDotNormal < 0.0f)
                characterImpulse += velocityDotNormal * normal;

            if (otherVelocityDotNormal > velocityDotNormal)
            {
                Vector3 relVel = (otherVelocityDotNormal - velocityDotNormal) * normal;

                characterImpulse += relVel * (1.0f - massRatio);
                otherImpulse -= relVel * massRatio;
            }
        }

        private void ResolveDynamicCollisions()
        {
            if (!EnablePhysicsInteraction)
                return;

            for (int i = 0; i < _collisionCount; i++)
            {
                ref CollisionResult collisionResult = ref _collisionResults[i];
                if (collisionResult.IsWalkable)
                    continue;

                Rigidbody otherRigidbody = collisionResult.Rigidbody;
                if (otherRigidbody == null)
                    continue;

                ComputeDynamicCollisionResponse(ref collisionResult, out Vector3 characterImpulse, out Vector3 otherImpulse);

                CollisionResponse?.Invoke(ref collisionResult, ref characterImpulse, ref otherImpulse);

                if (otherRigidbody.TryGetComponent(out CharacterMovement otherCharacter))
                {
                    if (PhysicsInteractionAffectsCharacters)
                    {
                        Velocity += characterImpulse;
                        otherCharacter.Velocity += otherImpulse * PushForceScale;
                    }
                }
                else
                {
                    _velocity += characterImpulse;

                    if (!otherRigidbody.isKinematic)
                    {
                        otherRigidbody.AddForceAtPosition(otherImpulse * PushForceScale, collisionResult.Point,
                            ForceMode.VelocityChange);
                    }
                }
            }

            if (IsGrounded)
                _velocity = _velocity.ProjectedOnPlane(_characterUp).normalized * _velocity.magnitude;

            _velocity = ConstrainVectorToPlane(_velocity);
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
            _hasLanded = false;
            _foundGround = default;

            UpdatedPosition = Transform.position;
            UpdatedRotation = Transform.rotation;

            _characterUp = UpdatedRotation * Vector3.up;

            _transformedCapsuleCenter = UpdatedRotation * _capsuleCenter;
            _transformedCapsuleTopCenter = UpdatedRotation * _capsuleTopCenter;
            _transformedCapsuleBottomCenter = UpdatedRotation * _capsuleBottomCenter;

            ResetCollisionFlags();
        }

        public void ClearAccumulatedForces()
        {
            _pendingForces = Vector3.zero;
            _pendingImpulses = Vector3.zero;
            _pendingLaunchVelocity = Vector3.zero;
        }

        public void AddForce(Vector3 force, ForceMode forceMode = ForceMode.Force)
        {
            switch (forceMode)
            {
                case ForceMode.Force:
                    {
                        _pendingForces += force / Rigidbody.mass;
                        break;
                    }

                case ForceMode.Acceleration:
                    {
                        _pendingForces += force;
                        break;
                    }

                case ForceMode.Impulse:
                    {
                        _pendingImpulses += force / Rigidbody.mass;
                        break;
                    }

                case ForceMode.VelocityChange:
                    {
                        _pendingImpulses += force;
                        break;
                    }
            }
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
            Vector3 finalVelocity = launchVelocity;
            
            Vector3 characterUp = Transform.up;

            if (!overrideLateralVelocity)
                finalVelocity += _velocity.ProjectedOnPlane(characterUp);


            if (!overrideVerticalVelocity)
                finalVelocity += _velocity.ProjectedOn(characterUp);

            _pendingLaunchVelocity = finalVelocity;
        }

        private void UpdateVelocity(Vector3 newVelocity, float deltaTime)
        {
            _velocity = newVelocity;
            
            _velocity += _pendingForces * deltaTime;
            _velocity += _pendingImpulses;
            
            if (_pendingLaunchVelocity.sqrMagnitude > 0.0f)
                _velocity = _pendingLaunchVelocity;
            
            ClearAccumulatedForces();
            
            _velocity = ConstrainVectorToPlane(_velocity);
        }
        
        public CollisionFlags Move(Vector3 newVelocity, float deltaTime)
        {
            UpdateCachedFields();

            ClearCollisionResults();
            
            UpdateVelocity(newVelocity, deltaTime);

            UpdatePlatformMovement(deltaTime);

            PerformMovement(deltaTime);
            
            if (IsGrounded || _hasLanded)
                FindGround(UpdatedPosition, out _foundGround);
            
            UpdateCurrentGround(ref _foundGround);
            {
                if (_unconstrainedTimer > 0.0f)
                {
                    _unconstrainedTimer -= deltaTime;
                    if (_unconstrainedTimer <= 0.0f)
                        _unconstrainedTimer = 0.0f;
                }
            }

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

            _isConstrainedToGround = inConstrainedToGround;
            _unconstrainedTimer = Mathf.Max(0.0f, inUnconstrainedTimer);

            _currentGround.HitGround = inHitGround;
            _currentGround.IsWalkable = inIsWalkable;

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

            _isConstrainedToGround = true;

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

            _advanced.OnValidate();
        }

        private void Awake()
        {
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

#if UNITY_EDITOR

        private static void DrawDisc(Vector3 pos, Quaternion rot, float radius, Color color = default,
            bool solid = true)
        {
            if (color != default)
                Handles.color = color;

            Matrix4x4 mtx = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);

            using (new Handles.DrawingScope(mtx))
            {
                if (solid)
                    Handles.DrawSolidDisc(Vector3.zero, Vector3.up, radius);
                else
                    Handles.DrawWireDisc(Vector3.zero, Vector3.up, radius);
            }
        }

        private void OnDrawGizmosSelected()
        {
            float skinRadius = _radius;
            Vector3 footPosition = GetFootPosition();

            Gizmos.color = new Color(0.569f, 0.957f, 0.545f, 0.5f);
            Gizmos.DrawLine(footPosition + Vector3.left * skinRadius, footPosition + Vector3.right * skinRadius);
            Gizmos.DrawLine(footPosition + Vector3.back * skinRadius, footPosition + Vector3.forward * skinRadius);
            
            if (PerchOffset > 0.0f && PerchOffset < Radius)
            {
                DrawDisc(footPosition, Rotation, _perchOffset, new Color(0.569f, 0.957f, 0.545f, 0.15f));
                DrawDisc(footPosition, Rotation, _perchOffset, new Color(0.569f, 0.957f, 0.545f, 0.75f), false);
            }
            
            if (StepOffset > 0.0f)
            {
                DrawDisc(footPosition + Transform.up * StepOffset, Rotation, Radius * 1.15f,
                    new Color(0.569f, 0.957f, 0.545f, 0.75f), false);
            }
        }

#endif
    }
}