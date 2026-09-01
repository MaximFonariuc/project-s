using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    /// <summary>
    /// Owns runtime controller selection for the player Animator. Every override
    /// must use the same base XController, so state and parameter contracts stay stable.
    /// </summary>
    internal sealed class PlayerAnimatorLoadout : IDisposable
    {
        private readonly struct ParameterSnapshot
        {
            public int NameHash { get; }
            public AnimatorControllerParameterType Type { get; }
            public float FloatValue { get; }
            public int IntValue { get; }
            public bool BoolValue { get; }

            public ParameterSnapshot(
                Animator animator,
                AnimatorControllerParameter parameter)
            {
                NameHash = parameter.nameHash;
                Type = parameter.type;
                FloatValue = Type == AnimatorControllerParameterType.Float
                    ? animator.GetFloat(NameHash)
                    : 0.0f;
                IntValue = Type == AnimatorControllerParameterType.Int
                    ? animator.GetInteger(NameHash)
                    : 0;
                BoolValue = Type == AnimatorControllerParameterType.Bool
                    ? animator.GetBool(NameHash)
                    : false;
            }

            public void Restore(Animator animator)
            {
                switch (Type)
                {
                    case AnimatorControllerParameterType.Float:
                        animator.SetFloat(NameHash, FloatValue);
                        break;
                    case AnimatorControllerParameterType.Int:
                        animator.SetInteger(NameHash, IntValue);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        animator.SetBool(NameHash, BoolValue);
                        break;
                }
            }
        }

        private readonly struct LayerSnapshot
        {
            public int StateHash { get; }
            public float NormalizedTime { get; }
            public float Weight { get; }

            public LayerSnapshot(Animator animator, int layerIndex)
            {
                AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(
                    layerIndex);
                if (animator.IsInTransition(layerIndex))
                {
                    AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(
                        layerIndex);
                    if (next.fullPathHash != 0)
                    {
                        state = next;
                    }
                }

                StateHash = state.fullPathHash;
                NormalizedTime = state.loop
                    ? Mathf.Repeat(state.normalizedTime, 1.0f)
                    : Mathf.Clamp01(state.normalizedTime);
                Weight = animator.GetLayerWeight(layerIndex);
            }

            public void Restore(Animator animator, int layerIndex)
            {
                if (StateHash != 0 && animator.HasState(layerIndex, StateHash))
                {
                    animator.Play(StateHash, layerIndex, NormalizedTime);
                }

                if (layerIndex > 0)
                {
                    animator.SetLayerWeight(layerIndex, Weight);
                }
            }
        }

        private readonly Animator _animator;
        private readonly RuntimeAnimatorController _baseController;
        private readonly Dictionary<EWeaponType, AnimatorOverrideController> _overrides;

        private bool _disposed;

        public PlayerAnimatorLoadout(
            Animator animator,
            PlayerCombatMovesetCatalog movesets)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _baseController = ResolveBaseController(_animator.runtimeAnimatorController) ??
                throw new InvalidOperationException(
                    $"Animator '{_animator.name}' requires a base Animator Controller.");
            _overrides = BuildOverrides(
                movesets ?? throw new ArgumentNullException(nameof(movesets)));
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
                throw new InvalidOperationException(
                    $"Weapon '{weaponType}' requires a compatible Animator Override Controller.");
            }
            else
            {
                targetController = animationOverride;
            }

            if (_animator.runtimeAnimatorController != targetController)
            {
                ApplyControllerPreservingPlayback(targetController);
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
            PlayerCombatMovesetCatalog movesets)
        {
            var result = new Dictionary<EWeaponType, AnimatorOverrideController>();
            IReadOnlyList<PlayerCombatMoveset> entries = movesets.All;
            for (int i = 0; i < entries.Count; i++)
            {
                PlayerCombatMoveset moveset = entries[i];
                if (moveset.WeaponType == EWeaponType.Unarmed)
                {
                    continue;
                }

                if (moveset.AnimationOverride == null)
                {
                    throw new InvalidOperationException(
                        $"Combat moveset '{moveset.WeaponType}' requires an Animator Override Controller.");
                }

                RuntimeAnimatorController bindingBase = ResolveBaseController(
                    moveset.AnimationOverride.runtimeAnimatorController);
                if (bindingBase != _baseController)
                {
                    throw new InvalidOperationException(
                        $"Animator override '{moveset.AnimationOverride.name}' must use base controller " +
                        $"'{_baseController.name}'.");
                }

                if (!result.TryAdd(moveset.WeaponType, moveset.AnimationOverride))
                {
                    throw new InvalidOperationException(
                        $"Player has multiple animation overrides for weapon '{moveset.WeaponType}'.");
                }
            }

            return result;
        }

        private void ApplyControllerPreservingPlayback(
            RuntimeAnimatorController targetController)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            var parameterSnapshots = new ParameterSnapshot[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                parameterSnapshots[i] = new ParameterSnapshot(
                    _animator,
                    parameters[i]);
            }

            int layerCount = _animator.layerCount;
            var layerSnapshots = new LayerSnapshot[layerCount];
            for (int i = 0; i < layerCount; i++)
            {
                layerSnapshots[i] = new LayerSnapshot(_animator, i);
            }

            float playbackSpeed = _animator.speed;
            _animator.runtimeAnimatorController = targetController;

            for (int i = 0; i < parameterSnapshots.Length; i++)
            {
                parameterSnapshots[i].Restore(_animator);
            }

            int restoredLayerCount = Mathf.Min(layerCount, _animator.layerCount);
            for (int i = 0; i < restoredLayerCount; i++)
            {
                layerSnapshots[i].Restore(_animator, i);
            }

            _animator.speed = playbackSpeed;
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
