using System;
using ModularAnimation;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public readonly struct AnimationActionChannel : IEquatable<AnimationActionChannel>
    {
        public int Id { get; }
        public int Priority { get; }

        public bool IsValid => Id != 0;

        public AnimationActionChannel(int id, int priority)
        {
            if (id == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            Id = id;
            Priority = priority;
        }

        public bool Equals(AnimationActionChannel other) =>
            Id == other.Id;

        public override bool Equals(object obj) =>
            obj is AnimationActionChannel other && Equals(other);

        public override int GetHashCode() => Id;
        public static bool operator ==(AnimationActionChannel left, AnimationActionChannel right) =>
            left.Equals(right);
        public static bool operator !=(AnimationActionChannel left, AnimationActionChannel right) =>
            !left.Equals(right);
    }

    public readonly struct AnimationActionLease
    {
        private readonly AnimationClipActionArbiter _owner;
        private readonly ulong _token;

        public AnimationActionPlayback Playback { get; }
        public bool IsActive =>
            Playback.IsValid && _owner != null && _owner.IsActive(_token);

        internal AnimationActionLease(
            AnimationClipActionArbiter owner,
            ulong token,
            AnimationActionPlayback playback)
        {
            _owner = owner;
            _token = token;
            Playback = playback;
        }

        public void Release()
        {
            _owner?.Release(_token);
        }
    }

    public sealed class AnimationClipActionArbiter
    {
        private readonly IAnimationClipActionDriver _driver;

        private AnimationActionChannel _activeChannel;
        private ulong _activeToken;
        private ulong _nextToken;
        private float _activeBlendOutSeconds;

        public bool HasActiveAction => _activeToken != 0;
        public AnimationActionChannel? ActiveChannel =>
            HasActiveAction ? _activeChannel : null;

        public AnimationClipActionArbiter(IAnimationClipActionDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public bool TryPlay(
            AnimationActionChannel channel,
            in AnimationClipActionRequest request,
            float blendOutSeconds,
            out AnimationActionLease lease)
        {
            if (!channel.IsValid || !request.IsValid ||
                (HasActiveAction && _activeChannel.Priority > channel.Priority))
            {
                lease = default;
                return false;
            }

            AnimationActionPlayback playback = _driver.PlayAction(in request);
            if (!playback.IsValid || playback.Duration <= 0.0f)
            {
                lease = default;
                return false;
            }

            _nextToken++;
            if (_nextToken == 0)
            {
                _nextToken = 1;
            }

            _activeChannel = channel;
            _activeToken = _nextToken;
            _activeBlendOutSeconds = Mathf.Max(0.0f, blendOutSeconds);
            lease = new AnimationActionLease(this, _activeToken, playback);
            return true;
        }

        public void Release(AnimationActionChannel channel)
        {
            if (HasActiveAction && _activeChannel == channel)
            {
                StopCurrent(_activeBlendOutSeconds);
            }
        }

        public void StopAll(float blendSeconds = 0.0f)
        {
            if (HasActiveAction || _driver.IsActionPlaying)
            {
                StopCurrent(blendSeconds);
            }
        }

        public void Tick(float deltaTime)
        {
            _driver.Tick(Mathf.Max(0.0f, deltaTime));
        }

        internal bool IsActive(ulong token) => token != 0 && token == _activeToken;

        internal void Release(ulong token)
        {
            if (IsActive(token))
            {
                StopCurrent(_activeBlendOutSeconds);
            }
        }

        private void StopCurrent(float blendSeconds)
        {
            _activeChannel = default;
            _activeToken = 0;
            _activeBlendOutSeconds = 0.0f;
            _driver.StopAction(Mathf.Max(0.0f, blendSeconds));
        }
    }
}
