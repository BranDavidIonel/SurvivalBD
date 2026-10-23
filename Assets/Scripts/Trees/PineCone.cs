using UnityEngine;

// a cone on the ground (made by PineForest): it grows into a young pine after a while, or can be picked up.
// When the player is near, it gets a soft light outline that shows through the grass.
public class PineCone : MonoBehaviour
{
    public PineConeData data;
    public InteractableObject pick;

    Outline outline;

    public void ShowOutline(bool show)
    {
        if (outline == null)
        {
            if (!show) return;
            outline = gameObject.AddComponent<Outline>();
            outline.OutlineMode = Outline.Mode.OutlineAll;
            outline.OutlineColor = new Color(1f, 0.86f, 0.55f, 0.85f);
            outline.OutlineWidth = 2.5f;
        }
        if (outline.enabled != show) outline.enabled = show;
    }
}
