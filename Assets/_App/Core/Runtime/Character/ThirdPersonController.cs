using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace _App
{
    public class ThirdPersonController : MonoBehaviour
    {
        [SerializeField] private ECharacterType _characterType;

        [Header("Cinemachine")] 
        [SerializeField] private CinemachineThirdPersonFollow _followCamera;
        [SerializeField] private Transform _followTarget;
        [SerializeField] private float _followDistance = 5.0f;

        [Space(15.0f)] 
        [SerializeField] private float _followMinDistance = 2.0f;
        [SerializeField] private float _followMaxDistance = 10.0f;

        [Space(15.0f)] 
        [SerializeField] private bool _invertLook = true;
        [SerializeField] private Vector2 _mouseSensitivity = new Vector2(1.5f, 1.25f);

        [Space(15.0f)] 
        [SerializeField] private float _minPitch = -80.0f;
        [SerializeField] private float _maxPitch = 80.0f;

        [Header("Character Features")]
        [FormerlySerializedAs("_abilitiesData")]
        [SerializeField] private CharacterFeatureSet _characterFeatures;

        [Header("CombatController")]
        [SerializeField] private EWeaponType _startingWeapon = EWeaponType.DualHand;
        [SerializeField] private PlayerCombatLoadoutProfile _combatLoadoutProfile;

        [Header("Combat Reactions")]
        [SerializeField] private CharacterReactionProfile _reactionProfile;

        [Header("AnimationController")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerWeaponLoadout _weaponLoadout;

        private PlayerActorController _actorController;

        internal CharacterFeatureSet CharacterFeatures => _characterFeatures;
        internal Animator PlayerAnimator => _animator;
        internal PlayerWeaponLoadout WeaponLoadout => _weaponLoadout;
        internal ECharacterType CharacterType => _characterType;
        internal EWeaponType StartingWeapon => _startingWeapon;
        internal PlayerCombatLoadoutProfile CombatLoadoutProfile => _combatLoadoutProfile;
        internal CharacterReactionProfile ReactionProfile => _reactionProfile;
        internal CinemachineThirdPersonFollow FollowCameraPrefab => _followCamera;
        internal Transform FollowTarget => _followTarget;
        internal PlayerCameraSettings CameraSettings => new PlayerCameraSettings(
            _followDistance,
            _followMinDistance,
            _followMaxDistance,
            _invertLook,
            _mouseSensitivity,
            _minPitch,
            _maxPitch);

        [Inject]
        public void Construct(PlayerActorController actorController)
        {
            _actorController = actorController;
        }

        #region Monobehavior

        protected virtual void Awake()
        {
            bool valid = true;
            if (_actorController == null)
            {
                Debug.LogError(
                    $"{nameof(ThirdPersonController)} requires {nameof(PlayerActorController)} from the composition root.",
                    this);
                valid = false;
            }

            if (_weaponLoadout == null || !_weaponLoadout.Supports(_startingWeapon))
            {
                Debug.LogError(
                    $"{nameof(ThirdPersonController)} requires a weapon loadout configured for '{_startingWeapon}'.",
                    this);
                valid = false;
            }

            if (_combatLoadoutProfile == null)
            {
                Debug.LogError(
                    $"{nameof(ThirdPersonController)} requires a combat loadout profile.",
                    this);
                valid = false;
            }

            if (!valid)
            {
                enabled = false;
            }
        }

        private void OnEnable()
        {
            _actorController?.Enable();
        }

        private void OnDisable()
        {
            _actorController?.Disable();
        }

        protected virtual void Update()
        {
            _actorController?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        protected virtual void LateUpdate()
        {
            _actorController?.LateTick(Time.unscaledDeltaTime);
        }

        #endregion

        public void InverseGravity()
        {
            _actorController?.TryInvertGravity();
        }
    }
}
