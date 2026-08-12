namespace _App
{
    public class WalkAbility : IAbility
    {
        private readonly Character _character;
        private readonly float _maxWalkSpeed;

        private bool _isSprinting;
        private bool _sprintInputPressed;

        private float _cachedMaxWalkSpeed;

        public WalkAbility(Character character, float maxSprintSpeed)
        {
            _character = character;
            _maxWalkSpeed = maxSprintSpeed;
        }

        public void Walk()
        {
            _sprintInputPressed = true;
        }

        public void StopWalking()
        {
            _sprintInputPressed = false;
        }
        
        private bool CanSprint()
        {
            return _character.IsWalking() && !_character.IsCrouched();
        }

        private void CheckSprintInput()
        {
            if (!_isSprinting && _sprintInputPressed && CanSprint())
            {
                _isSprinting = true;

                _cachedMaxWalkSpeed = _character.MaxWalkSpeed;
                _character.MaxWalkSpeed = _maxWalkSpeed;

            }
            else if (_isSprinting && (!_sprintInputPressed || !CanSprint()))
            {
                _isSprinting = false;
                
                _character.MaxWalkSpeed = _cachedMaxWalkSpeed;
            }
        }
        
        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            CheckSprintInput();
        }

        public void OnEnable()
        {
            _character.BeforeSimulationUpdated += OnBeforeSimulationUpdated;
        }
        
        public void OnDisable()
        {
            _character.BeforeSimulationUpdated -= OnBeforeSimulationUpdated;
        }
    }
}