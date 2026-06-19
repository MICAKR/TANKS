using UnityEngine;

public class InventoryUI : MonoBehaviour
{
    public Transform slotParent;
    public GameObject slotPrefab;

    void Start() {
        InventoryManager.Instance.OnInventoryChanged += UpdateUI;
        Invoke("UpdateUI", 0.1f);
    }

    void UpdateUI()
    {
        // 1. ล้างช่องเก่าออก
        foreach (Transform child in slotParent) Destroy(child.gameObject);

        // 2. สร้างใหม่ตามจำนวนไอเทมที่มี
        foreach (int id in InventoryManager.Instance.inventoryIds)
        {
            var itemData = InventoryManager.Instance.GetItemData(id);
            var slot = Instantiate(slotPrefab, slotParent).GetComponent<InventorySlot>();
            slot.SetSlot(itemData);
        }
    }
}