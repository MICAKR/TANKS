using UnityEngine;

public class TankWaterQuality : MonoBehaviour
{
    [Header("📊 ค่าน้ำปัจจุบัน (Current Water Status)")]
    [Range(0f, 100f)]
    public float turbidity = 0f;

    [Range(0f, 100f)]
    public float bacteria = 0f;

    public float salinity = 0f;

    [Range(0f, 14f)]
    public float ph = 7.0f;

    [Header("💧 ปริมาตรมวลสาร (Volume in Liters)")]
    [Tooltip("ปริมาณน้ำที่มีอยู่ในตู้ปัจจุบัน (หน่วย: ลิตร)")]
    public float waterVolumeLiters = 0f;

    [Tooltip("ปริมาณทรายในตู้ (หน่วย: ลิตร) - คิดอัตโนมัติ")]
    public float sandVolumeLiters = 0f;

    [Header("📐 ข้อมูลมิติของตู้ (Physical Dimensions)")]
    [Tooltip("ระดับความสูงของน้ำจริงในตู้ (เมตร) นับจากจุดต่ำสุดของพื้นตู้")]
    public float currentWaterHeight = 0f;

    private float currentSandVolume_M3 = 0f;
    private float tankBaseArea = 1f;
    private float tankMaxHeight = 1f;

    private SandGridGenerator gridGen;
    private SandSimulation sandSim;
    private WaterSystem waterSim;

    [Header("⚙️ ตั้งค่าการจำลอง (Simulation Settings)")]
    public float simulationInterval = 1.0f;

    [Header("🛠️ Debug Fast Forward")]
    public float timeMultiplier = 1.0f;

    [Header("📈 เกณฑ์ระบบนิเวศมาตรฐาน")]
    public float maxBacteriaCap = 100f;
    public float targetNaturalPH = 7.2f;

    private float tickTimer = 0f;

    [Header("🎨 การแสดงผลสีน้ำ (Water Visual Settings)")]
    [Tooltip("สีของน้ำตอนที่ใสสะอาด 100% (น้ำเงินเข้มเกือบดำ)")]
    public Color clearWaterColor = new Color(0.02f, 0.05f, 0.15f);

    [Tooltip("สีของน้ำตอนที่มีฝุ่นทรายขุ่น 100%")]
    public Color murkyWaterColor = new Color(0.5f, 0.4f, 0.2f);

    [Tooltip("สีของน้ำตอนที่ของเสีย/ไนโตรเจนเป็นพิษจัดๆ")]
    public Color toxicWaterColor = new Color(0.2f, 0.5f, 0.2f);

    [Space(10)]
    [Tooltip("ชื่อ Property สีใน Shader ของคุณ")]
    public string shaderColorPropertyName = "_WaterTint";

    private Material waterMaterial;

    void Start()
    {
        gridGen = GetComponentInChildren<SandGridGenerator>();
        sandSim = GetComponentInChildren<SandSimulation>();
        waterSim = GetComponentInChildren<WaterSystem>();

        if (waterSim != null)
        {
            MeshRenderer mr = waterSim.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                waterMaterial = mr.material;
            }
        }

        if (gridGen != null)
        {
            Vector3 scale = gridGen.transform.lossyScale;
            float width = (gridGen.gridXCount - 1) * gridGen.cellSize * scale.x;
            float length = (gridGen.gridZCount - 1) * gridGen.cellSize * scale.z;

            tankBaseArea = width * length;
            tankMaxHeight = gridGen.tankHeight * scale.y;
        }
        else
        {
            tankBaseArea = 1f;
            tankMaxHeight = 1f;
        }

        turbidity = 0f;
        tickTimer = Random.Range(0f, simulationInterval);

        UpdateWaterVisuals();
    }

    void Update()
    {
        tickTimer += Time.deltaTime;

        if (tickTimer >= simulationInterval)
        {
            UpdatePhysicalVolumes();
            ProcessEcosystemStep(simulationInterval * timeMultiplier);
            UpdateWaterVisuals();
            tickTimer = 0f;
        }
    }

    private void UpdatePhysicalVolumes()
    {
        if (gridGen == null) return;

        Vector3 scale = gridGen.transform.lossyScale;
        int mainVertCount = gridGen.gridXCount * gridGen.gridZCount;
        float exactCellArea = tankBaseArea / mainVertCount;

        // 1. คำนวณปริมาณวัสดุรองพื้น (ทราย)
        Vector3[] sVerts = null;
        if (sandSim != null)
        {
            sVerts = sandSim.GetCurrentVertices();
            if (sVerts != null)
            {
                float totalSand_M3 = 0f;
                for (int i = 0; i < mainVertCount && i < sVerts.Length; i++)
                {
                    if (sVerts[i].y > 0) totalSand_M3 += (sVerts[i].y * scale.y) * exactCellArea;
                }
                currentSandVolume_M3 = totalSand_M3;
                sandVolumeLiters = currentSandVolume_M3 * 1000f;
            }
        }

        // 2. คำนวณปริมาณน้ำ (🚨 แก้ไข: หักลบความสูงพื้นทรายออก เพื่อให้ได้มวลน้ำจริง)
        if (waterSim != null)
        {
            MeshRenderer waterMR = waterSim.GetComponent<MeshRenderer>();
            MeshFilter waterMF = waterSim.GetComponent<MeshFilter>();

            if (waterMF != null && waterMF.sharedMesh != null)
            {
                Vector3[] wVerts = waterMF.sharedMesh.vertices;
                float totalWater_M3 = 0f;

                for (int i = 0; i < mainVertCount && i < wVerts.Length; i++)
                {
                    // อ่านความสูงของทราย ณ พิกัดเดียวกัน (ถ้าจุดนั้นไม่มีทรายให้เป็น 0)
                    float sandHeightAtVertex = (sVerts != null && i < sVerts.Length) ? sVerts[i].y : 0f;

                    // ความหนาของน้ำที่แท้จริง = ระดับผิวน้ำรวม - ระดับพื้นทราย
                    float actualWaterThickness = wVerts[i].y - sandHeightAtVertex;

                    if (actualWaterThickness > 0.0001f)
                    {
                        totalWater_M3 += (actualWaterThickness * scale.y) * exactCellArea;
                    }
                }
                waterVolumeLiters = totalWater_M3 * 1000f;
            }
        }

        if (tankBaseArea > 0f)
        {
            float waterVolume_M3 = waterVolumeLiters / 1000f;
            currentWaterHeight = (waterVolume_M3 + currentSandVolume_M3) / tankBaseArea;
        }
    }

    [Header("🤢 ระบบของเสียและสารพิษ (Waste & Nitrogen System)")]
    public float waste = 0f;
    public float nitrogen = 0f;
    public float bacteriaEfficiency = 0.2f;

    private void ProcessEcosystemStep(float simDeltaTime)
    {
        if (waterVolumeLiters <= 0f)
        {
            bacteria = 0f;
            salinity = 0f;
            waste = 0f;
            nitrogen = 0f;
            ph = 7.0f;
            turbidity = 0f;
            return;
        }

        float waterVolumeFactor = Mathf.Clamp(waterVolumeLiters / 35f, 0.2f, 4.0f);

        if ((turbidity > 0f || currentSandVolume_M3 > 0f || waste > 0f) && bacteria < maxBacteriaCap)
        {
            float baseGrowthRate = 0.05f;
            float foodBonus = 1f + (waste * 0.1f);
            float homeBonus = 1f + (sandVolumeLiters * 0.05f);

            bacteria += baseGrowthRate * waterVolumeFactor * foodBonus * homeBonus * simDeltaTime;
            bacteria = Mathf.Clamp(bacteria, 0f, maxBacteriaCap);
        }

        float totalProcessingPower = bacteria * bacteriaEfficiency * simDeltaTime;

        if (waste > 0f)
        {
            if (waste <= totalProcessingPower)
            {
                waste = 0f;
                nitrogen = Mathf.MoveTowards(nitrogen, 0f, totalProcessingPower * 0.5f);
            }
            else
            {
                waste -= totalProcessingPower;
                float convertedToxic = waste * 0.25f * simDeltaTime;
                waste -= convertedToxic;
                nitrogen += convertedToxic;
            }
        }
        else
        {
            nitrogen = Mathf.MoveTowards(nitrogen, 0f, totalProcessingPower * 0.2f);
        }

        if (bacteria > 1f && bacteria < maxBacteriaCap * 0.75f)
        {
            float bloomRate = 0.5f;
            turbidity += bloomRate * simDeltaTime;
        }
        else if (bacteria >= maxBacteriaCap * 0.75f)
        {
            float clarityPower = 1.0f;
            turbidity -= clarityPower * simDeltaTime;
        }
        else if (turbidity > 0f)
        {
            turbidity -= 0.1f * simDeltaTime;
        }

        turbidity = Mathf.Clamp(turbidity, 0f, 100f);

        if (nitrogen > 0.05f)
        {
            ph = Mathf.MoveTowards(ph, 5.5f, nitrogen * 0.01f * simDeltaTime);
        }
        else if (currentSandVolume_M3 > 0f || bacteria > 5f)
        {
            ph = Mathf.MoveTowards(ph, targetNaturalPH, 0.005f * simDeltaTime);
        }
        else
        {
            ph = Mathf.MoveTowards(ph, 7.0f, 0.01f * simDeltaTime);
        }
    }

    private void UpdateWaterVisuals()
    {
        if (waterMaterial == null) return;

        float turbidityRatio = turbidity / 100f;

        // ผสมเฉพาะสี RGB ไปส่งให้ Shader
        Color targetColor = Color.Lerp(clearWaterColor, murkyWaterColor, turbidityRatio);

        if (nitrogen > 0f)
        {
            float toxicRatio = Mathf.Clamp01(nitrogen / 20f);
            targetColor = Color.Lerp(targetColor, toxicWaterColor, toxicRatio);
        }

        if (waterMaterial.HasProperty(shaderColorPropertyName))
        {
            waterMaterial.SetColor(shaderColorPropertyName, targetColor);
        }
    }

    public void PassTime(float hoursToSkip)
    {
        if (hoursToSkip <= 0f) return;

        float totalSeconds = hoursToSkip * 3600f;
        float timeSlice = 60f;
        int steps = Mathf.CeilToInt(totalSeconds / timeSlice);
        float remainingTime = totalSeconds;

        for (int i = 0; i < steps; i++)
        {
            float stepCurrent = Mathf.Min(timeSlice, remainingTime);
            ProcessEcosystemStep(stepCurrent);
            remainingTime -= stepCurrent;
        }

        UpdateWaterVisuals();
        Debug.Log($"<color=#f1c40f><b>[TankWaterQuality]</b> ข้ามเวลาไป {hoursToSkip} ชั่วโมง ระบบนิเวศอัปเดตเรียบร้อย!</color>");
    }

    public void AddWaste(float amount)
    {
        if (waterVolumeLiters > 0f && amount > 0f)
        {
            waste += amount;
        }
    }

    public void AddWater(float amount_M3)
    {
        if (amount_M3 <= 0f) return;
        float amountLiters = amount_M3 * 1000f;

        bool isFirstPour = (waterVolumeLiters <= 0f);

        if (salinity > 0f)
        {
            salinity -= (amountLiters * 0.001f);
            if (salinity < 0f) salinity = 0f;
        }

        if (isFirstPour)
        {
            UpdateWaterVisuals();
        }
    }

    public void RemoveWater(float amount_M3) { }

    public void AddSalinity(float amount)
    {
        if (waterVolumeLiters > 0f) salinity += amount;
    }

    public float GetClarityNormalized()
    {
        if (waterVolumeLiters <= 0f) return 1f;
        return 1f - (turbidity / 100f);
    }
    // เพิ่มไว้ล่างสุดใน TankWaterQuality.cs
    public float GetTotalTankVolumeLiters()
    {
        // คำนวณปริมาตรรวมของตู้ (กว้าง x ยาว x สูงสุด * 1000 ลิตร)
        return tankBaseArea * tankMaxHeight * 1000f;
    }
}