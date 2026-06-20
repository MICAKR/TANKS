using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class CameraModeController : MonoBehaviour
{
    [Tooltip("ลาก Object ที่มีสคริปต์ ToolManager มาใส่ที่นี่")]
    public ToolManager toolManagerRef;

    [Header("UI Manager")]
    [Tooltip("ลาก Object ที่มีสคริปต์ DecorationUIManager มาใส่ที่นี่")]
    public DecorationUIManager decorationUI;

    [Header("Mode Objects")]
    public GameObject playerCharacter;
    public Camera mainCamera;

    [Header("References")]
    public ComputerClickable computerRef;

    [Header("Highlight Settings")]
    public Color highlightColor = new Color(1f, 1f, 1f, 0.5f);
    private Renderer hoveredRenderer;
    private Color originalColor;

    [Header("Tank Selection")]
    public Transform selectedTank;

    [Header("Decoration Camera Settings")]
    public float orbitSpeed = 0.5f;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0.3f;
    public float maxZoom = 2.0f;
    public float minPitch = 5f;
    public float maxPitch = 85f;

    // 🚨 แยกสถานะออกเป็น 2 โหมด
    private bool isViewingMode = false; // โหมดดูตู้ (UI 1)
    private bool isEditingMode = false; // โหมดแก้ไขตู้ (UI 0)

    private float currentYaw = 0f;
    private float currentPitch = 45f;
    private float currentDistance = 1.0f;

    private SideScrollCamera playerCamScript;

    void Start()
    {
        if (mainCamera != null)
        {
            playerCamScript = mainCamera.GetComponent<SideScrollCamera>();

            if (selectedTank != null)
            {
                Vector3 angles = mainCamera.transform.eulerAngles;
                currentYaw = angles.y;
                currentPitch = angles.x;
                currentDistance = Vector3.Distance(mainCamera.transform.position, selectedTank.position);
            }
        }

        // สั่งให้ UI Manager ซ่อน UI ทุกตัวตอนเริ่มเกม
        if (decorationUI != null) decorationUI.Initialize(false);

        if (toolManagerRef != null) toolManagerRef.UpdateTargetTank(selectedTank);
    }

    void Update()
    {
        // 🚨 1. ระบบกด ESC เพื่อออกหรือย้อนกลับ
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isEditingMode)
            {
                // ถ้าอยู่โหมดแก้ไข -> ย้อนกลับไปโหมดดูตู้
                ExitEditMode();
            }
            else if (isViewingMode)
            {
                // ถ้าอยู่โหมดดูตู้ -> ออกไปเดินปกติ
                ExitViewMode();
            }
        }

        // 2. ระบบ Hover และ คลิกตู้ (ทำงานเฉพาะตอนเดินปกติ)
        if (!isViewingMode)
        {
            if (computerRef != null && computerRef.IsUsingComputer())
            {
                ClearHover();
                return;
            }

            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.collider.CompareTag("Tank"))
                {
                    Renderer hitRenderer = hit.collider.GetComponent<Renderer>();

                    if (hitRenderer != null && hitRenderer != hoveredRenderer)
                    {
                        ClearHover();
                        hoveredRenderer = hitRenderer;

                        if (hoveredRenderer.material.HasProperty("_BaseColor"))
                        {
                            originalColor = hoveredRenderer.material.GetColor("_BaseColor");
                            hoveredRenderer.material.SetColor("_BaseColor", highlightColor);
                        }
                        else if (hoveredRenderer.material.HasProperty("_FillColor"))
                        {
                            originalColor = hoveredRenderer.material.GetColor("_FillColor");
                            hoveredRenderer.material.SetColor("_FillColor", highlightColor);
                        }
                        else if (hoveredRenderer.material.HasProperty("_Color"))
                        {
                            originalColor = hoveredRenderer.material.color;
                            hoveredRenderer.material.color = highlightColor;
                        }
                    }

                    if (Mouse.current.leftButton.wasPressedThisFrame)
                    {
                        ClearHover();

                        Transform mainTank = hit.collider.transform.parent;
                        if (mainTank == null) mainTank = hit.collider.transform;

                        // 🚨 คลิกปุ๊บ เข้าโหมดดูตู้ทันที
                        EnterViewMode(mainTank);
                    }
                }
                else
                {
                    ClearHover();
                }
            }
            else
            {
                ClearHover();
            }
        }

        // 3. ระบบควบคุมกล้อง (ทำงานทั้งตอนดูตู้และตอนแก้ไข)
        if (isViewingMode && selectedTank != null && mainCamera != null)
        {
            if (Mouse.current.middleButton.isPressed)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                currentYaw += mouseDelta.x * orbitSpeed;
                currentPitch -= mouseDelta.y * orbitSpeed;
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }

            bool isAltPressed = Keyboard.current.altKey.isPressed;
            if (!isAltPressed)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    currentDistance -= Mathf.Sign(scrollY) * zoomSpeed;
                    currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
                }
            }

            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 position = selectedTank.position + (rotation * new Vector3(0f, 0f, -currentDistance));

            mainCamera.transform.position = position;
            mainCamera.transform.LookAt(selectedTank.position);
        }
    }

    void ClearHover()
    {
        if (hoveredRenderer != null)
        {
            if (hoveredRenderer.material.HasProperty("_BaseColor"))
                hoveredRenderer.material.SetColor("_BaseColor", originalColor);
            else if (hoveredRenderer.material.HasProperty("_FillColor"))
                hoveredRenderer.material.SetColor("_FillColor", originalColor);
            else if (hoveredRenderer.material.HasProperty("_Color"))
                hoveredRenderer.material.color = originalColor;

            hoveredRenderer = null;
        }
    }

    // ==========================================
    // 🚨 STATE MANAGEMENT (จัดการเข้า/ออกโหมดต่างๆ)
    // ==========================================

    // เข้าโหมดดูตู้ (เดิน -> ดูตู้)
    private void EnterViewMode(Transform newTank)
    {
        ChangeSelectedTank(newTank);
        isViewingMode = true;
        isEditingMode = false;

        // ปิดตัวละครและสลับกล้อง
        if (playerCharacter != null) playerCharacter.SetActive(false);
        if (playerCamScript != null) playerCamScript.enabled = false;

        // เปิด UI ดูตู้ (ID 1)
        if (decorationUI != null) decorationUI.ShowPanelByID(1);

        Debug.Log("🔍 เข้าสู่: โหมดดูตู้ (View Mode)");
    }

    // ออกจากโหมดดูตู้ (ดูตู้ -> เดิน)
    private void ExitViewMode()
    {
        isViewingMode = false;
        isEditingMode = false;

        // ปิด UI ดูตู้ (ID 1)
        if (decorationUI != null) decorationUI.HidePanelByID(1);

        // คืนค่ากล้องและตัวละคร
        if (playerCharacter != null) playerCharacter.SetActive(true);
        if (playerCamScript != null)
        {
            if (playerCharacter != null) playerCamScript.target = playerCharacter.transform;
            playerCamScript.enabled = true;
        }

        SettlePhysics(); // สั่งให้ทรายและน้ำสงบนิ่ง
        Debug.Log("🚶 ย้อนกลับ: โหมดเดินปกติ (Walking Mode)");
    }

    // ==========================================
    // 🚨 BUTTON METHODS (สำหรับเอาไปผูกกับปุ่ม UI)
    // ==========================================

    // เข้าโหมดแก้ไข (เรียกผ่านปุ่ม "Edit" ใน UI ID 1)
    public void EnterEditMode()
    {
        if (!isViewingMode) return;
        isEditingMode = true;

        if (decorationUI != null)
        {
            decorationUI.HidePanelByID(1); // ซ่อนหน้าดูตู้
            decorationUI.ShowPanelByID(0); // เปิดหน้าเครื่องมือจัดตู้
        }
        Debug.Log("🛠️ เข้าสู่: โหมดแก้ไขตู้ (Edit Mode)");
    }

    // ออกจากโหมดแก้ไข (ย้อนกลับไปหน้าดูตู้)
    public void ExitEditMode()
    {
        isEditingMode = false;

        if (decorationUI != null)
        {
            decorationUI.HidePanelByID(0); // ซ่อนหน้าเครื่องมือ
            decorationUI.ShowPanelByID(1); // เปิดหน้าดูตู้กลับมา
        }

        // เคลียร์บรัชทิ้งเผื่อผู้เล่นถือค้างไว้
        if (toolManagerRef != null) toolManagerRef.activeTool = ToolManager.CurrentTool.None;

        Debug.Log("🔍 ย้อนกลับ: โหมดดูตู้ (View Mode)");
    }

    // เมธอดสลับโหมด (เผื่ออยากใช้ปุ่มเดียวสลับไปมา)
    public void ToggleEditMode()
    {
        if (isEditingMode) ExitEditMode();
        else EnterEditMode();
    }

    // ==========================================

    private void SettlePhysics()
    {
        if (selectedTank != null)
        {
            SandSimulation sand = selectedTank.GetComponentInChildren<SandSimulation>();
            WaterSystem water = selectedTank.GetComponentInChildren<WaterSystem>();

            if (sand != null)
            {
                sand.ForceSettlePhysics();
                if (playerCharacter != null)
                {
                    float localGroundY = sand.GetHeightAtWorldPos(playerCharacter.transform.position);
                    float worldGroundY = sand.transform.TransformPoint(new Vector3(0, localGroundY, 0)).y;
                    Vector3 safePos = playerCharacter.transform.position;
                    safePos.y = worldGroundY + 1.0f;
                    playerCharacter.transform.position = safePos;

                    Rigidbody rb = playerCharacter.GetComponent<Rigidbody>();
                    if (rb != null) rb.linearVelocity = Vector3.zero;
                }
            }
            if (water != null) water.ForceSettlePhysics();
        }
    }

    public void ChangeSelectedTank(Transform newTank)
    {
        selectedTank = newTank;

        if (toolManagerRef != null)
            toolManagerRef.UpdateTargetTank(newTank);

        if (mainCamera != null)
            currentDistance = Vector3.Distance(mainCamera.transform.position, selectedTank.position);
    }

    // สคริปต์คอมพิวเตอร์ยังต้องใช้อยู่ เลยส่งค่า isViewingMode ไปแทน
    public bool IsDecorationMode()
    {
        return isViewingMode;
    }
    // ==========================================
    // 🚨 EMERGENCY EXIT (ให้สคริปต์คอมพิวเตอร์เรียกใช้)
    // ==========================================
    public void ExitDecorationMode()
    {
        // ถ้ากำลังจัดตู้อยู่ ให้ถอยกลับมาโหมดดูก่อน
        if (isEditingMode)
        {
            ExitEditMode();
        }

        // ถ้ากำลังดูตู้อยู่ ให้ออกไปโหมดเดินปกติ
        if (isViewingMode)
        {
            ExitViewMode();
        }
    }
}