using UnityEngine;
using VContainer;

namespace _App
{
    [RequireComponent(typeof(Animator))]
    public sealed class LadderGripIkAdapter : MonoBehaviour
    {
        [SerializeField, Range(0.0f, 1.0f)] private float _handWeight = 0.85f;
        [SerializeField, Range(0.0f, 1.0f)] private float _footWeight = 0.7f;
        [SerializeField, Min(0.01f)] private float _maxCorrection = 0.18f;
        [SerializeField, Min(0.01f)] private float _blendSeconds = 0.15f;

        private Animator _animator;
        private IPlayerLadderGrip _grip;
        private float _weight;

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
            _weight = 0.0f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_animator == null || layerIndex != 0)
            {
                return;
            }

            bool active = _grip != null && _grip.IsGripActive && _grip.ActiveLadder != null;
            float target = active ? 1.0f : 0.0f;
            _weight = Mathf.MoveTowards(
                _weight,
                target,
                Time.deltaTime / Mathf.Max(0.01f, _blendSeconds));

            if (_weight <= 0.001f)
            {
                ClearGoals();
                return;
            }

            Ladder ladder = _grip.ActiveLadder;
            ApplyGoal(AvatarIKGoal.LeftHand, ladder, _handWeight);
            ApplyGoal(AvatarIKGoal.RightHand, ladder, _handWeight);
            ApplyGoal(AvatarIKGoal.LeftFoot, ladder, _footWeight);
            ApplyGoal(AvatarIKGoal.RightFoot, ladder, _footWeight);
        }

        private void ApplyGoal(AvatarIKGoal goal, Ladder ladder, float limbWeight)
        {
            Vector3 animated = _animator.GetIKPosition(goal);
            Vector3 rung = ladder.NearestRungPoint(animated);
            Vector3 correction = rung - animated;
            if (correction.sqrMagnitude > _maxCorrection * _maxCorrection)
            {
                correction = correction.normalized * _maxCorrection;
            }

            _animator.SetIKPositionWeight(goal, _weight * limbWeight);
            _animator.SetIKPosition(goal, animated + correction);
        }

        private void ClearGoals()
        {
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, 0.0f);
            _animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, 0.0f);
        }
    }
}
