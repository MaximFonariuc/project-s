using UnityEngine;

namespace _App
{
    internal sealed class CharacterJumpState
    {
        private float _inputHoldTime;
        private float _forceTimeRemaining;
        private int _currentCount;

        public bool ApexNotificationPending { get; set; }

        public bool InputPressed { get; set; }

        public bool IsJumping { get; set; }

        public float InputHoldTime
        {
            get => _inputHoldTime;
            set => _inputHoldTime = Mathf.Max(0.0f, value);
        }

        public float ForceTimeRemaining
        {
            get => _forceTimeRemaining;
            set => _forceTimeRemaining = Mathf.Max(0.0f, value);
        }

        public int CurrentCount
        {
            get => _currentCount;
            set => _currentCount = Mathf.Max(0, value);
        }
    }
}
