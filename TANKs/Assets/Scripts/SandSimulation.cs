using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class SandSimulation : MonoBehaviour
{
    [System.Serializable]
    public struct SandLayer
    {
        public int sandTypeIndex;       // รหัส ID ชนิดทราย อิงตามดัชนีในคลังข้อมูลกลาง
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

    [Header("🚨 Shared Database Connecting")]
    public SandDatabase sandDatabase;

    private float tickTimer = 0f;
    private Mesh filterMesh;
    private Vector3[] baseVertices;
    private Vector3[] currentVertices;
    private SandColumn[] sandColumns;

    private int gridXCount;
    private int gridZCount;
    private float currentCellSize = 0.01f;

    private int mainVertexCount;
    private int[] perimeterIndices;
    private float skirtDepth = -0.05f;

    private float[] heightSnapshot;
    private float[] sandMoisture;

    private float maxHeightLimit = 0.4f;

    // ระบบคุมการหลับ/ตื่นฟิสิกส์เพื่อเร่งความเร็วคอม
    private bool isSimulationSleeping = false;

    private readonly int[] dx = { -1, 1, 0, 0, -1, 1, -1, 1 };
    private readonly int[] dz = { 0, 0, -1, 1, -1, -1, 1, 1 };
    private readonly float[] distMult = { 1f, 1f, 1f, 1f, 1.414f, 1.414f, 1.414f, 1.414f };

    void Start()
    {
        SandGridGenerator generator = GetComponent<SandGridGenerator>();
        if (generator != null)
        {
            gridXCount = generator.gridXCount;
            gridZCount = generator.gridZCount;
            currentCellSize = generator.cellSize;
            mainVertexCount = generator.mainVertexCount;
            perimeterIndices = generator.perimeterIndicesArray;
            skirtDepth = generator.skirtDepth;
            maxHeightLimit = generator.tankHeight;
        }
        else
        {
            Debug.LogError("[SandSimulation] 不พบสคริปต์ SandGridGenerator!");
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

        heightSnapshot = new float[mainVertexCount];
        sandMoisture = new float[mainVertexCount];
    }

    void Update()
    {
        if (sandDatabase == null) return;

        // คำนวณการระเหยของน้ำออกจากเนื้อทรายรายเฟรม
        float activeDrySpeed = sandDatabase.drySpeed;
        bool isDryingActive = false;
        for (int i = 0; i < mainVertexCount; i++)
        {
            if (sandMoisture[i] > 0f)
            {
                sandMoisture[i] -= activeDrySpeed * Time.deltaTime;
                if (sandMoisture[i] < 0f) sandMoisture[i] = 0f;
                isDryingActive = true;

                if (isSimulationSleeping) WakeUpSimulation();
            }
        }

        if (isSimulationSleeping && !isDryingActive) return;

        tickTimer += Time.deltaTime;

        if (tickTimer >= sandDatabase.simulationInterval)
        {
            bool avalancheChanged = ApplyAvalancheEffect(tickTimer);

            if (avalancheChanged || isDryingActive)
            {
                UpdateMesh();
            }
            else if (!avalancheChanged && !isDryingActive)
            {
                GoToSleepSimulation();
            }
            tickTimer = 0f;
        }
    }

    private void GoToSleepSimulation()
    {
        if (!isSimulationSleeping)
        {
            isSimulationSleeping = true;
            Debug.Log("<color=#7f8c8d><b>[SandSimulation]</b> 💤 ฟิสิกส์ทรายสงบนิ่งสนิทแล้ว... สั่งปิดลูปการคำนวณชั่วครู่เพื่อประหยัด CPU</color>");
        }
    }

    public void WakeUpSimulation()
    {
        if (isSimulationSleeping)
        {
            isSimulationSleeping = false;
            Debug.Log("<color=#f39c12><b>[SandSimulation]</b> ⚡ มีมวลสารขยับตัว! ปลุกระบบฟิสิกส์ถล่มให้ตื่นขึ้นมาคำนวณใหม่</color>");
        }
    }

    public Vector3[] GetCurrentVertices()
    {
        return currentVertices;
    }

    public void MakeWet(int index, float moistureValue = 1.0f)
    {
        if (sandMoisture != null && index >= 0 && index < sandMoisture.Length)
        {
            if (sandMoisture[index] < 0.9f && isSimulationSleeping) WakeUpSimulation();
            sandMoisture[index] = moistureValue;
        }
    }

    public void PourSand(Vector3 hitPoint, int sandTypeIndex, float brushRadius, float pourSpeed)
    {
        if (sandDatabase == null || sandDatabase.sandTypes == null || sandDatabase.sandTypes.Length == 0) return;
        sandTypeIndex = Mathf.Clamp(sandTypeIndex, 0, sandDatabase.sandTypes.Length - 1);

        WakeUpSimulation();

        for (int i = 0; i < mainVertexCount; i++)
        {
            Vector3 worldPt = transform.TransformPoint(currentVertices[i]);
            float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

            if (distance < brushRadius)
            {
                float falloff = 1f - (distance / brushRadius);
                float addedThickness = pourSpeed * falloff * Time.deltaTime;

                AddSandToColumn(i, sandTypeIndex, addedThickness);
                sandMoisture[i] = 0f;
            }
        }
        UpdateMesh();
    }

    public void VacuumSand(Vector3 hitPoint, float brushRadius, float vacuumSpeed)
    {
        WakeUpSimulation();

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
    // 🚨 🪐 [ฟังก์ชัน SmoothSand ฉบับกระจายโหลดแบบ Blur Filter คำนวณสมบูรณ์แบบ]
    public void SmoothSand(Vector3 hitPoint, float brushRadius, float smoothSpeed)
    {
        WakeUpSimulation();

        WaterSystem waterSys = GetComponent<WaterSystem>();
        if (waterSys != null) waterSys.WakeUpSimulation();

        // 1. ดึงความสูง ณ ปัจจุบันของทุกพิกเซลเก็บไว้เป็นต้นแบบอ้างอิงชั่วคราว (Snapshot)
        float[] tempHeights = new float[mainVertexCount];
        for (int i = 0; i < mainVertexCount; i++)
        {
            tempHeights[i] = sandColumns[i].GetTotalHeight();
        }

        bool hasChanged = false;

        // 2. เริ่มลูปคำนวณสมานความสูงเฉลี่ยในวงแปรงพู่กัน
        for (int z = 0; z < gridZCount; z++)
        {
            for (int x = 0; x < gridXCount; x++)
            {
                int currentIndex = z * gridXCount + x;
                if (currentIndex >= mainVertexCount) continue;

                Vector3 worldPt = transform.TransformPoint(currentVertices[currentIndex]);
                float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

                if (distance < brushRadius)
                {
                    float falloff = 1f - (distance / brushRadius);

                    // อัลกอริทึมเก็บค่าผลรวมความสูงและนับจำนวนเซลล์รอบข้าง 4 ทิศทาง (Cross Blur Filter)
                    float sumHeight = 0f;
                    int neighborCount = 0;

                    if (x > 0) { sumHeight += tempHeights[currentIndex - 1]; neighborCount++; }
                    if (x < gridXCount - 1) { sumHeight += tempHeights[currentIndex + 1]; neighborCount++; }
                    if (z > 0) { sumHeight += tempHeights[currentIndex - gridXCount]; neighborCount++; }
                    if (z < gridZCount - 1) { sumHeight += tempHeights[currentIndex + gridXCount]; neighborCount++; }

                    if (neighborCount > 0)
                    {
                        float targetAverageHeight = sumHeight / neighborCount;
                        float currentHeight = tempHeights[currentIndex];
                        float heightDifference = targetAverageHeight - currentHeight;

                        // เพิ่มกำลังแรงเกลี่ยคูณตาม Falloff (ตรงกลางบรัชจะสมูทไวกว่าขอบ)
                        float smoothAmount = heightDifference * smoothSpeed * falloff * Time.deltaTime;

                        if (Mathf.Abs(smoothAmount) > 0.00001f)
                        {
                            var column = sandColumns[currentIndex];

                            if (smoothAmount > 0f)
                            {
                                // คอลัมน์ต่ำกว่ารอบข้าง -> ดึงเนื้อทรายมาถมเพิ่มให้สูงเท่าเพื่อน
                                if (column.layers.Count > 0)
                                {
                                    var topLayer = column.layers[column.layers.Count - 1];
                                    topLayer.thickness += smoothAmount;
                                    column.layers[column.layers.Count - 1] = topLayer;
                                }
                                else
                                {
                                    int fallbackTypeIndex = 0;
                                    ToolManager toolManager = FindFirstObjectByType<ToolManager>();
                                    if (toolManager != null)
                                    {
                                        fallbackTypeIndex = toolManager.selectedSandTypeIndex;
                                    }

                                    column.layers.Add(new SandLayer { sandTypeIndex = fallbackTypeIndex, thickness = smoothAmount });
                                }
                            }
                            else
                            {
                                // คอลัมน์สูงโด่งแหลมกว่ารอบข้าง -> ปาดความหนาทรายส่วนเกินออก
                                if (column.layers.Count > 0)
                                {
                                    var topLayer = column.layers[column.layers.Count - 1];
                                    float absAmount = Mathf.Abs(smoothAmount);

                                    if (topLayer.thickness > absAmount)
                                    {
                                        topLayer.thickness -= absAmount;
                                        column.layers[column.layers.Count - 1] = topLayer;
                                    }
                                    else
                                    {
                                        column.layers.RemoveAt(column.layers.Count - 1);
                                    }
                                }
                            }
                            hasChanged = true;
                        }
                    }
                }
            }
        } // <-- สิ้นสุดลูปแกน Z และแกน X ทั้งหมดอย่างสมบูรณ์ตรงนี้

        // 🚨 🪐 [ย้ายมาจุดนี้] สั่ง UpdateMesh รวบยอด "ครั้งเดียวอยู่นอกลูปทั้งหมด" ของเฟรม
        if (hasChanged)
        {
            UpdateMesh();
        }
    }

    private bool ApplyAvalancheEffect(float simDeltaTime)
    {
        if (sandColumns == null || sandColumns.Length == 0) return false;
        if (sandDatabase == null || sandDatabase.sandTypes == null || sandDatabase.sandTypes.Length == 0) return false;

        bool meshChanged = false;

        for (int i = 0; i < mainVertexCount; i++)
        {
            heightSnapshot[i] = sandColumns[i].GetTotalHeight();
        }

        for (int z = 0; z < gridZCount; z++)
        {
            for (int x = 0; x < gridXCount; x++)
            {
                int currentIndex = z * gridXCount + x;
                if (currentIndex >= sandColumns.Length) continue;

                float currentHeight = heightSnapshot[currentIndex];
                if (currentHeight <= 0 || sandColumns[currentIndex].layers.Count == 0) continue;

                SandLayer topLayer = sandColumns[currentIndex].layers[sandColumns[currentIndex].layers.Count - 1];
                int currentSandType = Mathf.Clamp(topLayer.sandTypeIndex, 0, sandDatabase.sandTypes.Length - 1);

                SandDatabase.SandType activePhysics = sandDatabase.sandTypes[currentSandType];
                float allowedHeightDiff = activePhysics.maxSlopeAngle * currentCellSize;

                List<int> validNeighbors = new List<int>();
                List<float> excessWeights = new List<float>();
                float totalExcess = 0f;
                float maxExcess = 0f;

                for (int i = 0; i < 8; i++)
                {
                    int nx = x + dx[i];
                    int nz = z + dz[i];

                    if (nx >= 0 && nx < gridXCount && nz >= 0 && nz < gridZCount)
                    {
                        int neighborIndex = nz * gridXCount + nx;
                        float neighborHeight = heightSnapshot[neighborIndex];
                        float heightDiff = currentHeight - neighborHeight;
                        float targetSlope = allowedHeightDiff * distMult[i];

                        if (heightDiff > targetSlope)
                        {
                            float excess = heightDiff - targetSlope;

                            if (excess > activePhysics.staticFriction)
                            {
                                float flowableExcess = excess - activePhysics.staticFriction;

                                validNeighbors.Add(neighborIndex);
                                excessWeights.Add(flowableExcess);
                                totalExcess += flowableExcess;

                                if (flowableExcess > maxExcess) maxExcess = flowableExcess;
                            }
                        }
                    }
                }

                if (validNeighbors.Count > 0)
                {
                    float totalFlowAmount = totalExcess * activePhysics.flowRate * simDeltaTime;

                    float safeCap = maxExcess * 0.2f;
                    if (totalFlowAmount > safeCap) totalFlowAmount = safeCap;

                    float topLayerThickness = topLayer.thickness;
                    if (totalFlowAmount > topLayerThickness) totalFlowAmount = topLayerThickness;

                    if (totalFlowAmount > 0.00001f)
                    {
                        RemoveSandFromColumn(currentIndex, totalFlowAmount);

                        for (int n = 0; n < validNeighbors.Count; n++)
                        {
                            float portion = excessWeights[n] / totalExcess;
                            float amountToNeighbor = totalFlowAmount * portion;

                            AddSandToColumn(validNeighbors[n], topLayer.sandTypeIndex, amountToNeighbor);

                            if (sandMoisture[currentIndex] > 0.1f)
                            {
                                sandMoisture[validNeighbors[n]] = Mathf.Max(sandMoisture[validNeighbors[n]], sandMoisture[currentIndex] * 0.8f);
                            }
                        }
                        meshChanged = true;
                    }
                }
            }
        }

        // 🚨 🪐 [ดักแทรกปลุกพ่วงระบบน้ำข้างๆ] เมื่อมีทรายไหลถล่ม 
        if (meshChanged)
        {
            WaterSystem waterSys = GetComponent<WaterSystem>();
            if (waterSys != null) waterSys.WakeUpSimulation();
        }

        return meshChanged;
    }

    private void AddSandToColumn(int index, int sandTypeIndex, float amount)
    {
        var column = sandColumns[index];
        if (column.GetTotalHeight() + amount > maxHeightLimit) amount = maxHeightLimit - column.GetTotalHeight();
        if (amount <= 0) return;

        if (column.layers.Count > 0 && column.layers[column.layers.Count - 1].sandTypeIndex == sandTypeIndex)
        {
            var layer = column.layers[column.layers.Count - 1];
            layer.thickness += amount;
            column.layers[column.layers.Count - 1] = layer;
        }
        else
        {
            column.layers.Add(new SandLayer { sandTypeIndex = sandTypeIndex, thickness = amount });
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

    public float GetHeightAtWorldPos(Vector3 worldPos)
    {
        Vector3 localPos = transform.InverseTransformPoint(worldPos);

        float usableWidth = (gridXCount - 1) * currentCellSize;
        float usableLength = (gridZCount - 1) * currentCellSize;

        int x = Mathf.RoundToInt(((localPos.x / usableWidth) + 0.5f) * (gridXCount - 1));
        int z = Mathf.RoundToInt(((localPos.z / usableLength) + 0.5f) * (gridZCount - 1));

        x = Mathf.Clamp(x, 0, gridXCount - 1);
        z = Mathf.Clamp(z, 0, gridZCount - 1);

        int index = z * gridXCount + x;

        if (currentVertices != null && index < currentVertices.Length) return currentVertices[index].y;
        return 0f;
    }

    private void UpdateMesh()
    {
        if (sandDatabase == null || sandDatabase.sandTypes == null || sandDatabase.sandTypes.Length == 0) return;
        Color[] meshColors = new Color[currentVertices.Length];

        float activeMultiplier = sandDatabase.wetDarknessMultiplier;
        Color defaultEmptyColor = sandDatabase.baseDefaultColor;

        for (int i = 0; i < mainVertexCount; i++)
        {
            currentVertices[i].y = baseVertices[i].y + sandColumns[i].GetTotalHeight();

            if (sandColumns[i].layers.Count > 0)
            {
                SandLayer top = sandColumns[i].layers[sandColumns[i].layers.Count - 1];
                int typeIdx = Mathf.Clamp(top.sandTypeIndex, 0, sandDatabase.sandTypes.Length - 1);

                Color defaultColor = sandDatabase.sandTypes[typeIdx].sandColor;
                Color wetColor = new Color(defaultColor.r * activeMultiplier, defaultColor.g * activeMultiplier, defaultColor.b * activeMultiplier, 1f);

                meshColors[i] = Color.Lerp(defaultColor, wetColor, sandMoisture[i]);
            }
            else
            {
                meshColors[i] = defaultEmptyColor;
            }
        }

        if (perimeterIndices != null && perimeterIndices.Length > 0)
        {
            for (int i = mainVertexCount; i < currentVertices.Length; i++)
            {
                currentVertices[i].y = skirtDepth;

                int perimeterIndex = i - mainVertexCount;
                if (perimeterIndex < perimeterIndices.Length)
                {
                    int topVertexIndex = perimeterIndices[perimeterIndex];
                    meshColors[i] = meshColors[topVertexIndex];
                }
                else
                {
                    meshColors[i] = defaultEmptyColor;
                }
            }
        }

        filterMesh.vertices = currentVertices;
        filterMesh.colors = meshColors;
        filterMesh.RecalculateNormals();
        filterMesh.RecalculateTangents();

        filterMesh.bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));

        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            GetComponent<MeshCollider>().sharedMesh = filterMesh;
        }
    }
}