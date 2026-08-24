using UnityEngine;

/// <summary>
/// Tiny procedural mesh helpers (Unity has no cone primitive). Used for mountains
/// and tree canopies so we get recognizable shapes without importing assets.
/// </summary>
public static class MeshUtil
{
    public static Mesh Cone(int segments = 12, float radius = 1f, float height = 1f)
    {
        segments = Mathf.Max(3, segments);
        var verts = new Vector3[segments + 2];
        var tris = new int[segments * 6];

        verts[0] = Vector3.zero;                       // base centre
        verts[segments + 1] = new Vector3(0f, height, 0f); // apex
        for (int i = 0; i < segments; i++)
        {
            float a = (i / (float)segments) * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int cur = i + 1;
            int next = (i + 1) % segments + 1;
            // side
            tris[t++] = segments + 1; tris[t++] = next; tris[t++] = cur;
            // base
            tris[t++] = 0; tris[t++] = cur; tris[t++] = next;
        }

        var m = new Mesh();
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    public static GameObject MeshObject(string name, Mesh mesh, Transform parent,
                                        Vector3 pos, Vector3 scale, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        var mat = new Material(Shader.Find("Standard"));
        mat.color = color;
        mr.sharedMaterial = mat;
        return go;
    }
}
