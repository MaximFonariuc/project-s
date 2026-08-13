namespace _App
{
    public class SprintAbility : IAbility
    {
        private readonly float _maxSprintSpeed;

        public float MaxSpeed => _maxSprintSpeed;

        public SprintAbility(float maxSprintSpeed)
        {
            _maxSprintSpeed = maxSprintSpeed;
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }
    }
}
