using System.Collections.Generic;
using UnityEngine;

// Things the player drops on the ground from the inventory (drag an item out of the windows, then "Yes"): they lie
// in front of the player and can be picked up again. Each looks like the thing itself when the game has a model of
// it (a branch, a stone, a mushroom, a book, a log, meat...), otherwise like a small leather bag with the item's
// picture over it. The save keeps them (EnviromentData.dropped).
public static class DroppedItems
{
    // the model of the plants and things lying around (Resources/Vegetation) that shows each item
    static readonly Dictionary<string, string> Models = new Dictionary<string, string>
    {
        { "stick", "Vegetation/Branch 1" }, { "stone", "Vegetation/Stone 1" }, { "hay", "Vegetation/Dry Grass 1" },
        { "porcini", "Vegetation/Porcini" }, { "redmushroom", "Vegetation/Red Mushroom" }, { "fieldmushroom", "Vegetation/Field Mushroom" },
        { "hemp", "Vegetation/Hemp" }, { "heather", "Vegetation/Heather 1" }, { "chicory", "Vegetation/Chicory" },
        { "ironore", "Vegetation/Iron Ore" }, { "journal", "Vegetation/Old Journal" }, { "prayerbook", "Vegetation/Prayer Book" },
        { "plank", "Vegetation/Wreck Plank" },
    };

    public static readonly List<DroppedItem> All = new List<DroppedItem>();
    static Material logMaterial, bagMaterial;

    // the item leaves the inventory (or the quick bar) and lies on the ground in front of the player
    public static void DropFromInventory(GameObject inventoryItem)
    {
        if (inventoryItem == null) return;
        string item = InventorySystem.CleanName(inventoryItem.name);
        // a live rabbit is not put down: it is let go (and runs away, unless it is in a pen)
        if (RabbitPopulation.IsLiveRabbit(item))
        {
            RabbitPopulation.ReleaseFromInventory(inventoryItem);
            return;
        }
        string shown = NameOf(item);
        var equip = EquipSystem.Instance;
        if (equip != null && equip.selectedItem == inventoryItem)
        {
            // it was in the hand: the hand is empty now
            if (equip.selectedItemModel != null) Object.Destroy(equip.selectedItemModel);
            equip.selectedItemModel = null;
            equip.selectedItem = null;
            equip.selectedNumber = -1;
        }
        Object.DestroyImmediate(inventoryItem);
        if (InventorySystem.Instance != null) InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();

        var body = PlayerState.Instance != null && PlayerState.Instance.playerBody != null ? PlayerState.Instance.playerBody.transform : null;
        var cam = Camera.main;
        Vector3 from = body != null ? body.position : cam.transform.position;
        Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Spawn(item, from + forward * 1.4f + Random.insideUnitSphere * 0.25f, Random.Range(0f, 360f));
        if (InventorySystem.Instance != null) InventorySystem.Instance.ShowMessage(Lang.T("Dropped: ", "Aruncat: ") + Translations.Name(shown));
    }

    // the name shown in the game (the inventory item's own name: "Stone Axe" for "Axe")
    public static string NameOf(string item)
    {
        var prefab = Resources.Load<GameObject>(item);
        var info = prefab != null ? prefab.GetComponent<InventoryItem>() : null;
        return info != null && !string.IsNullOrEmpty(info.thisName) ? info.thisName : item;
    }

    public static GameObject Spawn(string item, Vector3 at, float yaw)
    {
        // a spear lies on the ground as the spear itself (ThrownSpear: taken back the same way)
        if (item == Spear.Item) return Spear.SpawnInWorld(at, yaw, 0f, false);
        GameObject go = null;
        InteractableObject pick = null;
        Transform picture = null;
        if (Models.TryGetValue(item, out var path) && Resources.Load<GameObject>(path) is GameObject model)
        {
            go = Object.Instantiate(model);
            foreach (var plant in go.GetComponentsInChildren<ScenePlant>(true)) Object.Destroy(plant);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }
        else if (Resources.Load<GameObject>(item + "_Model") is GameObject loot && loot.GetComponent<InteractableObject>() != null && loot.GetComponent<EquipableItem>() == null)
        {
            // the meat and the hide of a rabbit have their own model on the ground
            go = Object.Instantiate(loot);
            pick = go.GetComponent<InteractableObject>();
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) if (c.isTrigger) Object.Destroy(c);
        }
        else if (item == "log" || item == "pinelog")
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.45f, 0.55f, 0.45f);
            go.GetComponent<MeshRenderer>().sharedMaterial = LogMaterial(item == "pinelog");
        }
        else
        {
            // a small leather bag with the item's picture over it
            go = new GameObject("bag");
            var bag = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(bag.GetComponent<Collider>());
            bag.transform.SetParent(go.transform, false);
            bag.transform.localPosition = new Vector3(0f, 0.13f, 0f);
            bag.transform.localScale = new Vector3(0.34f, 0.26f, 0.28f);
            bag.GetComponent<MeshRenderer>().sharedMaterial = BagMaterial();
            picture = Picture(item, go.transform);
        }
        go.name = "Dropped " + item;
        go.tag = "Pickable";
        at.y = GroundUnder(at);
        bool log = item == "log" || item == "pinelog";
        go.transform.SetPositionAndRotation(at + (log ? Vector3.up * 0.22f : Vector3.zero),
            Quaternion.Euler(log ? 90f : 0f, yaw, 0f));
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

        // what the crosshair finds (and the hand picks up)
        var box = go.AddComponent<BoxCollider>();
        Bounds drawn = default;
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (!any) { drawn = r.bounds; any = true; } else drawn.Encapsulate(r.bounds);
        }
        Vector3 s = go.transform.lossyScale;
        Vector3 inverse = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.z)));
        if (any)
        {
            // (the drawn box, as seen by the object, at least 0.6 m wide: easy to hit with a finger)
            box.center = go.transform.InverseTransformPoint(drawn.center);
            Vector3 size = go.transform.InverseTransformVector(drawn.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            box.size = Vector3.Max(size, Vector3.Scale(new Vector3(0.6f, 0.45f, 0.6f), inverse));
        }
        var player = Object.FindObjectOfType<PlayerMovement>();
        var playerCollider = player != null ? player.GetComponent<CharacterController>() : null;
        if (playerCollider != null) Physics.IgnoreCollision(box, playerCollider);

        if (pick == null) pick = go.AddComponent<InteractableObject>();
        pick.ItemName = item;
        pick.displayName = NameOf(item);
        pick.onPicked = p => { if (p != null) Object.Destroy(p.gameObject); };
        var dropped = go.AddComponent<DroppedItem>();
        dropped.item = item;
        dropped.pick = pick;
        dropped.picture = picture;
        return go;
    }

    // the ground under a point: the cave's floor or a rock if there is one, else the island's ground
    public static float GroundUnder(Vector3 p)
    {
        float ground = Forest.GroundY(p);
        var hits = Physics.RaycastAll(p + Vector3.up * 2.2f, Vector3.down, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MinValue;
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerMovement>() != null || h.collider.GetComponentInParent<InteractableObject>() != null) continue;
            if (h.point.y > best) best = h.point.y;
        }
        return best > float.MinValue ? best : ground;
    }

    static Transform Picture(string item, Transform parent)
    {
        var prefab = Resources.Load<GameObject>(item);
        var image = prefab != null ? prefab.GetComponent<UnityEngine.UI.Image>() : null;
        var sprite = image != null ? image.sprite : null;
        if (sprite == null) return null;
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Object.Destroy(quad.GetComponent<Collider>());
        quad.name = "Picture";
        quad.transform.SetParent(parent, false);
        quad.transform.localPosition = new Vector3(0f, 0.62f, 0f);
        quad.transform.localScale = Vector3.one * 0.42f;
        var shader = Shader.Find("Unlit/Transparent");
        var m = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { name = "Dropped " + item, hideFlags = HideFlags.DontSave };
        m.mainTexture = sprite.texture;
        var rect = sprite.textureRect;
        m.mainTextureScale = new Vector2(rect.width / sprite.texture.width, rect.height / sprite.texture.height);
        m.mainTextureOffset = new Vector2(rect.x / sprite.texture.width, rect.y / sprite.texture.height);
        var r = quad.GetComponent<MeshRenderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return quad.transform;
    }

    static Material LogMaterial(bool pine)
    {
        if (logMaterial == null) logMaterial = new Material(Shader.Find("Standard")) { name = "Dropped log", hideFlags = HideFlags.DontSave, color = new Color(0.42f, 0.28f, 0.17f) };
        return logMaterial;
    }

    static Material BagMaterial()
    {
        if (bagMaterial == null) bagMaterial = new Material(Shader.Find("Standard")) { name = "Dropped bag", hideFlags = HideFlags.DontSave, color = new Color(0.45f, 0.3f, 0.18f) };
        bagMaterial.SetFloat("_Glossiness", 0.15f);
        return bagMaterial;
    }

    // ---- save / load ----

    public static List<DroppedItemData> GetData()
    {
        var list = new List<DroppedItemData>();
        foreach (var d in All) if (d != null) list.Add(new DroppedItemData
        {
            item = d.item, position = d.transform.position, yaw = d.transform.eulerAngles.y,
            pitch = d.standing ? d.transform.eulerAngles.x : 0f, standing = d.standing,
        });
        return list;
    }

    public static void SetData(List<DroppedItemData> saved)
    {
        foreach (var d in All.ToArray()) if (d != null) Object.Destroy(d.gameObject);
        All.Clear();
        if (saved == null) return;
        foreach (var d in saved)
        {
            if (string.IsNullOrEmpty(d.item)) continue;
            // a spear stuck in the ground or a tree comes back at its place and angle
            if (d.item == Spear.Item) Spear.SpawnInWorld(d.position, d.yaw, d.pitch, d.standing);
            else Spawn(d.item, d.position, d.yaw);
        }
    }
}

[System.Serializable]
public class DroppedItemData
{
    public string item;
    public Vector3 position;
    public float yaw;
    // a thrown spear stuck in the ground, a tree or a rock: its tilt (older saves: 0 and false, lying on the ground)
    public float pitch;
    public bool standing;
}

// on every dropped thing: close enough to pick up? Its picture turns to the camera.
public class DroppedItem : MonoBehaviour
{
    public string item;
    public InteractableObject pick;
    public Transform picture;
    public bool standing;   // a spear stuck where it landed (the save keeps its angle)
    Transform player;
    float tick;

    void OnEnable() { DroppedItems.All.Add(this); }
    void OnDisable() { DroppedItems.All.Remove(this); }

    void Update()
    {
        if (picture != null && Camera.main != null)
        {
            Vector3 look = picture.position - Camera.main.transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.001f) picture.rotation = Quaternion.LookRotation(look);
            picture.localPosition = new Vector3(0f, 0.62f + Mathf.Sin(Time.time * 2f) * 0.04f, 0f);
        }
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 0.1f;
        if (player == null)
        {
            var movement = FindObjectOfType<PlayerMovement>();
            if (movement != null) player = movement.transform;
        }
        if (pick != null && player != null) pick.playerInRange = (player.position - transform.position).sqrMagnitude < 3.4f * 3.4f;
    }
}
