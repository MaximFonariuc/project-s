
namespace _App
{
    public class SwimAbility : IAbility
    {
        private readonly Character _character;
        private readonly AnimationController _animationController;
        private readonly bool _canEverSwim;
        
        public SwimAbility(Character character, bool canEverSwim, AnimationController animationController)
        {
            _character = character;
            _animationController = animationController;
            _canEverSwim = canEverSwim;
        }

        private bool IsSwimAllowed()
        {
            return _canEverSwim && _character.IsSwimming();
        }

        private void CheckSwimInput()
        {
            if (_character.JumpInputPressed)
            {
                float depth = _character.CalcImmersionDepth();
                if (depth > 0.65f)
                {
                    var movementDirection = _character.GetMovementDirection();
                    movementDirection += _character.GetUpVector();
                    _animationController?.OnSwimDirectionChanged(SwimDirection.Up);
                    _character?.SetMovementDirection(movementDirection);
                }
                else
                {
                    _character?.SetMovementMode(EMovementMode.Falling);
                    _character?.LaunchCharacter(_character.GetUpVector() * 9.0f, true);
                }
            }
            else if (_character.CrouchInputPressed)
            {
                float depth = _character.CalcImmersionDepth();
                if (depth > 0.65f)
                {
                    var movementDirection = _character.GetMovementDirection();
                    movementDirection -= _character.GetUpVector();
                    _animationController?.OnSwimDirectionChanged(SwimDirection.Down);
                    _character.SetMovementDirection(movementDirection);
                }
            }
        }

        private void OnBeforeSimulationUpdated(float deltaTime)
        {
            if (_character.IsSwimming() && IsSwimAllowed())
            {
                CheckSwimInput();
            }
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

    public enum SwimDirection
    {
        Down = -1,
        Default = 0,
        Up = 1
    }
}