using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarketCardUI : MonoBehaviour
{
    [Header("UI Components")]
    public Image iconImage;
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI priceText;
    public Button clickButton;

    private MarketListing myListing;
    private MarketWebManager manager;

    public void Setup(MarketListing listing, MarketWebManager mgr)
    {
        myListing = listing;
        manager = mgr;

        // ดึงชื่อและรูปจาก ItemData
        if (nameText != null) nameText.text = myListing.GetDisplayName();

        // ใช้รูปแรกในแกลเลอรี่เป็นปก ถ้าไม่มีให้ไปเอารูปไอคอนจาก ItemData แทน
        if (iconImage != null)
        {
            if (myListing.galleryImages.Count > 0)
                iconImage.sprite = myListing.galleryImages[0];
            else
                iconImage.sprite = myListing.baseItem.icon;
        }

        // แสดงราคา (ถ้ามีส่วนลด โชว์สีแดงเตะตา)
        if (priceText != null)
        {
            if (myListing.discountPercent > 0)
                priceText.text = $"<color=red>฿{myListing.GetCurrentPrice():N0}</color>";
            else
                priceText.text = $"฿{myListing.basePrice:N0}";
        }

        // ตั้งค่าปุ่มกดเข้าไปดูรายละเอียด
        clickButton.onClick.RemoveAllListeners();
        clickButton.onClick.AddListener(() => manager.OpenDetails(myListing));
    }
}