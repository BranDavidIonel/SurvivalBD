using System.Collections.Generic;
using UnityEngine;

// The pine forest, made in code when the game scene loads (EnvironmentBootstrap).
//  - the pines are the ones placed in the scene: the "Fir_forest" groups (like Birch_forest: each pine can be moved,
//    turned, deleted or copied on its own, and has a ScenePine with its height) and the single firs (TreeFir).
//    When the game starts they become living pines, at the same place and size.
//  - 3 kinds of pines of the Low Poly Vegetation Kit
//  - they grow over game time: a young pine reaches its full size (30-40 m) in about daysToGrowUp game days
//  - grown pines drop cones; a cone left on the ground grows into a young pine after a while; a cone can be
//    picked up and planted anywhere (PinePlanting)
//  - chopping: small pines need 1-2 hits, big ones up to 8; it falls over and leaves 1 (small) to 5 (30 m and more)
//    dark pine logs and a stump
// The save keeps every pine, cone and stump (EnviromentData.pineForest); pines added to the scene later only
// show up in a new game.
public class PineForest : MonoBehaviour
{
    public static PineForest Instance { get; private set; }

    [Header("Growth (game time)")]
    public float daysToGrowUp = 10f;
    public float minFullHeight = 30f;
    public float maxFullHeight = 40f;
    public float saplingHeight = 0.7f;

    [Header("Cones")]
    public float coneFromHeight = 14f;                       // only grown pines make cones
    public Vector2 coneEveryHours = new Vector2(12f, 40f);   // game hours between two cones of one pine
    public Vector2 sproutAfterHours = new Vector2(48f, 96f); // a cone on the ground becomes a young pine
    public float startConeChance = 0.5f;                     // a new game: share of the grown pines with cones under them
    public int maxCones = 60;
    public int maxPines = 300;                               // cones stop growing into pines above this

    [Header("Planted cones")]
    public Vector2 plantedSproutHours = new Vector2(12f, 20f); // game hours until a planted cone sprouts
    public float sproutHeight = 0.35f;                         // the little pine that comes out of it

    public int seed = 20261023;

    public class Variant
    {
        public GameObject prefab;
        public Mesh mesh;
        public float nativeHeight;
        public float baseOffset;
        public Vector3 prefabScale;
    }

    // 0: bushy, 1: classic, 2: slim (the kit's Tree 2 is left out: with its few branches it looks like a dead tree)
    static readonly string[] VariantPaths = { "PineForest/Pine 1", "PineForest/Pine 3", "PineForest/Pine 5" };
    static List<Variant> variantCache;
    List<Variant> variants;

    // the pine models (also used by the Fir_forest preview in the editor)
    public static List<Variant> Variants
    {
        get
        {
            if (variantCache == null || variantCache.Count == 0 || variantCache[0].prefab == null) variantCache = LoadVariantList();
            return variantCache;
        }
    }
    readonly List<PineTree> trees = new List<PineTree>();
    readonly List<PineCone> cones = new List<PineCone>();
    readonly List<PineStump> stumps = new List<PineStump>();
    readonly List<PineSeed> seeds = new List<PineSeed>();
    readonly List<InteractableObject> logs = new List<InteractableObject>();
    Collider playerCollider;
    readonly Dictionary<Material, Material> instanced = new Dictionary<Material, Material>();

    Transform treeRoot, coneRoot, stumpRoot, logRoot, seedRoot;
    Material barkMaterial, woodMaterial, coneMaterial, soilMaterial;
    Mesh coneMesh, moundMesh;
    Terrain terrain;
    Transform player;
    bool built;
    float tick;
    System.Random random = new System.Random();

    public int PineCount => trees.Count;
    public int ConeCount => cones.Count;
    public Vector3 PlayerPosition => player != null ? player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

    void Awake()
    {
        Instance = this;
        treeRoot = NewRoot("Pines");
        coneRoot = NewRoot("Pine cones");
        stumpRoot = NewRoot("Pine stumps");
        seedRoot = NewRoot("Planted pine cones");
        logRoot = NewRoot("Pine logs");
        LoadVariants();
        MakeMaterials();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    Transform NewRoot(string name)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        return t;
    }

    void Start()
    {
        terrain = Terrain.activeTerrain;
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) { player = movement.transform; playerCollider = movement.GetComponent<CharacterController>(); }

        var firs = TakeSceneryFirs(); // the firs placed in the scene become pines of this forest
        if (!built) BuildNewForest(firs);
    }

    // ---------------- the pine models ---------------- //

    void LoadVariants()
    {
        variants = Variants;
    }

    static List<Variant> LoadVariantList()
    {
        var list = new List<Variant>();
        foreach (var path in VariantPaths)
        {
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) { Debug.LogWarning("PineForest: no Resources/" + path); continue; }
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var bounds = filter.sharedMesh.bounds;
            Vector3 scale = prefab.transform.localScale;
            list.Add(new Variant
            {
                prefab = prefab,
                mesh = filter.sharedMesh,
                prefabScale = scale,
                nativeHeight = Mathf.Max(0.01f, bounds.size.y * scale.y),
                baseOffset = bounds.min.y * scale.y,
            });
        }
        return list;
    }

    // copies of the pine materials that can be drawn many at once (GPU instancing): the original assets stay as they are
    public Material[] InstancedMaterials(Material[] materials)
    {
        var result = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var m = materials[i];
            if (m == null) continue;
            if (!instanced.TryGetValue(m, out var copy))
            {
                copy = new Material(m) { name = m.name + " (pines)", enableInstancing = true, hideFlags = HideFlags.DontSave };
                instanced[m] = copy;
            }
            result[i] = copy;
        }
        return result;
    }

    void MakeMaterials()
    {
        // the bark and the cut wood of the game's own chopped birch, the bark made much darker (pines have a black trunk)
        Material birchBark = null, birchWood = null;
        var chopped = Resources.Load<GameObject>("ChoppedTree");
        if (chopped != null)
        {
            foreach (var r in chopped.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Log" && birchBark == null) birchBark = r.sharedMaterial;
                if (r.name.StartsWith("inner_part") && birchWood == null) birchWood = r.sharedMaterial;
            }
        }
        var standard = Shader.Find("Standard");
        barkMaterial = birchBark != null ? new Material(birchBark) : new Material(standard);
        barkMaterial.name = "Pine bark (dark)";
        barkMaterial.color = birchBark != null ? new Color(0.3f, 0.26f, 0.24f) : new Color(0.12f, 0.09f, 0.08f);
        barkMaterial.hideFlags = HideFlags.DontSave;
        woodMaterial = birchWood != null ? new Material(birchWood) : new Material(standard) { color = new Color(0.78f, 0.62f, 0.4f) };
        woodMaterial.name = "Pine wood";
        woodMaterial.hideFlags = HideFlags.DontSave;
        coneMaterial = new Material(standard) { name = "Pine cone", color = Color.white, mainTexture = MakeConeTexture(), hideFlags = HideFlags.DontSave };
        if (coneMaterial.HasProperty("_Glossiness")) coneMaterial.SetFloat("_Glossiness", 0.12f);
        coneMesh = MakeConeMesh();
        soilMaterial = new Material(standard) { name = "Planted cone soil", color = new Color(0.2f, 0.13f, 0.07f), hideFlags = HideFlags.DontSave };
        if (soilMaterial.HasProperty("_Glossiness")) soilMaterial.SetFloat("_Glossiness", 0.05f);
        moundMesh = MakeMoundMesh();
    }

    // ---------------- a new game: the pines placed in the scene ---------------- //

    // The pines placed in the scene (the Fir_forest groups and the single firs: Tree 1/3/5 of the Low Poly kit)
    // are replaced by living pines of this forest at the same place and size. A ScenePine on one of them tells how
    // tall it may grow; without it, 30-40 m.
    List<PineData> TakeSceneryFirs()
    {
        var result = new List<PineData>();
        var meshes = new HashSet<Mesh>();
        foreach (var v in variants) meshes.Add(v.mesh);
        var rng = new System.Random(seed); // a new game always gets the same pines
        var filters = new List<MeshFilter>(FindObjectsOfType<MeshFilter>());
        filters.Sort((a, b) => a.transform.position.x != b.transform.position.x
            ? a.transform.position.x.CompareTo(b.transform.position.x) : a.transform.position.z.CompareTo(b.transform.position.z));
        foreach (var filter in filters)
        {
            if (filter.sharedMesh == null || !meshes.Contains(filter.sharedMesh)) continue;
            if (filter.transform.IsChildOf(transform)) continue;
            var r = filter.GetComponent<Renderer>();
            if (r == null) continue;
            float height = Mathf.Clamp(r.bounds.size.y, saplingHeight, maxFullHeight); // no pine taller than the limit
            var settings = filter.GetComponent<ScenePine>();
            float full = settings != null ? settings.growsUpTo : Mathf.Lerp(minFullHeight, maxFullHeight, (float)rng.NextDouble());
            int variant = variants.FindIndex(v => v.mesh == filter.sharedMesh);
            result.Add(new PineData
            {
                position = new Vector3(filter.transform.position.x, GroundHeight(filter.transform.position), filter.transform.position.z),
                rotation = filter.transform.eulerAngles.y,
                height = height,
                maxHeight = Mathf.Clamp(Mathf.Max(height, full), minFullHeight * 0.5f, maxFullHeight),
                variant = Mathf.Max(0, variant),
                coneHours = Mathf.Lerp(coneEveryHours.x, coneEveryHours.y, (float)rng.NextDouble()),
            });
            filter.gameObject.SetActive(false); // (hidden right away: Destroy only happens at the end of the frame)
            Destroy(filter.gameObject);
        }
        return result;
    }

    void BuildNewForest(List<PineData> firs)
    {
        built = true;
        Clear();
        foreach (var fir in firs) AddTree(fir);
        if (terrain == null || variants.Count == 0) return;

        AddStartCones();
    }

    // no pine (or stump) closer than spacing
    bool IsFreeOfPines(Vector3 p, float spacing)
    {
        float spacing2 = spacing * spacing;
        foreach (var t in trees)
        {
            if (t == null) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < spacing2) return false;
        }
        foreach (var s in stumps)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        foreach (var s in seeds)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        return true;
    }

    // a new game: a few cones already lie under the grown pines (so there are some to find and plant)
    void AddStartCones()
    {
        var rng = new System.Random(seed + 7);
        foreach (var t in trees.ToArray())
        {
            if (t == null || t.data.height < 18f) continue;
            if (rng.NextDouble() > startConeChance) continue;
            int count = 1 + (rng.NextDouble() < 0.4 ? 1 : 0);
            for (int i = 0; i < count; i++) DropCone(t, rng, (float)rng.NextDouble() * 24f);
        }
    }

    float RandomRange(float a, float b) => a + (float)random.NextDouble() * (b - a);

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------- where a pine can grow ---------------- //

    public float GroundHeight(Vector3 p) => GroundY(p);

    public static float GroundY(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
    }

    // On land (shoreMargin: how far from the sea, negative = inland), not too steep and (checkObjects) not on top of
    // something (a rock, a birch, a building...). Used in the game and by the Fir_forest preview in the editor.
    public static bool IsGoodGround(Vector3 p, float shoreMargin, bool checkObjects, Transform ignore = null)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null || terrain.terrainData == null) return false;
        var data = terrain.terrainData;
        Vector3 local = p - terrain.transform.position;
        float u = local.x / data.size.x, v = local.z / data.size.z;
        if (u < 0.02f || v < 0.02f || u > 0.98f || v > 0.98f) return false;
        if (data.GetSteepness(u, v) > 30f) return false;

        float ground = GroundY(p);
        var ocean = Ocean.Instance;
        if (ocean != null)
        {
            if (ground < ocean.Level + 0.8f) return false;
            if (ocean.DistanceFromShore(p) > shoreMargin) return false;
        }

        if (checkObjects)
        {
            Vector3 bottom = new Vector3(p.x, ground + 0.6f, p.z);
            foreach (var c in Physics.OverlapCapsule(bottom, bottom + Vector3.up * 2.5f, 1.1f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (c is TerrainCollider || c is CharacterController) continue;
                if (c.GetComponentInParent<PineTree>() != null) continue; // other pines: the spacing decides
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                return false;
            }
        }
        return true;
    }

    // good ground, and not too close to another pine or a stump
    public bool IsGoodSpot(Vector3 p, float spacing, float shoreMargin)
    {
        return IsGoodGround(p, shoreMargin, true) && IsFreeOfPines(p, spacing);
    }

    // ---------------- pines, cones, stumps ---------------- //

    PineTree AddTree(PineData data)
    {
        if (variants.Count == 0) return null;
        data.variant = Mathf.Clamp(data.variant, 0, variants.Count - 1);
        var go = new GameObject("Pine");
        go.transform.SetParent(treeRoot, false);
        var tree = go.AddComponent<PineTree>();
        tree.Setup(this, data, variants[data.variant]);
        trees.Add(tree);
        return tree;
    }

    PineCone AddCone(PineConeData data)
    {
        var go = new GameObject("Pine cone");
        go.transform.SetParent(coneRoot, false);
        go.transform.position = data.position;
        // lying on its side, a little tilted (the tip up)
        go.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(data.position.x * 37f + data.position.z * 11f, 360f), 0f) * Quaternion.Euler(72f, 0f, 0f);
        go.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = coneMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // a solid (not trigger) collider: the crosshair's ray only sees solid ones (Physics: Queries Hit Triggers is off).
        // The player walks through it; "close enough to pick it up" is worked out from the distance (UpdatePlayerReach).
        go.transform.localScale = Vector3.one * 1.2f; // 0.6 m: easy to see
        var hitBox = go.AddComponent<SphereCollider>();
        hitBox.radius = 0.34f;
        if (playerCollider != null) Physics.IgnoreCollision(hitBox, playerCollider);
        var pick = go.AddComponent<InteractableObject>();
        pick.ItemName = "pinecone";
        pick.displayName = "Pine Cone";
        go.tag = "Pickable";
        var cone = go.AddComponent<PineCone>();
        cone.data = data;
        cone.pick = pick;
        cones.Add(cone);
        return cone;
    }

    PineStump AddStump(PineStumpData data)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Pine stump";
        go.transform.SetParent(stumpRoot, false);
        float r = Mathf.Max(0.25f, data.radius);
        go.transform.position = data.position + Vector3.up * 0.15f;
        go.transform.localScale = new Vector3(r * 2f, 0.35f, r * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = barkMaterial;
        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(top.GetComponent<Collider>());
        top.name = "Cut";
        top.transform.SetParent(go.transform, false);
        top.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        top.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
        top.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;
        var stump = go.AddComponent<PineStump>();
        stump.data = data;
        stumps.Add(stump);
        return stump;
    }

    void SpawnLog(Vector3 position, Vector3 along, float thickness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Pine log";
        go.tag = "Pickable";
        go.transform.SetParent(logRoot, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, along) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = new Vector3(thickness, 1.1f, thickness);
        go.GetComponent<MeshRenderer>().sharedMaterial = barkMaterial;
        var body = go.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.maxDepenetrationVelocity = 1.5f; // if it still starts a bit inside the ground, it is pushed out gently
        // picked up like the birch logs ("close enough" from the distance: UpdatePlayerReach)
        var pick = go.AddComponent<InteractableObject>();
        pick.ItemName = "pinelog";
        pick.displayName = "Pine Log";
        logs.Add(pick);
    }

    // a pine finished falling: its logs lie where the trunk came down, the stump stays
    public void OnFelled(PineTree tree, Vector3 away)
    {
        float h = tree.data.height;
        int logs = PineTree.Logs(h);
        Vector3 basePos = tree.data.position;
        float thickness = Mathf.Clamp(0.45f + h / 45f, 0.45f, 1.2f);
        for (int i = 0; i < logs; i++)
        {
            float along = h * (0.12f + 0.72f * (i + 0.5f) / logs);
            Vector3 p = basePos + away * along;
            // above the ground under both of its ends too (on a slope it would start inside the hill and fly off)
            float ground = Mathf.Max(GroundHeight(p), Mathf.Max(GroundHeight(p - away * 1.2f), GroundHeight(p + away * 1.2f)));
            p.y = ground + thickness * 0.5f + 0.3f;
            SpawnLog(p, away, thickness);
        }
        AddStump(new PineStumpData { position = basePos, radius = tree.TrunkRadius * 1.1f });
        trees.Remove(tree);
        Destroy(tree.gameObject);
    }

    // ---------------- growing and spreading ---------------- //

    void Update()
    {
        UpdatePlayerReach();

        tick += Time.deltaTime;
        if (tick < 1f) return;
        float real = tick;
        tick = 0f;
        float hours = DayNightSystem.GameHoursPerSecond * real;
        if (hours <= 0f) return;
        Grow(hours);
    }

    // like the birch trees' trigger zone: the axe reaches a pine when the player stands next to its trunk
    void UpdatePlayerReach()
    {
        Vector3 p = PlayerPosition;
        foreach (var t in trees)
        {
            if (t == null || t.Falling) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            float reach = t.TrunkRadius + 3.6f;
            t.Choppable.playerInRange = d.sqrMagnitude < reach * reach;
        }
        // cones and logs on the ground: close enough to pick up
        for (int i = cones.Count - 1; i >= 0; i--)
        {
            var c = cones[i];
            if (c == null || c.pick == null) continue;
            Vector3 d = c.transform.position - p;
            c.pick.playerInRange = d.sqrMagnitude < PickReach * PickReach;
            // a soft outline when the player is near, seen through the grass
            c.ShowOutline(d.sqrMagnitude < OutlineReach * OutlineReach);
        }
        for (int i = logs.Count - 1; i >= 0; i--)
        {
            if (logs[i] == null) { logs.RemoveAt(i); continue; }
            Vector3 d = logs[i].transform.position - p;
            logs[i].playerInRange = d.sqrMagnitude < (PickReach + 1f) * (PickReach + 1f);
        }
    }

    const float PickReach = 3.4f;     // metres from the player's middle to a cone on the ground
    const float OutlineReach = 10f;   // closer than this, a cone shows an outline (it may hide in the grass)

    public void Grow(float hours)
    {
        // Like a real tree: slow while it is small, fastest at half its size, slow again near the top
        // (logistic growth). A sapling (0.7 m) gets to 95% of its full size in about daysToGrowUp days.
        float rate = 6.8f / Mathf.Max(1f, daysToGrowUp * 24f);
        float decay = Mathf.Exp(-rate * hours);
        for (int i = trees.Count - 1; i >= 0; i--)
        {
            var t = trees[i];
            if (t == null) { trees.RemoveAt(i); continue; }
            if (t.Falling) continue;
            var d = t.data;
            if (d.height < d.maxHeight)
            {
                float h = Mathf.Max(0.3f, d.height);
                d.height = Mathf.Min(d.maxHeight, d.maxHeight / (1f + (d.maxHeight / h - 1f) * decay));
                if (t.NeedsApply) t.Apply();
            }
            if (d.height >= coneFromHeight)
            {
                d.coneHours -= hours;
                if (d.coneHours <= 0f)
                {
                    d.coneHours = RandomRange(coneEveryHours.x, coneEveryHours.y);
                    DropCone(t);
                }
            }
        }

        for (int i = cones.Count - 1; i >= 0; i--)
        {
            var c = cones[i];
            if (c == null) { cones.RemoveAt(i); continue; } // picked up
            c.data.age += hours;
            if (c.data.age < c.data.sproutAt) continue;
            // a cone becomes a young pine where there is room; otherwise it rots away
            if (trees.Count < maxPines && IsGoodSpot(c.data.position, 4.5f, -10f))
            {
                AddTree(new PineData
                {
                    position = new Vector3(c.data.position.x, GroundHeight(c.data.position), c.data.position.z),
                    rotation = RandomRange(0f, 360f),
                    height = saplingHeight,
                    maxHeight = RandomRange(minFullHeight, maxFullHeight),
                    variant = random.Next(0, Mathf.Max(1, variants.Count)),
                    coneHours = RandomRange(coneEveryHours.x, coneEveryHours.y),
                });
            }
            cones.RemoveAt(i);
            Destroy(c.gameObject);
        }

        GrowSeeds(hours);
    }

    // A cone falls under a grown pine (age: game hours it has already been lying there).
    void DropCone(PineTree tree, System.Random rng = null, float age = 0f)
    {
        if (cones.Count >= maxCones) return;
        System.Func<float, float, float> range = (a, b) => rng != null ? a + (float)rng.NextDouble() * (b - a) : RandomRange(a, b);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float a = range(0f, Mathf.PI * 2f);
            float d = range(2.5f, Mathf.Max(3f, tree.data.height * 0.18f));
            Vector3 p = tree.data.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            float ground = GroundHeight(p);
            if (Ocean.Instance != null && ground < Ocean.Instance.Level + 0.3f) continue;
            p.y = ground + 0.18f;
            AddCone(new PineConeData { position = p, age = age, sproutAt = range(sproutAfterHours.x, sproutAfterHours.y) });
            return;
        }
    }

    // ---------------- cones planted by the player ---------------- //

    // A pine cone planted by the player (from the inventory: PinePlanting): first only a little mound of brown earth
    // with the cone in it; after plantedSproutHours a tiny pine comes out, which then grows like the others.
    public bool CanPlantAt(Vector3 p) => trees.Count + seeds.Count < maxPines + 60 && IsGoodSpot(p, 2.5f, -2f);

    public bool PlantAt(Vector3 p)
    {
        if (!CanPlantAt(p)) return false;
        AddSeed(new PineConeData
        {
            position = new Vector3(p.x, GroundHeight(p), p.z),
            age = 0f,
            sproutAt = RandomRange(plantedSproutHours.x, plantedSproutHours.y),
        });
        return true;
    }

    PineSeed AddSeed(PineConeData data)
    {
        var go = MakeSeedModel("Planted pine cone");
        go.transform.SetParent(seedRoot, false);
        go.transform.position = data.position;
        go.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(data.position.x * 53f + data.position.z * 29f, 360f), 0f);
        var seed = go.AddComponent<PineSeed>();
        seed.data = data;
        seeds.Add(seed);
        return seed;
    }

    // the planted cone: a low mound of dark earth with the cone half in it (also the preview while planting)
    public GameObject MakeSeedModel(string name)
    {
        var root = new GameObject(name);
        var mound = new GameObject("Earth");
        mound.transform.SetParent(root.transform, false);
        mound.AddComponent<MeshFilter>().sharedMesh = moundMesh;
        mound.AddComponent<MeshRenderer>().sharedMaterial = soilMaterial;
        var cone = new GameObject("Cone");
        cone.transform.SetParent(root.transform, false);
        cone.transform.localPosition = new Vector3(0.05f, 0.13f, 0f);
        cone.transform.localRotation = Quaternion.Euler(0f, 0f, 35f); // standing in the earth, leaning
        cone.transform.localScale = Vector3.one * 0.85f;
        cone.AddComponent<MeshFilter>().sharedMesh = coneMesh;
        cone.AddComponent<MeshRenderer>().sharedMaterial = coneMaterial;
        return root;
    }

    // planted cones that are ready: a tiny pine comes out
    void GrowSeeds(float hours)
    {
        for (int i = seeds.Count - 1; i >= 0; i--)
        {
            var s = seeds[i];
            if (s == null) { seeds.RemoveAt(i); continue; }
            s.data.age += hours;
            if (s.data.age < s.data.sproutAt) continue;
            AddTree(new PineData
            {
                position = s.data.position,
                rotation = RandomRange(0f, 360f),
                height = sproutHeight,
                maxHeight = RandomRange(minFullHeight, maxFullHeight),
                variant = random.Next(0, Mathf.Max(1, variants.Count)),
                coneHours = RandomRange(coneEveryHours.x, coneEveryHours.y),
            });
            seeds.RemoveAt(i);
            Destroy(s.gameObject);
        }
    }

    // a low mound of earth, a bit uneven, its edge sinking into the ground
    static Mesh MakeMoundMesh()
    {
        const int rings = 6, segments = 18;
        const float radius = 0.45f, height = 0.13f;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        vertices.Add(new Vector3(0f, height, 0f));
        for (int r = 1; r <= rings; r++)
        {
            float t = (float)r / rings;
            for (int s = 0; s < segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float bump = 1f + 0.18f * Mathf.Sin(a * 3f + t * 5f) * t + 0.1f * Mathf.Sin(a * 7f + 1.3f) * t;
                float rr = radius * t * (r == rings ? 1.15f : 1f) * bump;
                float y = r == rings ? -0.04f : height * Mathf.Pow(1f - t * t, 0.8f) * (1f + 0.12f * Mathf.Sin(a * 5f + t * 9f));
                vertices.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s, b = 1 + (s + 1) % segments;
            triangles.Add(0); triangles.Add(b); triangles.Add(a);
        }
        for (int r = 1; r < rings; r++)
        {
            int start = 1 + (r - 1) * segments, next = 1 + r * segments;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                triangles.Add(start + s); triangles.Add(start + s1); triangles.Add(next + s);
                triangles.Add(start + s1); triangles.Add(next + s1); triangles.Add(next + s);
            }
        }
        var mesh = new Mesh { name = "Planted cone earth (made in code)" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- save / load ---------------- //

    public PineForestData GetData()
    {
        var data = new PineForestData { version = 1 };
        foreach (var t in trees) if (t != null && !t.Falling) data.trees.Add(t.data);
        foreach (var c in cones) if (c != null) data.cones.Add(c.data);
        foreach (var s in stumps) if (s != null) data.stumps.Add(s.data);
        foreach (var s in seeds) if (s != null) data.seeds.Add(s.data);
        return data;
    }

    // a loaded game: the forest as it was saved (a save from before the pine forest keeps the new forest)
    public void SetData(PineForestData data)
    {
        if (data == null || data.version < 1 || data.trees == null)
        {
            if (!built) BuildNewForest(TakeSceneryFirs());
            return;
        }
        if (terrain == null) terrain = Terrain.activeTerrain;
        TakeSceneryFirs(); // they are in the save already
        built = true;
        Clear();
        foreach (var t in data.trees) AddTree(t);
        if (data.cones != null) foreach (var c in data.cones) AddCone(c);
        if (data.stumps != null) foreach (var s in data.stumps) AddStump(s);
        if (data.seeds != null) foreach (var s in data.seeds) AddSeed(s);
    }

    void Clear()
    {
        foreach (var t in trees) if (t != null) Destroy(t.gameObject);
        foreach (var c in cones) if (c != null) Destroy(c.gameObject);
        foreach (var s in stumps) if (s != null) Destroy(s.gameObject);
        foreach (var s in seeds) if (s != null) Destroy(s.gameObject);
        trees.Clear();
        cones.Clear();
        stumps.Clear();
        seeds.Clear();
    }

    // ---------------- the pine cone model (made in code) ---------------- //

    // An open pine cone: a thin core with about 90 woody scales around it in a spiral (like a real cone), the scales
    // pointing out and a little towards the tip. Dark brown, the scale tips a bit lighter (a gradient texture).
    // Bigger than a real cone (0.5 m), so it is easy to see and pick up.
    static Mesh MakeConeMesh()
    {
        const float length = 0.5f;
        const int scales = 90;
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        // the core (a thin egg shape)
        const int rings = 8, segments = 8;
        int coreStart = vertices.Count;
        for (int r = 0; r <= rings; r++)
        {
            float t = (float)r / rings;
            float radius = 0.055f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, t));
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                var n = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                vertices.Add(n * radius + Vector3.up * (t - 0.5f) * length * 0.92f);
                normals.Add(n);
                uvs.Add(new Vector2(0.5f, 0.05f));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = coreStart + r * (segments + 1) + s, j = i + segments + 1;
                triangles.Add(i); triangles.Add(j); triangles.Add(i + 1);
                triangles.Add(i + 1); triangles.Add(j); triangles.Add(j + 1);
            }

        // the scales
        const float golden = 2.39996323f; // radians
        for (int k = 0; k < scales; k++)
        {
            float t = (k + 0.5f) / scales; // 0 at the stem, 1 at the tip
            float y = (t - 0.5f) * length * 0.86f;
            float a = k * golden;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            // big near the stem end and the middle, small at the tip
            float size = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 0.96f, t)), 0.75f) * (1.05f - 0.45f * t);
            float len = 0.025f + 0.14f * size;
            float width = 0.018f + 0.07f * size;
            float thick = 0.012f + 0.014f * size;
            // pointing out, a little towards the tip (open cone)
            var dir = (outward * Mathf.Cos(0.42f) + Vector3.up * Mathf.Sin(0.42f)).normalized;
            var up = Vector3.Cross(side, dir).normalized; // the scale's top side
            float core = 0.045f * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, t));
            Vector3 root = outward * core + Vector3.up * y;
            AddScale(vertices, normals, uvs, triangles, root, dir, side, up, len, width, thick);
        }

        var mesh = new Mesh { name = "Pine cone (made in code)" };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // one woody scale: a flat shield (pentagon) with a thickness, thicker at its tip, flat-shaded
    static void AddScale(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri,
        Vector3 root, Vector3 dir, Vector3 side, Vector3 up, float len, float width, float thick)
    {
        // outline of the scale (from the root to the tip) and how far along it each point is (for the colour)
        Vector3[] outline =
        {
            root - side * width * 0.3f,
            root - side * width * 0.5f + dir * len * 0.65f,
            root + dir * len,
            root + side * width * 0.5f + dir * len * 0.65f,
            root + side * width * 0.3f,
        };
        float[] along = { 0f, 0.65f, 1f, 0.65f, 0f };
        var top = new Vector3[5];
        var bottom = new Vector3[5];
        for (int i = 0; i < 5; i++)
        {
            float h = Mathf.Lerp(thick * 0.4f, thick, along[i]);
            top[i] = outline[i] + up * h * 0.5f;
            bottom[i] = outline[i] - up * h * 0.5f;
        }
        // top and bottom faces
        AddPolygon(v, n, uv, tri, top, along, up, 1f);
        var bottomReversed = new Vector3[5];
        var alongReversed = new float[5];
        for (int i = 0; i < 5; i++) { bottomReversed[i] = bottom[4 - i]; alongReversed[i] = along[4 - i]; }
        AddPolygon(v, n, uv, tri, bottomReversed, alongReversed, -up, 0.7f);
        // the edges
        for (int i = 0; i < 5; i++)
        {
            int j = (i + 1) % 5;
            Vector3 normal = Vector3.Cross(top[j] - top[i], bottom[i] - top[i]).normalized;
            int start = v.Count;
            v.Add(top[i]); v.Add(top[j]); v.Add(bottom[j]); v.Add(bottom[i]);
            for (int q = 0; q < 4; q++) n.Add(normal);
            uv.Add(new Vector2(0.5f, along[i] * 0.95f)); uv.Add(new Vector2(0.5f, along[j] * 0.95f));
            uv.Add(new Vector2(0.5f, along[j] * 0.95f)); uv.Add(new Vector2(0.5f, along[i] * 0.95f));
            tri.Add(start); tri.Add(start + 1); tri.Add(start + 2);
            tri.Add(start); tri.Add(start + 2); tri.Add(start + 3);
        }
    }

    static void AddPolygon(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector3[] points, float[] along, Vector3 normal, float shade)
    {
        int start = v.Count;
        for (int i = 0; i < points.Length; i++)
        {
            v.Add(points[i]);
            n.Add(normal);
            uv.Add(new Vector2(0.5f, along[i] * 0.9f * shade));
        }
        for (int i = 1; i < points.Length - 1; i++)
        {
            tri.Add(start); tri.Add(start + i); tri.Add(start + i + 1);
        }
    }

    // dark brown at the scale's root, lighter towards its tip, with a darker rim at the very tip
    static Texture2D MakeConeTexture()
    {
        const int h = 64;
        var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        var root = new Color(0.12f, 0.07f, 0.035f);
        var middle = new Color(0.27f, 0.16f, 0.085f);
        var tip = new Color(0.38f, 0.25f, 0.14f);
        var rim = new Color(0.2f, 0.12f, 0.06f);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = t < 0.55f ? Color.Lerp(root, middle, t / 0.55f) : Color.Lerp(middle, tip, (t - 0.55f) / 0.4f);
            if (t > 0.94f) c = Color.Lerp(c, rim, (t - 0.94f) / 0.06f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }
}
