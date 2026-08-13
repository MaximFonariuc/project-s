using UnityEngine;
using UnityEditor;

public class FbxRootSettingsWindow : EditorWindow
{
    // --- Rotation settings
    private bool bakeRotation = true;
    private enum RotationBased { BodyOrientation, Original }
    private RotationBased rotationBased = RotationBased.BodyOrientation;

    // --- Position Y settings
    private bool bakeY = true;
    private enum BasedY { Original }
    private BasedY positionYBased = BasedY.Original;

    // --- Position XZ settings
    private bool bakeXZ = true;
    private enum BasedXZ { CenterOfMass, Original }
    private BasedXZ positionXZBased = BasedXZ.CenterOfMass;

    private DefaultAsset targetFolder;

    [MenuItem("Tools/FBX Root Settings")]
    private static void ShowWindow()
    {
        var window = GetWindow<FbxRootSettingsWindow>();
        window.titleContent = new GUIContent("FBX Root Settings");
        window.minSize = new Vector2(400, 250);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.Label("FBX Root Motion Settings", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        // Choose folder
        targetFolder = (DefaultAsset)EditorGUILayout.ObjectField("Target Folder", targetFolder, typeof(DefaultAsset), false);

        EditorGUILayout.Space();

        // Rotation
        GUILayout.Label("Rotation", EditorStyles.boldLabel);
        bakeRotation = EditorGUILayout.Toggle("Bake Into Pose", bakeRotation);
        rotationBased = (RotationBased)EditorGUILayout.EnumPopup("Based Upon", rotationBased);

        EditorGUILayout.Space();

        // Position Y
        GUILayout.Label("Position Y", EditorStyles.boldLabel);
        bakeY = EditorGUILayout.Toggle("Bake Into Pose", bakeY);
        positionYBased = (BasedY)EditorGUILayout.EnumPopup("Based Upon", positionYBased);

        EditorGUILayout.Space();

        // Position XZ
        GUILayout.Label("Position XZ", EditorStyles.boldLabel);
        bakeXZ = EditorGUILayout.Toggle("Bake Into Pose", bakeXZ);
        positionXZBased = (BasedXZ)EditorGUILayout.EnumPopup("Based Upon", positionXZBased);

        EditorGUILayout.Space();
        EditorGUILayout.Space();

        if (GUILayout.Button("Apply To Folder", GUILayout.Height(40)))
        {
            ApplySettings();
        }
    }

    private void ApplySettings()
    {
        if (targetFolder == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a folder with FBX models!", "OK");
            return;
        }

        string folderPath = AssetDatabase.GetAssetPath(targetFolder);
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            EditorUtility.DisplayDialog("Error", "Selected object is not a folder!", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { folderPath });

        int count = 0;
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) continue;

            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                var clip = clips[i];

                clip.lockRootRotation = bakeRotation;
                clip.keepOriginalOrientation = (rotationBased == RotationBased.Original);

                clip.lockRootHeightY = bakeY;
                clip.keepOriginalPositionY = (positionYBased == BasedY.Original);

                clip.lockRootPositionXZ = bakeXZ;
                clip.keepOriginalPositionXZ = (positionXZBased == BasedXZ.Original);
            }

            importer.clipAnimations = clips;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            count++;
        }

        EditorUtility.DisplayDialog("Done", $"{count} FBX files updated.", "OK");
        Debug.Log($"[FBX Root Settings] Updated {count} files in {folderPath}");
    }
}
