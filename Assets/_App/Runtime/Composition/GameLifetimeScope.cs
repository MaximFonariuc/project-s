using VContainer;
using VContainer.Unity;
using UnityEngine;

namespace _App
{
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private ThirdPersonController _player;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IInputHandler, InputHandler>(Lifetime.Scoped);
            builder.RegisterComponent(_player);
        }
    }
}
