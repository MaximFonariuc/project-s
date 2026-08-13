using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace _App
{
    [CreateAssetMenu(
        fileName = "CharacterFeatureSet",
        menuName = "Project S/Character/Feature Set")]
    public sealed class CharacterFeatureSet : ScriptableObject
    {
        [FormerlySerializedAs("<Abilities>k__BackingField")]
        [SerializeField] private List<CharacterFeatureEntry> _features;

        public IReadOnlyList<CharacterFeatureEntry> Features => _features;

        internal void ValidateConfiguration()
        {
            if (_features == null)
            {
                throw new InvalidOperationException(
                    $"{nameof(CharacterFeatureSet)} '{name}' has no feature list.");
            }

            var enabledTypes = new HashSet<Type>();
            foreach (CharacterFeatureEntry feature in _features)
            {
                if (feature == null || !feature.IsEnabled)
                {
                    continue;
                }

                if (feature.Settings == null)
                {
                    throw new InvalidOperationException(
                        $"{nameof(CharacterFeatureSet)} '{name}' contains an enabled feature without settings.");
                }

                Type settingsType = feature.Settings.GetType();
                if (!enabledTypes.Add(settingsType))
                {
                    throw new InvalidOperationException(
                        $"{nameof(CharacterFeatureSet)} '{name}' contains duplicate enabled settings " +
                        $"for {settingsType.Name}.");
                }
            }
        }

        internal T GetEnabled<T>() where T : CharacterFeatureSettings
        {
            if (_features == null)
            {
                return null;
            }

            foreach (CharacterFeatureEntry feature in _features)
            {
                if (feature is { IsEnabled: true, Settings: T settings })
                {
                    return settings;
                }
            }

            return null;
        }
    }

    public abstract class CharacterFeatureSettings : ScriptableObject
    {
    }

    [Serializable]
    public sealed class CharacterFeatureEntry
    {
        [FormerlySerializedAs("IsActive")]
        [SerializeField] private bool _isEnabled;

        [FormerlySerializedAs("Data")]
        [SerializeField] private CharacterFeatureSettings _settings;

        public bool IsEnabled => _isEnabled;
        public CharacterFeatureSettings Settings => _settings;
    }
}
