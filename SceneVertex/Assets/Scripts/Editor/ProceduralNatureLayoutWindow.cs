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

    private enum PropKind
    {
        Tree,
        Rock,
        Fence,
        Flower
    }

    private readonly struct PlacementRequest
    {
        public PlacementRequest(PropKind kind, Vector2 position, float yawDegrees, float uniformScale)
        {
            Kind = kind;
            Position = position;
            YawDegrees = yawDegrees;
            UniformScale = uniformScale;
        }

        public PropKind Kind { get; }
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

    private const string GroundName = "SceneVertex_Ground100m";
    private const string LayoutRootName = "SceneVertex_AutoLayout";
    private const string MeshFolder = "Assets/GeneratedMeshes";
    private const float GroundSize = 100f;
    private const float HalfGroundSize = GroundSize * 0.5f;

    [SerializeField] private PlacementPattern pattern = PlacementPattern.NaturalClearing;
    [SerializeField] private int treeCount = 18;
    [SerializeField] private int rockCount = 8;
    [SerializeField] private int fenceCount = 12;
    [SerializeField] private int flowerCount = 28;
    [SerializeField] private int seed = 12345;
    [SerializeField] private bool replacePreviousLayout = true;
    [SerializeField] private bool generateMissingAssets = true;

    private static Material cachedDefaultMaterial;

    [MenuItem("Tools/SceneVertex/Layout Window")]
    public static void OpenWindow()
    {
        var window = GetWindow<ProceduralNatureLayoutWindow>("SceneVertex Layout");
        window.minSize = new Vector2(340f, 360f);
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
            generateMissingAssets = EditorGUILayout.ToggleLeft("Generate missing mesh assets", generateMissingAssets);
        }

        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("Counts", EditorStyles.boldLabel);
            treeCount = Mathf.Max(0, EditorGUILayout.IntField("Trees", treeCount));
            rockCount = Mathf.Max(0, EditorGUILayout.IntField("Rocks", rockCount));
            fenceCount = Mathf.Max(0, EditorGUILayout.IntField("Fences", fenceCount));
            flowerCount = Mathf.Max(0, EditorGUILayout.IntField("Flowers", flowerCount));
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

        if (GUILayout.Button("Generate Mesh Assets"))
        {
            ProceduralNatureMeshAssetGenerator.GenerateAssets();
        }

        if (GUILayout.Button("Clear Placed Objects"))
        {
            ClearAutoLayoutInActiveScene(false);
        }

        if (GUILayout.Button("Clear Placed Objects + Ground"))
        {
            ClearAutoLayoutInActiveScene(true);
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

    private void ClearAutoLayoutInActiveScene(bool removeGroundToo)
    {
        ClearGeneratedObjectsInActiveScene(removeGroundToo);
    }

    private static void ClearGeneratedObjectsInActiveScene(bool removeGroundToo)
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            return;
        }

        var root = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.LayoutRoot);
        if (root == null)
        {
            root = FindNamedObject(scene, LayoutRootName);
        }

        var ground = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.GroundPlane);
        if (ground == null)
        {
            ground = FindNamedObject(scene, GroundName);
        }

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
    }

    private void BuildLayoutInActiveScene()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            EditorUtility.DisplayDialog("No Active Scene", "Open a scene before generating the layout.", "OK");
            return;
        }

        var meshes = LoadRequiredMeshes(generateMissingAssets);
        if (meshes == null)
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
        InstantiateRequests(container.transform, meshes, requests);

        Selection.activeGameObject = container;
        EditorSceneManager.MarkSceneDirty(scene);
        Undo.CollapseUndoOperations(undoGroup);
    }

    private Dictionary<PropKind, Mesh> LoadRequiredMeshes(bool allowGenerate)
    {
        var meshes = new Dictionary<PropKind, Mesh>();
        var missing = new List<string>();

        LoadMesh(meshes, missing, PropKind.Tree, "Tree");
        LoadMesh(meshes, missing, PropKind.Rock, "Rock");
        LoadMesh(meshes, missing, PropKind.Fence, "Fence");
        LoadMesh(meshes, missing, PropKind.Flower, "Flower");

        if (missing.Count > 0 && allowGenerate)
        {
            ProceduralNatureMeshAssetGenerator.GenerateAssets();
            meshes.Clear();
            missing.Clear();
            LoadMesh(meshes, missing, PropKind.Tree, "Tree");
            LoadMesh(meshes, missing, PropKind.Rock, "Rock");
            LoadMesh(meshes, missing, PropKind.Fence, "Fence");
            LoadMesh(meshes, missing, PropKind.Flower, "Flower");
        }

        if (missing.Count > 0)
        {
            EditorUtility.DisplayDialog(
                "Missing Mesh Assets",
                $"These mesh assets are missing in {MeshFolder}: {string.Join(", ", missing)}",
                "OK");
            return null;
        }

        return meshes;
    }

    private static void LoadMesh(Dictionary<PropKind, Mesh> meshes, List<string> missing, PropKind kind, string assetName)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MeshFolder}/{assetName}.asset");
        if (mesh == null)
        {
            missing.Add(assetName);
            return;
        }

        meshes[kind] = mesh;
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
        AddScatter(requests, occupied, rng, PropKind.Tree, treeCount, 3f, candidate => candidate.magnitude > 12f);
        AddScatter(requests, occupied, rng, PropKind.Rock, rockCount, 2.2f, candidate => candidate.magnitude > 6f);
        AddClusteredFlowers(requests, occupied, rng, flowerCount, new[]
        {
            new Vector2(-10f, 6f),
            new Vector2(8f, -4f),
            new Vector2(2f, 10f)
        }, 5f);

        return requests;
    }

    private List<PlacementRequest> BuildNaturalClearing(System.Random rng)
    {
        var requests = new List<PlacementRequest>();

        AddRing(requests, rng, PropKind.Tree, treeCount, 33f, 40f, -10f, 8f, 10f);
        AddArc(requests, rng, PropKind.Rock, rockCount, 210f, 320f, 22f, 30f, 18f);
        AddFenceEdge(requests, rng, fenceCount, 38f);
        AddClusteredFlowers(requests, new List<OccupiedCircle>(), rng, flowerCount, new[]
        {
            new Vector2(0f, 1f),
            new Vector2(-6f, -4f),
            new Vector2(7f, 5f)
        }, 4.8f);

        return requests;
    }

    private List<PlacementRequest> BuildGardenLayout(System.Random rng)
    {
        var requests = new List<PlacementRequest>();
        AddGardenFenceLoop(requests, rng, fenceCount);
        AddFlowerGrid(requests, rng, flowerCount);
        AddCornerTrees(requests, rng, treeCount);
        AddEntranceRocks(requests, rng, rockCount);
        return requests;
    }

    private void InstantiateRequests(Transform parent, Dictionary<PropKind, Mesh> meshes, List<PlacementRequest> requests)
    {
        foreach (var request in requests)
        {
            var mesh = meshes[request.Kind];
            var gameObject = new GameObject($"{request.Kind}_{parent.childCount:000}");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create Procedural Placement");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.rotation = Quaternion.Euler(0f, request.YawDegrees, 0f);
            gameObject.transform.localScale = Vector3.one * request.UniformScale;

            var meshFilter = gameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            var meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = GetDefaultMaterial();

            var verticalOffset = -mesh.bounds.min.y * request.UniformScale;
            gameObject.transform.position = new Vector3(request.Position.x, verticalOffset, request.Position.y);
        }
    }

    private static Material GetDefaultMaterial()
    {
        if (cachedDefaultMaterial != null)
        {
            return cachedDefaultMaterial;
        }

        cachedDefaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
        if (cachedDefaultMaterial != null)
        {
            return cachedDefaultMaterial;
        }

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (shader != null)
        {
            cachedDefaultMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        return cachedDefaultMaterial;
    }

    private static GameObject EnsureGroundPlane(Scene scene)
    {
        var plane = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.GroundPlane);
        if (plane == null)
        {
            plane = FindNamedObject(scene, GroundName);
        }

        if (plane == null)
        {
            plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(plane, "Create 100m Ground Plane");
            plane.name = GroundName;
            plane.transform.position = Vector3.zero;
        }

        plane.name = GroundName;
        plane.transform.position = Vector3.zero;
        plane.transform.rotation = Quaternion.identity;
        plane.transform.localScale = new Vector3(GroundSize / 10f, 1f, GroundSize / 10f);

        var marker = plane.GetComponent<SceneVertexGeneratedObjectMarker>();
        if (marker == null)
        {
            marker = Undo.AddComponent<SceneVertexGeneratedObjectMarker>(plane);
        }

        marker.kind = SceneVertexGeneratedObjectKind.GroundPlane;
        return plane;
    }

    private static GameObject GetOrCreateLayoutRoot(Scene scene)
    {
        var root = FindMarkedObject(scene, SceneVertexGeneratedObjectKind.LayoutRoot);
        if (root == null)
        {
            root = FindNamedObject(scene, LayoutRootName);
        }

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
            if (marker == null || marker.gameObject.scene != scene)
            {
                continue;
            }

            if (marker.kind == kind)
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

    private void AddScatter(
        List<PlacementRequest> requests,
        List<OccupiedCircle> occupied,
        System.Random rng,
        PropKind kind,
        int count,
        float margin,
        Predicate<Vector2> predicate)
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
            requests.Add(new PlacementRequest(kind, point, NextYaw(rng, kind), scale));
        }
    }

    private void AddClusteredFlowers(
        List<PlacementRequest> requests,
        List<OccupiedCircle> occupied,
        System.Random rng,
        int count,
        IReadOnlyList<Vector2> clusterCenters,
        float clusterRadius)
    {
        for (var i = 0; i < count; i++)
        {
            var center = clusterCenters[i % clusterCenters.Count];
            var scale = NextScale(rng, PropKind.Flower);
            var radius = GetFootprintRadius(PropKind.Flower, scale);
            var placed = false;

            for (var attempt = 0; attempt < 50; attempt++)
            {
                var candidate = center + RandomInsideCircle(rng, clusterRadius);
                if (!IsInsideBounds(candidate, 3f))
                {
                    continue;
                }

                if (IntersectsOccupied(candidate, radius, occupied))
                {
                    continue;
                }

                occupied.Add(new OccupiedCircle(candidate, radius));
                requests.Add(new PlacementRequest(PropKind.Flower, candidate, NextYaw(rng, PropKind.Flower), scale));
                placed = true;
                break;
            }

            if (!placed)
            {
                AddScatter(requests, occupied, rng, PropKind.Flower, 1, 2f, _ => true);
            }
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
                var scale = NextScale(rng, PropKind.Fence);
                var position = first + direction * (spacing * index);
                var radius = GetFootprintRadius(PropKind.Fence, scale);
                if (!IsInsideBounds(position, 4f) || IntersectsOccupied(position, radius, occupied))
                {
                    valid = false;
                    break;
                }

                circles.Add(new OccupiedCircle(position, radius));
                placements.Add(new PlacementRequest(PropKind.Fence, position, yaw, scale));
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

    private void AddRing(
        List<PlacementRequest> requests,
        System.Random rng,
        PropKind kind,
        int count,
        float minRadius,
        float maxRadius,
        float angleOffset,
        float radiusJitter,
        float angleJitter)
    {
        for (var i = 0; i < count; i++)
        {
            var t = count == 0 ? 0f : (float)i / Mathf.Max(1, count);
            var angle = angleOffset + t * 360f + NextRange(rng, -angleJitter, angleJitter);
            var radius = Mathf.Clamp(Mathf.Lerp(minRadius, maxRadius, (float)rng.NextDouble()) + NextRange(rng, -radiusJitter, radiusJitter), minRadius, maxRadius);
            var direction = DirectionFromYaw(angle);
            var scale = NextScale(rng, kind);
            requests.Add(new PlacementRequest(kind, direction * radius, angle + 180f + NextRange(rng, -20f, 20f), scale));
        }
    }

    private void AddArc(
        List<PlacementRequest> requests,
        System.Random rng,
        PropKind kind,
        int count,
        float startAngle,
        float endAngle,
        float minRadius,
        float maxRadius,
        float yawBias)
    {
        for (var i = 0; i < count; i++)
        {
            var t = count <= 1 ? 0.5f : (float)i / (count - 1);
            var angle = Mathf.Lerp(startAngle, endAngle, t) + NextRange(rng, -8f, 8f);
            var radius = NextRange(rng, minRadius, maxRadius);
            var position = DirectionFromYaw(angle) * radius;
            var scale = NextScale(rng, kind);
            requests.Add(new PlacementRequest(kind, position, angle + yawBias + NextRange(rng, -25f, 25f), scale));
        }
    }

    private void AddFenceEdge(List<PlacementRequest> requests, System.Random rng, int count, float zPosition)
    {
        const float spacing = 3.1f;
        var sideCount = Mathf.Min(count, 22);
        var startX = -((sideCount - 1) * spacing) * 0.5f;
        for (var i = 0; i < sideCount; i++)
        {
            var x = startX + i * spacing;
            var position = new Vector2(x, zPosition + NextRange(rng, -0.4f, 0.4f));
            var yaw = 0f + NextRange(rng, -4f, 4f);
            requests.Add(new PlacementRequest(PropKind.Fence, position, yaw, NextScale(rng, PropKind.Fence)));
        }

        for (var i = sideCount; i < count; i++)
        {
            var z = zPosition - 3.1f * (i - sideCount + 1);
            var position = new Vector2(-35f + NextRange(rng, -0.4f, 0.4f), z);
            var yaw = 90f + NextRange(rng, -4f, 4f);
            requests.Add(new PlacementRequest(PropKind.Fence, position, yaw, NextScale(rng, PropKind.Fence)));
        }
    }

    private void AddGardenFenceLoop(List<PlacementRequest> requests, System.Random rng, int count)
    {
        var candidates = BuildGardenFenceCandidates();
        var selected = SampleEvenly(candidates, count);
        foreach (var candidate in selected)
        {
            requests.Add(new PlacementRequest(PropKind.Fence, candidate.Position, candidate.YawDegrees + NextRange(rng, -2f, 2f), NextScale(rng, PropKind.Fence)));
        }
    }

    private void AddFlowerGrid(List<PlacementRequest> requests, System.Random rng, int count)
    {
        if (count <= 0)
        {
            return;
        }

        var columns = Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(count * 1.6f)));
        var rows = Mathf.CeilToInt((float)count / columns);

        for (var i = 0; i < count; i++)
        {
            var column = i % columns;
            var row = i / columns;
            var x = columns == 1 ? 0f : Mathf.Lerp(-10f, 10f, (float)column / (columns - 1));
            var z = rows == 1 ? 0f : Mathf.Lerp(-6f, 6f, (float)row / (rows - 1));
            var position = new Vector2(x, z) + new Vector2(NextRange(rng, -0.4f, 0.4f), NextRange(rng, -0.4f, 0.4f));
            requests.Add(new PlacementRequest(PropKind.Flower, position, NextYaw(rng, PropKind.Flower), NextScale(rng, PropKind.Flower)));
        }
    }

    private void AddCornerTrees(List<PlacementRequest> requests, System.Random rng, int count)
    {
        var anchors = new[]
        {
            new Vector2(-21f, -14f),
            new Vector2(21f, -14f),
            new Vector2(-21f, 14f),
            new Vector2(21f, 14f),
            new Vector2(-28f, 0f),
            new Vector2(28f, 0f),
            new Vector2(0f, 22f),
            new Vector2(0f, -22f)
        };

        for (var i = 0; i < count; i++)
        {
            var anchor = anchors[i % anchors.Length];
            var position = anchor + new Vector2(NextRange(rng, -1.4f, 1.4f), NextRange(rng, -1.4f, 1.4f));
            requests.Add(new PlacementRequest(PropKind.Tree, position, NextYaw(rng, PropKind.Tree), NextScale(rng, PropKind.Tree)));
        }
    }

    private void AddEntranceRocks(List<PlacementRequest> requests, System.Random rng, int count)
    {
        var anchors = new[]
        {
            new Vector2(-5f, -15f),
            new Vector2(5f, -15f),
            new Vector2(-17f, -2f),
            new Vector2(17f, -2f),
            new Vector2(-14f, 13f),
            new Vector2(14f, 13f)
        };

        for (var i = 0; i < count; i++)
        {
            var anchor = anchors[i % anchors.Length];
            var position = anchor + new Vector2(NextRange(rng, -1.2f, 1.2f), NextRange(rng, -1.2f, 1.2f));
            requests.Add(new PlacementRequest(PropKind.Rock, position, NextYaw(rng, PropKind.Rock), NextScale(rng, PropKind.Rock)));
        }
    }

    private static List<PlacementRequest> BuildGardenFenceCandidates()
    {
        const float halfWidth = 17f;
        const float halfHeight = 11f;
        const float spacing = 3.1f;
        const float gateHalfWidth = 4.5f;

        var candidates = new List<PlacementRequest>();

        for (var x = -halfWidth; x <= halfWidth + 0.01f; x += spacing)
        {
            if (Mathf.Abs(x) > gateHalfWidth)
            {
                candidates.Add(new PlacementRequest(PropKind.Fence, new Vector2(x, -halfHeight), 0f, 1f));
            }

            candidates.Add(new PlacementRequest(PropKind.Fence, new Vector2(x, halfHeight), 0f, 1f));
        }

        for (var z = -halfHeight + spacing; z <= halfHeight - spacing + 0.01f; z += spacing)
        {
            candidates.Add(new PlacementRequest(PropKind.Fence, new Vector2(-halfWidth, z), 90f, 1f));
            candidates.Add(new PlacementRequest(PropKind.Fence, new Vector2(halfWidth, z), 90f, 1f));
        }

        return candidates;
    }

    private static List<PlacementRequest> SampleEvenly(List<PlacementRequest> source, int count)
    {
        var result = new List<PlacementRequest>();
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
            var index = Mathf.RoundToInt(step * i);
            result.Add(source[index]);
        }

        return result;
    }

    private static bool TryFindPoint(
        System.Random rng,
        List<OccupiedCircle> occupied,
        float radius,
        float edgeMargin,
        Predicate<Vector2> predicate,
        out Vector2 point)
    {
        for (var attempt = 0; attempt < 150; attempt++)
        {
            var candidate = new Vector2(
                NextRange(rng, -HalfGroundSize + edgeMargin, HalfGroundSize - edgeMargin),
                NextRange(rng, -HalfGroundSize + edgeMargin, HalfGroundSize - edgeMargin));

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
        return point.x >= -HalfGroundSize + margin &&
               point.x <= HalfGroundSize - margin &&
               point.y >= -HalfGroundSize + margin &&
               point.y <= HalfGroundSize - margin;
    }

    private static Vector2 RandomInsideCircle(System.Random rng, float radius)
    {
        var angle = NextRange(rng, 0f, Mathf.PI * 2f);
        var distance = radius * Mathf.Sqrt((float)rng.NextDouble());
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    private static Vector2 DirectionFromYaw(float yawDegrees)
    {
        var radians = yawDegrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radians), Mathf.Cos(radians));
    }

    private static float NextScale(System.Random rng, PropKind kind)
    {
        return kind switch
        {
            PropKind.Tree => NextRange(rng, 0.9f, 1.25f),
            PropKind.Rock => NextRange(rng, 0.8f, 1.35f),
            PropKind.Fence => NextRange(rng, 0.96f, 1.05f),
            PropKind.Flower => NextRange(rng, 0.85f, 1.18f),
            _ => 1f
        };
    }

    private static float NextYaw(System.Random rng, PropKind kind)
    {
        return kind switch
        {
            PropKind.Fence => 90f * rng.Next(0, 4),
            _ => NextRange(rng, 0f, 360f)
        };
    }

    private static float GetFootprintRadius(PropKind kind, float scale)
    {
        var baseRadius = kind switch
        {
            PropKind.Tree => 2.4f,
            PropKind.Rock => 1.8f,
            PropKind.Fence => 1.7f,
            PropKind.Flower => 0.55f,
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
            PlacementPattern.RandomScatter => "랜덤 산포: 전체 영역에 흩뿌리되, 꽃은 군집으로 묶고 울타리는 짧은 줄 형태로 배치합니다.",
            PlacementPattern.NaturalClearing => "자연 공터: 나무를 바깥 고리로 두고, 바위는 한쪽 호 형태, 꽃은 중앙 군집, 울타리는 가장자리 라인으로 배치합니다.",
            PlacementPattern.GardenLayout => "정원형 배치: 울타리 테두리와 입구, 내부 꽃 격자, 코너 나무와 입구 주변 바위로 정돈된 구조를 만듭니다.",
            _ => string.Empty
        };
    }
}
