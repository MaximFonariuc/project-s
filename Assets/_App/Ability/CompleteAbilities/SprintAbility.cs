namespace _App
{
    public class SprintAbility : IAbility
    {
        private readonly Character _character;
        private readonly float _maxSprintSpeed;

        private bool _isSprinting;
        private bool _sprintInputPressed;

        private float _cachedMaxWalkSpeed;

        public SprintAbility(Character character, float maxSprintSpeed)
        {
            _character = character;
            _maxSprintSpeed = maxSprintSpeed;
        }

        public void Sprint()
        {
            _sprintInputPressed = true;
        }

        public void StopSprinting()
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
                _character.MaxWalkSpeed = _maxSprintSpeed;

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