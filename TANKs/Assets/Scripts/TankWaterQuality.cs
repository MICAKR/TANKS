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
    [Tooltip("อัปเดตความเปลี่ยนแปลงทุกๆ กี่วินาทีในชีวิตจริง? (แนะนำ 1-2 วิ เพื่อประหยัด CPU)")]
    public float simulationInterval = 1.0f;
    private float realTickTimer = 0f;

    [Header("📈 เกณฑ์ระบบนิเวศมาตรฐาน")]
    public float maxBacteriaCap = 100f;
    public float targetNaturalPH = 7.2f;

    [Header("🦠 ความเร็วระบบนิเวศ (หน่วย: ต่อ 1 ชั่วโมงในเกม)")]
    [Tooltip("แบคทีเรียเพิ่มขึ้นกี่หน่วย ต่อ 1 ชั่วโมงในเกม")]
    public float baseBacteriaGrowthRate = 0.5f;

    [Tooltip("ของเสียลดลงกี่หน่วย ต่อ 1 ชั่วโมงในเกม (ถ้ามีแบคทีเรียช่วยย่อย)")]
    public float bacteriaEfficiency = 0.1f;

    [Header("🎨 การแสดงผลสีน้ำ (Water Visual Settings)")]
    public Color clearWaterColor = new Color(0.02f, 0.05f, 0.15f);
    public Color murkyWaterColor = new Color(0.5f, 0.4f, 0.2f);
    public Color toxicWaterColor = new Color(0.2f, 0.5f, 0.2f);

    [Space(10)]
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
            if (mr != null) waterMaterial = mr.material;
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
        realTickTimer = 0f;

        UpdateWaterVisuals();
    }

    void Update()
    {
        float safeDeltaTime = Mathf.Min(Time.deltaTime, 0.1f);
        realTickTimer += safeDeltaTime;

        if (realTickTimer >= simulationInterval)
        {
            UpdatePhysicalVolumes();

            float currentSpeed = (TimeManager.Instance != null) ? TimeManager.Instance.timeScale : 1.0f;
            float inGameHoursPassed = (realTickTimer * currentSpeed) / 3600f;

            if (inGameHoursPassed > 1.0f)
            {
                PassTime(inGameHoursPassed);
            }
            else
            {
                ProcessEcosystemStep(inGameHoursPassed);
            }

            UpdateWaterVisuals();

            realTickTimer = 0f;
        }
    }

    private void UpdatePhysicalVolumes()
    {
        if (gridGen == null) return;

        Vector3 scale = gridGen.transform.lossyScale;
        int mainVertCount = gridGen.gridXCount * gridGen.gridZCount;
        float exactCellArea = tankBaseArea / mainVertCount;

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
                    float sandHeightAtVertex = (sVerts != null && i < sVerts.Length) ? sVerts[i].y : 0f;
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

    // 🚨 ฟังก์ชันใหม่: คำนวณ "แฟกเตอร์การเจือจาง"
    // ถ้าปริมาณน้ำเท่ากับ 35 ลิตร (ตู้มาตรฐาน) ค่าจะเท่ากับ 1.0 (แกว่งปกติ)
    // ถ้าน้ำน้อยกว่า 35 ลิตร ค่าจะ > 1.0 (เช่น ตู้ 7 ลิตร จะแกว่งไว 5 เท่า)
    // ถ้าน้ำมากกว่า 35 ลิตร ค่าจะ < 1.0 (เช่น ตู้ 70 ลิตร จะเสถียรขึ้น 2 เท่า)
    private float GetDilutionFactor()
    {
        if (waterVolumeLiters <= 0f) return 1f;
        return Mathf.Clamp(35f / waterVolumeLiters, 0.1f, 10f);
    }

    [Header("🤢 ระบบของเสียและสารพิษ (Waste & Nitrogen System)")]
    public float waste = 0f;
    public float nitrogen = 0f;

    private void ProcessEcosystemStep(float inGameHours)
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

        float dilutionFactor = GetDilutionFactor();
        float waterVolumeFactor = Mathf.Clamp(waterVolumeLiters / 35f, 0.2f, 4.0f);

        // 1. ย่อยสลายของเสีย
        float totalProcessingPower = bacteria * bacteriaEfficiency * inGameHours;

        if (waste > 0f)
        {
            float processedWaste = Mathf.Min(waste, totalProcessingPower);
            waste -= processedWaste;
            nitrogen += processedWaste * 0.5f;
        }
        else
        {
            nitrogen = Mathf.MoveTowards(nitrogen, 0f, totalProcessingPower * 0.2f);
        }

        // 2. แบคทีเรียเติบโต & อดอาหาร
        if (waste > 0f)
        {
            if (bacteria < maxBacteriaCap)
            {
                float activeBacteria = bacteria > 0f ? bacteria : 0.1f;
                float foodBonus = 1f + (waste * 0.05f);
                float homeBonus = 1f + (sandVolumeLiters * 0.01f);
                float populationBonus = 1f + (activeBacteria * 0.01f);

                bacteria += baseBacteriaGrowthRate * waterVolumeFactor * foodBonus * homeBonus * populationBonus * inGameHours;
                bacteria = Mathf.Clamp(bacteria, 0f, maxBacteriaCap);
            }
        }
        else
        {
            if (bacteria > 5.0f)
            {
                float starveRate = baseBacteriaGrowthRate * 0.2f;
                bacteria -= starveRate * inGameHours;
                bacteria = Mathf.Max(bacteria, 5.0f);
            }
        }

        // 3. ความขุ่นของน้ำ
        float targetTurbidity = Mathf.Clamp(waste * 2f, 0f, 30f);

        float bloomRatio = bacteria / maxBacteriaCap;
        if (bloomRatio > 0.05f && bloomRatio < 0.75f)
        {
            float bellCurve = Mathf.Sin(Mathf.InverseLerp(0.05f, 0.75f, bloomRatio) * Mathf.PI);
            targetTurbidity += bellCurve * 60f;
        }

        // 🚨 น้ำน้อยจะขุ่นไวและใสไวกว่าน้ำเยอะ (คูณ dilutionFactor)
        float turbidityChangeSpeed = 5.0f * dilutionFactor;
        turbidity = Mathf.MoveTowards(turbidity, targetTurbidity, turbidityChangeSpeed * inGameHours);
        turbidity = Mathf.Clamp(turbidity, 0f, 100f);

        // 4. การจัดการค่า pH
        if (nitrogen > 0.05f)
        {
            // 🚨 ถ้าไนโตรเจนสูง pH จะร่วง (ตู้เล็ก pH ร่วงง่ายและไวกว่า)
            float phDrop = Mathf.Clamp(nitrogen * 0.01f, 0f, 0.05f * dilutionFactor);
            ph = Mathf.MoveTowards(ph, 5.5f, phDrop * inGameHours);
        }
        else if (currentSandVolume_M3 > 0f || bacteria > 5f)
        {
            // 🚨 ตู้เล็ก ฟื้นฟู pH ไวกว่า
            ph = Mathf.MoveTowards(ph, targetNaturalPH, 0.02f * dilutionFactor * inGameHours);
        }
        else
        {
            ph = Mathf.MoveTowards(ph, 7.0f, 0.02f * dilutionFactor * inGameHours);
        }
    }

    private void UpdateWaterVisuals()
    {
        if (waterMaterial == null) return;

        float turbidityRatio = turbidity / 100f;
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

        int steps = Mathf.CeilToInt(hoursToSkip);
        float remainingHours = hoursToSkip;

        for (int i = 0; i < steps; i++)
        {
            float stepHours = Mathf.Min(1.0f, remainingHours);
            ProcessEcosystemStep(stepHours);
            remainingHours -= stepHours;
        }

        UpdateWaterVisuals();
        Debug.Log($"<color=#f1c40f><b>[TankWaterQuality]</b> คำนวณข้ามเวลา {hoursToSkip:F2} ชั่วโมงในเกมเสร็จสิ้น</color>");
    }

    // ==========================================
    // 🧪 การเพิ่มมวลสารและของเสียต่างๆ
    // ==========================================

    public void AddWaste(float amount)
    {
        if (waterVolumeLiters > 0f && amount > 0f)
        {
            // 🚨 ยิ่งตู้เล็ก ของเสียยิ่งเข้มข้นเร็ว
            waste += amount * GetDilutionFactor();
        }
    }

    public void AddSalinity(float amount)
    {
        if (waterVolumeLiters > 0f)
        {
            // 🚨 ยิ่งตู้เล็ก ค่าความเค็มยิ่งเข้มข้นและแกว่งไว
            salinity += amount * GetDilutionFactor();
        }
    }

    public void AddStarterBacteria(float amount)
    {
        if (waterVolumeLiters <= 0f)
        {
            Debug.LogWarning("[TankWaterQuality] ตู้ยังไม่มีน้ำ เติมจุลินทรีย์ไม่ได้!");
            return;
        }

        // 🚨 ยิ่งตู้เล็ก แบคทีเรียยิ่งลามทั่วตู้ไว
        bacteria += amount * GetDilutionFactor();
        bacteria = Mathf.Clamp(bacteria, 0f, maxBacteriaCap);

        Debug.Log($"<color=lime>🧪 เติมจุลินทรีย์ตั้งต้น +{amount}! (ค่าปัจจุบัน: {bacteria:F1} / {maxBacteriaCap})</color>");
    }

    public void AddWater(float amount_M3)
    {
        if (amount_M3 <= 0f) return;
        float amountLiters = amount_M3 * 1000f;

        bool isFirstPour = (waterVolumeLiters <= 0f);

        // 🚨 ระบบจำลองการเปลี่ยนน้ำ (Water Change)
        // เมื่อเติมน้ำจืดใหม่ลงไป จะทำให้สารต่างๆ ที่ละลายอยู่ในตู้เกิดการ "เจือจาง" ลดลง!
        if (waterVolumeLiters > 0f)
        {
            float newTotalVolume = waterVolumeLiters + amountLiters;
            float diluteRatio = waterVolumeLiters / newTotalVolume; // สัดส่วนน้ำเก่าต่อน้ำรวม

            waste *= diluteRatio;
            nitrogen *= diluteRatio;
            salinity *= diluteRatio;
            turbidity *= diluteRatio; // เติมน้ำใหม่ปุ๊บ น้ำจะใสขึ้นทันที!
        }

        UpdatePhysicalVolumes();

        if (isFirstPour)
        {
            bacteria = 5.0f;
            UpdateWaterVisuals();
        }
    }

    public void RemoveWater(float amount_M3) { }

    public float GetClarityNormalized()
    {
        if (waterVolumeLiters <= 0f) return 1f;
        return 1f - (turbidity / 100f);
    }

    public float GetTotalTankVolumeLiters()
    {
        return tankBaseArea * tankMaxHeight * 1000f;
    }
}