using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

public class WindowManager : MonoBehaviour
{
    public static WindowManager Instance;
    public float animDuration = 0.3f;

    [System.Serializable]
    public struct AppEntry
    {
        public string appName;
        public GameObject windowObject;
    }

    public List<AppEntry> appList; // ลากใส่ใน Inspector เอาชัวร์กว่าครับ

    void Awake() { Instance = this; }

    public void OpenApp(string appName)
    {
        AppEntry entry = appList.Find(x => x.appName == appName);

        if (entry.windowObject != null)
        {
            GameObject window = entry.windowObject;
            window.SetActive(true);
            window.transform.localScale = Vector3.zero;
            window.transform.DOScale(1f, animDuration).SetEase(Ease.OutBack);
        }
        else
        {
            Debug.LogWarning("ไม่พบโปรแกรมชื่อ: " + appName);
        }
        Debug.Log("พยายามเปิดโปรแกรม: " + appName);
    }

    // ... CloseApp เหมือนเดิม

public void CloseApp(GameObject window)
    {
        // ปิดด้วย DOTween
        window.transform.DOScale(0f, animDuration).SetEase(Ease.InBack)
              .OnComplete(() => window.SetActive(false));
    }
    // เพิ่มเมธอดนี้ลงใน WindowManager.cs
    public void ShutDownComputer()
    {
        // 1. ปิดหน้าต่างโปรแกรมทุกตัว
        foreach (var entry in appList)
        {
            if (entry.windowObject.activeSelf)
            {
                entry.windowObject.SetActive(false);
            }
        }

        // 2. เรียกให้คอมพิวเตอร์ปิดตัวเอง (ให้มันจัดการเมาส์เอง)
        ComputerClickable computer = Object.FindFirstObjectByType<ComputerClickable>();
        if (computer != null)
        {
            computer.ForceCloseComputer();
        }
    }
}