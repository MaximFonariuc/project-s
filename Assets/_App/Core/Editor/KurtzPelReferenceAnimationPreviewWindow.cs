using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace _App.Editor.Animation
{
    internal sealed class KurtzPelReferenceAnimationPreviewWindow : EditorWindow
    {
        private const float KurtzPelVisualYaw = -90.0f;

        private const string RootFolder =
            "Assets/_Game/Animations/Reference/KurtzPel/MaleComplete";

        private const string PreviewPrefabPath =
            RootFolder + "/Preview/KurtzPel_MaleReference.prefab";

        private static readonly PackDefinition[] Packs =
        {
            new("Movement / Common Ninja", RootFolder + "/Movement/KurtzPel_Common_Ninja.fbx"),
            new("Movement / Directional", RootFolder + "/Movement/KurtzPel_Male_Directional.fbx"),
            new("Movement / ENFJ", RootFolder + "/Movement/KurtzPel_Male_ENFJ.fbx"),
            new("Movement / ENTP", RootFolder + "/Movement/KurtzPel_Male_ENTP.fbx"),
            new("Movement / ESFP", RootFolder + "/Movement/KurtzPel_Male_ESFP.fbx"),
            new("Movement / ESTJ", RootFolder + "/Movement/KurtzPel_Male_ESTJ.fbx"),
            new("Movement / INFP", RootFolder + "/Movement/KurtzPel_Male_INFP.fbx"),
            new("Movement / INTJ", RootFolder + "/Movement/KurtzPel_Male_INTJ.fbx"),
            new("Movement / ISTJ", RootFolder + "/Movement/KurtzPel_Male_ISTJ.fbx"),
            new("Movement / ISTP", RootFolder + "/Movement/KurtzPel_Male_ISTP.fbx"),
            new("Reactions", RootFolder + "/Reactions/KurtzPel_Male_Reactions.fbx"),
            new("Misc", RootFolder + "/Misc/KurtzPel_Male_Misc.fbx"),
            new("Combat / Blazing Fist", RootFolder + "/Combat/KurtzPel_BlazingFist.fbx")
        };

        private static readonly string[] PackNames = Packs
            .Select(pack => pack.DisplayName)
            .ToArray();

        [SerializeField] private int _packIndex;
        [SerializeField] private string _searchText = string.Empty;
        [SerializeField] private float _playbackSpeed = 1f;
        [SerializeField] private bool _loop = true;

        private GameObject _target;
        private AnimationClip[] _allClips = Array.Empty<AnimationClip>();
        private AnimationClip[] _visibleClips = Array.Empty<AnimationClip>();
        private AnimationClip _selectedClip;
        private float _sampleTime;
        private bool _isPlaying;
        private double _lastUpdateTime;

        [MenuItem("Tools/Animation/KurtzPel/Open Animation Preview")]
        private static void OpenWindow()
        {
            GetWindow<KurtzPelReferenceAnimationPreviewWindow>("KurtzPel Preview");
        }

        private void OnEnable()
        {
            _packIndex = Mathf.Clamp(_packIndex, 0, Packs.Length - 1);
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
            EditorGUILayout.LabelField("KurtzPel scene animation preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Spawn the reference character into the current scene, then play or scrub " +
                "the imported clips without an Animator Controller.",
                MessageType.Info);

            DrawTargetSelection();
            EditorGUILayout.Space();
            DrawClipSelection();
            EditorGUILayout.Space();
            DrawPlaybackControls();
        }

        private void DrawTargetSelection()
        {
            EditorGUI.BeginChangeCheck();
            _target = (GameObject)EditorGUILayout.ObjectField(
                "Scene target",
                _target,
                typeof(GameObject),
                allowSceneObjects: true);
            if (EditorGUI.EndChangeCheck())
            {
                StopSampling();
                SampleCurrentFrame();
            }

            if (GUILayout.Button("Spawn KurtzPel reference target"))
            {
                SpawnReferenceTarget();
            }
        }

        private void DrawClipSelection()
        {
            EditorGUI.BeginChangeCheck();
            int packIndex = EditorGUILayout.Popup("Pack", _packIndex, PackNames);
            if (EditorGUI.EndChangeCheck())
            {
                _packIndex = packIndex;
                StopSampling();
                ReloadClips();
            }

            EditorGUI.BeginChangeCheck();
            string searchText = EditorGUILayout.TextField("Search", _searchText);
            if (EditorGUI.EndChangeCheck())
            {
                _searchText = searchText;
                ApplySearchFilter();
            }

            if (_visibleClips.Length == 0)
            {
                string message = _allClips.Length == 0
                    ? $"No clips found in {Packs[_packIndex].AssetPath}."
                    : "No clips match the current search.";
                EditorGUILayout.HelpBox(message, MessageType.Warning);
                return;
            }

            string[] clipNames = _visibleClips.Select(clip => clip.name).ToArray();
            int selectedIndex = Math.Max(0, Array.IndexOf(_visibleClips, _selectedClip));

            EditorGUI.BeginChangeCheck();
            selectedIndex = EditorGUILayout.Popup("Clip", selectedIndex, clipNames);
            if (EditorGUI.EndChangeCheck())
            {
                SelectClip(_visibleClips[selectedIndex]);
            }

            EditorGUILayout.LabelField(
                "Clips",
                $"{_visibleClips.Length} shown / {_allClips.Length} in pack");
        }

        private void DrawPlaybackControls()
        {
            AnimationClip clip = _selectedClip;
            using (new EditorGUI.DisabledScope(clip == null || !IsValidSceneTarget()))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(_isPlaying ? "Pause" : "Play"))
                {
                    TogglePlayback();
                }

                if (GUILayout.Button("Stop"))
                {
                    StopSampling();
                    _sampleTime = 0f;
                }
                EditorGUILayout.EndHorizontal();

                _loop = EditorGUILayout.Toggle("Loop", _loop);
                _playbackSpeed = EditorGUILayout.Slider("Speed", _playbackSpeed, 0.1f, 2f);

                float clipLength = clip != null ? clip.length : 0f;
                EditorGUI.BeginChangeCheck();
                _sampleTime = EditorGUILayout.Slider("Time", _sampleTime, 0f, clipLength);
                if (EditorGUI.EndChangeCheck())
                {
                    _isPlaying = false;
                    SampleCurrentFrame();
                }

                if (clip != null)
                {
                    int currentFrame = Mathf.RoundToInt(_sampleTime * clip.frameRate);
                    int totalFrames = Mathf.RoundToInt(clip.length * clip.frameRate);
                    EditorGUILayout.LabelField(
                        "Position",
                        $"{_sampleTime:F3} / {clip.length:F3} s    " +
                        $"frame {currentFrame} / {totalFrames}    {clip.frameRate:F0} fps");
                }
            }
        }

        private void TickPlayback()
        {
            if (!_isPlaying)
            {
                return;
            }

            if (_selectedClip == null || !IsValidSceneTarget())
            {
                StopSampling();
                Repaint();
                return;
            }

            double currentTime = EditorApplication.timeSinceStartup;
            float deltaTime = Mathf.Min((float)(currentTime - _lastUpdateTime), 0.1f);
            _lastUpdateTime = currentTime;

            float nextTime = _sampleTime + deltaTime * _playbackSpeed;
            if (_loop && _selectedClip.length > 0f)
            {
                _sampleTime = Mathf.Repeat(nextTime, _selectedClip.length);
            }
            else
            {
                _sampleTime = Mathf.Min(nextTime, _selectedClip.length);
                if (_sampleTime >= _selectedClip.length)
                {
                    _isPlaying = false;
                }
            }

            SampleCurrentFrame();
            Repaint();
        }

        private void TogglePlayback()
        {
            if (_selectedClip == null || !IsValidSceneTarget())
            {
                return;
            }

            if (!_isPlaying && _sampleTime >= _selectedClip.length)
            {
                _sampleTime = 0f;
            }

            _isPlaying = !_isPlaying;
            _lastUpdateTime = EditorApplication.timeSinceStartup;
            SampleCurrentFrame();
        }

        private void ReloadClips()
        {
            _allClips = AssetDatabase.LoadAllAssetsAtPath(Packs[_packIndex].AssetPath)
                .OfType<AnimationClip>()
                .Where(clip => !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                .OrderBy(clip => clip.name, StringComparer.Ordinal)
                .ToArray();
            _selectedClip = null;
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string search = _searchText?.Trim() ?? string.Empty;
            _visibleClips = string.IsNullOrEmpty(search)
                ? _allClips
                : _allClips
                    .Where(clip => clip.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToArray();

            if (_selectedClip == null || Array.IndexOf(_visibleClips, _selectedClip) < 0)
            {
                SelectClip(_visibleClips.FirstOrDefault());
            }
        }

        private void SelectClip(AnimationClip clip)
        {
            StopSampling();
            _selectedClip = clip;
            _sampleTime = 0f;
            SampleCurrentFrame();
        }

        private void SpawnReferenceTarget()
        {
            StopSampling();

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PreviewPrefabPath);
            if (prefab == null)
            {
                Debug.LogError(
                    "KurtzPel reference prefab is missing. Run " +
                    "Tools/Animation/KurtzPel/Configure Reference Pack first.");
                return;
            }

            _target = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            _target.transform.rotation = Quaternion.Euler(0.0f, KurtzPelVisualYaw, 0.0f);
            Undo.RegisterCreatedObjectUndo(_target, "Spawn KurtzPel reference target");
            Selection.activeGameObject = _target;
            SceneView.lastActiveSceneView?.FrameSelected();
            _sampleTime = 0f;
            SampleCurrentFrame();
        }

        private void SampleCurrentFrame()
        {
            if (_selectedClip == null || !IsValidSceneTarget())
            {
                return;
            }

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(_target, _selectedClip, _sampleTime);
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

        private bool IsValidSceneTarget()
        {
            return _target != null && !EditorUtility.IsPersistent(_target);
        }

        private readonly struct PackDefinition
        {
            public PackDefinition(string displayName, string assetPath)
            {
                DisplayName = displayName;
                AssetPath = assetPath;
            }

            public string DisplayName { get; }
            public string AssetPath { get; }
        }
    }
}
