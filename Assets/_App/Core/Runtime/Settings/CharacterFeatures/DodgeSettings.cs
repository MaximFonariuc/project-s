using ModularAnimation.Unity;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "DodgeSettings", menuName = "Project S/Character/Dodge")]
    public sealed class DodgeSettings : CharacterFeatureSettings
    {
        [SerializeField] private bool _canEverDodge = true;
        [SerializeField] private float _dodgeImpulse = 10.0f;
        [SerializeField, Min(0.01f)] private float _dodgeDuration = 0.35f;

        [Header("Animation-Derived Motion")]
        [SerializeField] private AnimationMotionTrajectory _forwardMotionTrajectory;
        [SerializeField] private AnimationMotionTrajectory _backwardMotionTrajectory;
        [SerializeField, Min(0.0f)] private float _motionTrajectoryScale = 1.0f;

        public bool IsEnabled => _canEverDodge;
        public float Impulse => _dodgeImpulse;
        public float Duration => Mathf.Max(0.01f, _dodgeDuration);
        public AnimationMotionTrajectory ForwardMotionTrajectory => _forwardMotionTrajectory;
        public AnimationMotionTrajectory BackwardMotionTrajectory => _backwardMotionTrajectory;
        public float MotionTrajectoryScale => Mathf.Max(0.0f, _motionTrajectoryScale);
    }
}
