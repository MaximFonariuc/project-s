using UnityEditor;
using UnityEngine;

namespace _App.Editor
{
    [CustomEditor(typeof(BodyColliderAuthoring))]
    public sealed class BodyColliderAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (BodyColliderAuthoring)target;
            EditorGUILayout.Space();

            if (authoring.Animator != null && !authoring.Animator.isHuman && authoring.GenericBones.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Generic biped bones will be inferred by common names. For creatures, unusual skeletons or ambiguous names, add explicit Generic Bones mappings.",
                    MessageType.Info);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Generate / Update"))
                {
                    BodyColliderGenerationResult result = BodyColliderGenerator.Generate(authoring);
                    LogResult(result, authoring);
                }

                if (GUILayout.Button("Clear Generated"))
                {
                    BodyColliderGenerationResult result = BodyColliderGenerator.ClearGenerated(authoring);
                    LogResult(result, authoring);
                }
            }
        }

        private static void LogResult(BodyColliderGenerationResult result, Object context)
        {
            if (result.Success)
            {
                Debug.Log(result.Message, context);
            }
            else
            {
                Debug.LogError(result.Message, context);
            }
        }
    }
}
