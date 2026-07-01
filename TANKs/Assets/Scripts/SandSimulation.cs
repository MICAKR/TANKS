using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public class SandSimulation : MonoBehaviour
{
    [System.Serializable]
    public struct SandLayer
    {
        public int sandTypeIndex;
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
    private Color[] currentMeshColors; // 🌟 Cache สีไว้ ไม่สร้างใหม่ทุกเฟรม
    private SandColumn[] sandColumns;

    private int gridXCount;
    private int gridZCount;
    private float currentCellSize = 0.01f;

    private int mainVertexCount;
    private int[] perimeterIndices;
    private float skirtDepth = -0.05f;

    private float[] heightSnapshot;
    private float[] sandMoisture;



    // ระบบคุมการหลับ/ตื่นฟิสิกส์เพื่อเร่งความเร็วคอม
   

    private float maxHeightLimit = 0.4f;
    private bool isSimulationSleeping = false;
    private WaterSystem cachedWaterSys;

    // 🚨 🪐 ตัวแปรใหม่สำหรับคุมความถี่การระบายสีทราย
    [Header("Optimization")]
    public bool isEditModeActive = false; // สถานะว่ากำลังจัดตู้หรือไม่
    private float colorUpdateTimer = 0f;
    public float idleColorUpdateInterval = 0.25f; // ลดการอัปเดตเหลือ 4 ครั้ง/วินาที ตอนไม่ได้จัดตู้

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
            Debug.LogError("[SandSimulation] ไม่พบสคริปต์ SandGridGenerator!");
            return;
        }

        cachedWaterSys = GetComponent<WaterSystem>();
        if (cachedWaterSys == null && transform.parent != null)
        {
            cachedWaterSys = transform.parent.GetComponentInChildren<WaterSystem>();
        }

        filterMesh = GetComponent<MeshFilter>().mesh;
        baseVertices = filterMesh.vertices;

        currentVertices = new Vector3[baseVertices.Length];
        currentMeshColors = new Color[baseVertices.Length]; // 🌟 เตรียม Array สีไว้ล่วงหน้า
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

        // 🌟 คำนวณเวลาและเลือกว่าจะใช้ความถี่แบบไหน
        colorUpdateTimer += Time.deltaTime;
        float targetInterval = isEditModeActive ? 0f : idleColorUpdateInterval;
        bool isColorDirty = false;

        // ถ้าระยะเวลาถึงกำหนด (หรือจัดตู้อยู่ = 0) ให้คำนวณสีทราย
        if (colorUpdateTimer >= targetInterval)
        {
            // ส่งค่าเวลาที่สะสมไว้ไปคำนวณ เพื่อให้ทรายแห้ง/เปียกด้วยความเร็วคงที่เสมอ
            isColorDirty = UpdateMoistureLogic(colorUpdateTimer);
            colorUpdateTimer = 0f; // รีเซ็ตเวลา
        }

        bool isPhysicsDirty = false;
        if (!isSimulationSleeping)
        {
            tickTimer += Time.deltaTime;
            if (tickTimer >= sandDatabase.simulationInterval)
            {
                bool avalancheChanged = ApplyAvalancheEffect(tickTimer);

                if (avalancheChanged)
                {
                    isPhysicsDirty = true;
                }
                else
                {
                    GoToSleepSimulation();
                }
                tickTimer = 0f;
            }
        }

        if (isPhysicsDirty)
        {
            UpdatePhysicsMesh();
            UpdateColorMesh();
        }
        else if (isColorDirty)
        {
            UpdateColorMesh();
        }
    }
    // 🌟 ระบบคำนวณความชื้น (แยกออกมาต่างหาก)
    private bool UpdateMoistureLogic(float deltaTime)
    {
        float activeDrySpeed = sandDatabase.drySpeed;
        bool moistureChangedThisFrame = false;

        for (int i = 0; i < mainVertexCount; i++)
        {
            bool isUnderwater = false;

            if (cachedWaterSys != null)
            {
                Vector3 worldPt = transform.TransformPoint(currentVertices[i]);
                float localWaterY = cachedWaterSys.GetHeightAtWorldPos(worldPt);

                if (localWaterY > currentVertices[i].y + 0.001f)
                {
                    isUnderwater = true;
                }
            }

            if (isUnderwater)
            {
                if (sandMoisture[i] < 1.0f)
                {
                    sandMoisture[i] += 5.0f * deltaTime; // ใช้ deltaTime ที่ส่งเข้ามา
                    if (sandMoisture[i] > 1.0f) sandMoisture[i] = 1.0f;
                    moistureChangedThisFrame = true;
                }
            }
            else
            {
                if (sandMoisture[i] > 0f)
                {
                    sandMoisture[i] -= activeDrySpeed * deltaTime; // ใช้ deltaTime ที่ส่งเข้ามา
                    if (sandMoisture[i] < 0f) sandMoisture[i] = 0f;
                    moistureChangedThisFrame = true;
                }
            }
        }

        return moistureChangedThisFrame;
    }

    public void SetEditMode(bool active)
    {
        isEditModeActive = active;
        if (active)
        {
            // ถ้ากลับมาจัดตู้ ให้เคลียร์เวลาแล้วบังคับระบายสีรอบนึงทันทีเพื่อความสมูท
            colorUpdateTimer = 0f;
            UpdateColorMesh();
        }
    }
    private void GoToSleepSimulation()
    {
        if (!isSimulationSleeping)
        {
            isSimulationSleeping = true;
            Debug.Log("<color=#7f8c8d><b>[SandSimulation]</b> 💤 มวลทรายเข้าสู่สมดุล... สั่งปิดลูปฟิสิกส์ (แต่สียังรันต่อได้อิสระ)</color>");
        }
    }

    public void WakeUpSimulation()
    {
        if (isSimulationSleeping)
        {
            isSimulationSleeping = false;
            Debug.Log("<color=#f39c12><b>[SandSimulation]</b> ⚡ ปลุกระบบฟิสิกส์ให้ตื่นขึ้นมาคำนวณมวลทราย</color>");
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
            sandMoisture[index] = moistureValue;
            // 🌟 ลบการปลุกฟิสิกส์ทิ้งไป เพราะสีไม่เกี่ยวกับการถล่ม
        }
    }

    public void PourSand(Vector3 hitPoint, int sandTypeIndex, float brushRadius, float pourSpeed)
    {
        if (sandDatabase == null || sandDatabase.sandTypes == null || sandDatabase.sandTypes.Length == 0) return;
        sandTypeIndex = Mathf.Clamp(sandTypeIndex, 0, sandDatabase.sandTypes.Length - 1);

        WakeUpSimulation();
        if (cachedWaterSys != null) cachedWaterSys.WakeUpSimulation();

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
        UpdatePhysicsMesh();
        UpdateColorMesh();
    }

    public void VacuumSand(Vector3 hitPoint, float brushRadius, float vacuumSpeed)
    {
        WakeUpSimulation();
        if (cachedWaterSys != null) cachedWaterSys.WakeUpSimulation();

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
        UpdatePhysicsMesh();
        UpdateColorMesh();
    }

    public void SmoothSand(Vector3 hitPoint, float brushRadius, float smoothSpeed)
    {
        WakeUpSimulation();
        if (cachedWaterSys != null) cachedWaterSys.WakeUpSimulation();

        float[] tempHeights = new float[mainVertexCount];
        for (int i = 0; i < mainVertexCount; i++)
        {
            tempHeights[i] = sandColumns[i].GetTotalHeight();
        }

        bool hasChanged = false;

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

                        float smoothAmount = heightDifference * smoothSpeed * falloff * Time.deltaTime;

                        if (Mathf.Abs(smoothAmount) > 0.00001f)
                        {
                            var column = sandColumns[currentIndex];

                            if (smoothAmount > 0f)
                            {
                                if (column.layers.Count > 0)
                                {
                                    var topLayer = column.layers[column.layers.Count - 1];
                                    topLayer.thickness += smoothAmount;
                                    column.layers[column.layers.Count - 1] = topLayer;
                                }
                                else
                                {
                                    int fallbackTypeIndex = 0;
                                    column.layers.Add(new SandLayer { sandTypeIndex = fallbackTypeIndex, thickness = smoothAmount });
                                }
                            }
                            else
                            {
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
        }

        if (hasChanged)
        {
            UpdatePhysicsMesh();
            UpdateColorMesh();
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

        if (meshChanged)
        {
            if (cachedWaterSys != null) cachedWaterSys.WakeUpSimulation();
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

    // 🌟 เปลี่ยนจากการอัปเดตทุกอย่าง ให้เหลือแค่อัปเดตพิกัด (ช่วยลดภาระเครื่อง)
    private void UpdatePhysicsMesh()
    {
        for (int i = 0; i < mainVertexCount; i++)
        {
            currentVertices[i].y = baseVertices[i].y + sandColumns[i].GetTotalHeight();
        }

        if (perimeterIndices != null && perimeterIndices.Length > 0)
        {
            for (int i = mainVertexCount; i < currentVertices.Length; i++)
            {
                currentVertices[i].y = skirtDepth;
            }
        }

        filterMesh.vertices = currentVertices;
        filterMesh.RecalculateNormals();
        filterMesh.RecalculateTangents();
        filterMesh.bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));

        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
        {
            GetComponent<MeshCollider>().sharedMesh = filterMesh;
        }
    }

    // 🌟 ฟังก์ชันแยกสำหรับระบายสีอย่างเดียวโดยไม่กระทบโครงสร้างฟิสิกส์
    private void UpdateColorMesh()
    {
        if (sandDatabase == null || sandDatabase.sandTypes == null || sandDatabase.sandTypes.Length == 0) return;

        float activeMultiplier = sandDatabase.wetDarknessMultiplier;
        Color defaultEmptyColor = sandDatabase.baseDefaultColor;

        for (int i = 0; i < mainVertexCount; i++)
        {
            if (sandColumns[i].layers.Count > 0)
            {
                SandLayer top = sandColumns[i].layers[sandColumns[i].layers.Count - 1];
                int typeIdx = Mathf.Clamp(top.sandTypeIndex, 0, sandDatabase.sandTypes.Length - 1);

                Color defaultColor = sandDatabase.sandTypes[typeIdx].sandColor;
                Color wetColor = new Color(defaultColor.r * activeMultiplier, defaultColor.g * activeMultiplier, defaultColor.b * activeMultiplier, 1f);

                currentMeshColors[i] = Color.Lerp(defaultColor, wetColor, sandMoisture[i]);
            }
            else
            {
                currentMeshColors[i] = defaultEmptyColor;
            }
        }

        if (perimeterIndices != null && perimeterIndices.Length > 0)
        {
            for (int i = mainVertexCount; i < currentVertices.Length; i++)
            {
                int perimeterIndex = i - mainVertexCount;
                if (perimeterIndex < perimeterIndices.Length)
                {
                    int topVertexIndex = perimeterIndices[perimeterIndex];
                    currentMeshColors[i] = currentMeshColors[topVertexIndex];
                }
                else
                {
                    currentMeshColors[i] = defaultEmptyColor;
                }
            }
        }

        filterMesh.colors = currentMeshColors;
    }

    public void ForceSettlePhysics()
    {
        if (sandDatabase == null) return;
        Debug.Log("<color=#e67e22><b>[SandSimulation]</b> ⏳ สั่งเร่งความเร็วฟิสิกส์ทรายให้เข้าสู่สมดุล...</color>");

        int maxIterations = 200;
        int currentIter = 0;
        bool stillFalling = true;

        while (stillFalling && currentIter < maxIterations)
        {
            stillFalling = ApplyAvalancheEffect(0.1f);
            currentIter++;
        }

        UpdatePhysicsMesh();
        UpdateColorMesh();
        GoToSleepSimulation();
        Debug.Log($"<color=#27ae60><b>[SandSimulation]</b> ✅ ทรายสงบนิ่งแล้ว (ใช้ไป {currentIter} รอบการคำนวณ)</color>");
    }
}