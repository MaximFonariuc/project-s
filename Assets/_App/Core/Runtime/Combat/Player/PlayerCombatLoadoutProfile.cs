using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [Serializable]
    public sealed class PlayerCombatMovesetAuthoring
    {
        [SerializeField] private EWeaponType _weaponType = EWeaponType.Unarmed;
        [SerializeField] private PlayerCombatActionProfile _actionProfile;
        [SerializeField] private AnimatorOverrideController _animationOverride;

        internal EWeaponType WeaponType => _weaponType;
        internal PlayerCombatActionProfile ActionProfile => _actionProfile;
        internal AnimatorOverrideController AnimationOverride => _animationOverride;

        internal PlayerCombatMoveset Build()
        {
            if (_weaponType == EWeaponType.None)
            {
                throw new InvalidOperationException(
                    "A combat moveset cannot use the reserved None weapon type.");
            }

            if (_actionProfile == null)
            {
                throw new InvalidOperationException(
                    $"Combat moveset '{_weaponType}' requires an action profile.");
            }

            if (_weaponType == EWeaponType.Unarmed && _animationOverride != null)
            {
                throw new InvalidOperationException(
                    "The Unarmed moveset must use the base Animator Controller.");
            }

            return new PlayerCombatMoveset(
                _weaponType,
                _actionProfile,
                _actionProfile.CreateActionGraph(),
                _animationOverride);
        }
    }

    [CreateAssetMenu(
        fileName = "PlayerCombatLoadoutProfile",
        menuName = "Combat/Player Combat Loadout")]
    public sealed class PlayerCombatLoadoutProfile : ScriptableObject
    {
        [SerializeField] private CombatActionAuthoring _weaponTransition =
            CombatActionAuthoring.CreateDefaultWeaponTransition();
        [SerializeField] private CombatInputBufferAuthoring _inputBuffer = new();

        [Header("Weapon Movesets")]
        [SerializeField] private List<PlayerCombatMovesetAuthoring> _movesets = new();

        internal CombatActionDefinition CreateWeaponTransitionDefinition()
        {
            if (_weaponTransition == null)
            {
                throw new InvalidOperationException(
                    $"Combat loadout '{name}' requires a weapon transition action.");
            }

            return _weaponTransition.Build();
        }

        internal CombatInputBufferSettings CreateInputBufferSettings()
        {
            if (_inputBuffer == null)
            {
                throw new InvalidOperationException(
                    $"Combat loadout '{name}' requires input buffer settings.");
            }

            return _inputBuffer.Build();
        }

        internal PlayerCombatMovesetCatalog CreateMovesetCatalog()
        {
            var movesets = new List<PlayerCombatMoveset>(_movesets?.Count ?? 0);
            if (_movesets != null)
            {
                for (int i = 0; i < _movesets.Count; i++)
                {
                    PlayerCombatMovesetAuthoring authoring = _movesets[i];
                    if (authoring == null)
                    {
                        throw new InvalidOperationException(
                            $"Combat loadout '{name}' has a null moveset at index {i}.");
                    }

                    movesets.Add(authoring.Build());
                }
            }

            return new PlayerCombatMovesetCatalog(movesets);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            try
            {
                CreateWeaponTransitionDefinition();
                CreateInputBufferSettings();
                CreateMovesetCatalog();
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"Combat loadout '{name}' is invalid: {exception.Message}",
                    this);
            }
        }
#endif
    }

    internal sealed class PlayerCombatMoveset
    {
        public EWeaponType WeaponType { get; }
        public PlayerCombatActionProfile ActionProfile { get; }
        public PlayerCombatActionGraph ActionGraph { get; }
        public AnimatorOverrideController AnimationOverride { get; }

        public PlayerCombatMoveset(
            EWeaponType weaponType,
            PlayerCombatActionProfile actionProfile,
            PlayerCombatActionGraph actionGraph,
            AnimatorOverrideController animationOverride)
        {
            WeaponType = weaponType;
            ActionProfile = actionProfile ?? throw new ArgumentNullException(nameof(actionProfile));
            ActionGraph = actionGraph ?? throw new ArgumentNullException(nameof(actionGraph));
            AnimationOverride = animationOverride;
        }
    }

    internal sealed class PlayerCombatMovesetCatalog
    {
        private readonly Dictionary<EWeaponType, PlayerCombatMoveset> _movesets;
        private readonly PlayerCombatMoveset[] _all;

        public IReadOnlyList<PlayerCombatMoveset> All => _all;

        public PlayerCombatMovesetCatalog(IEnumerable<PlayerCombatMoveset> movesets)
        {
            if (movesets == null)
            {
                throw new ArgumentNullException(nameof(movesets));
            }

            _movesets = new Dictionary<EWeaponType, PlayerCombatMoveset>();
            foreach (PlayerCombatMoveset moveset in movesets)
            {
                if (moveset == null)
                {
                    throw new InvalidOperationException("Combat moveset collection contains null.");
                }

                if (!_movesets.TryAdd(moveset.WeaponType, moveset))
                {
                    throw new InvalidOperationException(
                        $"Combat loadout has duplicate moveset '{moveset.WeaponType}'.");
                }
            }

            if (!_movesets.ContainsKey(EWeaponType.Unarmed))
            {
                throw new InvalidOperationException(
                    "Combat loadout requires an explicit Unarmed moveset.");
            }

            _all = new PlayerCombatMoveset[_movesets.Count];
            _movesets.Values.CopyTo(_all, 0);
        }

        public bool Supports(EWeaponType weaponType)
        {
            return _movesets.ContainsKey(weaponType);
        }

        public bool TryGet(
            EWeaponType weaponType,
            out PlayerCombatMoveset moveset)
        {
            return _movesets.TryGetValue(weaponType, out moveset);
        }

        public PlayerCombatMoveset Require(EWeaponType weaponType)
        {
            if (_movesets.TryGetValue(weaponType, out PlayerCombatMoveset moveset))
            {
                return moveset;
            }

            throw new InvalidOperationException(
                $"Combat loadout has no moveset for weapon '{weaponType}'.");
        }
    }
}
