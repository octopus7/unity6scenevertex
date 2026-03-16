using System;
using System.Collections.Generic;
using UnityEngine;

internal static class PlacementBitmapRenderer
{
    private sealed class RenderContext
    {
        private readonly Dictionary<string, PlacementElementDefinition> definitions;
        private readonly Dictionary<string, Color> colors;

        public RenderContext(PlacementElementCatalog catalog, PlacementLayoutScene layout)
        {
            definitions = new Dictionary<string, PlacementElementDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in catalog.elements)
            {
                definitions[definition.id] = definition;
            }

            var selectedVariants = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var variant in layout.typeVariants)
            {
                selectedVariants[variant.id] = variant.variantIndex;
            }

            colors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in definitions)
            {
                var index = selectedVariants.TryGetValue(pair.Key, out var selectedVariant) ? Mathf.Clamp(selectedVariant, 0, pair.Value.variants.Length - 1) : 0;
                ColorUtility.TryParseHtmlString(pair.Value.variants[index], out var parsedColor);
                colors[pair.Key] = parsedColor;
            }
        }

        public PlacementElementDefinition GetDefinition(string id)
        {
            return definitions[id];
        }

        public Color GetColor(string id)
        {
            return colors[id];
        }
    }

    public static Texture2D Render(PlacementElementCatalog catalog, PlacementLayoutScene layout)
    {
        var context = new RenderContext(catalog, layout);
        var resolved = PlacementProceduralSceneBuilder.Build(catalog, layout);
        var pixels = new Color32[PlacementEvaluationPaths.CanvasWidth * PlacementEvaluationPaths.CanvasHeight];

        FillBackground(pixels, context.GetColor("grass"), layout.meadow);

        DrawGroundPatches(pixels, resolved.groundPatches, "grass", context);
        DrawGroundPatches(pixels, resolved.groundPatches, "soil", context);
        DrawGroundPatches(pixels, resolved.groundPatches, "sand", context);
        DrawGroundPatches(pixels, resolved.groundPatches, "pebble", context);
        DrawGroundPatches(pixels, resolved.groundPatches, "water", context);

        foreach (var mark in resolved.trailMarks)
        {
            DrawTrailMark(pixels, mark, context);
        }

        DrawProps(pixels, resolved.props, "rock", context);
        DrawProps(pixels, resolved.props, "bush", context);
        DrawProps(pixels, resolved.props, "mushroom", context);
        DrawProps(pixels, resolved.props, "tree", context);

        foreach (var segment in resolved.fenceSegments)
        {
            DrawFenceSegment(pixels, segment, context.GetDefinition("fence"), context.GetColor("fence"));
        }

        var texture = new Texture2D(PlacementEvaluationPaths.CanvasWidth, PlacementEvaluationPaths.CanvasHeight, TextureFormat.RGBA32, false, false)
        {
            name = "PlacementEvaluationPreview",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
        return texture;
    }

    private static void FillBackground(Color32[] pixels, Color baseColor, PlacementMeadowSettings meadow)
    {
        meadow ??= new PlacementMeadowSettings();
        var center = new Vector2(PlacementEvaluationPaths.CanvasWidth * 0.52f, PlacementEvaluationPaths.CanvasHeight * 0.5f);
        var wind = meadow.windAngleDeg * Mathf.Deg2Rad;
        var windVector = new Vector2(Mathf.Cos(wind), Mathf.Sin(wind));
        for (var y = 0; y < PlacementEvaluationPaths.CanvasHeight; y++)
        {
            for (var x = 0; x < PlacementEvaluationPaths.CanvasWidth; x++)
            {
                var index = y * PlacementEvaluationPaths.CanvasWidth + x;
                var position = new Vector2(x, y);
                var distance = Vector2.Distance(position, center);
                var vignette = Mathf.InverseLerp(560f, 100f, distance);
                var stripe = Vector2.Dot(position, windVector) * 0.08f;
                var noise = HashSigned(x, y, 17);
                var gradient = 0.95f + 0.05f * vignette + Mathf.Sin(stripe) * 0.015f + noise * 0.035f;
                pixels[index] = Multiply(baseColor, gradient);
            }
        }
    }

    private static void DrawGroundPatches(Color32[] pixels, PlacementGroundPatch[] patches, string id, RenderContext context)
    {
        if (patches == null)
        {
            return;
        }

        var definition = context.GetDefinition(id);
        var color = context.GetColor(id);
        foreach (var patch in patches)
        {
            if (patch == null || !string.Equals(patch.id, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var radius = definition.baseRadius * patch.radiusScale;
            var radiusX = radius * patch.aspectX;
            var radiusY = radius * patch.aspectY;
            var center = new Vector2(patch.x, patch.y);

            switch (id)
            {
                case "grass":
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 0.92f), patch.opacity * 0.22f, 0.14f, 0.05f, 0.05f);
                    break;
                case "soil":
                    DrawFeatheredEllipse(pixels, center + new Vector2(2f, 3f), radiusX * 1.02f, radiusY * 1.02f, patch.rotationDeg, Color.black, patch.opacity * 0.04f, 0f, 0f, 0f);
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.62f, 0.08f, 0.05f, 0.04f);
                    break;
                case "sand":
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.66f, 0.2f, 0.04f, 0.03f);
                    DrawFeatheredEllipse(pixels, center + new Vector2(-3f, -2f), radiusX * 0.76f, radiusY * 0.72f, patch.rotationDeg, Multiply(color, 1.07f), patch.opacity * 0.15f, 0.18f, 0f, 0f);
                    break;
                case "pebble":
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.52f, 0.1f, 0.07f, 0.06f);
                    DrawPebbleSpeckles(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 1.1f), patch.opacity * 0.34f);
                    break;
                case "water":
                    DrawFeatheredEllipse(pixels, center + new Vector2(4f, 5f), radiusX * 1.05f, radiusY * 1.05f, patch.rotationDeg, Color.black, patch.opacity * 0.1f, 0f, 0f, 0f);
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 0.95f), patch.opacity * 0.8f, 0.08f, 0.16f, 0.03f);
                    DrawFeatheredEllipse(pixels, center + new Vector2(-7f, -5f), radiusX * 0.62f, radiusY * 0.56f, patch.rotationDeg, Multiply(color, 1.16f), patch.opacity * 0.2f, 0.18f, 0f, 0f);
                    break;
            }
        }
    }

    private static void DrawTrailMark(Color32[] pixels, PlacementTrailMark mark, RenderContext context)
    {
        var definition = context.GetDefinition("road");
        var grassColor = context.GetColor("grass");
        var roadColor = context.GetColor("road");
        var center = new Vector2(mark.x, mark.y);
        var length = definition.segmentLength * mark.lengthScale;
        var thickness = definition.segmentThickness * mark.thicknessScale;

        var wornGrass = Color.Lerp(grassColor, roadColor, 0.28f);
        var compactedEdge = Color.Lerp(grassColor, roadColor, 0.18f);
        var exposedSoil = Color.Lerp(grassColor, roadColor, 0.72f);

        DrawCapsule(pixels, center, length * 1.3f, thickness * 1.65f, mark.rotationDeg, compactedEdge, mark.opacity * 0.22f, 0.36f);
        DrawCapsule(pixels, center, length * 1.06f, thickness * 1.08f, mark.rotationDeg, wornGrass, mark.opacity * 0.3f, 0.26f);
        if (mark.soilExposure > 0.01f)
        {
            DrawCapsule(pixels, center, length * 0.78f, thickness * 0.5f, mark.rotationDeg, exposedSoil, mark.opacity * mark.soilExposure * 0.42f, 0.18f);
        }
    }

    private static void DrawProps(Color32[] pixels, PlacementPropInstance[] props, string id, RenderContext context)
    {
        if (props == null)
        {
            return;
        }

        var definition = context.GetDefinition(id);
        var color = context.GetColor(id);
        foreach (var prop in props)
        {
            if (prop == null || !string.Equals(prop.id, id, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            switch (id)
            {
                case "tree":
                    DrawTree(pixels, prop, definition, color);
                    break;
                case "rock":
                    DrawRock(pixels, prop, definition, color);
                    break;
                case "bush":
                    DrawBush(pixels, prop, definition, color);
                    break;
                case "mushroom":
                    DrawMushroom(pixels, prop, definition, color);
                    break;
            }
        }
    }

    private static void DrawTree(Color32[] pixels, PlacementPropInstance prop, PlacementElementDefinition definition, Color color)
    {
        var radius = definition.baseRadius * prop.radiusScale;
        var center = new Vector2(prop.x, prop.y);
        DrawFeatheredEllipse(pixels, center + new Vector2(6f, 8f), radius * 1.06f, radius * 0.82f, prop.rotationDeg, Color.black, prop.opacity * 0.12f, 0f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(-0.48f * radius, -0.1f * radius), prop.rotationDeg), radius * 0.8f, radius * 0.74f, prop.rotationDeg + 12f, Multiply(color, 0.96f), prop.opacity * 0.76f, 0.16f, 0.05f, 0.04f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.36f * radius, -0.28f * radius), prop.rotationDeg), radius * 0.72f, radius * 0.68f, prop.rotationDeg - 9f, Multiply(color, 1.04f), prop.opacity * 0.72f, 0.18f, 0.04f, 0.04f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.08f * radius, 0.32f * radius), prop.rotationDeg), radius * 0.77f, radius * 0.74f, prop.rotationDeg + 25f, Multiply(color, 0.9f), prop.opacity * 0.69f, 0.14f, 0.06f, 0.04f);
        DrawFeatheredEllipse(pixels, center + new Vector2(-4f, -5f), radius * 0.38f, radius * 0.34f, 0f, Multiply(color, 1.12f), prop.opacity * 0.12f, 0.2f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center, radius * 0.18f, radius * 0.18f, 0f, new Color(0.33f, 0.24f, 0.15f, 1f), prop.opacity * 0.28f, 0.05f, 0.04f, 0f);
    }

    private static void DrawRock(Color32[] pixels, PlacementPropInstance prop, PlacementElementDefinition definition, Color color)
    {
        var radius = definition.baseRadius * prop.radiusScale;
        var center = new Vector2(prop.x, prop.y);
        DrawFeatheredEllipse(pixels, center + new Vector2(4f, 5f), radius * 0.95f, radius * 0.68f, prop.rotationDeg, Color.black, prop.opacity * 0.12f, 0f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(-0.16f * radius, 0.02f * radius), prop.rotationDeg), radius * 0.84f, radius * 0.62f, prop.rotationDeg + 18f, Multiply(color, 0.96f), prop.opacity * 0.78f, 0.08f, 0.08f, 0.03f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.24f * radius, -0.18f * radius), prop.rotationDeg), radius * 0.58f, radius * 0.48f, prop.rotationDeg - 27f, Multiply(color, 1.04f), prop.opacity * 0.74f, 0.12f, 0.08f, 0.04f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(-0.08f * radius, -0.22f * radius), prop.rotationDeg), radius * 0.32f, radius * 0.24f, prop.rotationDeg - 5f, Multiply(color, 1.16f), prop.opacity * 0.18f, 0.2f, 0f, 0f);
    }

    private static void DrawBush(Color32[] pixels, PlacementPropInstance prop, PlacementElementDefinition definition, Color color)
    {
        var radius = definition.baseRadius * prop.radiusScale;
        var center = new Vector2(prop.x, prop.y);
        DrawFeatheredEllipse(pixels, center + new Vector2(4f, 4f), radius * 1.02f, radius * 0.74f, prop.rotationDeg, Color.black, prop.opacity * 0.08f, 0f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(-0.32f * radius, 0.05f * radius), prop.rotationDeg), radius * 0.55f, radius * 0.48f, prop.rotationDeg, Multiply(color, 0.94f), prop.opacity * 0.65f, 0.16f, 0.04f, 0.04f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.28f * radius, -0.18f * radius), prop.rotationDeg), radius * 0.48f, radius * 0.45f, prop.rotationDeg + 13f, Multiply(color, 1.02f), prop.opacity * 0.66f, 0.18f, 0.03f, 0.04f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.12f * radius, 0.26f * radius), prop.rotationDeg), radius * 0.52f, radius * 0.48f, prop.rotationDeg - 10f, Multiply(color, 0.88f), prop.opacity * 0.66f, 0.14f, 0.05f, 0.04f);
        DrawFeatheredEllipse(pixels, center + new Vector2(-3f, -3f), radius * 0.24f, radius * 0.2f, 0f, Multiply(color, 1.12f), prop.opacity * 0.16f, 0.25f, 0f, 0f);
    }

    private static void DrawMushroom(Color32[] pixels, PlacementPropInstance prop, PlacementElementDefinition definition, Color color)
    {
        var radius = definition.baseRadius * prop.radiusScale;
        var center = new Vector2(prop.x, prop.y);
        DrawFeatheredEllipse(pixels, center + new Vector2(1.5f, 2f), radius * 1.1f, radius * 0.7f, prop.rotationDeg, Color.black, prop.opacity * 0.08f, 0f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + new Vector2(0f, 1.2f), radius * 0.45f, radius * 0.35f, prop.rotationDeg, new Color(0.92f, 0.89f, 0.78f, 1f), prop.opacity * 0.26f, 0.08f, 0.02f, 0f);
        DrawFeatheredEllipse(pixels, center + new Vector2(0f, -0.8f), radius * 0.95f, radius * 0.72f, prop.rotationDeg, color, prop.opacity * 0.88f, 0.18f, 0.04f, 0.02f);
        DrawFeatheredEllipse(pixels, center + new Vector2(-1.2f, -1.8f), radius * 0.52f, radius * 0.28f, prop.rotationDeg, Multiply(color, 1.12f), prop.opacity * 0.14f, 0.2f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(-0.24f * radius, -0.12f * radius), prop.rotationDeg), radius * 0.14f, radius * 0.14f, 0f, Color.white, prop.opacity * 0.35f, 0.05f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.18f * radius, -0.06f * radius), prop.rotationDeg), radius * 0.12f, radius * 0.12f, 0f, Color.white, prop.opacity * 0.28f, 0.05f, 0f, 0f);
        DrawFeatheredEllipse(pixels, center + RotateVector(new Vector2(0.02f * radius, -0.28f * radius), prop.rotationDeg), radius * 0.1f, radius * 0.1f, 0f, Color.white, prop.opacity * 0.3f, 0.05f, 0f, 0f);
    }

    private static void DrawFenceSegment(Color32[] pixels, PlacementFenceSegment segment, PlacementElementDefinition definition, Color color)
    {
        var center = new Vector2(segment.x, segment.y);
        var length = definition.segmentLength * segment.lengthScale;
        var thickness = definition.segmentThickness;
        var radians = segment.rotationDeg * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        var perpendicular = new Vector2(-direction.y, direction.x);
        var railOffset = thickness * 0.42f;
        var railThickness = thickness * 0.24f;

        DrawCapsule(pixels, center + new Vector2(2f, 3f), length, thickness * 1.35f, segment.rotationDeg, Color.black, segment.opacity * 0.08f, 0.18f);
        DrawCapsule(pixels, center + perpendicular * railOffset, length, railThickness, segment.rotationDeg, Multiply(color, 1.05f), segment.opacity * 0.94f, 0.15f);
        DrawCapsule(pixels, center - perpendicular * railOffset, length, railThickness, segment.rotationDeg, Multiply(color, 0.9f), segment.opacity * 0.9f, 0.15f);

        var halfLength = length * 0.5f;
        DrawFeatheredEllipse(pixels, center - direction * halfLength, thickness * 0.55f, thickness * 0.55f, 0f, Multiply(color, 0.78f), segment.opacity, 0.08f, 0.08f, 0.03f);
        DrawFeatheredEllipse(pixels, center + direction * halfLength, thickness * 0.55f, thickness * 0.55f, 0f, Multiply(color, 0.78f), segment.opacity, 0.08f, 0.08f, 0.03f);
    }

    private static void DrawPebbleSpeckles(Color32[] pixels, Vector2 center, float radiusX, float radiusY, float rotationDeg, Color color, float opacity)
    {
        var samples = Mathf.Clamp(Mathf.RoundToInt(radiusX * radiusY / 240f), 8, 26);
        var seedX = Mathf.RoundToInt(center.x * 10f);
        var seedY = Mathf.RoundToInt(center.y * 10f);
        for (var index = 0; index < samples; index++)
        {
            var angle = Hash01(seedX, seedY, 110 + index) * Mathf.PI * 2f;
            var radius = Mathf.Sqrt(Hash01(seedX, seedY, 210 + index));
            var local = new Vector2(Mathf.Cos(angle) * radiusX * 0.76f * radius, Mathf.Sin(angle) * radiusY * 0.76f * radius);
            var position = center + RotateVector(local, rotationDeg);
            var speckRadius = 0.9f + Hash01(seedX, seedY, 310 + index) * 1.8f;
            DrawFeatheredEllipse(pixels, position, speckRadius, speckRadius * 0.82f, rotationDeg + HashSigned(seedX, seedY, 410 + index) * 28f, color, opacity * (0.4f + Hash01(seedX, seedY, 510 + index) * 0.5f), 0.08f, 0.04f, 0f);
        }
    }

    private static void DrawFeatheredEllipse(Color32[] pixels, Vector2 center, float radiusX, float radiusY, float rotationDeg, Color color, float opacity, float highlight, float edgeDarken, float noiseStrength)
    {
        if (opacity <= 0f || radiusX <= 0f || radiusY <= 0f)
        {
            return;
        }

        var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - radiusX - 3f));
        var maxX = Mathf.Min(PlacementEvaluationPaths.CanvasWidth - 1, Mathf.CeilToInt(center.x + radiusX + 3f));
        var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - radiusY - 3f));
        var maxY = Mathf.Min(PlacementEvaluationPaths.CanvasHeight - 1, Mathf.CeilToInt(center.y + radiusY + 3f));
        var radians = rotationDeg * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = x + 0.5f - center.x;
                var dy = y + 0.5f - center.y;
                var localX = cos * dx + sin * dy;
                var localY = -sin * dx + cos * dy;
                var normalized = Mathf.Sqrt((localX * localX) / (radiusX * radiusX) + (localY * localY) / (radiusY * radiusY));
                if (normalized > 1f)
                {
                    continue;
                }

                var alpha = normalized <= 0.72f ? 1f : Mathf.InverseLerp(1f, 0.72f, normalized);
                alpha = Smooth(alpha) * opacity;
                var highlightFactor = 1f + highlight * (1f - normalized);
                var edgeFactor = 1f - edgeDarken * Mathf.SmoothStep(0.72f, 1f, normalized);
                var noiseFactor = 1f + noiseStrength * HashSigned(x, y, Mathf.RoundToInt(center.x + center.y));
                BlendPixel(pixels, x, y, Multiply(color, highlightFactor * edgeFactor * noiseFactor), alpha);
            }
        }
    }

    private static void DrawCapsule(Color32[] pixels, Vector2 center, float length, float thickness, float rotationDeg, Color color, float opacity, float feather)
    {
        if (opacity <= 0f || length <= 0f || thickness <= 0f)
        {
            return;
        }

        var radius = thickness * 0.5f;
        var radians = rotationDeg * Mathf.Deg2Rad;
        var direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        var start = center - direction * (length * 0.5f);
        var end = center + direction * (length * 0.5f);
        var minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.x, end.x) - radius - 3f));
        var maxX = Mathf.Min(PlacementEvaluationPaths.CanvasWidth - 1, Mathf.CeilToInt(Mathf.Max(start.x, end.x) + radius + 3f));
        var minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(start.y, end.y) - radius - 3f));
        var maxY = Mathf.Min(PlacementEvaluationPaths.CanvasHeight - 1, Mathf.CeilToInt(Mathf.Max(start.y, end.y) + radius + 3f));

        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var distance = DistancePointToSegment(new Vector2(x + 0.5f, y + 0.5f), start, end);
                if (distance > radius)
                {
                    continue;
                }

                var alpha = Mathf.InverseLerp(radius, radius * Mathf.Clamp01(1f - feather), distance);
                alpha = Smooth(alpha) * opacity;
                var centerFactor = 1f + 0.08f * Mathf.InverseLerp(radius, 0f, distance);
                BlendPixel(pixels, x, y, Multiply(color, centerFactor), alpha);
            }
        }
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var delta = end - start;
        var lengthSquared = delta.sqrMagnitude;
        if (lengthSquared <= Mathf.Epsilon)
        {
            return Vector2.Distance(point, start);
        }

        var t = Mathf.Clamp01(Vector2.Dot(point - start, delta) / lengthSquared);
        return Vector2.Distance(point, start + delta * t);
    }

    private static void BlendPixel(Color32[] pixels, int x, int y, Color source, float alpha)
    {
        if (alpha <= 0f)
        {
            return;
        }

        var index = y * PlacementEvaluationPaths.CanvasWidth + x;
        var destination = (Color)pixels[index];
        var blended = Color.Lerp(destination, source, Mathf.Clamp01(alpha));
        blended.a = 1f;
        pixels[index] = blended;
    }

    private static float Smooth(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static Color Multiply(Color color, float factor)
    {
        factor = Mathf.Max(0f, factor);
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            1f);
    }

    private static Vector2 RotateVector(Vector2 vector, float rotationDeg)
    {
        var radians = rotationDeg * Mathf.Deg2Rad;
        var cos = Mathf.Cos(radians);
        var sin = Mathf.Sin(radians);
        return new Vector2(cos * vector.x - sin * vector.y, sin * vector.x + cos * vector.y);
    }

    private static float Hash01(int a, int b, int c)
    {
        unchecked
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)a) * 16777619u;
            hash = (hash ^ (uint)b) * 16777619u;
            hash = (hash ^ (uint)c) * 16777619u;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float HashSigned(int a, int b, int c)
    {
        return Hash01(a, b, c) * 2f - 1f;
    }
}

internal static class PlacementProceduralSceneBuilder
{
    public static PlacementResolvedScene Build(PlacementElementCatalog catalog, PlacementLayoutScene layout)
    {
        var state = new BuilderState(catalog, layout);
        state.GenerateAccentGrass();
        state.GeneratePonds();
        state.GenerateTrails();
        state.GenerateGroves();
        state.GenerateScatterZones();
        state.GenerateFenceLines();
        return state.Build();
    }

    private sealed class BuilderState
    {
        private readonly Dictionary<string, PlacementElementDefinition> definitions;
        private readonly List<PlacementGroundPatch> groundPatches = new();
        private readonly List<PlacementTrailMark> trailMarks = new();
        private readonly List<PlacementPropInstance> props = new();
        private readonly List<PlacementFenceSegment> fenceSegments = new();
        private readonly PlacementLayoutScene layout;
        private readonly System.Random random;

        public BuilderState(PlacementElementCatalog catalog, PlacementLayoutScene layout)
        {
            definitions = new Dictionary<string, PlacementElementDefinition>(StringComparer.OrdinalIgnoreCase);
            foreach (var definition in catalog.elements)
            {
                definitions[definition.id] = definition;
            }

            this.layout = layout;
            random = new System.Random(layout.seed);
        }

        public PlacementResolvedScene Build()
        {
            return new PlacementResolvedScene
            {
                groundPatches = groundPatches.ToArray(),
                trailMarks = trailMarks.ToArray(),
                props = props.ToArray(),
                fenceSegments = fenceSegments.ToArray()
            };
        }

        public void GenerateAccentGrass()
        {
            var meadow = layout.meadow ?? new PlacementMeadowSettings();
            var desiredCount = Mathf.Max(0, meadow.accentGrassCount);
            var attempts = Mathf.Max(desiredCount * 8, 64);
            for (var index = 0; index < attempts && desiredCount > 0; index++)
            {
                var x = RandomRange(28f, PlacementEvaluationPaths.CanvasWidth - 28f);
                var y = RandomRange(28f, PlacementEvaluationPaths.CanvasHeight - 28f);
                if (IsInsideOpenCenter(x, y, meadow) && Random01() < 0.82f)
                {
                    continue;
                }

                groundPatches.Add(new PlacementGroundPatch
                {
                    id = "grass",
                    x = x,
                    y = y,
                    radiusScale = RandomRange(0.54f, 0.84f),
                    aspectX = RandomRange(0.86f, 1.18f),
                    aspectY = RandomRange(0.82f, 1.12f),
                    rotationDeg = meadow.windAngleDeg + RandomRange(-18f, 18f),
                    opacity = RandomRange(0.26f, 0.5f)
                });

                desiredCount--;
            }
        }

        public void GeneratePonds()
        {
            foreach (var pond in layout.ponds)
            {
                if (pond == null)
                {
                    continue;
                }

                EmitPondCluster(pond.center.x, pond.center.y, pond.radiusX, pond.radiusY, pond.rotationDeg, pond.sandWidth, pond.pebbleWidth, pond.irregularity, 1f);

                foreach (var lobe in pond.lobes)
                {
                    if (lobe == null)
                    {
                        continue;
                    }

                    var lobeAngle = (pond.rotationDeg + lobe.angleDeg) * Mathf.Deg2Rad;
                    var lobeCenterX = pond.center.x + Mathf.Cos(lobeAngle) * lobe.distance;
                    var lobeCenterY = pond.center.y + Mathf.Sin(lobeAngle) * lobe.distance;
                    EmitPondCluster(
                        lobeCenterX,
                        lobeCenterY,
                        pond.radiusX * Mathf.Max(0.18f, lobe.radiusXScale),
                        pond.radiusY * Mathf.Max(0.18f, lobe.radiusYScale),
                        pond.rotationDeg + lobe.rotationDeg,
                        pond.sandWidth * 0.9f,
                        pond.pebbleWidth * 0.86f,
                        pond.irregularity,
                        0.72f);
                }
            }
        }

        public void GenerateTrails()
        {
            foreach (var trail in layout.trails)
            {
                if (trail == null || trail.points == null || trail.points.Length < 2)
                {
                    continue;
                }

                var points = ToVectorArray(trail.points);
                var totalLength = GetPolylineLength(points);
                var sampleCount = Mathf.Clamp(Mathf.RoundToInt(totalLength / 18f * Mathf.Lerp(0.92f, 1.34f, Mathf.Clamp01(trail.traffic))), 8, 96);
                EmitTrailPass(points, trail, sampleCount, 0f, 1f);

                if (trail.braid > 0.05f)
                {
                    var offset = 8f * trail.width * Mathf.Lerp(0.7f, 1.25f, Mathf.Clamp01(trail.braid));
                    EmitTrailPass(points, trail, Mathf.Max(6, Mathf.RoundToInt(sampleCount * 0.78f)), offset, 0.58f);
                }

                if (trail.braid > 0.16f)
                {
                    var offset = -7f * trail.width * Mathf.Lerp(0.68f, 1.18f, Mathf.Clamp01(trail.braid));
                    EmitTrailPass(points, trail, Mathf.Max(6, Mathf.RoundToInt(sampleCount * 0.68f)), offset, 0.42f);
                }

                EmitTrailSoil(trail, points);
            }
        }

        public void GenerateGroves()
        {
            foreach (var grove in layout.groves)
            {
                if (grove == null)
                {
                    continue;
                }

                EmitGroveProps(grove, "tree", grove.treeCount, grove.innerClear, grove.treeEdgeBias, 0.84f, 1.16f, 0.62f, 0.86f);
                EmitGroveProps(grove, "bush", grove.bushCount, grove.innerClear * 0.3f, 0.44f, 0.8f, 1.08f, 0.56f, 0.84f);
                EmitGroveProps(grove, "mushroom", grove.mushroomCount, 0f, 0.24f, 0.82f, 1.18f, 0.68f, 0.92f);
            }
        }

        public void GenerateScatterZones()
        {
            foreach (var zone in layout.scatterZones)
            {
                if (zone == null)
                {
                    continue;
                }

                for (var index = 0; index < Mathf.Max(0, zone.count); index++)
                {
                    var radius = Mathf.Lerp(Mathf.Clamp01(zone.innerRadius), 1f, Mathf.Sqrt(Random01()));
                    var angle = RandomRange(0f, Mathf.PI * 2f);
                    var local = new Vector2(Mathf.Cos(angle) * zone.radiusX * radius, Mathf.Sin(angle) * zone.radiusY * radius);
                    var point = RotatePoint(local, zone.rotationDeg) + new Vector2(zone.center.x, zone.center.y);
                    props.Add(new PlacementPropInstance
                    {
                        id = zone.kind,
                        x = point.x + RandomRange(-2.5f, 2.5f),
                        y = point.y + RandomRange(-2.5f, 2.5f),
                        radiusScale = RandomRange(zone.scaleMin, zone.scaleMax),
                        rotationDeg = RandomRange(-180f, 180f),
                        opacity = RandomRange(zone.opacityMin, zone.opacityMax)
                    });
                }
            }
        }

        public void GenerateFenceLines()
        {
            var fenceDefinition = GetDefinition("fence");
            foreach (var line in layout.fenceLines)
            {
                if (line == null || line.points == null || line.points.Length < 2)
                {
                    continue;
                }

                var points = ToVectorArray(line.points);
                var length = GetPolylineLength(points);
                var spacing = Mathf.Max(3.5f, fenceDefinition.segmentLength * Mathf.Max(0.32f, line.lengthScale) * 0.84f / Mathf.Max(0.1f, line.density));
                var sampleCount = Mathf.Max(2, Mathf.RoundToInt(length / spacing) + 1);
                var samples = SamplePolyline(points, sampleCount);
                foreach (var sample in samples)
                {
                    if (Random01() < line.brokenness)
                    {
                        continue;
                    }

                    fenceSegments.Add(new PlacementFenceSegment
                    {
                        x = sample.position.x + RandomRange(-1.4f, 1.4f),
                        y = sample.position.y + RandomRange(-1.1f, 1.1f),
                        rotationDeg = sample.rotationDeg + RandomRange(-6f, 6f),
                        lengthScale = line.lengthScale * RandomRange(0.86f, 1.06f),
                        opacity = line.opacity * RandomRange(0.92f, 1.04f)
                    });
                }
            }
        }

        private void EmitPondCluster(float centerX, float centerY, float radiusX, float radiusY, float rotationDeg, float sandWidth, float pebbleWidth, float irregularity, float densityScale)
        {
            var waterCount = Mathf.Clamp(Mathf.RoundToInt((radiusX * radiusY) / 780f * densityScale), 8, 40);
            var sandCount = Mathf.Clamp(Mathf.RoundToInt(waterCount * 1.25f), 10, 48);
            var pebbleCount = Mathf.Clamp(Mathf.RoundToInt(waterCount * 0.7f), 8, 24);

            EmitEllipseGround("water", centerX, centerY, radiusX, radiusY, rotationDeg, waterCount, 0f, 1f, 0.86f, 1.14f, 0.9f, 1.16f, 0.82f, 1.08f, 0.64f, 0.88f, irregularity * 8f);
            EmitRingGround("sand", centerX, centerY, radiusX + sandWidth, radiusY + sandWidth * 0.8f, rotationDeg, sandCount, 0.58f, 1f, 0.72f, 0.98f, 1.04f, 1.28f, 0.82f, 1.04f, 0.46f, 0.66f, irregularity * 10f);
            EmitRingGround("pebble", centerX, centerY, radiusX + sandWidth + pebbleWidth, radiusY + sandWidth * 0.8f + pebbleWidth * 0.82f, rotationDeg, pebbleCount, 0.78f, 1.08f, 0.58f, 0.8f, 0.84f, 1.12f, 0.84f, 1.08f, 0.42f, 0.6f, irregularity * 12f);
        }

        private void EmitTrailPass(Vector2[] points, PlacementTrailCorridor trail, int sampleCount, float lateralOffset, float weight)
        {
            var samples = SamplePolyline(points, sampleCount);
            foreach (var sample in samples)
            {
                var radians = sample.rotationDeg * Mathf.Deg2Rad;
                var perpendicular = new Vector2(-Mathf.Sin(radians), Mathf.Cos(radians));
                var wanderScale = trail.wander * 7f * trail.width;
                var offset = lateralOffset + RandomRange(-wanderScale, wanderScale);
                var position = sample.position + perpendicular * offset;

                trailMarks.Add(new PlacementTrailMark
                {
                    x = position.x,
                    y = position.y,
                    rotationDeg = sample.rotationDeg + RandomRange(-4f, 4f),
                    lengthScale = RandomRange(0.68f, 0.98f),
                    thicknessScale = trail.width * Mathf.Lerp(0.82f, 1.24f, Mathf.Clamp01(trail.traffic)) * Mathf.Lerp(0.72f, 1f, weight),
                    opacity = Mathf.Clamp01((0.22f + trail.traffic * 0.18f) * weight + RandomRange(-0.04f, 0.06f)),
                    soilExposure = Mathf.Clamp01(trail.soilExposure * Mathf.Lerp(0.68f, 1f, weight) + RandomRange(-0.06f, 0.08f))
                });
            }
        }

        private void EmitTrailSoil(PlacementTrailCorridor trail, Vector2[] points)
        {
            for (var index = 0; index < points.Length; index++)
            {
                var position = points[index];
                var isInterior = index > 0 && index < points.Length - 1;
                var rotation = 0f;
                if (isInterior)
                {
                    var delta = (points[index + 1] - points[index - 1]).normalized;
                    rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                }
                else if (index < points.Length - 1)
                {
                    var delta = (points[index + 1] - points[index]).normalized;
                    rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                }
                else if (index > 0)
                {
                    var delta = (points[index] - points[index - 1]).normalized;
                    rotation = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                }

                groundPatches.Add(new PlacementGroundPatch
                {
                    id = "soil",
                    x = position.x + RandomRange(-8f, 8f),
                    y = position.y + RandomRange(-6f, 6f),
                    radiusScale = isInterior ? RandomRange(0.56f, 0.82f) * trail.width : RandomRange(0.46f, 0.68f) * trail.width,
                    aspectX = RandomRange(1.08f, 1.42f),
                    aspectY = RandomRange(0.66f, 0.92f),
                    rotationDeg = rotation + RandomRange(-10f, 10f),
                    opacity = Mathf.Clamp01(0.08f + trail.soilExposure * (isInterior ? 0.22f : 0.12f))
                });
            }
        }

        private void EmitGroveProps(PlacementGroveZone grove, string kind, int count, float innerClear, float edgeBias, float scaleMin, float scaleMax, float opacityMin, float opacityMax)
        {
            for (var index = 0; index < Mathf.Max(0, count); index++)
            {
                var radius = SampleBiasedRadius(innerClear, edgeBias);
                var angle = RandomRange(0f, Mathf.PI * 2f);
                var local = new Vector2(Mathf.Cos(angle) * grove.radiusX * radius, Mathf.Sin(angle) * grove.radiusY * radius);
                var point = RotatePoint(local, grove.rotationDeg) + new Vector2(grove.center.x, grove.center.y);
                props.Add(new PlacementPropInstance
                {
                    id = kind,
                    x = point.x + RandomRange(-3.5f, 3.5f),
                    y = point.y + RandomRange(-3.5f, 3.5f),
                    radiusScale = RandomRange(scaleMin, scaleMax),
                    rotationDeg = RandomRange(-180f, 180f),
                    opacity = RandomRange(opacityMin, opacityMax)
                });
            }
        }

        private void EmitEllipseGround(
            string id,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            float rotationDeg,
            int count,
            float innerRadius,
            float outerRadius,
            float radiusScaleMin,
            float radiusScaleMax,
            float aspectXMin,
            float aspectXMax,
            float aspectYMin,
            float aspectYMax,
            float opacityMin,
            float opacityMax,
            float jitter)
        {
            for (var index = 0; index < count; index++)
            {
                var radius = Mathf.Lerp(innerRadius, outerRadius, Mathf.Sqrt(Random01()));
                var angle = RandomRange(0f, Mathf.PI * 2f);
                var local = new Vector2(Mathf.Cos(angle) * radiusX * radius, Mathf.Sin(angle) * radiusY * radius);
                var point = RotatePoint(local, rotationDeg) + new Vector2(centerX, centerY);
                groundPatches.Add(new PlacementGroundPatch
                {
                    id = id,
                    x = point.x + RandomRange(-jitter, jitter),
                    y = point.y + RandomRange(-jitter, jitter),
                    radiusScale = RandomRange(radiusScaleMin, radiusScaleMax),
                    aspectX = RandomRange(aspectXMin, aspectXMax),
                    aspectY = RandomRange(aspectYMin, aspectYMax),
                    rotationDeg = rotationDeg + RandomRange(-28f, 28f),
                    opacity = RandomRange(opacityMin, opacityMax)
                });
            }
        }

        private void EmitRingGround(
            string id,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            float rotationDeg,
            int count,
            float innerRadius,
            float outerRadius,
            float radiusScaleMin,
            float radiusScaleMax,
            float aspectXMin,
            float aspectXMax,
            float aspectYMin,
            float aspectYMax,
            float opacityMin,
            float opacityMax,
            float jitter)
        {
            EmitEllipseGround(id, centerX, centerY, radiusX, radiusY, rotationDeg, count, innerRadius, outerRadius, radiusScaleMin, radiusScaleMax, aspectXMin, aspectXMax, aspectYMin, aspectYMax, opacityMin, opacityMax, jitter);
        }

        private PlacementElementDefinition GetDefinition(string id)
        {
            return definitions[id];
        }

        private bool IsInsideOpenCenter(float x, float y, PlacementMeadowSettings meadow)
        {
            var normalizedX = (x - meadow.openCenterX) / Mathf.Max(1f, meadow.openCenterRadiusX);
            var normalizedY = (y - meadow.openCenterY) / Mathf.Max(1f, meadow.openCenterRadiusY);
            return normalizedX * normalizedX + normalizedY * normalizedY <= 1f;
        }

        private float SampleBiasedRadius(float innerClear, float edgeBias)
        {
            var exponent = Mathf.Lerp(1.3f, 0.55f, Mathf.Clamp01(edgeBias));
            var radius = Mathf.Pow(Random01(), exponent);
            return Mathf.Lerp(Mathf.Clamp01(innerClear), 1f, radius);
        }

        private Vector2[] ToVectorArray(PlacementPoint[] points)
        {
            var result = new Vector2[points.Length];
            for (var index = 0; index < points.Length; index++)
            {
                result[index] = new Vector2(points[index].x, points[index].y);
            }

            return result;
        }

        private float GetPolylineLength(Vector2[] points)
        {
            var length = 0f;
            for (var index = 0; index < points.Length - 1; index++)
            {
                length += Vector2.Distance(points[index], points[index + 1]);
            }

            return length;
        }

        private PolylineSample[] SamplePolyline(Vector2[] points, int sampleCount)
        {
            if (points.Length < 2)
            {
                return Array.Empty<PolylineSample>();
            }

            var segmentLengths = new float[points.Length - 1];
            var totalLength = 0f;
            for (var index = 0; index < points.Length - 1; index++)
            {
                var length = Vector2.Distance(points[index], points[index + 1]);
                segmentLengths[index] = length;
                totalLength += length;
            }

            var samples = new PolylineSample[sampleCount];
            for (var sampleIndex = 0; sampleIndex < sampleCount; sampleIndex++)
            {
                var distance = sampleCount == 1 ? 0f : totalLength * sampleIndex / (sampleCount - 1);
                var walked = 0f;
                var segmentIndex = 0;
                while (segmentIndex < segmentLengths.Length - 1 && walked + segmentLengths[segmentIndex] < distance)
                {
                    walked += segmentLengths[segmentIndex];
                    segmentIndex++;
                }

                var segmentLength = Mathf.Max(segmentLengths[segmentIndex], 0.0001f);
                var t = (distance - walked) / segmentLength;
                var start = points[segmentIndex];
                var end = points[segmentIndex + 1];
                var position = Vector2.Lerp(start, end, t);
                var delta = (end - start).normalized;
                var angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
                samples[sampleIndex] = new PolylineSample(position, angle);
            }

            return samples;
        }

        private Vector2 RotatePoint(Vector2 point, float rotationDeg)
        {
            var radians = rotationDeg * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);
            return new Vector2(cos * point.x - sin * point.y, sin * point.x + cos * point.y);
        }

        private float Random01()
        {
            return (float)random.NextDouble();
        }

        private float RandomRange(float min, float max)
        {
            return Mathf.Lerp(min, max, Random01());
        }
    }

    private readonly struct PolylineSample
    {
        public PolylineSample(Vector2 position, float rotationDeg)
        {
            this.position = position;
            this.rotationDeg = rotationDeg;
        }

        public Vector2 position { get; }
        public float rotationDeg { get; }
    }
}
