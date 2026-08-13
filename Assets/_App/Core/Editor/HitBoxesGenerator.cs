using _App;
using _App.Editor;
using UnityEditor;
using UnityEngine;

public static class HitBoxesGenerator
{
    [MenuItem("Tools/Project S/Body Colliders/Generate Selected", priority = 100)]
    [MenuItem("Tools/HitBoxesGenerator/Generate Hit Boxes")]
    private static void GenerateSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            Debug.LogWarning("Select the actor root that owns Health and Animator.");
            return;
        }

        BodyColliderAuthoring authoring = BodyColliderGenerator.GetOrAddAuthoring(selected);
        BodyColliderGenerationResult result = BodyColliderGenerator.Generate(authoring);
        Log(result, authoring);
    }

    [MenuItem("Tools/Project S/Body Colliders/Clear Generated", priority = 101)]
    [MenuItem("Tools/HitBoxesGenerator/Clear All Colliders")]
    private static void ClearGenerated()
    {
        GameObject selected = Selection.activeGameObject;
        BodyColliderAuthoring authoring = selected != null ? selected.GetComponent<BodyColliderAuthoring>() : null;
        BodyColliderGenerationResult result = BodyColliderGenerator.ClearGenerated(authoring);
        Log(result, authoring);
    }

    [MenuItem("Tools/Project S/Body Colliders/Generate Selected", true)]
    [MenuItem("Tools/Project S/Body Colliders/Clear Generated", true)]
    private static bool ValidateActorSelection()
    {
        return Selection.activeGameObject != null;
    }

    private static void Log(BodyColliderGenerationResult result, Object context)
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
