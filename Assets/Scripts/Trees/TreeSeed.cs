using UnityEngine;

// A seed lying on the ground (made by Forest.cs): a pine cone, an acorn, a beechnut or a birch catkin. After a
// few game days it grows into a young tree where it lies (if there is room), or it can be picked up and planted.
// When the player is near, it gets a soft light outline that shows through the grass.
public class TreeSeed : MonoBehaviour
{
    public SeedData data;
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
