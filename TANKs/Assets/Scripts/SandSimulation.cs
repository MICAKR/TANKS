using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class SandSimulation : MonoBehaviour
{
    [System.Serializable]
    public struct SandLayer
    {
        public int colorIndex;
        public float thickness;
    }

    public class SandColumn
    {
        public List<SandLayer> layers = new List<SandLayer>();

        public float GetTotalHeight()
        {
            float total = 0;
            foreach (var layer in layers) total += layer.thickness;
            return total;
        }
    }

    [Header("Optimization Settings")]
    public float simulationInterval = 0.03f;
    private float tickTimer = 0f;

    [Header("Erosion & Physics Settings")]
    public float maxSlopeAngle = 0.3f;
    public float flowRate = 12.0f;
    public float maxHeight = 1.0f;

    [Header("Sand Visual Colors")]
    public Color[] sandColors = new Color[] { Color.white, Color.blue, Color.red };
    public Color baseDefaultColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    private Mesh filterMesh;
    private Vector3[] baseVertices;
    private Vector3[] currentVertices;
    private SandColumn[] sandColumns;

    private int gridXCount;
    private int gridZCount;
    private float currentCellSize = 0.01f;

    private int mainVertexCount;
    private int[] perimeterIndices;
    private float skirtDepth = -0.05f; // ตัวแปรรับค่าความลึกกำแพง

    private readonly int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
    private readonly int[] dz = { 0, 0, -1, 1, -1, -1, 1, 1 };
    private readonly float[] distMult = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

    void Start()
    {
        SandGridGenerator generator = GetComponent<SandGridGenerator>();
        if (generator != null)
        {
            generator.GenerateGrid();
            gridXCount = generator.gridXCount;
            gridZCount = generator.gridZCount;
            currentCellSize = generator.cellSize;
            mainVertexCount = generator.mainVertexCount;
            perimeterIndices = generator.perimeterIndicesArray;
            skirtDepth = generator.skirtDepth; // อ่านค่าความลึกกำแพงฝังดินมาจาก Generator
        }
        else
        {
            Debug.LogError("[SandSimulation] ไม่พบสคริปต์ SandGridGenerator!");
            return;
        }

        filterMesh = GetComponent<MeshFilter>().mesh;
        baseVertices = filterMesh.vertices;

        currentVertices = new Vector3[baseVertices.Length];
        System.Array.Copy(baseVertices, currentVertices, baseVertices.Length);

        sandColumns = new SandColumn[mainVertexCount];
        for (int i = 0; i < sandColumns.Length; i++)
        {
            sandColumns[i] = new SandColumn();
        }
    }

    void Update()
    {
        tickTimer += Time.deltaTime;
        if (tickTimer >= simulationInterval)
        {
            ApplyAvalancheEffect(tickTimer);
            tickTimer = 0f;
        }
    }

    public void PourSand(Vector3 hitPoint, int colorIndex, float brushRadius, float pourSpeed)
    {
        for (int i = 0; i < mainVertexCount; i++)
        {
            Vector3 worldPt = transform.TransformPoint(currentVertices[i]);
            float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

            if (distance < brushRadius)
            {
                float falloff = 1f - (distance / brushRadius);
                float addedThickness = pourSpeed * falloff * Time.deltaTime;

                AddSandToColumn(i, colorIndex, addedThickness);
            }
        }
        UpdateMesh();
    }

    public void VacuumSand(Vector3 hitPoint, float brushRadius, float vacuumSpeed)
    {
        for (int i = 0; i < mainVertexCount; i++)
        {
            Vector3 worldPt = transform.TransformPoint(currentVertices[i]);
            float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

            if (distance < brushRadius)
            {
                float falloff = 1f - (distance / brushRadius);
                float removedThickness = vacuumSpeed * falloff * Time.deltaTime;

                RemoveSandFromColumn(i, removedThickness);
            }
        }
        UpdateMesh();
    }

    private void ApplyAvalancheEffect(float simDeltaTime)
    {
        if (sandColumns == null || sandColumns.Length == 0) return;
        bool meshChanged = false;

        float allowedHeightDiff = maxSlopeAngle * currentCellSize;

        List<int> validNeighbors = new List<int>();
        List<float> excessWeights = new List<float>();

        for (int z = 0; z < gridZCount; z++)
        {
            for (int x = 0; x < gridXCount; x++)
            {
                int currentIndex = z * gridXCount + x;
                if (currentIndex >= sandColumns.Length) continue;

                float currentHeight = sandColumns[currentIndex].GetTotalHeight();
                if (currentHeight <= 0 || sandColumns[currentIndex].layers.Count == 0) continue;

                validNeighbors.Clear();
                excessWeights.Clear();
                float totalExcess = 0f;

                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx[i];
                    int nz = z + dz[i];

                    if (nx >= 0 && nx < gridXCount && nz >= 0 && nz < gridZCount)
                    {
                        int neighborIndex = nz * gridXCount + nx;
                        float neighborHeight = sandColumns[neighborIndex].GetTotalHeight();
                        float heightDiff = currentHeight - neighborHeight;

                        float targetSlope = allowedHeightDiff * distMult[i];

                        if (heightDiff > targetSlope)
                        {
                            float excess = heightDiff - targetSlope;
                            validNeighbors.Add(neighborIndex);
                            excessWeights.Add(excess);
                            totalExcess += excess;
                        }
                    }
                }

                if (validNeighbors.Count > 0)
                {
                    float totalFlowAmount = totalExcess * flowRate * simDeltaTime;

                    int topColor = sandColumns[currentIndex].layers[sandColumns[currentIndex].layers.Count - 1].colorIndex;
                    float topLayerThickness = sandColumns[currentIndex].layers[sandColumns[currentIndex].layers.Count - 1].thickness;

                    if (totalFlowAmount > topLayerThickness) totalFlowAmount = topLayerThickness;
                    if (totalFlowAmount > totalExcess * 0.4f) totalFlowAmount = totalExcess * 0.4f;

                    if (totalFlowAmount > 0f)
                    {
                        RemoveSandFromColumn(currentIndex, totalFlowAmount);

                        for (int n = 0; n < validNeighbors.Count; n++)
                        {
                            float portion = excessWeights[n] / totalExcess;
                            float amountToNeighbor = totalFlowAmount * portion;

                            AddSandToColumn(validNeighbors[n], topColor, amountToNeighbor);
                        }
                        meshChanged = true;
                    }
                }
            }
        }

        if (meshChanged)
        {
            UpdateMesh();
        }
    }

    private void AddSandToColumn(int index, int colorIndex, float amount)
    {
        var column = sandColumns[index];
        if (column.GetTotalHeight() + amount > maxHeight) amount = maxHeight - column.GetTotalHeight();
        if (amount <= 0) return;

        if (column.layers.Count > 0 && column.layers[column.layers.Count - 1].colorIndex == colorIndex)
        {
            var layer = column.layers[column.layers.Count - 1];
            layer.thickness += amount;
            column.layers[column.layers.Count - 1] = layer;
        }
        else
        {
            column.layers.Add(new SandLayer { colorIndex = colorIndex, thickness = amount });
        }
    }

    private void RemoveSandFromColumn(int index, float amount)
    {
        var column = sandColumns[index];
        while (amount > 0 && column.layers.Count > 0)
        {
            var topLayer = column.layers[column.layers.Count - 1];
            if (topLayer.thickness > amount)
            {
                topLayer.thickness -= amount;
                column.layers[column.layers.Count - 1] = topLayer;
                amount = 0;
            }
            else
            {
                amount -= topLayer.thickness;
                column.layers.RemoveAt(column.layers.Count - 1);
            }
        }
    }

    private void UpdateMesh()
    {
        Color[] meshColors = new Color[currentVertices.Length];

        for (int i = 0; i < mainVertexCount; i++)
        {
            currentVertices[i].y = baseVertices[i].y + sandColumns[i].GetTotalHeight();

            if (sandColumns[i].layers.Count > 0)
            {
                int topColorIndex = sandColumns[i].layers[sandColumns[i].layers.Count - 1].colorIndex;
                int safeIndex = Mathf.Clamp(topColorIndex, 0, sandColors.Length - 1);
                meshColors[i] = sandColors[safeIndex];
            }
            else
            {
                meshColors[i] = baseDefaultColor;
            }
        }

        if (perimeterIndices != null && perimeterIndices.Length > 0)
        {
            for (int i = mainVertexCount; i < currentVertices.Length; i++)
            {
                currentVertices[i].y = skirtDepth; // 🚨 ล็อกฐานกำแพงให้ดิ่งจมลงใต้ดินตามค่าที่เราตั้งไว้

                int perimeterIndex = i - mainVertexCount;
                if (perimeterIndex < perimeterIndices.Length)
                {
                    int topVertexIndex = perimeterIndices[perimeterIndex];
                    meshColors[i] = meshColors[topVertexIndex];
                }
                else
                {
                    meshColors[i] = baseDefaultColor;
                }
            }
        }

        filterMesh.vertices = currentVertices;
        filterMesh.colors = meshColors;
        filterMesh.RecalculateNormals();

        filterMesh.bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));

        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            GetComponent<MeshCollider>().sharedMesh = filterMesh;
        }
    }
}