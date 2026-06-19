using System;
using System.Collections.Generic;
using UnityEngine;

public class LocalizationManager : MonoBehaviour
{
    // ทำเป็น Singleton เพื่อให้สคริปต์อื่นเรียกใช้ได้ง่ายๆ (LocalizationManager.Instance...)
    public static LocalizationManager Instance { get; private set; }

    public enum Language { Thai, English }

    [Header("ตั้งค่าภาษา")]
    public Language currentLanguage = Language.Thai;

    // Event เอาไว้ตะโกนบอก UI ทุกตัวในฉากเวลาภาษาเปลี่ยน จะได้อัปเดตข้อความพร้อมกัน
    public event Action OnLanguageChanged;

    // พจนานุกรมเก็บคำแปล
    private Dictionary<string, string> localizedText;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // เปลี่ยนฉากก็ไม่โดนทำลาย
        }
        else
        {
            Destroy(gameObject);
        }

        LoadLocalizedText();
    }

    // 🚨 🪐 ดึงข้อมูลคำแปล (ในอนาคตคุณสามารถเปลี่ยนให้มันไปดึงจากไฟล์ JSON หรือ CSV ได้)
    private void LoadLocalizedText()
    {
        localizedText = new Dictionary<string, string>();

        if (currentLanguage == Language.Thai)
        {
            localizedText.Add("TOOL_B_Sand", "ทราย");
            localizedText.Add("TOOL_B_Water", "น้ำ"); 
            localizedText.Add("TOOL_B_Smooth", "เกลี่ย");
            
        }
        else if (currentLanguage == Language.English)
        {
            localizedText.Add("TOOL_B_Sand", "Sand");
            localizedText.Add("TOOL_B_Water", "Water");
            localizedText.Add("TOOL_B_Smooth", "Smooth");
           
        }
    }

    // ฟังก์ชันให้ UI เรียกมาขอคำแปล
    public string GetTranslatedText(string key)
    {
        if (localizedText != null && localizedText.ContainsKey(key))
        {
            return localizedText[key];
        }

        // ถ้าหาคำแปลไม่เจอ ให้คืนค่า Key กลับไป จะได้รู้ว่าลืมแปลคำไหน
        return "<Missing:" + key + ">";
    }

    // ฟังก์ชันสำหรับกดปุ่มเปลี่ยนภาษา
    public void SetLanguage(Language newLanguage)
    {
        if (currentLanguage != newLanguage)
        {
            currentLanguage = newLanguage;
            LoadLocalizedText(); // โหลดพจนานุกรมเล่มใหม่

            // ตะโกนบอก UI ทุกตัวให้อัปเดตข้อความเดี๋ยวนี้!
            OnLanguageChanged?.Invoke();

            Debug.Log("🌐 เปลี่ยนภาษาเป็น: " + newLanguage);
        }
    }
    // =======================================================
    // 🛠️ ฟังก์ชันสำหรับให้ปุ่ม UI (Button) เรียกใช้งาน
    // =======================================================

    // 1. สำหรับปุ่มที่กดแล้วเปลี่ยนเป็น "ภาษาไทย" ทันที
    public void SetLanguageToThai()
    {
        SetLanguage(Language.Thai);
    }

    // 2. สำหรับปุ่มที่กดแล้วเปลี่ยนเป็น "ภาษาอังกฤษ" ทันที
    public void SetLanguageToEnglish()
    {
        SetLanguage(Language.English);
    }

    // 3. สำหรับ "ปุ่มเดียว" กดแล้วสลับไปมา (Thai <-> English)
    public void ToggleLanguage()
    {
        if (currentLanguage == Language.Thai)
        {
            SetLanguage(Language.English);
        }
        else
        {
            SetLanguage(Language.Thai);
        }
    }
}