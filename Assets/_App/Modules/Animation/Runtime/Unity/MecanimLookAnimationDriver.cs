using System;
using UnityEngine;

namespace ModularAnimation.Unity
{
    public sealed class MecanimLookAnimationDriver : ICharacterLookAnimationDriver
    {
        private readonly Animator _animator;
        private readonly MecanimParameterBinding _horizontal;
        private readonly MecanimParameterBinding _vertical;

        public MecanimLookAnimationDriver(
            Animator animator,
            string horizontalParameter,
            string verticalParameter)
        {
            _animator = animator != null
                ? animator
                : throw new ArgumentNullException(nameof(animator));
            _horizontal = new MecanimParameterBinding(horizontalParameter);
            _vertical = new MecanimParameterBinding(verticalParameter);

            RequireFloat(_horizontal);
            RequireFloat(_vertical);
        }

        public void SetLook(float horizontal, float vertical)
        {
            _animator.SetFloat(_horizontal.Hash, horizontal);
            _animator.SetFloat(_vertical.Hash, vertical);
        }

        private void RequireFloat(MecanimParameterBinding binding)
        {
            AnimatorControllerParameter[] parameters = _animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == binding.Hash &&
                    parameter.type == AnimatorControllerParameterType.Float)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Animator '{_animator.name}' requires Float parameter '{binding.Name}'.");
        }
    }
}
