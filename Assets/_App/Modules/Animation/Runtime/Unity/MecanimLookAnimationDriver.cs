namespace ModularAnimation.Unity
{
    public sealed class NullCharacterLookAnimationDriver : ICharacterLookAnimationDriver
    {
        public static NullCharacterLookAnimationDriver Instance { get; } =
            new NullCharacterLookAnimationDriver();

        private NullCharacterLookAnimationDriver()
        {
        }

        public void ApplyLook(in CharacterLookFrame frame, float deltaTime)
        {
        }
    }
}
