using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class AppIcon : MonoBehaviour, IPointerClickHandler
{
    public string appName;
    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.3f; // เวลาที่อนุญาตให้คลิกได้

    public void OnPointerClick(PointerEventData eventData)
    {
        if (Time.time - lastClickTime < doubleClickThreshold)
        {
            // ทำการเปิดโปรแกรม
            WindowManager.Instance.OpenApp(appName);
        }
        lastClickTime = Time.time;
    }
}