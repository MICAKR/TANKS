using UnityEngine;
using UnityEngine.InputSystem;

public class CameraModeController : MonoBehaviour
{
    [Header("Mode Objects")]
    [Tooltip("ใส่ Object ของตัวละครผู้เล่น (โหมดเดินปกติ)")]
    public GameObject playerCharacter;

    [Tooltip("ใส่กล้อง Main Camera เข้ามาช่องนี้ (ใช้กล้องตัวเดียวเลยครับ)")]
    public Camera mainCamera;

    [Header("Tank Selection (ข้อมูลตู้ที่เลือก)")]
    [Tooltip("ลาก Object ตู้ปลาตรงกลางมาใส่ เพื่อให้กล้องมองและหมุนรอบตู้นี้")]
    public Transform selectedTank;

    [Header("Decoration Camera Settings")]
    public float orbitSpeed = 0.5f;
    public float zoomSpeed = 0.5f;
    public float minZoom = 0.3f;
    public float maxZoom = 2.0f;
    public float minPitch = 5f;
    public float maxPitch = 85f;

    private bool isDecorationMode = true;
    private float currentYaw = 0f;
    private float currentPitch = 45f;
    private float currentDistance = 1.0f;

    // เก็บอ้างอิงสคริปต์กล้องวิ่งตาม
    private SideScrollCamera playerCamScript;

    void Start()
    {
        if (mainCamera != null)
        {
            // ค้นหาสคริปต์ SideScrollCamera ที่แปะอยู่บนกล้อง
            playerCamScript = mainCamera.GetComponent<SideScrollCamera>();

            if (selectedTank != null)
            {
                Vector3 angles = mainCamera.transform.eulerAngles;
                currentYaw = angles.y;
                currentPitch = angles.x;
                currentDistance = Vector3.Distance(mainCamera.transform.position, selectedTank.position);
            }
        }

        SetMode(isDecorationMode);
    }

    void Update()
    {
        if (Keyboard.current.bKey.wasPressedThisFrame)
        {
            isDecorationMode = !isDecorationMode;
            SetMode(isDecorationMode);
        }

        // 🚨 ถ้าอยู่ในโหมดจัดตู้ สคริปต์นี้จะคำนวณตำแหน่งกล้องเอง
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

    private void SetMode(bool decorationMode)
    {
        if (playerCharacter != null) playerCharacter.SetActive(!decorationMode);

        // 🚨 🪐 สลับการทำงานของสคริปต์วิ่งตาม 
        if (playerCamScript != null)
        {
            // 🚨 🪐 ถ้าย้ายมาโหมดเดิน ให้จับตัวละครยัดใส่เป้าหมายให้กล้องด้วย!
            if (!decorationMode && playerCharacter != null)
            {
                playerCamScript.target = playerCharacter.transform;
            }

            playerCamScript.enabled = !decorationMode;
        }
        else
        {
            Debug.LogWarning("⚠️ ไม่พบสคริปต์ SideScrollCamera บน MainCamera อย่าลืมเอาไปแปะนะครับ!");
        }

        if (!decorationMode && selectedTank != null)
        {
            SandSimulation sand = selectedTank.GetComponentInChildren<SandSimulation>();
            WaterSystem water = selectedTank.GetComponentInChildren<WaterSystem>();

            if (sand != null)
            {
                sand.ForceSettlePhysics();

                // 🚨 🪐 ระบบเซฟตี้กันตกโลก: ดึงผู้เล่นขึ้นมาเหนือพื้นทราย 1 เมตรก่อนเริ่มเดิน
                if (playerCharacter != null)
                {
                    float localGroundY = sand.GetHeightAtWorldPos(playerCharacter.transform.position);
                    float worldGroundY = sand.transform.TransformPoint(new Vector3(0, localGroundY, 0)).y;

                    Vector3 safePos = playerCharacter.transform.position;
                    safePos.y = worldGroundY + 1.0f; // ลอยขึ้น 1 เมตร
                    playerCharacter.transform.position = safePos;

                    // รีเซ็ตแรงตกของ Rigidbody เผื่อมันสะสมแรงตกลงมาหนักๆ ไว้
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
    }
}