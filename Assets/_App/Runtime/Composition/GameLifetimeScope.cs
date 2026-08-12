using ModularInput;
using ModularInput.Unity;
using VContainer;
using VContainer.Unity;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _App
{
    [DefaultExecutionOrder(-5000)]
    public sealed class GameLifetimeScope : LifetimeScope
    {
        [SerializeField] private ThirdPersonController _player;
        [SerializeField] private InputActionAsset _inputActions;

        protected override void Configure(IContainerBuilder builder)
        {
            builder.Register<IInputSource>(_ => new UnityInputSource(_inputActions), Lifetime.Scoped);
            builder.Register<IInputService, InputRouter>(Lifetime.Scoped);
            builder.Register<IPlayerInput, PlayerInputAdapter>(Lifetime.Scoped);
            builder.RegisterComponent(_player);
        }
    }
}
