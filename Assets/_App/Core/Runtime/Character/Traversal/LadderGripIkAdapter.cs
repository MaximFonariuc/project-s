using UnityEngine;
using VContainer;

namespace _App
{
    [RequireComponent(typeof(Animator))]
    public sealed class LadderGripIkAdapter : MonoBehaviour
    {
        private struct LimbGrip
        {
            public Vector3 PreviousPosition;
            public Vector3 Target;
            public Vector3 Correction;
            public float Weight;
            public bool HasPrevious;
            public bool HasTarget;
        }

        private static readonly AvatarIKGoal[] Goals =
        {
            AvatarIKGoal.LeftHand,
            AvatarIKGoal.RightHand,
            AvatarIKGoal.LeftFoot,
            AvatarIKGoal.RightFoot
        };

        private const int HandGoalCount = 2;

        [SerializeField, Range(0.0f, 1.0f)] private float _handWeight = 0.9f;
        [SerializeField, Range(0.0f, 1.0f)] private float _footWeight = 0.75f;
        [SerializeField, Min(0.01f)] private float _maxCorrection = 0.09f;
        [SerializeField, Min(0.01f)] private float _plantSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float _latchDistance = 0.1f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.12f;
        [SerializeField] private float _leftHandHeightAboveRung = 0.03f;
        [SerializeField] private float _rightHandHeightAboveRung = 0.01f;
        [SerializeField] private float _handDepthFromRung = -0.05f;
        [SerializeField] private float _footHeightAboveRung = 0.06f;
        [SerializeField] private float _footDepthFromRung = -0.094f;
        [SerializeField] private AvatarIKGoal _baseFootGoal = AvatarIKGoal.LeftFoot;
        [SerializeField] private Vector3 _baseFootEntryOffset = new Vector3(-0.123f, 0.071f, 0.04f);
        [SerializeField] private Vector3 _baseFootClimbOffset = new Vector3(-0.059f, 0.085f, 0.171f);
        [SerializeField, Min(0.0f)] private float _baseStepStartCycles = 0.1f;
        [SerializeField, Min(0.0f)] private float _baseStepEndCycles = 0.22f;
        [SerializeField, Min(0.0f)] private float _bodyStandoff = 0.05f;
        [SerializeField, Min(0.0f)] private float _bodyDrop = 0.03f;
        [SerializeField, Min(0.01f)] private float _standoffBlendSeconds = 0.25f;

        private readonly LimbGrip[] _limbs = new LimbGrip[Goals.Length];
        private readonly Vector3[] _animated = new Vector3[Goals.Length];

        private Animator _animator;
        private IPlayerLadderGrip _grip;
        private Vector3 _baseFootSpot;
        private Vector3 _baseFootShift;
        private Vector3 _standoffDirection;
        private Vector3 _dropDirection;
        private float _baseFootBlend;
        private float _baseFootAnchor;
        private float _baseFootWeight;
        private float _standoffWeight;
        private float _standoffHold;
        private float _fullHold;

        [Inject]
        public void Construct(IPlayerLadderGrip grip)
        {
            _grip = grip;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnDisable()
        {
            for (int i = 0; i < _limbs.Length; i++)
            {
                _limbs[i] = default;
            }

            _baseFootBlend = 0.0f;
            _baseFootAnchor = 0.0f;
            _baseFootWeight = 0.0f;
            _standoffWeight = 0.0f;
            _standoffHold = 0.0f;
            _fullHold = 0.0f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || layerIndex != 0)
            {
                return;
            }

            bool active = _grip != null && _grip.IsGripActive;
            Ladder ladder = active ? _grip.ActiveLadder : null;
            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float weightStep = deltaTime / Mathf.Max(0.01f, _blendSeconds);
            for (int i = 0; i < Goals.Length; i++)
            {
                _animated[i] = _animator.GetIKPosition(Goals[i]);
            }

            UpdateBaseFoot(weightStep);
            UpdateBodyOffset(deltaTime);
            bool allHeld = ladder != null;
            for (int i = 0; i < Goals.Length; i++)
            {
                ApplyGoal(i, ladder, deltaTime, weightStep);
                allHeld &= _limbs[i].HasTarget;
            }

            _fullHold = Mathf.MoveTowards(_fullHold, allHeld ? 1.0f : 0.0f, weightStep);
        }

        private void UpdateBodyOffset(float deltaTime)
        {
            bool offset = _bodyStandoff > 0.0f || _bodyDrop > 0.0f;
            bool hanging = offset && _grip != null && _grip.IsHanging;
            if (hanging)
            {
                Ladder ladder = _grip.ActiveLadder;
                _standoffDirection = ladder.ClimbRotation * Vector3.forward;
                _dropDirection = ladder.ClimbAxis.normalized;
            }

            _standoffWeight = Mathf.MoveTowards(
                _standoffWeight,
                hanging ? 1.0f : 0.0f,
                deltaTime / Mathf.Max(0.01f, _standoffBlendSeconds));
            _standoffHold = Mathf.SmoothStep(0.0f, 1.0f, _standoffWeight);
            if (_standoffHold <= 0.0f)
            {
                return;
            }

            Vector3 drop = _dropDirection * (_bodyDrop * _standoffHold);
            _animator.bodyPosition -= _standoffDirection * (_bodyStandoff * _standoffHold) + drop;
            for (int i = 0; i < Goals.Length; i++)
            {
                float share = Goals[i] == _baseFootGoal ? 1.0f - _baseFootWeight : 1.0f;
                _animated[i] -= drop * share;
            }
        }

        private void UpdateBaseFoot(float weightStep)
        {
            Ladder ladder = _grip?.ActiveLadder;
            bool transition = ladder != null && _grip.IsBaseTransition;
            float blend = ladder != null ? ResolveBaseFootBlend(transition) : 0.0f;
            if (blend > 0.0f)
            {
                Quaternion rotation = ladder.ClimbRotation;
                _baseFootSpot = ladder.BottomAnchorPoint + rotation * _baseFootEntryOffset;
                _baseFootShift = rotation * (_baseFootEntryOffset - _baseFootClimbOffset);
                _baseFootBlend = blend;
                _baseFootAnchor = transition
                    ? 1.0f
                    : Mathf.MoveTowards(_baseFootAnchor, 0.0f, weightStep);
            }

            _baseFootWeight = Mathf.MoveTowards(
                _baseFootWeight,
                blend > 0.0f ? 1.0f : 0.0f,
                weightStep);
        }

        private float ResolveBaseFootBlend(bool transition)
        {
            if (transition)
            {
                return 1.0f;
            }

            if (!_grip.IsGripActive)
            {
                return 0.0f;
            }

            float step = Mathf.InverseLerp(
                _baseStepStartCycles,
                _baseStepEndCycles,
                _grip.ClimbCycles);
            return 1.0f - Mathf.SmoothStep(0.0f, 1.0f, step);
        }

        private void ApplyGoal(int index, Ladder ladder, float deltaTime, float weightStep)
        {
            AvatarIKGoal goal = Goals[index];
            Vector3 animated = _animated[index];
            LimbGrip limb = _limbs[index];
            bool planted = false;

            if (ladder != null)
            {
                float speed = limb.HasPrevious
                    ? (animated - limb.PreviousPosition).magnitude / deltaTime
                    : 0.0f;
                bool resting = speed <= _plantSpeed;
                if (limb.HasTarget)
                {
                    planted = resting;
                    limb.HasTarget = resting;
                }
                else if (resting && limb.Weight <= 0.0f)
                {
                    Vector3 grip = ResolveRungOffset(ladder, index);
                    Vector3 rung = ladder.NearestRungPoint(animated - grip) + grip;
                    if ((rung - animated).sqrMagnitude <= _latchDistance * _latchDistance)
                    {
                        limb.Target = rung;
                        limb.HasTarget = true;
                        planted = true;
                    }
                }
            }
            else
            {
                limb.HasTarget = false;
            }

            if (planted)
            {
                Vector3 correction = limb.Target - animated;
                if (correction.sqrMagnitude > _maxCorrection * _maxCorrection)
                {
                    correction = correction.normalized * _maxCorrection;
                }

                limb.Correction = correction;
            }
            else if (limb.Weight <= 0.0f)
            {
                limb.Correction = Vector3.MoveTowards(
                    limb.Correction,
                    Vector3.zero,
                    _maxCorrection * weightStep);
            }

            limb.PreviousPosition = animated;
            limb.HasPrevious = ladder != null;
            limb.Weight = Mathf.MoveTowards(limb.Weight, planted ? 1.0f : 0.0f, weightStep);
            _limbs[index] = limb;

            float limbWeight = Mathf.Lerp(
                index < HandGoalCount ? _handWeight : _footWeight,
                1.0f,
                _fullHold);
            float gripWeight = limb.Weight * limbWeight;
            Vector3 gripped = animated + limb.Correction;
            Vector3 held = animated + limb.Correction * gripWeight;
            float hold = _standoffHold;
            if (goal == _baseFootGoal && _baseFootWeight > 0.0f)
            {
                Vector3 based = Vector3.Lerp(
                    held + _baseFootShift * _baseFootBlend,
                    _baseFootSpot,
                    _baseFootAnchor);
                held = Vector3.Lerp(held, based, _baseFootWeight);
                hold = Mathf.Max(hold, _baseFootWeight);
            }

            if (hold <= 0.0f)
            {
                _animator.SetIKPositionWeight(goal, gripWeight);
                if (limb.Weight > 0.0f)
                {
                    _animator.SetIKPosition(goal, gripped);
                }

                return;
            }

            _animator.SetIKPositionWeight(goal, Mathf.Lerp(gripWeight, 1.0f, hold));
            _animator.SetIKPosition(goal, Vector3.Lerp(gripped, held, hold));
        }

        private Vector3 ResolveRungOffset(Ladder ladder, int index)
        {
            bool hand = index < HandGoalCount;
            Vector3 up = ladder.ClimbAxis.normalized;
            Vector3 forward = ladder.ClimbRotation * Vector3.forward;
            return up * ResolveRungHeight(Goals[index])
                + forward * (hand ? _handDepthFromRung : _footDepthFromRung);
        }

        private float ResolveRungHeight(AvatarIKGoal goal)
        {
            return goal switch
            {
                AvatarIKGoal.LeftHand => _leftHandHeightAboveRung,
                AvatarIKGoal.RightHand => _rightHandHeightAboveRung,
                _ => _footHeightAboveRung
            };
        }
    }
}
