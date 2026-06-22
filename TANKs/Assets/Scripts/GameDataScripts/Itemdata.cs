using UnityEngine;

[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item")]
public class ItemData : ScriptableObject
{
    // สร้าง Enum สำหรับหมวดหมู่ไอเทม
    public enum ItemCategory
    {
        Tank,           // ตู้ปลา
        Stone,          // หิน
        Wood,           // ไม้
        Decoration,     // ของตกแต่ง
        Fish,           // ปลา
        Substrate,      // วัสดุปูพื้น
        PetFood,        // อาหารสัตว์เลี้ยง
        Medicine,       // ยาสัตว์เลี้ยง
        Electronics,    // อุปกรณ์อิเล็กทรอนิกส์
        FilterMedia,    // ไส้กรอง
        Plant           // ต้นไม้ 👈 (เพิ่มให้แล้วครับ)
    }

    public int id;
    public string itemName;
    public Sprite icon;

    [Header("Settings")]
    public ItemCategory category; // เลือกหมวดหมู่จาก Dropdown ใน Inspector

    [TextArea] public string description;
}