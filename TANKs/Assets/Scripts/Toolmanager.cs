using UnityEngine;
using UnityEngine.InputSystem;

public class ToolManager : MonoBehaviour
{
    public enum CurrentTool { None, Sand, Water, Smooth }

    [Header("Tool Status")]
    public CurrentTool activeTool = CurrentTool.None;

    [Header("⏳ Sand Brush Settings (เททราย)")]
    public float sandBrushRadius = 0.05f;
    public float sandMinRadius = 0.02f;
    public float sandMaxRadius = 0.2f;
    public float sandSizeStep = 0.01f;
    [Space(5)]
    public float pourSpeed = 3.0f;
    public float vacuumSpeed = 3.0f;

    [Header("🌊 Water Brush Settings (เทน้ำ)")]
    public float waterBrushRadius = 0.15f;
    public float waterMinRadius = 0.05f;
    public float waterMaxRadius = 0.6f;
    public float waterSizeStep = 0.02f;
    [Space(5)]
    public float waterPourSpeed = 1.5f;
    public float waterVacuumSpeed = 1.5f;

    // 🚨 🪐 [เพิ่มและปรับค่าความแรงเกลี่ยทรายใหม่ให้สูงขึ้น]
    [Header("✨ Smooth Brush Settings (เกลี่ยหน้าดิน)")]
    public float smoothBrushRadius = 0.08f;
    public float smoothMinRadius = 0.02f;
    public float smoothMaxRadius = 0.3f;
    public float smoothSizeStep = 0.01f;
    [Tooltip("ความแรงในการเกลี่ย (ยิ่งเยอะเนื้อทรายยิ่งยุบราบไว) *ต้องใช้เลขเยอะๆ ถึงจะเห็นผล*")]
    // 🚨 ปรับจาก 2.0 เป็น 100.0 เพื่อให้เห็นผลทันที!!! (สามารถปรับลดได้ใน Inspector)
    public float smoothStrength = 100.0f;

    [Header("Sand Settings")]
    [Tooltip("เลข ID ชนิดทรายที่จะเท (0 = ทรายมาตรฐาน)")]
    public int selectedSandTypeIndex = 0;

    [Header("Simulation References")]
    public SandSimulation sandSimulation;
    public WaterSystem waterSystem;

    [Header("Layer Settings")]
    public LayerMask sandLayer;
    public LayerMask waterLayer;

    [Header("Visual Indicator (วงกลมพู่กัน)")]
    public Color indicatorColor = Color.cyan;
    public float indicatorLineWidth = 0.005f;

    [Header("📦 Physical Dust Settings")]
    [Tooltip("ลาก Prefab เม็ดทรายที่มี Rigidbody มาใส่ช่องนี้")]
    public GameObject sandDustPrefab;
    [Tooltip("ความหนาแน่นในการเสกต่อเฟรม (ยิ่งเยอะเม็ดทรายยิ่งพูนสะใจ)")]
    public int spawnDensityPerFrame = 3;

    private LineRenderer brushIndicator;
    private int circleSegments = 36;

    void Start()
    {
        SetupBrushIndicator();
    }

    void Update()
    {
        if (activeTool != CurrentTool.None && Mouse.current != null)
        {
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                if (activeTool == CurrentTool.Sand)
                {
                    sandBrushRadius += Mathf.Sign(scrollY) * sandSizeStep;
                    sandBrushRadius = Mathf.Clamp(sandBrushRadius, sandMinRadius, sandMaxRadius);
                }
                else if (activeTool == CurrentTool.Water)
                {
                    waterBrushRadius += Mathf.Sign(scrollY) * waterSizeStep;
                    waterBrushRadius = Mathf.Clamp(waterBrushRadius, waterMinRadius, waterMaxRadius);
                }
                else if (activeTool == CurrentTool.Smooth)
                {
                    smoothBrushRadius += Mathf.Sign(scrollY) * smoothSizeStep;
                    smoothBrushRadius = Mathf.Clamp(smoothBrushRadius, smoothMinRadius, smoothMaxRadius);
                }
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sandLayer))
            {
                Vector3 interactivePoint = hit.point;
                float currentActiveRadius = 0f;

                if (activeTool == CurrentTool.Water && waterSystem != null)
                {
                    float localY = waterSystem.GetHeightAtWorldPos(hit.point);
                    Vector3 localPos = waterSystem.transform.InverseTransformPoint(hit.point);
                    localPos.y = localY;
                    interactivePoint = waterSystem.transform.TransformPoint(localPos);

                    currentActiveRadius = waterBrushRadius;
                }
                else if (activeTool == CurrentTool.Sand && sandSimulation != null)
                {
                    float localY = sandSimulation.GetHeightAtWorldPos(hit.point);
                    Vector3 localPos = sandSimulation.transform.InverseTransformPoint(hit.point);
                    localPos.y = localY;
                    interactivePoint = sandSimulation.transform.TransformPoint(localPos);

                    currentActiveRadius = sandBrushRadius;
                }
                else if (activeTool == CurrentTool.Smooth && sandSimulation != null)
                {
                    float localY = sandSimulation.GetHeightAtWorldPos(hit.point);
                    Vector3 localPos = sandSimulation.transform.InverseTransformPoint(hit.point);
                    localPos.y = localY;
                    interactivePoint = sandSimulation.transform.TransformPoint(localPos);

                    currentActiveRadius = smoothBrushRadius;
                }

                brushIndicator.enabled = true;
                DrawPreviewCircle(interactivePoint, currentActiveRadius);

                bool isLeftPressed = Mouse.current.leftButton.isPressed;
                bool isRightPressed = Mouse.current.rightButton.isPressed;

                if (isLeftPressed && activeTool == CurrentTool.Sand && sandDustPrefab != null)
                {
                    for (int i = 0; i < spawnDensityPerFrame; i++)
                    {
                        Vector2 randomCircle = Random.insideUnitCircle * currentActiveRadius;
                        Vector3 spawnPos = new Vector3(
                            interactivePoint.x + randomCircle.x,
                            interactivePoint.y + 0.08f,
                            interactivePoint.z + randomCircle.y
                        );

                        GameObject dust = Instantiate(sandDustPrefab, spawnPos, Quaternion.identity);

                        Rigidbody rb = dust.GetComponent<Rigidbody>();
                        if (rb != null)
                        {
                            rb.linearVelocity = new Vector3(Random.Range(-0.1f, 0.1f), -1f, Random.Range(-0.1f, 0.1f));
                        }
                    }
                }

                if (isLeftPressed)
                {
                    if (activeTool == CurrentTool.Sand && sandSimulation != null)
                    {
                        sandSimulation.PourSand(interactivePoint, selectedSandTypeIndex, currentActiveRadius, pourSpeed);
                    }
                    else if (activeTool == CurrentTool.Water && waterSystem != null)
                    {
                        waterSystem.PourWater(interactivePoint, currentActiveRadius, waterPourSpeed);
                    }
                    else if (activeTool == CurrentTool.Smooth && sandSimulation != null)
                    {
                        // 🚨 🪐 [แก้ไขจุดนี้] ส่งค่า `smoothStrength` (100.0) แทนค่าเดิม
                        sandSimulation.SmoothSand(interactivePoint, currentActiveRadius, smoothStrength);
                    }
                }
                else if (isRightPressed)
                {
                    if (activeTool == CurrentTool.Sand && sandSimulation != null)
                    {
                        sandSimulation.VacuumSand(interactivePoint, currentActiveRadius, vacuumSpeed);
                    }
                    else if (activeTool == CurrentTool.Water && waterSystem != null)
                    {
                        waterSystem.VacuumWater(interactivePoint, currentActiveRadius, waterVacuumSpeed);
                    }
                }
                return;
            }
        }

        if (brushIndicator != null) brushIndicator.enabled = false;
    }

    private void SetupBrushIndicator()
    {
        brushIndicator = gameObject.GetComponent<LineRenderer>();
        if (brushIndicator == null)
        {
            brushIndicator = gameObject.AddComponent<LineRenderer>();
        }

        brushIndicator.positionCount = circleSegments + 1;
        brushIndicator.useWorldSpace = true;
        brushIndicator.startWidth = indicatorLineWidth;
        brushIndicator.endWidth = indicatorLineWidth;

        Material whiteDiffuseMat = new Material(Shader.Find("Sprites/Default"));
        brushIndicator.material = whiteDiffuseMat;

        brushIndicator.startColor = indicatorColor;
        brushIndicator.endColor = indicatorColor;
        brushIndicator.enabled = false;
    }

    private void DrawPreviewCircle(Vector3 center, float radius)
    {
        float angle = 0f;
        for (int i = 0; i <= circleSegments; i++)
        {
            float x = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
            Vector3 worldPoint = new Vector3(center.x + x, center.y, center.z + z);

            float exactY = center.y;

            if ((activeTool == CurrentTool.Sand || activeTool == CurrentTool.Smooth) && sandSimulation != null)
            {
                float localY = sandSimulation.GetHeightAtWorldPos(worldPoint);
                exactY = sandSimulation.transform.TransformPoint(new Vector3(0, localY, 0)).y;
            }
            else if (activeTool == CurrentTool.Water && waterSystem != null)
            {
                float localY = waterSystem.GetHeightAtWorldPos(worldPoint);
                exactY = waterSystem.transform.TransformPoint(new Vector3(0, localY, 0)).y;
            }

            Vector3 pointPos = new Vector3(worldPoint.x, exactY + 0.005f, worldPoint.z);
            brushIndicator.SetPosition(i, pointPos);
            angle += (360f / circleSegments);
        }
    }

    public void ClickSandTool() { activeTool = (activeTool == CurrentTool.Sand) ? CurrentTool.None : CurrentTool.Sand; }
    public void ClickWaterTool() { activeTool = (activeTool == CurrentTool.Water) ? CurrentTool.None : CurrentTool.Water; }
    public void ClickSmoothTool() { activeTool = (activeTool == CurrentTool.Smooth) ? CurrentTool.None : CurrentTool.Smooth; }

    public void ChangeSandType(int typeIndex)
    {
        if (sandSimulation != null && sandSimulation.sandDatabase != null && sandSimulation.sandDatabase.sandTypes != null)
        {
            selectedSandTypeIndex = Mathf.Clamp(typeIndex, 0, sandSimulation.sandDatabase.sandTypes.Length - 1);
        }
    }
}