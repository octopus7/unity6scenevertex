using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

internal sealed class PlacementValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool HasErrors => Errors.Count > 0;
}

internal static class PlacementValidation
{
    private static readonly string[] RequiredIds =
    {
        "water",
        "pebble",
        "sand",
        "soil",
        "grass",
        "road",
        "tree",
        "rock",
        "bush",
        "mushroom",
        "fence"
    };

    private static readonly HashSet<string> GroundIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "water",
        "pebble",
        "sand",
        "soil",
        "grass"
    };

    private static readonly HashSet<string> PropIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "tree",
        "rock",
        "bush",
        "mushroom"
    };

    public static PlacementValidationResult Validate(PlacementElementCatalog catalog, PlacementLayoutScene layout)
    {
        var result = new PlacementValidationResult();

        if (catalog == null)
        {
            result.Errors.Add("Element catalog JSON did not load.");
            return result;
        }

        if (layout == null)
        {
            result.Errors.Add("Layout JSON did not load.");
            return result;
        }

        if (catalog.canvasWidth != PlacementEvaluationPaths.CanvasWidth || catalog.canvasHeight != PlacementEvaluationPaths.CanvasHeight)
        {
            result.Errors.Add($"Canvas size must stay fixed at {PlacementEvaluationPaths.CanvasWidth}x{PlacementEvaluationPaths.CanvasHeight}.");
        }

        if (catalog.elements == null || catalog.elements.Length == 0)
        {
            result.Errors.Add("placement_elements.json must contain a non-empty 'elements' array.");
        }

        if (layout.typeVariants == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'typeVariants'.");
        }

        if (layout.groundPatches == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'groundPatches'.");
        }

        if (layout.roadSegments == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'roadSegments'.");
        }

        if (layout.props == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'props'.");
        }

        if (layout.fenceSegments == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'fenceSegments'.");
        }

        if (string.IsNullOrWhiteSpace(layout.sceneName))
        {
            result.Warnings.Add("sceneName is empty. The curated sample expects 'AestheticNatural01'.");
        }
        else if (!string.Equals(layout.sceneName, "AestheticNatural01", StringComparison.Ordinal))
        {
            result.Warnings.Add($"sceneName is '{layout.sceneName}'. The preview output path remains fixed at AestheticNatural01_preview.png.");
        }

        var definitions = new Dictionary<string, PlacementElementDefinition>(StringComparer.OrdinalIgnoreCase);
        if (catalog.elements != null)
        {
            foreach (var definition in catalog.elements)
            {
                if (definition == null)
                {
                    result.Errors.Add("placement_elements.json contains a null element entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(definition.id))
                {
                    result.Errors.Add("Each element definition requires a non-empty 'id'.");
                    continue;
                }

                if (!definitions.TryAdd(definition.id, definition))
                {
                    result.Errors.Add($"Duplicate element definition id '{definition.id}'.");
                    continue;
                }

                ValidateElementDefinition(definition, result);
            }
        }

        foreach (var requiredId in RequiredIds)
        {
            if (!definitions.ContainsKey(requiredId))
            {
                result.Errors.Add($"placement_elements.json is missing required element '{requiredId}'.");
            }
        }

        var selectedVariants = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (layout.typeVariants != null)
        {
            foreach (var typeVariant in layout.typeVariants)
            {
                if (typeVariant == null)
                {
                    result.Errors.Add("typeVariants contains a null entry.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(typeVariant.id))
                {
                    result.Errors.Add("Each typeVariants entry requires a non-empty 'id'.");
                    continue;
                }

                if (!definitions.ContainsKey(typeVariant.id))
                {
                    result.Errors.Add($"typeVariants references unknown id '{typeVariant.id}'.");
                    continue;
                }

                if (!selectedVariants.TryAdd(typeVariant.id, typeVariant.variantIndex))
                {
                    result.Errors.Add($"typeVariants contains duplicate id '{typeVariant.id}'.");
                }

                if (typeVariant.variantIndex < 0 || typeVariant.variantIndex > 2)
                {
                    result.Errors.Add($"typeVariants for '{typeVariant.id}' must use variantIndex 0, 1, or 2.");
                }
            }
        }

        foreach (var requiredId in RequiredIds)
        {
            if (!selectedVariants.ContainsKey(requiredId))
            {
                result.Errors.Add($"typeVariants is missing required id '{requiredId}'.");
            }
        }

        if (layout.groundPatches != null)
        {
            for (var index = 0; index < layout.groundPatches.Length; index++)
            {
                var patch = layout.groundPatches[index];
                if (patch == null)
                {
                    result.Errors.Add($"groundPatches[{index}] is null.");
                    continue;
                }

                if (!GroundIds.Contains(patch.id))
                {
                    result.Errors.Add($"groundPatches[{index}] uses unsupported id '{patch.id}'.");
                }

                if (patch.radiusScale <= 0f)
                {
                    result.Errors.Add($"groundPatches[{index}] radiusScale must be greater than 0.");
                }

                if (patch.aspectX <= 0f || patch.aspectY <= 0f)
                {
                    result.Errors.Add($"groundPatches[{index}] aspectX and aspectY must be greater than 0.");
                }

                ValidateOpacity(patch.opacity, $"groundPatches[{index}]", result);
                WarnIfOutOfBounds(patch.x, patch.y, $"groundPatches[{index}]", result);
            }
        }

        if (layout.roadSegments != null)
        {
            for (var index = 0; index < layout.roadSegments.Length; index++)
            {
                var segment = layout.roadSegments[index];
                if (segment == null)
                {
                    result.Errors.Add($"roadSegments[{index}] is null.");
                    continue;
                }

                if (segment.lengthScale <= 0f)
                {
                    result.Errors.Add($"roadSegments[{index}] lengthScale must be greater than 0.");
                }

                if (segment.thicknessScale <= 0f)
                {
                    result.Errors.Add($"roadSegments[{index}] thicknessScale must be greater than 0.");
                }

                ValidateOpacity(segment.opacity, $"roadSegments[{index}]", result);
                WarnIfOutOfBounds(segment.x, segment.y, $"roadSegments[{index}]", result);
            }
        }

        if (layout.props != null)
        {
            for (var index = 0; index < layout.props.Length; index++)
            {
                var prop = layout.props[index];
                if (prop == null)
                {
                    result.Errors.Add($"props[{index}] is null.");
                    continue;
                }

                if (!PropIds.Contains(prop.id))
                {
                    result.Errors.Add($"props[{index}] uses unsupported id '{prop.id}'.");
                }

                if (prop.radiusScale <= 0f)
                {
                    result.Errors.Add($"props[{index}] radiusScale must be greater than 0.");
                }

                ValidateOpacity(prop.opacity, $"props[{index}]", result);
                WarnIfOutOfBounds(prop.x, prop.y, $"props[{index}]", result);
            }
        }

        if (layout.fenceSegments != null)
        {
            for (var index = 0; index < layout.fenceSegments.Length; index++)
            {
                var segment = layout.fenceSegments[index];
                if (segment == null)
                {
                    result.Errors.Add($"fenceSegments[{index}] is null.");
                    continue;
                }

                if (segment.lengthScale <= 0f)
                {
                    result.Errors.Add($"fenceSegments[{index}] lengthScale must be greater than 0.");
                }

                ValidateOpacity(segment.opacity, $"fenceSegments[{index}]", result);
                WarnIfOutOfBounds(segment.x, segment.y, $"fenceSegments[{index}]", result);
            }
        }

        return result;
    }

    private static void ValidateElementDefinition(PlacementElementDefinition definition, PlacementValidationResult result)
    {
        switch (definition.id)
        {
            case "water":
            case "pebble":
            case "sand":
            case "soil":
            case "grass":
                ValidateLayerAndShape(definition, "ground", "blob", result);
                if (definition.baseRadius <= 0f)
                {
                    result.Errors.Add($"Element '{definition.id}' requires a positive baseRadius.");
                }
                break;
            case "road":
                ValidateLayerAndShape(definition, "road", "segment", result);
                if (definition.segmentLength <= 0f || definition.segmentThickness <= 0f)
                {
                    result.Errors.Add("Element 'road' requires positive segmentLength and segmentThickness.");
                }
                break;
            case "tree":
                ValidateLayerAndShape(definition, "prop", "tree", result);
                if (definition.baseRadius <= 0f)
                {
                    result.Errors.Add("Element 'tree' requires a positive baseRadius.");
                }
                break;
            case "rock":
                ValidateLayerAndShape(definition, "prop", "rock", result);
                if (definition.baseRadius <= 0f)
                {
                    result.Errors.Add("Element 'rock' requires a positive baseRadius.");
                }
                break;
            case "bush":
                ValidateLayerAndShape(definition, "prop", "bush", result);
                if (definition.baseRadius <= 0f)
                {
                    result.Errors.Add("Element 'bush' requires a positive baseRadius.");
                }
                break;
            case "mushroom":
                ValidateLayerAndShape(definition, "prop", "mushroom", result);
                if (definition.baseRadius <= 0f)
                {
                    result.Errors.Add("Element 'mushroom' requires a positive baseRadius.");
                }
                break;
            case "fence":
                ValidateLayerAndShape(definition, "fence", "fence", result);
                if (definition.segmentLength <= 0f || definition.segmentThickness <= 0f)
                {
                    result.Errors.Add("Element 'fence' requires positive segmentLength and segmentThickness.");
                }
                break;
            default:
                result.Errors.Add($"Unsupported element id '{definition.id}' in placement_elements.json.");
                break;
        }

        if (definition.variants == null || definition.variants.Length != 3)
        {
            result.Errors.Add($"Element '{definition.id}' must define exactly 3 color variants.");
            return;
        }

        for (var index = 0; index < definition.variants.Length; index++)
        {
            if (!ColorUtility.TryParseHtmlString(definition.variants[index], out _))
            {
                result.Errors.Add($"Element '{definition.id}' variant {index} must be a valid HTML color.");
            }
        }
    }

    private static void ValidateLayerAndShape(PlacementElementDefinition definition, string expectedLayer, string expectedShape, PlacementValidationResult result)
    {
        if (!string.Equals(definition.layer, expectedLayer, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Element '{definition.id}' must use layer '{expectedLayer}'.");
        }

        if (!string.Equals(definition.shape, expectedShape, StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"Element '{definition.id}' must use shape '{expectedShape}'.");
        }
    }

    private static void ValidateOpacity(float opacity, string label, PlacementValidationResult result)
    {
        if (opacity < 0f || opacity > 1f)
        {
            result.Errors.Add($"{label} opacity must be between 0 and 1.");
        }
    }

    private static void WarnIfOutOfBounds(float x, float y, string label, PlacementValidationResult result)
    {
        if (x < 0f || x > PlacementEvaluationPaths.CanvasWidth || y < 0f || y > PlacementEvaluationPaths.CanvasHeight)
        {
            result.Warnings.Add($"{label} center is outside the {PlacementEvaluationPaths.CanvasWidth}x{PlacementEvaluationPaths.CanvasHeight} canvas and will be clipped.");
        }
    }
}

internal static class PlacementEvaluationPreviewUtility
{
    public static bool TryRenderAndSavePreview(out Texture2D previewTexture, out List<string> warnings, out string error)
    {
        previewTexture = null;
        warnings = new List<string>();
        error = string.Empty;

        if (!PlacementEvaluationDataLoader.TryLoad(out var catalog, out var layout, out error))
        {
            return false;
        }

        var validation = PlacementValidation.Validate(catalog, layout);
        warnings.AddRange(validation.Warnings);
        if (validation.HasErrors)
        {
            error = string.Join(Environment.NewLine, validation.Errors);
            return false;
        }

        previewTexture = PlacementBitmapRenderer.Render(catalog, layout);
        return TrySavePreview(previewTexture, out error);
    }

    public static bool TryLoadExistingPreview(out Texture2D previewTexture, out string error)
    {
        previewTexture = null;
        error = string.Empty;
        var previewPath = PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.PreviewPngAssetPath);
        if (!File.Exists(previewPath))
        {
            error = "Preview PNG does not exist yet. Use Refresh Preview to generate it.";
            return false;
        }

        try
        {
            var bytes = File.ReadAllBytes(previewPath);
            previewTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false)
            {
                name = "PlacementEvaluationPreview",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
            previewTexture.LoadImage(bytes, false);
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to load preview PNG: {exception.Message}";
            return false;
        }
    }

    public static void GeneratePreviewBatch()
    {
        if (!TryRenderAndSavePreview(out var texture, out var warnings, out var error))
        {
            throw new InvalidOperationException(error);
        }

        if (warnings.Count > 0)
        {
            Debug.LogWarning(string.Join(Environment.NewLine, warnings));
        }

        UnityEngine.Object.DestroyImmediate(texture);
    }

    [MenuItem("Tools/SceneVertex/Refresh Placement Evaluation Preview")]
    private static void RefreshPreviewFromMenu()
    {
        if (!TryRenderAndSavePreview(out var texture, out var warnings, out var error))
        {
            Debug.LogError(error);
            return;
        }

        if (warnings.Count > 0)
        {
            Debug.LogWarning(string.Join(Environment.NewLine, warnings));
        }

        UnityEngine.Object.DestroyImmediate(texture);
        Debug.Log($"Placement evaluation preview saved to {PlacementEvaluationPaths.PreviewPngAssetPath}.");
    }

    private static bool TrySavePreview(Texture2D previewTexture, out string error)
    {
        error = string.Empty;
        if (previewTexture == null)
        {
            error = "Cannot save a null preview texture.";
            return false;
        }

        try
        {
            var previewPath = PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.PreviewPngAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(previewPath) ?? PlacementEvaluationPaths.GetSystemPath(PlacementEvaluationPaths.RootAssetFolder));
            File.WriteAllBytes(previewPath, previewTexture.EncodeToPNG());
            AssetDatabase.Refresh();
            return true;
        }
        catch (Exception exception)
        {
            error = $"Failed to save preview PNG: {exception.Message}";
            return false;
        }
    }
}
