using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal sealed class Pso2ReferenceAnimationPreviewWindow : EditorWindow
    {
        private const string RootFolder = "Assets/_Game/Animations/Reference/PSO2NGS";

        private enum BodyVariant
        {
            Female200000,
            Male100000
        }

        private enum AnimationPack
        {
            CommonLocomotion,
            DualBladeLocomotion,
            DualBladeCombat
        }

        private BodyVariant _bodyVariant = BodyVariant.Male100000;
        private AnimationPack _animationPack;
        private GameObject _target;
        private AnimationClip[] _clips = Array.Empty<AnimationClip>();
        private int _clipIndex;
        private float _sampleTime;
        private bool _isPlaying;
        private double _lastUpdateTime;

        [MenuItem("Tools/Animation/PSO2 NGS/Open Reference Preview")]
        private static void OpenWindow()
        {
            GetWindow<Pso2ReferenceAnimationPreviewWindow>("PSO2 NGS Preview");
        }

        private void OnEnable()
        {
            EditorApplication.update += TickPlayback;
            ReloadClips();
        }

        private void OnDisable()
        {
            EditorApplication.update -= TickPlayback;
            StopSampling();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Native Generic reference playback", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "The preview preserves the complete PSO2 skeleton. Spawn a local reference instance, " +
                "then play or scrub any imported clip without an Animator Controller.",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            _bodyVariant = (BodyVariant)EditorGUILayout.EnumPopup("Body", _bodyVariant);
            _animationPack = (AnimationPack)EditorGUILayout.EnumPopup("Pack", _animationPack);
            if (EditorGUI.EndChangeCheck())
            {
                StopSampling();
                ReloadClips();
            }

            EditorGUI.BeginChangeCheck();
            _target = (GameObject)EditorGUILayout.ObjectField(
                "Scene target",
                _target,
                typeof(GameObject),
                allowSceneObjects: true);
            if (EditorGUI.EndChangeCheck())
            {
                StopSampling();
            }

            if (GUILayout.Button($"Spawn {_bodyVariant} reference target"))
            {
                SpawnReferenceTarget();
            }

            if (_clips.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    $"No clips found at {GetPackPath()}.",
                    MessageType.Warning);
                return;
            }

            string[] clipNames = _clips.Select(clip => clip.name).ToArray();
            EditorGUI.BeginChangeCheck();
            _clipIndex = EditorGUILayout.Popup("Clip", _clipIndex, clipNames);
            if (EditorGUI.EndChangeCheck())
            {
                StopSampling();
                _sampleTime = 0f;
                SampleCurrentFrame();
            }

            AnimationClip clip = _clips[_clipIndex];
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(_target == null))
            {
                if (GUILayout.Button(_isPlaying ? "Pause" : "Play"))
                {
                    _isPlaying = !_isPlaying;
                    _lastUpdateTime = EditorApplication.timeSinceStartup;
                    SampleCurrentFrame();
                }

                if (GUILayout.Button("Stop"))
                {
                    StopSampling();
                    _sampleTime = 0f;
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            _sampleTime = EditorGUILayout.Slider("Time", _sampleTime, 0f, clip.length);
            if (EditorGUI.EndChangeCheck())
            {
                _isPlaying = false;
                SampleCurrentFrame();
            }

            EditorGUILayout.LabelField(
                "Duration",
                $"{clip.length:F3} s at {clip.frameRate:F0} fps");
        }

        private void TickPlayback()
        {
            if (!_isPlaying || _target == null || _clips.Length == 0)
            {
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = (float)(currentTime - _lastUpdateTime);
            _lastUpdateTime = currentTime;

            AnimationClip clip = _clips[_clipIndex];
            _sampleTime = clip.length > 0f
                ? Mathf.Repeat(_sampleTime + deltaTime, clip.length)
                : 0f;

            SampleCurrentFrame();
            Repaint();
        }

        private void ReloadClips()
        {
            _clips = AssetDatabase.LoadAllAssetsAtPath(GetPackPath())
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => clip.name, StringComparer.Ordinal)
                .ToArray();
            _clipIndex = Mathf.Clamp(_clipIndex, 0, Math.Max(0, _clips.Length - 1));
            _sampleTime = 0f;
        }

        private string GetPackPath()
        {
            string bodyFolder = _bodyVariant.ToString();
            return _animationPack switch
            {
                AnimationPack.CommonLocomotion =>
                    $"{RootFolder}/{bodyFolder}/Common/PSO2NGS_Common_Locomotion.fbx",
                AnimationPack.DualBladeLocomotion =>
                    $"{RootFolder}/{bodyFolder}/DualBlade/PSO2NGS_DualBlade_Locomotion.fbx",
                AnimationPack.DualBladeCombat =>
                    $"{RootFolder}/{bodyFolder}/DualBlade/PSO2NGS_DualBlade_CombatCandidates.fbx",
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        private string GetPrefabPath()
        {
            return $"{RootFolder}/Preview/PSO2NGS_ReferenceCharacter_{_bodyVariant}.prefab";
        }

        private void SpawnReferenceTarget()
        {
            StopSampling();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GetPrefabPath());
            if (prefab == null)
            {
                Debug.LogError(
                    "PSO2 NGS reference prefab is missing. Run " +
                    "Tools/Animation/PSO2 NGS/Configure Reference FBX first.");
                return;
            }

            _target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(_target, "Spawn PSO2 NGS reference target");
            Selection.activeGameObject = _target;
            SceneView.lastActiveSceneView?.FrameSelected();
            _sampleTime = 0f;
            SampleCurrentFrame();
        }

        private void SampleCurrentFrame()
        {
            if (_target == null || _clips.Length == 0 || EditorUtility.IsPersistent(_target))
            {
                return;
            }

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_target, _clips[_clipIndex], _sampleTime);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        private void StopSampling()
        {
            _isPlaying = false;
            if (AnimationMode.InAnimationMode())
            {
                AnimationMode.StopAnimationMode();
                SceneView.RepaintAll();
            }
        }
    }
}
