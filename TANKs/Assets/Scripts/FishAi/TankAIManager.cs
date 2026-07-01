using UnityEngine;
using System.Collections.Generic;

public class TankAIManager : MonoBehaviour
{
    

    [Header("🔗 References")]
    public SandSimulation sandSim;
    public WaterSystem waterSim;
    public TankWaterQuality waterQuality;

    [Header("🐟 Fish Tracking")]
    public List<FishAI> allFishInTank = new List<FishAI>();

    private void Awake()
    {
   
    }

    public void RegisterFish(FishAI fish)
    {
        if (!allFishInTank.Contains(fish)) allFishInTank.Add(fish);
    }

    public void UnregisterFish(FishAI fish)
    {
        if (allFishInTank.Contains(fish)) allFishInTank.Remove(fish);
    }

    // 🚨 แก้ไขบั๊กสเปซพิกัด: แปลงค่าความสูงของน้ำและทรายให้เป็น World Space ทั้งหมด
    // 🚨 แก้ไขให้ปลาว่ายอยู่ใน "มวลน้ำ" เท่านั้น (ไม่ว่ายบนอากาศ ไม่มุดทราย)
    public Vector3 GetValidWaypoint(SwimZone zone, Vector3 currentPos, float maxRadius)
    {
        GameObject tank = GameObject.FindWithTag("Tank");
        if (tank == null) return currentPos;

        Bounds b = tank.GetComponent<Collider>().bounds;
        Random.InitState((int)(System.DateTime.Now.Ticks + currentPos.x * 1000));

        // 🌟 1. ปลดล็อกข้อจำกัดเวลาน้ำน้อย
        SwimZone effectiveZone = zone;
        if (waterQuality != null)
        {
            // คำนวณความจุน้ำสูงสุดที่ตู้รับได้ (ความจุรวม - ปริมาตรทราย)
            float maxWaterCapacity = waterQuality.GetTotalTankVolumeLiters() - waterQuality.sandVolumeLiters;

            // ถ้าน้ำมีน้อยกว่า 50% ของความจุ ให้บังคับปลาว่ายทั่วตู้ (All) ไปเลย จะได้ไม่อึดอัดอยู่แค่ตรงกลาง
            if (waterQuality.waterVolumeLiters < maxWaterCapacity * 0.5f)
            {
                effectiveZone = SwimZone.All;
            }
        }

        for (int i = 0; i < 5; i++)
        {
            // 🌟 2. กระจายจุดว่ายน้ำให้ออกไปเลาะขอบตู้มากขึ้น
            float margin = 0.05f; // ระยะขอบ (ห่างจากกระจกนิดหน่อยกันชน)
            float randomX = 0f;
            float randomZ = 0f;

            // เพิ่มโอกาส 60% ที่ปลาจะเจาะจงว่ายไปริมกระจกด้านใดด้านหนึ่ง
            if (Random.value < 0.6f)
            {
                if (Random.value < 0.5f)
                {
                    // ชิดขอบซ้าย หรือ ขอบขวา
                    randomX = Random.value < 0.5f ? Random.Range(b.min.x + margin, b.min.x + 0.15f) : Random.Range(b.max.x - 0.15f, b.max.x - margin);
                    randomZ = Random.Range(b.min.z + margin, b.max.z - margin);
                }
                else
                {
                    // ชิดขอบหน้า หรือ ขอบหลัง
                    randomX = Random.Range(b.min.x + margin, b.max.x - margin);
                    randomZ = Random.value < 0.5f ? Random.Range(b.min.z + margin, b.min.z + 0.15f) : Random.Range(b.max.z - 0.15f, b.max.z - margin);
                }
            }
            else
            {
                // โอกาส 40% ว่ายตัดกลางตู้ปกติ
                randomX = Random.Range(b.min.x + margin, b.max.x - margin);
                randomZ = Random.Range(b.min.z + margin, b.max.z - margin);
            }

            Vector3 randomPoint = new Vector3(randomX, currentPos.y, randomZ);

            float worldSandY = sandSim != null ? sandSim.GetHeightAtWorldPos(randomPoint) : 0f;
            float worldWaterY = waterSim != null ? waterSim.GetHeightAtWorldPos(randomPoint) : 0.5f;

            // 🌟 3. ใช้ effectiveZone ที่ผ่านการเช็คระดับน้ำมาแล้ว
            float yPos = 0f;
            switch (effectiveZone)
            {
                case SwimZone.Bottom: yPos = Random.Range(worldSandY + 0.02f, worldSandY + 0.15f); break;
                case SwimZone.Surface: yPos = Random.Range(worldWaterY - 0.08f, worldWaterY - 0.03f); break;
                case SwimZone.Middle: yPos = Mathf.Lerp(worldSandY, worldWaterY, Random.Range(0.3f, 0.7f)); break;
                case SwimZone.All: yPos = Mathf.Lerp(worldSandY, worldWaterY, Random.Range(0.1f, 0.9f)); break;
            }

            // ถ้าระดับ Y ที่สุ่มได้อยู่ในเขตน้ำปลอดภัย ให้ส่งค่านี้กลับไป
            if (yPos > worldSandY + 0.03f && yPos < worldWaterY - 0.02f)
            {
                return new Vector3(randomPoint.x, yPos, randomPoint.z);
            }
        }

        return currentPos;
    }
    public FishAI FindPreyFor(FishAI hunter)
    {
        FishAI bestTarget = null;
        float minDistance = float.MaxValue;

        foreach (FishAI prey in allFishInTank)
        {
            if (prey == hunter || prey.currentState == FishState.Dead) continue;

            if (hunter.currentSize > prey.currentSize * 1.5f || hunter.data.preySpecies.Contains(prey.data.speciesName))
            {
                float dist = Vector3.Distance(hunter.transform.position, prey.transform.position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    bestTarget = prey;
                }
            }
        }
        return bestTarget;
    }

    public float GetTankWidth()
    {
        if (sandSim != null)
        {
            var gridGen = sandSim.GetComponent<SandGridGenerator>();
            if (gridGen != null)
            {
                return (gridGen.gridXCount - 1) * gridGen.cellSize * gridGen.transform.lossyScale.x;
            }
        }
        return 1f;
    }

    public float GetTankLength()
    {
        if (sandSim != null)
        {
            var gridGen = sandSim.GetComponent<SandGridGenerator>();
            if (gridGen != null)
            {
                return (gridGen.gridZCount - 1) * gridGen.cellSize * gridGen.transform.lossyScale.z;
            }
        }
        return 1f;
    }
}