using UnityEngine;

public class DropletController : MonoBehaviour
{
    [Header("Auto-Assigned Reference")]
    public WaterSystem waterSystem;
    private SandSimulation sandSystem; // 🚨 🪐 เพิ่มตัวอ้างอิงถึงระบบพื้นทราย

    private Material dropletMaterial;

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

    [Header("⏳ Initial Fall Settings")]
    [Tooltip("ความเร็วในการจมดิ่งลงดินตอนเกิดแรกเริ่ม")]
    public float fallSpeed = 0.8f;

    [Header("🎈 Buoyancy Settings (ระบบลอยตัว)")]
    [Tooltip("ความเร็วสูงสุดในการลอยตัวกลับขึ้นสู่ผิวน้ำ")]
    public float riseSpeed = 0.6f;
    [Tooltip("ความเร็วในการเปลี่ยนทิศทางจากจมเป็นลอยขึ้น (ยิ่งมากยิ่งหักหัวกลับขึ้นไว)")]
    public float buoyancyDamping = 4.0f;
    private float currentVerticalVelocity;

    private float usableWidth;
    private float usableLength;
    private bool hasGridBounds = false;

    void Start()
    {
        if (waterSystem == null)
        {
            waterSystem = FindFirstObjectByType<WaterSystem>();
        }

        if (waterSystem != null)
        {
            SandGridGenerator generator = waterSystem.GetComponent<SandGridGenerator>();
            if (generator != null)
            {
                usableWidth = (generator.gridXCount - 1) * generator.cellSize;
                usableLength = (generator.gridZCount - 1) * generator.cellSize;
                hasGridBounds = true;
            }

            // 🚨 🪐 พยายามค้นหาระบบทรายที่อยู่ในตู้เดียวกัน
            Transform tankParent = waterSystem.transform.parent;
            if (tankParent != null)
            {
                sandSystem = tankParent.GetComponentInChildren<SandSimulation>();
            }
            if (sandSystem == null) sandSystem = FindFirstObjectByType<SandSimulation>();
        }

        dropletMaterial = GetComponent<Renderer>().material;

        float randomScale = Random.Range(minScale, maxScale);
        transform.localScale = new Vector3(randomScale, randomScale, randomScale);

        currentSpreadSpeed = Random.Range(minSpreadSpeed, maxSpreadSpeed);

        float randomX = Random.Range(-1f, 1f);
        float randomZ = Random.Range(-1f, 1f);
        float randomY = Random.Range(-0.1f, 0.2f);

        moveDirection = new Vector3(randomX, randomY, randomZ).normalized;

        currentVerticalVelocity = -fallSpeed;

        Destroy(gameObject, 2.5f);
    }

    void Update()
    {
        currentVerticalVelocity = Mathf.MoveTowards(currentVerticalVelocity, riseSpeed, Time.deltaTime * buoyancyDamping);

        Vector3 currentVelocity = moveDirection * currentSpreadSpeed;
        currentVelocity.y += currentVerticalVelocity;

        transform.position += currentVelocity * Time.deltaTime;

        if (hasGridBounds && waterSystem != null)
        {
            Vector3 localPos = waterSystem.transform.InverseTransformPoint(transform.position);
            if (Mathf.Abs(localPos.x) > usableWidth * 0.5f || Mathf.Abs(localPos.z) > usableLength * 0.5f)
            {
                Destroy(gameObject);
                return;
            }
        }

        currentSpreadSpeed = Mathf.Lerp(currentSpreadSpeed, 0f, Time.deltaTime * 5f);

        transform.localScale -= Vector3.one * dissolveSpeed * Time.deltaTime;
        if (transform.localScale.x <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // 🚨 ตรวจจับการชนทั้งผิวน้ำด้านบน และพื้นทรายด้านล่าง
        if (waterSystem != null)
        {
            // --- 1. คำนวณความสูงผิวน้ำ ---
            float localY = waterSystem.GetHeightAtWorldPos(transform.position);
            float worldWaterY = waterSystem.transform.TransformPoint(new Vector3(0, localY, 0)).y;

            dropletMaterial.SetFloat("_WaterHeight", worldWaterY);

            // แตกเมื่อลอยชนผิวน้ำ
            if (currentVerticalVelocity > 0f && transform.position.y >= worldWaterY - 0.005f)
            {
                Destroy(gameObject);
                return;
            }

            // --- 2. คำนวณความสูงพื้นทราย/ก้นตู้ ---
            float worldSandY = worldWaterY - 0.5f; // ค่าเริ่มต้นเผื่อหาทรายไม่เจอ (ลึก 50 ซม.)
            if (sandSystem != null)
            {
                float localSandY = sandSystem.GetHeightAtWorldPos(transform.position);
                worldSandY = sandSystem.transform.TransformPoint(new Vector3(0, localSandY, 0)).y;
            }

            // 🚨 แตกเมื่อจมลงไปชนพื้นทราย (เผื่อระยะ offset ไว้นิดนึงให้ดูเนียนตา)
            if (transform.position.y <= worldSandY + 0.02f)
            {
                Destroy(gameObject);
                return;
            }
        }
    }
}