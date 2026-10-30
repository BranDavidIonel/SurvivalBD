using System.Collections.Generic;
using UnityEngine;

// The spear: a straight wooden shaft with a knapped stone point tied on with hemp cord.
//  - made in Crafting > Tools from 2 sticks, when the player has a stone axe or an iron axe (the axe knaps the point
//    and is not used up: CraftingSystem)
//  - put in the quick bar and taken in the hand like the axe; a "+" in the middle of the screen shows where it goes
//    (SelectionManager); left click / the big "Throw" button throws it (SpearHeld)
//  - it flies point first in a gentle arc to the "+" (10-15 m is easy, farther works too) and kills a rabbit it hits;
//    where it lands it sticks (ThrownSpear): in the ground, a tree, a rock, or the rabbit's body
//  - it is taken back like anything lying around (the hand icon, "Spear"), and the save keeps it (DroppedItems)
// The model is made in code (here); the editor tool Tools > Nature > 6. Make The Spear Item makes the inventory item
// (Resources/spear) and its icon from it. Nothing else needs a prefab.
public static class Spear
{
    public const string Item = "spear";
    public const string Name = "Spear";       // its name in the game (Translations puts it in Romanian)
    public const int Damage = 20;             // a rabbit has 20 (RabbitLife): one hit kills it (Animal.TakeDamage)
    public const float TipForward = 1.36f;    // metres from the grip (the model's origin) to the tip, along +Z
    public const float ButtBack = 0.84f;      // metres from the grip to the back end
    public const float Length = TipForward + ButtBack;
    public const float ThrowSpeed = 24f;      // metres per second when it leaves the hand
    public const float Gravity = 9.81f;
    public const float AimReach = 40f;        // what the "+" is on counts this far; farther, it aims at this distance

    public static bool IsSpear(string name) => !string.IsNullOrEmpty(name) && InventorySystem.CleanName(name) == Item;

    // the spear is in the player's hand now
    public static bool IsHeld
    {
        get
        {
            var equip = EquipSystem.Instance;
            return equip != null && equip.selectedItem != null && IsSpear(equip.selectedItem.name);
        }
    }

    // ---------------- aiming ---------------- //

    // the point under the "+" (the player, the things lying around and other spears don't count), or a point
    // AimReach metres away along the aim when it is on nothing
    public static Vector3 AimPoint()
    {
        Ray ray = GameInput.AimRay();
        var hits = Physics.RaycastAll(ray, AimReach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = AimReach;
        foreach (var hit in hits)
        {
            if (Ignored(hit.collider, null)) continue;
            if (hit.distance < best) best = hit.distance;
        }
        return ray.GetPoint(best);
    }

    // what a flying spear passes through: the player, the things lying around (they have an InteractableObject: a
    // mushroom, a stone, another spear...), and the spear itself. An animal is always hit.
    public static bool Ignored(Collider c, Transform self)
    {
        if (c == null) return true;
        if (self != null && c.transform.IsChildOf(self)) return true;
        if (c.GetComponentInParent<PlayerMovement>() != null) return true;
        return c.GetComponentInParent<InteractableObject>() != null && c.GetComponentInParent<Animal>() == null;
    }

    // The velocity that throws from 'from' to 'to' at ThrowSpeed in a low arc (the flat way when it can't reach).
    public static Vector3 LaunchVelocity(Vector3 from, Vector3 to)
    {
        Vector3 d = to - from;
        Vector3 flat = new Vector3(d.x, 0f, d.z);
        float x = flat.magnitude, y = d.y, v = ThrowSpeed, g = Gravity;
        if (x < 0.3f) return (d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward) * v;
        float v2 = v * v;
        float root = v2 * v2 - g * (g * x * x + 2f * y * v2);
        float angle = root >= 0f ? Mathf.Atan((v2 - Mathf.Sqrt(root)) / (g * x)) : 45f * Mathf.Deg2Rad;
        return flat / x * (Mathf.Cos(angle) * v) + Vector3.up * (Mathf.Sin(angle) * v);
    }

    // ---------------- the inventory: the thrown spear leaves it, the next one comes to the hand ---------------- //

    // Called by the spear in the hand (SpearHeld) when it lets go: the spear item leaves the quick bar, and if the
    // player has another spear (quick bar first, then the inventory) it comes to the hand.
    public static void AfterThrow(GameObject heldModel)
    {
        var equip = EquipSystem.Instance;
        if (equip == null) return;
        var item = equip.selectedItem;
        int slot = equip.selectedNumber;
        equip.selectedItem = null;
        equip.selectedNumber = -1;
        if (equip.selectedItemModel == heldModel) equip.selectedItemModel = null;
        if (heldModel != null) Object.Destroy(heldModel);
        if (item != null && IsSpear(item.name))
        {
            item.transform.SetParent(null, false); // (the slot is empty right away)
            Object.Destroy(item);
        }
        if (InventorySystem.Instance != null) InventorySystem.Instance.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();
        EquipFromInventory(slot);
    }

    // Puts a spear in the hand: one already in the quick bar, or one from the inventory (it goes into a free quick
    // slot, 'preferredSlot' if it is free). False when the player has no spear, or the quick bar is full.
    // (Also for a test from a script: Spear.EquipFromInventory().)
    public static bool EquipFromInventory(int preferredSlot = -1)
    {
        var equip = EquipSystem.Instance;
        if (equip == null) return false;
        if (IsHeld) return true;
        for (int i = 0; i < equip.quickSlotsList.Count; i++)
        {
            var quick = equip.quickSlotsList[i];
            if (quick != null && quick.transform.childCount > 0 && IsSpear(quick.transform.GetChild(0).name))
            {
                equip.ToggleQuickSlot(i);
                return IsHeld;
            }
        }
        var inventory = InventorySystem.Instance;
        if (inventory == null) return false;
        GameObject spear = null;
        foreach (var s in inventory.slotList)
            if (s != null && s.transform.childCount > 0 && IsSpear(s.transform.GetChild(0).name)) { spear = s.transform.GetChild(0).gameObject; break; }
        if (spear == null) return false;
        GameObject free = null;
        if (preferredSlot >= 0 && preferredSlot < equip.quickSlotsList.Count && equip.quickSlotsList[preferredSlot].transform.childCount == 0)
            free = equip.quickSlotsList[preferredSlot];
        if (free == null) foreach (var quick in equip.quickSlotsList) if (quick != null && quick.transform.childCount == 0) { free = quick; break; }
        if (free == null) return false;
        spear.transform.SetParent(free.transform, false);
        var info = spear.GetComponent<InventoryItem>();
        if (info != null) info.isInsideQuickSlot = true;
        inventory.RecalculateList();
        QuickBarUI.Changed();
        equip.ToggleQuickSlot(equip.quickSlotsList.IndexOf(free));
        return IsHeld;
    }

    // a test from a script: throws the spear in the hand now (where the "+" is). False when no spear is ready.
    public static bool ThrowNow() => SpearHeld.Current != null && SpearHeld.Current.BeginThrow(true);

    // ---------------- in the world ---------------- //

    // A spear in the world that is not flying (a dropped one, or one from a saved game): standing = stuck at this
    // exact place and angle (in the ground, a tree...), else lying on the ground under 'at'.
    public static GameObject SpawnInWorld(Vector3 at, float yaw, float pitch, bool standing)
    {
        var go = BuildModel("Spear");
        var spear = go.AddComponent<ThrownSpear>();
        if (standing) spear.StandAt(at, Quaternion.Euler(pitch, yaw, 0f));
        else spear.LieOnGround(at, yaw);
        return go;
    }

    // a spear leaves the hand: from this place, with this velocity
    public static ThrownSpear Launch(Vector3 grip, Quaternion rotation, Vector3 velocity)
    {
        var go = BuildModel("Thrown spear");
        go.transform.SetPositionAndRotation(grip, rotation);
        var spear = go.AddComponent<ThrownSpear>();
        spear.Launch(velocity);
        return spear;
    }

    // ---------------- the model (made in code, low-poly) ---------------- //

    // The spear along +Z, its grip at the origin: a six-sided shaft, the stone point at the front, three turns of cord
    // tying the point on and four more for the grip.
    public static GameObject BuildModel(string name)
    {
        var root = new GameObject(name);
        Part(root.transform, "Shaft", ShaftMesh, ShaftMaterial, Vector3.zero);
        Part(root.transform, "Stone point", PointMesh, StoneMaterial, Vector3.zero);
        for (int i = 0; i < 3; i++) Part(root.transform, "Cord", CordRing, Buildings.Cord, new Vector3(0f, 0f, TipForward - 0.31f + i * 0.032f));
        for (int i = 0; i < 4; i++) Part(root.transform, "Grip cord", CordRing, Buildings.Cord, new Vector3(0f, 0f, -0.07f + i * 0.035f));
        return root;
    }

    public static void SetLayer(GameObject go, int layer)
    {
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
    }

    static void Part(Transform parent, string name, Mesh mesh, Material material, Vector3 at)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = at;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
    }

    static Material shaftMaterial, stoneMaterial;
    static Material ShaftMaterial => shaftMaterial != null ? shaftMaterial : (shaftMaterial = NewMaterial("Spear shaft", new Color(0.62f, 0.45f, 0.27f), 0.12f));
    static Material StoneMaterial => stoneMaterial != null ? stoneMaterial : (stoneMaterial = NewMaterial("Spear stone point", new Color(0.44f, 0.43f, 0.41f), 0.32f));

    static Material NewMaterial(string name, Color color, float gloss)
    {
        var m = new Material(Shader.Find("Standard")) { name = name, hideFlags = HideFlags.DontSave, color = color };
        m.SetFloat("_Glossiness", gloss);
        return m;
    }

    static Mesh shaftMesh, pointMesh, cordRing;

    // six flat sides, from the back end to just behind the point
    static Mesh ShaftMesh
    {
        get
        {
            if (shaftMesh != null) return shaftMesh;
            const int sides = 6;
            const float r = 0.026f;
            float z0 = -ButtBack, z1 = TipForward - 0.22f;
            var v = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 a = Around(i, sides, r, r), b = Around(i + 1, sides, r, r);
                Quad(v, a + Vector3.forward * z0, b + Vector3.forward * z0, b + Vector3.forward * z1, a + Vector3.forward * z1);
                Tri(v, Vector3.forward * z0, b + Vector3.forward * z0, a + Vector3.forward * z0); // the back end
                Tri(v, Vector3.forward * z1, a + Vector3.forward * z1, b + Vector3.forward * z1); // under the point
            }
            return shaftMesh = FlatMesh("Spear shaft", v);
        }
    }

    // a knapped stone leaf: flat (wide and thin), widest near its back, with a sharp tip
    static Mesh PointMesh
    {
        get
        {
            if (pointMesh != null) return pointMesh;
            const int sides = 4;
            Vector3 back = new Vector3(0f, 0f, TipForward - 0.31f), tip = new Vector3(0f, 0f, TipForward);
            float zA = TipForward - 0.21f, zB = TipForward - 0.11f;
            var v = new List<Vector3>();
            for (int i = 0; i < sides; i++)
            {
                Vector3 a0 = Around(i, sides, 0.047f, 0.018f) + Vector3.forward * zA, a1 = Around(i + 1, sides, 0.047f, 0.018f) + Vector3.forward * zA;
                Vector3 b0 = Around(i, sides, 0.034f, 0.014f) + Vector3.forward * zB, b1 = Around(i + 1, sides, 0.034f, 0.014f) + Vector3.forward * zB;
                Tri(v, back, a1, a0);
                Quad(v, a0, a1, b1, b0);
                Tri(v, b0, b1, tip);
            }
            return pointMesh = FlatMesh("Spear stone point", v);
        }
    }

    static Mesh CordRing
    {
        get
        {
            if (cordRing != null) return cordRing;
            cordRing = Buildings.RingMesh(0.029f, 0.009f, 10, 4);
            cordRing.name = "Spear cord";
            cordRing.hideFlags = HideFlags.DontSave;
            return cordRing;
        }
    }

    // a point of a ring around the Z axis (rx wide, ry high)
    static Vector3 Around(int i, int sides, float rx, float ry)
    {
        float a = i * Mathf.PI * 2f / sides;
        return new Vector3(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry, 0f);
    }

    static void Tri(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c) { v.Add(a); v.Add(b); v.Add(c); }
    static void Quad(List<Vector3> v, Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Tri(v, a, b, c); Tri(v, a, c, d); }

    // every triangle with its own corners: flat faces (the low-poly look)
    static Mesh FlatMesh(string name, List<Vector3> v)
    {
        var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
        mesh.SetVertices(v);
        var t = new int[v.Count];
        for (int i = 0; i < t.Length; i++) t[i] = i;
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
