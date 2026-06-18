using UnityEngine;

public class SandDustController : MonoBehaviour
{
    [Header("Life Settings")]
    public float lifeTime = 0.6f; // ตัวเลขวินาทีก่อนโดนลบทิ้ง (ปรับเพิ่ม/ลดได้ตามความลึกตู้ปลา)

    void Start()
    {
        // สั่งให้ทำลายตัวเองทิ้งทันทีเมื่อเวลาหมดนับจากจุดเริ่มเสก
        Destroy(gameObject, lifeTime);
    }
}