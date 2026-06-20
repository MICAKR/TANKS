using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

// 📦 สร้างชุดข้อมูลสำหรับ UI แต่ละตัว (เปลี่ยนเป็น class เพื่อให้ตั้งค่าเริ่มต้นได้)
[System.Serializable]
public class UIPanelConfig
{
    [Tooltip("ชื่อกำกับ (ตั้งไว้ดูเองใน Inspector)")]
    public string panelName;

    [Tooltip("เลข ID ประจำตัวของ UI นี้ (ห้ามตั้งซ้ำกัน เพื่อให้สคริปต์อื่นเรียกใช้ถูกตัว)")]
    public int panelID;

    public RectTransform panel;

    [Tooltip("ตำแหน่งตอนซ่อนของ Panel นี้")]
    public Vector2 hiddenPos;

    [Tooltip("ตำแหน่งตอนโชว์ของ Panel นี้")]
    public Vector2 shownPos;

    [Header("Animation Settings")]
    [Tooltip("ระยะเวลาที่ใช้เลื่อนเข้า-ออก")]
    public float animationDuration = 0.5f; // 👈 ย้ายมาปรับแยกอิสระตรงนี้

    [Tooltip("สไตล์แอนิเมชันตอนโชว์ (แนะนำ OutCubic หรือ OutBack)")]
    public Ease openEase = Ease.OutCubic;  // 👈 ปรับสไตล์แยกได้

    [Tooltip("สไตล์แอนิเมชันตอนซ่อน (แนะนำ InCubic หรือ InBack)")]
    public Ease closeEase = Ease.InCubic;  // 👈 ปรับสไตล์แยกได้
}

public class DecorationUIManager : MonoBehaviour
{
    [Header("UI Panels Settings")]
    [Tooltip("กดปุ่ม + เพื่อเพิ่ม UI Panel กี่ตัวก็ได้")]
    public List<UIPanelConfig> uiPanels;

    public void Initialize(bool isVisible)
    {
        foreach (var config in uiPanels)
        {
            if (config.panel != null)
            {
                config.panel.anchoredPosition = isVisible ? config.shownPos : config.hiddenPos;
            }
        }
    }

    // --------------------------------------------------
    // ระบบสำหรับ "โหมดจัดตู้" (เปิด/ปิด ทุกตัวพร้อมกัน)
    // --------------------------------------------------
    public void ShowUI()
    {
        foreach (var config in uiPanels)
        {
            if (config.panel != null)
            {
                config.panel.DOKill();
                // 🚨 เรียกใช้ค่าแอนิเมชันจาก config ของตัวเอง
                config.panel.DOAnchorPos(config.shownPos, config.animationDuration).SetEase(config.openEase);
            }
        }
    }

    public void HideUI()
    {
        foreach (var config in uiPanels)
        {
            if (config.panel != null)
            {
                config.panel.DOKill();
                // 🚨 เรียกใช้ค่าแอนิเมชันจาก config ของตัวเอง
                config.panel.DOAnchorPos(config.hiddenPos, config.animationDuration).SetEase(config.closeEase);
            }
        }
    }

    // --------------------------------------------------
    // 🚨 ระบบใหม่: สั่งเปิด/ปิด เจาะจงเฉพาะบางตัวผ่าน ID
    // --------------------------------------------------

    public void ShowPanelByID(int targetID)
    {
        foreach (var config in uiPanels)
        {
            if (config.panelID == targetID && config.panel != null)
            {
                config.panel.DOKill();
                config.panel.DOAnchorPos(config.shownPos, config.animationDuration).SetEase(config.openEase);
                break; // เจอตัวที่ใช่แล้ว สั่งหยุดลูปเลย
            }
        }
    }

    public void HidePanelByID(int targetID)
    {
        foreach (var config in uiPanels)
        {
            if (config.panelID == targetID && config.panel != null)
            {
                config.panel.DOKill();
                config.panel.DOAnchorPos(config.hiddenPos, config.animationDuration).SetEase(config.closeEase);
                break;
            }
        }
    }

    public void TogglePanelByID(int targetID, bool show)
    {
        if (show) ShowPanelByID(targetID);
        else HidePanelByID(targetID);
    }
}