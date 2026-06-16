using UnityEngine;
using UnityEngine.InputSystem;

public class ToolManager : MonoBehaviour
{
    public enum CurrentTool { None, PourSand, VacuumSand }

    [Header("Tool Status")]
    public CurrentTool activeTool = CurrentTool.None;

    [Header("Brush Settings")]
    public float brushRadius = 0.1f; // แนะนำ 0.05 - 0.2 สำหรับตู้ 20 นิ้ว
    public float pourSpeed = 3.0f;
    public float vacuumSpeed = 3.0f;

    [Header("Sand Settings")]
    public Color[] sandColors;
    public int selectedColorIndex = 0;

    [Header("Visual Indicator (วงกลมพู่กัน)")]
    public Color indicatorColor = Color.cyan; // สีของเส้นพู่กัน
    public float indicatorLineWidth = 0.005f; // ความหนาของเส้น

    private LineRenderer brushIndicator;
    private int circleSegments = 36; // ความเนียนของวงกลม (จำนวนเหลี่ยม)

    void Start()
    {
        // สร้างระบบวาดเส้นวงกลมพรีวิวขึ้นมาอัตโนมัติเมื่อเริ่มเกม
        SetupBrushIndicator();
    }

    void Update()
    {
        // 1. ระบบตรวจจับพิกัดเมาส์ตลอดเวลา (เพื่อแสดงวงกลมพรีวิว แม้ไม่ได้กดคลิก)
        if (activeTool != CurrentTool.None && Mouse.current != null)
        {
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(mousePos);

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                SandSimulation sandSim = hit.collider.GetComponent<SandSimulation>();

                if (sandSim != null)
                {
                    // แสดงวงกลมและวาดตามขนาดรัศมีพู่กันจริง
                    brushIndicator.enabled = true;
                    DrawPreviewCircle(hit.point, brushRadius);

                    // 2. ถ้าผู้เล่นกดคลิกซ้ายค้างไว้ ให้เริ่มทำงาน (เท/ดูด ทราย)
                    if (Mouse.current.leftButton.isPressed)
                    {
                        if (activeTool == CurrentTool.PourSand)
                        {
                            sandSim.PourSand(hit.point, selectedColorIndex, brushRadius, pourSpeed);
                        }
                        else if (activeTool == CurrentTool.VacuumSand)
                        {
                            sandSim.VacuumSand(hit.point, brushRadius, vacuumSpeed);
                        }
                    }
                    return;
                }
            }
        }

        // ถ้าไม่ได้เลือกเครื่องมือ หรือเมาส์หลุดออกจากตู้ปลา ให้ซ่อนวงกลมพรีวิว
        if (brushIndicator != null) brushIndicator.enabled = false;
    }

    // ฟังก์ชันสร้าง LineRenderer อัตโนมัติในสคริปต์
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

        // สร้าง Material เปล่าๆ สีขาวเพื่อให้ระบายสีเส้นได้
        Material whiteDiffuseMat = new Material(Shader.Find("Sprites/Default"));
        brushIndicator.material = whiteDiffuseMat;

        // ตั้งค่าสีเส้นพู่กัน
        brushIndicator.startColor = indicatorColor;
        brushIndicator.endColor = indicatorColor;
        brushIndicator.enabled = false;
    }

    // ฟังก์ชันคำนวณคณิตศาสตร์วาดวงกลม 360 องศารอบจุดที่เมาส์ชี้
    private void DrawPreviewCircle(Vector3 center, float radius)
    {
        float angle = 0f;
        for (int i = 0; i <= circleSegments; i++)
        {
            float x = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
            float z = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;

            // วาดให้เส้นลอยเหนือผิวทรายนิดนึง (0.01) เส้นจะได้ไม่จมหายลงไปในเนื้อทราย
            Vector3 pointPos = new Vector3(center.x + x, center.y + 0.01f, center.z + z);
            brushIndicator.SetPosition(i, pointPos);

            angle += (360f / circleSegments);
        }
    }

    public void ClickPourSandTool()
    {
        if (activeTool == CurrentTool.PourSand)
        {
            activeTool = CurrentTool.None;
            Debug.Log("ยกเลิกเครื่องมือ: กลับไปเมาส์ปกติ");
        }
        else
        {
            activeTool = CurrentTool.PourSand;
            Debug.Log("เปิดเครื่องมือ: เททราย");
        }
    }

    public void ClickVacuumSandTool()
    {
        if (activeTool == CurrentTool.VacuumSand)
        {
            activeTool = CurrentTool.None;
            Debug.Log("ยกเลิกเครื่องมือ: กลับไปเมาส์ปกติ");
        }
        else
        {
            activeTool = CurrentTool.VacuumSand;
            Debug.Log("เปิดเครื่องมือ: ดูดทราย");
        }
    }

    public void ChangeSandColor(int colorIndex)
    {
        selectedColorIndex = Mathf.Clamp(colorIndex, 0, sandColors.Length - 1);
        Debug.Log("เปลี่ยนสีทรายเป็น Index: " + selectedColorIndex);
    }
}