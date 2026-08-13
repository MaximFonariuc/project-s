using UnityEditor;
using UnityEngine;
using System.IO;

public class FBXImportUtility : Editor
{
    [MenuItem("Assets/Disable Mesh and Material Import (Recursive for FBX)", false, 1001)]
    private static void DisableMeshAndMaterialImport()
    {
        string folderPath = AssetDatabase.GetAssetPath(Selection.activeObject);

        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] files = Directory.GetFiles(folderPath, "*.fbx", SearchOption.AllDirectories);

        foreach (string filePath in files)
        {
            string assetPath = filePath.Replace("\\", "/");
            ModelImporter modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;

            if (modelImporter != null)
            {
                modelImporter.importBlendShapes = false;
                modelImporter.importVisibility = false;
                modelImporter.importCameras = false;
                modelImporter.importLights = false;
                modelImporter.preserveHierarchy = false;
                modelImporter.materialImportMode = ModelImporterMaterialImportMode.None;

                modelImporter.SaveAndReimport();
            }
        }

        Debug.Log("Импорт геометрии и материалов отключён.");
    }
}