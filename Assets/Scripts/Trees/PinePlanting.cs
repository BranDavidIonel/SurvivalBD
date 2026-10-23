using UnityEngine;

// Planting a pine cone where you want: use a cone from the inventory or the quick bar (double tap / right click /
// tap in the quick bar), then the cone in a little mound of earth shows where you look, green outline where it can grow (red where
// it can't: water, sand by the sea, steep slopes, too close to another tree or to a building).
// Place: left click / the "Place" button. Cancel: X / the "Cancel" button (the cone goes back to the inventory).
// Made with the pine forest (EnvironmentBootstrap).
public class PinePlanting : MonoBehaviour
{
    public static PinePlanting Instance { get; private set; }
    public static bool Active => Instance != null && Instance.cone != null;

    public float maxDistance = 9f;

    GameObject cone;      // the cone in the inventory (hidden while planting)
    GameObject ghost;
    Outline outline;
    Vector3 spot;
    bool valid;
    int startFrame;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Begin(GameObject inventoryCone)
    {
        if (Active) Cancel();
        var forest = PineForest.Instance;
        if (forest == null || inventoryCone == null) return;
        cone = inventoryCone;
        cone.SetActive(false);
        startFrame = Time.frameCount;

        // the cone to plant (a little mound of earth with the cone in it), shown where the player looks
        ghost = forest.MakeSeedModel("Cone to plant");
        ghostBase = 0f;
        outline = ghost.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineWidth = 5f;

        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.DisableSelection();
            SelectionManager.Instance.enabled = false; // nothing is picked up or opened while planting
        }
        InventorySystem.Instance.ShowMessage(GameInput.TouchControls ? "Look where to plant it, press Place" : "Look where to plant it and click (X: cancel)");
        Update();
    }

    float ghostBase;

    void Update()
    {
        if (!Active) return;
        var forest = PineForest.Instance;
        if (forest == null) { Cancel(); return; }

        // a window or the menu was opened: stop
        bool menuOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        if (InventorySystem.Instance.isOpen || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen) || menuOpen)
        {
            Cancel();
            return;
        }

        // the spot: where the crosshair meets the ground, not too far; otherwise a few metres ahead
        Vector3 player = forest.PlayerPosition;
        Ray ray = GameInput.AimRay();
        Vector3 p;
        if (Physics.Raycast(ray, out RaycastHit hit, 300f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerMovement>() == null)
        {
            p = hit.point;
        }
        else
        {
            Vector3 forward = ray.direction; forward.y = 0f;
            p = player + forward.normalized * 4f; // looking at the sky
        }
        Vector3 flat = p - player; flat.y = 0f;
        if (flat.magnitude > maxDistance) p = player + flat.normalized * maxDistance;
        if (flat.magnitude < 1.5f) p = player + (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * 1.5f;
        p.y = forest.GroundHeight(p);
        spot = p;
        valid = forest.CanPlantAt(p);

        ghost.transform.position = p - Vector3.up * ghostBase;
        outline.OutlineColor = valid ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.25f, 0.2f);

        if (Time.frameCount > startFrame + 1 && GameInput.PrimaryDown)
        {
            if (valid) Plant();
            else InventorySystem.Instance.ShowMessage("A pine can't grow here");
        }
        else if (GameInput.CancelDown)
        {
            Cancel();
        }
    }

    void Plant()
    {
        if (!PineForest.Instance.PlantAt(spot)) return;
        var item = cone;
        cone = null;
        DestroyImmediate(item);
        InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);
        InventorySystem.Instance.ShowMessage("Cone planted: a little pine will come out soon");
        End();
    }

    public void Cancel()
    {
        if (cone != null) cone.SetActive(true); // back in its slot
        cone = null;
        End();
    }

    void End()
    {
        if (ghost != null) Destroy(ghost);
        ghost = null;
        outline = null;
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.enabled = true;
            SelectionManager.Instance.EnableSelection();
        }
    }
}
