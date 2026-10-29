using UnityEngine;

// Putting up a fence (Fences): use a fence panel or a gate from the inventory or the quick bar (double tap / right
// click), then the panel shows where you look: green where it can stand, red where it can't (water, a tree or a rock in
// the way, too steep). Near the free end of a panel already standing, the new one starts there and turns where you
// look, so the fence goes on in any shape; if its other end comes near another free end, it closes the yard there.
// Place: left click / "Place" - and while you have more panels the next one is ready at once. Cancel: X / "Cancel".
// Made with the rabbits (RabbitPopulation).
public class FencePlacing : MonoBehaviour
{
    public static FencePlacing Instance { get; private set; }
    public static bool Active => Instance != null && Instance.item != null;

    public float maxDistance = 12f;
    const float SnapReach = Fences.Length + 1.5f; // a free end this close to where you look: the panel starts there
    const float CloseReach = 1.6f;                // the other end this close to a free end: it closes the yard

    GameObject item;        // the panel in the inventory (hidden while placing)
    string itemName;
    BuildingKind kind;
    GameObject ghost;
    Vector3 centre, lastCentre;
    float yaw, lastYaw, length, lastLength;
    bool valid, lastValid;
    string why = "";
    int startFrame;
    Material green, red;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Begin(GameObject inventoryItem)
    {
        if (Active) Cancel();
        if (inventoryItem == null) return;
        item = inventoryItem;
        itemName = InventorySystem.CleanName(item.name);
        kind = itemName == Fences.GateItem ? BuildingKind.FenceGate : BuildingKind.Fence;
        item.SetActive(false);
        startFrame = Time.frameCount;
        lastLength = -1f;
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.DisableSelection();
            SelectionManager.Instance.enabled = false;
        }
        InventorySystem.Instance.ShowMessage(GameInput.TouchControls
            ? Lang.T("Look where the fence goes, press Place. Near the end of a fence, it goes on from there.",
                     "Privește unde vine gardul, apasă Așază. Lângă capătul unui gard, continuă de acolo.")
            : Lang.T("Look where the fence goes and click (X: stop). Near the end of a fence, it goes on from there.",
                     "Privește unde vine gardul și apasă (X: te oprești). Lângă capătul unui gard, continuă de acolo."));
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

        FindPose();
        if (ghost == null || Vector3.Distance(centre, lastCentre) > 0.04f || Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.7f
            || Mathf.Abs(length - lastLength) > 0.02f || valid != lastValid)
            MakeGhost();

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

    // where the panel goes: from the free end near the aim, or across the aim
    void FindPose()
    {
        var cam = Camera.main;
        var body = PlayerState.Instance != null && PlayerState.Instance.playerBody != null ? PlayerState.Instance.playerBody.transform : null;
        Vector3 player = body != null ? body.position : (cam != null ? cam.transform.position : Vector3.zero);
        Ray ray = GameInput.AimRay();
        Vector3 aim;
        if (Physics.Raycast(ray, out RaycastHit hit, 60f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerMovement>() == null)
            aim = hit.point;
        else
        {
            Vector3 f = ray.direction; f.y = 0f;
            aim = player + (f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward) * 6f;
        }
        Vector3 flat = aim - player; flat.y = 0f;
        if (flat.magnitude > maxDistance) aim = player + flat.normalized * maxDistance;
        if (flat.magnitude < 2f) aim = player + (flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward) * 2f;

        // a gap between two free ends near where you look (the panel that fills it closes the yard)
        var ends = Fences.FreeEnds();
        Vector3 start, end, dir;
        length = Fences.Length;
        int gapA = -1, gapB = -1;
        float gapBest = SnapReach;
        for (int i = 0; i < ends.Count; i++)
            for (int j = i + 1; j < ends.Count; j++)
            {
                if (ends[i].piece == ends[j].piece) continue; // (the two ends of one lonely panel)
                float span = Flat(ends[j].at - ends[i].at);
                if (span < Fences.MinLength || span > Fences.MaxLength) continue;
                float d = DistanceToSegment(aim, ends[i].at, ends[j].at);
                if (d < gapBest) { gapBest = d; gapA = i; gapB = j; }
            }
        // looking into the gap: it is filled. Else the nearest free end (the fence goes on from there), and if that
        // panel can't stand (it would lie over another one: you looked at a corner already closed), the gap near it.
        int best = -1;
        float bestD = SnapReach;
        bool intoGap = gapA >= 0 && gapBest < 2.4f;
        if (!intoGap)
            for (int i = 0; i < ends.Count; i++)
            {
                float d = Flat(ends[i].at - aim);
                if (d < bestD) { bestD = d; best = i; }
            }
        if (!intoGap && best >= 0 && gapA >= 0)
        {
            Vector3 toAim = aim - ends[best].at; toAim.y = 0f;
            Vector3 d = toAim.magnitude > 1f ? toAim.normalized : ends[best].outward;
            if (!CanStand(ends[best].at, ends[best].at + d * length, out _)) intoGap = true;
        }
        if (intoGap)
        {
            start = ends[gapA].at;
            end = ends[gapB].at;
            Vector3 d = end - start; d.y = 0f;
            length = d.magnitude;
            dir = d / Mathf.Max(0.001f, length);
        }
        else if (best >= 0)
        {
            start = ends[best].at;
            Vector3 toAim = aim - start; toAim.y = 0f;
            dir = toAim.magnitude > 1f ? toAim.normalized : ends[best].outward;
            end = start + dir * length;
            // its other end near another free end: the yard closes there
            for (int i = 0; i < ends.Count; i++)
            {
                if (i == best) continue;
                Vector3 gap = ends[i].at - end; gap.y = 0f;
                float span = Flat(ends[i].at - start);
                if (gap.magnitude < CloseReach && span >= Fences.MinLength && span <= Fences.MaxLength)
                {
                    end = ends[i].at;
                    Vector3 d = end - start; d.y = 0f;
                    dir = d.normalized;
                    length = span;
                    break;
                }
            }
        }
        else
        {
            // a new fence: across the view, its middle where you look
            Vector3 view = flat.sqrMagnitude > 0.01f ? flat.normalized : Vector3.forward;
            dir = Vector3.Cross(Vector3.up, view).normalized;
            start = aim - dir * length * 0.5f;
            end = aim + dir * length * 0.5f;
        }
        centre = (start + end) * 0.5f;
        centre.y = Forest.GroundY(centre);
        yaw = Quaternion.LookRotation(dir, Vector3.up).eulerAngles.y;
        valid = CanStand(start, end, out why);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z), A = new Vector2(a.x, a.z), B = new Vector2(b.x, b.z);
        Vector2 ab = B - A;
        float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(P - A, ab) / ab.sqrMagnitude) : 0f;
        return Vector2.Distance(P, A + ab * t);
    }

    bool CanStand(Vector3 a, Vector3 b, out string reason)
    {
        reason = "";
        var ocean = Ocean.Instance;
        float lowest = float.MaxValue, highest = float.MinValue;
        for (int i = 0; i <= 8; i++)
        {
            Vector3 p = Vector3.Lerp(a, b, i / 8f);
            if (River.IsWater(p, 0.8f) || (MiracleStream.Instance != null && MiracleStream.Instance.DistanceToWater(p) < 0.8f))
            { reason = Lang.T("Not in the water", "Nu în apă"); return false; }
            float g = Forest.GroundY(p);
            if (ocean != null && (g < ocean.Level + 0.4f || ocean.DistanceFromShore(p) > -3f)) { reason = Lang.T("Not by the sea", "Nu lângă mare"); return false; }
            lowest = Mathf.Min(lowest, g); highest = Mathf.Max(highest, g);
        }
        float span = Flat(b - a);
        float steep = (highest - lowest) / Mathf.Max(0.5f, span);
        if (steep > (kind == BuildingKind.FenceGate ? 0.12f : 0.6f))
        {
            reason = kind == BuildingKind.FenceGate ? Lang.T("The gate needs flat ground", "Poarta are nevoie de loc drept")
                                                    : Lang.T("Too steep here", "Prea abrupt aici");
            return false;
        }
        // nothing solid in the way (the panel's own neighbours only touch it at the ends)
        Vector3 dir = (b - a); dir.y = 0f; dir.Normalize();
        Vector3 mid = (a + b) * 0.5f;
        mid.y = (lowest + highest) * 0.5f + 1.1f;
        var rotation = Quaternion.LookRotation(dir, Vector3.up);
        var half = new Vector3(0.25f, 0.75f + (highest - lowest) * 0.5f, Mathf.Max(0.2f, span * 0.5f - 0.45f));
        foreach (var c in Physics.OverlapBox(mid, half, rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c is TerrainCollider || c is CharacterController) continue;
            if (c.GetComponentInParent<PlayerMovement>() != null) continue;
            if (c.GetComponent<InteractableObject>() != null || c.GetComponent<TreeSeed>() != null) continue;
            if (c.GetComponentInParent<ScenePlant>() != null || c.GetComponentInParent<Animal>() != null) continue;
            if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
            if (ghost != null && c.transform.IsChildOf(ghost.transform)) continue;
            reason = c.GetComponentInParent<ForestTree>() != null ? Lang.T("A tree is in the way (chop it first)", "Un copac e în cale (taie-l întâi)")
                                                                  : Lang.T("Something is in the way", "Ceva e în cale");
            return false;
        }
        return true;
    }

    void MakeGhost()
    {
        DestroyGhost();
        ghost = Fences.Create(kind, centre, yaw, length, false);
        ghost.name = kind == BuildingKind.FenceGate ? "Gate to place" : "Fence to place";
        if (green == null) MakeMaterials();
        var m = valid ? green : red;
        foreach (var r in ghost.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        lastCentre = centre; lastYaw = yaw; lastLength = length; lastValid = valid;
    }

    // the building ghost's green and red (the old wall's), else plain see-through colours
    void MakeMaterials()
    {
        var wall = Resources.Load<GameObject>("WallModel");
        var settings = wall != null ? wall.GetComponent<Constructable>() : null;
        green = settings != null && settings.greenMaterial != null ? settings.greenMaterial : Plain(new Color(0.3f, 1f, 0.35f));
        red = settings != null && settings.redMaterial != null ? settings.redMaterial : Plain(new Color(1f, 0.25f, 0.2f));
    }

    static Material Plain(Color c) => new Material(Shader.Find("Standard")) { color = c, hideFlags = HideFlags.DontSave };

    void DestroyGhost()
    {
        if (ghost == null) return;
        Fences.DestroyMeshes(ghost);
        Destroy(ghost);
        ghost = null;
    }

    void Place()
    {
        var go = Fences.Create(kind, centre, yaw, length, true);
        var manager = ConstructionManager.Instance;
        Transform parent = manager != null && manager.transform.parent != null ? manager.transform.parent.parent : null;
        if (parent != null) go.transform.SetParent(parent, true);
        go.tag = "placedWall";
        Buildings.OnPlaced(go, kind, length);
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();

        var used = item;
        item = null;
        DestroyImmediate(used);
        InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();

        // more panels of the same kind: the next one is ready at once
        var next = FindItem(itemName);
        if (next != null)
        {
            item = next;
            item.SetActive(false);
            startFrame = Time.frameCount;
            lastLength = -1f;
            int left = InventorySystem.Instance.CountItem(itemName);
            InventorySystem.Instance.ShowMessage(Lang.T(left + (left == 1 ? " panel" : " panels") + " left  (X / Cancel: stop)",
                "Mai ai " + left + (left == 1 ? " panou" : " panouri") + "  (X / Anulează: te oprești)"));
            return;
        }
        InventorySystem.Instance.ShowMessage(kind == BuildingKind.FenceGate
            ? Lang.T("Gate built. Look at it and click to open or shut it.", "Poarta e gata. Privește-o și apasă ca s-o deschizi sau s-o închizi.")
            : Lang.T("Fence built. Close the yard with a gate.", "Gardul e gata. Închide curtea cu o poartă."));
        End();
    }

    // another panel of this kind in the inventory or the quick bar
    static GameObject FindItem(string name)
    {
        var inventory = InventorySystem.Instance;
        foreach (var slot in inventory.slotList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name)
                return slot.transform.GetChild(0).gameObject;
        if (EquipSystem.Instance != null)
            foreach (var slot in EquipSystem.Instance.quickSlotsList)
            {
                if (slot == null || slot.transform.childCount == 0) continue;
                var child = slot.transform.GetChild(0);
                var info = child.GetComponent<InventoryItem>();
                if (InventorySystem.CleanName(child.name) == name && (info == null || !info.isSelected)) return child.gameObject;
            }
        return null;
    }

    public void Cancel()
    {
        if (item != null) item.SetActive(true);
        item = null;
        End();
    }

    void End()
    {
        DestroyGhost();
        if (SelectionManager.Instance != null)
        {
            SelectionManager.Instance.enabled = true;
            SelectionManager.Instance.EnableSelection();
        }
    }
}
