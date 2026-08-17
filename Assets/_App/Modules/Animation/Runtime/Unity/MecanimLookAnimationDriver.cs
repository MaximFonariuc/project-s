namespace ModularAnimation.Unity
{
    public sealed class NullCharacterLookAnimationDriver : ICharacterLookAnimationDriver
    {
        public static NullCharacterLookAnimationDriver Instance { get; } =
            new NullCharacterLookAnimationDriver();

        private NullCharacterLookAnimationDriver()
        {
        }

        public void SetLook(float horizontal, float vertical)
        {
        }
    }
}
