using UnityEngine;

// Setting a snare where you want: use it from the inventory or the quick bar (double tap / right click / tap in the quick
// bar), then the snare shows where you look, green where it can stand (red: water, the beach by the sea, too close to
// another snare). Place: left click / "Place". Cancel: X / "Cancel" (it goes back to the inventory).
// Made with the rabbits (RabbitPopulation).
public class SnarePlacing : MonoBehaviour
{
    public static SnarePlacing Instance { get; private set; }
    public static bool Active => Instance != null && Instance.item != null;

    public float maxDistance = 8f;

    // the same way of setting things on the ground: a snare, or the canon's cross (level 9, on the other mountain)
    public enum Thing { Snare, Cross }
    Thing thing;

    GameObject item;      // the snare in the inventory (hidden while placing)
    GameObject ghost;
    Outline outline;
    Vector3 spot;
    float yaw;
    bool valid;
    int startFrame;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Begin(GameObject inventoryItem, Thing what = Thing.Snare)
    {
        if (Active) Cancel();
        if (inventoryItem == null) return;
        if (what == Thing.Cross && StoryManager.Instance != null && !StoryManager.Instance.CanonReadyForCross)
        {
            inventoryItem.SetActive(true);
            InventorySystem.Instance.ShowMessage(Lang.T("The cross goes up last: first the rabbits, the pen and the trees of the canon",
                "Crucea se pune la sfârșit: întâi iepurii, țarcul și copacii canonului"));
            return;
        }
        thing = what;
        item = inventoryItem;
        item.SetActive(false);
        startFrame = Time.frameCount;
        ghost = what == Thing.Cross ? CanonCross.BuildModel() : RabbitSnare.BuildModel();
        ghost.name = what == Thing.Cross ? "Cross to raise" : "Snare to set";
        outline = ghost.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineAll;
        outline.OutlineWidth = 5f;
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.DisableSelection();
            SelectionManager.Instance.enabled = false;
        }
        if (what == Thing.Cross)
            InventorySystem.Instance.ShowMessage(Lang.T("Raise it on the top of the other mountain", "Înalț-o pe vârful celuilalt munte"));
        else
            InventorySystem.Instance.ShowMessage(GameInput.TouchControls
                ? Lang.T("Look where to set it (near rabbits), press Place", "Privește unde s-o pui (lângă iepuri), apasă Așază")
                : Lang.T("Look where to set it (near rabbits) and click (X: cancel)", "Privește unde s-o pui (lângă iepuri) și apasă (X: anulezi)"));
        Update();
    }

    void Update()
    {
        if (!Active) return;
        bool menuOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        if (InventorySystem.Instance.isOpen || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen) || menuOpen)
        {
            Cancel();
            return;
        }

        var cam = Camera.main;
        var body = PlayerState.Instance != null && PlayerState.Instance.playerBody != null ? PlayerState.Instance.playerBody.transform : null;
        Vector3 player = body != null ? body.position : (cam != null ? cam.transform.position : Vector3.zero);
        Ray ray = GameInput.AimRay();
        Vector3 p;
        if (Physics.Raycast(ray, out RaycastHit hit, 200f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerMovement>() == null && hit.collider.GetComponentInParent<RabbitSnare>() == null)
        {
            p = hit.point;
        }
        else
        {
            Vector3 forward = ray.direction; forward.y = 0f;
            p = player + forward.normalized * 4f;
        }
        Vector3 flat = p - player; flat.y = 0f;
        if (flat.magnitude > maxDistance) p = player + flat.normalized * maxDistance;
        if (flat.magnitude < 1.5f) p = player + (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * 1.5f;
        p.y = Buildings.GroundAt(p);
        spot = p;
        // the loop faces the player (the rabbit runs into it from the side); the cross faces the player
        Vector3 face = flat.sqrMagnitude > 0.01f ? flat : Vector3.forward;
        yaw = thing == Thing.Cross ? Quaternion.LookRotation(-face).eulerAngles.y : Quaternion.LookRotation(Quaternion.Euler(0f, 90f, 0f) * face).eulerAngles.y;
        string why;
        valid = thing == Thing.Cross ? CanonCross.CanStandAt(p, out why) : CanStand(p, out why);

        ghost.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        outline.OutlineColor = valid ? new Color(0.3f, 1f, 0.35f) : new Color(1f, 0.25f, 0.2f);

        if (Time.frameCount > startFrame + 1 && GameInput.PrimaryDown)
        {
            if (valid) Place();
            else InventorySystem.Instance.ShowMessage(why);
        }
        else if (GameInput.CancelDown)
        {
            Cancel();
        }
    }

    static bool CanStand(Vector3 p, out string why)
    {
        why = "";
        if (River.IsWater(p, 1f)) { why = Lang.T("Not in the water", "Nu în apă"); return false; }
        var ocean = Ocean.Instance;
        if (ocean != null && (p.y < ocean.Level + 0.4f || ocean.DistanceFromShore(p) > -3f)) { why = Lang.T("Not by the sea", "Nu lângă mare"); return false; }
        foreach (var s in RabbitSnare.All)
        {
            if (s == null) continue;
            Vector3 d = s.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2f * 2f) { why = Lang.T("Too close to another snare", "Prea aproape de altă cursă"); return false; }
        }
        return true;
    }

    void Place()
    {
        if (thing == Thing.Cross)
        {
            CanonCross.Show(spot, yaw);
            if (StoryManager.Instance != null) StoryManager.Instance.OnCrossRaised(spot, yaw);
            var cross = item;
            item = null;
            DestroyImmediate(cross);
            InventorySystem.Instance.RecalculateList();
            if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
            QuickBarUI.Changed();
            if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();
            End();
            return;
        }
        RabbitSnare.Spawn(spot, yaw);
        var placed = item;
        item = null;
        DestroyImmediate(placed);
        InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);
        InventorySystem.Instance.ShowMessage(Lang.T("Snare set. Grown rabbits nearby will come to it.", "Cursa e pusă. Iepurii mari din jur vor veni la ea."));
        End();
    }

    public void Cancel()
    {
        if (item != null) item.SetActive(true);
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
