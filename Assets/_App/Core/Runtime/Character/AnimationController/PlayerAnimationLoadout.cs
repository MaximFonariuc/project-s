using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [Serializable]
    public sealed class PlayerAnimationOverrideBinding
    {
        [SerializeField] private EWeaponType _weaponType = EWeaponType.TwoHanded;
        [SerializeField] private AnimatorOverrideController _controller;

        internal EWeaponType WeaponType => _weaponType;
        internal AnimatorOverrideController Controller => _controller;
    }

    /// <summary>
    /// Owns runtime controller selection for the player Animator. Every override
    /// must use the same base XController, so state and parameter contracts stay stable.
    /// </summary>
    internal sealed class PlayerAnimatorLoadout : IDisposable
    {
        private readonly Animator _animator;
        private readonly RuntimeAnimatorController _baseController;
        private readonly Dictionary<EWeaponType, AnimatorOverrideController> _overrides;

        private bool _disposed;

        public PlayerAnimatorLoadout(
            Animator animator,
            PlayerAnimationOverrideBinding[] bindings)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _baseController = ResolveBaseController(_animator.runtimeAnimatorController) ??
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a base Animator Controller.");
            _overrides = BuildOverrides(bindings);
        }

        public void Apply(EWeaponType weaponType)
        {
            ThrowIfDisposed();

            RuntimeAnimatorController targetController;
            if (weaponType is EWeaponType.None or EWeaponType.Unarmed)
            {
                targetController = _baseController;
            }
            else if (!_overrides.TryGetValue(
                         weaponType,
                         out AnimatorOverrideController animationOverride))
            {
                // An override is optional. The restored XController already owns
                // the common locomotion, combat and weapon transition clips, so
                // unsupported loadouts keep using its base clip table.
                targetController = _baseController;
            }
            else
            {
                targetController = animationOverride;
            }

            if (_animator.runtimeAnimatorController != targetController)
            {
                // Controllers share one base graph, so assigning the override keeps
                // the current state machines and only changes their clip table.
                _animator.runtimeAnimatorController = targetController;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_animator != null && _animator.runtimeAnimatorController != _baseController)
            {
                _animator.runtimeAnimatorController = _baseController;
            }

            _disposed = true;
        }

        private Dictionary<EWeaponType, AnimatorOverrideController> BuildOverrides(
            PlayerAnimationOverrideBinding[] bindings)
        {
            var result = new Dictionary<EWeaponType, AnimatorOverrideController>();
            if (bindings == null)
            {
                return result;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                PlayerAnimationOverrideBinding binding = bindings[i];
                if (binding == null)
                {
                    throw new InvalidOperationException(
                        $"Player animation override entry {i} is null.");
                }

                if (binding.WeaponType is EWeaponType.None or EWeaponType.Unarmed)
                {
                    throw new InvalidOperationException(
                        $"Weapon '{binding.WeaponType}' uses the base controller and " +
                        "must not have an override entry.");
                }

                if (binding.Controller == null)
                {
                    // The list is optional authoring data. A removed override may
                    // leave an empty serialized slot behind; unsupported weapons
                    // still fail explicitly if Apply is called for them.
                    continue;
                }

                RuntimeAnimatorController bindingBase = ResolveBaseController(
                    binding.Controller.runtimeAnimatorController);
                if (bindingBase != _baseController)
                {
                    throw new InvalidOperationException(
                        $"Animator override '{binding.Controller.name}' must use base controller " +
                        $"'{_baseController.name}'.");
                }

                if (!result.TryAdd(binding.WeaponType, binding.Controller))
                {
                    throw new InvalidOperationException(
                        $"Player has multiple animation overrides for weapon '{binding.WeaponType}'.");
                }
            }

            return result;
        }

        private static RuntimeAnimatorController ResolveBaseController(
            RuntimeAnimatorController controller)
        {
            RuntimeAnimatorController current = controller;
            while (current is AnimatorOverrideController animationOverride)
            {
                current = animationOverride.runtimeAnimatorController;
            }

            return current;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(PlayerAnimatorLoadout));
            }

            if (_animator == null)
            {
                throw new InvalidOperationException("The Animator owner has been destroyed.");
            }
        }
    }
}
