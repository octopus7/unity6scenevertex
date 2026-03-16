using System;
using System.IO;
using UnityEngine;

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
    public PlacementTypeVariant[] typeVariants = Array.Empty<PlacementTypeVariant>();
    public PlacementGroundPatch[] groundPatches = Array.Empty<PlacementGroundPatch>();
    public PlacementRoadSegment[] roadSegments = Array.Empty<PlacementRoadSegment>();
    public PlacementPropInstance[] props = Array.Empty<PlacementPropInstance>();
    public PlacementFenceSegment[] fenceSegments = Array.Empty<PlacementFenceSegment>();
}

[Serializable]
internal sealed class PlacementTypeVariant
{
    public string id = string.Empty;
    public int variantIndex;
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
internal sealed class PlacementRoadSegment
{
    public float x;
    public float y;
    public float rotationDeg;
    public float lengthScale = 1f;
    public float thicknessScale = 1f;
    public float opacity = 1f;
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
