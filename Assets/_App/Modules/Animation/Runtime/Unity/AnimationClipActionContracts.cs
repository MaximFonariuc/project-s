using UnityEngine;

namespace ModularAnimation.Unity
{
    public readonly struct AnimationClipActionRequest
    {
        public AnimationClip Clip { get; }
        public AvatarMask AvatarMask { get; }
        public float BlendSeconds { get; }
        public float PlaybackSpeed { get; }
        public bool ApplyFootIk { get; }

        public bool IsValid => Clip != null && Clip.length > 0.0f && PlaybackSpeed > 0.0f;

        public AnimationClipActionRequest(
            AnimationClip clip,
            float blendSeconds,
            float playbackSpeed = 1.0f,
            AvatarMask avatarMask = null,
            bool applyFootIk = false)
        {
            Clip = clip;
            AvatarMask = avatarMask;
            BlendSeconds = Mathf.Max(0.0f, blendSeconds);
            PlaybackSpeed = Mathf.Max(0.0f, playbackSpeed);
            ApplyFootIk = applyFootIk;
        }
    }

    public interface IAnimationClipActionDriver
    {
        bool IsActionPlaying { get; }

        AnimationActionPlayback PlayAction(in AnimationClipActionRequest request);
        void StopAction(float blendSeconds);
        void Tick(float deltaTime);
    }
}
