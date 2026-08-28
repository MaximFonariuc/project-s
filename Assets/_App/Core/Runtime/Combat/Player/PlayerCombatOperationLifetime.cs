using System;
using System.Threading;

namespace _App
{
    internal sealed class PlayerCombatOperationLifetime
    {
        private readonly IPlayerCombatAnimation _animation;
        private readonly CombatActionSession _actionSession;
        private readonly PlayerWeaponTransitionSession _weaponTransitionSession;
        private CancellationTokenSource _activeCancellation;

        public PlayerCombatOperationLifetime(
            IPlayerCombatAnimation animation,
            CombatActionSession actionSession,
            PlayerWeaponTransitionSession weaponTransitionSession)
        {
            _animation = animation ?? throw new ArgumentNullException(nameof(animation));
            _actionSession = actionSession ?? throw new ArgumentNullException(nameof(actionSession));
            _weaponTransitionSession = weaponTransitionSession ??
                throw new ArgumentNullException(nameof(weaponTransitionSession));
        }

        public CancellationTokenSource Begin()
        {
            Cancel(CombatActionEndReason.Cancelled);

            var cancellation = new CancellationTokenSource();
            _activeCancellation = cancellation;
            return cancellation;
        }

        public bool TryComplete(CancellationTokenSource cancellation)
        {
            if (cancellation == null)
            {
                throw new ArgumentNullException(nameof(cancellation));
            }

            if (!ReferenceEquals(_activeCancellation, cancellation))
            {
                return false;
            }

            _activeCancellation = null;
            cancellation.Dispose();
            return true;
        }

        public void Cancel(CombatActionEndReason reason)
        {
            try
            {
                _animation.Cancel();
            }
            finally
            {
                CancelOperation(reason);
            }
        }

        private void CancelOperation(CombatActionEndReason reason)
        {
            CancellationTokenSource cancellation = _activeCancellation;
            _activeCancellation = null;

            try
            {
                cancellation?.Cancel();
            }
            finally
            {
                try
                {
                    _actionSession.Abort(reason);
                }
                finally
                {
                    try
                    {
                        _weaponTransitionSession.Abort();
                    }
                    finally
                    {
                        cancellation?.Dispose();
                    }
                }
            }
        }
    }
}
