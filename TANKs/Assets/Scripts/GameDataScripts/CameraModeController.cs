using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class CameraModeController : MonoBehaviour
{
    [Tooltip("ลาก Object ที่มีสคริปต์ ToolManager มาใส่ที่นี่")]
    public ToolManager toolManagerRef;

    [Header("UI Manager")]
    [Tooltip("ลาก Object ที่มีสคริปต์ DecorationUIManager มาใส่ที่นี่ 👈")]
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

    private bool isDecorationMode = false;
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

        // สั่งให้ UI Manager จัดการตำแหน่งเริ่มต้น
        if (decorationUI != null) decorationUI.Initialize(isDecorationMode);

        SetMode(isDecorationMode);
        if (toolManagerRef != null) toolManagerRef.UpdateTargetTank(selectedTank);
    }

    void Update()
    {
        if (isDecorationMode && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            ExitDecorationMode();
        }

        if (!isDecorationMode)
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

                        ChangeSelectedTank(mainTank);
                        isDecorationMode = true;
                        SetMode(isDecorationMode);
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

        if (isDecorationMode && selectedTank != null && mainCamera != null)
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
            {
                hoveredRenderer.material.SetColor("_BaseColor", originalColor);
            }
            else if (hoveredRenderer.material.HasProperty("_FillColor"))
            {
                hoveredRenderer.material.SetColor("_FillColor", originalColor);
            }
            else if (hoveredRenderer.material.HasProperty("_Color"))
            {
                hoveredRenderer.material.color = originalColor;
            }
            hoveredRenderer = null;
        }
    }

    public void ExitDecorationMode()
    {
        if (isDecorationMode)
        {
            isDecorationMode = false;
            SetMode(isDecorationMode);
        }
    }

    private void SetMode(bool decorationMode)
    {
        if (playerCharacter != null) playerCharacter.SetActive(!decorationMode);

        // 🚨 สั่งงานเปิด/ปิด แบบเจาะจงเฉพาะ Panel ID = 0 เท่านั้น
        if (decorationUI != null)
        {
            if (decorationMode)
                decorationUI.ShowPanelByID(0); // เปิดแค่ Tool Panel (ID 0)
            else
                decorationUI.HidePanelByID(0); // ปิดแค่ Tool Panel (ID 0)
        }

        if (playerCamScript != null)
        {
            if (!decorationMode && playerCharacter != null)
            {
                playerCamScript.target = playerCharacter.transform;
            }
            playerCamScript.enabled = !decorationMode;
        }

        if (!decorationMode && selectedTank != null)
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

        Debug.Log("สลับโหมด: " + (decorationMode ? "โหมดจัดตู้ (Decoration)" : "โหมดเดินปกติ (Walking)"));
    }

    public void ChangeSelectedTank(Transform newTank)
    {
        selectedTank = newTank;

        if (toolManagerRef != null)
        {
            toolManagerRef.UpdateTargetTank(newTank);
        }

        if (mainCamera != null)
        {
            currentDistance = Vector3.Distance(mainCamera.transform.position, selectedTank.position);
        }
    }

    public bool IsDecorationMode()
    {
        return isDecorationMode;
    }
}