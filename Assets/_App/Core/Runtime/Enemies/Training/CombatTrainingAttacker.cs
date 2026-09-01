using System;
using UnityEngine;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("Project S/Enemies/Combat Training Attacker")]
    public sealed class CombatTrainingAttacker :
        MonoBehaviour,
        ICombatAttackOutcomeReceiver
    {
        private static readonly CombatActionId TrainingActionId =
            new CombatActionId("training.attack.basic");

        [SerializeField] private Health _health;
        [SerializeField] private Rigidbody _body;
        [SerializeField] private CharacterReactionHost _reactionHost;
        [SerializeField] private BodyColliderRig _targetRig;
        [SerializeField] private Transform _attackOrigin;
        [SerializeField] private bool _automatic = true;
        [SerializeField] private CombatTrainingAttackSettings _attack = new();

        private CombatTrainingAttackDefinition _definition;
        private CombatHitResolver _resolver;
        private readonly CombatAttackExecution _execution = new();
        private CombatTrainingAttackPhase _phase;
        private float _phaseDuration;
        private float _remaining;
        private ulong _nextExecutionId;
        private bool _hasConfirmedHit;
        private bool _initialized;

        public event Action<CombatTrainingAttackSnapshot> StateChanged;
        public event Action<CombatHitResult> HitConfirmed;

        public CombatTrainingAttackSnapshot Snapshot => new(
            _phase,
            CalculateNormalizedTime(),
            _hasConfirmedHit);
        public float AttackRange => _initialized ? _definition.Range : 0.0f;
        public float AttackArcDegrees =>
            _initialized ? _definition.AttackArcDegrees : 0.0f;
        public float AttackActionDuration =>
            _initialized ? _definition.ActionDuration : 0.0f;
        public float ParryStaggerDuration =>
            _initialized ? _definition.ParryStaggerDuration : 0.0f;
        public BodyColliderRig Target => _targetRig;

        private void Awake()
        {
            ResolveReferences();
            if (_health == null || _body == null || _attack == null)
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingAttacker)} requires Health, Rigidbody " +
                    "and attack settings.",
                    this);
                enabled = false;
                return;
            }

            try
            {
                _definition = _attack.Compile();
                _resolver = new CombatHitResolver(new UnityCombatClock());
                _initialized = true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            _health.Died += OnDied;
            _health.Revived += OnRevived;
            EnterPhase(CombatTrainingAttackPhase.Waiting, _definition.InitialDelay);
        }

        private void Update()
        {
            if (!_initialized || !_health.IsAlive)
            {
                return;
            }

            if (_reactionHost != null &&
                _reactionHost.State is { BlocksActions: true })
            {
                Interrupt();
                return;
            }

            float deltaTime = Mathf.Max(0.0f, Time.deltaTime);
            _remaining = Mathf.Max(0.0f, _remaining - deltaTime);

            switch (_phase)
            {
                case CombatTrainingAttackPhase.Waiting:
                    if (_automatic && _remaining <= 0.0f)
                    {
                        TryStartAttack();
                    }
                    break;
                case CombatTrainingAttackPhase.Windup:
                    TickWindup();
                    break;
                case CombatTrainingAttackPhase.Active:
                    TickActive();
                    break;
                case CombatTrainingAttackPhase.Recovery:
                    if (_remaining <= 0.0f)
                    {
                        EnterPhase(
                            CombatTrainingAttackPhase.Waiting,
                            _definition.CooldownDuration);
                    }
                    break;
                case CombatTrainingAttackPhase.ParryStagger:
                    if (_remaining <= 0.0f)
                    {
                        EnterPhase(
                            CombatTrainingAttackPhase.Waiting,
                            _definition.CooldownDuration);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            PublishState();
        }

        private void FixedUpdate()
        {
            if (!_initialized || !_health.IsAlive ||
                _reactionHost?.State is { BlocksActions: true } ||
                _phase is not (CombatTrainingAttackPhase.Waiting or
                    CombatTrainingAttackPhase.Windup))
            {
                return;
            }

            TurnTowardsTarget(Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }

            EndExecution();
            if (_initialized)
            {
                _phase = CombatTrainingAttackPhase.Waiting;
                _phaseDuration = 0.0f;
                _remaining = 0.0f;
            }
            _hasConfirmedHit = false;
            PublishState();
        }

        private void OnDestroy()
        {
            _resolver?.Dispose();
            _resolver = null;
            StateChanged = null;
            HitConfirmed = null;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        public bool TryStartAttack()
        {
            if (!_initialized || !isActiveAndEnabled || !_health.IsAlive ||
                _phase != CombatTrainingAttackPhase.Waiting || _remaining > 0.0f ||
                !CanReachTarget(_definition.Range))
            {
                return false;
            }

            EnterPhase(
                CombatTrainingAttackPhase.Windup,
                _definition.WindupDuration);
            return true;
        }

        public void ConfigureTarget(BodyColliderRig targetRig)
        {
            _targetRig = targetRig;
            if (_targetRig == null)
            {
                Interrupt();
            }
        }

        private void TickWindup()
        {
            if (!CanReachTarget(_definition.Range + _definition.WindupLeash))
            {
                Interrupt();
                return;
            }

            if (_remaining > 0.0f)
            {
                return;
            }

            BeginExecution();
            EnterPhase(
                CombatTrainingAttackPhase.Active,
                _definition.ActiveDuration);
        }

        private void TickActive()
        {
            if (!_hasConfirmedHit)
            {
                TryResolveHit();
            }

            if (_remaining > 0.0f)
            {
                return;
            }

            EndExecution();
            EnterPhase(
                CombatTrainingAttackPhase.Recovery,
                _definition.RecoveryDuration);
        }

        private void BeginExecution()
        {
            EndExecution();
            _nextExecutionId++;
            if (_nextExecutionId == 0)
            {
                _nextExecutionId = 1;
            }

            _execution.Begin(
                new AttackExecutionId(_nextExecutionId),
                TrainingActionId,
                _definition.Hit,
                gameObject,
                this);
            _hasConfirmedHit = false;
        }

        void ICombatAttackOutcomeReceiver.OnAttackOutcome(in CombatHitResult hit)
        {
            if (!_initialized || _phase != CombatTrainingAttackPhase.Active ||
                !_execution.IsActive || hit.ExecutionId != _execution.Id ||
                hit.Outcome != CombatHitOutcome.Parried)
            {
                return;
            }

            EndExecution();
            EnterPhase(
                CombatTrainingAttackPhase.ParryStagger,
                _definition.ParryStaggerDuration);
        }

        private void TryResolveHit()
        {
            if (_resolver == null || !_execution.IsActive ||
                !CanHitTarget() ||
                !TrySelectTarget(out ICombatHitTarget target))
            {
                return;
            }

            Vector3 origin = GetAttackOrigin();
            Vector3 point = target is Component component
                ? component.transform.position
                : origin + transform.forward;
            Vector3 direction = point - origin;
            var candidate = new CombatHitCandidate(target, point, direction);
            if (!_resolver.TryResolve(_execution, in candidate, out CombatHitResult hit))
            {
                return;
            }

            _hasConfirmedHit = true;
            SafeEvent.Publish(HitConfirmed, hit, this);
        }

        private bool TrySelectTarget(out ICombatHitTarget target)
        {
            target = null;
            if (_targetRig == null || !_targetRig.IsAlive)
            {
                return false;
            }

            Hurtbox fallback = null;
            var zones = _targetRig.Zones;
            for (int i = 0; i < zones.Count; i++)
            {
                Hurtbox zone = zones[i];
                if (zone == null || !zone.isActiveAndEnabled || !zone.IsAlive)
                {
                    continue;
                }

                if (zone.Zone == BodyHitZone.Root || zone.Zone == BodyHitZone.Chest)
                {
                    target = zone;
                    return true;
                }

                fallback ??= zone;
            }

            target = fallback;
            return target != null;
        }

        private bool CanReachTarget(float range)
        {
            if (_targetRig == null || !_targetRig.IsAlive ||
                _targetRig.Owner == null)
            {
                return false;
            }

            Vector3 offset = _targetRig.Owner.transform.position - GetAttackOrigin();
            return offset.sqrMagnitude <= range * range;
        }

        private bool CanHitTarget()
        {
            if (!CanReachTarget(_definition.Range) ||
                _targetRig == null || _targetRig.Owner == null)
            {
                return false;
            }

            Vector3 up = transform.up;
            Vector3 toTarget = Vector3.ProjectOnPlane(
                _targetRig.Owner.transform.position - GetAttackOrigin(),
                up);
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
            return forward.sqrMagnitude > 0.0001f &&
                   Vector3.Dot(forward.normalized, toTarget.normalized) >=
                   _definition.MinimumAttackDot;
        }

        private void TurnTowardsTarget(float deltaTime)
        {
            if (_body == null || _targetRig == null || _targetRig.Owner == null)
            {
                return;
            }

            Vector3 up = transform.up;
            Vector3 direction = Vector3.ProjectOnPlane(
                _targetRig.Owner.transform.position - transform.position,
                up);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(
                direction.normalized,
                up);
            Quaternion rotation = Quaternion.RotateTowards(
                _body.rotation,
                targetRotation,
                _definition.TurnRateDegrees * Mathf.Max(0.0f, deltaTime));
            _body.MoveRotation(rotation);
        }

        private Vector3 GetAttackOrigin()
        {
            return _attackOrigin != null ? _attackOrigin.position : transform.position;
        }

        private void Interrupt()
        {
            if (!_initialized || _phase == CombatTrainingAttackPhase.Waiting)
            {
                return;
            }

            EndExecution();
            EnterPhase(
                CombatTrainingAttackPhase.Waiting,
                _definition.CooldownDuration);
        }

        private void EndExecution()
        {
            if (_execution.IsActive)
            {
                _execution.End();
            }
        }

        private void EnterPhase(CombatTrainingAttackPhase phase, float duration)
        {
            _phase = phase;
            _phaseDuration = Mathf.Max(0.0f, duration);
            _remaining = _phaseDuration;
            if (phase != CombatTrainingAttackPhase.Active)
            {
                _hasConfirmedHit = false;
            }

            PublishState();
        }

        private float CalculateNormalizedTime()
        {
            return _phaseDuration > 0.0f
                ? 1.0f - Mathf.Clamp01(_remaining / _phaseDuration)
                : 1.0f;
        }

        private void PublishState()
        {
            CombatTrainingAttackSnapshot snapshot = Snapshot;
            SafeEvent.Publish(StateChanged, snapshot, this);
        }

        private void OnDied(CombatHitResult hit)
        {
            EndExecution();
            EnterPhase(CombatTrainingAttackPhase.Waiting, 0.0f);
        }

        private void OnRevived()
        {
            EnterPhase(CombatTrainingAttackPhase.Waiting, _definition.InitialDelay);
        }

        private void ResolveReferences()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_body == null)
            {
                _body = GetComponent<Rigidbody>();
            }

            if (_reactionHost == null)
            {
                _reactionHost = GetComponent<CharacterReactionHost>();
            }

            if (_attackOrigin == null)
            {
                _attackOrigin = transform;
            }
        }
    }
}
