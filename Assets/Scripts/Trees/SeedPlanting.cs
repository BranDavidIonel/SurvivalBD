using UnityEngine;

// Planting a seed where you want (pine cone, acorn, beechnut, birch catkin): use it from the inventory or the quick
// bar (double tap / right click / tap in the quick bar), then the seed in a little mound of earth shows where you
// look, green outline where it can grow (red where it can't: water, sand by the sea, steep slopes, too close to
// another tree or to a building).
// Place: left click / the "Place" button. Cancel: X / the "Cancel" button (the seed goes back to the inventory).
// Made with the forest (EnvironmentBootstrap).
public class SeedPlanting : MonoBehaviour
{
    public static SeedPlanting Instance { get; private set; }
    public static bool Active => Instance != null && Instance.item != null;

    public float maxDistance = 9f;

    GameObject item;      // the seed in the inventory (hidden while planting)
    int species;
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

    // inventoryItem: the seed in the inventory / quick bar
    public void Begin(GameObject inventoryItem)
    {
        if (Active) Cancel();
        var forest = Forest.Instance;
        if (forest == null || inventoryItem == null) return;
        species = TreeSpecies.FromSeedItem(inventoryItem.name);
        if (species < 0) return;
        item = inventoryItem;
        item.SetActive(false);
        startFrame = Time.frameCount;

        ghost = SeedModels.MakePlantedModel("Seed to plant", species);
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

    void Update()
    {
        if (!Active) return;
        var forest = Forest.Instance;
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
        valid = forest.CanPlantAt(p, species);

        ghost.transform.position = p;
        outline.OutlineColor = valid ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.25f, 0.2f);

        if (Time.frameCount > startFrame + 1 && GameInput.PrimaryDown)
        {
            if (valid) Plant();
            else InventorySystem.Instance.ShowMessage("A tree can't grow here");
        }
        else if (GameInput.CancelDown)
        {
            Cancel();
        }
    }

    void Plant()
    {
        if (!Forest.Instance.PlantAt(spot, species)) return;
        var planted = item;
        item = null;
        DestroyImmediate(planted);
        InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);
        InventorySystem.Instance.ShowMessage("Planted: a little " + TreeSpecies.Get(species).name.ToLowerInvariant() + " will come out soon");
        End();
    }

    public void Cancel()
    {
        if (item != null) item.SetActive(true); // back in its slot
        item = null;
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
