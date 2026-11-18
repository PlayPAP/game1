using UnityEngine;

public enum ItemType
{
    Resource,   // 광석, 주괴, 보석 등 재료
    Pickaxe,    // 곡괭이 (장착 시 채광력 증가)
    Tool,       // 횃불, 랜턴, 밧줄 등 도구
    Block       // 설치 가능한 블록
}

[CreateAssetMenu(fileName = "NewItemData", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemName = "새 아이템";
    public Sprite icon;                 // UI에 표시될 _x4 스프라이트 연결
    public ItemType itemType = ItemType.Resource;

    [Header("Stack Settings")]
    public bool isStackable = true;     // 곡괭이는 false, 광물/재료는 true
    public int maxStack = 99;

    [Header("Tool / Pickaxe Stats")]
    public int miningPower = 1;         // 곡괭이일 때 타일에 가하는 대미지
}