using System;
using System.Collections.Generic;
using ModularInput;
using UnityEngine;

namespace _App
{
    internal sealed class CombatInputBufferSettings
    {
        private readonly Dictionary<PlayerCombatCommandId, InputBufferWriteOptions> _overrides;

        public InputBufferOptions BufferOptions { get; }
        public InputBufferWriteOptions DefaultWriteOptions { get; }
        public float ChargeHoldSeconds { get; }

        public CombatInputBufferSettings(
            InputBufferOptions bufferOptions,
            InputBufferWriteOptions defaultWriteOptions,
            Dictionary<PlayerCombatCommandId, InputBufferWriteOptions> overrides,
            float chargeHoldSeconds)
        {
            BufferOptions = bufferOptions;
            DefaultWriteOptions = defaultWriteOptions;
            _overrides = overrides ?? throw new ArgumentNullException(nameof(overrides));
            if (chargeHoldSeconds <= 0.0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(chargeHoldSeconds),
                    "Charge hold threshold must be positive.");
            }

            ChargeHoldSeconds = chargeHoldSeconds;
        }

        public InputBufferWriteOptions GetWriteOptions(PlayerCombatCommandId commandId)
        {
            return _overrides.TryGetValue(commandId, out InputBufferWriteOptions options)
                ? options
                : DefaultWriteOptions;
        }
    }

    [Serializable]
    public sealed class CombatInputBufferAuthoring
    {
        [SerializeField, Range(1, 16)] private int _capacity = 3;
        [SerializeField] private InputBufferOverflowPolicy _overflowPolicy =
            InputBufferOverflowPolicy.DropOldest;
        [SerializeField] private InputBufferSelectionPolicy _selectionPolicy =
            InputBufferSelectionPolicy.OldestFirst;
        [SerializeField] private CombatBufferedActionRuleAuthoring _defaultRule = new();
        [SerializeField] private List<CombatBufferedActionOverrideAuthoring> _actionOverrides = new();
        [SerializeField, Min(0.05f)] private float _chargeHoldSeconds = 0.3f;

        internal CombatInputBufferSettings Build()
        {
            if (_defaultRule == null)
            {
                throw new InvalidOperationException("Combat input buffer requires a default action rule.");
            }

            var overrides = new Dictionary<PlayerCombatCommandId, InputBufferWriteOptions>();
            if (_actionOverrides != null)
            {
                for (int i = 0; i < _actionOverrides.Count; i++)
                {
                    CombatBufferedActionOverrideAuthoring entry = _actionOverrides[i];
                    if (entry == null || entry.Rule == null)
                    {
                        throw new InvalidOperationException(
                            $"Combat input buffer has an incomplete action override at index {i}.");
                    }

                    if (!overrides.TryAdd(entry.CommandId, entry.Rule.Build()))
                    {
                        throw new InvalidOperationException(
                            $"Combat input buffer has duplicate rules for '{entry.CommandId}'.");
                    }
                }
            }

            return new CombatInputBufferSettings(
                new InputBufferOptions(_capacity, _overflowPolicy, _selectionPolicy),
                _defaultRule.Build(),
                overrides,
                _chargeHoldSeconds);
        }
    }

    [Serializable]
    public sealed class CombatBufferedActionRuleAuthoring
    {
        [SerializeField, Min(0.01f)] private float _lifetime = 0.35f;
        [SerializeField] private int _priority;
        [SerializeField] private InputBufferDuplicatePolicy _duplicatePolicy =
            InputBufferDuplicatePolicy.Allow;

        internal InputBufferWriteOptions Build()
        {
            return new InputBufferWriteOptions(_lifetime, _priority, _duplicatePolicy);
        }
    }

    [Serializable]
    public sealed class CombatBufferedActionOverrideAuthoring
    {
        [SerializeField] private string _commandId = "attack.light";
        [SerializeField] private CombatBufferedActionRuleAuthoring _rule = new();

        internal PlayerCombatCommandId CommandId => new PlayerCombatCommandId(_commandId);
        internal CombatBufferedActionRuleAuthoring Rule => _rule;
    }
}
