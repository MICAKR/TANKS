using System.Collections.Generic;
using UnityEngine;

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance;

    // เก็บ ID ไอเทมที่ครอบครอง
    public List<int> inventoryIds = new List<int>();

    // ฐานข้อมูลไอเทมทั้งหมดในเกม (ลากเอา ItemData มาใส่ตรงนี้)
    public List<ItemData> itemDatabase;

    void Awake() { Instance = this; }

    public void AddItem(int id)
    {
        inventoryIds.Add(id);
        OnInventoryChanged?.Invoke(); // แจ้งเตือน UI
    }

    public ItemData GetItemData(int id)
    {
        return itemDatabase.Find(x => x.id == id);
    }

    public event System.Action OnInventoryChanged;
}