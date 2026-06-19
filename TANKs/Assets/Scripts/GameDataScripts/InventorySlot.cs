using UnityEngine;
using UnityEngine.UI;
public class InventorySlot : MonoBehaviour
{
    public Image iconImage;

    public void SetSlot(ItemData data)
    {
        if (data == null) { iconImage.enabled = false; return; }
        iconImage.sprite = data.icon;
        iconImage.enabled = true;
    }
}