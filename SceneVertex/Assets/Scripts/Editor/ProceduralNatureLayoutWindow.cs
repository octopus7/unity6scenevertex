using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class ProceduralNatureLayoutWindow : EditorWindow
{
    private enum PlacementPattern
    {
        RandomScatter,
        NaturalClearing,
        GardenLayout
    }

    private readonly struct PlacementRequest
    {
        public PlacementRequest(ProceduralNaturePropCategory kind, int variantIndex, Vector2 position, float yawDegrees, float uniformScale)
        {
            Kind = kind;
            VariantIndex = variantIndex;
            Position = position;
            YawDegrees = yawDegrees;
            UniformScale = uniformScale;
        }

        public ProceduralNaturePropCategory Kind { get; }
        public int VariantIndex { get; }
        public Vector2 Position { get; }
        public float YawDegrees { get; }
        public float UniformScale { get; }
    }

    private readonly struct OccupiedCircle
    {
        public OccupiedCircle(Vector2 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector2 Center { get; }
        public float Radius { get; }
    }

    private readonly struct PlacementAnchor
    {
        public PlacementAnchor(Vector2 position, float yawDegrees)
        {
            Position = position;
            YawDegrees = yawDegrees;
        }

        public Vector2 Position { get; }
        public float YawDegrees { get; }
    }

    private sealed class RequiredAssets
    {
        public Dictionary<ProceduralNaturePropCategory, List<Mesh>> Meshes { get; } = new();
        public Material SharedMaterial { get; set; }
    }

    private const string GroundName = "SceneVertex_Ground100m";
    private const string GroundMeshName = "SceneVertex_Ground100m_Mesh";
    private const string LayoutRootName = "SceneVertex_AutoLayout";
    private const float GroundSize = 100f;
    private const float HalfGroundSize = GroundSize * 0.5f;
    private const StaticEditorFlags BasePlacementStaticFlags =
        StaticEditorFlags.ContributeGI |
        StaticEditorFlags.OccludeeStatic |
        StaticEditorFlags.OccluderStatic |
        StaticEditorFlags.ReflectionProbeStatic;

    [SerializeField] private PlacementPattern pattern = PlacementPattern.NaturalClearing;
    [SerializeField] private int treeCount = 18;
    [SerializeField] private int rockCount = 8;
    [SerializeField] private int fenceCount = 12;
    [SerializeField] private int flowerCount = 28;
    [SerializeField] private int bushCount = 12;
    [SerializeField] private int mushroomCount = 16;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool replacePreviousLayout = true;
    [SerializeField] private bool generateMissingAssets = true;
    [SerializeField] private bool enableBatchingStatic;
    [SerializeField] private bool saveScenesBeforeBake = true;

    private bool lastBakeRunning;
    private long sceneVertexCount;
    private int sceneMeshObjectCount;
    private long layoutVertexCount;
    private int layoutMeshObjectCount;
    private string statsSceneName = string.Empty;

    [MenuItem("Tools/SceneVertex/Layout Window")]
    public static void OpenWindow()
    {
        var window = GetWindow<ProceduralNatureLayoutWindow>("SceneVertex Layout");
        window.minSize = new Vector2(360f, 520f);
    }

    private void OnEnable()
    {
        lastBakeRunning = Lightmapping.isRunning;
        RefreshSceneStats();
    }

    private void Update()
    {
        var isRunning = Lightmapping.isRunning;
        if (isRunning != lastBakeRunning)
        {
            if (!isRunning)
            {
                Debug.Log($"Lighting bake finished for scene '{EditorSceneManager.GetActiveScene().name}'.");
            }

            lastBakeRunning = isRunning;
            Repaint();
        }

        if (isRunning)
        {
            Repaint();
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Placement Tool", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(GetPatternDescription(pattern), MessageType.Info);

        using (new EditorGUILayout.VerticalScope("box"))
        {
            pattern = (PlacementPattern)EditorGUILayout.EnumPopup("Pattern", pattern);
            seed = EditorGUILayout.IntField("Seed", seed);
            replacePreviousLayout = EditorGUILayout.ToggleLeft("Replace previous auto layout", replacePreviousLayout);
            generateMissingAssets = EditorGUILayout.ToggleLeft("Generate missing procedural assets", generateMissingAssets);
            enableBatchingStatic = EditorGUILayout.ToggleLeft("Enable Batching Static", enableBatchingStatic);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Counts", EditorStyles.boldLabel);
            treeCount = Mathf.Max(0, EditorGUILayout.IntField("Trees", treeCount));
            rockCount = Mathf.Max(0, EditorGUILayout.IntField("Rocks", rockCount));
            fenceCount = Mathf.Max(0, EditorGUILayout.IntField("Fences", fenceCount));
            flowerCount = Mathf.Max(0, EditorGUILayout.IntField("Flowers", flowerCount));
            bushCount = Mathf.Max(0, EditorGUILayout.IntField("Bushes", bushCount));
            mushroomCount = Mathf.Max(0, EditorGUILayout.IntField("Mushrooms", mushroomCount));
        }

        GUILayout.Space(6f);
        if (GUILayout.Button("Build Layout In Active Scene", GUILayout.Height(34f)))
        {
            BuildLayoutInActiveScene();
        }

        if (GUILayout.Button("Ensure 100m Ground Plane"))
        {
            EnsureGroundInActiveScene();
        }

        if (GUILayout.Button("Generate Procedural Assets"))
        {
            ProceduralNatureMeshAssetGenerator.GenerateAssets();
        }

        if (GUILayout.Button("Clear Placed Objects"))
        {
            ClearGeneratedObjectsInActiveScene(false);
        }

        if (GUILayout.Button("Clear Placed Objects + Ground"))
        {
            ClearGeneratedObjectsInActiveScene(true);
        }

        GUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Lighting", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Status", Lightmapping.isRunning ? "Baking..." : "Idle");
            saveScenesBeforeBake = EditorGUILayout.ToggleLeft("Save open scenes before bake", saveScenesBeforeBake);

            using (new EditorGUI.DisabledScope(Lightmapping.isRunning))
            {
                if (GUILayout.Button("Bake Scene Lighting"))
                {
                    BakeSceneLighting();
                }
            }

            using (new EditorGUI.DisabledScope(!Lightmapping.isRunning))
            {
                if (GUILayout.Button("Cancel Lighting Bake"))
                {
                    CancelLightingBake();
                }
            }

            if (GUILayout.Button("Clear Baked Lighting"))
            {
                ClearBakedLighting();
            }
        }

        GUILayout.Space(6f);
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Scene Stats", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Scene", string.IsNullOrEmpty(statsSceneName) ? "(none)" : statsSceneName);
            EditorGUILayout.LabelField("Scene Mesh Objects", sceneMeshObjectCount.ToString("N0"));
            EditorGUILayout.LabelField("Scene Vertices", sceneVertexCount.ToString("N0"));
            EditorGUILayout.LabelField("Auto Layout Mesh Objects", layoutMeshObjectCount.ToString("N0"));
            EditorGUILayout.LabelField("Auto Layout Vertices", layoutVertexCount.ToString("N0"));

            if (GUILayout.Button("Refresh Scene Stats"))
            {
                RefreshSceneStats();
            }
        }
    }

    private void EnsureGroundInActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before creating the ground plane.", "OK");
            return;
        }

        EnsureGroundPlane(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        RefreshSceneStats();
    }

    [MenuItem("Tools/SceneVertex/Clear Placed Objects")]
    private static void ClearPlacedObjectsMenu()
    {
        ClearGeneratedObjectsInActiveScene(false);
    }

    [MenuItem("Tools/SceneVertex/Clear Placed Objects And Ground")]
    private static void ClearPlacedObjectsAndGroundMenu()
    {
        ClearGeneratedObjectsInActiveScene(true);
    }

    private static void ClearGeneratedObjectsInActiveScene(bool removeGroundToo)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        var root = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.LayoutRoot) ?? FindNamedObject(scene, LayoutRootName);
        var ground = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.GroundPlane) ?? FindNamedObject(scene, GroundName);
        if (root == null && (!removeGroundToo || ground == null))
        {
            return;
        }

        Undo.SetCurrentGroupName("Clear SceneVertex Auto Layout");
        var undoGroup = Undo.GetCurrentGroup();
        if (root != null)
        {
            ClearChildren(root.transform);
        }

        if (removeGroundToo)
        {
            if (root != null)
            {
                Undo.DestroyObjectImmediate(root);
            }

            if (ground != null)
            {
                Undo.DestroyObjectImmediate(ground);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        RefreshAllOpenWindowsSceneStats();
    }

    private void BuildLayoutInActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before generating the layout.", "OK");
            return;
        }

        var assets = LoadRequiredAssets(generateMissingAssets);
        if (assets == null)
        {
            return;
        }

        Undo.SetCurrentGroupName("Build SceneVertex Layout");
        var undoGroup = Undo.GetCurrentGroup();
        EnsureGroundPlane(scene);
        var layoutRoot = GetOrCreateLayoutRoot(scene);
        if (replacePreviousLayout)
        {
            ClearChildren(layoutRoot.transform);
        }

        var container = new GameObject($"{pattern}_{DateTime.Now:HHmmss}");
        Undo.RegisterCreatedObjectUndo(container, "Create Layout Container");
        container.transform.SetParent(layoutRoot.transform, false);

        var requests = BuildPlacementRequests();
        InstantiateRequests(container.transform, assets, requests);
        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
        RefreshSceneStats();
    }

    private void BakeSceneLighting()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before starting a lighting bake.", "OK");
            return;
        }

        if (Lightmapping.isRunning)
        {
            return;
        }

        if (saveScenesBeforeBake && !EditorSceneManager.SaveOpenScenes())
        {
            return;
        }

        lastBakeRunning = true;
        Lightmapping.BakeAsync();
        Debug.Log($"Started lighting bake for scene '{scene.name}'.");
        Repaint();
    }

    private void CancelLightingBake()
    {
        if (!Lightmapping.isRunning)
        {
            return;
        }

        Lightmapping.Cancel();
        lastBakeRunning = false;
        Debug.Log("Canceled lighting bake.");
        Repaint();
    }

    private void ClearBakedLighting()
    {
        if (Lightmapping.isRunning)
        {
            EditorUtility.DisplayDialog("Lighting Bake Running", "Cancel the current bake before clearing baked lighting.", "OK");
            return;
        }

        Lightmapping.Clear();
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.IsValid())
        {
            EditorSceneManager.MarkSceneDirty(scene);
        }

        DynamicGI.UpdateEnvironment();
        Debug.Log($"Cleared baked lighting for scene '{scene.name}'.");
        Repaint();
    }

    private void RefreshSceneStats()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            statsSceneName = string.Empty;
            sceneMeshObjectCount = 0;
            sceneVertexCount = 0;
            layoutMeshObjectCount = 0;
            layoutVertexCount = 0;
            Repaint();
            return;
        }

        statsSceneName = scene.name;
        CollectMeshStats(scene, null, out sceneMeshObjectCount, out sceneVertexCount);

        var layoutRoot = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.LayoutRoot) ?? FindNamedObject(scene, LayoutRootName);
        if (layoutRoot != null)
        {
            CollectMeshStats(scene, layoutRoot.transform, out layoutMeshObjectCount, out layoutVertexCount);
        }
        else
        {
            layoutMeshObjectCount = 0;
            layoutVertexCount = 0;
        }

        Repaint();
    }

    private static void RefreshAllOpenWindowsSceneStats()
    {
        var windows = Resources.FindObjectsOfTypeAll<ProceduralNatureLayoutWindow>();
        foreach (var window in windows)
        {
            window.RefreshSceneStats();
        }
    }

    private static void CollectMeshStats(Scene scene, Transform root, out int meshObjectCount, out long vertexCount)
    {
        meshObjectCount = 0;
        vertexCount = 0;

        if (root != null)
        {
            AccumulateMeshStatsRecursive(root, ref meshObjectCount, ref vertexCount);
            return;
        }

        foreach (var rootObject in scene.GetRootGameObjects())
        {
            AccumulateMeshStatsRecursive(rootObject.transform, ref meshObjectCount, ref vertexCount);
        }
    }

    private static void AccumulateMeshStatsRecursive(Transform current, ref int meshObjectCount, ref long vertexCount)
    {
        var meshFilter = current.GetComponent<MeshFilter>();
        var meshRenderer = current.GetComponent<MeshRenderer>();
        if (meshFilter != null && meshRenderer != null && meshFilter.sharedMesh != null)
        {
            meshObjectCount++;
            vertexCount += meshFilter.sharedMesh.vertexCount;
        }

        var skinnedMeshRenderer = current.GetComponent<SkinnedMeshRenderer>();
        if (skinnedMeshRenderer != null && skinnedMeshRenderer.sharedMesh != null)
        {
            meshObjectCount++;
            vertexCount += skinnedMeshRenderer.sharedMesh.vertexCount;
        }

        for (var i = 0; i < current.childCount; i++)
        {
            AccumulateMeshStatsRecursive(current.GetChild(i), ref meshObjectCount, ref vertexCount);
        }
    }

    private RequiredAssets LoadRequiredAssets(bool allowGenerate)
    {
        var assets = new RequiredAssets();
        var missingMeshes = new List<string>();
        LoadMeshes(assets.Meshes, missingMeshes);
        assets.SharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProceduralNatureMeshAssetGenerator.SharedMaterialAssetPath);

        if ((missingMeshes.Count > 0 || assets.SharedMaterial == null) && allowGenerate)
        {
            ProceduralNatureMeshAssetGenerator.GenerateAssets();
            assets = new RequiredAssets();
            missingMeshes.Clear();
            LoadMeshes(assets.Meshes, missingMeshes);
            assets.SharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProceduralNatureMeshAssetGenerator.SharedMaterialAssetPath);
        }

        if (missingMeshes.Count > 0 || assets.SharedMaterial == null)
        {
            var message = $"Missing mesh assets: {string.Join(", ", missingMeshes)}\nMissing shared material: {assets.SharedMaterial == null}";
            EditorUtility.DisplayDialog("Missing Procedural Assets", message, "OK");
            return null;
        }

        return assets;
    }

    private static void LoadMeshes(Dictionary<ProceduralNaturePropCategory, List<Mesh>> meshes, List<string> missing)
    {
        foreach (var category in ProceduralNatureAssetCatalog.GetAllCategories())
        {
            var list = new List<Mesh>();
            foreach (var assetName in ProceduralNatureAssetCatalog.GetMeshAssetNames(category))
            {
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{ProceduralNatureMeshAssetGenerator.MeshOutputFolder}/{assetName}.asset");
                if (mesh == null)
                {
                    missing.Add(assetName);
                    continue;
                }

                list.Add(mesh);
            }

            if (list.Count > 0)
            {
                meshes[category] = list;
            }
        }
    }

    private List<PlacementRequest> BuildPlacementRequests()
    {
        var rng = new System.Random(seed);
        return pattern switch
        {
            PlacementPattern.RandomScatter => BuildRandomScatter(rng),
            PlacementPattern.NaturalClearing => BuildNaturalClearing(rng),
            PlacementPattern.GardenLayout => BuildGardenLayout(rng),
            _ => BuildRandomScatter(rng)
        };
    }

    private List<PlacementRequest> BuildRandomScatter(System.Random rng)
    {
        var requests = new List<PlacementRequest>();
        var occupied = new List<OccupiedCircle>();
        AddRandomFenceRuns(requests, occupied, rng, fenceCount);
        AddScatter(requests, occupied, rng, ProceduralNaturePropCategory.Tree, treeCount, 4f, candidate => candidate.magnitude > 10f);
        AddScatter(requests, occupied, rng, ProceduralNaturePropCategory.Rock, rockCount, 3f, candidate => candidate.magnitude > 6f);
        AddScatter(requests, occupied, rng, ProceduralNaturePropCategory.Bush, bushCount, 2.5f, candidate => candidate.magnitude > 6f && candidate.magnitude < 38f);
        AddClusteredProps(requests, occupied, rng, ProceduralNaturePropCategory.Flower, flowerCount, new[] { new Vector2(-10f, 8f), new Vector2(8f, -5f), new Vector2(3f, 12f) }, 5.2f);
        AddClusteredProps(requests, occupied, rng, ProceduralNaturePropCategory.Mushroom, mushroomCount, new[] { new Vector2(-18f, -10f), new Vector2(16f, 8f), new Vector2(0f, -16f) }, 3.6f);
        return requests;
    }

    private List<PlacementRequest> BuildNaturalClearing(System.Random rng)
    {
        var requests = new List<PlacementRequest>();
        AddRing(requests, rng, ProceduralNaturePropCategory.Tree, treeCount, 33f, 40f, -10f, 8f, 10f);
        AddArc(requests, rng, ProceduralNaturePropCategory.Rock, rockCount, 208f, 320f, 22f, 30f, 18f);
        AddRing(requests, rng, ProceduralNaturePropCategory.Bush, bushCount, 16f, 24f, 22f, 4f, 16f);
        AddFenceEdge(requests, rng, fenceCount, 38f);
        AddClusteredPattern(requests, rng, ProceduralNaturePropCategory.Flower, flowerCount, new[] { new Vector2(0f, 1f), new Vector2(-6f, -4f), new Vector2(7f, 5f) }, 4.8f);
        AddClusteredPattern(requests, rng, ProceduralNaturePropCategory.Mushroom, mushroomCount, new[] { new Vector2(-13f, 14f), new Vector2(12f, 12f), new Vector2(-16f, -6f), new Vector2(15f, -9f) }, 3.2f);
        return requests;
    }

    private List<PlacementRequest> BuildGardenLayout(System.Random rng)
    {
        var requests = new List<PlacementRequest>();
        AddGardenFenceLoop(requests, rng, fenceCount);
        AddFlowerGrid(requests, rng, flowerCount);
        AddAnchoredProps(requests, rng, ProceduralNaturePropCategory.Tree, treeCount, new[] { new Vector2(-22f, -14f), new Vector2(22f, -14f), new Vector2(-22f, 14f), new Vector2(22f, 14f), new Vector2(0f, 24f), new Vector2(-28f, 0f), new Vector2(28f, 0f) }, 1.6f);
        AddAnchoredProps(requests, rng, ProceduralNaturePropCategory.Rock, rockCount, new[] { new Vector2(-6f, -16f), new Vector2(6f, -16f), new Vector2(-18f, -2f), new Vector2(18f, -2f), new Vector2(-14f, 13f), new Vector2(14f, 13f) }, 1.2f);
        AddAnchoredProps(requests, rng, ProceduralNaturePropCategory.Bush, bushCount, new[] { new Vector2(-12f, -9f), new Vector2(12f, -9f), new Vector2(-15f, 8f), new Vector2(15f, 8f), new Vector2(0f, 10f), new Vector2(-7f, 0f), new Vector2(7f, 0f) }, 1.1f);
        AddClusteredPattern(requests, rng, ProceduralNaturePropCategory.Mushroom, mushroomCount, new[] { new Vector2(-18f, 16f), new Vector2(18f, 16f), new Vector2(-20f, -12f), new Vector2(20f, -12f) }, 2.4f);
        return requests;
    }

    private void InstantiateRequests(Transform parent, RequiredAssets assets, List<PlacementRequest> requests)
    {
        foreach (var request in requests)
        {
            var variants = assets.Meshes[request.Kind];
            var sourceMesh = variants[Mathf.Clamp(request.VariantIndex, 0, variants.Count - 1)];
            var gameObject = new GameObject($"{request.Kind}_{parent.childCount:000}");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Procedural Placement");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.rotation = Quaternion.Euler(0f, request.YawDegrees, 0f);
            gameObject.transform.localScale = Vector3.one * request.UniformScale;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshFilter.sharedMesh = sourceMesh;
            meshRenderer.sharedMaterial = assets.SharedMaterial;
            meshRenderer.receiveGI = ReceiveGI.Lightmaps;
            GameObjectUtility.SetStaticEditorFlags(gameObject, GetPlacementStaticFlags());

            var verticalOffset = -sourceMesh.bounds.min.y * request.UniformScale;
            gameObject.transform.position = new Vector3(request.Position.x, verticalOffset, request.Position.y);
        }
    }

    private GameObject EnsureGroundPlane(Scene scene)
    {
        var plane = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.GroundPlane) ?? FindNamedObject(scene, GroundName);
        if (plane == null)
        {
            plane = new GameObject(GroundName);
            Undo.RegisterCreatedObjectUndo(plane, "Create 100m Ground Plane");
        }

        plane.name = GroundName;
        plane.transform.position = Vector3.zero;
        plane.transform.rotation = Quaternion.identity;
        plane.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);
        GameObjectUtility.SetStaticEditorFlags(plane, GetPlacementStaticFlags());

        var meshFilter = plane.GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = Undo.AddComponent<MeshFilter>(plane);
        }

        var meshRenderer = plane.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = Undo.AddComponent<MeshRenderer>(plane);
        }
        meshRenderer.receiveGI = ReceiveGI.Lightmaps;

        var embeddedMesh = plane.GetComponent<SceneVertexEmbeddedMeshData>();
        if (embeddedMesh == null)
        {
            embeddedMesh = Undo.AddComponent<SceneVertexEmbeddedMeshData>(plane);
        }

        if (plane.GetComponent<MeshCollider>() == null)
        {
            Undo.AddComponent<MeshCollider>(plane);
        }

        Undo.RecordObject(embeddedMesh, "Update 100m Ground Plane");
        CreateGroundPlaneData(out var vertices, out var triangles, out var normals, out var uv, out var colors);
        embeddedMesh.SetMeshData(GroundMeshName, vertices, triangles, normals, uv, colors);

        if (meshRenderer.sharedMaterial == null)
        {
            var groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProceduralNatureMeshAssetGenerator.SharedMaterialAssetPath);
            if (groundMaterial == null)
            {
                ProceduralNatureMeshAssetGenerator.GenerateAssets();
                groundMaterial = AssetDatabase.LoadAssetAtPath<Material>(ProceduralNatureMeshAssetGenerator.SharedMaterialAssetPath);
            }

            if (groundMaterial != null)
            {
                meshRenderer.sharedMaterial = groundMaterial;
            }
        }

        var marker = plane.GetComponent<SceneVertexGeneratedObjectMarker>();
        if (marker == null)
        {
            marker = Undo.AddComponent<SceneVertexGeneratedObjectMarker>(plane);
        }

        marker.kind = SceneVertexGeneratedObjectKind.GroundPlane;
        return plane;
    }

    private static void CreateGroundPlaneData(
        out Vector3[] vertices,
        out int[] triangles,
        out Vector3[] normals,
        out Vector2[] uv,
        out Color[] colors)
    {
        const int segments = 10;
        const float basePlaneSize = 10f;
        var vertexCountPerAxis = segments + 1;
        vertices = new Vector3[vertexCountPerAxis * vertexCountPerAxis];
        normals = new Vector3[vertices.Length];
        uv = new Vector2[vertices.Length];
        colors = new Color[vertices.Length];
        triangles = new int[segments * segments * 6];

        var vertexIndex = 0;
        for (var z = 0; z <= segments; z++)
        {
            for (var x = 0; x <= segments; x++)
            {
                var xPos = ((float)x / segments - 0.5f) * basePlaneSize;
                var zPos = ((float)z / segments - 0.5f) * basePlaneSize;
                vertices[vertexIndex] = new Vector3(xPos, 0f, zPos);
                normals[vertexIndex] = Vector3.up;
                uv[vertexIndex] = new Vector2((float)x / segments, (float)z / segments);
                var shade = Mathf.Lerp(0.72f, 0.9f, (float)z / segments);
                colors[vertexIndex] = new Color(0.18f * shade, 0.48f * shade, 0.2f * shade, 1f);
                vertexIndex++;
            }
        }

        var triangleIndex = 0;
        for (var z = 0; z < segments; z++)
        {
            for (var x = 0; x < segments; x++)
            {
                var bottomLeft = z * vertexCountPerAxis + x;
                var bottomRight = bottomLeft + 1;
                var topLeft = bottomLeft + vertexCountPerAxis;
                var topRight = topLeft + 1;

                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomLeft;
                triangles[triangleIndex++] = topRight;
                triangles[triangleIndex++] = bottomRight;
            }
        }
    }

    private StaticEditorFlags GetPlacementStaticFlags()
    {
        return enableBatchingStatic
            ? BasePlacementStaticFlags | StaticEditorFlags.BatchingStatic
            : BasePlacementStaticFlags;
    }

    private static GameObject GetOrCreateLayoutRoot(Scene scene)
    {
        var root = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.LayoutRoot) ?? FindNamedObject(scene, LayoutRootName);
        if (root == null)
        {
            root = new GameObject(LayoutRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Layout Root");
        }

        root.name = LayoutRootName;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        var marker = root.GetComponent<SceneVertexGeneratedObjectMarker>();
        if (marker == null)
        {
            marker = Undo.AddComponent<SceneVertexGeneratedObjectMarker>(root);
        }

        marker.kind = SceneVertexGeneratedObjectKind.LayoutRoot;
        return root;
    }

    private static void ClearChildren(Transform root)
    {
        for (var i = root.childCount - 1; i >= 0; i--)
        {
            Undo.DestroyObjectImmediate(root.GetChild(i).gameObject);
        }
    }

    private static GameObject FindMarkedObject(Scene scene, SceneVertexGeneratedObjectKind kind)
    {
        var markers = UnityEngine.Object.FindObjectsByType<SceneVertexGeneratedObjectMarker>(FindObjectsSortMode.None);
        foreach (var marker in markers)
        {
            if (marker != null && marker.gameObject.scene == scene && marker.kind == kind)
            {
                return marker.gameObject;
            }
        }

        return null;
    }

    private static GameObject FindNamedObject(Scene scene, string name)
    {
        foreach (var rootObject in scene.GetRootGameObjects())
        {
            if (rootObject.name == name)
            {
                return rootObject;
            }
        }

        return null;
    }

    private void AddScatter(List<PlacementRequest> requests, List<OccupiedCircle> occupied, System.Random rng, ProceduralNaturePropCategory kind, int count, float margin, Predicate<Vector2> predicate)
    {
        for (var i = 0; i < count; i++)
        {
            var scale = NextScale(rng, kind);
            var radius = GetFootprintRadius(kind, scale);
            if (!TryFindPoint(rng, occupied, radius, margin, predicate, out var point))
            {
                continue;
            }

            occupied.Add(new OccupiedCircle(point, radius));
            requests.Add(CreateRequest(rng, kind, point, NextYaw(rng, kind), scale));
        }
    }

    private void AddClusteredProps(List<PlacementRequest> requests, List<OccupiedCircle> occupied, System.Random rng, ProceduralNaturePropCategory kind, int count, IReadOnlyList<Vector2> centers, float clusterRadius)
    {
        for (var i = 0; i < count; i++)
        {
            var center = centers[i % centers.Count];
            var scale = NextScale(rng, kind);
            var radius = GetFootprintRadius(kind, scale);
            var placed = false;

            for (var attempt = 0; attempt < 50; attempt++)
            {
                var candidate = center + RandomInsideCircle(rng, clusterRadius);
                if (!IsInsideBounds(candidate, 3f) || IntersectsOccupied(candidate, radius, occupied))
                {
                    continue;
                }

                occupied.Add(new OccupiedCircle(candidate, radius));
                requests.Add(CreateRequest(rng, kind, candidate, NextYaw(rng, kind), scale));
                placed = true;
                break;
            }

            if (!placed)
            {
                AddScatter(requests, occupied, rng, kind, 1, 2f, _ => true);
            }
        }
    }

    private void AddClusteredPattern(List<PlacementRequest> requests, System.Random rng, ProceduralNaturePropCategory kind, int count, IReadOnlyList<Vector2> centers, float clusterRadius)
    {
        for (var i = 0; i < count; i++)
        {
            var position = centers[i % centers.Count] + RandomInsideCircle(rng, clusterRadius);
            requests.Add(CreateRequest(rng, kind, position, NextYaw(rng, kind), NextScale(rng, kind)));
        }
    }

    private void AddRandomFenceRuns(List<PlacementRequest> requests, List<OccupiedCircle> occupied, System.Random rng, int count)
    {
        var remaining = count;
        while (remaining > 0)
        {
            var runLength = Mathf.Min(remaining, rng.Next(2, 5));
            if (!TryPlaceFenceRun(requests, occupied, rng, runLength))
            {
                break;
            }

            remaining -= runLength;
        }
    }

    private bool TryPlaceFenceRun(List<PlacementRequest> requests, List<OccupiedCircle> occupied, System.Random rng, int runLength)
    {
        const float spacing = 3.15f;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var yaw = 90f * rng.Next(0, 4);
            var direction = DirectionFromYaw(yaw);
            var span = spacing * (runLength - 1);
            var edgeMargin = span * 0.5f + 6f;
            if (!TryFindPoint(rng, occupied, span * 0.5f + 1.5f, edgeMargin, _ => true, out var center))
            {
                continue;
            }

            var first = center - direction * (span * 0.5f);
            var placements = new List<PlacementRequest>();
            var circles = new List<OccupiedCircle>();
            var valid = true;

            for (var index = 0; index < runLength; index++)
            {
                var scale = NextScale(rng, ProceduralNaturePropCategory.Fence);
                var position = first + direction * (spacing * index);
                var radius = GetFootprintRadius(ProceduralNaturePropCategory.Fence, scale);
                if (!IsInsideBounds(position, 4f) || IntersectsOccupied(position, radius, occupied))
                {
                    valid = false;
                    break;
                }

                circles.Add(new OccupiedCircle(position, radius));
                placements.Add(CreateRequest(rng, ProceduralNaturePropCategory.Fence, position, yaw, scale));
            }

            if (!valid)
            {
                continue;
            }

            occupied.AddRange(circles);
            requests.AddRange(placements);
            return true;
        }

        return false;
    }

    private void AddRing(List<PlacementRequest> requests, System.Random rng, ProceduralNaturePropCategory kind, int count, float minRadius, float maxRadius, float angleOffset, float radiusJitter, float angleJitter)
    {
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0.5f : (float)i / count;
            var angle = angleOffset + t * 360f + NextRange(rng, -angleJitter, angleJitter);
            var radius = Mathf.Clamp(NextRange(rng, minRadius, maxRadius) + NextRange(rng, -radiusJitter, radiusJitter), minRadius, maxRadius);
            var position = DirectionFromYaw(angle) * radius;
            requests.Add(CreateRequest(rng, kind, position, angle + 180f + NextRange(rng, -20f, 20f), NextScale(rng, kind)));
        }
    }

    private void AddArc(List<PlacementRequest> requests, System.Random rng, ProceduralNaturePropCategory kind, int count, float startAngle, float endAngle, float minRadius, float maxRadius, float yawBias)
    {
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0.5f : (float)i / (count - 1);
            var angle = Mathf.Lerp(startAngle, endAngle, t) + NextRange(rng, -8f, 8f);
            var radius = NextRange(rng, minRadius, maxRadius);
            var position = DirectionFromYaw(angle) * radius;
            requests.Add(CreateRequest(rng, kind, position, angle + yawBias + NextRange(rng, -25f, 25f), NextScale(rng, kind)));
        }
    }

    private void AddFenceEdge(List<PlacementRequest> requests, System.Random rng, int count, float zPosition)
    {
        const float spacing = 3.1f;
        var sideCount = Mathf.Min(count, 22);
        var startX = -((sideCount - 1) * spacing) * 0.5f;
        for (var i = 0; i < sideCount; i++)
        {
            var position = new Vector2(startX + i * spacing, zPosition + NextRange(rng, -0.4f, 0.4f));
            requests.Add(CreateRequest(rng, ProceduralNaturePropCategory.Fence, position, NextRange(rng, -4f, 4f), NextScale(rng, ProceduralNaturePropCategory.Fence)));
        }

        for (var i = sideCount; i < count; i++)
        {
            var z = zPosition - 3.1f * (i - sideCount + 1);
            var position = new Vector2(-35f + NextRange(rng, -0.4f, 0.4f), z);
            requests.Add(CreateRequest(rng, ProceduralNaturePropCategory.Fence, position, 90f + NextRange(rng, -4f, 4f), NextScale(rng, ProceduralNaturePropCategory.Fence)));
        }
    }

    private void AddGardenFenceLoop(List<PlacementRequest> requests, System.Random rng, int count)
    {
        var candidates = BuildGardenFenceCandidates();
        var selected = SampleEvenly(candidates, count);
        foreach (var candidate in selected)
        {
            requests.Add(CreateRequest(rng, ProceduralNaturePropCategory.Fence, candidate.Position, candidate.YawDegrees, NextScale(rng, ProceduralNaturePropCategory.Fence)));
        }
    }

    private void AddFlowerGrid(List<PlacementRequest> requests, System.Random rng, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * 1.5f)));
        var rows = Mathf.CeilToInt((float)count / columns);
        for (var i = 0; i < count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = columns == 1 ? 0f : Mathf.Lerp(-10f, 10f, (float)column / (columns - 1));
            var z = rows == 1 ? 0f : Mathf.Lerp(-6f, 6f, (float)row / (rows - 1));
            var position = new Vector2(x, z) + new Vector2(NextRange(rng, -0.4f, 0.4f), NextRange(rng, -0.4f, 0.4f));
            requests.Add(CreateRequest(rng, ProceduralNaturePropCategory.Flower, position, NextYaw(rng, ProceduralNaturePropCategory.Flower), NextScale(rng, ProceduralNaturePropCategory.Flower)));
        }
    }

    private void AddAnchoredProps(List<PlacementRequest> requests, System.Random rng, ProceduralNaturePropCategory kind, int count, IReadOnlyList<Vector2> anchors, float jitter)
    {
        for (var i = 0; i < count; i++)
        {
            var anchor = anchors[i % anchors.Count];
            var position = anchor + new Vector2(NextRange(rng, -jitter, jitter), NextRange(rng, -jitter, jitter));
            requests.Add(CreateRequest(rng, kind, position, NextYaw(rng, kind), NextScale(rng, kind)));
        }
    }

    private static List<PlacementAnchor> BuildGardenFenceCandidates()
    {
        const float halfWidth = 17f;
        const float halfHeight = 11f;
        const float spacing = 3.1f;
        const float gateHalfWidth = 4.5f;
        var result = new List<PlacementAnchor>();

        for (var x = -halfWidth; x <= halfWidth + 0.01f; x += spacing)
        {
            if (Mathf.Abs(x) > gateHalfWidth)
            {
                result.Add(new PlacementAnchor(new Vector2(x, -halfHeight), 0f));
            }

            result.Add(new PlacementAnchor(new Vector2(x, halfHeight), 0f));
        }

        for (var z = -halfHeight + spacing; z <= halfHeight - spacing + 0.01f; z += spacing)
        {
            result.Add(new PlacementAnchor(new Vector2(-halfWidth, z), 90f));
            result.Add(new PlacementAnchor(new Vector2(halfWidth, z), 90f));
        }

        return result;
    }

    private static List<PlacementAnchor> SampleEvenly(List<PlacementAnchor> source, int count)
    {
        var result = new List<PlacementAnchor>();
        if (count <= 0 || source.Count == 0)
        {
            return result;
        }

        if (count >= source.Count)
        {
            result.AddRange(source);
            return result;
        }

        var step = (source.Count - 1f) / Mathf.Max(1, count - 1);
        for (var i = 0; i < count; i++)
        {
            result.Add(source[Mathf.RoundToInt(step * i)]);
        }

        return result;
    }

    private static bool TryFindPoint(System.Random rng, List<OccupiedCircle> occupied, float radius, float edgeMargin, Predicate<Vector2> predicate, out Vector2 point)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var candidate = new Vector2(NextRange(rng, -HalfGroundSize + edgeMargin, HalfGroundSize - edgeMargin), NextRange(rng, -HalfGroundSize + edgeMargin, HalfGroundSize - edgeMargin));
            if (predicate != null && !predicate(candidate))
            {
                continue;
            }

            if (IntersectsOccupied(candidate, radius, occupied))
            {
                continue;
            }

            point = candidate;
            return true;
        }

        point = default;
        return false;
    }

    private static bool IntersectsOccupied(Vector2 candidate, float radius, List<OccupiedCircle> occupied)
    {
        foreach (var item in occupied)
        {
            var minDistance = radius + item.Radius;
            if ((candidate - item.Center).sqrMagnitude < minDistance * minDistance)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideBounds(Vector2 point, float margin)
    {
        return point.x >= -HalfGroundSize + margin && point.x <= HalfGroundSize - margin && point.y >= -HalfGroundSize + margin && point.y <= HalfGroundSize - margin;
    }

    private static Vector2 RandomInsideCircle(System.Random rng, float radius)
    {
        var angle = NextRange(rng, 0f, Mathf.PI * 2f);
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * Mathf.Sqrt((float)rng.NextDouble()));
    }

    private static Vector2 DirectionFromYaw(float yawDegrees)
    {
        var radians = yawDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static PlacementRequest CreateRequest(System.Random rng, ProceduralNaturePropCategory kind, Vector2 position, float yawDegrees, float uniformScale)
    {
        return new PlacementRequest(kind, rng.Next(ProceduralNatureAssetCatalog.GetVariantCount(kind)), position, yawDegrees, uniformScale);
    }

    private static float NextScale(System.Random rng, ProceduralNaturePropCategory kind)
    {
        return kind switch
        {
            ProceduralNaturePropCategory.Tree => NextRange(rng, 0.90f, 1.25f),
            ProceduralNaturePropCategory.Rock => NextRange(rng, 0.80f, 1.35f),
            ProceduralNaturePropCategory.Fence => NextRange(rng, 0.96f, 1.05f),
            ProceduralNaturePropCategory.Flower => NextRange(rng, 0.85f, 1.18f),
            ProceduralNaturePropCategory.Bush => NextRange(rng, 0.90f, 1.20f),
            ProceduralNaturePropCategory.Mushroom => NextRange(rng, 0.80f, 1.15f),
            _ => 1f
        };
    }

    private static float NextYaw(System.Random rng, ProceduralNaturePropCategory kind)
    {
        return kind == ProceduralNaturePropCategory.Fence ? 90f * rng.Next(0, 4) : NextRange(rng, 0f, 360f);
    }

    private static float GetFootprintRadius(ProceduralNaturePropCategory kind, float scale)
    {
        var baseRadius = kind switch
        {
            ProceduralNaturePropCategory.Tree => 2.5f,
            ProceduralNaturePropCategory.Rock => 1.9f,
            ProceduralNaturePropCategory.Fence => 1.7f,
            ProceduralNaturePropCategory.Flower => 0.55f,
            ProceduralNaturePropCategory.Bush => 1.9f,
            ProceduralNaturePropCategory.Mushroom => 0.75f,
            _ => 1f
        };

        return baseRadius * scale;
    }

    private static float NextRange(System.Random rng, float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private static string GetPatternDescription(PlacementPattern value)
    {
        return value switch
        {
            PlacementPattern.RandomScatter => "Scatter everything across the plane, but keep flowers and mushrooms in clusters and fences in short runs.",
            PlacementPattern.NaturalClearing => "Trees form an outer ring, rocks sit in an arc, bushes fill the mid band, and small plants cluster in the clearing.",
            PlacementPattern.GardenLayout => "A fenced garden with flower beds, corner trees, trimmed bushes, and accent props around the edges.",
            _ => string.Empty
        };
    }
}
