using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewFishData", menuName = "Aquarium/Fish Data")]
public class FishData : ScriptableObject
{
    [Header("📊 สถานะพื้นฐาน (Base Stats)")]
    public string speciesName;
    public float adultSize = 1.0f;
    public int daysToMature = 30;
    public float baseSpeed = 2.0f;
    public float attackPower = 10f;

    [Header("🧠 อารมณ์และนิสัย (Personality)")]
    [Range(0, 100)]
    [Tooltip("ความดีด: สูง=ว่ายพุ่งสลับหยุด, ต่ำ=ว่ายเนือยๆ")]
    public float hyperLevel = 50f;
    [Range(0, 100)]
    [Tooltip("ความขี้โมโห: สูง=ไล่กัดเพื่อนง่ายเมื่อหิวหรืออารมณ์เสีย")]
    public float baseAggression = 20f;
    [Tooltip("จำนวนฝูงที่ชอบ (1 = ชอบอยู่ตัวเดียว หวงถิ่น)")]
    public int preferredSchoolSize = 5;

    [Header("🍔 พฤติกรรมการกิน (Diet & Hunting)")]
    public DietType dietType;
    public HuntStyle huntingStyle;
    public FoodPreference foodPreference;

    [Header("🌊 ถิ่นที่อยู่ (Habitat)")]
    public SwimZone swimZone;
    public float preferredPH = 7.0f;
    public float preferredSpaceM3 = 0.05f; // พื้นที่ที่ต้องการต่อตัว

    [Header("❤️ ความสัมพันธ์ (Relationships)")]
    [Tooltip("ปลาที่อยู่ด้วยกันได้")]
    public List<string> compatibleSpecies;
    [Tooltip("ปลาที่ไม่ชอบหน้า (เจอกันไล่กัด)")]
    public List<string> hatedSpecies;
    [Tooltip("ปลาที่เป็นอาหาร (เหยื่อ)")]
    public List<string> preySpecies;
}