using UnityEngine;
using System.Collections.Generic;

[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class SandGridGenerator : MonoBehaviour
{
    [Header("Tank Dimensions (ขนาดตู้ปลาขอบนอก)")]
    public float width = 0.508f;
    public float length = 0.254f;

    [Header("Grid Density Settings")]
    public float cellSize = 0.01f;

    [Header("Border Settings (ระบบกันทรายทะลุกระจก)")]
    [Tooltip("ความหนาของกระจกตู้ปลา (หน่วยเป็นเมตร)")]
    public float glassThickness = 0.01f;
    [Tooltip("ความลึกของกำแพงข้างตู้ ให้ตั้งเป็นค่าติดลบเพื่อขุดกำแพงฝังทะลุลงไปใต้พื้นตู้ปลา (เช่น -0.05 คือลึกลงไป 5 ซม.)")]
    public float skirtDepth = -0.05f;

    [HideInInspector] public int gridXCount;
    [HideInInspector] public int gridZCount;
    [HideInInspector] public int mainVertexCount;
    [HideInInspector] public int[] perimeterIndicesArray;

    private Mesh generatedMesh;

    void Awake()
    {
        GenerateGrid();
    }

    [ContextMenu("🪄 Generate Sand Grid")]
    public void GenerateGrid()
    {
        if (cellSize <= 0) cellSize = 0.01f;

        float usableWidth = width - (glassThickness * 2f);
        float usableLength = length - (glassThickness * 2f);

        if (usableWidth <= 0) usableWidth = 0.05f;
        if (usableLength <= 0) usableLength = 0.05f;

        int resolutionX = Mathf.RoundToInt(usableWidth / cellSize);
        int resolutionZ = Mathf.RoundToInt(usableLength / cellSize);

        if (resolutionX < 1) resolutionX = 1;
        if (resolutionZ < 1) resolutionZ = 1;

        gridXCount = resolutionX + 1;
        gridZCount = resolutionZ + 1;
        mainVertexCount = gridXCount * gridZCount;

        generatedMesh = new Mesh();
        generatedMesh.name = "Dynamic Sand Grid with Side Walls";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        // 1. สร้างจุดผิวบน
        for (int z = 0; z <= resolutionZ; z++)
        {
            for (int x = 0; x <= resolutionX; x++)
            {
                float xPos = ((float)x / resolutionX - 0.5f) * usableWidth;
                float zPos = ((float)z / resolutionZ - 0.5f) * usableLength;

                vertices.Add(new Vector3(xPos, 0, zPos));
                uvs.Add(new Vector2((float)x / resolutionX, (float)z / resolutionZ));
            }
        }

        // 2. ถักสามเหลี่ยมผิวบน
        int vi = 0;
        for (int z = 0; z < resolutionZ; z++)
        {
            for (int x = 0; x < resolutionX; x++)
            {
                triangles.Add(vi);
                triangles.Add(vi + resolutionX + 1);
                triangles.Add(vi + 1);

                triangles.Add(vi + 1);
                triangles.Add(vi + resolutionX + 1);
                triangles.Add(vi + resolutionX + 2);
                vi++;
            }
            vi++;
        }

        // 3. กำแพงข้างตู้
        List<int> perimeterIndices = new List<int>();
        for (int x = 0; x < resolutionX; x++) perimeterIndices.Add(0 * gridXCount + x);
        for (int z = 0; z < resolutionZ; z++) perimeterIndices.Add(z * gridXCount + resolutionX);
        for (int x = resolutionX; x > 0; x--) perimeterIndices.Add(resolutionZ * gridXCount + x);
        for (int z = resolutionZ; z > 0; z--) perimeterIndices.Add(z * gridXCount + 0);

        int perimeterCount = perimeterIndices.Count;
        perimeterIndicesArray = perimeterIndices.ToArray();
        int bottomStartVertexIndex = vertices.Count;

        // สร้างจุดฐานล่างอิงตามค่า skirtDepth (ขุดลงใต้พื้น)
        for (int e = 0; e < perimeterCount; e++)
        {
            int topVertIndex = perimeterIndices[e];
            Vector3 topVertPos = vertices[topVertIndex];

            vertices.Add(new Vector3(topVertPos.x, skirtDepth, topVertPos.z));
            uvs.Add(uvs[topVertIndex]);
        }

        // ถักสามเหลี่ยมกำแพงข้าง
        for (int e = 0; e < perimeterCount; e++)
        {
            int nextE = (e + 1) % perimeterCount;

            int topCurrent = perimeterIndices[e];
            int topNext = perimeterIndices[nextE];

            int bottomCurrent = bottomStartVertexIndex + e;
            int bottomNext = bottomStartVertexIndex + nextE;

            triangles.Add(topCurrent);
            triangles.Add(topNext);
            triangles.Add(bottomCurrent);

            triangles.Add(topNext);
            triangles.Add(bottomNext);
            triangles.Add(bottomCurrent);
        }

        generatedMesh.vertices = vertices.ToArray();
        generatedMesh.triangles = triangles.ToArray();
        generatedMesh.uv = uvs.ToArray();
        generatedMesh.RecalculateNormals();

        GetComponent<MeshFilter>().sharedMesh = generatedMesh;
        GetComponent<MeshCollider>().sharedMesh = generatedMesh;

        Debug.Log($"[SandGridGenerator] สร้างแผ่นทรายพร้อมกำแพงทึบหลบขอบกระจกสำเร็จ!");
    }
}