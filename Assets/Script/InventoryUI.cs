using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [Header("UI Panels")]
    [SerializeField] private Transform hotbarParent;    // 하단 8칸 부모 (GridLayoutGroup)
    [SerializeField] private GameObject bagPanel;       // 가방 전체 패널 (I / Tab 토글)
    [SerializeField] private Transform bagGridParent;   // 가방 24칸 부모 (GridLayoutGroup)
    [SerializeField] private GameObject slotPrefab;

    [Header("Cursor Drag Icon")]
    [SerializeField] private Image cursorIcon;
    [SerializeField] private TextMeshProUGUI cursorCountText;

    private List<InventorySlotUI> slotUIs = new List<InventorySlotUI>();

    private void Start()
    {
        BuildSlotUIs();

        if (bagPanel != null) bagPanel.SetActive(false);
        if (cursorIcon != null) cursorIcon.raycastTarget = false;

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged += RefreshAll;
        }

        RefreshAll();
    }

    private void OnDestroy()
    {
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnInventoryChanged -= RefreshAll;
        }
    }

    private void BuildSlotUIs()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        // 0 ~ 7: 핫바 슬롯 생성
        for (int i = 0; i < inv.hotbarSize; i++)
        {
            GameObject obj = Instantiate(slotPrefab, hotbarParent);
            InventorySlotUI ui = obj.GetComponent<InventorySlotUI>();
            ui.Init(i);
            slotUIs.Add(ui);
        }

        // 8 ~ 31: 가방 슬롯 생성
        for (int i = inv.hotbarSize; i < inv.totalSlots; i++)
        {
            GameObject obj = Instantiate(slotPrefab, bagGridParent);
            InventorySlotUI ui = obj.GetComponent<InventorySlotUI>();
            ui.Init(i);
            slotUIs.Add(ui);
        }
    }

    private void Update()
    {
        HandleToggleInput();
        HandleHotbarInput();
        UpdateCursorItem();
    }

    private void HandleToggleInput()
    {
        if (Input.GetKeyDown(KeyCode.I) || Input.GetKeyDown(KeyCode.Tab))
        {
            if (bagPanel != null)
            {
                bool nextState = !bagPanel.activeSelf;
                bagPanel.SetActive(nextState);

                // 가방을 닫을 때 마우스에 들고 있던 아이템이 있으면 빈 슬롯으로 자동 복귀
                if (!nextState && InventoryManager.Instance != null && !InventoryManager.Instance.heldSlot.IsEmpty)
                {
                    InventorySlot held = InventoryManager.Instance.heldSlot;
                    int leftover = InventoryManager.Instance.AddItem(held.item, held.count);
                    if (leftover <= 0) held.Clear();
                    else held.count = leftover;

                    InventoryManager.Instance.SelectHotbarSlot(InventoryManager.Instance.selectedHotbarIndex);
                }
            }
        }
    }

    private void HandleHotbarInput()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        // 숫자키 1 ~ 8 입력
        for (int i = 0; i < inv.hotbarSize; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                inv.SelectHotbarSlot(i);
                return;
            }
        }

        // 마우스 휠 스크롤로 핫바 전환
        float scroll = Input.mouseScrollDelta.y;
        if (scroll > 0f)
        {
            int next = (inv.selectedHotbarIndex - 1 + inv.hotbarSize) % inv.hotbarSize;
            inv.SelectHotbarSlot(next);
        }
        else if (scroll < 0f)
        {
            int next = (inv.selectedHotbarIndex + 1) % inv.hotbarSize;
            inv.SelectHotbarSlot(next);
        }
    }

    private void UpdateCursorItem()
    {
        if (cursorIcon == null || InventoryManager.Instance == null) return;

        InventorySlot held = InventoryManager.Instance.heldSlot;
        if (held == null || held.IsEmpty)
        {
            cursorIcon.gameObject.SetActive(false);
        }
        else
        {
            cursorIcon.gameObject.SetActive(true);
            cursorIcon.transform.position = Input.mousePosition;
            cursorIcon.sprite = held.item.icon;
            cursorCountText.text = (held.item.isStackable && held.count > 1) ? held.count.ToString() : "";
        }
    }

    public void RefreshAll()
    {
        InventoryManager inv = InventoryManager.Instance;
        if (inv == null) return;

        for (int i = 0; i < slotUIs.Count; i++)
        {
            bool isSelected = (i == inv.selectedHotbarIndex);
            slotUIs[i].Refresh(inv.slots[i], isSelected);
        }
    }
}