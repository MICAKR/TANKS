using UnityEngine;
using UnityEngine.InputSystem;

public class ComputerClickable : MonoBehaviour
{
    [Header("Settings")]
    public GameObject computerUI;
    public float holdDuration = 1.0f;

    [Header("References")]
    public CameraModeController cameraController;

    private bool isUsingComputer = false;
    private float holdTimer = 0f;

    void Start()
    {
        // ปิด UI ทันทีตอนเริ่มเกม
        if (computerUI != null) computerUI.SetActive(false);
    }

    void Update()
    {
        // 1. กด ESC เพื่อออก
        if (isUsingComputer && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CloseUI();
        }

        // 2. กด E ค้างเพื่อเปิด (ต้องไม่อยู่ในโหมดจัดตู้)
        if (!isUsingComputer && Keyboard.current.eKey.isPressed)
        {
            if (cameraController != null && !cameraController.IsDecorationMode())
            {
                holdTimer += Time.deltaTime;
                if (holdTimer >= holdDuration)
                {
                    OpenUI();
                }
            }
        }
        else
        {
            // รีเซ็ตตัวนับถ้าไม่ได้กดค้าง หรือกดเปิดไปแล้ว
            if (!isUsingComputer) holdTimer = 0f;
        }
    }

    void OpenUI()
    {
        isUsingComputer = true;
        holdTimer = 0f; // รีเซ็ตทันที

        if (computerUI != null) computerUI.SetActive(true);

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void CloseUI()
    {
        isUsingComputer = false; // ปลดล็อกตรงนี้เลย
        holdTimer = 0f;

        if (computerUI != null) computerUI.SetActive(false);

        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    // ใน ComputerClickable.cs
    public void ForceCloseComputer()
    {
        computerUI.SetActive(false);
        isUsingComputer = false;

        // ตรงนี้สำคัญ: ถ้าหลังจากปิดคอม คุณต้องการให้เมาส์กลับมาเป็นปกติ (เห็นเมาส์)
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}