using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Dry branches lying under the trees (the owner: more sticks, mostly by the trees). Every grown tree of the forest
// (Forest.cs) has 0-2 of its own branches on the ground around its trunk, always in the same places (worked out from
// where the tree stands, nothing is kept for the thousands of trees). Only the branches near the player are real
// objects, taken from a small pool, so the phone pays only for the few it can see.
//  - a branch gives a stick (Take, like the other branches) and lies there again after regrowHours (game hours)
//  - a felled tree takes its branches with it (it leaves its own, Forest.OnFelled); young trees have none yet
//  - the save keeps the branches that were taken and are not back yet (ForestData.branches)
public class FallenBranches : MonoBehaviour
{
    public static FallenBranches Instance { get; private set; }

    [Tooltip("The branches closer than this to the player are on the ground (metres): phone, PC")]
    public float showRadiusPhone = 28f;
    public float showRadiusPC = 36f;
    [Tooltip("Game hours until a taken branch lies there again")]
    public float regrowHours = 48f;
    [Tooltip("Trees lower than this (metres) have no dry branches yet")]
    public float minTreeHeight = 4f;

    const float PickReach = 4.4f;
    const int Models = 3;

    class Branch
    {
        public long key;
        public int model;
        public GameObject go;
        public InteractableObject pick;
        public bool seen;
    }

    readonly Dictionary<long, Branch> shown = new Dictionary<long, Branch>();
    readonly Dictionary<long, float> taken = new Dictionary<long, float>(); // key -> game hour it lies there again
    readonly Dictionary<InteractableObject, Branch> byPick = new Dictionary<InteractableObject, Branch>();
    readonly Stack<GameObject>[] pool = { new Stack<GameObject>(), new Stack<GameObject>(), new Stack<GameObject>() };
    readonly List<ForestTree> query = new List<ForestTree>();
    readonly List<long> gone = new List<long>();
    readonly Dictionary<Material, Material> instanced = new Dictionary<Material, Material>();
    GameObject[] prefabs;
    Transform root, poolRoot;
    Collider playerCollider;
    float tick, reachTick;

    public int ShownCount => shown.Count;
    public int TakenCount => taken.Count;
    float ShowRadius => GameInput.TouchControls ? showRadiusPhone : showRadiusPC;
    static float Clock => Vegetation.Instance != null ? Vegetation.Instance.Clock : Time.time / 15f;

    void Awake()
    {
        Instance = this;
        root = new GameObject("Fallen branches").transform;
        root.SetParent(transform, false);
        poolRoot = new GameObject("Unused fallen branches").transform; // (stays active: see Parked)
        poolRoot.SetParent(transform, false);
        prefabs = new GameObject[Models];
        for (int i = 0; i < Models; i++) prefabs[i] = Resources.Load<GameObject>("Vegetation/Branch " + (i + 1));
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) playerCollider = movement.GetComponent<CharacterController>();
    }

    void Update()
    {
        var forest = Forest.Instance;
        if (forest == null) return;
        tick -= Time.deltaTime;
        if (tick <= 0f) { tick = 0.5f; Refresh(forest); }
        reachTick -= Time.deltaTime;
        if (reachTick <= 0f)
        {
            reachTick = 0.1f;
            Vector3 p = forest.PlayerPosition;
            foreach (var b in shown.Values)
            {
                if (b.pick == null) continue;
                Vector3 d = b.go.transform.position - p;
                b.pick.playerInRange = d.sqrMagnitude < PickReach * PickReach;
            }
        }
    }

    // ---------------- where the branches of a tree lie ---------------- //

    // the same numbers for the same tree, every time (from where it stands)
    static uint Hash(Vector3 p)
    {
        uint h = (uint)Mathf.RoundToInt(p.x * 10f) * 73856093u ^ (uint)Mathf.RoundToInt(p.z * 10f) * 19349663u;
        h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
        return h;
    }

    static float Next(ref uint state)
    {
        state ^= state << 13; state ^= state >> 17; state ^= state << 5;
        return (state & 0xFFFFFF) / (float)0x1000000;
    }

    static long KeyOf(Vector3 treePosition, int index) =>
        ((long)Mathf.RoundToInt(treePosition.x * 10f) << 32) ^ (uint)(Mathf.RoundToInt(treePosition.z * 10f) * 4 + index);

    // how many branches lie under this tree (0-2), and where (index 0, 1)
    static int CountFor(ForestTree tree, out uint state)
    {
        state = Hash(tree.data.position) | 1u;
        float r = Next(ref state);
        return r < 0.25f ? 0 : r < 0.75f ? 1 : 2; // about one branch for every tree
    }

    static bool PlaceOf(ForestTree tree, ref uint state, out Vector3 p, out float yaw, out int model)
    {
        float angle = Next(ref state) * Mathf.PI * 2f;
        float crown = Mathf.Clamp(tree.CrownRadius * 0.8f, 1.2f, 5f);
        float distance = tree.TrunkRadius + 0.7f + Next(ref state) * crown;
        yaw = Next(ref state) * 360f;
        model = Mathf.Min(Models - 1, (int)(Next(ref state) * Models));
        p = tree.data.position + new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
        float ground = Forest.GroundY(p);
        p.y = ground + 0.05f;
        if (Ocean.Instance != null && ground < Ocean.Instance.Level + 0.3f) return false;
        if (River.IsWater(p, 0.5f)) return false;
        var river = River.Instance; // (not in the crater lake either)
        if (river != null && river.lakeRadius > 0f)
        {
            Vector3 d = p - river.lakeCentre; d.y = 0f;
            if (d.magnitude < river.lakeRadius + 1f && ground < river.lakeCentre.y + 0.3f) return false;
        }
        if (IslandPaths.KeepClear(p, 0.2f)) return false; // (Brother Ioan's paths and steps stay clear)
        return true;
    }

    // ---------------- the real objects near the player ---------------- //

    void Refresh(Forest forest)
    {
        Vector3 player = forest.PlayerPosition;
        float radius = ShowRadius;
        float clock = Clock;
        foreach (var b in shown.Values) b.seen = false;

        query.Clear();
        forest.TreesNear(player, radius + 6f, query);
        foreach (var tree in query)
        {
            if (tree == null || tree.Falling || tree.data.height < minTreeHeight) continue;
            Vector3 d = tree.data.position - player; d.y = 0f;
            if (d.sqrMagnitude > (radius + 6f) * (radius + 6f)) continue;
            int count = CountFor(tree, out uint state);
            for (int i = 0; i < count; i++)
            {
                if (!PlaceOf(tree, ref state, out Vector3 p, out float yaw, out int model)) continue;
                long key = KeyOf(tree.data.position, i);
                Vector3 dp = p - player; dp.y = 0f;
                if (dp.sqrMagnitude > radius * radius) continue;
                if (taken.TryGetValue(key, out float back))
                {
                    if (clock < back) continue;
                    taken.Remove(key); // lies there again
                }
                if (shown.TryGetValue(key, out var existing)) { existing.seen = true; continue; }
                var branch = Spawn(key, model, p, yaw);
                if (branch != null) { branch.seen = true; shown[key] = branch; }
            }
        }

        // the ones too far away now (or under a felled tree, or taken)
        gone.Clear();
        foreach (var pair in shown) if (!pair.Value.seen) gone.Add(pair.Key);
        foreach (long key in gone) Despawn(key);
    }

    // The unused branches are never switched off: they wait far under the island, hidden. (A collider switched off
    // gives no OnTriggerExit, so a campfire or a foundation being placed over a branch that was just taken stayed red;
    // and a collider switched off forgets Physics.IgnoreCollision with the player.)
    static readonly Vector3 Parked = new Vector3(0f, -5000f, 0f);
    readonly Dictionary<GameObject, Renderer[]> drawn = new Dictionary<GameObject, Renderer[]>();

    void Show(GameObject go, bool on)
    {
        if (drawn.TryGetValue(go, out var renderers))
            foreach (var r in renderers) if (r != null) r.enabled = on;
        var pick = go.GetComponent<InteractableObject>();
        if (pick != null) { pick.enabled = on; pick.playerInRange = false; }
    }

    Branch Spawn(long key, int model, Vector3 p, float yaw)
    {
        GameObject go = pool[model].Count > 0 ? pool[model].Pop() : Make(model);
        if (go == null) return null;
        go.transform.SetParent(root, false);
        go.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, yaw, 0f));
        Show(go, true);
        var box = go.GetComponent<BoxCollider>();
        if (box != null && playerCollider != null) Physics.IgnoreCollision(box, playerCollider);
        var pick = go.GetComponent<InteractableObject>();
        var branch = new Branch { key = key, model = model, go = go, pick = pick };
        byPick[pick] = branch;
        return branch;
    }

    void Despawn(long key)
    {
        if (!shown.TryGetValue(key, out var b)) return;
        shown.Remove(key);
        if (b.pick != null) byPick.Remove(b.pick);
        if (b.go != null)
        {
            Show(b.go, false);
            b.go.transform.SetParent(poolRoot, false);
            b.go.transform.position = Parked;
            pool[b.model].Push(b.go);
        }
    }

    // a branch like the ones Forest.SpawnBranch leaves under a felled tree
    GameObject Make(int model)
    {
        var prefab = prefabs[model];
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            foreach (var plant in go.GetComponentsInChildren<ScenePlant>(true)) DestroyImmediate(plant); // (not a scene plant)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) DestroyImmediate(c);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.08f, 0.7f, 0.08f);
        }
        go.name = "Branch";
        go.tag = "Pickable";
        var renderers = go.GetComponentsInChildren<Renderer>(true);
        drawn[go] = renderers;
        foreach (var r in renderers)
        {
            r.sharedMaterials = Instanced(r.sharedMaterials);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        var box = go.AddComponent<BoxCollider>();
        var filter = go.GetComponentInChildren<MeshFilter>();
        if (filter != null && filter.sharedMesh != null && filter.transform == go.transform)
        {
            box.center = filter.sharedMesh.bounds.center;
            box.size = Vector3.Max(filter.sharedMesh.bounds.size, Vector3.one * 0.25f);
        }
        if (playerCollider != null) Physics.IgnoreCollision(box, playerCollider);
        var pick = go.AddComponent<InteractableObject>();
        pick.ItemName = "stick";
        pick.displayName = "Branch";
        pick.onPicked = OnPicked;
        return go;
    }

    Material[] Instanced(Material[] materials)
    {
        var result = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var m = materials[i];
            if (m == null) continue;
            if (!instanced.TryGetValue(m, out var copy))
            {
                copy = new Material(m) { name = m.name + " (fallen branches)", enableInstancing = true, hideFlags = HideFlags.DontSave };
                instanced[m] = copy;
            }
            result[i] = copy;
        }
        return result;
    }

    // InteractableObject has put the stick in the inventory: the branch is gone until its time comes
    void OnPicked(InteractableObject pick)
    {
        if (!byPick.TryGetValue(pick, out var b)) return;
        taken[b.key] = Clock + regrowHours;
        Despawn(b.key);
    }

    // ---------------- save / load ---------------- //

    public List<TakenBranchData> GetData()
    {
        var list = new List<TakenBranchData>();
        float clock = Clock;
        foreach (var pair in taken) if (pair.Value > clock) list.Add(new TakenBranchData { key = pair.Key, backAt = pair.Value });
        return list;
    }

    public void SetData(List<TakenBranchData> data)
    {
        foreach (long key in new List<long>(shown.Keys)) Despawn(key);
        taken.Clear();
        if (data != null) foreach (var d in data) taken[d.key] = d.backAt;
        tick = 0f;
    }

    // ---------------- for the automatic tests ---------------- //

    // the branch objects near the player now (to aim at one)
    public IEnumerable<GameObject> ShownObjects()
    {
        foreach (var b in shown.Values) if (b.go != null) yield return b.go;
    }

    public void RefreshNow()
    {
        if (Forest.Instance != null) Refresh(Forest.Instance);
    }
}

[System.Serializable]
public class TakenBranchData
{
    public long key;      // which tree and which of its branches
    public float backAt;  // the game hour (Vegetation.Clock) it lies there again
}
