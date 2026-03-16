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

        if (layout.meadow == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'meadow'.");
        }

        if (layout.ponds == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'ponds'.");
        }

        if (layout.trails == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'trails'.");
        }

        if (layout.groves == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'groves'.");
        }

        if (layout.scatterZones == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'scatterZones'.");
        }

        if (layout.fenceLines == null)
        {
            result.Errors.Add("placement_layout_curated.json is missing 'fenceLines'.");
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

        ValidateMeadow(layout.meadow, result);
        ValidatePonds(layout.ponds, result);
        ValidateTrails(layout.trails, result);
        ValidateGroves(layout.groves, result);
        ValidateScatterZones(layout.scatterZones, result);
        ValidateFenceLines(layout.fenceLines, result);

        if (layout.ponds != null && layout.ponds.Length == 0)
        {
            result.Warnings.Add("No ponds were authored. The curated sample normally uses at least one water feature.");
        }

        if (layout.trails != null && layout.trails.Length == 0)
        {
            result.Warnings.Add("No trails were authored. The new layout schema expects at least one trail corridor.");
        }

        return result;
    }

    private static void ValidateMeadow(PlacementMeadowSettings meadow, PlacementValidationResult result)
    {
        if (meadow == null)
        {
            return;
        }

        if (meadow.accentGrassCount < 0)
        {
            result.Errors.Add("meadow.accentGrassCount must be 0 or greater.");
        }

        if (meadow.openCenterRadiusX <= 0f || meadow.openCenterRadiusY <= 0f)
        {
            result.Errors.Add("meadow.openCenterRadiusX and meadow.openCenterRadiusY must be greater than 0.");
        }

        WarnIfOutOfBounds(meadow.openCenterX, meadow.openCenterY, "meadow open center", result);
    }

    private static void ValidatePonds(PlacementPondFeature[] ponds, PlacementValidationResult result)
    {
        if (ponds == null)
        {
            return;
        }

        for (var index = 0; index < ponds.Length; index++)
        {
            var pond = ponds[index];
            if (pond == null)
            {
                result.Errors.Add($"ponds[{index}] is null.");
                continue;
            }

            if (pond.center == null)
            {
                result.Errors.Add($"ponds[{index}] is missing center.");
                continue;
            }

            if (pond.radiusX <= 0f || pond.radiusY <= 0f)
            {
                result.Errors.Add($"ponds[{index}] radiusX and radiusY must be greater than 0.");
            }

            if (pond.sandWidth < 0f || pond.pebbleWidth < 0f)
            {
                result.Errors.Add($"ponds[{index}] sandWidth and pebbleWidth must be 0 or greater.");
            }

            if (pond.irregularity < 0f || pond.irregularity > 1f)
            {
                result.Errors.Add($"ponds[{index}] irregularity must be between 0 and 1.");
            }

            WarnIfOutOfBounds(pond.center.x, pond.center.y, $"ponds[{index}]", result);

            if (pond.lobes == null)
            {
                result.Errors.Add($"ponds[{index}] is missing lobes.");
                continue;
            }

            for (var lobeIndex = 0; lobeIndex < pond.lobes.Length; lobeIndex++)
            {
                var lobe = pond.lobes[lobeIndex];
                if (lobe == null)
                {
                    result.Errors.Add($"ponds[{index}].lobes[{lobeIndex}] is null.");
                    continue;
                }

                if (lobe.distance < 0f)
                {
                    result.Errors.Add($"ponds[{index}].lobes[{lobeIndex}] distance must be 0 or greater.");
                }

                if (lobe.radiusXScale <= 0f || lobe.radiusYScale <= 0f)
                {
                    result.Errors.Add($"ponds[{index}].lobes[{lobeIndex}] radius scales must be greater than 0.");
                }
            }
        }
    }

    private static void ValidateTrails(PlacementTrailCorridor[] trails, PlacementValidationResult result)
    {
        if (trails == null)
        {
            return;
        }

        for (var index = 0; index < trails.Length; index++)
        {
            var trail = trails[index];
            if (trail == null)
            {
                result.Errors.Add($"trails[{index}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(trail.id))
            {
                result.Errors.Add($"trails[{index}] requires a non-empty id.");
            }

            if (trail.points == null || trail.points.Length < 2)
            {
                result.Errors.Add($"trails[{index}] must contain at least 2 points.");
                continue;
            }

            if (trail.traffic <= 0f)
            {
                result.Errors.Add($"trails[{index}] traffic must be greater than 0.");
            }

            if (trail.width <= 0f)
            {
                result.Errors.Add($"trails[{index}] width must be greater than 0.");
            }

            if (trail.soilExposure < 0f || trail.soilExposure > 1f)
            {
                result.Errors.Add($"trails[{index}] soilExposure must be between 0 and 1.");
            }

            if (trail.braid < 0f || trail.braid > 1f)
            {
                result.Errors.Add($"trails[{index}] braid must be between 0 and 1.");
            }

            if (trail.wander < 0f || trail.wander > 1f)
            {
                result.Errors.Add($"trails[{index}] wander must be between 0 and 1.");
            }

            for (var pointIndex = 0; pointIndex < trail.points.Length; pointIndex++)
            {
                var point = trail.points[pointIndex];
                if (point == null)
                {
                    result.Errors.Add($"trails[{index}].points[{pointIndex}] is null.");
                    continue;
                }

                WarnIfOutOfBounds(point.x, point.y, $"trails[{index}].points[{pointIndex}]", result);
            }
        }
    }

    private static void ValidateGroves(PlacementGroveZone[] groves, PlacementValidationResult result)
    {
        if (groves == null)
        {
            return;
        }

        for (var index = 0; index < groves.Length; index++)
        {
            var grove = groves[index];
            if (grove == null)
            {
                result.Errors.Add($"groves[{index}] is null.");
                continue;
            }

            if (grove.center == null)
            {
                result.Errors.Add($"groves[{index}] is missing center.");
                continue;
            }

            if (grove.radiusX <= 0f || grove.radiusY <= 0f)
            {
                result.Errors.Add($"groves[{index}] radiusX and radiusY must be greater than 0.");
            }

            if (grove.treeCount < 0 || grove.bushCount < 0 || grove.mushroomCount < 0)
            {
                result.Errors.Add($"groves[{index}] counts must be 0 or greater.");
            }

            if (grove.innerClear < 0f || grove.innerClear >= 1f)
            {
                result.Errors.Add($"groves[{index}] innerClear must be between 0 and 1.");
            }

            if (grove.treeEdgeBias < 0f || grove.treeEdgeBias > 1f)
            {
                result.Errors.Add($"groves[{index}] treeEdgeBias must be between 0 and 1.");
            }

            WarnIfOutOfBounds(grove.center.x, grove.center.y, $"groves[{index}]", result);
        }
    }

    private static void ValidateScatterZones(PlacementScatterZone[] scatterZones, PlacementValidationResult result)
    {
        if (scatterZones == null)
        {
            return;
        }

        for (var index = 0; index < scatterZones.Length; index++)
        {
            var zone = scatterZones[index];
            if (zone == null)
            {
                result.Errors.Add($"scatterZones[{index}] is null.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(zone.id))
            {
                result.Errors.Add($"scatterZones[{index}] requires a non-empty id.");
            }

            if (!PropIds.Contains(zone.kind))
            {
                result.Errors.Add($"scatterZones[{index}] uses unsupported kind '{zone.kind}'.");
            }

            if (zone.center == null)
            {
                result.Errors.Add($"scatterZones[{index}] is missing center.");
                continue;
            }

            if (zone.radiusX <= 0f || zone.radiusY <= 0f)
            {
                result.Errors.Add($"scatterZones[{index}] radiusX and radiusY must be greater than 0.");
            }

            if (zone.count < 0)
            {
                result.Errors.Add($"scatterZones[{index}] count must be 0 or greater.");
            }

            if (zone.innerRadius < 0f || zone.innerRadius > 1f)
            {
                result.Errors.Add($"scatterZones[{index}] innerRadius must be between 0 and 1.");
            }

            if (zone.scaleMin <= 0f || zone.scaleMax <= 0f || zone.scaleMax < zone.scaleMin)
            {
                result.Errors.Add($"scatterZones[{index}] scaleMin/scaleMax must be positive and ordered.");
            }

            if (zone.opacityMin < 0f || zone.opacityMax > 1f || zone.opacityMax < zone.opacityMin)
            {
                result.Errors.Add($"scatterZones[{index}] opacityMin/opacityMax must stay within 0..1 and be ordered.");
            }

            WarnIfOutOfBounds(zone.center.x, zone.center.y, $"scatterZones[{index}]", result);
        }
    }

    private static void ValidateFenceLines(PlacementFenceLine[] fenceLines, PlacementValidationResult result)
    {
        if (fenceLines == null)
        {
            return;
        }

        for (var index = 0; index < fenceLines.Length; index++)
        {
            var line = fenceLines[index];
            if (line == null)
            {
                result.Errors.Add($"fenceLines[{index}] is null.");
                continue;
            }

            if (line.points == null || line.points.Length < 2)
            {
                result.Errors.Add($"fenceLines[{index}] must contain at least 2 points.");
                continue;
            }

            if (line.density <= 0f)
            {
                result.Errors.Add($"fenceLines[{index}] density must be greater than 0.");
            }

            if (line.brokenness < 0f || line.brokenness > 1f)
            {
                result.Errors.Add($"fenceLines[{index}] brokenness must be between 0 and 1.");
            }

            if (line.lengthScale <= 0f)
            {
                result.Errors.Add($"fenceLines[{index}] lengthScale must be greater than 0.");
            }

            ValidateOpacity(line.opacity, $"fenceLines[{index}]", result);
            for (var pointIndex = 0; pointIndex < line.points.Length; pointIndex++)
            {
                var point = line.points[pointIndex];
                if (point == null)
                {
                    result.Errors.Add($"fenceLines[{index}].points[{pointIndex}] is null.");
                    continue;
                }

                WarnIfOutOfBounds(point.x, point.y, $"fenceLines[{index}].points[{pointIndex}]", result);
            }
        }
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
