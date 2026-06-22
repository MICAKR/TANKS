using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    // 🚨 แกนหลักของ Singleton เพื่อให้สคริปต์อื่นเรียกใช้ได้ทันที (เช่น CurrencyManager.Instance.AddMoney(100);)
    public static CurrencyManager Instance { get; private set; }

    [Header("💰 ระบบการเงินส่วนกลาง")]
    [SerializeField] private float currentMoney = 0f; // ยอดเงินปัจจุบัน (ใช้ serializefield เพื่อให้เห็นใน Inspector แต่แก้จากสคริปต์อื่นตรงๆ ไม่ได้)

    // Event เผื่อเอาไว้ให้ UI ชิ้นต่างๆ มาดักฟังเวลาเงินเปลี่ยน จะได้อัปเดตตัวเลขบนจออัตโนมัติ
    public System.Action<float> OnMoneyChanged;

    void Awake()
    {
        // ตรวจสอบและสร้าง Singleton
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // เปลี่ยนฉากแล้วเงินจะไม่หาย Object ไม่โดนทำลาย
        }
        else
        {
            Destroy(gameObject); // ถ้ามีตัวซ้ำในฉากอื่น ให้ทำลายตัวที่สร้างใหม่ทิ้งซะ
        }
    }

    // เมธอดสำหรับเรียกดูยอดเงินปัจจุบัน
    public float GetCurrentMoney()
    {
        return currentMoney;
    }

    // เมธอดสำหรับ "เพิ่มเงิน"
    public void AddMoney(float amount)
    {
        if (amount <= 0) return;

        currentMoney += amount;

        // ตะโกนบอก UI ให้เปลี่ยนตัวเลขตาม
        OnMoneyChanged?.Invoke(currentMoney);

        Debug.Log($"[Currency] ได้รับเงิน: +{amount} | ยอดเงินคงเหลือปัจจุบัน: {currentMoney}");
    }

    // เมธอดสำหรับ "หักเงิน/ซื้อของ" (ส่งค่ากลับเป็น true ถ้าเงินพอหัก, ส่งกลับเป็น false ถ้าเงินไม่พอ)
    public bool TryRemoveMoney(float amount)
    {
        if (amount <= 0) return false;

        if (currentMoney >= amount)
        {
            currentMoney -= amount;

            // ตะโกนบอก UI ให้เปลี่ยนตัวเลขตาม
            OnMoneyChanged?.Invoke(currentMoney);

            Debug.Log($"[Currency] ใช้จ่ายเงิน: -{amount} | ยอดเงินคงเหลือปัจจุบัน: {currentMoney}");
            return true;
        }

        // ถ้าเงินไม่พอ ซื้อไม่ได้
        Debug.LogWarning($"[Currency] เงินไม่พอซื้อของ! ต้องการ: {amount} | แต่คุณมีเพียง: {currentMoney}");
        return false;
    }
}