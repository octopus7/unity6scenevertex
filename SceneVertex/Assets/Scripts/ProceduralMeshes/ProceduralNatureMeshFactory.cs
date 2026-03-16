using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public readonly struct ProceduralMeshDefinition
{
    public ProceduralMeshDefinition(ProceduralNaturePropCategory category, string assetName, Mesh mesh)
    {
        Category = category;
        AssetName = assetName;
        Mesh = mesh;
    }

    public ProceduralNaturePropCategory Category { get; }
    public string AssetName { get; }
    public Mesh Mesh { get; }
}

public static class ProceduralNatureMeshFactory
{
    private static readonly Color Bark = new(0.43f, 0.28f, 0.16f);
    private static readonly Color BarkDark = new(0.31f, 0.20f, 0.12f);
    private static readonly Color LeafDark = new(0.24f, 0.47f, 0.22f);
    private static readonly Color LeafMid = new(0.31f, 0.56f, 0.28f);
    private static readonly Color LeafBright = new(0.38f, 0.64f, 0.32f);
    private static readonly Color RockLight = new(0.58f, 0.59f, 0.62f);
    private static readonly Color RockMid = new(0.47f, 0.49f, 0.52f);
    private static readonly Color RockDark = new(0.36f, 0.37f, 0.41f);
    private static readonly Color FenceWood = new(0.54f, 0.37f, 0.21f);
    private static readonly Color StemGreen = new(0.29f, 0.58f, 0.24f);
    private static readonly Color PetalPink = new(0.89f, 0.41f, 0.57f);
    private static readonly Color PetalPurple = new(0.67f, 0.44f, 0.86f);
    private static readonly Color PetalYellow = new(0.94f, 0.79f, 0.20f);
    private static readonly Color PetalWhite = new(0.96f, 0.93f, 0.88f);
    private static readonly Color BushGreen = new(0.21f, 0.55f, 0.26f);
    private static readonly Color BushDark = new(0.16f, 0.42f, 0.18f);
    private static readonly Color MushroomStem = new(0.88f, 0.82f, 0.67f);
    private static readonly Color MushroomCapRed = new(0.75f, 0.24f, 0.20f);
    private static readonly Color MushroomCapBrown = new(0.56f, 0.33f, 0.21f);
    private static readonly Color MushroomCapAmber = new(0.82f, 0.54f, 0.20f);

    public static List<ProceduralMeshDefinition> CreateAllMeshes()
    {
        return new List<ProceduralMeshDefinition>
        {
            new(ProceduralNaturePropCategory.Tree, "TreeA", CreateTreeA()),
            new(ProceduralNaturePropCategory.Tree, "TreeB", CreateTreeB()),
            new(ProceduralNaturePropCategory.Tree, "TreeC", CreateTreeC()),
            new(ProceduralNaturePropCategory.Rock, "RockA", CreateRockA()),
            new(ProceduralNaturePropCategory.Rock, "RockB", CreateRockB()),
            new(ProceduralNaturePropCategory.Rock, "RockC", CreateRockC()),
            new(ProceduralNaturePropCategory.Fence, "Fence", CreateFence()),
            new(ProceduralNaturePropCategory.Flower, "FlowerA", CreateFlowerA()),
            new(ProceduralNaturePropCategory.Flower, "FlowerB", CreateFlowerB()),
            new(ProceduralNaturePropCategory.Flower, "FlowerC", CreateFlowerC()),
            new(ProceduralNaturePropCategory.Bush, "BushA", CreateBushA()),
            new(ProceduralNaturePropCategory.Bush, "BushB", CreateBushB()),
            new(ProceduralNaturePropCategory.Bush, "BushC", CreateBushC()),
            new(ProceduralNaturePropCategory.Mushroom, "MushroomA", CreateMushroomA()),
            new(ProceduralNaturePropCategory.Mushroom, "MushroomB", CreateMushroomB()),
            new(ProceduralNaturePropCategory.Mushroom, "MushroomC", CreateMushroomC())
        };
    }

    private static Mesh CreateTreeA()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 1.55f, 0.18f, 0.12f, 10, Bark);
        builder.AddCylinder(new Vector3(0f, 0.98f, 0.02f), 0.55f, 0.055f, 0.028f, 8, Quaternion.Euler(20f, 28f, 22f), BarkDark);
        builder.AddCylinder(new Vector3(0f, 1.14f, -0.02f), 0.48f, 0.045f, 0.022f, 8, Quaternion.Euler(-18f, -35f, -26f), BarkDark);
        builder.AddDeformedSphere(new Vector3(0f, 2.14f, 0f), new Vector3(0.96f, 0.78f, 0.90f), 8, 12, 0.16f, 4.2f, LeafMid);
        builder.AddDeformedSphere(new Vector3(0.42f, 1.90f, 0.06f), new Vector3(0.50f, 0.40f, 0.46f), 7, 10, 0.10f, 5.4f, LeafBright);
        builder.AddDeformedSphere(new Vector3(-0.38f, 1.86f, -0.14f), new Vector3(0.45f, 0.36f, 0.42f), 7, 10, 0.10f, 5.0f, LeafDark);
        return builder.ToMesh("ProceduralTreeA");
    }

    private static Mesh CreateTreeB()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 2.20f, 0.14f, 0.08f, 10, BarkDark);
        builder.AddCylinder(new Vector3(0f, 0.82f, 0f), 0.92f, 0.95f, 0.03f, 12, LeafDark);
        builder.AddCylinder(new Vector3(0f, 1.30f, 0f), 0.82f, 0.72f, 0.025f, 12, LeafMid);
        builder.AddCylinder(new Vector3(0f, 1.74f, 0f), 0.68f, 0.48f, 0.02f, 12, LeafBright);
        builder.AddCylinder(new Vector3(0f, 2.12f, 0f), 0.28f, 0.16f, 0f, 10, LeafBright);
        return builder.ToMesh("ProceduralTreeB");
    }

    private static Mesh CreateTreeC()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 1.68f, 0.19f, 0.11f, 10, Quaternion.Euler(5f, 10f, -7f), Bark);
        builder.AddCylinder(new Vector3(0.10f, 1.16f, -0.02f), 0.62f, 0.06f, 0.025f, 8, Quaternion.Euler(-20f, 34f, 28f), BarkDark);
        builder.AddCylinder(new Vector3(-0.04f, 1.28f, 0.05f), 0.56f, 0.05f, 0.02f, 8, Quaternion.Euler(12f, -42f, -30f), BarkDark);
        builder.AddDeformedSphere(new Vector3(0.18f, 2.08f, 0f), new Vector3(0.62f, 0.52f, 0.60f), 8, 12, 0.15f, 4.6f, LeafDark);
        builder.AddDeformedSphere(new Vector3(-0.42f, 1.96f, 0.14f), new Vector3(0.56f, 0.46f, 0.54f), 8, 12, 0.14f, 4.0f, LeafMid);
        builder.AddDeformedSphere(new Vector3(0.48f, 1.84f, -0.24f), new Vector3(0.50f, 0.40f, 0.46f), 7, 10, 0.12f, 5.2f, LeafBright);
        return builder.ToMesh("ProceduralTreeC");
    }

    private static Mesh CreateRockA()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(0f, 0.42f, 0f), new Vector3(0.86f, 0.44f, 0.72f), 9, 14, 0.22f, 6.1f, RockMid);
        builder.AddDeformedSphere(new Vector3(-0.18f, 0.32f, 0.18f), new Vector3(0.34f, 0.18f, 0.28f), 7, 10, 0.12f, 5.4f, RockDark);
        return builder.ToMesh("ProceduralRockA");
    }

    private static Mesh CreateRockB()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(0f, 0.34f, 0f), new Vector3(0.42f, 0.28f, 0.34f), 8, 12, 0.12f, 5.8f, RockDark);
        builder.AddCylinder(new Vector3(0.06f, 0.18f, -0.02f), 1.02f, 0.44f, 0.08f, 9, Quaternion.Euler(-10f, 22f, 8f), RockMid);
        builder.AddPyramid(new Vector3(0.20f, 0.96f, 0.04f), new Vector2(0.34f, 0.28f), 0.34f, Quaternion.Euler(12f, 24f, 0f), false, RockLight);
        return builder.ToMesh("ProceduralRockB");
    }

    private static Mesh CreateRockC()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(-0.30f, 0.34f, 0.12f), new Vector3(0.40f, 0.28f, 0.34f), 8, 12, 0.16f, 5.0f, RockLight);
        builder.AddDeformedSphere(new Vector3(0.18f, 0.42f, 0f), new Vector3(0.52f, 0.34f, 0.42f), 8, 12, 0.18f, 6.4f, RockMid);
        builder.AddDeformedSphere(new Vector3(0.46f, 0.26f, -0.18f), new Vector3(0.26f, 0.16f, 0.22f), 7, 10, 0.14f, 4.6f, RockDark);
        return builder.ToMesh("ProceduralRockC");
    }

    private static Mesh CreateFence()
    {
        var builder = new MeshBuilder();
        const float postWidth = 0.16f;
        const float postHeight = 1.08f;
        const float postDepth = 0.16f;
        const float capHeight = 0.18f;

        var postPositions = new[] { -1.35f, -0.45f, 0.45f, 1.35f };
        foreach (var x in postPositions)
        {
            builder.AddBox(new Vector3(x, postHeight * 0.5f, 0f), new Vector3(postWidth, postHeight, postDepth), Quaternion.identity, FenceWood);
            builder.AddPyramid(new Vector3(x, postHeight, 0f), new Vector2(postWidth, postDepth), capHeight, Quaternion.identity, false, FenceWood);
        }

        builder.AddBox(new Vector3(0f, 0.74f, 0f), new Vector3(2.98f, 0.12f, 0.12f), Quaternion.identity, FenceWood);
        builder.AddBox(new Vector3(0f, 0.42f, 0f), new Vector3(2.98f, 0.12f, 0.12f), Quaternion.identity, FenceWood);
        return builder.ToMesh("ProceduralFence");
    }

    private static Mesh CreateFlowerA()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 0.92f, 0.035f, 0.02f, 8, StemGreen);
        builder.AddDeformedSphere(new Vector3(0f, 0.95f, 0f), new Vector3(0.10f, 0.10f, 0.10f), 5, 8, 0.05f, 8.0f, PetalYellow);
        AddPetalRing(builder, new Vector3(0f, 0.92f, 0f), 6, 0.18f, 0.34f, 0.03f, 12f, PetalPink);
        builder.AddPetal(new Vector3(0f, 0.36f, 0f), Quaternion.LookRotation(new Vector3(-0.84f, 0.22f, 0.20f).normalized, Vector3.up), 0.24f, 0.11f, 0.02f, StemGreen);
        builder.AddPetal(new Vector3(0f, 0.56f, 0f), Quaternion.LookRotation(new Vector3(0.80f, 0.18f, -0.22f).normalized, Vector3.up), 0.20f, 0.10f, 0.02f, StemGreen);
        return builder.ToMesh("ProceduralFlowerA");
    }

    private static Mesh CreateFlowerB()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 0.84f, 0.04f, 0.025f, 8, StemGreen);
        builder.AddDeformedSphere(new Vector3(0f, 0.82f, 0f), new Vector3(0.08f, 0.10f, 0.08f), 5, 8, 0.03f, 7.0f, PetalYellow);

        for (var i = 0; i < 5; i++)
        {
            var angle = i * 72f;
            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var rotation = Quaternion.LookRotation((outward * 0.35f + Vector3.up).normalized, Vector3.up);
            builder.AddPetal(new Vector3(0f, 0.68f, 0f), rotation, 0.34f, 0.18f, 0.03f, PetalPurple);
        }

        builder.AddPetal(new Vector3(0f, 0.28f, 0f), Quaternion.LookRotation(new Vector3(-0.76f, 0.24f, 0.16f).normalized, Vector3.up), 0.22f, 0.10f, 0.02f, StemGreen);
        builder.AddPetal(new Vector3(0f, 0.48f, 0f), Quaternion.LookRotation(new Vector3(0.72f, 0.18f, -0.18f).normalized, Vector3.up), 0.18f, 0.09f, 0.02f, StemGreen);
        return builder.ToMesh("ProceduralFlowerB");
    }

    private static Mesh CreateFlowerC()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 1.02f, 0.032f, 0.018f, 8, StemGreen);
        builder.AddCylinder(new Vector3(0f, 0.62f, 0f), 0.30f, 0.014f, 0.008f, 6, Quaternion.Euler(-28f, 24f, 22f), StemGreen);
        builder.AddCylinder(new Vector3(0f, 0.70f, 0f), 0.26f, 0.014f, 0.008f, 6, Quaternion.Euler(24f, -34f, -18f), StemGreen);
        AddPetalRing(builder, new Vector3(0f, 1.00f, 0f), 5, 0.12f, 0.22f, 0.02f, 16f, PetalWhite);
        builder.AddDeformedSphere(new Vector3(0f, 1.02f, 0f), new Vector3(0.06f, 0.06f, 0.06f), 4, 6, 0.03f, 8.0f, PetalYellow);
        AddPetalRing(builder, new Vector3(0.16f, 0.88f, 0.22f), 4, 0.09f, 0.17f, 0.018f, 20f, PetalPink);
        AddPetalRing(builder, new Vector3(-0.14f, 0.90f, -0.18f), 4, 0.09f, 0.17f, 0.018f, 20f, PetalPurple);
        builder.AddPetal(new Vector3(0f, 0.34f, 0f), Quaternion.LookRotation(new Vector3(-0.88f, 0.20f, 0.18f).normalized, Vector3.up), 0.22f, 0.10f, 0.02f, StemGreen);
        builder.AddPetal(new Vector3(0f, 0.56f, 0f), Quaternion.LookRotation(new Vector3(0.84f, 0.20f, -0.18f).normalized, Vector3.up), 0.20f, 0.09f, 0.02f, StemGreen);
        return builder.ToMesh("ProceduralFlowerC");
    }

    private static Mesh CreateBushA()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(0f, 0.48f, 0f), new Vector3(0.68f, 0.42f, 0.62f), 8, 12, 0.18f, 5.0f, BushGreen);
        builder.AddDeformedSphere(new Vector3(-0.34f, 0.34f, 0.08f), new Vector3(0.36f, 0.26f, 0.32f), 7, 10, 0.12f, 6.4f, BushDark);
        builder.AddDeformedSphere(new Vector3(0.30f, 0.36f, -0.10f), new Vector3(0.34f, 0.24f, 0.30f), 7, 10, 0.12f, 5.8f, LeafBright);
        return builder.ToMesh("ProceduralBushA");
    }

    private static Mesh CreateBushB()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(-0.36f, 0.28f, 0.06f), new Vector3(0.36f, 0.24f, 0.30f), 7, 10, 0.12f, 5.4f, BushDark);
        builder.AddDeformedSphere(new Vector3(0f, 0.34f, 0f), new Vector3(0.52f, 0.30f, 0.46f), 8, 12, 0.15f, 5.8f, BushGreen);
        builder.AddDeformedSphere(new Vector3(0.42f, 0.30f, -0.02f), new Vector3(0.38f, 0.24f, 0.32f), 7, 10, 0.12f, 6.0f, LeafMid);
        builder.AddDeformedSphere(new Vector3(0.10f, 0.46f, 0.18f), new Vector3(0.28f, 0.18f, 0.24f), 6, 8, 0.10f, 6.8f, LeafBright);
        return builder.ToMesh("ProceduralBushB");
    }

    private static Mesh CreateBushC()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(0f, 0.30f, 0f), new Vector3(0.62f, 0.24f, 0.56f), 8, 12, 0.14f, 5.2f, BushDark);
        builder.AddDeformedSphere(new Vector3(-0.20f, 0.52f, 0.12f), new Vector3(0.42f, 0.28f, 0.36f), 7, 10, 0.14f, 6.0f, BushGreen);
        builder.AddDeformedSphere(new Vector3(0.24f, 0.58f, -0.10f), new Vector3(0.38f, 0.26f, 0.34f), 7, 10, 0.12f, 5.6f, LeafBright);
        builder.AddDeformedSphere(new Vector3(0.02f, 0.74f, 0.02f), new Vector3(0.28f, 0.18f, 0.24f), 6, 8, 0.10f, 6.6f, LeafMid);
        return builder.ToMesh("ProceduralBushC");
    }

    private static Mesh CreateMushroomA()
    {
        var builder = new MeshBuilder();
        AddMushroom(builder, Vector3.zero, 0.42f, 0.05f, new Vector3(0f, 0.48f, 0f), new Vector3(0.24f, 0.12f, 0.24f), MushroomStem, MushroomCapRed, 0.10f, 7.2f);
        return builder.ToMesh("ProceduralMushroomA");
    }

    private static Mesh CreateMushroomB()
    {
        var builder = new MeshBuilder();
        AddMushroom(builder, Vector3.zero, 0.56f, 0.04f, new Vector3(0f, 0.62f, 0f), new Vector3(0.18f, 0.18f, 0.18f), MushroomStem, MushroomCapBrown, 0.12f, 8.0f);
        return builder.ToMesh("ProceduralMushroomB");
    }

    private static Mesh CreateMushroomC()
    {
        var builder = new MeshBuilder();
        AddMushroom(builder, new Vector3(-0.12f, 0f, 0.06f), 0.34f, 0.04f, new Vector3(-0.12f, 0.40f, 0.06f), new Vector3(0.16f, 0.10f, 0.16f), MushroomStem, MushroomCapAmber, 0.08f, 6.4f);
        AddMushroom(builder, new Vector3(0.12f, 0f, -0.04f), 0.26f, 0.03f, new Vector3(0.12f, 0.30f, -0.04f), new Vector3(0.12f, 0.08f, 0.12f), MushroomStem, MushroomCapRed, 0.10f, 7.6f);
        AddMushroom(builder, new Vector3(0.02f, 0f, 0.16f), 0.20f, 0.025f, new Vector3(0.02f, 0.24f, 0.16f), new Vector3(0.10f, 0.06f, 0.10f), MushroomStem, MushroomCapBrown, 0.08f, 7.0f);
        return builder.ToMesh("ProceduralMushroomC");
    }

    private static void AddPetalRing(MeshBuilder builder, Vector3 center, int count, float radius, float length, float thickness, float tiltDegrees, Color color)
    {
        for (var i = 0; i < count; i++)
        {
            var angle = i * (360f / count);
            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var rotation = Quaternion.LookRotation((outward + Vector3.up * Mathf.Tan(tiltDegrees * Mathf.Deg2Rad)).normalized, Vector3.up);
            builder.AddPetal(center + outward * radius, rotation, length, length * 0.52f, thickness, color);
        }
    }

    private static void AddMushroom(
        MeshBuilder builder,
        Vector3 stemBase,
        float stemHeight,
        float stemRadius,
        Vector3 capCenter,
        Vector3 capRadii,
        Color stemColor,
        Color capColor,
        float wobble,
        float frequency)
    {
        builder.AddCylinder(stemBase, stemHeight, stemRadius, stemRadius * 0.72f, 8, stemColor);
        builder.AddDeformedSphere(capCenter, capRadii, 6, 10, wobble, frequency, capColor);
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();
        private readonly List<Color> colors = new();

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation, Color color)
        {
            var half = size * 0.5f;
            var right = rotation * new Vector3(half.x, 0f, 0f);
            var up = rotation * new Vector3(0f, half.y, 0f);
            var forward = rotation * new Vector3(0f, 0f, half.z);

            var v000 = center - right - up - forward;
            var v001 = center - right - up + forward;
            var v010 = center - right + up - forward;
            var v011 = center - right + up + forward;
            var v100 = center + right - up - forward;
            var v101 = center + right - up + forward;
            var v110 = center + right + up - forward;
            var v111 = center + right + up + forward;

            AddQuadFacing(v001, v101, v111, v011, rotation * Vector3.forward, color);
            AddQuadFacing(v100, v000, v010, v110, rotation * Vector3.back, color);
            AddQuadFacing(v000, v001, v011, v010, rotation * Vector3.left, color);
            AddQuadFacing(v101, v100, v110, v111, rotation * Vector3.right, color);
            AddQuadFacing(v011, v111, v110, v010, rotation * Vector3.up, color);
            AddQuadFacing(v000, v100, v101, v001, rotation * Vector3.down, color);
        }

        public void AddPyramid(Vector3 baseCenter, Vector2 baseSize, float height, Quaternion rotation, bool includeBase, Color color)
        {
            var matrix = Matrix4x4.TRS(baseCenter, rotation, Vector3.one);
            var halfX = baseSize.x * 0.5f;
            var halfZ = baseSize.y * 0.5f;

            var a = matrix.MultiplyPoint3x4(new Vector3(-halfX, 0f, -halfZ));
            var b = matrix.MultiplyPoint3x4(new Vector3(halfX, 0f, -halfZ));
            var c = matrix.MultiplyPoint3x4(new Vector3(halfX, 0f, halfZ));
            var d = matrix.MultiplyPoint3x4(new Vector3(-halfX, 0f, halfZ));
            var apex = matrix.MultiplyPoint3x4(new Vector3(0f, height, 0f));

            AddTriangleFacing(a, b, apex, ((a + b + apex) / 3f) - baseCenter, color);
            AddTriangleFacing(b, c, apex, ((b + c + apex) / 3f) - baseCenter, color);
            AddTriangleFacing(c, d, apex, ((c + d + apex) / 3f) - baseCenter, color);
            AddTriangleFacing(d, a, apex, ((d + a + apex) / 3f) - baseCenter, color);

            if (includeBase)
            {
                AddQuadFacing(a, d, c, b, rotation * Vector3.down, color);
            }
        }

        public void AddCylinder(Vector3 baseCenter, float height, float bottomRadius, float topRadius, int sides, Color color)
        {
            AddCylinder(baseCenter, height, bottomRadius, topRadius, sides, Quaternion.identity, color);
        }

        public void AddCylinder(Vector3 baseCenter, float height, float bottomRadius, float topRadius, int sides, Quaternion rotation, Color color)
        {
            var matrix = Matrix4x4.TRS(baseCenter, rotation, Vector3.one);
            var topCenter = matrix.MultiplyPoint3x4(new Vector3(0f, height, 0f));
            var bottomCenter = matrix.MultiplyPoint3x4(Vector3.zero);

            for (var i = 0; i < sides; i++)
            {
                var t0 = (float)i / sides;
                var t1 = (float)(i + 1) / sides;
                var a0 = t0 * Mathf.PI * 2f;
                var a1 = t1 * Mathf.PI * 2f;

                var b0 = matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius));
                var b1 = matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius));
                var top0 = matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(a0) * topRadius, height, Mathf.Sin(a0) * topRadius));
                var top1 = matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(a1) * topRadius, height, Mathf.Sin(a1) * topRadius));

                var sideHint = rotation * new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                AddQuadFacing(b0, b1, top1, top0, sideHint, color);
                AddTriangleFacing(bottomCenter, b1, b0, rotation * Vector3.down, color);
                AddTriangleFacing(topCenter, top0, top1, rotation * Vector3.up, color);
            }
        }

        public void AddDeformedSphere(Vector3 center, Vector3 radii, int latSegments, int lonSegments, float amplitude, float frequency, Color color)
        {
            for (var lat = 0; lat < latSegments; lat++)
            {
                var phi0 = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)lat / latSegments);
                var phi1 = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, (float)(lat + 1) / latSegments);

                for (var lon = 0; lon < lonSegments; lon++)
                {
                    var theta0 = Mathf.Lerp(0f, Mathf.PI * 2f, (float)lon / lonSegments);
                    var theta1 = Mathf.Lerp(0f, Mathf.PI * 2f, (float)(lon + 1) / lonSegments);

                    var p00 = EvaluateSpherePoint(center, radii, phi0, theta0, amplitude, frequency);
                    var p01 = EvaluateSpherePoint(center, radii, phi0, theta1, amplitude, frequency);
                    var p10 = EvaluateSpherePoint(center, radii, phi1, theta0, amplitude, frequency);
                    var p11 = EvaluateSpherePoint(center, radii, phi1, theta1, amplitude, frequency);

                    if (lat == 0)
                    {
                        AddTriangleFacing(p00, p11, p10, ((p00 + p10 + p11) / 3f) - center, color);
                    }
                    else if (lat == latSegments - 1)
                    {
                        AddTriangleFacing(p00, p01, p10, ((p00 + p01 + p10) / 3f) - center, color);
                    }
                    else
                    {
                        AddQuadFacing(p00, p01, p11, p10, ((p00 + p01 + p10 + p11) * 0.25f) - center, color);
                    }
                }
            }
        }

        public void AddPetal(Vector3 position, Quaternion rotation, float length, float width, float thickness, Color color)
        {
            var matrix = Matrix4x4.TRS(position, rotation, Vector3.one);

            var baseFront = matrix.MultiplyPoint3x4(new Vector3(0f, thickness * 0.5f, 0f));
            var leftFront = matrix.MultiplyPoint3x4(new Vector3(-width * 0.5f, thickness * 0.5f, length * 0.45f));
            var tipFront = matrix.MultiplyPoint3x4(new Vector3(0f, thickness * 0.18f, length));
            var rightFront = matrix.MultiplyPoint3x4(new Vector3(width * 0.5f, thickness * 0.5f, length * 0.45f));

            var baseBack = matrix.MultiplyPoint3x4(new Vector3(0f, -thickness * 0.5f, 0f));
            var leftBack = matrix.MultiplyPoint3x4(new Vector3(-width * 0.5f, -thickness * 0.5f, length * 0.45f));
            var tipBack = matrix.MultiplyPoint3x4(new Vector3(0f, -thickness * 0.18f, length));
            var rightBack = matrix.MultiplyPoint3x4(new Vector3(width * 0.5f, -thickness * 0.5f, length * 0.45f));

            var frontHint = rotation * Vector3.up;
            AddTriangleFacing(baseFront, leftFront, tipFront, frontHint, color);
            AddTriangleFacing(baseFront, tipFront, rightFront, frontHint, color);
            AddTriangleFacing(baseBack, tipBack, leftBack, -frontHint, color);
            AddTriangleFacing(baseBack, rightBack, tipBack, -frontHint, color);

            AddQuadFacing(baseFront, leftFront, leftBack, baseBack, MidpointHint(position, baseFront, leftFront, leftBack, baseBack), color);
            AddQuadFacing(leftFront, tipFront, tipBack, leftBack, MidpointHint(position, leftFront, tipFront, tipBack, leftBack), color);
            AddQuadFacing(tipFront, rightFront, rightBack, tipBack, MidpointHint(position, tipFront, rightFront, rightBack, tipBack), color);
            AddQuadFacing(rightFront, baseFront, baseBack, rightBack, MidpointHint(position, rightFront, baseFront, baseBack, rightBack), color);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetColors(colors);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 EvaluateSpherePoint(Vector3 center, Vector3 radii, float phi, float theta, float amplitude, float frequency)
        {
            var cosPhi = Mathf.Cos(phi);
            var direction = new Vector3(
                cosPhi * Mathf.Cos(theta),
                Mathf.Sin(phi),
                cosPhi * Mathf.Sin(theta));

            var wobble =
                Mathf.Sin((direction.x + 0.31f) * frequency) +
                Mathf.Sin((direction.y - 0.17f) * frequency * 1.31f) +
                Mathf.Cos((direction.z + 0.73f) * frequency * 0.93f);

            var scale = 1f + amplitude * wobble / 3f;
            return center + Vector3.Scale(direction * scale, radii);
        }

        private static Vector3 MidpointHint(Vector3 origin, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            return ((a + b + c + d) * 0.25f) - origin;
        }

        private void AddQuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardHint, Color color)
        {
            AddTriangleFacing(a, b, c, outwardHint, color);
            AddTriangleFacing(a, c, d, outwardHint, color);
        }

        private void AddTriangleFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 outwardHint, Color color)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outwardHint) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
