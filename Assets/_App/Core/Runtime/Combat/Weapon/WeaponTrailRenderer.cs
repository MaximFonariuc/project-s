using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace _App
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Weapon))]
    public sealed class WeaponTrailRenderer : MonoBehaviour
    {
        private const string RuntimeObjectName = "Weapon Trail (Runtime)";

        [SerializeField] private Collider _bladeCollider;
        [SerializeField, Min(0.01f)] private float _lifetime = 0.14f;
        [SerializeField, Range(4, 48)] private int _maxSamples = 24;
        [SerializeField, Min(0.001f)] private float _minimumSampleDistance = 0.0125f;
        [SerializeField, Min(0.0f)] private float _endpointInset = 0.025f;
        [SerializeField] private Color _headColor = new Color(0.8f, 0.95f, 1.0f, 0.8f);
        [SerializeField] private Color _tailColor = new Color(0.15f, 0.55f, 1.0f, 0.0f);

        private Vector3[] _bases;
        private Vector3[] _tips;
        private float[] _times;
        private Vector3[] _vertices;
        private Color32[] _colors;
        private int[] _triangles;
        private GameObject _runtimeRoot;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;
        private int _sampleCount;
        private bool _emitting;
        private bool _initialized;

        internal bool IsEmitting => _emitting;
        internal int SampleCount => _sampleCount;

        private void Awake()
        {
            Initialize();
            Clear();
        }

        private void LateUpdate()
        {
            if (!_initialized || _bladeCollider == null)
            {
                return;
            }

            float now = Time.time;
            Prune(now);
            if (_emitting)
            {
                Sample(now, force: false);
            }

            Rebuild(now);
        }

        private void OnDisable()
        {
            Clear();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Object.Destroy(_material);
            }

            if (_mesh != null)
            {
                Object.Destroy(_mesh);
            }

            if (_runtimeRoot != null)
            {
                Object.Destroy(_runtimeRoot);
            }

            _initialized = false;
        }

        internal void Begin()
        {
            Initialize();
            if (!_initialized)
            {
                return;
            }

            ClearSamples();
            _emitting = true;
            float now = Time.time;
            Sample(now, force: true);
            Rebuild(now);
        }

        internal void End()
        {
            _emitting = false;
        }

        internal void Clear()
        {
            _emitting = false;
            ClearSamples();
            if (_mesh != null)
            {
                _mesh.Clear(keepVertexLayout: false);
            }

            if (_renderer != null)
            {
                _renderer.enabled = false;
            }
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            if (_bladeCollider == null)
            {
                _bladeCollider = GetComponent<Collider>();
            }

            if (_bladeCollider == null)
            {
                Debug.LogError(
                    $"{nameof(WeaponTrailRenderer)} on '{name}' requires a blade collider.",
                    this);
                enabled = false;
                return;
            }

            _lifetime = Mathf.Max(0.01f, _lifetime);
            _maxSamples = Mathf.Clamp(_maxSamples, 4, 48);
            _minimumSampleDistance = Mathf.Max(0.001f, _minimumSampleDistance);
            _endpointInset = Mathf.Max(0.0f, _endpointInset);

            _bases = new Vector3[_maxSamples];
            _tips = new Vector3[_maxSamples];
            _times = new float[_maxSamples];
            _vertices = new Vector3[_maxSamples * 2];
            _colors = new Color32[_maxSamples * 2];
            _triangles = new int[(_maxSamples - 1) * 12];

            _runtimeRoot = new GameObject(RuntimeObjectName)
            {
                layer = gameObject.layer,
                hideFlags = HideFlags.DontSave
            };
            _runtimeRoot.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            _runtimeRoot.transform.localScale = Vector3.one;

            var filter = _runtimeRoot.AddComponent<MeshFilter>();
            _renderer = _runtimeRoot.AddComponent<MeshRenderer>();
            _mesh = new Mesh
            {
                name = $"{name} Trail Mesh (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            _mesh.MarkDynamic();
            filter.sharedMesh = _mesh;

            _material = CreateMaterial();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.enabled = false;
            _initialized = true;
        }

        private Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Sprites/Default");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "No compatible transparent shader was found for weapon trails.");
            }

            var material = new Material(shader)
            {
                name = $"{name} Trail Material (Runtime)",
                hideFlags = HideFlags.DontSave,
                renderQueue = (int)RenderQueue.Transparent,
                enableInstancing = true
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", Color.white);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", Color.white);
            }

            if (material.HasProperty("_Cull"))
            {
                material.SetFloat("_Cull", (float)CullMode.Off);
            }

            return material;
        }

        private void Sample(float now, bool force)
        {
            GetBladeEndpoints(out Vector3 basePoint, out Vector3 tipPoint);
            if (!force && _sampleCount > 0)
            {
                int previous = _sampleCount - 1;
                float travel = Mathf.Max(
                    Vector3.Distance(_bases[previous], basePoint),
                    Vector3.Distance(_tips[previous], tipPoint));
                if (travel < _minimumSampleDistance)
                {
                    return;
                }
            }

            if (_sampleCount == _maxSamples)
            {
                ShiftLeft(1);
            }

            _bases[_sampleCount] = basePoint;
            _tips[_sampleCount] = tipPoint;
            _times[_sampleCount] = now;
            _sampleCount++;
        }

        private void Prune(float now)
        {
            int expired = 0;
            while (expired < _sampleCount && now - _times[expired] > _lifetime)
            {
                expired++;
            }

            ShiftLeft(expired);
        }

        private void ShiftLeft(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (count >= _sampleCount)
            {
                ClearSamples();
                return;
            }

            int remaining = _sampleCount - count;
            Array.Copy(_bases, count, _bases, 0, remaining);
            Array.Copy(_tips, count, _tips, 0, remaining);
            Array.Copy(_times, count, _times, 0, remaining);
            _sampleCount = remaining;
        }

        private void Rebuild(float now)
        {
            if (_sampleCount < 2)
            {
                _mesh.Clear(keepVertexLayout: false);
                _renderer.enabled = false;
                return;
            }

            int vertexCount = _sampleCount * 2;
            for (int i = 0; i < _sampleCount; i++)
            {
                float freshness = Mathf.Clamp01(1.0f - (now - _times[i]) / _lifetime);
                Color color = Color.Lerp(_tailColor, _headColor, freshness);
                color.a *= freshness;
                int vertex = i * 2;
                _vertices[vertex] = _bases[i];
                _vertices[vertex + 1] = _tips[i];
                _colors[vertex] = color;
                _colors[vertex + 1] = color;
            }

            int triangleIndex = 0;
            for (int i = 0; i < _sampleCount - 1; i++)
            {
                int a = i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;

                _triangles[triangleIndex++] = a;
                _triangles[triangleIndex++] = b;
                _triangles[triangleIndex++] = c;
                _triangles[triangleIndex++] = c;
                _triangles[triangleIndex++] = b;
                _triangles[triangleIndex++] = d;

                _triangles[triangleIndex++] = c;
                _triangles[triangleIndex++] = b;
                _triangles[triangleIndex++] = a;
                _triangles[triangleIndex++] = d;
                _triangles[triangleIndex++] = b;
                _triangles[triangleIndex++] = c;
            }

            _mesh.Clear(keepVertexLayout: false);
            _mesh.SetVertices(_vertices, 0, vertexCount);
            _mesh.SetColors(_colors, 0, vertexCount);
            _mesh.SetTriangles(_triangles, 0, triangleIndex, 0, calculateBounds: true);
            _renderer.enabled = true;
        }

        private void GetBladeEndpoints(out Vector3 basePoint, out Vector3 tipPoint)
        {
            Transform colliderTransform = _bladeCollider.transform;
            switch (_bladeCollider)
            {
                case CapsuleCollider capsule:
                {
                    Vector3 axis = capsule.direction switch
                    {
                        0 => Vector3.right,
                        2 => Vector3.forward,
                        _ => Vector3.up
                    };
                    float halfLength = Mathf.Max(0.0f, capsule.height * 0.5f - _endpointInset);
                    basePoint = colliderTransform.TransformPoint(capsule.center - axis * halfLength);
                    tipPoint = colliderTransform.TransformPoint(capsule.center + axis * halfLength);
                    return;
                }
                case BoxCollider box:
                {
                    Vector3 size = box.size;
                    Vector3 axis;
                    float halfLength;
                    if (size.x >= size.y && size.x >= size.z)
                    {
                        axis = Vector3.right;
                        halfLength = size.x * 0.5f;
                    }
                    else if (size.z >= size.y)
                    {
                        axis = Vector3.forward;
                        halfLength = size.z * 0.5f;
                    }
                    else
                    {
                        axis = Vector3.up;
                        halfLength = size.y * 0.5f;
                    }

                    halfLength = Mathf.Max(0.0f, halfLength - _endpointInset);
                    basePoint = colliderTransform.TransformPoint(box.center - axis * halfLength);
                    tipPoint = colliderTransform.TransformPoint(box.center + axis * halfLength);
                    return;
                }
                case SphereCollider sphere:
                {
                    float halfLength = Mathf.Max(0.0f, sphere.radius - _endpointInset);
                    basePoint = colliderTransform.TransformPoint(
                        sphere.center - Vector3.up * halfLength);
                    tipPoint = colliderTransform.TransformPoint(
                        sphere.center + Vector3.up * halfLength);
                    return;
                }
                default:
                    basePoint = colliderTransform.position;
                    tipPoint = colliderTransform.position + colliderTransform.up * 0.1f;
                    return;
            }
        }

        private void ClearSamples()
        {
            _sampleCount = 0;
        }
    }
}
