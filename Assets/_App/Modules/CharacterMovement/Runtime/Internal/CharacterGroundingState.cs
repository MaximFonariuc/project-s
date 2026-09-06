using UnityEngine;

namespace ModularCharacterMovement
{
    internal sealed class CharacterGroundingState
    {
        private bool _constrainToGround = true;
        private float _unconstrainedTimer;
        private FindGroundResult _foundGround;
        private FindGroundResult _currentGround;

        public bool ConstrainToGround
        {
            get => _constrainToGround;
            set => _constrainToGround = value;
        }

        public bool IsConstrained => _constrainToGround && _unconstrainedTimer == 0.0f;

        public bool IsConstraintPaused => _constrainToGround && _unconstrainedTimer > 0.0f;

        public float UnconstrainedTimer => _unconstrainedTimer;

        public bool WasOnGround { get; private set; }

        public bool WasOnWalkableGround { get; private set; }

        public bool WasGrounded { get; private set; }

        public bool IsOnGround => _currentGround.HitGround;

        public bool IsOnWalkableGround => _currentGround.IsWalkableGround;

        public bool IsGrounded => IsOnWalkableGround && IsConstrained;

        public bool HasLanded { get; private set; }

        public Vector3 LandedVelocity { get; private set; }

        public ref FindGroundResult FoundGround => ref _foundGround;

        public ref FindGroundResult CurrentGround => ref _currentGround;

        public void BeginMove()
        {
            HasLanded = false;
            _foundGround = default;
        }

        public void RecordLanding(Vector3 velocity)
        {
            HasLanded = true;
            LandedVelocity = velocity;
        }

        public void UpdateCurrent(ref FindGroundResult groundResult)
        {
            WasOnGround = IsOnGround;
            WasOnWalkableGround = IsOnWalkableGround;
            WasGrounded = IsGrounded;
            _currentGround = groundResult;
        }

        public void Pause(float duration)
        {
            _unconstrainedTimer = Mathf.Max(0.0f, duration);
        }

        public void TickConstraint(float deltaTime)
        {
            if (_unconstrainedTimer <= 0.0f)
                return;

            _unconstrainedTimer = Mathf.Max(0.0f, _unconstrainedTimer - deltaTime);
        }

        public void Restore(bool constrainToGround, float unconstrainedTimer, bool hitGround, bool isWalkable)
        {
            _constrainToGround = constrainToGround;
            _unconstrainedTimer = Mathf.Max(0.0f, unconstrainedTimer);
            _currentGround.HitGround = hitGround;
            _currentGround.IsWalkable = isWalkable;
        }
    }
}
