using UnityEngine;
using TMPro;
using System;

public class TimeManager : MonoBehaviour
{
    // 🚨 แกนหลัก Singleton ให้สคริปต์อื่นเรียกใช้ผ่าน TimeManager.Instance
    public static TimeManager Instance { get; private set; }

    [Header("⚙️ ตั้งค่าความเร็วเวลา")]
    [Tooltip("1 วินาทีของจริง = กี่วินาทีในเกม?")]
    public float timeScale = 60f;
    [Tooltip("เริ่มเกมมาตอนกี่โมง? (0 - 23)")]
    public int startHour = 8;
    [Tooltip("เริ่มเกมมาตอนปี ค.ศ. อะไร?")]
    public int startYear = 2026; // 👈 เพิ่มช่องกำหนดปีเริ่มเกม (ตั้งต้นไว้ที่ 2026)

    [Header("⏰ UI แสดงผลเวลา (เช่น 08:00)")]
    public TextMeshProUGUI timeText1;
    public TextMeshProUGUI timeText2;

    [Header("📅 UI แสดงผลวันที่ (รูปแบบตัวเลขล้วน เช่น 01/01/2026)")]
    public TextMeshProUGUI dateText1;
    public TextMeshProUGUI dateText2;

    // ตัวแปรเก็บเวลาภายในเกม
    private float currentTime;
    private int currentDay = 1;   // วันที่ (1-30)
    private int currentMonth = 1; // เดือน (1-12)
    private int currentYear;      // ปีปัจจุบัน (จะถูกเซ็ตค่าตอน Start)

    // 📢 Event เสริม: ให้สคริปต์อื่นดักฟัง
    public Action<int, int, int> OnDateChanged;
    public Action<int, int> OnHourChanged;
    private int lastRecordedHour = -1;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        currentTime = startHour * 3600f;
        currentYear = startYear; // 🚨 ดึงปีเริ่มต้นจากที่คุณตั้งค่าไว้ใน Inspector มาใช้
        UpdateUI();
    }

    private void Update()
    {
        currentTime += Time.deltaTime * timeScale;

        // ระบบปฏิทิน: ใช้ while loop เผื่อในกรณีที่มีการกด "ข้ามเวลา" ไปหลายๆ วันพร้อมกัน
        while (currentTime >= 86400f)
        {
            currentTime -= 86400f;
            currentDay++;

            // ครบ 30 วัน -> ขึ้นเดือนใหม่
            if (currentDay > 30)
            {
                currentDay = 1;
                currentMonth++;

                // ครบ 12 เดือน -> ขึ้นปีใหม่
                if (currentMonth > 12)
                {
                    currentMonth = 1;
                    currentYear++; // ปีจะขยับเพิ่มขึ้นทีละ 1 (เช่น 2026 -> 2027)
                }
            }

            OnDateChanged?.Invoke(currentDay, currentMonth, currentYear);
        }

        UpdateUI();
        CheckEvents();
    }

    private void UpdateUI()
    {
        int hours = Mathf.FloorToInt(currentTime / 3600f);
        int minutes = Mathf.FloorToInt((currentTime % 3600f) / 60f);

        // ⏰ อัปเดตตัวเลขเวลา (เช่น 08:30)
        string timeString = $"{hours:00}:{minutes:00}";
        if (timeText1 != null) timeText1.text = timeString;
        if (timeText2 != null) timeText2.text = timeString;

        // 📅 อัปเดตตัวเลขวันที่ (แสดงผลเป็น 01/01/2026)
        // สำหรับปีใช้ :0000 เผื่อต้องการแสดงผลเลข 4 หลักให้สวยงามและเป็นสากลครับ
        string dateString = $"{currentDay:00}/{currentMonth:00}/{currentYear:0000}";
        if (dateText1 != null) dateText1.text = dateString;
        if (dateText2 != null) dateText2.text = dateString;
    }

    private void CheckEvents()
    {
        int currentHour = Mathf.FloorToInt(currentTime / 3600f);
        if (currentHour != lastRecordedHour)
        {
            lastRecordedHour = currentHour;
            OnHourChanged?.Invoke(currentDay, currentHour);
        }
    }

    // ==========================================
    // 🛠️ เมธอดสาธารณะ (ให้สคริปต์อื่นดึงไปใช้ได้เลย)
    // ==========================================

    public int GetCurrentDay() => currentDay;
    public int GetCurrentMonth() => currentMonth;
    public int GetCurrentYear() => currentYear;
    public int GetCurrentHour() => Mathf.FloorToInt(currentTime / 3600f);
    public int GetCurrentMinute() => Mathf.FloorToInt((currentTime % 3600f) / 60f);

    public void SkipTime(float hoursToSkip)
    {
        if (hoursToSkip <= 0) return;

        currentTime += hoursToSkip * 3600f;
        Debug.Log($"<color=cyan>[TimeManager] ข้ามเวลาไป {hoursToSkip} ชั่วโมง</color>");
    }
}