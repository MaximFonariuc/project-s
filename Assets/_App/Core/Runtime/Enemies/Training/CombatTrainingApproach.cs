using System;
using ModularAnimation;
using UnityEngine;

namespace _App
{
    public enum CombatTrainingApproachState
    {
        Disengaged,
        Pursuing,
        Holding,
        Retreating,
        Committed
    }

    public readonly struct CombatTrainingApproachSnapshot
    {
        public CombatTrainingApproachState State { get; }
        public float ForwardSpeed { get; }
        public float LateralSpeed { get; }
        public float VerticalSpeed { get; }
        public float NormalizedSpeed { get; }
        public bool IsGrounded { get; }

        public CombatTrainingApproachSnapshot(
            CombatTrainingApproachState state,
            float forwardSpeed,
            float lateralSpeed,
            float verticalSpeed,
            float normalizedSpeed,
            bool isGrounded)
        {
            State = state;
            ForwardSpeed = forwardSpeed;
            LateralSpeed = lateralSpeed;
            VerticalSpeed = verticalSpeed;
            NormalizedSpeed = Mathf.Clamp01(normalizedSpeed);
            IsGrounded = isGrounded;
        }
    }

    [Serializable]
    public sealed class CombatTrainingApproachSettings
    {
        [Header("Steering")]
        [SerializeField, Min(0.01f)] private float _moveSpeed = 2.4f;
        [SerializeField, Min(0.01f)] private float _acceleration = 14.0f;
        [SerializeField] private CharacterGait _gait = CharacterGait.Walk;

        [Header("Standoff")]
        [SerializeField, Range(0.1f, 1.0f)] private float _approachRangeRatio = 0.80f;
        [SerializeField, Range(0.0f, 1.0f)] private float _retreatRangeRatio = 0.45f;

        [Header("Engagement")]
        [SerializeField, Min(0.0f)] private float _pursuitRange = 20.0f;

        internal CombatTrainingApproachDefinition Compile()
        {
            float moveSpeed = RequirePositive(_moveSpeed, nameof(_moveSpeed));
            float acceleration = RequirePositive(_acceleration, nameof(_acceleration));
            float approachRatio = RequireRange(
                _approachRangeRatio,
                0.1f,
                1.0f,
                nameof(_approachRangeRatio));
            float retreatRatio = RequireRange(
                _retreatRangeRatio,
                0.0f,
                1.0f,
                nameof(_retreatRangeRatio));
            if (retreatRatio >= approachRatio)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(_retreatRangeRatio),
                    retreatRatio,
                    "Retreat ratio must stay below the approach ratio so the hold " +
                    "band cannot collapse.");
            }

            float pursuitRange = RequireNonNegative(_pursuitRange, nameof(_pursuitRange));

            return new CombatTrainingApproachDefinition(
                moveSpeed,
                acceleration,
                _gait,
                approachRatio,
                retreatRatio,
                pursuitRange);
        }

        private static float RequirePositive(float value, string parameterName)
        {
            CombatActionNumeric.RequireFinite(value, parameterName);
            if (value <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float RequireNonNegative(float value, string parameterName)
        {
            CombatActionNumeric.RequireFinite(value, parameterName);
            if (value < 0.0f)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }

        private static float RequireRange(
            float value,
            float minimum,
            float maximum,
            string parameterName)
        {
            CombatActionNumeric.RequireFinite(value, parameterName);
            if (value < minimum || value > maximum)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }

            return value;
        }
    }

    internal readonly struct CombatTrainingApproachDefinition
    {
        public float MoveSpeed { get; }
        public float Acceleration { get; }
        public CharacterGait Gait { get; }
        public float ApproachRangeRatio { get; }
        public float RetreatRangeRatio { get; }
        public float PursuitRange { get; }

        public CombatTrainingApproachDefinition(
            float moveSpeed,
            float acceleration,
            CharacterGait gait,
            float approachRangeRatio,
            float retreatRangeRatio,
            float pursuitRange)
        {
            MoveSpeed = moveSpeed;
            Acceleration = acceleration;
            Gait = gait;
            ApproachRangeRatio = approachRangeRatio;
            RetreatRangeRatio = retreatRangeRatio;
            PursuitRange = pursuitRange;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CombatTrainingAttacker))]
    [AddComponentMenu("Project S/Enemies/Combat Training Approach")]
    public sealed class CombatTrainingApproach : MonoBehaviour
    {
        private const float DirectionEpsilon = 0.0001f;

        [SerializeField] private CombatTrainingAttacker _attacker;
        [SerializeField] private Health _health;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private RigidbodyCharacterImpactMotor _impactMotor;
        [SerializeField] private CharacterReactionHost _reactionHost;
        [SerializeField] private CombatTrainingDefense _defense;
        [SerializeField] private CombatTrainingApproachSettings _approach = new();

        private CombatTrainingApproachDefinition _definition;
        private CombatTrainingApproachState _state;
        private Vector3 _planarVelocity;
        private float _verticalSpeed;
        private bool _initialized;

        public event Action<CombatTrainingApproachSnapshot> StateChanged;

        public CombatTrainingApproachSnapshot Snapshot => BuildSnapshot();

        private void Awake()
        {
            ResolveReferences();
            if (_attacker == null || _health == null || _body == null)
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingApproach)} requires " +
                    $"{nameof(CombatTrainingAttacker)}, {nameof(Health)} and Rigidbody.",
                    this);
                enabled = false;
                return;
            }

            try
            {
                _definition = _approach.Compile();
                _initialized = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void OnDisable()
        {
            _planarVelocity = Vector3.zero;
            _verticalSpeed = 0.0f;
            SetState(CombatTrainingApproachState.Disengaged);
        }

        private void OnDestroy()
        {
            StateChanged = null;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        private void FixedUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            float deltaTime = Mathf.Max(0.0f, Time.fixedDeltaTime);
            Vector3 up = transform.up;
            Vector3 velocity = _body.linearVelocity;
            Vector3 planarVelocity = Vector3.ProjectOnPlane(velocity, up);
            _verticalSpeed = Vector3.Dot(velocity, up);

            if (IsSteeringSuspended())
            {
                _planarVelocity = planarVelocity;
                SetState(CombatTrainingApproachState.Disengaged);
                PublishState();
                return;
            }

            CombatTrainingApproachState state = ResolveState(
                up,
                out Vector3 desiredPlanarVelocity);
            Vector3 steeredPlanarVelocity = Vector3.MoveTowards(
                planarVelocity,
                desiredPlanarVelocity,
                _definition.Acceleration * deltaTime);

            _body.linearVelocity = steeredPlanarVelocity + up * _verticalSpeed;
            _planarVelocity = steeredPlanarVelocity;
            SetState(state);
            PublishState();
        }

        private bool IsSteeringSuspended()
        {
            return !_health.IsAlive ||
                   _reactionHost != null &&
                   _reactionHost.State is { BlocksActions: true } ||
                   _defense != null && _defense.BlocksActions;
        }

        private CombatTrainingApproachState ResolveState(
            Vector3 up,
            out Vector3 desiredPlanarVelocity)
        {
            desiredPlanarVelocity = Vector3.zero;
            if (_attacker.Snapshot.Phase != CombatTrainingAttackPhase.Waiting)
            {
                return CombatTrainingApproachState.Committed;
            }

            if (!TryGetPlanarOffsetToTarget(up, out Vector3 offset))
            {
                return CombatTrainingApproachState.Disengaged;
            }

            float distance = offset.magnitude;
            if (distance <= DirectionEpsilon ||
                distance > _definition.PursuitRange)
            {
                return CombatTrainingApproachState.Disengaged;
            }

            float attackRange = _attacker.AttackRange;
            if (attackRange <= 0.0f)
            {
                return CombatTrainingApproachState.Disengaged;
            }

            Vector3 direction = offset / distance;
            float approachDistance = attackRange * _definition.ApproachRangeRatio;
            float retreatDistance = attackRange * _definition.RetreatRangeRatio;

            if (distance > approachDistance)
            {
                desiredPlanarVelocity = direction * _definition.MoveSpeed;
                return CombatTrainingApproachState.Pursuing;
            }

            if (distance < retreatDistance)
            {
                desiredPlanarVelocity = -direction * _definition.MoveSpeed;
                return CombatTrainingApproachState.Retreating;
            }

            return CombatTrainingApproachState.Holding;
        }

        private bool TryGetPlanarOffsetToTarget(Vector3 up, out Vector3 offset)
        {
            offset = Vector3.zero;
            BodyColliderRig target = _attacker.Target;
            if (target == null || !target.IsAlive || target.Owner == null)
            {
                return false;
            }

            offset = Vector3.ProjectOnPlane(
                target.Owner.transform.position - transform.position,
                up);
            return true;
        }

        private CombatTrainingApproachSnapshot BuildSnapshot()
        {
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float planarSpeed = _planarVelocity.magnitude;
            float normalizedSpeed = _definition.MoveSpeed > 0.0f
                ? Mathf.Clamp01(planarSpeed / _definition.MoveSpeed)
                : 0.0f;

            return new CombatTrainingApproachSnapshot(
                _state,
                Vector3.Dot(_planarVelocity, forward),
                Vector3.Dot(_planarVelocity, right),
                _verticalSpeed,
                normalizedSpeed,
                _impactMotor == null || _impactMotor.IsGrounded);
        }

        private void SetState(CombatTrainingApproachState state)
        {
            _state = state;
        }

        private void PublishState()
        {
            CombatTrainingApproachSnapshot snapshot = BuildSnapshot();
            SafeEvent.Publish(StateChanged, snapshot, this);
        }

        private void ResolveReferences()
        {
            if (_attacker == null)
            {
                _attacker = GetComponent<CombatTrainingAttacker>();
            }

            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_impactMotor == null)
            {
                _impactMotor = GetComponent<RigidbodyCharacterImpactMotor>();
            }

            if (_reactionHost == null)
            {
                _reactionHost = GetComponent<CharacterReactionHost>();
            }

            if (_defense == null)
            {
                _defense = GetComponent<CombatTrainingDefense>();
            }
        }
    }
}
