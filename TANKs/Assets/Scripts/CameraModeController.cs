using UnityEngine;
using UnityEngine.InputSystem;

public class CameraModeController : MonoBehaviour
{
    [Header("Mode Objects")]
    [Tooltip("ใส่ Object ของตัวละครผู้เล่น (โหมดเดินปกติ)")]
    public GameObject playerCharacter;
    [Tooltip("ใส่กล้อง Main Camera ที่ใช้จัดตู้ (โหมดจัดตู้)")]
    public GameObject decorationCamera;

    [Header("Tank Selection (ข้อมูลตู้ที่เลือก)")]
    [Tooltip("ลาก Object ตู้ปลาตรงกลางมาใส่ เพื่อให้กล้องมองและหมุนรอบตู้นี้")]
    public Transform selectedTank;

    [Header("Decoration Camera Settings")]
    public float orbitSpeed = 0.5f;
    public float zoomSpeed = 0.5f; // ความเร็วซูม
    public float minZoom = 0.3f;   // ซูมเข้าใกล้สุด
    public float maxZoom = 2.0f;   // ซูมออกไกลสุด
    public float minPitch = 5f;    // มุมก้มต่ำสุด (ไม่ให้มุดดิน)
    public float maxPitch = 85f;   // มุมเงยสูงสุด (ไม่ให้ข้ามหัว)

    // สถานะปัจจุบัน
    private bool isDecorationMode = true;
    private float currentYaw = 0f;
    private float currentPitch = 45f;
    private float currentDistance = 1.0f;

    void Start()
    {
        // ดึงค่าองศาปัจจุบันของกล้องมาตั้งเป็นค่าเริ่มต้น
        if (decorationCamera != null && selectedTank != null)
        {
            Vector3 angles = decorationCamera.transform.eulerAngles;
            currentYaw = angles.y;
            currentPitch = angles.x;
            currentDistance = Vector3.Distance(decorationCamera.transform.position, selectedTank.position);
        }

        // อัปเดตสถานะเริ่มต้น (เริ่มมาให้เป็นโหมดจัดตู้ก่อน)
        SetMode(isDecorationMode);
    }

    void Update()
    {
        // 1. กดปุ่ม B เพื่อสลับโหมด (Toggle)
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            isDecorationMode = !isDecorationMode;
            SetMode(isDecorationMode);
        }

        // 2. ควบคุมกล้องในโหมดจัดตู้
        if (isDecorationMode && selectedTank != null && decorationCamera != null)
        {
            // กดเมาส์กลางค้างเพื่อหมุนมุมกล้อง (Orbit)
            if (Mouse.current.middleButton.isPressed)
            {
                Vector2 mouseDelta = Mouse.current.delta.ReadValue();
                currentYaw += mouseDelta.x * orbitSpeed;
                currentPitch -= mouseDelta.y * orbitSpeed;
                currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
            }

            // ใช้ลูกกลิ้งเมาส์เพื่อซูมเข้า/ออก (ดักเช็คว่าต้อง "ไม่ได้กดปุ่ม Alt" เพื่อไม่ให้ชนกับระบบปรับบรัช)
            bool isAltPressed = Keyboard.current.altKey.isPressed;
            if (!isAltPressed)
            {
                float scrollY = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    // ปรับระยะซูม
                    currentDistance -= Mathf.Sign(scrollY) * zoomSpeed;
                    currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
                }
            }

            // คำนวณพิกัดและสั่งให้กล้องขยับไปอยู่ตามมุมและระยะซูมที่ตั้งไว้
            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 position = selectedTank.position + (rotation * new Vector3(0f, 0f, -currentDistance));

            decorationCamera.transform.position = position;
            decorationCamera.transform.LookAt(selectedTank.position);
        }
    }

    private void SetMode(bool decorationMode)
    {
        if (decorationCamera != null) decorationCamera.SetActive(decorationMode);
        if (playerCharacter != null) playerCharacter.SetActive(!decorationMode);

        Debug.Log("สลับโหมด: " + (decorationMode ? "โหมดจัดตู้ (Decoration)" : "โหมดเดินปกติ (Walking)"));
    }

    // ฟังก์ชันเผื่ออนาคตสำหรับเรียกเปลี่ยนตู้
    public void ChangeSelectedTank(Transform newTank)
    {
        selectedTank = newTank;
    }
}