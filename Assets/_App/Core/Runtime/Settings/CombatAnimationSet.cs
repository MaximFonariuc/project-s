using System;
using System.Collections.Generic;
using ModularAnimation.Unity;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace _App
{
    [MovedFrom(true, sourceNamespace: "_App", sourceClassName: "OverrideAnimation")]
    [CreateAssetMenu(fileName = "CombatAnimationSet", menuName = "Combat/Animation Set")]
    public sealed class CombatAnimationSet : ScriptableObject
    {
        [FormerlySerializedAs("<WeaponType>k__BackingField")]
        [SerializeField] private EWeaponType _weaponType;

        [FormerlySerializedAs("<DefaultsAnimationClips>k__BackingField")]
        [SerializeField] private List<AnimationClip> _controllerOverrides = new List<AnimationClip>();

        [SerializeField] private List<CombatAnimationPresentation> _comboActions =
            new List<CombatAnimationPresentation>();

        [FormerlySerializedAs("<Actions>k__BackingField")]
        [SerializeField] private List<CombatAnimationPresentation> _namedActions =
            new List<CombatAnimationPresentation>();

        [SerializeField] private CombatAnimationPresentation _drawAction;
        [SerializeField] private CombatAnimationPresentation _sheatheAction;

        public EWeaponType WeaponType => _weaponType;
        public IReadOnlyList<AnimationClip> ControllerOverrides => _controllerOverrides;
        public IReadOnlyList<CombatAnimationPresentation> ComboActions => _comboActions;

        public bool TryGetNamedAction(
            PlayerCombatAnimationId animationId,
            out CombatAnimationPresentation presentation)
        {
            if (_namedActions != null && !string.IsNullOrEmpty(animationId.Value))
            {
                for (int i = 0; i < _namedActions.Count; i++)
                {
                    CombatAnimationPresentation candidate = _namedActions[i];
                    if (candidate != null &&
                        string.Equals(candidate.Id, animationId.Value, StringComparison.Ordinal) &&
                        candidate.IsValid)
                    {
                        presentation = candidate;
                        return true;
                    }
                }
            }

            presentation = null;
            return false;
        }

        public bool TryGetWeaponTransition(
            bool draw,
            out CombatAnimationPresentation presentation)
        {
            presentation = draw ? _drawAction : _sheatheAction;
            return presentation != null && presentation.IsValid;
        }

        public void ValidateConfiguration()
        {
            if (_weaponType == EWeaponType.None)
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' requires a weapon type.");
            }

            if (_comboActions == null)
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' has no combo collection.");
            }

            for (int i = 0; i < _comboActions.Count; i++)
            {
                if (_comboActions[i] == null || !_comboActions[i].IsValid)
                {
                    throw new InvalidOperationException(
                        $"Combat animation set '{name}' has an invalid combo presentation at index {i}.");
                }
            }

            var actionIds = new HashSet<string>(StringComparer.Ordinal);
            if (_namedActions != null)
            {
                for (int i = 0; i < _namedActions.Count; i++)
                {
                    CombatAnimationPresentation action = _namedActions[i];
                    if (action == null || !action.IsValid || string.IsNullOrWhiteSpace(action.Id))
                    {
                        throw new InvalidOperationException(
                            $"Combat animation set '{name}' has an invalid named action at index {i}.");
                    }

                    if (!actionIds.Add(action.Id))
                    {
                        throw new InvalidOperationException(
                            $"Combat animation set '{name}' contains duplicate action id '{action.Id}'.");
                    }
                }
            }

            if (_weaponType != EWeaponType.Unarmed &&
                (_drawAction == null || !_drawAction.IsValid ||
                 _sheatheAction == null || !_sheatheAction.IsValid))
            {
                throw new InvalidOperationException(
                    $"Combat animation set '{name}' requires valid draw and sheathe actions.");
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_weaponType == EWeaponType.None)
            {
                return;
            }

            string expectedName = $"{_weaponType}_CombatAnimationSet";
            if (name == expectedName)
            {
                return;
            }

            string assetPath = UnityEditor.AssetDatabase.GetAssetPath(this);
            UnityEditor.AssetDatabase.RenameAsset(assetPath, expectedName);
        }
#endif
    }

    [Serializable]
    [MovedFrom(true, sourceNamespace: "_App", sourceClassName: "CombatAnimationClip")]
    public sealed class CombatAnimationPresentation
    {
        [FormerlySerializedAs("<Id>k__BackingField")]
        [SerializeField] private string _id;

        [FormerlySerializedAs("<AnimationClip>k__BackingField")]
        [SerializeField] private AnimationClip _clip;

        [SerializeField, Min(0.0f)] private float _blendInSeconds = 0.08f;
        [SerializeField, Min(0.0f)] private float _blendOutSeconds = 0.1f;
        [SerializeField, Min(0.01f)] private float _playbackSpeed = 1.0f;
        [SerializeField] private AvatarMask _avatarMask;
        [SerializeField] private bool _applyFootIk;

        public string Id => _id;
        public AnimationClip Clip => _clip;
        public float BlendOutSeconds => Mathf.Max(0.0f, _blendOutSeconds);
        public bool IsValid => _clip != null && _clip.length > 0.0f && _playbackSpeed > 0.0f;

        public AnimationClipActionRequest CreateRequest()
        {
            return new AnimationClipActionRequest(
                _clip,
                _blendInSeconds,
                _playbackSpeed,
                _avatarMask,
                _applyFootIk);
        }
    }
}
