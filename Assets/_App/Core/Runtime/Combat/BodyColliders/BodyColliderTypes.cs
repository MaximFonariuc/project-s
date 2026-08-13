using System;
using UnityEngine;

namespace _App
{
    public enum BodyRigType
    {
        Auto,
        Humanoid,
        Generic
    }

    public enum BodyColliderShape
    {
        Capsule,
        Box,
        Sphere
    }

    public enum BodyHitZone
    {
        Root,
        Head,
        Neck,
        Chest,
        Abdomen,
        Pelvis,
        LeftUpperArm,
        LeftLowerArm,
        LeftHand,
        RightUpperArm,
        RightLowerArm,
        RightHand,
        LeftUpperLeg,
        LeftLowerLeg,
        LeftFoot,
        RightUpperLeg,
        RightLowerLeg,
        RightFoot,
        Custom
    }

    [Serializable]
    public sealed class BodyColliderBoneDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private BodyHitZone _zone = BodyHitZone.Custom;
        [SerializeField] private string _customZoneId;
        [SerializeField] private BodyColliderShape _shape = BodyColliderShape.Capsule;
        [SerializeField] private Transform _startBone;
        [SerializeField] private Transform _endBone;
        [SerializeField] private bool _anchorAtStart;
        [SerializeField, Min(0.01f)] private float _radiusScale = 0.15f;
        [SerializeField, Min(0f)] private float _padding = 0.02f;
        [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
        [SerializeField] private bool _enabled = true;

        public string Id => _id;
        public BodyHitZone Zone => _zone;
        public string CustomZoneId => _customZoneId;
        public BodyColliderShape Shape => _shape;
        public Transform StartBone => _startBone;
        public Transform EndBone => _endBone;
        public bool AnchorAtStart => _anchorAtStart;
        public float RadiusScale => Mathf.Max(0.01f, _radiusScale);
        public float Padding => Mathf.Max(0f, _padding);
        public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);
        public bool Enabled => _enabled;

        public string EffectiveId => string.IsNullOrWhiteSpace(_id)
            ? GetZoneId(_zone, _customZoneId)
            : _id.Trim();

        public static string GetZoneId(BodyHitZone zone, string customZoneId)
        {
            return zone == BodyHitZone.Custom && !string.IsNullOrWhiteSpace(customZoneId)
                ? customZoneId.Trim()
                : zone.ToString();
        }
    }
}
