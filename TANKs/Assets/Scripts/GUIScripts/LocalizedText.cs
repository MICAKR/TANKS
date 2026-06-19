using UnityEngine;
using UnityEngine.UI;
using TMPro; // สำคัญมากถ้าคุณใช้ TextMeshPro

public class LocalizedText : MonoBehaviour
{
    [Header("รหัสคำแปล (Translation Key)")]
    [Tooltip("ใส่รหัส Key ที่ตรงกับในระบบ เช่น UI_START")]
    public string translationKey;

    private Text legacyText;
    private TextMeshProUGUI tmpText;

    void Start()
    {
        // ค้นหาว่า UI ตัวนี้ใช้ Text แบบไหน
        legacyText = GetComponent<Text>();
        tmpText = GetComponent<TextMeshProUGUI>();

        // สมัครรับข่าวสาร: ถ้าสมองกลางบอกว่าภาษาเปลี่ยน ให้เรียกฟังก์ชัน UpdateText
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += UpdateText;
        }

        // ดึงข้อความมาแสดงทันทีที่เปิดเกม
        UpdateText();
    }

    void OnDestroy()
    {
        // ยกเลิกรับข่าวสารตอน UI ถูกทำลาย (ป้องกัน Error)
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= UpdateText;
        }
    }

    // 🚨 🪐 ฟังก์ชันเปลี่ยนข้อความบนจอ
    private void UpdateText()
    {
        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(translationKey)) return;

        string translated = LocalizationManager.Instance.GetTranslatedText(translationKey);

        if (tmpText != null)
        {
            tmpText.text = translated;
        }
        else if (legacyText != null)
        {
            legacyText.text = translated;
        }
    }
}