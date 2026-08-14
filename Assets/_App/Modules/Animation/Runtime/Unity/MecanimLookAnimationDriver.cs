using System;

namespace ModularAnimation.Unity
{
    public sealed class MecanimLookAnimationDriver : ICharacterLookAnimationDriver
    {
        private readonly MecanimCharacterAnimationDriver _driver;
        private readonly MecanimParameterBinding _horizontal;
        private readonly MecanimParameterBinding _vertical;

        public MecanimLookAnimationDriver(
            MecanimCharacterAnimationDriver driver,
            string horizontalParameter,
            string verticalParameter)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _horizontal = new MecanimParameterBinding(horizontalParameter);
            _vertical = new MecanimParameterBinding(verticalParameter);

            RequireFloat(_horizontal);
            RequireFloat(_vertical);
        }

        public void SetLook(float horizontal, float vertical)
        {
            _driver.SetFloat(_horizontal.Hash, horizontal);
            _driver.SetFloat(_vertical.Hash, vertical);
        }

        private void RequireFloat(MecanimParameterBinding binding)
        {
            _driver.RequireFloat(binding);
        }
    }
}
