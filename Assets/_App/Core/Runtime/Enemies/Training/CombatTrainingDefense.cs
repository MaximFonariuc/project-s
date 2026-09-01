using System;
using UnityEngine;

namespace _App
{
    public enum CombatTrainingDefenseMode
    {
        Vulnerable = 0,
        Block = 1,
        Parry = 2,
        Cycle = 3
    }

    public enum CombatTrainingDefenseState
    {
        Vulnerable = 0,
        Block = 1,
        Parry = 2
    }

    public readonly struct CombatTrainingDefenseSnapshot
    {
        public CombatTrainingDefenseState State { get; }
        public float RemainingDuration { get; }
        public CombatHitResult Hit { get; }
        public bool BlocksActions => State != CombatTrainingDefenseState.Vulnerable;

        internal CombatTrainingDefenseSnapshot(
            CombatTrainingDefenseState state,
            float remainingDuration,
            in CombatHitResult hit)
        {
            State = state;
            RemainingDuration = Mathf.Max(0.0f, remainingDuration);
            Hit = hit;
        }
    }

    /// <summary>
    /// Deterministic receiving policy for the combat training target. It is a
    /// scene harness for damage/block/parry validation, not enemy decision AI.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(BodyColliderRig))]
    [AddComponentMenu("Project S/Enemies/Combat Training Defense")]
    public sealed class CombatTrainingDefense :
        MonoBehaviour,
        ICombatHitReceiver
    {
        private const float DirectionThresholdSqr = 0.0001f;

        [SerializeField] private Health _health;
        [SerializeField] private BodyColliderRig _bodyRig;
        [SerializeField] private CombatTrainingAttacker _attacker;
        [SerializeField] private CombatTrainingDefenseMode _mode =
            CombatTrainingDefenseMode.Cycle;
        [SerializeField, Range(1.0f, 360.0f)] private float _guardArcDegrees = 180.0f;
        [SerializeField, Min(0.01f)] private float _blockDuration = 0.50f;
        [SerializeField, Min(0.01f)] private float _parryDuration = 0.75f;

        private CombatTrainingDefenseSnapshot _snapshot;
        private float _minimumGuardDot;
        private int _cycleIndex;
        private bool _initialized;

        public event Action<CombatTrainingDefenseSnapshot> StateChanged;
        public event Action<CombatHitResult> Defended;

        public CombatTrainingDefenseMode Mode => _mode;
        public CombatTrainingDefenseSnapshot Snapshot => _snapshot;
        public bool BlocksActions => _snapshot.BlocksActions;

        private void Awake()
        {
            ResolveReferences();
            if (_health == null || _bodyRig == null)
            {
                Debug.LogError(
                    $"{nameof(CombatTrainingDefense)} requires Health and BodyColliderRig.",
                    this);
                enabled = false;
                return;
            }

            RecalculateGuardDot();
            _initialized = true;
        }

        private void OnEnable()
        {
            if (!_initialized)
            {
                return;
            }

            try
            {
                _bodyRig.SetHitReceiver(this);
                _health.Died += OnDied;
                _health.Revived += OnRevived;
                ResetState(resetCycle: true);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
            }
        }

        private void Update()
        {
            if (!_initialized || !BlocksActions)
            {
                return;
            }

            float remaining = Mathf.Max(
                0.0f,
                _snapshot.RemainingDuration - Mathf.Max(0.0f, Time.deltaTime));
            if (remaining <= 0.0f)
            {
                ResetState(resetCycle: false);
                return;
            }

            CombatHitResult activeHit = _snapshot.Hit;
            _snapshot = new CombatTrainingDefenseSnapshot(
                _snapshot.State,
                remaining,
                in activeHit);
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= OnDied;
                _health.Revived -= OnRevived;
            }

            if (_bodyRig != null)
            {
                _bodyRig.ClearHitReceiver(this);
            }

            ResetState(resetCycle: true);
        }

        private void OnDestroy()
        {
            StateChanged = null;
            Defended = null;
        }

        private void Reset()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
            _guardArcDegrees = Mathf.Clamp(_guardArcDegrees, 1.0f, 360.0f);
            _blockDuration = Mathf.Max(0.01f, _blockDuration);
            _parryDuration = Mathf.Max(0.01f, _parryDuration);
            RecalculateGuardDot();
        }

        bool ICombatHitReceiver.TryReceiveHit(
            in CombatHitResult proposedHit,
            out CombatHitResult resolvedHit)
        {
            resolvedHit = default;
            if (!_initialized || !isActiveAndEnabled || !_health.IsAlive ||
                proposedHit.Outcome != CombatHitOutcome.Damaged)
            {
                return false;
            }

            CombatHitOutcome outcome = ResolveOutcome(in proposedHit);
            if (outcome == CombatHitOutcome.Damaged)
            {
                if (!_health.TryApplyHit(in proposedHit))
                {
                    return false;
                }

                resolvedHit = proposedHit;
                AdvanceCycle();
                return true;
            }

            resolvedHit = proposedHit.AsDefenseOutcome(outcome);
            AdvanceCycle();
            _attacker?.InterruptForDefense();
            EnterDefense(in resolvedHit);
            SafeEvent.Publish(Defended, resolvedHit, this);
            return true;
        }

        private CombatHitOutcome ResolveOutcome(in CombatHitResult hit)
        {
            if (!IsWithinGuardArc(in hit))
            {
                return CombatHitOutcome.Damaged;
            }

            return _mode switch
            {
                CombatTrainingDefenseMode.Block => CombatHitOutcome.Blocked,
                CombatTrainingDefenseMode.Parry => CombatHitOutcome.Parried,
                CombatTrainingDefenseMode.Cycle => (_cycleIndex % 3) switch
                {
                    1 => CombatHitOutcome.Blocked,
                    2 => CombatHitOutcome.Parried,
                    _ => CombatHitOutcome.Damaged
                },
                _ => CombatHitOutcome.Damaged
            };
        }

        private bool IsWithinGuardArc(in CombatHitResult hit)
        {
            if (_mode == CombatTrainingDefenseMode.Vulnerable)
            {
                return false;
            }

            Vector3 up = transform.up;
            Vector3 toSource = hit.Source != null
                ? hit.Source.transform.position - transform.position
                : -hit.Direction;
            toSource = Vector3.ProjectOnPlane(toSource, up);
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, up);
            if (toSource.sqrMagnitude <= DirectionThresholdSqr ||
                forward.sqrMagnitude <= DirectionThresholdSqr)
            {
                return false;
            }

            return Vector3.Dot(forward.normalized, toSource.normalized) >=
                   _minimumGuardDot;
        }

        private void EnterDefense(in CombatHitResult hit)
        {
            CombatTrainingDefenseState state = hit.Outcome == CombatHitOutcome.Parried
                ? CombatTrainingDefenseState.Parry
                : CombatTrainingDefenseState.Block;
            float duration = state == CombatTrainingDefenseState.Parry
                ? _parryDuration
                : _blockDuration;
            _snapshot = new CombatTrainingDefenseSnapshot(state, duration, in hit);
            SafeEvent.Publish(StateChanged, _snapshot, this);
        }

        private void ResetState(bool resetCycle)
        {
            bool changed = _snapshot.State != CombatTrainingDefenseState.Vulnerable;
            CombatHitResult noHit = default;
            _snapshot = new CombatTrainingDefenseSnapshot(
                CombatTrainingDefenseState.Vulnerable,
                0.0f,
                in noHit);
            if (resetCycle)
            {
                _cycleIndex = 0;
            }

            if (changed)
            {
                SafeEvent.Publish(StateChanged, _snapshot, this);
            }
        }

        private void AdvanceCycle()
        {
            if (_mode == CombatTrainingDefenseMode.Cycle)
            {
                _cycleIndex = (_cycleIndex + 1) % 3;
            }
        }

        private void OnDied(CombatHitResult hit)
        {
            ResetState(resetCycle: true);
        }

        private void OnRevived()
        {
            ResetState(resetCycle: true);
        }

        private void ResolveReferences()
        {
            if (_health == null)
            {
                _health = GetComponent<Health>();
            }

            if (_bodyRig == null)
            {
                _bodyRig = GetComponent<BodyColliderRig>();
            }

            if (_attacker == null)
            {
                _attacker = GetComponent<CombatTrainingAttacker>();
            }
        }

        private void RecalculateGuardDot()
        {
            _minimumGuardDot = Mathf.Cos(
                Mathf.Clamp(_guardArcDegrees, 1.0f, 360.0f) *
                0.5f * Mathf.Deg2Rad);
        }
    }
}
