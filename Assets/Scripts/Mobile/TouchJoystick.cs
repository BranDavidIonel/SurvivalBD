using UnityEngine;
using UnityEngine.EventSystems;

// On-screen joystick. This object's rect is the touch zone: put a finger anywhere inside it,
// the joystick jumps under the finger and Value follows the drag (-1..1 on each axis).
// When the finger is lifted it goes back to its resting place.
public class TouchJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IInitializePotentialDragHandler
{
    public RectTransform background; // the ring
    public RectTransform handle;     // the knob inside the ring
    public float deadZone = 0.12f;

    public Vector2 Value { get; private set; }
    public bool IsPressed => activePointer != NoPointer;

    const int NoPointer = int.MinValue;
    int activePointer = NoPointer;
    Vector2 restPosition;
    bool restSaved;

    void Start()
    {
        SaveRestPosition();
    }

    void SaveRestPosition()
    {
        if (restSaved) return;
        restPosition = background.anchoredPosition;
        restSaved = true;
    }

    public void OnInitializePotentialDrag(PointerEventData eventData)
    {
        // react from the first pixel, not after the usual 10 px drag threshold
        eventData.useDragThreshold = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != NoPointer) return;
        SaveRestPosition();
        activePointer = eventData.pointerId;

        // Screen Space Overlay canvas: world position == screen pixels
        background.position = eventData.position;
        UpdateValue(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        UpdateValue(eventData.position);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        activePointer = NoPointer;
        Value = Vector2.zero;
        if (handle != null) handle.anchoredPosition = Vector2.zero;
        if (background != null && restSaved) background.anchoredPosition = restPosition;
    }

    void UpdateValue(Vector2 screenPosition)
    {
        float radiusPixels = background.rect.width * 0.5f * background.lossyScale.x;
        if (radiusPixels <= 0f) return;

        Vector2 offset = (screenPosition - (Vector2)background.position) / radiusPixels;
        offset = Vector2.ClampMagnitude(offset, 1f);

        handle.anchoredPosition = offset * background.rect.width * 0.5f;

        // dead zone, then rescale so movement starts smoothly from 0
        float magnitude = offset.magnitude;
        Value = magnitude < deadZone ? Vector2.zero : offset.normalized * ((magnitude - deadZone) / (1f - deadZone));
    }
}
