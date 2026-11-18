using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int count;

    public bool IsEmpty => item == null || count <= 0;

    public void Clear()
    {
        item = null;
        count = 0;
    }

    public void Set(ItemData newItem, int newCount)
    {
        item = newItem;
        count = newCount;
    }
}

public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [Header("Inventory Size")]
    public int hotbarSize = 8;
    public int totalSlots = 32; // 0~7: 핫바, 8~31: 메인 가방

    [Header("Starting Items")]
    [SerializeField] private List<ItemData> startingItems = new List<ItemData>();

    public InventorySlot[] slots;
    public InventorySlot heldSlot = new InventorySlot(); // 마우스로 집어 든 아이템
    public int selectedHotbarIndex = 0;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        slots = new InventorySlot[totalSlots];
        for (int i = 0; i < totalSlots; i++)
        {
            slots[i] = new InventorySlot();
        }
    }

    private void Start()
    {
        // 게임 시작 시 기본 곡괭이/횃불 지급
        foreach (ItemData item in startingItems)
        {
            if (item != null) AddItem(item, 1);
        }
        OnInventoryChanged?.Invoke();
    }

    public ItemData GetSelectedItem()
    {
        if (selectedHotbarIndex < 0 || selectedHotbarIndex >= hotbarSize) return null;
        return slots[selectedHotbarIndex].item;
    }

    public void SelectHotbarSlot(int index)
    {
        if (index < 0 || index >= hotbarSize) return;
        selectedHotbarIndex = index;
        OnInventoryChanged?.Invoke();
    }

    // 아이템 추가 후 가방이 가득 차서 못 넣은 잔여 수량 반환
    public int AddItem(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return 0;

        // 1단계: 이미 같은 아이템이 있는 슬롯에 겹쳐 담기
        if (item.isStackable)
        {
            for (int i = 0; i < totalSlots; i++)
            {
                if (slots[i].item == item && slots[i].count < item.maxStack)
                {
                    int space = item.maxStack - slots[i].count;
                    int toAdd = Mathf.Min(space, amount);
                    slots[i].count += toAdd;
                    amount -= toAdd;

                    if (amount <= 0)
                    {
                        OnInventoryChanged?.Invoke();
                        return 0;
                    }
                }
            }
        }

        // 2단계: 빈 슬롯에 새로 배치 (핫바 우선)
        for (int i = 0; i < totalSlots; i++)
        {
            if (slots[i].IsEmpty)
            {
                int toAdd = item.isStackable ? Mathf.Min(item.maxStack, amount) : 1;
                slots[i].Set(item, toAdd);
                amount -= toAdd;

                if (amount <= 0)
                {
                    OnInventoryChanged?.Invoke();
                    return 0;
                }
            }
        }

        OnInventoryChanged?.Invoke();
        return amount;
    }

    // 슬롯 좌클릭: 집기 / 내려놓기 / 합치기 / 교환
    public void OnSlotLeftClick(int index)
    {
        if (index < 0 || index >= totalSlots) return;
        InventorySlot clicked = slots[index];

        // 손이 비어있을 때 -> 슬롯 아이템 집기
        if (heldSlot.IsEmpty)
        {
            if (!clicked.IsEmpty)
            {
                heldSlot.Set(clicked.item, clicked.count);
                clicked.Clear();
            }
        }
        // 손에 아이템을 들고 있을 때
        else
        {
            if (clicked.IsEmpty)
            {
                clicked.Set(heldSlot.item, heldSlot.count);
                heldSlot.Clear();
            }
            else if (clicked.item == heldSlot.item && clicked.item.isStackable)
            {
                int space = clicked.item.maxStack - clicked.count;
                int toMove = Mathf.Min(space, heldSlot.count);
                clicked.count += toMove;
                heldSlot.count -= toMove;
                if (heldSlot.count <= 0) heldSlot.Clear();
            }
            else
            {
                // 서로 다른 아이템이면 위치 교환
                ItemData tempItem = clicked.item;
                int tempCount = clicked.count;
                clicked.Set(heldSlot.item, heldSlot.count);
                heldSlot.Set(tempItem, tempCount);
            }
        }

        OnInventoryChanged?.Invoke();
    }

    // 슬롯 우클릭: 절반 집기 또는 1개씩 내려놓기
    public void OnSlotRightClick(int index)
    {
        if (index < 0 || index >= totalSlots) return;
        InventorySlot clicked = slots[index];

        if (heldSlot.IsEmpty && !clicked.IsEmpty && clicked.count > 1)
        {
            int half = clicked.count / 2;
            heldSlot.Set(clicked.item, half);
            clicked.count -= half;
        }
        else if (!heldSlot.IsEmpty)
        {
            if (clicked.IsEmpty)
            {
                clicked.Set(heldSlot.item, 1);
                heldSlot.count--;
                if (heldSlot.count <= 0) heldSlot.Clear();
            }
            else if (clicked.item == heldSlot.item && clicked.item.isStackable && clicked.count < clicked.item.maxStack)
            {
                clicked.count++;
                heldSlot.count--;
                if (heldSlot.count <= 0) heldSlot.Clear();
            }
        }

        OnInventoryChanged?.Invoke();
    }
}