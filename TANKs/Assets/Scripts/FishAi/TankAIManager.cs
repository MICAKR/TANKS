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

        for (int i = 0; i < 5; i++)
        {
            // 🌟 เพิ่มการสุ่มแบบกระจายตัว (แทนที่จะสุ่มกลางตู้)
            // ใช้วิธีสุ่มระยะห่างจากขอบ (Margin) มากกว่าสุ่มจากกึ่งกลาง
            float margin = 0.2f;
            float randomX = Random.value < 0.5f ? Random.Range(b.min.x + 0.1f, b.min.x + margin) : Random.Range(b.max.x - margin, b.max.x - 0.1f);
            float randomZ = Random.value < 0.5f ? Random.Range(b.min.z + 0.1f, b.min.z + margin) : Random.Range(b.max.z - margin, b.max.z - 0.1f);

            // บางครั้งสุ่มกลางตู้บ้าง (20% ของเวลา) เพื่อให้มันว่ายตัดบ้าง
            if (Random.value < 0.2f)
            {
                randomX = Random.Range(b.min.x + 0.2f, b.max.x - 0.2f);
                randomZ = Random.Range(b.min.z + 0.2f, b.max.z - 0.2f);
            }

            Vector3 randomPoint = new Vector3(randomX, currentPos.y, randomZ);

            float worldSandY = sandSim != null ? sandSim.GetHeightAtWorldPos(randomPoint) : 0f;
            float worldWaterY = waterSim != null ? waterSim.GetHeightAtWorldPos(randomPoint) : 0.5f;

            // คำนวณ Y ตามโซน
            float yPos = 0f;
            switch (zone)
            {
                case SwimZone.Bottom: yPos = Random.Range(worldSandY + 0.05f, worldSandY + 0.2f); break;
                case SwimZone.Surface: yPos = Random.Range(worldWaterY - 0.2f, worldWaterY - 0.05f); break;
                case SwimZone.Middle: yPos = Mathf.Lerp(worldSandY, worldWaterY, Random.Range(0.4f, 0.6f)); break;

                case SwimZone.All: yPos = Mathf.Lerp(worldSandY, worldWaterY, Random.Range(0.1f, 0.9f)); break;
            }

            if (yPos > worldSandY + 0.05f && yPos < worldWaterY - 0.05f)
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