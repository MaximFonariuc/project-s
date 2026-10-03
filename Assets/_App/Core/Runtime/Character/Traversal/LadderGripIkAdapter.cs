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
        [SerializeField, Min(0.01f)] private float _maxCorrection = 0.06f;
        [SerializeField, Min(0.01f)] private float _plantSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float _latchDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.12f;
        [SerializeField] private float _handHeightAboveRung = 0.028f;
        [SerializeField] private float _handDepthFromRung = 0.008f;
        [SerializeField] private float _footHeightAboveRung = 0.127f;
        [SerializeField] private float _footDepthFromRung = -0.041f;

        private readonly LimbGrip[] _limbs = new LimbGrip[Goals.Length];

        private Animator _animator;
        private IPlayerLadderGrip _grip;

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
                ApplyGoal(i, ladder, deltaTime, weightStep);
            }
        }

        private void ApplyGoal(int index, Ladder ladder, float deltaTime, float weightStep)
        {
            AvatarIKGoal goal = Goals[index];
            Vector3 animated = _animator.GetIKPosition(goal);
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

            limb.PreviousPosition = animated;
            limb.HasPrevious = ladder != null;
            limb.Weight = Mathf.MoveTowards(limb.Weight, planted ? 1.0f : 0.0f, weightStep);
            _limbs[index] = limb;

            float limbWeight = index < HandGoalCount ? _handWeight : _footWeight;
            _animator.SetIKPositionWeight(goal, limb.Weight * limbWeight);
            if (limb.Weight > 0.0f)
            {
                _animator.SetIKPosition(goal, animated + limb.Correction);
            }
        }

        private Vector3 ResolveRungOffset(Ladder ladder, int index)
        {
            bool hand = index < HandGoalCount;
            Vector3 up = ladder.ClimbAxis.normalized;
            Vector3 forward = ladder.ClimbRotation * Vector3.forward;
            return up * (hand ? _handHeightAboveRung : _footHeightAboveRung) +
                forward * (hand ? _handDepthFromRung : _footDepthFromRung);
        }
    }
}
