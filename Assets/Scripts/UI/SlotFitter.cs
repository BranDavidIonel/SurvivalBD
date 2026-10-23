using UnityEngine;
using UnityEngine.UI;

// Put on every slot (inventory, quick bar, storage box, campfire) by GameWindowsLayout.
// The slots were made bigger in code, so the item inside is centred and sized to its slot here, whatever put it
// there (picked up, crafted, dragged, loaded from a save...). It also tells the item whether it is in a quick slot.
[DisallowMultipleComponent]
public class SlotFitter : MonoBehaviour
{
    public float fill = 0.78f; // part of the slot the item picture covers

    void OnEnable() => Fit();

    void OnTransformChildrenChanged()
    {
        Fit();
        QuickBarUI.Changed();
    }

    public void Fit()
    {
        if (transform.childCount == 0) return;
        var item = transform.GetChild(0) as RectTransform;
        if (item == null) return;

        var slot = (RectTransform)transform;
        float size = Mathf.Min(slot.rect.width, slot.rect.height) * fill;
        if (size <= 1f) return;

        // centre anchors and a fixed size (not "stretch"): the item keeps this size while it is dragged on the canvas
        item.anchorMin = item.anchorMax = item.pivot = new Vector2(0.5f, 0.5f);
        item.anchoredPosition3D = Vector3.zero;
        item.sizeDelta = new Vector2(size, size);
        item.localScale = Vector3.one;
        item.localRotation = Quaternion.identity;

        var image = item.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;

        var inventoryItem = item.GetComponent<InventoryItem>();
        if (inventoryItem != null) inventoryItem.isInsideQuickSlot = CompareTag("QuickSlot");
    }
}
