using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class ProceduralNatureMeshFactory
{
    public static Mesh CreateTreeMesh()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 1.25f, 0.22f, 0.15f, 12);
        builder.AddDeformedSphere(new Vector3(0f, 1.72f, 0f), new Vector3(0.72f, 0.68f, 0.72f), 8, 12, 0.16f, 4.3f);
        builder.AddDeformedSphere(new Vector3(0.18f, 1.44f, 0.12f), new Vector3(0.34f, 0.28f, 0.34f), 6, 10, 0.10f, 5.6f);
        return builder.ToMesh("ProceduralTree");
    }

    public static Mesh CreateRockMesh()
    {
        var builder = new MeshBuilder();
        builder.AddDeformedSphere(new Vector3(0f, 0.42f, 0f), new Vector3(0.78f, 0.45f, 0.68f), 9, 14, 0.22f, 6.1f);
        builder.AddDeformedSphere(new Vector3(0.28f, 0.38f, -0.12f), new Vector3(0.34f, 0.22f, 0.30f), 7, 12, 0.12f, 7.3f);
        return builder.ToMesh("ProceduralRock");
    }

    public static Mesh CreateFenceMesh()
    {
        var builder = new MeshBuilder();
        const float postWidth = 0.16f;
        const float postHeight = 1.08f;
        const float postDepth = 0.16f;
        const float capHeight = 0.18f;

        var postPositions = new[] { -1.35f, -0.45f, 0.45f, 1.35f };
        foreach (var x in postPositions)
        {
            builder.AddBox(new Vector3(x, postHeight * 0.5f, 0f), new Vector3(postWidth, postHeight, postDepth), Quaternion.identity);
            builder.AddPyramid(new Vector3(x, postHeight, 0f), new Vector2(postWidth, postDepth), capHeight, Quaternion.identity, false);
        }

        builder.AddBox(new Vector3(0f, 0.74f, 0f), new Vector3(2.98f, 0.12f, 0.12f), Quaternion.identity);
        builder.AddBox(new Vector3(0f, 0.42f, 0f), new Vector3(2.98f, 0.12f, 0.12f), Quaternion.identity);
        return builder.ToMesh("ProceduralFence");
    }

    public static Mesh CreateFlowerMesh()
    {
        var builder = new MeshBuilder();
        builder.AddCylinder(Vector3.zero, 0.92f, 0.035f, 0.02f, 8);
        builder.AddDeformedSphere(new Vector3(0f, 0.95f, 0f), new Vector3(0.11f, 0.11f, 0.11f), 5, 8, 0.05f, 8.0f);

        var blossomCenter = new Vector3(0f, 0.92f, 0f);
        for (var i = 0; i < 6; i++)
        {
            var angle = i * 60f;
            var outward = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
            var rotation = Quaternion.LookRotation((outward + Vector3.up * 0.18f).normalized, Vector3.up);
            builder.AddPetal(blossomCenter, rotation, 0.34f, 0.18f, 0.03f);
        }

        var leftLeafRotation = Quaternion.LookRotation(new Vector3(-0.85f, 0.25f, 0.18f).normalized, Vector3.up);
        var rightLeafRotation = Quaternion.LookRotation(new Vector3(0.82f, 0.18f, -0.22f).normalized, Vector3.up);
        builder.AddPetal(new Vector3(0f, 0.36f, 0f), leftLeafRotation, 0.26f, 0.12f, 0.02f);
        builder.AddPetal(new Vector3(0f, 0.56f, 0f), rightLeafRotation, 0.22f, 0.10f, 0.02f);
        return builder.ToMesh("ProceduralFlower");
    }

    public static Dictionary<string, Mesh> CreateAllMeshes()
    {
        return new Dictionary<string, Mesh>
        {
            { "Tree", CreateTreeMesh() },
            { "Rock", CreateRockMesh() },
            { "Fence", CreateFenceMesh() },
            { "Flower", CreateFlowerMesh() }
        };
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new();
        private readonly List<int> triangles = new();

        public void AddBox(Vector3 center, Vector3 size, Quaternion rotation)
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

            AddQuadFacing(v001, v101, v111, v011, rotation * Vector3.forward);
            AddQuadFacing(v100, v000, v010, v110, rotation * Vector3.back);
            AddQuadFacing(v000, v001, v011, v010, rotation * Vector3.left);
            AddQuadFacing(v101, v100, v110, v111, rotation * Vector3.right);
            AddQuadFacing(v011, v111, v110, v010, rotation * Vector3.up);
            AddQuadFacing(v000, v100, v101, v001, rotation * Vector3.down);
        }

        public void AddPyramid(Vector3 baseCenter, Vector2 baseSize, float height, Quaternion rotation, bool includeBase)
        {
            var matrix = Matrix4x4.TRS(baseCenter, rotation, Vector3.one);
            var halfX = baseSize.x * 0.5f;
            var halfZ = baseSize.y * 0.5f;

            var a = matrix.MultiplyPoint3x4(new Vector3(-halfX, 0f, -halfZ));
            var b = matrix.MultiplyPoint3x4(new Vector3(halfX, 0f, -halfZ));
            var c = matrix.MultiplyPoint3x4(new Vector3(halfX, 0f, halfZ));
            var d = matrix.MultiplyPoint3x4(new Vector3(-halfX, 0f, halfZ));
            var apex = matrix.MultiplyPoint3x4(new Vector3(0f, height, 0f));

            AddTriangleFacing(a, b, apex, ((a + b + apex) / 3f) - baseCenter);
            AddTriangleFacing(b, c, apex, ((b + c + apex) / 3f) - baseCenter);
            AddTriangleFacing(c, d, apex, ((c + d + apex) / 3f) - baseCenter);
            AddTriangleFacing(d, a, apex, ((d + a + apex) / 3f) - baseCenter);

            if (includeBase)
            {
                AddQuadFacing(a, d, c, b, rotation * Vector3.down);
            }
        }

        public void AddCylinder(Vector3 baseCenter, float height, float bottomRadius, float topRadius, int sides)
        {
            var topCenter = baseCenter + Vector3.up * height;
            for (var i = 0; i < sides; i++)
            {
                var t0 = (float)i / sides;
                var t1 = (float)(i + 1) / sides;
                var a0 = t0 * Mathf.PI * 2f;
                var a1 = t1 * Mathf.PI * 2f;

                var b0 = baseCenter + new Vector3(Mathf.Cos(a0) * bottomRadius, 0f, Mathf.Sin(a0) * bottomRadius);
                var b1 = baseCenter + new Vector3(Mathf.Cos(a1) * bottomRadius, 0f, Mathf.Sin(a1) * bottomRadius);
                var top0 = topCenter + new Vector3(Mathf.Cos(a0) * topRadius, 0f, Mathf.Sin(a0) * topRadius);
                var top1 = topCenter + new Vector3(Mathf.Cos(a1) * topRadius, 0f, Mathf.Sin(a1) * topRadius);

                var sideHint = new Vector3(Mathf.Cos((a0 + a1) * 0.5f), 0f, Mathf.Sin((a0 + a1) * 0.5f));
                AddQuadFacing(b0, b1, top1, top0, sideHint);
                AddTriangleFacing(baseCenter, b1, b0, Vector3.down);
                AddTriangleFacing(topCenter, top0, top1, Vector3.up);
            }
        }

        public void AddDeformedSphere(Vector3 center, Vector3 radii, int latSegments, int lonSegments, float amplitude, float frequency)
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
                        AddTriangleFacing(p00, p11, p10, ((p00 + p10 + p11) / 3f) - center);
                    }
                    else if (lat == latSegments - 1)
                    {
                        AddTriangleFacing(p00, p01, p10, ((p00 + p01 + p10) / 3f) - center);
                    }
                    else
                    {
                        AddQuadFacing(p00, p01, p11, p10, ((p00 + p01 + p10 + p11) * 0.25f) - center);
                    }
                }
            }
        }

        public void AddPetal(Vector3 position, Quaternion rotation, float length, float width, float thickness)
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
            AddTriangleFacing(baseFront, leftFront, tipFront, frontHint);
            AddTriangleFacing(baseFront, tipFront, rightFront, frontHint);
            AddTriangleFacing(baseBack, tipBack, leftBack, -frontHint);
            AddTriangleFacing(baseBack, rightBack, tipBack, -frontHint);

            AddQuadFacing(baseFront, leftFront, leftBack, baseBack, MidpointHint(position, baseFront, leftFront, leftBack, baseBack));
            AddQuadFacing(leftFront, tipFront, tipBack, leftBack, MidpointHint(position, leftFront, tipFront, tipBack, leftBack));
            AddQuadFacing(tipFront, rightFront, rightBack, tipBack, MidpointHint(position, tipFront, rightFront, rightBack, tipBack));
            AddQuadFacing(rightFront, baseFront, baseBack, rightBack, MidpointHint(position, rightFront, baseFront, baseBack, rightBack));
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

        private void AddQuadFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outwardHint)
        {
            AddTriangleFacing(a, b, c, outwardHint);
            AddTriangleFacing(a, c, d, outwardHint);
        }

        private void AddTriangleFacing(Vector3 a, Vector3 b, Vector3 c, Vector3 outwardHint)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outwardHint) < 0f)
            {
                (b, c) = (c, b);
            }

            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }
    }
}
