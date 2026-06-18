using UnityEngine;

public class SideScrollCamera : MonoBehaviour
{
    [Header("🎯 เป้าหมาย (Target)")]
    [Tooltip("ลากตัวละครมาใส่ช่องนี้")]
    public Transform target;

    [Header("📐 ระยะห่างกล้อง (Camera Offset)")]
    [Tooltip("ตั้งค่าว่าจะให้กล้องอยู่ห่างจากตัวละครแค่ไหน (X=ซ้ายขวา, Y=ความสูง, Z=ระยะห่าง)")]
    public Vector3 offset = new Vector3(0f, 3f, -10f);

    [Header("⚙️ ความสมูท (Smoothness)")]
    [Tooltip("ยิ่งค่าน้อย กล้องยิ่งวิ่งตามช้าๆ หนืดๆ (แนะนำ 5 - 10)")]
    public float smoothSpeed = 8f;

    void LateUpdate()
    {
        if (target == null) return;

        // 1. คำนวณจุดที่กล้องควรจะไปอยู่ (อิงจากแกน X และ Y ของตัวละคร แต่ไม่ตามแกน Z ของตัวละครตรงๆ)
        // เพื่อให้เป็น Side-Scroll เราจะให้กล้องขยับซ้าย-ขวา และขึ้น-ลง ตามตัวละคร
        // แต่ถ้าไม่อยากให้กล้องซูมเข้า/ออกตอนตัวละครเดินลึก ให้ใช้ offset.z คงที่
        Vector3 targetPosition = new Vector3(target.position.x + offset.x, target.position.y + offset.y, offset.z);

        // 2. เกลี่ยตำแหน่งกล้องให้ค่อยๆ สไลด์ไปหาเป้าหมายแบบนุ่มนวล
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed * Time.deltaTime);

        // หมายเหตุ: ถ้าอยากให้กล้องหันมองกดหัวลงมาหาตัวละครนิดนึง ให้เอา Comment บรรทัดล่างออกครับ
        // transform.LookAt(target.position + Vector3.up * 1f);
    }
}