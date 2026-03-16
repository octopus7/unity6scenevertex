using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProceduralNatureMeshAssetGenerator
{
    public const string MeshOutputFolder = "Assets/GeneratedMeshes";
    public const string MaterialOutputFolder = "Assets/GeneratedMaterials";
    public const string SharedMaterialAssetPath = MaterialOutputFolder + "/" + ProceduralNatureAssetCatalog.SharedMaterialAssetName + ".mat";

    private const string SharedShaderAssetPath = "Assets/Shaders/SceneVertexVertexColor.shader";

    [MenuItem("Tools/SceneVertex/Generate Procedural Assets")]
    public static void GenerateAssets()
    {
        EnsureFolders();
        CleanupObsoleteAssets();

        var meshDefinitions = ProceduralNatureMeshFactory.CreateAllMeshes();

        AssetDatabase.StartAssetEditing();
        try
        {
            foreach (var definition in meshDefinitions)
            {
                EnsureLightmapUvChannel(definition.Mesh);
                SaveMeshAsset($"{MeshOutputFolder}/{definition.AssetName}.asset", definition.Mesh);
            }

            EnsureSharedMaterial();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Object>($"{MeshOutputFolder}/TreeA.asset");
        Debug.Log($"Generated {meshDefinitions.Count} mesh assets in {MeshOutputFolder} and one shared material in {MaterialOutputFolder}.");
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder(MeshOutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedMeshes");
        }

        if (!AssetDatabase.IsValidFolder(MaterialOutputFolder))
        {
            AssetDatabase.CreateFolder("Assets", "GeneratedMaterials");
        }
    }

    private static void CleanupObsoleteAssets()
    {
        var expectedMeshPaths = new HashSet<string>();
        foreach (var assetName in ProceduralNatureAssetCatalog.GetAllMeshAssetNames())
        {
            expectedMeshPaths.Add($"{MeshOutputFolder}/{assetName}.asset");
        }

        var expectedMaterialPaths = new HashSet<string>
        {
            SharedMaterialAssetPath
        };

        DeleteUnexpectedAssets(MeshOutputFolder, "t:Mesh", expectedMeshPaths);
        DeleteUnexpectedAssets(MaterialOutputFolder, "t:Material", expectedMaterialPaths);
    }

    private static void DeleteUnexpectedAssets(string folder, string searchFilter, HashSet<string> expectedPaths)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        var guids = AssetDatabase.FindAssets(searchFilter, new[] { folder });
        foreach (var guid in guids)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!expectedPaths.Contains(assetPath))
            {
                AssetDatabase.DeleteAsset(assetPath);
            }
        }
    }

    private static void SaveMeshAsset(string assetPath, Mesh sourceMesh)
    {
        var existingMesh = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
        if (existingMesh == null)
        {
            AssetDatabase.CreateAsset(sourceMesh, assetPath);
            EnsureLightmapUvChannel(sourceMesh);
            EditorUtility.SetDirty(sourceMesh);
            return;
        }

        EditorUtility.CopySerialized(sourceMesh, existingMesh);
        existingMesh.name = sourceMesh.name;
        EnsureLightmapUvChannel(existingMesh);
        Object.DestroyImmediate(sourceMesh);
        EditorUtility.SetDirty(existingMesh);
    }

    public static bool EnsureLightmapUvChannel(Mesh mesh)
    {
        if (mesh == null || mesh.vertexCount == 0 || mesh.triangles == null || mesh.triangles.Length == 0)
        {
            return false;
        }

        if (mesh.uv2 != null && mesh.uv2.Length == mesh.vertexCount)
        {
            return false;
        }

        Unwrapping.GenerateSecondaryUVSet(mesh);
        return true;
    }

    private static void EnsureSharedMaterial()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(SharedShaderAssetPath);
        if (shader == null)
        {
            shader = Shader.Find("SceneVertex/Vertex Color");
        }

        if (shader == null)
        {
            Debug.LogError("Could not find the shared vertex color shader.");
            return;
        }

        var material = AssetDatabase.LoadAssetAtPath<Material>(SharedMaterialAssetPath);
        if (material == null)
        {
            material = new Material(shader)
            {
                name = ProceduralNatureAssetCatalog.SharedMaterialAssetName,
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, SharedMaterialAssetPath);
            return;
        }

        if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.enableInstancing = true;
        EditorUtility.SetDirty(material);
    }
}
