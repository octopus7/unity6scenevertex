using System;
using System.IO;
using UnityEngine;

#pragma warning disable CS0649

[Serializable]
internal sealed class PlacementElementCatalog
{
    public int canvasWidth = PlacementEvaluationPaths.CanvasWidth;
    public int canvasHeight = PlacementEvaluationPaths.CanvasHeight;
    public PlacementElementDefinition[] elements = Array.Empty<PlacementElementDefinition>();
}

[Serializable]
internal sealed class PlacementElementDefinition
{
    public string id = string.Empty;
    public string layer = string.Empty;
    public string shape = string.Empty;
    public float baseRadius;
    public float segmentLength;
    public float segmentThickness;
    public string[] variants = Array.Empty<string>();
}

[Serializable]
internal sealed class PlacementLayoutScene
{
    public string sceneName = string.Empty;
    public int seed = 1;
    public PlacementTypeVariant[] typeVariants = Array.Empty<PlacementTypeVariant>();
    public PlacementMeadowSettings meadow = new();
    public PlacementPondFeature[] ponds = Array.Empty<PlacementPondFeature>();
    public PlacementTrailCorridor[] trails = Array.Empty<PlacementTrailCorridor>();
    public PlacementGroveZone[] groves = Array.Empty<PlacementGroveZone>();
    public PlacementScatterZone[] scatterZones = Array.Empty<PlacementScatterZone>();
    public PlacementFenceLine[] fenceLines = Array.Empty<PlacementFenceLine>();
}

[Serializable]
internal sealed class PlacementTypeVariant
{
    public string id = string.Empty;
    public int variantIndex;
}

[Serializable]
internal sealed class PlacementPoint
{
    public float x;
    public float y;
}

[Serializable]
internal sealed class PlacementMeadowSettings
{
    public int accentGrassCount = 96;
    public float openCenterX = PlacementEvaluationPaths.CanvasWidth * 0.5f;
    public float openCenterY = PlacementEvaluationPaths.CanvasHeight * 0.5f;
    public float openCenterRadiusX = 220f;
    public float openCenterRadiusY = 128f;
    public float windAngleDeg = 8f;
}

[Serializable]
internal sealed class PlacementPondFeature
{
    public PlacementPoint center = new();
    public float radiusX = 72f;
    public float radiusY = 56f;
    public float rotationDeg;
    public float sandWidth = 18f;
    public float pebbleWidth = 10f;
    public float irregularity = 0.22f;
    public PlacementPondLobe[] lobes = Array.Empty<PlacementPondLobe>();
}

[Serializable]
internal sealed class PlacementPondLobe
{
    public float angleDeg;
    public float distance;
    public float radiusXScale = 0.75f;
    public float radiusYScale = 0.75f;
    public float rotationDeg;
}

[Serializable]
internal sealed class PlacementTrailCorridor
{
    public string id = string.Empty;
    public PlacementPoint[] points = Array.Empty<PlacementPoint>();
    public float traffic = 0.8f;
    public float width = 1f;
    public float soilExposure = 0.3f;
    public float braid = 0.12f;
    public float wander = 0.18f;
}

[Serializable]
internal sealed class PlacementGroveZone
{
    public string id = string.Empty;
    public PlacementPoint center = new();
    public float radiusX = 76f;
    public float radiusY = 52f;
    public float rotationDeg;
    public int treeCount = 16;
    public int bushCount = 20;
    public int mushroomCount = 12;
    public float innerClear = 0.08f;
    public float treeEdgeBias = 0.58f;
}

[Serializable]
internal sealed class PlacementScatterZone
{
    public string id = string.Empty;
    public string kind = string.Empty;
    public PlacementPoint center = new();
    public float radiusX = 40f;
    public float radiusY = 20f;
    public float rotationDeg;
    public int count = 10;
    public float innerRadius = 0f;
    public float scaleMin = 0.8f;
    public float scaleMax = 1.12f;
    public float opacityMin = 0.58f;
    public float opacityMax = 0.82f;
}

[Serializable]
internal sealed class PlacementFenceLine
{
    public PlacementPoint[] points = Array.Empty<PlacementPoint>();
    public float density = 1f;
    public float brokenness = 0.12f;
    public float lengthScale = 0.92f;
    public float opacity = 0.8f;
}

[Serializable]
internal sealed class PlacementGroundPatch
{
    public string id = string.Empty;
    public float x;
    public float y;
    public float radiusScale = 1f;
    public float aspectX = 1f;
    public float aspectY = 1f;
    public float rotationDeg;
    public float opacity = 1f;
}

[Serializable]
internal sealed class PlacementTrailMark
{
    public float x;
    public float y;
    public float rotationDeg;
    public float lengthScale = 1f;
    public float thicknessScale = 1f;
    public float opacity = 1f;
    public float soilExposure = 0.3f;
}

[Serializable]
internal sealed class PlacementPropInstance
{
    public string id = string.Empty;
    public float x;
    public float y;
    public float radiusScale = 1f;
    public float rotationDeg;
    public float opacity = 1f;
}

[Serializable]
internal sealed class PlacementFenceSegment
{
    public float x;
    public float y;
    public float rotationDeg;
    public float lengthScale = 1f;
    public float opacity = 1f;
}

internal sealed class PlacementResolvedScene
{
    public PlacementGroundPatch[] groundPatches = Array.Empty<PlacementGroundPatch>();
    public PlacementTrailMark[] trailMarks = Array.Empty<PlacementTrailMark>();
    public PlacementPropInstance[] props = Array.Empty<PlacementPropInstance>();
    public PlacementFenceSegment[] fenceSegments = Array.Empty<PlacementFenceSegment>();
}

internal static class PlacementEvaluationPaths
{
    public const int CanvasWidth = 1024;
    public const int CanvasHeight = 512;
    public const string RootAssetFolder = "Assets/PlacementEvaluation";
    public const string ElementsJsonAssetPath = RootAssetFolder + "/placement_elements.json";
    public const string LayoutJsonAssetPath = RootAssetFolder + "/placement_layout_curated.json";
    public const string RulesMarkdownAssetPath = RootAssetFolder + "/JSON_EDIT_RULES.md";
    public const string CurationMarkdownAssetPath = RootAssetFolder + "/CURATION_GUIDE.md";
    public const string PreviewPngAssetPath = RootAssetFolder + "/AestheticNatural01_preview.png";

    public static string ProjectRootPath
    {
        get
        {
            var dataPath = Application.dataPath;
            var parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }
    }

    public static string GetSystemPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRootPath, assetPath.Replace('/', Path.DirectorySeparatorChar)));
    }
}

internal static class PlacementEvaluationDataLoader
{
    public static bool TryLoad(out PlacementElementCatalog catalog, out PlacementLayoutScene layout, out string error)
    {
        catalog = null;
        layout = null;
        error = string.Empty;

        if (!TryLoadJson(PlacementEvaluationPaths.ElementsJsonAssetPath, out catalog, out error))
        {
            return false;
        }

        if (!TryLoadJson(PlacementEvaluationPaths.LayoutJsonAssetPath, out layout, out error))
        {
            return false;
        }

        return true;
    }

    private static bool TryLoadJson<T>(string assetPath, out T value, out string error)
        where T : class
    {
        value = null;
        error = string.Empty;

        var filePath = PlacementEvaluationPaths.GetSystemPath(assetPath);
        if (!File.Exists(filePath))
        {
            error = $"Missing required JSON file: {assetPath}";
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            value = JsonUtility.FromJson<T>(json);
            if (value == null)
            {
                error = $"Failed to deserialize JSON file: {assetPath}";
                return false;
            }

            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to read JSON file '{assetPath}': {exception.Message}";
            return false;
        }
    }
}

#pragma warning restore CS0649
