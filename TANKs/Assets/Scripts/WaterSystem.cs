using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter))]
public class WaterSystem : MonoBehaviour
{
    [Header("Optimization Settings")]
    public float simulationInterval = 0.02f;
    private float tickTimer = 0f;

    [Header("Physics Settings (ฟิสิกส์ของเหลว)")]
    [Tooltip("ความเร็วในการไหลแผ่กระจายของน้ำ (ปรับได้ 30 - 60 ยิ่งเยอะน้ำยิ่งแบนราบเรียบไวขึ้น)")]
    public float waterFlowRate = 40.0f;

    // เพดานความสูงตู้ปลาสัมบูรณ์จากพื้นตู้ (เมตร) ดึงค่ามาจาก SandGridGenerator
    private float maxWaterHeight = 0.4f;

    [Header("🌊 Dynamic Wave Settings (ระบบระลอกคลื่นสะท้อน)")]
    [Range(0.1f, 0.8f)]
    public float waveTravelSpeed = 0.5f;
    [Range(0.9f, 0.99f)]
    public float waveDamping = 0.97f;

    public float pourWaveImpulse = 0.004f;
    public float vacuumWaveImpulse = 0.004f;
    public float maxWaveHeightLimit = 0.015f;

    [Header("Sand Reference (ค้นหาให้อัตโนมัติแล้ว)")]
    public MeshFilter sandMeshFilter;

    private SandSimulation cachedSandSim;

    private Mesh filterMesh;
    private Vector3[] baseVertices;
    private Vector3[] currentVertices;
    private float[] waterThicknesses;

    private float[] currentWaves;
    private float[] previousWaves;
    private float[] nextWaves;

    private int gridXCount;
    private int gridZCount;
    private float currentCellSize = 0.01f;

    private int mainVertexCount;
    private int[] perimeterIndices;
    private float skirtDepth = -0.05f;

    // 🚨 🪐 [ระบบคุมการหลับ/ตื่นฟิสิกส์ของเหลว]
    private bool isSimulationSleeping = false;

    void Start()
    {
        SandGridGenerator generator = GetComponent<SandGridGenerator>();
        if (generator == null && transform.parent != null)
        {
            generator = transform.parent.GetComponentInChildren<SandGridGenerator>();
        }

        if (generator != null)
        {
            gridXCount = generator.gridXCount;
            gridZCount = generator.gridZCount;
            currentCellSize = generator.cellSize;
            mainVertexCount = generator.mainVertexCount;
            perimeterIndices = generator.perimeterIndicesArray;
            skirtDepth = generator.skirtDepth;
            maxWaterHeight = generator.tankHeight;
        }
        else
        {
            Debug.LogError("[WaterSystem] ไม่พบสคริปต์ SandGridGenerator ทั้งในตัวเองและใน Parent!");
            return;
        }

        SandSimulation sandSim = GetComponent<SandSimulation>();
        if (sandSim == null && transform.parent != null)
        {
            sandSim = transform.parent.GetComponentInChildren<SandSimulation>();
        }

        if (sandSim != null)
        {
            cachedSandSim = sandSim;
            sandMeshFilter = sandSim.GetComponent<MeshFilter>();
        }
        else
        {
            Debug.LogWarning("[WaterSystem] ไม่พบสคริปต์ SandSimulation ในโครงสร้าง Prefab เดียวกัน!");
        }

        filterMesh = GetComponent<MeshFilter>().mesh;
        baseVertices = filterMesh.vertices;

        currentVertices = new Vector3[baseVertices.Length];
        System.Array.Copy(baseVertices, currentVertices, baseVertices.Length);

        waterThicknesses = new float[mainVertexCount];

        currentWaves = new float[mainVertexCount];
        previousWaves = new float[mainVertexCount];
        nextWaves = new float[mainVertexCount];
    }

    void Update()
    {
        // 🚨 🪐 ถ้าระบบน้ำแผ่ราบและคลื่นนิ่งสนิทจนสั่ง Sleep ไปแล้ว ให้หยุดข้ามกระบวนการลูปคำนวณทันทีเพื่อรีดสปีดเครื่อง
        if (isSimulationSleeping) return;

        tickTimer += Time.deltaTime;
        if (tickTimer >= simulationInterval)
        {
            bool waterMoved = ApplyWaterFlowEffect(tickTimer);
            bool wavesActive = ApplyWaveSimulation();
            UpdateMesh();

            // 🚨 🪐 [เช็คจุดสลบผิวน้ำ] ถ้าน้ำไม่มีการขยับตัว และ คลื่นสลายตัวนิ่งสนิทแล้ว สั่งปิดเครื่องนอนทันที
            if (!waterMoved && !wavesActive)
            {
                GoToSleepSimulation();
            }

            tickTimer = 0f;
        }
    }

    // 💤 สั่งตัดกระแสไฟฟิสิกส์ระบบน้ำเข้าโหมดจำศีล
    private void GoToSleepSimulation()
    {
        if (!isSimulationSleeping)
        {
            isSimulationSleeping = true;
            Debug.Log("<color=#3498db><b>[WaterSystem]</b> 🌊 ผิวน้ำเกลี่ยแบนราบและระลอกคลื่นนิ่งสนิทแล้ว... สั่งปิดลูปการคำนวณชั่วครู่</color>");
        }
    }

    // ⚡ ปลุกระบบน้ำให้ตื่นขึ้นมาแผ่มลสารและทำระลอกคลื่นใหม่
    public void WakeUpSimulation()
    {
        if (isSimulationSleeping)
        {
            isSimulationSleeping = false;
            Debug.Log("<color=#2ecc71><b>[WaterSystem]</b> ⚡ ผิวน้ำโดนรบกวน! ปลุกระบบฟิสิกส์ของเหลวและคลื่นให้กลับมาทำงานเรียลไทม์</color>");
        }
    }

    public void PourWater(Vector3 hitPoint, float brushRadius, float pourSpeed)
    {
        // 🚨 ปลุกระบบน้ำทันทีที่มีการเติมน้ำใหม่ลงตู้
        WakeUpSimulation();

        float densityMultiplier = 0.01f / currentCellSize;
        float actualSpeed = pourSpeed * densityMultiplier * 5.0f;

        Vector3[] sandVertices = null;
        if (cachedSandSim != null) sandVertices = cachedSandSim.GetCurrentVertices();

        for (int i = 0; i < mainVertexCount; i++)
        {
            Vector3 worldPt = transform.TransformPoint(new Vector3(baseVertices[i].x, 0, baseVertices[i].z));
            float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

            if (distance < brushRadius)
            {
                float falloff = 1f - (distance / brushRadius);
                waterThicknesses[i] += actualSpeed * falloff * Time.deltaTime;

                float sandY = (sandVertices != null && i < sandVertices.Length) ? sandVertices[i].y : 0f;
                if (sandY + waterThicknesses[i] > maxWaterHeight)
                {
                    waterThicknesses[i] = maxWaterHeight - sandY;
                    if (waterThicknesses[i] < 0f) waterThicknesses[i] = 0f;
                }

                float waveIncrement = pourWaveImpulse * falloff * Time.deltaTime * 60f;
                currentWaves[i] = Mathf.Clamp(currentWaves[i] + waveIncrement, -maxWaveHeightLimit, maxWaveHeightLimit);
            }
        }
    }

    public void VacuumWater(Vector3 hitPoint, float brushRadius, float vacuumSpeed)
    {
        // 🚨 ปลุกระบบน้ำทันทีที่มีการดูดลบมวลน้ำออก
        WakeUpSimulation();

        float densityMultiplier = 0.01f / currentCellSize;
        float actualSpeed = vacuumSpeed * densityMultiplier * 5.0f;

        for (int i = 0; i < mainVertexCount; i++)
        {
            Vector3 worldPt = transform.TransformPoint(new Vector3(baseVertices[i].x, 0, baseVertices[i].z));
            float distance = Vector2.Distance(new Vector2(worldPt.x, worldPt.z), new Vector2(hitPoint.x, hitPoint.z));

            if (distance < brushRadius)
            {
                float falloff = 1f - (distance / brushRadius);
                waterThicknesses[i] -= actualSpeed * falloff * Time.deltaTime;
                if (waterThicknesses[i] < 0f) waterThicknesses[i] = 0f;

                float waveIncrement = vacuumWaveImpulse * falloff * Time.deltaTime * 60f;
                currentWaves[i] = Mathf.Clamp(currentWaves[i] - waveIncrement, -maxWaveHeightLimit, maxWaveHeightLimit);
            }
        }
    }

    // 🚨 ปรับให้ส่งค่ากลับเป็น bool เพื่อส่งสัญญาณบอก Update ว่าน้ำเฟรมนี้ยังไหลอยู่ไหม
    private bool ApplyWaterFlowEffect(float simDeltaTime)
    {
        if (waterThicknesses == null || waterThicknesses.Length == 0) return false;

        Vector3[] sandVertices = null;
        if (cachedSandSim != null) sandVertices = cachedSandSim.GetCurrentVertices();
        if (sandVertices == null) return false;

        int iterations = 6;
        float stepFlowRate = waterFlowRate / iterations;
        bool globalWaterMoved = false;

        for (int iter = 0; iter < iterations; iter++)
        {
            float[] nextWaterThicknesses = new float[waterThicknesses.Length];
            System.Array.Copy(waterThicknesses, nextWaterThicknesses, waterThicknesses.Length);
            bool waterMovedInIteration = false;

            for (int z = 0; z < gridZCount; z++)
            {
                for (int x = 0; x < gridXCount; x++)
                {
                    int currentIndex = z * gridXCount + x;
                    float currentWater = waterThicknesses[currentIndex];
                    if (currentWater <= 0.00001f) continue;

                    float currentSand = sandVertices[currentIndex].y;
                    float currentTotalHeight = currentSand + currentWater;

                    float totalDiff = 0f;
                    int lowerCount = 0;

                    int nLeft = (x > 0) ? currentIndex - 1 : -1;
                    int nRight = (x < gridXCount - 1) ? currentIndex + 1 : -1;
                    int nDown = (z > 0) ? currentIndex - gridXCount : -1;
                    int nUp = (z < gridZCount - 1) ? currentIndex + gridXCount : -1;

                    float dLeft = 0, dRight = 0, dDown = 0, dUp = 0;
                    float sumNeighborHeights = 0f;

                    if (nLeft != -1)
                    {
                        dLeft = currentTotalHeight - (sandVertices[nLeft].y + waterThicknesses[nLeft]);
                        if (dLeft > 0.00001f) { totalDiff += dLeft; lowerCount++; sumNeighborHeights += sandVertices[nLeft].y + waterThicknesses[nLeft]; }
                    }
                    if (nRight != -1)
                    {
                        dRight = currentTotalHeight - (sandVertices[nRight].y + waterThicknesses[nRight]);
                        if (dRight > 0.00001f) { totalDiff += dRight; lowerCount++; sumNeighborHeights += sandVertices[nRight].y + waterThicknesses[nRight]; }
                    }
                    if (nDown != -1)
                    {
                        dDown = currentTotalHeight - (sandVertices[nDown].y + waterThicknesses[nDown]);
                        if (dDown > 0.00001f) { totalDiff += dDown; lowerCount++; sumNeighborHeights += sandVertices[nDown].y + waterThicknesses[nDown]; }
                    }
                    if (nUp != -1)
                    {
                        dUp = currentTotalHeight - (sandVertices[nUp].y + waterThicknesses[nUp]);
                        if (dUp > 0.00001f) { totalDiff += dUp; lowerCount++; sumNeighborHeights += sandVertices[nUp].y + waterThicknesses[nUp]; }
                    }

                    if (lowerCount > 0)
                    {
                        float targetHeight = (currentTotalHeight + sumNeighborHeights) / (lowerCount + 1);
                        float maxSafeOutflow = currentTotalHeight - targetHeight;

                        float intendedFlow = totalDiff * stepFlowRate * simDeltaTime;
                        float finalFlow = Mathf.Min(intendedFlow, maxSafeOutflow * 0.8f);
                        finalFlow = Mathf.Min(finalFlow, currentWater);

                        if (finalFlow > 0.00001f)
                        {
                            nextWaterThicknesses[currentIndex] -= finalFlow;

                            if (nLeft != -1 && dLeft > 0.00001f) nextWaterThicknesses[nLeft] += finalFlow * (dLeft / totalDiff);
                            if (nRight != -1 && dRight > 0.00001f) nextWaterThicknesses[nRight] += finalFlow * (dRight / totalDiff);
                            if (nDown != -1 && dDown > 0.00001f) nextWaterThicknesses[nDown] += finalFlow * (dDown / totalDiff);
                            if (nUp != -1 && dUp > 0.00001f) nextWaterThicknesses[nUp] += finalFlow * (dUp / totalDiff);

                            waterMovedInIteration = true;
                            globalWaterMoved = true;
                        }
                    }
                }
            }

            if (!waterMovedInIteration) break;
            System.Array.Copy(nextWaterThicknesses, waterThicknesses, waterThicknesses.Length);
        }

        return globalWaterMoved;
    }

    // 🚨 ปรับให้ส่งค่ากลับเป็น bool เพื่อเช็คว่าพิกัดคลื่นยังคงสั่นไหวระลอกคลื่นอยู่ไหม
    private bool ApplyWaveSimulation()
    {
        int[] wdx = { -1, 1, 0, 0 };
        int[] wdz = { 0, 0, -1, 1 };
        bool wavesAreActive = false;

        for (int z = 0; z < gridZCount; z++)
        {
            for (int x = 0; x < gridXCount; x++)
            {
                int idx = z * gridXCount + x;
                if (idx >= mainVertexCount) continue;

                if (waterThicknesses[idx] <= 0.00005f)
                {
                    currentWaves[idx] = 0f;
                    previousWaves[idx] = 0f;
                    nextWaves[idx] = 0f;
                    continue;
                }

                float neighborSum = 0f;

                for (int i = 0; i < 4; i++)
                {
                    int nx = x + wdx[i];
                    int nz = z + wdz[i];

                    if (nx >= 0 && nx < gridXCount && nz >= 0 && nz < gridZCount)
                    {
                        neighborSum += currentWaves[nz * gridXCount + nx];
                    }
                    else
                    {
                        neighborSum += currentWaves[idx];
                    }
                }

                float currentWaveValue = currentWaves[idx];
                float previousWaveValue = previousWaves[idx];

                float updateWaveValue = 2f * currentWaveValue - previousWaveValue + waveTravelSpeed * ((neighborSum * 0.25f) - currentWaveValue);
                updateWaveValue *= waveDamping;

                nextWaves[idx] = Mathf.Clamp(updateWaveValue, -maxWaveHeightLimit, maxWaveHeightLimit);

                // 🟢 ถ้าค่าระลอกคลื่นสูงเกินระดับจุลภาค ถือว่าคลื่นยังขยับอยู่ ห้ามเพิ่งหลับชั่วครู่
                if (Mathf.Abs(nextWaves[idx]) > 0.0001f)
                {
                    wavesAreActive = true;
                }
            }
        }

        float[] temp = previousWaves;
        previousWaves = currentWaves;
        currentWaves = nextWaves;
        nextWaves = temp;

        return wavesAreActive;
    }

    private void UpdateMesh()
    {
        Vector3[] sandVertices = null;
        if (cachedSandSim != null) sandVertices = cachedSandSim.GetCurrentVertices();

        for (int i = 0; i < mainVertexCount; i++)
        {
            float sandY = (sandVertices != null && i < sandVertices.Length) ? sandVertices[i].y : 0f;
            float waterThickness = waterThicknesses[i];

            if (waterThickness > 0.00005f)
            {
                float targetWaterY = sandY + waterThickness + currentWaves[i];
                if (targetWaterY > maxWaterHeight) targetWaterY = maxWaterHeight;

                currentVertices[i].y = targetWaterY;

                if (cachedSandSim != null)
                {
                    cachedSandSim.MakeWet(i, 1.0f);
                }
            }
            else
            {
                float targetY = sandY - 0.002f;
                currentVertices[i].y = Mathf.Lerp(currentVertices[i].y, targetY, Time.deltaTime * 12f);

                if (currentVertices[i].y > targetY)
                {
                    currentVertices[i].y = targetY;
                }
            }
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
        filterMesh.bounds = new Bounds(Vector3.zero, new Vector3(20f, 20f, 20f));
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

        if (currentVertices != null && index < currentVertices.Length)
        {
            return currentVertices[index].y;
        }
        return 0f;
    }
    // ... [โค้ดเดิมด้านบนทั้งหมด] ...

    // 🚨 🪐 ฟังก์ชันใหม่: สั่งเร่งเวลาฟิสิกส์น้ำให้ราบเรียบและคลื่นนิ่งสนิทในพริบตา
    public void ForceSettlePhysics()
    {
        Debug.Log("<color=#3498db><b>[WaterSystem]</b> ⏳ สั่งเร่งความเร็วฟิสิกส์ของเหลวให้เข้าสู่สมดุล...</color>");

        int maxIterations = 200;
        int currentIter = 0;
        bool stillMoving = true;
        bool wavesActive = true;

        // จำลองเวลาเฟรมละ 0.1 วิ (เร็วกว่าปกติ 5 เท่า) เพื่อเคลียร์คลื่นและน้ำให้เรียบ
        while ((stillMoving || wavesActive) && currentIter < maxIterations)
        {
            stillMoving = ApplyWaterFlowEffect(0.1f);
            wavesActive = ApplyWaveSimulation();
            currentIter++;
        }

        // รีเซ็ตคลื่นให้เป็น 0 ไปเลยเพื่อความชัวร์ว่านิ่งกริ๊บ
        for (int i = 0; i < currentWaves.Length; i++)
        {
            currentWaves[i] = 0f;
            previousWaves[i] = 0f;
            nextWaves[i] = 0f;
        }

        UpdateMesh();
        GoToSleepSimulation();
        Debug.Log($"<color=#2ecc71><b>[WaterSystem]</b> ✅ ผิวน้ำสงบนิ่งแล้ว (ใช้ไป {currentIter} รอบการคำนวณ)</color>");
    }

}