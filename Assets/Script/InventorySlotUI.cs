using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class InventorySlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI countText;
    [SerializeField] private GameObject selectedBorder; // 핫바 선택 테두리

    private int slotIndex;

    public void Init(int index)
    {
        slotIndex = index;
    }

    public void Refresh(InventorySlot slot, bool isSelected)
    {
        if (selectedBorder != null)
        {
            selectedBorder.SetActive(isSelected);
        }

        if (slot == null || slot.IsEmpty)
        {
            iconImage.enabled = false;
            countText.text = "";
        }
        else
        {
            iconImage.enabled = true;
            iconImage.sprite = slot.item.icon;
            countText.text = (slot.item.isStackable && slot.count > 1) ? slot.count.ToString() : "";
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (InventoryManager.Instance == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
        {
            InventoryManager.Instance.OnSlotLeftClick(slotIndex);
        }
        else if (eventData.button == PointerEventData.InputButton.Right)
        {
            InventoryManager.Instance.OnSlotRightClick(slotIndex);
        }
    }
}