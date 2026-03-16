using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SceneVertexBundleAnalysisWindow : EditorWindow
{
    private enum MeshResidencyKind
    {
        SceneEmbedded,
        SameSceneBundleAsset,
        ExternalBundleAsset,
        UnassignedProjectAsset,
        BuiltinOrPackageAsset
    }

    private sealed class CategorySummary
    {
        public string Label { get; set; }
        public string Description { get; set; }
        public int ObjectCount { get; set; }
        public int UniqueMeshCount { get; set; }
        public long VertexInstances { get; set; }
    }

    private sealed class MeshUsageEntry
    {
        public MeshResidencyKind ResidencyKind { get; set; }
        public string ResidencyLabel { get; set; }
        public string MeshName { get; set; }
        public string AssetPath { get; set; }
        public string BundleName { get; set; }
        public int VertexCountPerMesh { get; set; }
        public int InstanceCount { get; set; }
        public long TotalVertexInstances { get; set; }
        public string ExampleObjectPath { get; set; }
    }

    [SerializeField] private Vector2 summaryScrollPosition;
    [SerializeField] private string analyzedSceneName = string.Empty;
    [SerializeField] private string analyzedScenePath = string.Empty;
    [SerializeField] private string analyzedSceneBundleName = string.Empty;
    [SerializeField] private string analyzedSceneBundleFilePath = string.Empty;
    [SerializeField] private long analyzedSceneBundleSizeBytes;
    [SerializeField] private int totalMeshObjectCount;
    [SerializeField] private int totalUniqueMeshCount;
    [SerializeField] private long totalVertexInstances;
    [SerializeField] private long likelyInsideSceneBundleVertices;
    [SerializeField] private long likelyExternalBundleVertices;
    [SerializeField] private long builtinOrPackageVertices;
    [SerializeField] private List<CategorySummary> categorySummaries = new();
    [SerializeField] private List<MeshUsageEntry> usageEntries = new();

    [MenuItem("Tools/SceneVertex/Scene Vertex Analysis")]
    public static void OpenWindow()
    {
        var window = GetWindow<SceneVertexBundleAnalysisWindow>("Scene Vertex Analysis");
        window.minSize = new Vector2(760f, 560f);
        window.AnalyzeActiveScene();
    }

    private void OnEnable()
    {
        EnsureState();
        if (string.IsNullOrEmpty(analyzedSceneName))
        {
            AnalyzeActiveScene();
        }
    }

    private void OnGUI()
    {
        EnsureState();
        EditorGUILayout.LabelField("Scene Vertex Analysis", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "This view classifies scene mesh references by where their vertex data is likely to live for AssetBundle builds: scene-embedded, same scene bundle, external bundle, unassigned project asset, or built-in/package asset.",
            MessageType.Info);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!EditorSceneManager.GetActiveScene().IsValid()))
            {
                if (GUILayout.Button("Analyze Active Scene", GUILayout.Height(28f)))
                {
                    AnalyzeActiveScene();
                }
            }

            if (GUILayout.Button("Clear Report", GUILayout.Height(28f)))
            {
                ClearReport();
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(analyzedSceneName) ? "(none)" : analyzedSceneName);
            EditorGUILayout.LabelField("Scene Path", string.IsNullOrEmpty(analyzedScenePath) ? "(none)" : analyzedScenePath);
            EditorGUILayout.LabelField("Scene Bundle", string.IsNullOrEmpty(analyzedSceneBundleName) ? "(unassigned)" : analyzedSceneBundleName);
            EditorGUILayout.LabelField("Scene Bundle Size", GetSceneBundleSizeLabel());
            EditorGUILayout.LabelField("Bundle Output", string.IsNullOrEmpty(analyzedSceneBundleFilePath) ? "(not built for current target)" : analyzedSceneBundleFilePath);
            EditorGUILayout.LabelField("Mesh Objects", totalMeshObjectCount.ToString("N0"));
            EditorGUILayout.LabelField("Unique Meshes", totalUniqueMeshCount.ToString("N0"));
            EditorGUILayout.LabelField("Vertex Instances", totalVertexInstances.ToString("N0"));
            EditorGUILayout.LabelField("Likely In Scene Bundle", likelyInsideSceneBundleVertices.ToString("N0"));
            EditorGUILayout.LabelField("Likely External Bundle", likelyExternalBundleVertices.ToString("N0"));
            EditorGUILayout.LabelField("Builtin/Package", builtinOrPackageVertices.ToString("N0"));
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Category Summary", EditorStyles.boldLabel);
            foreach (var summary in categorySummaries)
            {
                using (new EditorGUILayout.VerticalScope("box"))
                {
                    EditorGUILayout.LabelField(summary.Label, EditorStyles.boldLabel);
                    EditorGUILayout.LabelField(summary.Description, EditorStyles.wordWrappedLabel);
                    EditorGUILayout.LabelField("Mesh Objects", summary.ObjectCount.ToString("N0"));
                    EditorGUILayout.LabelField("Unique Meshes", summary.UniqueMeshCount.ToString("N0"));
                    EditorGUILayout.LabelField("Vertex Instances", summary.VertexInstances.ToString("N0"));
                }
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Mesh Usage Detail", EditorStyles.boldLabel);
            summaryScrollPosition = EditorGUILayout.BeginScrollView(summaryScrollPosition, GUILayout.MinHeight(260f));

            if (usageEntries.Count == 0)
            {
                EditorGUILayout.HelpBox("No analysis data. Run 'Analyze Active Scene' first.", MessageType.None);
            }
            else
            {
                foreach (var entry in usageEntries)
                {
                    using (new EditorGUILayout.VerticalScope("box"))
                    {
                        EditorGUILayout.LabelField($"{entry.MeshName} [{entry.ResidencyLabel}]", EditorStyles.boldLabel);
                        EditorGUILayout.LabelField("Asset Path", string.IsNullOrEmpty(entry.AssetPath) ? "(scene-local or built-in)" : entry.AssetPath);
                        EditorGUILayout.LabelField("Bundle", string.IsNullOrEmpty(entry.BundleName) ? "(none)" : entry.BundleName);
                        EditorGUILayout.LabelField("Instances", entry.InstanceCount.ToString("N0"));
                        EditorGUILayout.LabelField("Vertices Per Mesh", entry.VertexCountPerMesh.ToString("N0"));
                        EditorGUILayout.LabelField("Total Vertex Instances", entry.TotalVertexInstances.ToString("N0"));
                        EditorGUILayout.LabelField("Example Object", entry.ExampleObjectPath);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void AnalyzeActiveScene()
    {
        EnsureState();
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            ClearReport();
            return;
        }

        analyzedSceneName = scene.name;
        analyzedScenePath = scene.path;
        analyzedSceneBundleName = GetAssetBundleName(scene.path);
        analyzedSceneBundleFilePath = GetBundleFilePath(analyzedSceneBundleName);
        analyzedSceneBundleSizeBytes = GetBundleFileSize(analyzedSceneBundleFilePath);

        totalMeshObjectCount = 0;
        totalVertexInstances = 0;
        likelyInsideSceneBundleVertices = 0;
        likelyExternalBundleVertices = 0;
        builtinOrPackageVertices = 0;
        categorySummaries.Clear();
        usageEntries.Clear();

        var groupedEntries = new Dictionary<string, MeshUsageEntry>(StringComparer.OrdinalIgnoreCase);
        var categoryUniqueMeshes = new Dictionary<MeshResidencyKind, HashSet<string>>();
        var categoryObjectCounts = new Dictionary<MeshResidencyKind, int>();
        var categoryVertexCounts = new Dictionary<MeshResidencyKind, long>();
        var uniqueMeshKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var rootObject in scene.GetRootGameObjects())
        {
            AnalyzeTransformRecursive(
                rootObject.transform,
                analyzedSceneBundleName,
                groupedEntries,
                categoryUniqueMeshes,
                categoryObjectCounts,
                categoryVertexCounts,
                uniqueMeshKeys);
        }

        totalUniqueMeshCount = uniqueMeshKeys.Count;
        usageEntries = groupedEntries.Values
            .OrderByDescending(entry => entry.TotalVertexInstances)
            .ThenBy(entry => entry.MeshName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var kind in Enum.GetValues(typeof(MeshResidencyKind)).Cast<MeshResidencyKind>())
        {
            categoryObjectCounts.TryGetValue(kind, out var objectCount);
            categoryVertexCounts.TryGetValue(kind, out var vertexInstances);
            categoryUniqueMeshes.TryGetValue(kind, out var uniqueMeshes);

            categorySummaries.Add(new CategorySummary
            {
                Label = GetResidencyLabel(kind),
                Description = GetResidencyDescription(kind),
                ObjectCount = objectCount,
                UniqueMeshCount = uniqueMeshes?.Count ?? 0,
                VertexInstances = vertexInstances
            });
        }
    }

    private void AnalyzeTransformRecursive(
        Transform current,
        string sceneBundleName,
        Dictionary<string, MeshUsageEntry> groupedEntries,
        Dictionary<MeshResidencyKind, HashSet<string>> categoryUniqueMeshes,
        Dictionary<MeshResidencyKind, int> categoryObjectCounts,
        Dictionary<MeshResidencyKind, long> categoryVertexCounts,
        HashSet<string> uniqueMeshKeys)
    {
        var embeddedMesh = current.GetComponent<SceneVertexEmbeddedMeshData>();
        if (embeddedMesh != null)
        {
            embeddedMesh.RebuildMesh();
        }

        var meshFilter = current.GetComponent<MeshFilter>();
        var meshRenderer = current.GetComponent<MeshRenderer>();
        AddMeshUsage(
            meshFilter != null ? meshFilter.sharedMesh : null,
            meshRenderer != null,
            current,
            sceneBundleName,
            groupedEntries,
            categoryUniqueMeshes,
            categoryObjectCounts,
            categoryVertexCounts,
            uniqueMeshKeys);

        var skinnedMeshRenderer = current.GetComponent<SkinnedMeshRenderer>();
        AddMeshUsage(
            skinnedMeshRenderer != null ? skinnedMeshRenderer.sharedMesh : null,
            skinnedMeshRenderer != null,
            current,
            sceneBundleName,
            groupedEntries,
            categoryUniqueMeshes,
            categoryObjectCounts,
            categoryVertexCounts,
            uniqueMeshKeys);

        for (var i = 0; i < current.childCount; i++)
        {
            AnalyzeTransformRecursive(
                current.GetChild(i),
                sceneBundleName,
                groupedEntries,
                categoryUniqueMeshes,
                categoryObjectCounts,
                categoryVertexCounts,
                uniqueMeshKeys);
        }
    }

    private void AddMeshUsage(
        Mesh mesh,
        bool hasRenderer,
        Transform transform,
        string sceneBundleName,
        Dictionary<string, MeshUsageEntry> groupedEntries,
        Dictionary<MeshResidencyKind, HashSet<string>> categoryUniqueMeshes,
        Dictionary<MeshResidencyKind, int> categoryObjectCounts,
        Dictionary<MeshResidencyKind, long> categoryVertexCounts,
        HashSet<string> uniqueMeshKeys)
    {
        if (!hasRenderer || mesh == null)
        {
            return;
        }

        var assetPath = AssetDatabase.GetAssetPath(mesh);
        var bundleName = GetAssetBundleName(assetPath);
        var residencyKind = ClassifyMesh(mesh, assetPath, bundleName, sceneBundleName);
        var meshKey = GetMeshKey(mesh, assetPath);
        var groupingKey = $"{residencyKind}|{meshKey}|{bundleName}";

        if (!groupedEntries.TryGetValue(groupingKey, out var entry))
        {
            entry = new MeshUsageEntry
            {
                ResidencyKind = residencyKind,
                ResidencyLabel = GetResidencyLabel(residencyKind),
                MeshName = mesh.name,
                AssetPath = assetPath,
                BundleName = bundleName,
                VertexCountPerMesh = mesh.vertexCount,
                ExampleObjectPath = GetTransformPath(transform)
            };
            groupedEntries[groupingKey] = entry;
        }

        entry.InstanceCount++;
        entry.TotalVertexInstances += mesh.vertexCount;

        totalMeshObjectCount++;
        totalVertexInstances += mesh.vertexCount;

        if (!categoryUniqueMeshes.TryGetValue(residencyKind, out var uniqueMeshes))
        {
            uniqueMeshes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            categoryUniqueMeshes[residencyKind] = uniqueMeshes;
        }

        uniqueMeshes.Add(meshKey);
        uniqueMeshKeys.Add($"{residencyKind}|{meshKey}");
        categoryObjectCounts[residencyKind] = categoryObjectCounts.GetValueOrDefault(residencyKind) + 1;
        categoryVertexCounts[residencyKind] = categoryVertexCounts.GetValueOrDefault(residencyKind) + mesh.vertexCount;

        switch (residencyKind)
        {
            case MeshResidencyKind.SceneEmbedded:
            case MeshResidencyKind.SameSceneBundleAsset:
            case MeshResidencyKind.UnassignedProjectAsset:
                likelyInsideSceneBundleVertices += mesh.vertexCount;
                break;
            case MeshResidencyKind.ExternalBundleAsset:
                likelyExternalBundleVertices += mesh.vertexCount;
                break;
            case MeshResidencyKind.BuiltinOrPackageAsset:
                builtinOrPackageVertices += mesh.vertexCount;
                break;
        }
    }

    private static MeshResidencyKind ClassifyMesh(Mesh mesh, string assetPath, string meshBundleName, string sceneBundleName)
    {
        if (mesh == null)
        {
            return MeshResidencyKind.SceneEmbedded;
        }

        if (!EditorUtility.IsPersistent(mesh))
        {
            return MeshResidencyKind.SceneEmbedded;
        }

        if (string.IsNullOrEmpty(assetPath))
        {
            return MeshResidencyKind.BuiltinOrPackageAsset;
        }

        if (!assetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return MeshResidencyKind.BuiltinOrPackageAsset;
        }

        if (string.IsNullOrEmpty(meshBundleName))
        {
            return MeshResidencyKind.UnassignedProjectAsset;
        }

        if (!string.IsNullOrEmpty(sceneBundleName) && string.Equals(meshBundleName, sceneBundleName, StringComparison.OrdinalIgnoreCase))
        {
            return MeshResidencyKind.SameSceneBundleAsset;
        }

        return MeshResidencyKind.ExternalBundleAsset;
    }

    private static string GetAssetBundleName(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return string.Empty;
        }

        var importer = AssetImporter.GetAtPath(assetPath);
        return importer != null ? importer.assetBundleName : string.Empty;
    }

    private static string GetMeshKey(Mesh mesh, string assetPath)
    {
        return string.IsNullOrEmpty(assetPath) ? $"scene:{mesh.GetInstanceID()}" : assetPath;
    }

    private static string GetTransformPath(Transform transform)
    {
        var names = new Stack<string>();
        var current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static string GetResidencyLabel(MeshResidencyKind kind)
    {
        return kind switch
        {
            MeshResidencyKind.SceneEmbedded => "Scene Embedded",
            MeshResidencyKind.SameSceneBundleAsset => "Same Scene Bundle Asset",
            MeshResidencyKind.ExternalBundleAsset => "External Bundle Asset",
            MeshResidencyKind.UnassignedProjectAsset => "Unassigned Project Asset",
            MeshResidencyKind.BuiltinOrPackageAsset => "Builtin Or Package Asset",
            _ => kind.ToString()
        };
    }

    private static string GetResidencyDescription(MeshResidencyKind kind)
    {
        return kind switch
        {
            MeshResidencyKind.SceneEmbedded => "Mesh is stored directly in the scene, so its vertex data lives with the scene or scene bundle.",
            MeshResidencyKind.SameSceneBundleAsset => "Mesh is a project asset and is explicitly assigned to the same AssetBundle as the scene.",
            MeshResidencyKind.ExternalBundleAsset => "Mesh is a project asset assigned to a different AssetBundle, so the scene should reference it externally.",
            MeshResidencyKind.UnassignedProjectAsset => "Mesh is a project asset with no AssetBundle assignment. If the scene is bundled, Unity will usually include it implicitly in the scene bundle.",
            MeshResidencyKind.BuiltinOrPackageAsset => "Mesh comes from built-in resources or a package path rather than a project asset under Assets/.",
            _ => string.Empty
        };
    }

    private void ClearReport()
    {
        EnsureState();
        analyzedSceneName = string.Empty;
        analyzedScenePath = string.Empty;
        analyzedSceneBundleName = string.Empty;
        analyzedSceneBundleFilePath = string.Empty;
        analyzedSceneBundleSizeBytes = 0;
        totalMeshObjectCount = 0;
        totalUniqueMeshCount = 0;
        totalVertexInstances = 0;
        likelyInsideSceneBundleVertices = 0;
        likelyExternalBundleVertices = 0;
        builtinOrPackageVertices = 0;
        categorySummaries.Clear();
        usageEntries.Clear();
        Repaint();
    }

    private void EnsureState()
    {
        categorySummaries ??= new List<CategorySummary>();
        usageEntries ??= new List<MeshUsageEntry>();
    }

    private string GetSceneBundleSizeLabel()
    {
        if (string.IsNullOrEmpty(analyzedSceneBundleName))
        {
            return "(unassigned)";
        }

        if (string.IsNullOrEmpty(analyzedSceneBundleFilePath) || analyzedSceneBundleSizeBytes <= 0)
        {
            return "(not built for current target)";
        }

        return FormatSize(analyzedSceneBundleSizeBytes);
    }

    private static string GetBundleFilePath(string bundleName)
    {
        if (string.IsNullOrEmpty(bundleName))
        {
            return string.Empty;
        }

        var outputFolder = Path.Combine(
            Directory.GetCurrentDirectory(),
            "AssetBundleBuilds",
            EditorUserBuildSettings.activeBuildTarget.ToString());
        var bundleFilePath = Path.Combine(outputFolder, bundleName);
        return File.Exists(bundleFilePath) ? bundleFilePath : string.Empty;
    }

    private static long GetBundleFileSize(string bundleFilePath)
    {
        return string.IsNullOrEmpty(bundleFilePath) || !File.Exists(bundleFilePath)
            ? 0L
            : new FileInfo(bundleFilePath).Length;
    }

    private static string FormatSize(long sizeBytes)
    {
        const double kilo = 1024d;
        const double mega = kilo * 1024d;
        const double giga = mega * 1024d;

        if (sizeBytes >= giga)
        {
            return $"{sizeBytes / giga:0.00} GB";
        }

        if (sizeBytes >= mega)
        {
            return $"{sizeBytes / mega:0.00} MB";
        }

        if (sizeBytes >= kilo)
        {
            return $"{sizeBytes / kilo:0.00} KB";
        }

        return $"{sizeBytes} B";
    }
}
