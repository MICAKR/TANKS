using UnityEngine;

[CreateAssetMenu(fileName = "New Sand Database", menuName = "Aquarium/Sand Database")]
public class SandDatabase : ScriptableObject
{
    [System.Serializable]
    public struct SandType
    {
        public string sandName;          // ชื่อเรียกชนิดทราย
        public float maxSlopeAngle;     // มุมชันสูงสุด
        public float flowRate;          // อัตราความเร็วในการไหลถล่ม
        public Color sandColor;         // สีประจำตัวทรายชนิดนี้

        // 🚨 🪐 [ฟีเจอร์ใหม่] คุมระยะการกองรวมตัวของทราย
        [Tooltip("แรงเสียดทานสถิต ยิ่งเยอะทรายยิ่งกองรวมกันเป็นเนินหนาได้สะใจ ก่อนจะเริ่มสไลด์ถล่ม (แนะนำ 0.003 - 0.012)")]
        public float staticFriction;
    }

    [Header("🌐 Global Simulation Settings (ควบคุมระบบอัปเดตฟิสิกส์)")]
    public float simulationInterval = 0.03f;
    public float maxHeight = 1.0f;
    public Color baseDefaultColor = new Color(0.8f, 0.8f, 0.8f, 1f);

    [Header("🌊 Global Wet Sand Settings (ควบคุมระบบทรายเปียก)")]
    public float drySpeed = 0.1f;
    [Range(0.3f, 0.8f)]
    public float wetDarknessMultiplier = 0.55f;

    [Header("🚨 Sand Presets Registry")]
    public SandType[] sandTypes = new SandType[]
    {
        // ตั้งค่าพรีเซ็ตแรงเสียดทานเริ่มต้นให้เหมาะกับสเกลตู้ปลาของคุณเรียบร้อยครับ
        new SandType { sandName = "White Sand", maxSlopeAngle = 0.25f, flowRate = 25f, sandColor = Color.white, staticFriction = 0.005f },
        new SandType { sandName = "Blue Sand", maxSlopeAngle = 0.3f, flowRate = 20f, sandColor = Color.blue, staticFriction = 0.006f },
        new SandType { sandName = "Heavy Mud", maxSlopeAngle = 0.5f, flowRate = 10f, sandColor = Color.black, staticFriction = 0.015f }
    };
}