using System;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter))]
public sealed class SceneVertexEmbeddedMeshData : MonoBehaviour
{
    [SerializeField] private string meshName = "SceneVertexEmbeddedMesh";
    [SerializeField] private Vector3[] vertices = Array.Empty<Vector3>();
    [SerializeField] private int[] triangles = Array.Empty<int>();
    [SerializeField] private Vector3[] normals = Array.Empty<Vector3>();
    [SerializeField] private Vector2[] uv = Array.Empty<Vector2>();
    [SerializeField] private Vector2[] uv2 = Array.Empty<Vector2>();
    [SerializeField] private Color[] colors = Array.Empty<Color>();

    [NonSerialized] private Mesh generatedMesh;

    public int SerializedVertexCount => vertices?.Length ?? 0;

    public void SetMeshData(
        string newMeshName,
        Vector3[] newVertices,
        int[] newTriangles,
        Vector3[] newNormals,
        Vector2[] newUv,
        Vector2[] newUv2,
        Color[] newColors)
    {
        meshName = string.IsNullOrWhiteSpace(newMeshName) ? "SceneVertexEmbeddedMesh" : newMeshName;
        vertices = CloneOrEmpty(newVertices);
        triangles = CloneOrEmpty(newTriangles);
        normals = CloneOrEmpty(newNormals);
        uv = CloneOrEmpty(newUv);
        uv2 = CloneOrEmpty(newUv2);
        colors = CloneOrEmpty(newColors);
        RebuildMesh();
    }

    public void SetMeshDataFromSourceMesh(string newMeshName, Mesh sourceMesh)
    {
        if (sourceMesh == null)
        {
            SetMeshData(newMeshName, Array.Empty<Vector3>(), Array.Empty<int>(), Array.Empty<Vector3>(), Array.Empty<Vector2>(), Array.Empty<Vector2>(), Array.Empty<Color>());
            return;
        }

        var sourceVertices = sourceMesh.vertices;
        var sourceColors = sourceMesh.colors;
        if (sourceColors == null || sourceColors.Length != sourceVertices.Length)
        {
            sourceColors = new Color[sourceVertices.Length];
            for (var i = 0; i < sourceColors.Length; i++)
            {
                sourceColors[i] = Color.white;
            }
        }

        SetMeshData(
            newMeshName,
            sourceVertices,
            sourceMesh.triangles,
            sourceMesh.normals,
            sourceMesh.uv,
            sourceMesh.uv2,
            sourceColors);
    }

    public void RebuildMesh()
    {
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            return;
        }

        EnsureGeneratedMesh();

        generatedMesh.Clear();
        generatedMesh.name = meshName;
        generatedMesh.vertices = vertices ?? Array.Empty<Vector3>();

        if (uv != null && uv.Length == generatedMesh.vertexCount)
        {
            generatedMesh.uv = uv;
        }

        if (uv2 != null && uv2.Length == generatedMesh.vertexCount)
        {
            generatedMesh.uv2 = uv2;
        }

        if (colors != null && colors.Length == generatedMesh.vertexCount)
        {
            generatedMesh.colors = colors;
        }

        generatedMesh.triangles = triangles ?? Array.Empty<int>();

        if (normals != null && normals.Length == generatedMesh.vertexCount)
        {
            generatedMesh.normals = normals;
        }
        else if (generatedMesh.vertexCount > 0 && generatedMesh.triangles.Length > 0)
        {
            generatedMesh.RecalculateNormals();
        }

        generatedMesh.RecalculateBounds();
        meshFilter.sharedMesh = generatedMesh;

        var meshCollider = GetComponent<MeshCollider>();
        if (meshCollider != null)
        {
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = generatedMesh;
        }
    }

    private void OnEnable()
    {
        RebuildMesh();
    }

    private void OnValidate()
    {
        RebuildMesh();
    }

    private void OnDestroy()
    {
        if (generatedMesh == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(generatedMesh);
        }
        else
        {
            DestroyImmediate(generatedMesh);
        }
    }

    private void EnsureGeneratedMesh()
    {
        if (generatedMesh != null)
        {
            return;
        }

        generatedMesh = new Mesh
        {
            name = meshName,
            hideFlags = HideFlags.HideAndDontSave
        };
    }

    private static T[] CloneOrEmpty<T>(T[] source)
    {
        if (source == null || source.Length == 0)
        {
            return Array.Empty<T>();
        }

        var clone = new T[source.Length];
        Array.Copy(source, clone, source.Length);
        return clone;
    }
}
