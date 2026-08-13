using System;

namespace _App
{
    public sealed class PlayerPhysicsVolumeAnimationAdapter : IDisposable
    {
        private readonly Character _character;
        private readonly AnimationController _animation;

        private bool _enabled;

        public PlayerPhysicsVolumeAnimationAdapter(
            Character character,
            AnimationController animation)
        {
            _character = character != null
                ? character
                : throw new ArgumentNullException(nameof(character));
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
        }

        public void Enable()
        {
            if (_enabled)
            {
                return;
            }

            _character.PhysicsVolumeChanged += _animation.OnPhysicsVolumeChanged;
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled)
            {
                return;
            }

            _character.PhysicsVolumeChanged -= _animation.OnPhysicsVolumeChanged;
            _enabled = false;
        }

        public void Dispose()
        {
            Disable();
        }
    }
}
