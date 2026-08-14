using System;
using ModularAnimation.Unity;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace _App
{
    [Serializable]
    [MovedFrom(true, sourceNamespace: "_App", sourceClassName: "CombatAnimationPresentation")]
    public sealed class CharacterAnimationActionPresentation
    {
        [FormerlySerializedAs("<Id>k__BackingField")]
        [SerializeField] private string _id;

        [FormerlySerializedAs("<AnimationClip>k__BackingField")]
        [SerializeField] private AnimationClip _clip;

        [SerializeField, Min(0.0f)] private float _blendInSeconds = 0.08f;
        [SerializeField, Min(0.0f)] private float _blendOutSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float _playbackSpeed = 1.0f;
        [SerializeField] private AvatarMask _avatarMask;
        [SerializeField] private bool _applyFootIk;

        public string Id => _id;
        public AnimationClip Clip => _clip;
        public float BlendOutSeconds => Mathf.Max(0.0f, _blendOutSeconds);
        public bool IsValid =>
            _clip != null && _clip.length > 0.0f && _playbackSpeed > 0.0f;

        public AnimationClipActionRequest CreateRequest()
        {
            return new AnimationClipActionRequest(
                _clip,
                _blendInSeconds,
                _playbackSpeed,
                _avatarMask,
                _applyFootIk);
        }
    }
}
