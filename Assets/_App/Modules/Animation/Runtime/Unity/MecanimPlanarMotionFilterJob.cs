using UnityEngine;
using UnityEngine.Animations;

namespace ModularAnimation.Unity
{
    internal struct MecanimPlanarMotionFilterJob : IAnimationJob
    {
        private TransformStreamHandle _motionRoot;
        private Vector3 _referenceLocalPosition;

        public MecanimPlanarMotionFilterJob(
            TransformStreamHandle motionRoot,
            Vector3 referenceLocalPosition)
        {
            _motionRoot = motionRoot;
            _referenceLocalPosition = referenceLocalPosition;
        }

        public void ProcessAnimation(AnimationStream stream)
        {
            if (!_motionRoot.IsValid(stream))
            {
                return;
            }

            Vector3 position = _motionRoot.GetLocalPosition(stream);
            position.x = _referenceLocalPosition.x;
            position.z = _referenceLocalPosition.z;
            _motionRoot.SetLocalPosition(stream, position);
        }

        public void ProcessRootMotion(AnimationStream stream)
        {
        }
    }
}
