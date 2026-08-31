using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _App
{
    internal sealed class CombatImpactPresentation : IDisposable
    {
        private const int AudioSourceCount = 4;
        private const int MaxParticles = 256;
        private const string RuntimeObjectName = "Combat Feedback (Runtime)";

        private readonly ICameraImpactFeedback _camera;
        private readonly GameObject _root;
        private readonly ParticleSystem _particles;
        private readonly AudioSource[] _audioSources;
        private readonly Material _particleMaterial;
        private readonly Texture2D _particleTexture;

        private int _nextAudioSource;
        private bool _disposed;

        public CombatImpactPresentation(
            Transform owner,
            ICameraImpactFeedback camera)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            _camera = camera ?? throw new ArgumentNullException(nameof(camera));
            _root = new GameObject(RuntimeObjectName);
            _root.transform.SetParent(owner, worldPositionStays: false);
            _root.layer = owner.gameObject.layer;

            _particles = _root.AddComponent<ParticleSystem>();
            _particleMaterial = ConfigureParticles(_particles, out _particleTexture);
            _audioSources = CreateAudioSources(_root.transform);
        }

        public void Play(in CombatHitResult hit, CombatFeedbackDefinition definition)
        {
            if (_disposed)
            {
                return;
            }

            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            Vector3 point = IsFinite(hit.Point)
                ? hit.Point
                : ResolveFallbackPoint(in hit);
            Vector3 direction = IsFinite(hit.Direction) && hit.Direction.sqrMagnitude > 0.0001f
                ? hit.Direction.normalized
                : Vector3.forward;

            if (definition.CameraImpulse > 0.0f)
            {
                _camera.EmitImpact(point, direction, definition.CameraImpulse);
            }

            EmitParticles(point, direction, definition);
            PlayAudio(point, definition);
        }

        public void Stop()
        {
            if (_disposed)
            {
                return;
            }

            if (_particles != null)
            {
                _particles.Stop(
                    withChildren: true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            for (int i = 0; i < _audioSources.Length; i++)
            {
                if (_audioSources[i] != null)
                {
                    _audioSources[i].Stop();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Stop();
            _disposed = true;
            if (_particleMaterial != null)
            {
                Object.Destroy(_particleMaterial);
            }

            if (_particleTexture != null)
            {
                Object.Destroy(_particleTexture);
            }

            if (_root != null)
            {
                Object.Destroy(_root);
            }
        }

        private void EmitParticles(
            Vector3 point,
            Vector3 direction,
            CombatFeedbackDefinition definition)
        {
            if (!definition.HasParticles)
            {
                return;
            }

            if (!_particles.isPlaying)
            {
                _particles.Play(withChildren: false);
            }

            Vector3 awayFromAttack = -direction;
            var emit = new ParticleSystem.EmitParams
            {
                startColor = definition.ParticleColor,
                startLifetime = definition.ParticleLifetime,
                startSize = definition.ParticleSize
            };

            for (int i = 0; i < definition.ParticleCount; i++)
            {
                Vector3 randomDirection = UnityEngine.Random.onUnitSphere;
                Vector3 particleDirection = Vector3.Lerp(
                    awayFromAttack,
                    randomDirection,
                    definition.ParticleScatter);
                if (particleDirection.sqrMagnitude <= 0.0001f)
                {
                    particleDirection = randomDirection;
                }

                float speedScale = UnityEngine.Random.Range(0.75f, 1.2f);
                emit.position = point + randomDirection * (definition.ParticleSize * 0.25f);
                emit.velocity = particleDirection.normalized *
                    (definition.ParticleSpeed * speedScale);
                _particles.Emit(emit, 1);
            }
        }

        private void PlayAudio(Vector3 point, CombatFeedbackDefinition definition)
        {
            if (!definition.HasAudio)
            {
                return;
            }

            AudioSource source = FindAudioSource();
            source.transform.position = point;
            source.pitch = UnityEngine.Random.Range(
                definition.AudioPitchMin,
                definition.AudioPitchMax);
            source.PlayOneShot(definition.AudioClip, definition.AudioVolume);
        }

        private AudioSource FindAudioSource()
        {
            for (int offset = 0; offset < _audioSources.Length; offset++)
            {
                int index = (_nextAudioSource + offset) % _audioSources.Length;
                if (_audioSources[index].isPlaying)
                {
                    continue;
                }

                _nextAudioSource = (index + 1) % _audioSources.Length;
                return _audioSources[index];
            }

            AudioSource fallback = _audioSources[_nextAudioSource];
            _nextAudioSource = (_nextAudioSource + 1) % _audioSources.Length;
            fallback.Stop();
            return fallback;
        }

        private static Material ConfigureParticles(
            ParticleSystem particles,
            out Texture2D particleTexture)
        {
            particleTexture = null;
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = MaxParticles;
            main.startLifetime = 0.2f;
            main.startSpeed = 0.0f;
            main.startSize = 0.04f;
            main.gravityModifier = 0.15f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0.0f),
                    new GradientColorKey(Color.white, 1.0f)
                },
                new[]
                {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                });
            colorOverLifetime.color = gradient;

            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.cameraVelocityScale = 0.0f;
            renderer.velocityScale = 0.08f;
            renderer.lengthScale = 2.0f;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                            Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                renderer.enabled = false;
                return null;
            }

            var material = new Material(shader)
            {
                name = "Combat Impact Particles (Runtime)",
                hideFlags = HideFlags.DontSave
            };
            particleTexture = CreateParticleTexture();
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", particleTexture);
            }
            else if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", particleTexture);
            }

            renderer.sharedMaterial = material;
            return material;
        }

        private static Texture2D CreateParticleTexture()
        {
            const int width = 32;
            const int height = 8;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float vertical = Mathf.Abs((y + 0.5f) / height * 2.0f - 1.0f);
                float verticalAlpha = Mathf.Pow(1.0f - vertical, 1.75f);
                for (int x = 0; x < width; x++)
                {
                    float horizontal = (x + 0.5f) / width;
                    float horizontalAlpha = Mathf.Sin(horizontal * Mathf.PI);
                    byte alpha = (byte)Mathf.RoundToInt(
                        Mathf.Clamp01(verticalAlpha * horizontalAlpha) * 255.0f);
                    pixels[y * width + x] = new Color32(255, 255, 255, alpha);
                }
            }

            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                mipChain: false,
                linear: true)
            {
                name = "Combat Impact Streak (Runtime)",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            return texture;
        }

        private static AudioSource[] CreateAudioSources(Transform parent)
        {
            var sources = new AudioSource[AudioSourceCount];
            for (int i = 0; i < sources.Length; i++)
            {
                var audioObject = new GameObject($"Impact Audio {i + 1}");
                audioObject.transform.SetParent(parent, worldPositionStays: false);
                AudioSource source = audioObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = false;
                source.spatialBlend = 1.0f;
                source.dopplerLevel = 0.0f;
                source.rolloffMode = AudioRolloffMode.Logarithmic;
                source.minDistance = 0.5f;
                source.maxDistance = 15.0f;
                sources[i] = source;
            }

            return sources;
        }

        private Vector3 ResolveFallbackPoint(in CombatHitResult hit)
        {
            if (hit.Target != null)
            {
                return hit.Target.transform.position;
            }

            return _root != null ? _root.transform.position : Vector3.zero;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                   !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                   !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }
}
