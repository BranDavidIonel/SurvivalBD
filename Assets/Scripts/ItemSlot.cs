using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ItemSlot : MonoBehaviour, IDropHandler
{
    public GameObject Item
    {
        get
        {
            if (transform.childCount > 0)
            {
                return transform.GetChild(0).gameObject;
            }

            return null;
        }
    }


    public void OnDrop(PointerEventData eventData)
    {
        TryPlace(DragDrop.itemBeingDragged, DragDrop.dragStartParent);
    }

    // Puts a dragged item in this slot. If the slot already holds an item, the two swap places
    // (not when the item here is the one held in the hand). Returns false if nothing moved.
    public bool TryPlace(GameObject dragged, Transform from)
    {
        if (dragged == null || dragged.GetComponent<InventoryItem>() == null) return false;
        GameObject existing = Item;
        if (existing == dragged) return false;

        if (existing != null)
        {
            var fromSlot = from != null ? from.GetComponent<ItemSlot>() : null;
            if (fromSlot == null || fromSlot == this || fromSlot.Item != null) return false;
            var existingItem = existing.GetComponent<InventoryItem>();
            if (existingItem != null && existingItem.isSelected) return false;

            existing.transform.SetParent(from, false);
            existing.transform.localPosition = Vector3.zero;
            if (existingItem != null) existingItem.isInsideQuickSlot = from.CompareTag("QuickSlot");
        }

        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);

        dragged.transform.SetParent(transform, false);
        dragged.transform.localPosition = Vector3.zero;
        dragged.GetComponent<InventoryItem>().isInsideQuickSlot = transform.CompareTag("QuickSlot");

        InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();
        return true;
    }

    // The slot nearest to a screen point, if the point is inside it or close to it (within 60% of a slot's size).
    // Used when an item is let go just next to a slot.
    public static ItemSlot FindNearest(Vector2 screenPoint, Transform except)
    {
        ItemSlot best = null;
        float bestDistance = float.MaxValue;
        var corners = new Vector3[4];
        foreach (var slot in FindObjectsOfType<ItemSlot>())
        {
            if (slot.transform == except || !slot.isActiveAndEnabled) continue;
            ((RectTransform)slot.transform).GetWorldCorners(corners); // overlay canvas: world = screen pixels
            float width = corners[2].x - corners[0].x;
            float dx = Mathf.Max(corners[0].x - screenPoint.x, 0f, screenPoint.x - corners[2].x);
            float dy = Mathf.Max(corners[0].y - screenPoint.y, 0f, screenPoint.y - corners[2].y);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            if (distance > width * 0.6f) continue;
            // prefer the slot whose centre is closest (when the point is inside two padded areas)
            Vector2 centre = (corners[0] + corners[2]) * 0.5f;
            float score = distance * 4f + Vector2.Distance(centre, screenPoint);
            if (score < bestDistance)
            {
                bestDistance = score;
                best = slot;
            }
        }
        return best;
    }
}
