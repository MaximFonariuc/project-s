namespace _App
{
    public class WalkAbility : IAbility
    {
        private readonly float _maxWalkSpeed;

        public float MaxSpeed => _maxWalkSpeed;

        public WalkAbility(float maxWalkSpeed)
        {
            _maxWalkSpeed = maxWalkSpeed;
        }

        public void OnEnable()
        {
        }

        public void OnDisable()
        {
        }
    }
}
