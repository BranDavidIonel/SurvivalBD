using UnityEngine;
using UnityEngine.EventSystems;

// Invisible area behind all the other controls: dragging a finger on any free part of
// the screen turns the camera (like in most mobile shooters).
public class TouchLookArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    const int NoPointer = int.MinValue;
    int activePointer = NoPointer;
    Vector2 accumulated; // screen pixels dragged since the last frame

    // Pixels dragged since the last call (MobileControls reads this once per frame).
    public Vector2 TakeDelta()
    {
        Vector2 delta = accumulated;
        accumulated = Vector2.zero;
        return delta;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer == NoPointer) activePointer = eventData.pointerId;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointer) accumulated += eventData.delta;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == activePointer) activePointer = NoPointer;
    }

    void OnDisable()
    {
        activePointer = NoPointer;
        accumulated = Vector2.zero;
    }
}
