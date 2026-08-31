using System;
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
        [SerializeField] private CombatStyleHudView _combatStyleHud;

        protected override void Configure(IContainerBuilder builder)
        {
            if (_player == null)
            {
                throw new InvalidOperationException($"{nameof(GameLifetimeScope)} requires a Player reference.");
            }

            if (_inputActions == null)
            {
                throw new InvalidOperationException($"{nameof(GameLifetimeScope)} requires an InputActionAsset.");
            }

            builder.Register<IInputSource>(_ => new UnityInputSource(_inputActions), Lifetime.Scoped);
            builder.Register<IInputService, InputRouter>(Lifetime.Scoped);
            builder.Register<IPlayerInput, PlayerInputAdapter>(Lifetime.Scoped);
            builder.RegisterPlayer(_player);
            if (_combatStyleHud != null)
            {
                builder.RegisterComponent(_combatStyleHud);
                builder.RegisterEntryPoint<CombatStylePresenter>(Lifetime.Scoped);
            }
        }
    }
}
