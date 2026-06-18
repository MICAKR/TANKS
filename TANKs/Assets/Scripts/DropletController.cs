using UnityEngine;

public class DropletController : MonoBehaviour
{
    [Header("Auto-Assigned Reference")]
    public WaterSystem waterSystem;
    private Material dropletMaterial;

    // 🚨 🪐 [ตัวแปรคุมแรงกระจายตัวแผ่ออกข้าง]
    private Vector3 moveDirection;
    private float currentSpreadSpeed;

    [Header("🎲 Spread Settings")]
    public float minScale = 0.02f;
    public float maxScale = 0.06f;
    [Tooltip("ความเร็วในการพุ่งกระจายออกข้าง")]
    public float minSpreadSpeed = 0.5f;
    public float maxSpreadSpeed = 2.0f;
    [Tooltip("ความเร็วในการหดตัวละลายกลืนไปกับผิวน้ำ")]
    public float dissolveSpeed = 0.05f;

    // 🚨 🪐 [เพิ่มตัวแปรคุมให้ค่อยๆ ลอยตกลงมา]
    [Tooltip("ความเร็วในการลอยตกลงสู่ผิวน้ำ (ยิ่งเยอะยิ่งร่วงไว)")]
    public float fallSpeed = 0.8f;

    // 🚨 🪐 [ตัวแปรคำนวณพื้นที่ขอบเขตตู้ปลา]
    private float usableWidth;
    private float usableLength;
    private bool hasGridBounds = false;

    void Start()
    {
        if (waterSystem == null)
        {
            waterSystem = FindFirstObjectByType<WaterSystem>();
        }

        // 🚨 🪐 [ระบบคำนวณหาขอบเขตตู้ปลาอัตโนมัติ]
        if (waterSystem != null)
        {
            // วิ่งไปขอข้อมูลขนาด Grid จากคอมโพเนนต์เจนฉากมาคำนวณขอบตู้กระจกแบบ Real-time
            SandGridGenerator generator = waterSystem.GetComponent<SandGridGenerator>();
            if (generator != null)
            {
                usableWidth = (generator.gridXCount - 1) * generator.cellSize;
                usableLength = (generator.gridZCount - 1) * generator.cellSize;
                hasGridBounds = true;
            }
        }

        dropletMaterial = GetComponent<Renderer>().material;

        // 1. สุ่มขนาดเริ่มต้น
        float randomScale = Random.Range(minScale, maxScale);
        transform.localScale = new Vector3(randomScale, randomScale, randomScale);

        // 2. สุ่มความเร็วแผ่ออกข้าง
        currentSpreadSpeed = Random.Range(minSpreadSpeed, maxSpreadSpeed);

        // 3. สุ่มทิศทางแบนราบ (รอบทิศทาง 360 องศา)
        float randomX = Random.Range(-1f, 1f);
        float randomZ = Random.Range(-1f, 1f);
        float randomY = Random.Range(-0.1f, 0.2f);

        moveDirection = new Vector3(randomX, randomY, randomZ).normalized;

        // ทำลายตัวเองทิ้งหลังผ่านไป 2 วินาที เผื่อสคริปต์หดตัวทำงานไม่ทัน
        Destroy(gameObject, 2f);
    }

    void Update()
    {
        // 4. ⚡ ขยับตำแหน่งพุ่งกระจายออกไป + ค่อยๆ ลอยตกลงมา
        Vector3 currentVelocity = moveDirection * currentSpreadSpeed;
        currentVelocity.y -= fallSpeed; // ใส่แรงดึงลงไปในแกน Y เพื่อให้ค่อยๆ ร่วง

        transform.position += currentVelocity * Time.deltaTime;

        // 🚨 🪐 [ระบบตรวจจับการหลุดออกนอกขอบเขตน้ำ/ขอบตู้ปลา]
        if (hasGridBounds && waterSystem != null)
        {
            // แปลงพิกัดโลกของหยดน้ำให้กลายเป็นพิกัดภายในตู้ปลา (Local Space)
            Vector3 localPos = waterSystem.transform.InverseTransformPoint(transform.position);

            // เช็คแกน X และแกน Z ว่าหลุดออกจากรัศมีครึ่งหนึ่งของบ่อทราย/บ่อน้ำหรือไม่
            if (Mathf.Abs(localPos.x) > usableWidth * 0.5f || Mathf.Abs(localPos.z) > usableLength * 0.5f)
            {
                // ถ้าหลุดขอบกระจกปุ๊บ สั่งลบตัวเองทิ้งทันทีในเฟรมนั้นเลย!
                Destroy(gameObject);
                return;
            }
        }

        // 5. ระบบแรงหนืดน้ำ ชะลอความเร็วการแผ่ออกข้างลงเรื่อยๆ
        currentSpreadSpeed = Mathf.Lerp(currentSpreadSpeed, 0f, Time.deltaTime * 5f);

        // 6. ระบบละลาย ค่อยๆ ยุบสเกลลงจนเหลือ 0 แล้วทำลายตัวเองทิ้ง
        transform.localScale -= Vector3.one * dissolveSpeed * Time.deltaTime;

        if (transform.localScale.x <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 7. จัดการส่งค่าให้ Shader ล่องหนใต้น้ำเหมือนเดิม
        if (waterSystem != null)
        {
            float localY = waterSystem.GetHeightAtWorldPos(transform.position);
            float worldWaterY = waterSystem.transform.TransformPoint(new Vector3(0, localY, 0)).y;

            dropletMaterial.SetFloat("_WaterHeight", worldWaterY);

            // เผื่อหยดน้ำมุดลงไปลึกเกิน 5 ซม. ก็ให้เคลียร์ทิ้งเลย
            if (transform.position.y < worldWaterY - 0.05f)
            {
                Destroy(gameObject);
            }
        }
    }
}