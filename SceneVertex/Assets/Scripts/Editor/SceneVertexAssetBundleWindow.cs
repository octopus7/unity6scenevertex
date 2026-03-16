using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class SceneVertexAssetBundleWindow : EditorWindow
{
    [Serializable]
    private sealed class BundleBuildReportEntry
    {
        public string bundleName;
        public long sizeBytes;
        public int explicitAssetCount;
        public string[] directDependencies;
        public string[] explicitAssets;
    }

    private const string GeneratedAssetsBundleName = "scenevertex-generated-assets";
    private const string SampleSceneBundleName = "scenevertex-sample-scene";
    private const string SampleSceneTexturesBundleName = "scenevertex-sample-scene-textures";
    private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
    private const string SharedShaderPath = "Assets/Shaders/SceneVertexVertexColor.shader";
    private const string OutputRootFolderName = "AssetBundleBuilds";

    [SerializeField] private Vector2 reportScrollPosition;
    [SerializeField] private string lastBuildOutputPath = string.Empty;
    [SerializeField] private long totalBundleSizeBytes;
    [SerializeField] private List<BundleBuildReportEntry> reportEntries = new();

    [MenuItem("Tools/SceneVertex/AssetBundle Window")]
    public static void OpenWindow()
    {
        var window = GetWindow<SceneVertexAssetBundleWindow>("SceneVertex Bundles");
        window.minSize = new Vector2(520f, 560f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Bundle Settings", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Use the buttons below to assign generated assets, the sample scene, and the sample scene textures to separate AssetBundles. Then build the bundles and inspect the output sizes.",
            MessageType.Info);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Target Scene", SampleScenePath);
            EditorGUILayout.LabelField("Build Target", EditorUserBuildSettings.activeBuildTarget.ToString());
            EditorGUILayout.LabelField("Output Folder", GetOutputFolderPath());
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Assignments", EditorStyles.boldLabel);

            if (GUILayout.Button("Set Generated Meshes + Material Bundle"))
            {
                ConfigureGeneratedAssetsBundle();
            }

            if (GUILayout.Button("Set SampleScene Bundle"))
            {
                ConfigureSampleSceneBundle();
            }

            if (GUILayout.Button("Set SampleScene Textures Bundle"))
            {
                ConfigureSampleSceneTexturesBundle();
            }

            if (GUILayout.Button("Set All Recommended Bundle Assignments"))
            {
                ConfigureGeneratedAssetsBundle();
                ConfigureSampleSceneBundle();
                ConfigureSampleSceneTexturesBundle();
                AssetDatabase.SaveAssets();
                AssetDatabase.RemoveUnusedAssetBundleNames();
                AssetDatabase.Refresh();
                Debug.Log("Applied all recommended SceneVertex AssetBundle assignments.");
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Build", EditorStyles.boldLabel);

            if (GUILayout.Button("Build Configured AssetBundles", GUILayout.Height(32f)))
            {
                BuildConfiguredBundles();
            }

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastBuildOutputPath) || !Directory.Exists(lastBuildOutputPath)))
            {
                if (GUILayout.Button("Open Last Build Folder"))
                {
                    EditorUtility.RevealInFinder(lastBuildOutputPath);
                }
            }
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Build Report", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Last Output", string.IsNullOrEmpty(lastBuildOutputPath) ? "(not built yet)" : lastBuildOutputPath);
            EditorGUILayout.LabelField("Bundle Count", reportEntries.Count.ToString("N0"));
            EditorGUILayout.LabelField("Total Size", FormatSize(totalBundleSizeBytes));

            using (var scrollView = new EditorGUILayout.ScrollViewScope(reportScrollPosition, GUILayout.MinHeight(260f)))
            {
                reportScrollPosition = scrollView.scrollPosition;

                if (reportEntries.Count == 0)
                {
                    EditorGUILayout.HelpBox("No build report yet. Build the configured bundles to see size details.", MessageType.None);
                }
                else
                {
                    foreach (var entry in reportEntries)
                    {
                        using (new EditorGUILayout.VerticalScope("box"))
                        {
                            EditorGUILayout.LabelField(entry.bundleName, EditorStyles.boldLabel);
                            EditorGUILayout.LabelField("Size", FormatSize(entry.sizeBytes));
                            EditorGUILayout.LabelField("Explicit Assets", entry.explicitAssetCount.ToString("N0"));
                            EditorGUILayout.LabelField(
                                "Direct Dependencies",
                                entry.directDependencies != null && entry.directDependencies.Length > 0
                                    ? string.Join(", ", entry.directDependencies)
                                    : "(none)");

                            if (entry.explicitAssets != null && entry.explicitAssets.Length > 0)
                            {
                                EditorGUILayout.LabelField("Assets");
                                foreach (var assetPath in entry.explicitAssets)
                                {
                                    EditorGUILayout.SelectableLabel(assetPath, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    private static void ConfigureGeneratedAssetsBundle()
    {
        var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddAllAssetsUnderFolder(assetPaths, ProceduralNatureMeshAssetGenerator.MeshOutputFolder);
        AddAllAssetsUnderFolder(assetPaths, ProceduralNatureMeshAssetGenerator.MaterialOutputFolder);
        AddIfAssetExists(assetPaths, SharedShaderPath);
        ApplyBundleNameToAssets(assetPaths, GeneratedAssetsBundleName);
        Debug.Log($"Assigned {assetPaths.Count} generated assets to bundle '{GeneratedAssetsBundleName}'.");
    }

    private static void ConfigureSampleSceneBundle()
    {
        if (!AssetExists(SampleScenePath))
        {
            Debug.LogError($"Could not find scene asset at {SampleScenePath}.");
            return;
        }

        var assetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            SampleScenePath
        };

        ApplyBundleNameToAssets(assetPaths, SampleSceneBundleName);
        Debug.Log($"Assigned sample scene to bundle '{SampleSceneBundleName}'.");
    }

    private static void ConfigureSampleSceneTexturesBundle()
    {
        if (!AssetExists(SampleScenePath))
        {
            Debug.LogError($"Could not find scene asset at {SampleScenePath}.");
            return;
        }

        var texturePaths = new HashSet<string>(GetSceneTextureDependencyPaths(SampleScenePath), StringComparer.OrdinalIgnoreCase);
        ApplyBundleNameToAssets(texturePaths, SampleSceneTexturesBundleName);
        Debug.Log($"Assigned {texturePaths.Count} scene texture assets to bundle '{SampleSceneTexturesBundleName}'.");
    }

    private void BuildConfiguredBundles()
    {
        var outputFolder = GetOutputFolderPath();
        Directory.CreateDirectory(outputFolder);

        var manifest = BuildPipeline.BuildAssetBundles(
            outputFolder,
            BuildAssetBundleOptions.ChunkBasedCompression,
            EditorUserBuildSettings.activeBuildTarget);

        if (manifest == null)
        {
            Debug.LogError("AssetBundle build failed.");
            return;
        }

        lastBuildOutputPath = outputFolder;
        RebuildReport(manifest, outputFolder);
        Debug.Log($"Built {reportEntries.Count} AssetBundles to {outputFolder}.");
    }

    private void RebuildReport(AssetBundleManifest manifest, string outputFolder)
    {
        reportEntries.Clear();
        totalBundleSizeBytes = 0;

        foreach (var bundleName in manifest.GetAllAssetBundles().OrderByDescending(GetBundleSizeBytes))
        {
            var entry = new BundleBuildReportEntry
            {
                bundleName = bundleName,
                sizeBytes = GetBundleSizeBytes(bundleName),
                explicitAssets = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName).OrderBy(path => path).ToArray(),
                directDependencies = manifest.GetDirectDependencies(bundleName)
            };

            entry.explicitAssetCount = entry.explicitAssets.Length;
            totalBundleSizeBytes += entry.sizeBytes;
            reportEntries.Add(entry);
        }

        long GetBundleSizeBytes(string bundleName)
        {
            var bundlePath = Path.Combine(outputFolder, bundleName);
            return File.Exists(bundlePath) ? new FileInfo(bundlePath).Length : 0L;
        }
    }

    private static void ApplyBundleNameToAssets(HashSet<string> desiredAssetPaths, string bundleName)
    {
        var currentPaths = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        foreach (var assetPath in currentPaths)
        {
            if (!desiredAssetPaths.Contains(assetPath))
            {
                SetBundleName(assetPath, string.Empty);
            }
        }

        foreach (var assetPath in desiredAssetPaths)
        {
            SetBundleName(assetPath, bundleName);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.RemoveUnusedAssetBundleNames();
        AssetDatabase.Refresh();
    }

    private static void SetBundleName(string assetPath, string bundleName)
    {
        var importer = AssetImporter.GetAtPath(assetPath);
        if (importer == null)
        {
            return;
        }

        importer.assetBundleName = bundleName;
        importer.SaveAndReimport();
    }

    private static IEnumerable<string> GetSceneTextureDependencyPaths(string scenePath)
    {
        foreach (var dependencyPath in AssetDatabase.GetDependencies(scenePath, true))
        {
            if (!dependencyPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (string.Equals(dependencyPath, scenePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsTextureAssetPath(dependencyPath))
            {
                yield return dependencyPath;
            }
        }
    }

    private static bool IsTextureAssetPath(string assetPath)
    {
        var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
        return assetType != null && typeof(Texture).IsAssignableFrom(assetType);
    }

    private static void AddAllAssetsUnderFolder(HashSet<string> assetPaths, string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { folderPath }))
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetExists(assetPath))
            {
                continue;
            }

            assetPaths.Add(assetPath);
        }
    }

    private static void AddIfAssetExists(HashSet<string> assetPaths, string assetPath)
    {
        if (AssetExists(assetPath))
        {
            assetPaths.Add(assetPath);
        }
    }

    private static bool AssetExists(string assetPath)
    {
        return !string.IsNullOrEmpty(assetPath) && !AssetDatabase.IsValidFolder(assetPath) && AssetDatabase.GetMainAssetTypeAtPath(assetPath) != null;
    }

    private static string GetOutputFolderPath()
    {
        return Path.Combine(Directory.GetCurrentDirectory(), OutputRootFolderName, EditorUserBuildSettings.activeBuildTarget.ToString());
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
