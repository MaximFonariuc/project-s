using UnityEngine;
using UnityEngine.Rendering;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CombatTrainingAttacker))]
    [AddComponentMenu("Project S/Enemies/Combat Training Attack Telegraph")]
    public sealed class CombatTrainingAttackTelegraph : MonoBehaviour
    {
        private const int SegmentCount = 48;

        [SerializeField] private CombatTrainingAttacker _attacker;
        [SerializeField, Min(0.001f)] private float _lineWidth = 0.045f;
        [SerializeField] private Color _windupColor = new(1.0f, 0.45f, 0.05f, 0.85f);
        [SerializeField] private Color _activeColor = new(1.0f, 0.08f, 0.03f, 1.0f);
        [SerializeField] private Color _parryStaggerColor = new(0.05f, 0.8f, 1.0f, 0.9f);

        private LineRenderer _line;
        private Material _material;
        private GameObject _visual;
        private readonly Vector3[] _positions = new Vector3[SegmentCount + 1];

        private void Awake()
        {
            ResolveAttacker();
            CreateVisual();
        }

        private void LateUpdate()
        {
            if (_attacker == null || _line == null)
            {
                return;
            }

            CombatTrainingAttackSnapshot snapshot = _attacker.Snapshot;
            bool visible = snapshot.Phase == CombatTrainingAttackPhase.Windup ||
                           snapshot.Phase == CombatTrainingAttackPhase.Active ||
                           snapshot.Phase == CombatTrainingAttackPhase.ParryStagger;
            _line.enabled = visible;
            if (!visible)
            {
                return;
            }

            float progress = snapshot.NormalizedTime;
            float radiusScale = ResolveRadiusScale(snapshot.Phase, progress);
            Color color = ResolveColor(snapshot.Phase, progress);
            _line.startColor = color;
            _line.endColor = color;
            if (_material != null && _material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", color);
            }
            BuildCircle(_attacker.AttackRange * radiusScale);
        }

        private float ResolveRadiusScale(
            CombatTrainingAttackPhase phase,
            float progress)
        {
            if (phase == CombatTrainingAttackPhase.Windup)
            {
                return Mathf.Lerp(0.35f, 1.0f, progress);
            }

            return phase == CombatTrainingAttackPhase.ParryStagger
                ? 1.0f + Mathf.Sin(progress * Mathf.PI * 4.0f) * 0.06f
                : 1.0f;
        }

        private Color ResolveColor(
            CombatTrainingAttackPhase phase,
            float progress)
        {
            if (phase == CombatTrainingAttackPhase.Active)
            {
                return _activeColor;
            }

            return phase == CombatTrainingAttackPhase.ParryStagger
                ? _parryStaggerColor
                : Color.Lerp(_windupColor, _activeColor, progress);
        }

        private void OnDestroy()
        {
            if (_visual != null)
            {
                Destroy(_visual);
            }

            if (_material != null)
            {
                Destroy(_material);
            }

            _visual = null;
            _line = null;
            _material = null;
        }

        private void Reset()
        {
            ResolveAttacker();
        }

        private void OnValidate()
        {
            _lineWidth = Mathf.Max(0.001f, _lineWidth);
            ResolveAttacker();
        }

        private void CreateVisual()
        {
            if (_visual != null)
            {
                return;
            }

            _visual = new GameObject("Attack Telegraph");
            _visual.transform.SetParent(transform, false);
            _visual.transform.localPosition = new Vector3(0.0f, 0.04f, 0.0f);
            _line = _visual.AddComponent<LineRenderer>();
            _line.useWorldSpace = false;
            _line.loop = false;
            _line.positionCount = _positions.Length;
            _line.widthMultiplier = _lineWidth;
            _line.textureMode = LineTextureMode.Stretch;
            _line.alignment = LineAlignment.View;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.numCornerVertices = 2;
            _line.numCapVertices = 2;
            _line.enabled = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogError("Training attack telegraph requires an unlit shader.", this);
                enabled = false;
                return;
            }

            _material = new Material(shader)
            {
                name = "Training Attack Telegraph (Runtime)",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = (int)RenderQueue.Transparent
            };
            ConfigureTransparentMaterial(_material);
            if (_material.HasProperty("_BaseColor"))
            {
                _material.SetColor("_BaseColor", Color.white);
            }
            _line.sharedMaterial = _material;
        }

        private void BuildCircle(float radius)
        {
            for (int i = 0; i <= SegmentCount; i++)
            {
                float angle = i / (float)SegmentCount * Mathf.PI * 2.0f;
                _positions[i] = new Vector3(
                    Mathf.Cos(angle) * radius,
                    0.0f,
                    Mathf.Sin(angle) * radius);
            }

            _line.SetPositions(_positions);
        }

        private static void ConfigureTransparentMaterial(Material material)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            SetFloatIfPresent(material, "_Surface", 1.0f);
            SetFloatIfPresent(material, "_Blend", 0.0f);
            SetFloatIfPresent(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloatIfPresent(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloatIfPresent(material, "_ZWrite", 0.0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private static void SetFloatIfPresent(
            Material material,
            string propertyName,
            float value)
        {
            if (material.HasProperty(propertyName))
            {
                material.SetFloat(propertyName, value);
            }
        }

        private void ResolveAttacker()
        {
            if (_attacker == null)
            {
                _attacker = GetComponent<CombatTrainingAttacker>();
            }
        }
    }
}
