using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DragDrop : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IDragHandler
{

    //[SerializeField] private Canvas canvas;
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;

    public static GameObject itemBeingDragged;
    public static Transform dragStartParent; // the slot the dragged item came from
    Vector3 startPosition;
    Transform startParent;

    const float DragScale = 1.15f; // a bit bigger while dragged, so it shows around the finger



    private void Awake()
    {

        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

    }


    public void OnBeginDrag(PointerEventData eventData)
    {
        canvasGroup.alpha = .75f;
        //So the ray cast will ignore the item itself.
        canvasGroup.blocksRaycasts = false;
        startPosition = transform.position;
        startParent = transform.parent;
        dragStartParent = startParent;
        transform.SetParent(transform.root);
        transform.SetAsLastSibling(); // drawn above the windows
        transform.localScale = Vector3.one * DragScale;
        itemBeingDragged = gameObject;

    }

    public void OnDrag(PointerEventData eventData)
    {
        //So the item will move with our mouse (at same speed)  and so it will be consistant if the canvas has a different scale (other then 1);
        //on a Screen Space Overlay canvas the world position is in screen pixels, like eventData.delta
        rectTransform.position += (Vector3)eventData.delta;

    }



    // over one of the open windows (inventory, crafting, storage box, campfire) or the quick bar?
    static bool OverAWindow(Vector2 screenPoint)
    {
        var windows = new System.Collections.Generic.List<GameObject>();
        if (InventorySystem.Instance != null) windows.Add(InventorySystem.Instance.inventoryScreenUI);
        var crafting = CraftingSystem.Instance;
        if (crafting != null)
        {
            windows.Add(crafting.craftingScreenUI);
            windows.Add(crafting.toolsScreenUI);
            windows.Add(crafting.survivalScreenUI);
            windows.Add(crafting.refineScreenUI);
            windows.Add(crafting.constructionScreenUI);
        }
        if (StorageManager.Instance != null) windows.Add(StorageManager.Instance.StorageUI);
        if (EquipSystem.Instance != null) windows.Add(EquipSystem.Instance.quickSlotsPanel);
        foreach (var w in windows)
        {
            if (w == null || !w.activeInHierarchy) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint((RectTransform)w.transform, screenPoint, null)) return true;
        }
        if (EquipSystem.Instance != null)
            foreach (var slot in EquipSystem.Instance.quickSlotsList)
                if (slot != null && slot.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)slot.transform, screenPoint, null)) return true;
        return false;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // (a slot's OnDrop runs before this; if one took the item, its parent is that slot now)
        itemBeingDragged = null;

        if (transform.parent == transform.root)
        {
            // Not dropped exactly on a slot: put it in the nearest slot, if the finger let go close to one
            // (much easier on a phone). Dropped on the trash can: it waits there for the "throw away?" answer.
            bool onTrash = eventData.pointerCurrentRaycast.gameObject != null
                && eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<TrashSlot>() != null;
            ItemSlot nearest = onTrash ? null : ItemSlot.FindNearest(eventData.position, startParent);
            if (nearest == null || !nearest.TryPlace(gameObject, startParent))
            {
                transform.SetParent(startParent);
                transform.position = startPosition;
                // let go out of the windows (over the game itself): "drop it on the ground?" (it waits in its slot)
                if (!onTrash && nearest == null && !OverAWindow(eventData.position) && TrashSlot.Instance != null)
                    TrashSlot.Instance.AskDrop(gameObject);
            }
        }
        else if (transform.parent == startParent)
        {
            transform.position = startPosition;
        }

        dragStartParent = null;
        transform.localScale = Vector3.one;
        //no more transparent
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        var fitter = transform.parent != null ? transform.parent.GetComponent<SlotFitter>() : null;
        if (fitter != null) fitter.Fit();
    }
}
