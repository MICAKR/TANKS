using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerClickable : MonoBehaviour
{
    [Header("Settings")]
    public GameObject computerUI;
    public float holdDuration = 1.0f;
    public float interactionDistance = 2.5f; // ระยะที่คลิกได้

    [Header("References")]
    public CameraModeController cameraController;
    public Transform playerTransform; // 👈 ลาก Player มาใส่ที่นี่

    private bool isUsingComputer = false;
    private float holdTimer = 0f;

    void Start()
    {
        if (computerUI != null) computerUI.SetActive(false);
    }

    void Update()
    {
        if (isUsingComputer && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseUI();
        }

        // เช็คระยะห่างเฉพาะตอนกำลังกดปุ่ม
        if (!isUsingComputer && Keyboard.current.eKey.isPressed)
        {
            if (IsPlayerInRange())
            {
                if (cameraController != null && !cameraController.IsDecorationMode())
                {
                    holdTimer += Time.deltaTime;
                    if (holdTimer >= holdDuration) OpenUI();
                }
            }
            else
            {
                holdTimer = 0f; // ถ้าเดินออกห่างขณะกด ให้รีเซ็ต
            }
        }
        else
        {
            if (!isUsingComputer) holdTimer = 0f;
        }
    }

    bool IsPlayerInRange()
    {
        if (playerTransform == null) return false;
        float sqrDistance = (playerTransform.position - transform.position).sqrMagnitude;
        return sqrDistance < (interactionDistance * interactionDistance);
    }

    public bool IsUsingComputer()
    {
        return isUsingComputer;
    }

    void OpenUI()
    {
        // บังคับกล้องให้ออกจากโหมดจัดตู้ (กันเหนียว)
        if (cameraController != null) cameraController.ExitDecorationMode();

        isUsingComputer = true;
        holdTimer = 0f;
        if (computerUI != null) computerUI.SetActive(true);
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void CloseUI()
    {
        isUsingComputer = false;
        holdTimer = 0f;
        if (computerUI != null) computerUI.SetActive(false);

        // ปลดล็อคและแสดงเมาส์ให้ชัดเจน
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void ForceCloseComputer()
    {
        isUsingComputer = false;
        if (computerUI != null) computerUI.SetActive(false);

        // ปลดล็อคและแสดงเมาส์ให้ชัดเจน
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

}