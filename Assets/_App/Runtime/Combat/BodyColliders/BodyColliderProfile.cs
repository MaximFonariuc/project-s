using System;
using System.Collections.Generic;
using UnityEngine;

namespace _App
{
    [CreateAssetMenu(fileName = "BodyColliderProfile", menuName = "Project S/Combat/Body Collider Profile")]
    public sealed class BodyColliderProfile : ScriptableObject
    {
        [Serializable]
        public sealed class ZoneOverride
        {
            [SerializeField] private BodyHitZone _zone;
            [SerializeField, Min(0.01f)] private float _radiusScale = 0.15f;
            [SerializeField, Min(0f)] private float _padding = 0.02f;
            [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

            public BodyHitZone Zone => _zone;
            public float RadiusScale => Mathf.Max(0.01f, _radiusScale);
            public float Padding => Mathf.Max(0f, _padding);
            public float DamageMultiplier => Mathf.Max(0f, _damageMultiplier);
        }

        [Header("Generated colliders")]
        [SerializeField] private string _layerName = "Hitbox";
        [SerializeField] private bool _isTrigger = true;
        [SerializeField, Min(0.001f)] private float _minimumRadius = 0.025f;

        [Header("Skin fitting")]
        [SerializeField, Range(0.01f, 1f)] private float _minimumBoneWeight = 0.2f;
        [SerializeField, Range(0.5f, 1f)] private float _vertexPercentile = 0.92f;

        [Header("Zone overrides")]
        [SerializeField] private List<ZoneOverride> _zoneOverrides = new List<ZoneOverride>();

        public string LayerName => string.IsNullOrWhiteSpace(_layerName) ? "Hitbox" : _layerName.Trim();
        public bool IsTrigger => _isTrigger;
        public float MinimumRadius => Mathf.Max(0.001f, _minimumRadius);
        public float MinimumBoneWeight => Mathf.Clamp01(_minimumBoneWeight);
        public float VertexPercentile => Mathf.Clamp(_vertexPercentile, 0.5f, 1f);
        public float GetRadiusScale(BodyHitZone zone, float fallback)
        {
            ZoneOverride value = FindOverride(zone);
            return value != null ? value.RadiusScale : Mathf.Max(0.01f, fallback);
        }

        public float GetPadding(BodyHitZone zone, float fallback)
        {
            ZoneOverride value = FindOverride(zone);
            return value != null ? value.Padding : Mathf.Max(0f, fallback);
        }

        public float GetDamageMultiplier(BodyHitZone zone, float fallback = 1f)
        {
            ZoneOverride value = FindOverride(zone);
            return value != null ? value.DamageMultiplier : Mathf.Max(0f, fallback);
        }

        private ZoneOverride FindOverride(BodyHitZone zone)
        {
            for (int i = 0; i < _zoneOverrides.Count; i++)
            {
                ZoneOverride value = _zoneOverrides[i];
                if (value != null && value.Zone == zone)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
