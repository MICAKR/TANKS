using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class CameraModeController : MonoBehaviour
{
    [Tooltip("ลาก Object ที่มีสคริปต์ ToolManager มาใส่ที่นี่")]
    public ToolManager toolManagerRef;

    [Tooltip("ลาก Object ที่มีสคริปต์ TankInfoUI มาใส่ที่นี่")]
    public TankInfoUI tankInfoUI;

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

    [Header("🛡️ Optimization & Distance Settings")]
    [Tooltip("ระยะทางสูงสุดที่ผู้เล่นยืนห่างจากตู้แล้วยังสามารถส่องหรือคลิกได้ (หน่วย: เมตร)")]
    public float maxInteractionDistance = 3.5f;

    [Tooltip("ความถี่ในการคำนวณระบบ Hover (เช่น 0.1f คือคำนวณแค่ 10 ครั้งต่อวินาที แทนการทำทุกเฟรม)")]
    public float hoverCheckInterval = 0.1f;
    private float hoverTimer = 0f;

    [Header("Tank Selection")]
    public Transform selectedTank;

    [Header("Decoration Camera Settings")]
    public float orbitSpeed = 0.5f;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0.3f;
    public float maxZoom = 2.0f;
    public float minPitch = 5f;
    public float maxPitch = 85f;

    private bool isViewingMode = false;
    private bool isEditingMode = false;

    private float currentYaw = 0f;
    private float currentPitch = 45f;
    private float currentDistance = 1.0f;

    private SideScrollCamera playerCamScript;

    // 🚨 ตัวแปรใหม่สำหรับตั้งเวลากระตุ้นทราย
    private float sandWakeTimer = 0f;
    private float sandWakeInterval = 0.5f; // กระตุ้นทุกๆ ครึ่งวินาที (ปรับค่าได้ตามต้องการ)

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

        if (decorationUI != null) decorationUI.Initialize(false);
        if (toolManagerRef != null) toolManagerRef.UpdateTargetTank(selectedTank);
    }

    void Update()
    {
        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (isEditingMode) ExitEditMode();
            else if (isViewingMode) ExitViewMode();
        }

        // 🚨 ของใหม่: ตั้งเวลาให้กระตุ้นการคำนวณทรายเป็นระยะๆ แทนการทำทุกเฟรม (ประหยัด CPU)
        if (isEditingMode && selectedTank != null)
        {
            sandWakeTimer += Time.deltaTime;
            if (sandWakeTimer >= sandWakeInterval)
            {
                sandWakeTimer = 0f;
                SandSimulation sandSim = selectedTank.GetComponentInChildren<SandSimulation>();
                if (sandSim != null)
                {
                    sandSim.WakeUpSimulation();
                }
            }
        }

        if (!isViewingMode)
        {
            if (computerRef != null && computerRef.IsUsingComputer())
            {
                ClearHover();
                return;
            }

            bool leftMouseClicked = Mouse.current.leftButton.wasPressedThisFrame;

            hoverTimer += Time.deltaTime;
            if (hoverTimer >= hoverCheckInterval || leftMouseClicked)
            {
                hoverTimer = 0f;
                PerformInteractionCheck(leftMouseClicked);
            }
        }

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

    private void PerformInteractionCheck(bool isClicked)
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Tank"))
            {
                if (playerCharacter != null)
                {
                    float distanceToTank = Vector3.Distance(playerCharacter.transform.position, hit.point);

                    if (distanceToTank > maxInteractionDistance)
                    {
                        ClearHover();
                        return;
                    }
                }

                Renderer hitRenderer = hit.collider.GetComponent<Renderer>();

                if (hitRenderer != null && hitRenderer != hoveredRenderer)
                {
                    ClearHover();
                    hoveredRenderer = hitRenderer;

                    if (hoveredRenderer.material.HasProperty("_BaseColor"))
                        hoveredRenderer.material.SetColor("_BaseColor", highlightColor);
                    else if (hoveredRenderer.material.HasProperty("_FillColor"))
                        hoveredRenderer.material.SetColor("_FillColor", highlightColor);
                    else if (hoveredRenderer.material.HasProperty("_Color"))
                        hoveredRenderer.material.color = highlightColor;
                }

                if (isClicked)
                {
                    ClearHover();
                    Transform mainTank = hit.collider.transform.parent;
                    if (mainTank == null) mainTank = hit.collider.transform;
                    EnterViewMode(mainTank);
                }

                return;
            }
        }
        ClearHover();
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

    private void EnterViewMode(Transform newTank)
    {
        ChangeSelectedTank(newTank);
        isViewingMode = true;
        isEditingMode = false;

        if (playerCharacter != null) playerCharacter.SetActive(false);
        if (playerCamScript != null) playerCamScript.enabled = false;

        if (decorationUI != null) decorationUI.ShowPanelByID(1);

        TankWaterQuality tankData = newTank.GetComponentInChildren<TankWaterQuality>();
        if (tankInfoUI != null) tankInfoUI.DisplayTankInfo(tankData);

        Debug.Log("🔍 เข้าสู่: โหมดดูตู้ (View Mode)");
    }

    private void ExitViewMode()
    {
        isViewingMode = false;
        isEditingMode = false;

        if (decorationUI != null) decorationUI.HidePanelByID(1);

        if (playerCharacter != null) playerCharacter.SetActive(true);
        if (playerCamScript != null)
        {
            if (playerCharacter != null) playerCamScript.target = playerCharacter.transform;
            playerCamScript.enabled = true;
        }
        if (tankInfoUI != null) tankInfoUI.HideInfo();

        SettlePhysics();
        Debug.Log("🚶 ย้อนกลับ: โหมดเดินปกติ (Walking Mode)");
    }

    public void EnterEditMode()
    {
        if (!isViewingMode) return;
        isEditingMode = true;

        if (decorationUI != null)
        {
            decorationUI.HidePanelByID(1);
            decorationUI.ShowPanelByID(0);
        }
        Debug.Log("🛠️ เข้าสู่: โหมดแก้ไขตู้ (Edit Mode)");
    }

    public void ExitEditMode()
    {
        isEditingMode = false;

        if (decorationUI != null)
        {
            decorationUI.HidePanelByID(0);
            decorationUI.ShowPanelByID(1);
        }

        if (toolManagerRef != null) toolManagerRef.activeTool = ToolManager.CurrentTool.None;

        Debug.Log("🔍 ย้อนกลับ: โหมดดูตู้ (View Mode)");
    }

    public void ToggleEditMode()
    {
        if (isEditingMode) ExitEditMode();
        else EnterEditMode();
    }

    private void SettlePhysics()
    {
        if (selectedTank != null)
        {
            SandSimulation sand = selectedTank.GetComponentInChildren<SandSimulation>();
            WaterSystem water = selectedTank.GetComponentInChildren<WaterSystem>();

            if (sand != null) sand.ForceSettlePhysics();
            if (water != null) water.ForceSettlePhysics();
        }

        if (playerCharacter != null)
        {
            Rigidbody rb = playerCharacter.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
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

    public bool IsDecorationMode()
    {
        return isViewingMode;
    }

    public void ExitDecorationMode()
    {
        if (isEditingMode) ExitEditMode();
        if (isViewingMode) ExitViewMode();
    }
}