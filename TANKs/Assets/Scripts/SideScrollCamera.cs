using UnityEngine;

public class SideScrollCamera : MonoBehaviour
{
    [Header("🎯 เป้าหมาย (Target)")]
    [Tooltip("ตัวละครที่จะให้กล้องวิ่งตาม (สคริปต์ CameraModeController จะเป็นคนเอามาใส่ให้เอง)")]
    public Transform target;

    [Header("📐 ระยะห่างกล้อง (Camera Offset)")]
    [Tooltip("ระยะห่างระหว่างกล้องกับตัวละคร (X=ซ้ายขวา, Y=ความสูง, Z=ระยะห่างจากหน้าจอ)")]
    public Vector3 offset = new Vector3(0f, 2f, -8f);

    [Header("🎥 มุมกล้องเริ่มต้น (Default Rotation)")]
    [Tooltip("องศากล้องตอนเดินเล่น (ปกติ X จะก้มลงนิดๆ เช่น 10-15 องศา, Y 0, Z 0)")]
    public Vector3 defaultRotation = new Vector3(10f, 0f, 0f);

    [Header("⚙️ ความสมูท (Smoothness)")]
    [Tooltip("ยิ่งค่าน้อย กล้องยิ่งวิ่งตามช้าๆ (แนะนำ 8 - 10)")]
    public float smoothSpeed = 10f;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. วิ่งตามพิกัดตัวละครทั้ง 3 แกนแบบเป๊ะๆ (บวกด้วยระยะห่าง Offset)
        Vector3 targetPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // 2. บังคับหันหน้ากล้องกลับมามองตรงๆ เสมอ (ล้างการหมุนที่ค้างมาจากโหมดจัดตู้)
        Quaternion targetRot = Quaternion.Euler(defaultRotation);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, smoothSpeed * Time.deltaTime);
    }
}