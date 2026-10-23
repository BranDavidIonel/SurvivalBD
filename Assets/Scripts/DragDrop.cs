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
