using UnityEngine;
using UnityEngine.InputSystem;

public class ToolManager : MonoBehaviour
{
    public enum CurrentTool { None, Sand, Water }

    [Header("Tool Status")]
    public CurrentTool activeTool = CurrentTool.None;

    // 🚨 🪐 [ปรับปรุงใหญ่] แยกขนาดวงพู่กันรายชิ้นเพื่อให้จูนสเกลการเทแยกกันได้อย่างอิสระ
    [Header("⏳ Sand Brush Settings")]
    public float sandBrushRadius = 0.05f;
    public float sandMinRadius = 0.02f;
    public float sandMaxRadius = 0.2f;
    public float sandSizeStep = 0.01f;
    [Space(5)]
    public float pourSpeed = 3.0f;
    public float vacuumSpeed = 3.0f;

    [Header("🌊 Water Brush Settings")]
    public float waterBrushRadius = 0.15f; // ตั้งค่าเริ่มต้นให้น้ำวงใหญ่กว่าทรายแต่แรกได้เลย
    public float waterMinRadius = 0.05f;
    public float waterMaxRadius = 0.6f;    // ขยาย Max ให้สาดน้ำได้สะใจเต็มตู้ปลา
    public float waterSizeStep = 0.02f;   // ลูกกลิ้งเมาส์ขยับไซส์ไวกว่าทรายเพื่อความลื่นไหล
    [Space(5)]
    public float waterPourSpeed = 1.5f;
    public float waterVacuumSpeed = 1.5f;

    [Header("Sand Settings")]
    [Tooltip("เลข ID ชนิดทรายที่จะเท (0 = ทรายมาตรฐาน, 1 = โคลนหนืด อิงตามพรีเซ็ตใน SandSimulation)")]
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
            // 🚨 🪐 [ระบบตรวจจับกลิ้งเมาส์แยกบรัช] 
            // เช็คว่าปัจจุบันถือส้อมเทอะไรอยู่ แล้วแยกสายคณิตศาสตร์ Clamp ตัวแปรของใครของมันชัดเจน
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
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            // ยิงเลเซอร์เจาะลงพื้นทรายก้นตู้เพื่อหาพิกัดราบ XZ ที่เสถียรที่สุด
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, sandLayer))
            {
                Vector3 interactivePoint = hit.point;
                float currentActiveRadius = 0f; // ตัวแปรพักค่ารัศมีพู่กันเพื่อส่งต่อให้ฟังก์ชันวาดและฟิสิกส์

                // ระบบคำนวณสแนปผิวสามมิติพร้อมคัดเลือกขนาดรัศมีบรัชปัจจุบันมาใช้งาน
                if (activeTool == CurrentTool.Water && waterSystem != null)
                {
                    float localY = waterSystem.GetHeightAtWorldPos(hit.point);
                    Vector3 localPos = waterSystem.transform.InverseTransformPoint(hit.point);
                    localPos.y = localY;
                    interactivePoint = waterSystem.transform.TransformPoint(localPos);

                    currentActiveRadius = waterBrushRadius; // ล็อกใช้ค่ารัศมีพู่กันน้ำ
                }
                else if (activeTool == CurrentTool.Sand && sandSimulation != null)
                {
                    float localY = sandSimulation.GetHeightAtWorldPos(hit.point);
                    Vector3 localPos = sandSimulation.transform.InverseTransformPoint(hit.point);
                    localPos.y = localY;
                    interactivePoint = sandSimulation.transform.TransformPoint(localPos);

                    currentActiveRadius = sandBrushRadius; // ล็อกใช้ค่ารัศมีพู่กันทราย
                }

                brushIndicator.enabled = true;

                // วาดวงกลมพรีวิวตามขนาดของบรัชชนิดนั้นๆ
                DrawPreviewCircle(interactivePoint, currentActiveRadius);

                bool isLeftPressed = Mouse.current.leftButton.isPressed;
                bool isRightPressed = Mouse.current.rightButton.isPressed;

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

            if (activeTool == CurrentTool.Sand && sandSimulation != null)
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

    public void ChangeSandType(int typeIndex)
    {
        if (sandSimulation != null && sandSimulation.sandDatabase != null && sandSimulation.sandDatabase.sandTypes != null)
        {
            selectedSandTypeIndex = Mathf.Clamp(typeIndex, 0, sandSimulation.sandDatabase.sandTypes.Length - 1);
        }
    }
}