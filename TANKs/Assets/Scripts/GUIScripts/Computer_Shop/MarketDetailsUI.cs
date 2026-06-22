using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MarketDetailsUI : MonoBehaviour
{
    [Header("📝 ข้อมูลทั่วไป")]
    public TextMeshProUGUI nameText;
    public TextMeshProUGUI sellerText;
    public TextMeshProUGUI descriptionText;
    public TextMeshProUGUI ratingText;

    [Header("💰 โซนราคา")]
    public TextMeshProUGUI currentPriceText;

    // 🚨 เอา Original Price Obj ออกไปแล้ว เหลือแค่ตัวหนังสือ 2 ตัว
    public TextMeshProUGUI originalPriceText; // ราคาเดิม
    public TextMeshProUGUI discountPercentText; // เปอร์เซ็นต์ส่วนลด

    [Header("🖼️ แกลเลอรี่")]
    public Image displayImage;
    public Button nextBtn;
    public Button prevBtn;

    [Header("💬 รีวิวและปุ่มซื้อ")]
    public TextMeshProUGUI reviewsText;
    public Button buyButton;

    private MarketListing currentListing;
    private int imgIndex = 0;

    public void Setup(MarketListing listing)
    {
        currentListing = listing;

        if (nameText != null) nameText.text = listing.GetDisplayName();
        if (sellerText != null) sellerText.text = $"ผู้ขาย: {listing.sellerName}";
        if (descriptionText != null) descriptionText.text = listing.GetDisplayDescription();
        if (ratingText != null) ratingText.text = $"คะแนน: {listing.rating:F1}/5 ⭐";

        // จัดการเรื่องราคาและส่วนลด
        if (currentPriceText != null)
            currentPriceText.text = $"฿{listing.GetCurrentPrice():N0}";

        if (listing.discountPercent > 0)
        {
            // ถ้ามีลดราคา -> เปิดให้แสดงผลตัวหนังสือ
            if (originalPriceText != null) originalPriceText.gameObject.SetActive(true);
            if (discountPercentText != null) discountPercentText.gameObject.SetActive(true);

            // ใช้แท็ก <s>...</s> สำหรับขีดฆ่าข้อความ
            if (originalPriceText != null) originalPriceText.text = $"<s>฿{listing.basePrice:N0}</s>";
            if (discountPercentText != null) discountPercentText.text = $"-{listing.discountPercent:F0}%";
        }
        else
        {
            // ถ้าไม่มีลดราคา -> สั่งปิด(ซ่อน)ตัวหนังสือทิ้งไป
            if (originalPriceText != null) originalPriceText.gameObject.SetActive(false);
            if (discountPercentText != null) discountPercentText.gameObject.SetActive(false);
        }

        // จัดการรีวิว
        if (reviewsText != null)
        {
            if (listing.reviews.Count > 0)
            {
                string allReviews = "";
                foreach (var rev in listing.reviews)
                {
                    allReviews += $"<b>{rev.reviewerName}</b>: {rev.reviewText}\n\n";
                }
                reviewsText.text = allReviews;
            }
            else
            {
                reviewsText.text = "ยังไม่มีรีวิว";
            }
        }

        // จัดการรูปภาพ
        imgIndex = 0;
        UpdateImage();

        if (nextBtn != null)
        {
            nextBtn.onClick.RemoveAllListeners();
            nextBtn.onClick.AddListener(NextImg);
        }

        if (prevBtn != null)
        {
            prevBtn.onClick.RemoveAllListeners();
            prevBtn.onClick.AddListener(PrevImg);
        }

        // จัดการปุ่มซื้อ
        if (buyButton != null)
        {
            buyButton.onClick.RemoveAllListeners();
            buyButton.onClick.AddListener(BuyItem);
        }
    }

    private void UpdateImage()
    {
        if (currentListing.galleryImages.Count == 0)
        {
            if (displayImage != null) displayImage.sprite = currentListing.baseItem.icon;
            if (nextBtn != null) nextBtn.gameObject.SetActive(false);
            if (prevBtn != null) prevBtn.gameObject.SetActive(false);
            return;
        }

        if (displayImage != null) displayImage.sprite = currentListing.galleryImages[imgIndex];

        bool hasMultiple = currentListing.galleryImages.Count > 1;
        if (nextBtn != null) nextBtn.gameObject.SetActive(hasMultiple);
        if (prevBtn != null) prevBtn.gameObject.SetActive(hasMultiple);
    }

    private void NextImg()
    {
        imgIndex = (imgIndex + 1) % currentListing.galleryImages.Count;
        UpdateImage();
    }

    private void PrevImg()
    {
        imgIndex--;
        if (imgIndex < 0) imgIndex = currentListing.galleryImages.Count - 1;
        UpdateImage();
    }

    private void BuyItem()
    {
        if (CurrencyManager.Instance != null && CurrencyManager.Instance.TryRemoveMoney(currentListing.GetCurrentPrice()))
        {
            Debug.Log($"ซื้อ {currentListing.baseItem.itemName} สำเร็จ!");
            // ส่งไอเทมเข้ากระเป๋าได้เลย
        }
    }
}