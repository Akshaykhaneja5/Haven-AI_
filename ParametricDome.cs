// parametric dome

using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ParametricDome : MonoBehaviour
{
    public float radius = 1.5f; // Radius of the dome
    public int uSegments = 30;  // Number of segments along the u-axis
    public int vSegments = 15;  // Number of segments along the v-axis

    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = CreateDomeMesh(radius, uSegments, vSegments);
    }

    Mesh CreateDomeMesh(float R, int uSegments, int vSegments)
    {
        Mesh mesh = new Mesh();
        Vector3[] vertices = new Vector3[(uSegments + 1) * (vSegments + 1)];
        int[] triangles = new int[uSegments * vSegments * 6];
        float uStep = (Mathf.PI * 2) / uSegments;
        float vStep = R / vSegments;
        float minY = 6.89f; // The initial base height of the dome in your calculations

        for (int v = 0, i = 0; v <= vSegments; v++)
        {
            for (int u = 0; u <= uSegments; u++, i++)
            {
                float uAngle = u * uStep;
                float vPos = v * vStep;
                float x = Mathf.Sqrt(R * R - vPos * vPos) * Mathf.Cos(uAngle);
                float z = Mathf.Sqrt(R * R - vPos * vPos) * Mathf.Sin(uAngle);
                float y = vPos + 6.89f - minY; // Adjust y so the base of the dome is at y = 0
                vertices[i] = new Vector3(x, y, z);
            }
        }

        for (int v = 0, ti = 0; v < vSegments; v++)
        {
            for (int u = 0; u < uSegments; u++, ti += 6)
            {
                int a = v * (uSegments + 1) + u;
                int b = a + uSegments + 1;
                int c = a + 1;
                int d = b + 1;
                triangles[ti] = a;
                triangles[ti + 1] = b;
                triangles[ti + 2] = c;
                triangles[ti + 3] = c;
                triangles[ti + 4] = b;
                triangles[ti + 5] = d;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals(); // Automatically calculate normals for better lighting
        return mesh;
    }
}
