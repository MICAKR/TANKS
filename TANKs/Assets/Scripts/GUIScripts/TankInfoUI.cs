using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TankInfoUI : MonoBehaviour
{
    [Header("เป้าหมายตู้ปัจจุบัน")]
    public TankWaterQuality targetTank;

    [Header("UI Texts (เชื่อมต่อ TextMeshPro)")]
    public TextMeshProUGUI waterText;
    public TextMeshProUGUI sandText;
    public TextMeshProUGUI nitrogenText;
    public TextMeshProUGUI salinityText;
    public TextMeshProUGUI phText;

    [Header("Combined Slider Bar (แถบ 3 สี)")]
    [Tooltip("Image แถบสีฟ้า (น้ำ) - ตั้ง Image Type เป็น Filled")]
    public Image waterFillBar;
    [Tooltip("Image แถบสีเหลือง (ทราย) - ตั้ง Image Type เป็น Filled")]
    public Image sandFillBar;

    [Header("✨ ระบบแถบสี pH Spectrum")]
    [Tooltip("ลาก Component Slider ของแถบ pH มาใส่ตรงนี้")]
    public Slider phSpectrumSlider;

    [Header("Localization Keys")]
    public string waterKey = "INFO_WATER";
    public string sandKey = "INFO_SAND";
    public string nitrogenKey = "INFO_NITROGEN";
    public string salinityKey = "INFO_SALINITY";
    public string phKey = "INFO_PH";
    public string noWaterKey = "INFO_NO_WATER"; // 👈 เพิ่ม Key สำหรับตอนไม่มีน้ำ

    private bool isActive = false;

    void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += UpdateTextLabels;
    }

    void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= UpdateTextLabels;
    }

    public void DisplayTankInfo(TankWaterQuality tank)
    {
        targetTank = tank;
        isActive = (tank != null);

        if (phSpectrumSlider != null)
        {
            phSpectrumSlider.minValue = 0f;
            phSpectrumSlider.maxValue = 14f;
            phSpectrumSlider.interactable = false;
        }

        UpdateTextLabels();
    }

    public void HideInfo()
    {
        isActive = false;
        targetTank = null;
    }

    void Update()
    {
        if (!isActive || targetTank == null) return;
        UpdateTextLabels();
    }

    private void UpdateTextLabels()
    {
        if (LocalizationManager.Instance == null) return;

        string waterLbl = LocalizationManager.Instance.GetTranslatedText(waterKey);
        string sandLbl = LocalizationManager.Instance.GetTranslatedText(sandKey);
        string nitroLbl = LocalizationManager.Instance.GetTranslatedText(nitrogenKey);
        string saltLbl = LocalizationManager.Instance.GetTranslatedText(salinityKey);
        string phLbl = LocalizationManager.Instance.GetTranslatedText(phKey);
        string noWaterLbl = LocalizationManager.Instance.GetTranslatedText(noWaterKey); // 👈 ดึงคำแปล "ยังไม่มีน้ำ"

        float nitrogenPercent = 0f;
        if (targetTank.waterVolumeLiters > 0)
        {
            nitrogenPercent = (targetTank.nitrogen / targetTank.waterVolumeLiters) * 100f;
        }

        if (waterText) waterText.text = $"{waterLbl}: {targetTank.waterVolumeLiters:F1} L";
        if (sandText) sandText.text = $"{sandLbl}: {targetTank.sandVolumeLiters:F1} L";
        if (nitrogenText) nitrogenText.text = $"{nitroLbl}: {nitrogenPercent:F1} %";
        if (salinityText) salinityText.text = $"{saltLbl}: {targetTank.salinity:F1} ppt";

        // 🚨 เช็คว่ามีน้ำในตู้หรือไม่ 
        if (targetTank.waterVolumeLiters <= 0.01f)
        {
            // เอา {phLbl}: ออก เพื่อให้โชว์แค่คำว่า "ไม่มีน้ำ" โดดๆ เลย
            if (phText) phText.text = noWaterLbl;
        }
        else
        {
            // ถ้ามีน้ำ ก็โชว์ "ค่า pH: X" ตามปกติ
            if (phText) phText.text = $"{phLbl}: {targetTank.ph:F0}";
        }

        UpdateCombinedSlider();
        UpdatePHVisual();
    }

    private void UpdateCombinedSlider()
    {
        float maxVolume = targetTank.GetTotalTankVolumeLiters();
        if (maxVolume <= 0) return;

        float sandPercent = targetTank.sandVolumeLiters / maxVolume;
        float waterPercent = targetTank.waterVolumeLiters / maxVolume;

        if (sandFillBar != null)
        {
            sandFillBar.fillAmount = Mathf.Clamp01(sandPercent);
        }

        if (waterFillBar != null)
        {
            waterFillBar.fillAmount = Mathf.Clamp01(sandPercent + waterPercent);
        }
    }

    private void UpdatePHVisual()
    {
        if (phSpectrumSlider != null)
        {
            // 🚨 ถ้าไม่มีน้ำ ให้ขีดชี้กลับไปอยู่ตรงกลาง (7) ก่อน
            if (targetTank.waterVolumeLiters <= 0.01f)
            {
                phSpectrumSlider.value = 7f;
            }
            else
            {
                phSpectrumSlider.value = targetTank.ph;
            }
        }
    }
}