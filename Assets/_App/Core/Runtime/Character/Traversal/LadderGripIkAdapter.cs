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
        [SerializeField, Min(0.01f)] private float _maxCorrection = 0.14f;
        [SerializeField, Min(0.01f)] private float _plantSpeed = 0.8f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.12f;

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
                planted = speed <= _plantSpeed;
                if (planted && !limb.HasTarget)
                {
                    limb.Target = ladder.NearestRungPoint(animated);
                    limb.HasTarget = true;
                }
                else if (!planted)
                {
                    limb.HasTarget = false;
                }
            }
            else
            {
                limb.HasTarget = false;
            }

            limb.PreviousPosition = animated;
            limb.HasPrevious = ladder != null;
            limb.Weight = Mathf.MoveTowards(limb.Weight, planted ? 1.0f : 0.0f, weightStep);
            _limbs[index] = limb;

            float limbWeight = index < HandGoalCount ? _handWeight : _footWeight;
            _animator.SetIKPositionWeight(goal, limb.Weight * limbWeight);
            if (limb.Weight <= 0.0f)
            {
                return;
            }

            Vector3 correction = limb.Target - animated;
            if (correction.sqrMagnitude > _maxCorrection * _maxCorrection)
            {
                correction = correction.normalized * _maxCorrection;
            }

            _animator.SetIKPosition(goal, animated + correction);
        }
    }
}
