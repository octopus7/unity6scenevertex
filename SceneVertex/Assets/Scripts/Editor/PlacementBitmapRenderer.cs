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
        var pixels = new Color32[PlacementEvaluationPaths.CanvasWidth * PlacementEvaluationPaths.CanvasHeight];
        FillBackground(pixels, context.GetColor("grass"));

        DrawGroundPatches(pixels, layout.groundPatches, "grass", context);
        DrawGroundPatches(pixels, layout.groundPatches, "soil", context);
        DrawGroundPatches(pixels, layout.groundPatches, "sand", context);
        DrawGroundPatches(pixels, layout.groundPatches, "pebble", context);
        DrawGroundPatches(pixels, layout.groundPatches, "water", context);

        foreach (var segment in layout.roadSegments)
        {
            DrawRoadSegment(pixels, segment, context.GetDefinition("road"), context.GetColor("road"));
        }

        DrawProps(pixels, layout.props, "rock", context);
        DrawProps(pixels, layout.props, "bush", context);
        DrawProps(pixels, layout.props, "mushroom", context);
        DrawProps(pixels, layout.props, "tree", context);

        foreach (var segment in layout.fenceSegments)
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

    private static void FillBackground(Color32[] pixels, Color baseColor)
    {
        var center = new Vector2(PlacementEvaluationPaths.CanvasWidth * 0.5f, PlacementEvaluationPaths.CanvasHeight * 0.5f);
        for (var y = 0; y < PlacementEvaluationPaths.CanvasHeight; y++)
        {
            for (var x = 0; x < PlacementEvaluationPaths.CanvasWidth; x++)
            {
                var index = y * PlacementEvaluationPaths.CanvasWidth + x;
                var distance = Vector2.Distance(new Vector2(x, y), center);
                var vignette = Mathf.InverseLerp(540f, 80f, distance);
                var noise = HashSigned(x, y, 17);
                var gradient = 0.96f + 0.06f * vignette + noise * 0.035f;
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
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 0.93f), patch.opacity * 0.24f, 0.15f, 0.06f, 0.05f);
                    break;
                case "soil":
                    DrawFeatheredEllipse(pixels, center + new Vector2(3f, 4f), radiusX * 1.02f, radiusY * 1.02f, patch.rotationDeg, Color.black, patch.opacity * 0.06f, 0f, 0f, 0f);
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.72f, 0.08f, 0.05f, 0.04f);
                    break;
                case "sand":
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.68f, 0.22f, 0.04f, 0.03f);
                    DrawFeatheredEllipse(pixels, center + new Vector2(-4f, -3f), radiusX * 0.78f, radiusY * 0.72f, patch.rotationDeg, Multiply(color, 1.08f), patch.opacity * 0.18f, 0.2f, 0f, 0f);
                    break;
                case "pebble":
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, color, patch.opacity * 0.55f, 0.1f, 0.07f, 0.06f);
                    DrawPebbleSpeckles(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 1.12f), patch.opacity * 0.42f);
                    break;
                case "water":
                    DrawFeatheredEllipse(pixels, center + new Vector2(5f, 6f), radiusX * 1.05f, radiusY * 1.05f, patch.rotationDeg, Color.black, patch.opacity * 0.11f, 0f, 0f, 0f);
                    DrawFeatheredEllipse(pixels, center, radiusX, radiusY, patch.rotationDeg, Multiply(color, 0.95f), patch.opacity * 0.82f, 0.08f, 0.16f, 0.03f);
                    DrawFeatheredEllipse(pixels, center + new Vector2(-8f, -6f), radiusX * 0.64f, radiusY * 0.58f, patch.rotationDeg, Multiply(color, 1.18f), patch.opacity * 0.22f, 0.18f, 0f, 0f);
                    break;
            }
        }
    }

    private static void DrawRoadSegment(Color32[] pixels, PlacementRoadSegment segment, PlacementElementDefinition definition, Color color)
    {
        var center = new Vector2(segment.x, segment.y);
        var length = definition.segmentLength * segment.lengthScale;
        var thickness = definition.segmentThickness * segment.thicknessScale;
        DrawCapsule(pixels, center + new Vector2(4f, 5f), length * 1.01f, thickness * 1.05f, segment.rotationDeg, Color.black, segment.opacity * 0.08f, 0.2f);
        DrawCapsule(pixels, center, length, thickness, segment.rotationDeg, color, segment.opacity * 0.92f, 0.18f);
        DrawCapsule(pixels, center + new Vector2(-2f, -2f), length * 0.92f, thickness * 0.52f, segment.rotationDeg, Multiply(color, 1.1f), segment.opacity * 0.15f, 0.12f);
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
