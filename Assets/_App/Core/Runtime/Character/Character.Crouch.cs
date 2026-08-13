using UnityEngine;

namespace _App
{
    public partial class Character
    {
        public virtual bool IsCrouched()
        {
            return _isCrouched;
        }

        public virtual void Crouch()
        {
            CrouchInputPressed = true;
        }

        public virtual void UnCrouch()
        {
            CrouchInputPressed = false;
        }

        protected virtual bool IsCrouchAllowed()
        {
            return CanEverCrouch && IsWalking();
        }

        protected virtual bool CanUnCrouch()
        {
            bool overlapped = CharacterMovement.CheckHeight(_unCrouchedHeight);
            return !overlapped;
        }

        protected virtual void CheckCrouchInput()
        {
            if (!_isCrouched && CrouchInputPressed && IsCrouchAllowed())
            {
                _isCrouched = true;
                CharacterMovement.SetHeight(_crouchedHeight);

                OnCrouched();
            }
            else if (_isCrouched && (!CrouchInputPressed || !IsCrouchAllowed()))
            {
                if (!CanUnCrouch())
                    return;

                _isCrouched = false;
                CharacterMovement.SetHeight(_unCrouchedHeight);

                OnUnCrouched();
            }
        }

    }
}
