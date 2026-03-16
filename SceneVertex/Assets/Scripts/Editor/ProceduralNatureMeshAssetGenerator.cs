using UnityEditor;
using UnityEngine;

public static class ProceduralNatureMeshAssetGenerator
{
    private const string OutputFolder = "Assets/GeneratedMeshes";

    [MenuItem("Tools/SceneVertex/Generate Vertex Mesh Assets")]
    public static void GenerateAssets()
    {
        EnsureOutputFolder();
        var meshes = ProceduralNatureMeshFactory.CreateAllMeshes();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var pair in meshes)
            {
                SaveMeshAsset($"{OutputFolder}/{pair.Key}.asset", pair.Value);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>($"{OutputFolder}/Tree.asset");
        Debug.Log($"Generated {meshes.Count} procedural mesh assets in {OutputFolder}.");
    }

    private static void EnsureOutputFolder()
    {
        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
        }
    }

    private static void SaveMeshAsset(string assetPath, Mesh sourceMesh)
    {
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(sourceMesh, assetPath);
            return;
        }

        EditorUtility.CopySerialized(sourceMesh, existingMesh);
        existingMesh.name = sourceMesh.name;
        Object.DestroyImmediate(sourceMesh);
        EditorUtility.SetDirty(existingMesh);
    }
}
