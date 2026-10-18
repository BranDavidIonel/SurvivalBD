using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// On-screen button: pressing it is like pressing a key (see GameInput.TouchAction).
public class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public GameInput.TouchAction action;
    public Image image;
    public Text label;
    public Color normalColor = new Color(0f, 0f, 0f, 0.45f);
    public Color pressedColor = new Color(1f, 1f, 1f, 0.55f);

    const int NoPointer = int.MinValue;
    int activePointer = NoPointer;

    void Start()
    {
        if (image != null) image.color = normalColor;
    }

    public void SetLabel(string text)
    {
        if (label != null && label.text != text) label.text = text;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (activePointer != NoPointer) return;
        activePointer = eventData.pointerId;
        GameInput.PressTouch(action);
        if (image != null) image.color = pressedColor;
        transform.localScale = Vector3.one * 0.92f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointer) return;
        Release();
    }

    void OnDisable()
    {
        if (activePointer != NoPointer) Release();
    }

    void Release()
    {
        activePointer = NoPointer;
        GameInput.ReleaseTouch(action);
        if (image != null) image.color = normalColor;
        transform.localScale = Vector3.one;
    }
}
