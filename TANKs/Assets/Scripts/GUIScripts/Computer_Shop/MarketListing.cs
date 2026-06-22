using UnityEngine;
using System.Collections.Generic;

// โครงสร้างสำหรับเก็บรีวิว
[System.Serializable]
public struct ReviewData
{
    public string reviewerName; // ชื่อคนรีวิว
    [TextArea(2, 3)]
    public string reviewText;   // ข้อความรีวิว
}

[CreateAssetMenu(fileName = "New Listing", menuName = "Marketplace/Listing")]
public class MarketListing : ScriptableObject
{
    [Header("📦 ข้อมูลสินค้าหลัก")]
    public ItemData baseItem; // ตัวไอเทมที่จะได้รับเข้ากระเป๋าเวลาซื้อ

    [Header("📝 ข้อมูลโพสต์ขายของ")]
    [Tooltip("ชื่อสินค้าที่จะแสดงบนเว็บ (ถ้าเว้นว่างไว้ ระบบจะไปดึงชื่อจาก ItemData มาใช้แทน)")]
    public string listingTitle;

    // 🚨 เพิ่มช่องคำอธิบายเฉพาะโพสต์
    [TextArea(3, 5)]
    [Tooltip("รายละเอียดสินค้าเฉพาะโพสต์นี้ (ถ้าเว้นว่างไว้ จะดึงคำอธิบายจาก ItemData มาใช้แทน)")]
    public string listingDescription;

    [Header("🏪 ข้อมูลร้านค้าและราคา")]
    public string sellerName = "ร้านค้าปริศนา";
    public float basePrice = 100f; // ราคาเต็ม

    [Range(0f, 100f)]
    [Tooltip("เปอร์เซ็นต์ส่วนลด (0 = ไม่ลดราคา)")]
    public float discountPercent = 0f;

    [Header("🖼️ แกลเลอรี่รูปภาพ")]
    [Tooltip("ใส่รูปหลายๆ รูปได้ รูปแรกจะถูกใช้เป็นภาพปกหน้าเว็บ")]
    public List<Sprite> galleryImages;

    [Header("⭐ คะแนนและรีวิว")]
    [Range(0f, 5f)] public float rating = 5f;
    public List<ReviewData> reviews;

    // คำนวณราคาขายจริง (หักส่วนลดแล้ว)
    public float GetCurrentPrice()
    {
        if (discountPercent <= 0f) return basePrice;
        return basePrice * (1f - (discountPercent / 100f));
    }

    // ฟังก์ชันใหม่: ดึงชื่อไปแสดงผลบนเว็บ
    public string GetDisplayName()
    {
        if (!string.IsNullOrEmpty(listingTitle))
        {
            return listingTitle;
        }

        if (baseItem != null)
        {
            return baseItem.itemName;
        }

        return "สินค้าไร้ชื่อ";
    }

    // 🚨 ฟังก์ชันใหม่: ดึงคำอธิบายไปแสดงผลบนเว็บ
    public string GetDisplayDescription()
    {
        // ถ้ามีการพิมพ์คำอธิบายโพสต์ไว้ ให้ใช้ของโพสต์
        if (!string.IsNullOrEmpty(listingDescription))
        {
            return listingDescription;
        }

        // แต่ถ้าเว้นว่างไว้ ให้ไปดูดคำอธิบายมาจากไอเทมหลักแทน
        if (baseItem != null && !string.IsNullOrEmpty(baseItem.description))
        {
            return baseItem.description;
        }

        return "ไม่มีคำอธิบายสินค้า";
    }
}