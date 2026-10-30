using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// The forests of the island, made in code when the game scene loads (EnvironmentBootstrap).
//  - the trees are the ones placed in the scene: the forest groups under Enviroment/Trees (Fir_forest, Birch_forest,
//    Oak_forest, Beech_forest, Mixed_forest: every tree its own object with a ScenePine / SceneTree) and the old
//    single trees (TreeFir, the choppable Tree_Parent birches). When the game starts they become living trees at the
//    same place and size: pine, birch, oak, beech (TreeSpecies.cs)
//  - they grow over game time; grown trees near the player drop seeds (pine cones, birch catkins, acorns,
//    beechnuts) under and around their crowns; a seed left on the ground grows into a young tree after 3-4 game days
//    (about half of them, where there is room); a seed can be picked up and planted (SeedPlanting)
//  - far from the player young trees also appear next to grown ones by themselves, so the forests slowly spread
//  - a new tree always keeps its distance from the others (spacing, crowding), never on paths, water or rocks
//  - chopping: small trees need 1-2 hits, big ones up to 8; the tree falls over and leaves logs and a stump
//  - phone friendly: only the trees near the player are real 3D models; farther away every tree is one flat picture
//    that turns to the camera (2 triangles), all drawn at once
// The save keeps every tree, seed and stump (EnviromentData.pineForest); trees added to the scene later only show
// up in a new game.
public class Forest : MonoBehaviour
{
    public static Forest Instance { get; private set; }

    [Header("Seeds")]
    [Tooltip("Only trees this close to the player drop the seeds you can see and pick up (metres)")]
    public float seedDropRadius = 140f;
    [Tooltip("Seeds closer than this to the player are real objects you can see and pick up (farther: only kept in the save)")]
    public float seedShowRadius = 50f;
    [Tooltip("All the seeds lying on the island (each one a few bytes in the save)")]
    public int maxSeedsOnGround = 1500;
    [Tooltip("At most this many seeds lie under one crown: a tree drops no more until they sprout or rot")]
    public int seedsPerCrown = 2;
    [Tooltip("Share of the seeds that grow into a young tree when their time comes (the others rot, like in nature)")]
    public float sproutChance = 0.5f;
    [Tooltip("A new game: share of the grown trees near the player with a seed already under them")]
    public float startSeedChance = 0.35f;
    [Tooltip("Far from the player young trees appear next to grown ones by themselves (tries per game day for the whole island; a try succeeds only where there is room)")]
    public float naturalSaplingsPerDay = 120f;
    [Tooltip("No more trees than this on the island (phone memory and speed)")]
    public int maxTrees = 8500;
    [Tooltip("Far away no new trees once the island is this close to Max Trees: the rest is kept for what grows near the player")]
    public int nearReserve = 700;
    [Tooltip("A new tree needs fewer than Crowd Limit trees within Crowd Radius (metres): forests get thicker, never a thicket")]
    public float crowdRadius = 8f;
    public int crowdLimit = 3;
    public float saplingHeight = 1f;   // a young tree coming out of a seed on the ground
    public float sproutHeight = 0.35f; // a tree coming out of a planted seed
    [Tooltip("A young tree grows at least this much per game day (metres), so the change is seen every day")]
    public float youngGrowthPerDay = 1.2f;

    [Header("Drawing")]
    [Tooltip("Closer than this the real 3D trees are drawn, farther away a flat picture of each tree (phone)")]
    public float modelDistancePhone = 110f;
    [Tooltip("The same on a PC")]
    public float modelDistancePC = 170f;

    public int seed = 20261023;

    public class Variant
    {
        public int species, index;
        public string name;              // "Oak 1"
        public GameObject prefab;
        public Mesh mesh;
        public float nativeHeight, baseOffset;
        public Vector3 prefabScale;
        public float crownRatio;         // crown radius / height
        public float trunkRatio;         // the model's trunk radius (at the height of the player's eyes) / height
        public Material billboard;       // the far away picture (null: not made yet)
        public Vector4 bbRatios;         // the picture's width, height and bottom, per metre of tree height
        public readonly List<ForestTree> trees = new List<ForestTree>();
        public readonly List<Matrix4x4[]> batches = new List<Matrix4x4[]>();
        public readonly List<int> batchCounts = new List<int>();
    }

    static List<Variant>[] variantCache;

    // the models of one species (also used by the editor tools)
    public static List<Variant> VariantsOf(int species)
    {
        if (variantCache == null) variantCache = new List<Variant>[TreeSpecies.Count];
        species = Mathf.Clamp(species, 0, TreeSpecies.Count - 1);
        var list = variantCache[species];
        if (list == null || list.Count == 0 || list[0].prefab == null) variantCache[species] = list = LoadVariants(species);
        return list;
    }

    static List<Variant> LoadVariants(int species)
    {
        var info = TreeSpecies.Get(species);
        var list = new List<Variant>();
        for (int i = 0; i < info.models.Length; i++)
        {
            var prefab = Resources.Load<GameObject>(info.models[i]);
            if (prefab == null) { Debug.LogWarning("Forest: no Resources/" + info.models[i]); continue; }
            var filter = prefab.GetComponentInChildren<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var bounds = filter.sharedMesh.bounds;
            Vector3 scale = prefab.transform.localScale;
            float height = Mathf.Max(0.01f, bounds.size.y * scale.y);
            string name = info.name + " " + (i + 1);
            var v = new Variant
            {
                species = species,
                index = list.Count,
                name = name,
                prefab = prefab,
                mesh = filter.sharedMesh,
                prefabScale = scale,
                nativeHeight = height,
                baseOffset = 0f,
                crownRatio = Mathf.Max(bounds.size.x * scale.x, bounds.size.z * scale.z) * 0.5f / height,
                trunkRatio = MeasureTrunk(prefab, filter, height),
                billboard = Resources.Load<Material>("Forest/Billboards/" + name),
            };
            if (v.billboard != null)
            {
                v.bbRatios = v.billboard.GetVector("_BBRatios");
                v.billboard.enableInstancing = true;
                if (v.bbRatios.y <= 0f) v.billboard = null;
            }
            list.Add(v);
        }
        return list;
    }

    // How thick the model's trunk really is (per metre of the tree's height), from its bark: the trunks of the oaks and
    // beeches are 3-4 times thicker than the old guess (trunkPerMetre), so the player walked into them and saw through
    // the bark. Measured low on the trunk, where the player stands (3-17% of the height), 85% of the bark points
    // inside. 0: no bark found (the guess is used).
    static float MeasureTrunk(GameObject prefab, MeshFilter filter, float height)
    {
        var mesh = filter.sharedMesh;
        var renderer = filter.GetComponent<MeshRenderer>();
        if (mesh == null || renderer == null || !mesh.isReadable || height <= 0f) return 0f;
        Matrix4x4 m = Matrix4x4.Scale(prefab.transform.localScale) * (prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix);
        var vertices = mesh.vertices;
        float bottom = float.MaxValue;
        foreach (var v in vertices) bottom = Mathf.Min(bottom, m.MultiplyPoint3x4(v).y);
        var materials = renderer.sharedMaterials;
        var distances = new List<float>();
        for (int s = 0; s < mesh.subMeshCount && s < materials.Length; s++)
        {
            if (materials[s] == null || IsLeaves(materials[s])) continue;
            foreach (int index in mesh.GetIndices(s))
            {
                Vector3 p = m.MultiplyPoint3x4(vertices[index]);
                float up = (p.y - bottom) / height;
                if (up < 0.03f || up > 0.17f) continue;
                distances.Add(new Vector2(p.x, p.z).magnitude);
            }
        }
        if (distances.Count < 6) return 0f;
        distances.Sort();
        return distances[Mathf.Min(distances.Count - 1, (int)(distances.Count * 0.85f))] / height;
    }

    // the editor made new tree prefabs or pictures: load them again next time
    public static void ClearVariantCache() => variantCache = null;

    // the pine models (old name, used by the Fir_forest tool)
    public static List<Variant> Variants => VariantsOf((int)TreeSpecies.Kind.Pine);

    readonly List<Variant>[] variants = new List<Variant>[TreeSpecies.Count];
    readonly Dictionary<Mesh, Variant> byMesh = new Dictionary<Mesh, Variant>();
    readonly List<ForestTree> trees = new List<ForestTree>();
    readonly SpatialGrid<ForestTree> treeGrid = new SpatialGrid<ForestTree>(48f);
    // every seed lying on the island (the save keeps them); only those near the player have an object (seedObjects)
    readonly List<SeedData> lying = new List<SeedData>();
    readonly SpatialGrid<SeedData> seedGrid = new SpatialGrid<SeedData>(16f);
    readonly Dictionary<SeedData, TreeSeed> seedObjects = new Dictionary<SeedData, TreeSeed>();
    readonly List<TreeSeed> seedsOnGround = new List<TreeSeed>(); // the seed objects near the player
    readonly List<SeedData> seedQuery = new List<SeedData>();
    readonly List<TreeStump> stumps = new List<TreeStump>();
    readonly List<PlantedSeed> planted = new List<PlantedSeed>();
    readonly List<InteractableObject> logs = new List<InteractableObject>();
    readonly Dictionary<Variant, Stack<GameObject>> pool = new Dictionary<Variant, Stack<GameObject>>();
    readonly List<ForestTree> shown = new List<ForestTree>();
    readonly List<ForestTree> inReach = new List<ForestTree>();
    readonly List<ForestTree> query = new List<ForestTree>();
    readonly Dictionary<Material, Material> treeMaterials = new Dictionary<Material, Material>();
    readonly HashSet<Material> ownMaterials = new HashSet<Material>();
    Collider playerCollider;

    Transform treeRoot, seedRoot, stumpRoot, plantedRoot, logRoot, poolRoot;
    Material[] barkMaterials = new Material[TreeSpecies.Count];
    Material woodMaterial;
    Shader leafShader, barkShader;
    Mesh quad;
    Terrain terrain;
    Transform player;
    bool built;
    bool billboardsDirty = true;
    float growthSinceBillboards;
    float tick, modelTick, seasonTick, billboardTick, seedTick;
    float spreadBudget;
    System.Random random = new System.Random();

    static readonly int BillboardNearId = Shader.PropertyToID("_SB_BillboardNear");
    static readonly int WindId = Shader.PropertyToID("_SB_Wind");
    static readonly int AutumnId = Shader.PropertyToID("_SB_Autumn");
    static readonly int LeafLossId = Shader.PropertyToID("_SB_LeafLoss");

    public int TreeCount => trees.Count;
    public IReadOnlyList<ForestTree> Trees => trees; // (the map draws the forests)
    public int SeedCount => lying.Count;              // all the seeds lying on the island
    public int SeedObjectCount => seedsOnGround.Count; // the ones near the player (real objects)
    public int ShownModels => shown.Count;

    // What happened since the scene loaded (for tests and the TEST panel; not saved)
    public int SeedsDropped { get; private set; }     // fallen from trees near the player
    public int SeedsSprouted { get; private set; }    // seeds on the ground that became young trees
    public int SeedsRotted { get; private set; }      // seeds whose time came but that did not grow (chance, no room)
    public int SaplingsFarAway { get; private set; }  // young trees that came up by themselves far from the player

    // young trees (not yet tall enough to drop seeds), seeds on the ground and all trees within radius of centre
    public void CountNear(Vector3 centre, float radius, out int young, out int seeds, out int allTrees)
    {
        young = 0; seeds = 0; allTrees = 0;
        float r2 = radius * radius;
        foreach (var t in trees)
        {
            if (t == null || t.Falling) continue;
            Vector3 d = t.data.position - centre; d.y = 0f;
            if (d.sqrMagnitude > r2) continue;
            allTrees++;
            if (t.data.height < t.Species.seedFromHeight) young++;
        }
        foreach (var s in lying)
        {
            Vector3 d = s.position - centre; d.y = 0f;
            if (d.sqrMagnitude <= r2) seeds++;
        }
    }

    // TEST: lets game time pass for the trees at once (growing, seeds falling and sprouting, spreading), in steps of
    // at most 2 game hours. The clock itself does not move.
    public void FastForward(float hours)
    {
        while (hours > 0.001f)
        {
            float step = Mathf.Min(2f, hours);
            Grow(step);
            hours -= step;
        }
        UpdateSeedObjects(int.MaxValue);
        UpdateModels();
        billboardsDirty = true;
    }
    public Vector3 PlayerPosition => player != null ? player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);
    public float ModelDistance => GameInput.TouchControls ? modelDistancePhone : modelDistancePC;

    void Awake()
    {
        Instance = this;
        treeRoot = NewRoot("Trees");
        seedRoot = NewRoot("Seeds on the ground");
        stumpRoot = NewRoot("Stumps");
        plantedRoot = NewRoot("Planted seeds");
        logRoot = NewRoot("Logs");
        poolRoot = NewRoot("Unused tree models");
        poolRoot.gameObject.SetActive(false);
        SnowCover.Exclude(transform); // the trees get their snow from their own shaders

        leafShader = Resources.Load<Shader>("Environment/SB_TreeLeaves");
        barkShader = Resources.Load<Shader>("Environment/SB_TreeBark");
        if (leafShader != null && !leafShader.isSupported) leafShader = null;
        if (barkShader != null && !barkShader.isSupported) barkShader = null;

        for (int s = 0; s < TreeSpecies.Count; s++)
        {
            variants[s] = VariantsOf(s);
            foreach (var v in variants[s]) if (v.mesh != null && !byMesh.ContainsKey(v.mesh)) byMesh.Add(v.mesh, v);
        }
        MakeMaterials();
        quad = MakeQuad();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Shader.SetGlobalFloat(AutumnId, 0f);
        Shader.SetGlobalFloat(LeafLossId, 0f);
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

        var sceneTrees = TakeSceneTrees(); // the trees placed in the scene become living trees of this forest
        if (!built) BuildNewForest(sceneTrees);
        UpdateSeason();
        UpdateModels();
        UpdateSeedObjects(int.MaxValue);
    }

    // ---------------- materials ---------------- //

    void MakeMaterials()
    {
        // the bark and the cut wood of the game's own chopped birch
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
        woodMaterial = birchWood != null ? new Material(birchWood) : new Material(standard) { color = new Color(0.78f, 0.62f, 0.4f) };
        woodMaterial.name = "Cut wood";
        woodMaterial.hideFlags = HideFlags.DontSave;

        for (int s = 0; s < TreeSpecies.Count; s++)
        {
            var info = TreeSpecies.Get(s);
            Material m;
            if (s == (int)TreeSpecies.Kind.Pine || s == (int)TreeSpecies.Kind.Birch || variants[s].Count == 0)
            {
                // pines have a black trunk: the birch bark made much darker
                m = birchBark != null ? new Material(birchBark) : new Material(standard);
                m.color = birchBark != null ? info.barkTint : new Color(0.12f, 0.09f, 0.08f);
            }
            else
            {
                // the bark texture of the tree's own model
                m = new Material(standard) { color = info.barkTint };
                var r = variants[s][0].prefab.GetComponentInChildren<Renderer>();
                if (r != null)
                    foreach (var source in r.sharedMaterials)
                        if (source != null && !IsLeaves(source) && source.HasProperty("_MainTex")) { m.mainTexture = source.mainTexture; break; }
                if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
            }
            m.name = info.name + " bark";
            m.hideFlags = HideFlags.DontSave;
            m.enableInstancing = true;
            barkMaterials[s] = m;
        }
    }

    public static bool IsLeaves(Material m)
    {
        string shader = m.shader != null ? m.shader.name : "";
        string name = m.name.ToLowerInvariant();
        return shader.Contains("Leaves") || name.Contains("leaves") || name.Contains("leaf") || name.Contains("branch")
            || m.IsKeywordEnabled("_ALPHATEST_ON") || m.renderQueue >= 2450;
    }

    // the tree's materials, as copies with the forest shaders (wind, autumn, snow, many drawn at once)
    public Material[] TreeMaterials(Material[] materials, int species)
    {
        var result = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var m = materials[i];
            if (m == null || ownMaterials.Contains(m)) { result[i] = m; continue; }
            if (!treeMaterials.TryGetValue(m, out var copy))
            {
                copy = MakeTreeMaterial(m, species);
                treeMaterials[m] = copy;
                ownMaterials.Add(copy);
            }
            result[i] = copy;
        }
        return result;
    }

    Material MakeTreeMaterial(Material source, int species) => MakeForestMaterial(source, species, leafShader, barkShader);

    // (also used by the editor tool that takes the pictures of the trees)
    public static Material MakeForestMaterial(Material source, int species, Shader leafShader, Shader barkShader)
    {
        bool leaves = IsLeaves(source);
        var shader = leaves ? leafShader : barkShader;
        if (shader == null) return new Material(source) { name = source.name + " (forest)", enableInstancing = true, hideFlags = HideFlags.DontSave };
        var m = new Material(shader) { name = source.name + " (forest)", enableInstancing = true, hideFlags = HideFlags.DontSave };
        if (source.HasProperty("_MainTex"))
        {
            m.mainTexture = source.mainTexture;
            m.mainTextureScale = source.mainTextureScale;
            m.mainTextureOffset = source.mainTextureOffset;
        }
        m.color = source.HasProperty("_Color") ? source.color : Color.white;
        bool pine = species == (int)TreeSpecies.Kind.Pine;
        m.SetFloat("_AO", pine ? 0f : 0.55f); // the leafy trees keep the shading baked in their vertex colours
        if (leaves)
        {
            m.SetFloat("_Cutoff", source.HasProperty("_Cutoff") ? Mathf.Clamp(source.GetFloat("_Cutoff"), 0.2f, 0.6f) : 0.4f);
            m.SetFloat("_Deciduous", TreeSpecies.Get(species).deciduous ? 1f : 0f);
            m.SetFloat("_Flutter", pine ? 0.4f : 1f);
        }
        return m;
    }

    // a model of a tree is made ready for the forest: no colliders of its own (the trunk box is enough), the
    // forest materials, no scene settings
    public void PrepareModel(GameObject model, int species)
    {
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
        foreach (var s in model.GetComponentsInChildren<ScenePine>(true)) Destroy(s);
        foreach (var s in model.GetComponentsInChildren<SceneTree>(true)) Destroy(s);
        foreach (var l in model.GetComponentsInChildren<LODGroup>(true)) Destroy(l);
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = TreeMaterials(r.sharedMaterials, species);
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }
        model.isStatic = false;
    }

    // ---------------- a new game: the trees placed in the scene ---------------- //

    struct SceneTreeInfo
    {
        public ForestTreeData data;
        public GameObject model; // reused as the tree's model (null: a new one is made)
    }

    // The trees placed in the scene (found by their model) are replaced by living trees at the same place and size.
    // A ScenePine / SceneTree on one of them tells how tall it may grow.
    List<SceneTreeInfo> TakeSceneTrees()
    {
        var result = new List<SceneTreeInfo>();
        var rng = new System.Random(seed); // a new game always gets the same trees
        var filters = new List<MeshFilter>(FindObjectsOfType<MeshFilter>());
        filters.Sort((a, b) => a.transform.position.x != b.transform.position.x
            ? a.transform.position.x.CompareTo(b.transform.position.x) : a.transform.position.z.CompareTo(b.transform.position.z));
        foreach (var filter in filters)
        {
            if (filter == null || filter.sharedMesh == null || !byMesh.TryGetValue(filter.sharedMesh, out var variant)) continue;
            if (filter.transform.IsChildOf(transform)) continue;
            var r = filter.GetComponent<Renderer>();
            if (r == null) continue;
            var info = TreeSpecies.Get(variant.species);
            // (a tree left standing on one of Brother Ioan's paths or in the stream's bed: not in the game)
            if (IslandPaths.KeepClear(filter.transform.position, 0.3f)) { filter.gameObject.SetActive(false); continue; }

            // an old choppable birch (Tree_Parent: Animator -> model -> tree_base with ChoppableTree): all of it goes
            Transform root = filter.transform;
            bool oldBirch = filter.GetComponentInChildren<ChoppableTree>(true) != null
                || (root.parent != null && root.parent.GetComponent<Animator>() != null && root.parent.GetComponentInChildren<ChoppableTree>(true) != null);
            if (oldBirch && root.parent != null && root.parent.GetComponent<Animator>() != null) root = root.parent;

            float height = Mathf.Clamp(r.bounds.size.y, 0.5f, info.maxFull);
            var pineSettings = filter.GetComponent<ScenePine>();
            var treeSettings = filter.GetComponent<SceneTree>();
            float full = treeSettings != null ? treeSettings.growsUpTo
                : pineSettings != null ? pineSettings.growsUpTo
                : Mathf.Lerp(info.minFull, info.maxFull, (float)rng.NextDouble());
            var data = new ForestTreeData
            {
                position = new Vector3(filter.transform.position.x, GroundHeight(filter.transform.position), filter.transform.position.z),
                rotation = filter.transform.eulerAngles.y,
                height = height,
                maxHeight = Mathf.Clamp(Mathf.Max(height, full), info.minFull * 0.5f, info.maxFull),
                variant = variant.index,
                coneHours = Mathf.Lerp(info.seedEveryHours.x, info.seedEveryHours.y, (float)rng.NextDouble()),
                species = variant.species,
            };
            if (oldBirch)
            {
                root.gameObject.SetActive(false); // (hidden right away: Destroy only happens at the end of the frame)
                Destroy(root.gameObject);
                result.Add(new SceneTreeInfo { data = data });
            }
            else
            {
                result.Add(new SceneTreeInfo { data = data, model = filter.gameObject });
            }
        }
        return result;
    }

    void BuildNewForest(List<SceneTreeInfo> sceneTrees)
    {
        built = true;
        Clear();
        foreach (var t in sceneTrees) AddTree(t.data, t.model);
        AddStartSeeds();
        billboardsDirty = true;
    }

    // A new game: seeds already lie under some of the grown trees near the player (so there are some to find). They
    // fell at different times, so from the first game day on some of them come up as young trees.
    void AddStartSeeds()
    {
        var rng = new System.Random(seed + 7);
        Vector3 p = PlayerPosition;
        foreach (var t in trees.ToArray())
        {
            if (t == null || t.data.height < t.Species.seedFromHeight + 2f) continue;
            if (FlatDistance(t.data.position, p) > seedDropRadius * 1.5f) continue;
            if (rng.NextDouble() > startSeedChance) continue;
            DropSeed(t, rng, -1f);
        }
    }

    float RandomRange(float a, float b) => a + (float)random.NextDouble() * (b - a);
    float RandomRange(Vector2 range) => RandomRange(range.x, range.y);

    static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // ---------------- where a tree can grow ---------------- //

    public float GroundHeight(Vector3 p) => GroundY(p);

    public static float GroundY(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
    }

    static readonly Collider[] overlaps = new Collider[48]; // (no new array for every check: phone)

    // On land (shoreMargin: how far from the sea, negative = inland), not too steep and (checkObjects) not on top of
    // something (a rock, a building...). Used in the game and by the forest tools in the editor.
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
        if (River.IsWater(p, 2.5f)) return false; // not in the river or on its very edge
        if (IslandPaths.KeepClear(p)) return false; // not on Brother Ioan's paths or in the stream's bed

        if (checkObjects)
        {
            Vector3 bottom = new Vector3(p.x, ground + 0.6f, p.z);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, bottom + Vector3.up * 2.5f, 1.1f, overlaps, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var c = overlaps[i];
                if (c == null || c is TerrainCollider || c is CharacterController) continue;
                if (c.GetComponentInParent<ForestTree>() != null) continue; // other trees: the spacing decides
                if (c.GetComponent<TreeSeed>() != null || c.GetComponent<ScenePlant>() != null) continue;
                if (c.GetComponent<InteractableObject>() != null) continue; // logs, branches, things to pick up
                if (c.GetComponentInParent<Animal>() != null) continue;
                if (ignore != null && c.transform.IsChildOf(ignore)) continue;
                return false;
            }
        }
        return true;
    }

    // no trunk, stump or planted seed closer than spacing
    bool IsFreeOfTrees(Vector3 p, float spacing)
    {
        float spacing2 = spacing * spacing;
        query.Clear();
        treeGrid.Query(p, spacing, query);
        foreach (var t in query)
        {
            if (t == null) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            float r = spacing + t.TrunkRadius;
            if (d.sqrMagnitude < r * r) return false;
        }
        foreach (var s in stumps)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        foreach (var s in planted)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        return true;
    }

    public bool IsGoodSpot(Vector3 p, float spacing, float shoreMargin)
    {
        return IsFreeOfTrees(p, spacing) && IsGoodGround(p, shoreMargin, true);
    }

    // Room for a young tree that comes up by itself (from a seed on the ground, or far away): no trunk closer than
    // the species' spacing (from its bark), fewer than crowdLimit trees within crowdRadius, no stump or planted seed
    // right there, and good ground (not on paths, water, the beach, steep slopes, rocks or buildings).
    // So the forests get thicker and spread at their edges, but never become a thicket.
    bool HasRoom(Vector3 p, int species)
    {
        float spacing = TreeSpecies.Get(species).spacing;
        float crowd2 = crowdRadius * crowdRadius;
        int around = 0;
        query.Clear();
        treeGrid.Query(p, Mathf.Max(crowdRadius, spacing + 3f), query);
        foreach (var t in query)
        {
            if (t == null || t.Falling) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            float d2 = d.sqrMagnitude;
            float r = spacing + t.TrunkRadius;
            if (d2 < r * r) return false;
            if (d2 < crowd2 && ++around >= crowdLimit) return false;
        }
        foreach (var s in stumps)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        foreach (var s in planted)
        {
            if (s == null) continue;
            Vector3 d = s.data.position - p; d.y = 0f;
            if (d.sqrMagnitude < 2.5f * 2.5f) return false;
        }
        return IsGoodGround(p, -10f, true);
    }

    // ---------------- trees, seeds, stumps ---------------- //

    ForestTree AddTree(ForestTreeData data, GameObject reuse = null)
    {
        data.species = Mathf.Clamp(data.species, 0, TreeSpecies.Count - 1);
        var list = variants[data.species];
        if (list.Count == 0) return null;
        data.variant = Mathf.Clamp(data.variant, 0, list.Count - 1);
        var variant = list[data.variant];
        if (reuse == null && pool.TryGetValue(variant, out var stack) && stack.Count > 0) reuse = stack.Pop();

        var go = new GameObject(TreeSpecies.Get(data.species).name);
        go.transform.SetParent(treeRoot, false);
        var tree = go.AddComponent<ForestTree>();
        tree.Setup(this, data, variant, reuse);
        trees.Add(tree);
        treeGrid.Add(tree, data.position);
        variant.trees.Add(tree);
        billboardsDirty = true;
        return tree;
    }

    ForestTreeData NewSapling(Vector3 p, int species, float height)
    {
        var info = TreeSpecies.Get(species);
        return new ForestTreeData
        {
            position = new Vector3(p.x, GroundHeight(p), p.z),
            rotation = RandomRange(0f, 360f),
            height = height,
            maxHeight = RandomRange(info.minFull, info.maxFull),
            variant = random.Next(0, Mathf.Max(1, variants[species].Count)),
            coneHours = RandomRange(info.seedEveryHours),
            species = species,
        };
    }

    // the model goes into the pool (a loaded game or a new tree of the same kind reuses it)
    void PoolModel(ForestTree tree)
    {
        var model = tree.TakeModel();
        if (model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        model.transform.SetParent(poolRoot, false);
        if (!pool.TryGetValue(tree.Variant, out var stack)) pool[tree.Variant] = stack = new Stack<GameObject>();
        stack.Push(model);
    }

    void RemoveTree(ForestTree tree)
    {
        trees.Remove(tree);
        treeGrid.Remove(tree, tree.data.position);
        tree.Variant.trees.Remove(tree);
        shown.Remove(tree);
        inReach.Remove(tree);
        PoolModel(tree);
        Destroy(tree.gameObject);
        billboardsDirty = true;
    }

    static float SeedLift(int species)
    {
        switch ((TreeSpecies.Kind)species)
        {
            case TreeSpecies.Kind.Pine: return 0.18f;
            case TreeSpecies.Kind.Birch: return 0.07f;
            case TreeSpecies.Kind.Oak: return 0.1f;
            default: return 0.09f;
        }
    }

    // a seed now lies on the ground (its object is made when the player is near: UpdateSeedObjects)
    void AddSeed(SeedData data)
    {
        if (data == null) return;
        lying.Add(data);
        seedGrid.Add(data, data.position);
    }

    // a seed is gone (sprouted, rotted or picked up), with its object if it has one
    void RemoveSeed(SeedData data)
    {
        lying.Remove(data);
        seedGrid.Remove(data, data.position);
        HideSeed(data);
    }

    void HideSeed(SeedData data)
    {
        if (!seedObjects.TryGetValue(data, out var seedObject)) return;
        seedObjects.Remove(data);
        seedsOnGround.Remove(seedObject);
        if (seedObject != null) Destroy(seedObject.gameObject);
    }

    // how many seeds lie within radius of p
    int SeedsNear(Vector3 p, float radius)
    {
        int n = 0;
        float r2 = radius * radius;
        seedQuery.Clear();
        seedGrid.Query(p, radius, seedQuery);
        foreach (var s in seedQuery)
        {
            Vector3 d = s.position - p; d.y = 0f;
            if (d.sqrMagnitude < r2) n++;
        }
        return n;
    }

    const float SeedHideRadius = 12f; // (a seed object goes only this much farther than seedShowRadius: no flicker)
    const int MaxSeedObjects = 220;

    // The seeds near the player become real objects (to see and pick up), the ones left behind lose theirs.
    // budget: how many objects may be made now (a few per call, so walking never makes the phone stutter).
    void UpdateSeedObjects(int budget)
    {
        Vector3 p = PlayerPosition;
        float hide = seedShowRadius + SeedHideRadius;
        for (int i = seedsOnGround.Count - 1; i >= 0; i--)
        {
            var c = seedsOnGround[i];
            if (c == null) { seedsOnGround.RemoveAt(i); continue; }
            Vector3 d = c.data.position - p; d.y = 0f;
            if (d.sqrMagnitude > hide * hide) HideSeed(c.data);
        }
        if (budget <= 0 || seedsOnGround.Count >= MaxSeedObjects) return;
        float show2 = seedShowRadius * seedShowRadius;
        seedQuery.Clear();
        seedGrid.Query(p, seedShowRadius, seedQuery);
        foreach (var s in seedQuery)
        {
            if (seedObjects.ContainsKey(s)) continue;
            Vector3 d = s.position - p; d.y = 0f;
            if (d.sqrMagnitude > show2) continue;
            ShowSeed(s);
            if (--budget <= 0 || seedsOnGround.Count >= MaxSeedObjects) return;
        }
    }

    // the player took a seed: it is in the inventory now, no longer on the ground
    void OnSeedPicked(InteractableObject pick)
    {
        if (pick == null) return;
        var seedObject = pick.GetComponent<TreeSeed>();
        if (seedObject != null && seedObject.data != null) RemoveSeed(seedObject.data);
        Destroy(pick.gameObject);
    }

    TreeSeed ShowSeed(SeedData data)
    {
        var info = TreeSpecies.Get(data.species);
        var go = new GameObject(info.seedName);
        go.transform.SetParent(seedRoot, false);
        go.transform.position = data.position;
        float turn = Mathf.Repeat(data.position.x * 37f + data.position.z * 11f, 360f);
        // lying on its side, a little tilted (the beechnut sits in its husk)
        float lie = data.species == (int)TreeSpecies.Kind.Beech ? 12f : data.species == (int)TreeSpecies.Kind.Pine ? 72f : 82f;
        go.transform.rotation = Quaternion.Euler(0f, turn, 0f) * Quaternion.Euler(lie, 0f, 0f);
        go.transform.localScale = Vector3.one * SeedModels.ScaleOf(data.species);
        go.AddComponent<MeshFilter>().sharedMesh = SeedModels.MeshOf(data.species);
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = SeedModels.MaterialOf(data.species);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        // a solid (not trigger) collider: the crosshair's ray only sees solid ones (Physics: Queries Hit Triggers is off).
        // The player walks through it; "close enough to pick it up" is worked out from the distance (UpdatePlayerReach).
        var hitBox = go.AddComponent<SphereCollider>();
        hitBox.radius = 0.3f;
        if (playerCollider != null) Physics.IgnoreCollision(hitBox, playerCollider);
        var pick = go.AddComponent<InteractableObject>();
        pick.ItemName = info.seedItem;
        pick.displayName = info.seedName;
        pick.onPicked = OnSeedPicked; // (not kept in the save's list of picked up scene items)
        go.tag = "Pickable";
        var seedOnGround = go.AddComponent<TreeSeed>();
        seedOnGround.data = data;
        seedOnGround.pick = pick;
        seedsOnGround.Add(seedOnGround);
        seedObjects[data] = seedOnGround;
        return seedOnGround;
    }

    TreeStump AddStump(StumpData data)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = TreeSpecies.Get(data.species).name + " stump";
        go.transform.SetParent(stumpRoot, false);
        float r = Mathf.Max(0.25f, data.radius);
        go.transform.position = data.position + Vector3.up * 0.15f;
        go.transform.localScale = new Vector3(r * 2f, 0.35f, r * 2f);
        go.GetComponent<MeshRenderer>().sharedMaterial = barkMaterials[Mathf.Clamp(data.species, 0, TreeSpecies.Count - 1)];
        var top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(top.GetComponent<Collider>());
        top.name = "Cut";
        top.transform.SetParent(go.transform, false);
        top.transform.localPosition = new Vector3(0f, 1.0f, 0f);
        top.transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);
        top.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;
        var stump = go.AddComponent<TreeStump>();
        stump.data = data;
        stumps.Add(stump);
        return stump;
    }

    void SpawnLog(Vector3 position, Vector3 along, float thickness, int species)
    {
        var info = TreeSpecies.Get(species);
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = info.logName;
        go.tag = "Pickable";
        go.transform.SetParent(logRoot, false);
        go.transform.position = position;
        go.transform.rotation = Quaternion.FromToRotation(Vector3.up, along) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        go.transform.localScale = new Vector3(thickness, 1.1f, thickness);
        go.GetComponent<MeshRenderer>().sharedMaterial = barkMaterials[species];
        var body = go.AddComponent<Rigidbody>();
        body.mass = 1f;
        body.maxDepenetrationVelocity = 1.5f; // if it still starts a bit inside the ground, it is pushed out gently
        var pick = go.AddComponent<InteractableObject>();
        pick.ItemName = info.logItem;
        pick.displayName = info.logName;
        logs.Add(pick);
    }

    // a tree started to fall: no more far away picture of it
    public void OnFallStarted(ForestTree tree)
    {
        tree.Variant.trees.Remove(tree);
        billboardsDirty = true;
    }

    // a tree finished falling: its logs lie where the trunk came down, the stump stays
    public void OnFelled(ForestTree tree, Vector3 away)
    {
        float h = tree.data.height;
        int count = TreeSpecies.Logs(tree.data.species, h);
        Vector3 basePos = tree.data.position;
        float thickness = Mathf.Clamp(0.45f + h / 45f, 0.45f, 1.2f);
        for (int i = 0; i < count; i++)
        {
            float along = h * (0.12f + 0.72f * (i + 0.5f) / count);
            Vector3 p = basePos + away * along;
            // above the ground under both of its ends too (on a slope it would start inside the hill and fly off)
            float ground = Mathf.Max(GroundHeight(p), Mathf.Max(GroundHeight(p - away * 1.2f), GroundHeight(p + away * 1.2f)));
            p.y = ground + thickness * 0.5f + 0.3f;
            SpawnLog(p, away, thickness, tree.data.species);
        }
        // branches broken off the crown (sticks): few from a pine, more from the leafy trees
        int branches = tree.data.species == (int)TreeSpecies.Kind.Pine
            ? (h > 15f ? 2 : 1)
            : Mathf.Clamp(2 + Mathf.RoundToInt(h / 6f), 2, 6);
        if (h < 3f) branches = 1;
        Vector3 side = Vector3.Cross(Vector3.up, away);
        for (int i = 0; i < branches; i++)
        {
            float along = h * Mathf.Lerp(0.45f, 0.95f, (i + 0.5f) / branches);
            Vector3 p = basePos + away * along + side * RandomRange(-1f, 1f) * Mathf.Max(1.5f, tree.CrownRadius * 0.6f);
            SpawnBranch(p);
        }
        // a grown tree also leaves some of its seeds (cones, acorns...) where its crown came down: to pick up and plant
        // (left there, they grow into young trees like the others)
        var species = tree.Species;
        if (h >= species.seedFromHeight * 0.8f)
        {
            int seeds = 2 + random.Next(3), dropped = 0;
            for (int i = 0; i < seeds; i++)
            {
                Vector3 p = basePos + away * h * RandomRange(0.55f, 0.95f) + side * RandomRange(-1f, 1f) * Mathf.Max(1.5f, tree.CrownRadius * 0.5f);
                float ground = GroundHeight(p);
                if (Ocean.Instance != null && ground < Ocean.Instance.Level + 0.3f) continue;
                p.y = ground + SeedLift(tree.data.species);
                AddSeed(new SeedData { position = p, age = 0f, sproutAt = RandomRange(species.sproutAfterHours), species = tree.data.species });
                dropped++;
                SeedsDropped++;
            }
            UpdateSeedObjects(dropped); // (seen right away)
            if (dropped > 0 && InventorySystem.Instance != null && FlatDistance(PlayerPosition, basePos) < 40f)
                InventorySystem.Instance.ShowMessage(Lang.T(dropped + " × " + species.seedName + " fell from the crown",
                    dropped + " × " + Translations.Name(species.seedName) + " au căzut din coroană"));
        }
        AddStump(new StumpData { position = basePos, radius = tree.TrunkRadius * 1.1f, species = tree.data.species });
        RemoveTree(tree);
    }

    // a branch on the ground that gives a stick (the model of the plants' branches)
    void SpawnBranch(Vector3 p)
    {
        var prefab = Resources.Load<GameObject>("Vegetation/Branch " + (1 + random.Next(3)));
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab);
            foreach (var plant in go.GetComponentsInChildren<ScenePlant>(true)) Destroy(plant);
        }
        else
        {
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = new Vector3(0.08f, 0.7f, 0.08f);
            go.GetComponent<MeshRenderer>().sharedMaterial = barkMaterials[0];
        }
        go.name = "Branch";
        go.tag = "Pickable";
        go.transform.SetParent(logRoot, true);
        p.y = GroundHeight(p) + 0.05f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(prefab != null ? 0f : 90f, RandomRange(0f, 360f), 0f);
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
        logs.Add(pick);
    }

    // ---------------- every frame: reach, the 3D trees near the player, the far away pictures ---------------- //

    void Update()
    {
        UpdatePlayerReach();

        modelTick -= Time.deltaTime;
        if (modelTick <= 0f) { modelTick = 0.2f; UpdateModels(10); }

        seasonTick -= Time.deltaTime;
        if (seasonTick <= 0f) { seasonTick = 0.5f; UpdateSeason(); }

        seedTick -= Time.deltaTime;
        if (seedTick <= 0f) { seedTick = 0.25f; UpdateSeedObjects(8); }

        tick += Time.deltaTime;
        if (tick >= 1f)
        {
            float real = tick;
            tick = 0f;
            float hours = DayNightSystem.GameHoursPerSecond * real;
            if (hours > 0f) Grow(hours);
        }

        billboardTick -= Time.deltaTime;
        if (billboardsDirty || (billboardTick <= 0f && growthSinceBillboards > 0.3f))
        {
            RebuildBillboards();
            billboardTick = 5f;
        }
        DrawBillboards();
    }

    const float PickReach = 3.4f;     // metres from the player's middle to a seed on the ground
    const float OutlineReach = 10f;   // closer than this, a seed shows an outline (it may hide in the grass)

    // like the birch trees' trigger zone: the axe reaches a tree when the player stands next to its trunk
    void UpdatePlayerReach()
    {
        Vector3 p = PlayerPosition;
        foreach (var t in inReach) if (t != null && t.Choppable != null) t.Choppable.playerInRange = false;
        inReach.Clear();
        query.Clear();
        treeGrid.Query(p, 8f, query);
        foreach (var t in query)
        {
            if (t == null || t.Falling) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            float reach = t.TrunkRadius + 3.6f;
            if (d.sqrMagnitude < reach * reach)
            {
                t.Choppable.playerInRange = true;
                inReach.Add(t);
            }
        }
        // seeds and logs on the ground: close enough to pick up
        for (int i = seedsOnGround.Count - 1; i >= 0; i--)
        {
            var c = seedsOnGround[i];
            if (c == null || c.pick == null) continue;
            Vector3 d = c.data.position - p;
            c.pick.playerInRange = d.sqrMagnitude < PickReach * PickReach;
            c.ShowOutline(d.sqrMagnitude < OutlineReach * OutlineReach);
        }
        for (int i = logs.Count - 1; i >= 0; i--)
        {
            if (logs[i] == null) { logs.RemoveAt(i); continue; }
            Vector3 d = logs[i].transform.position - p;
            logs[i].playerInRange = d.sqrMagnitude < (PickReach + 1f) * (PickReach + 1f);
        }
    }

    // the real 3D trees only near the player. newModels: how many young trees that never had a model may get one now
    // (a few per call while walking, so the phone never stutters; all at once after loading)
    void UpdateModels() => UpdateModels(int.MaxValue);

    void UpdateModels(int newModels)
    {
        float distance = ModelDistance;
        // the pictures start a little inside the 3D zone, so a tree is never missing while the player runs
        Shader.SetGlobalFloat(BillboardNearId, distance - 6f);
        Vector3 p = PlayerPosition;
        float limit2 = distance * distance;
        for (int i = shown.Count - 1; i >= 0; i--)
        {
            var t = shown[i];
            if (t == null) { shown.RemoveAt(i); continue; }
            if (t.Falling) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            if (d.sqrMagnitude > limit2)
            {
                t.ShowModel(false);
                shown[i] = shown[shown.Count - 1];
                shown.RemoveAt(shown.Count - 1);
            }
        }
        query.Clear();
        treeGrid.Query(p, distance, query);
        foreach (var t in query)
        {
            if (t == null || t.ModelShown) continue;
            Vector3 d = t.data.position - p; d.y = 0f;
            if (d.sqrMagnitude <= limit2)
            {
                if (!t.HasModel && newModels-- <= 0) continue; // (next time)
                t.ShowModel(true);
                shown.Add(t);
            }
        }
    }

    // A quad: x -0.5..0.5, y 0..1 (the shader turns it to the camera)
    static Mesh MakeQuad()
    {
        var mesh = new Mesh { name = "Tree picture quad (made in code)" };
        mesh.vertices = new[] { new Vector3(-0.5f, 0f, 0f), new Vector3(0.5f, 0f, 0f), new Vector3(0.5f, 1f, 0f), new Vector3(-0.5f, 1f, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        // big bounds: the shader moves the corners, the mesh must never be culled by its own small box
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2000f);
        return mesh;
    }

    void RebuildBillboards()
    {
        billboardsDirty = false;
        growthSinceBillboards = 0f;
        for (int s = 0; s < TreeSpecies.Count; s++)
        {
            foreach (var v in variants[s])
            {
                v.batchCounts.Clear();
                if (v.billboard == null) continue;
                int batch = 0, n = 0;
                foreach (var t in v.trees)
                {
                    if (t == null || t.Falling) continue;
                    if (n == 0)
                    {
                        if (v.batches.Count <= batch) v.batches.Add(new Matrix4x4[1023]);
                    }
                    float h = Mathf.Max(0.2f, t.data.height);
                    float wide = h * t.WidthFactor * t.Species.widthScale;
                    var bb = v.bbRatios;
                    Vector3 pos = t.data.position + Vector3.up * (bb.z * h - t.Sink);
                    v.batches[batch][n] = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(bb.x * wide, bb.y * h, 1f));
                    n++;
                    if (n == 1023) { v.batchCounts.Add(n); batch++; n = 0; }
                }
                if (n > 0) v.batchCounts.Add(n);
            }
        }
    }

    void DrawBillboards()
    {
        if (quad == null) return;
        for (int s = 0; s < TreeSpecies.Count; s++)
        {
            foreach (var v in variants[s])
            {
                if (v.billboard == null) continue;
                for (int b = 0; b < v.batchCounts.Count; b++)
                {
                    Graphics.DrawMeshInstanced(quad, 0, v.billboard, v.batches[b], v.batchCounts[b], null,
                        ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off, null);
                }
            }
        }
    }

    // ---------------- seasons and wind (for the tree shaders) ---------------- //

    void UpdateSeason()
    {
        float autumn = 0f, leafLoss = 0f;
        var time = TimeManager.Instance;
        if (time != null)
        {
            float hour = DayNightSystem.Instance != null ? DayNightSystem.Instance.Hour : 12f;
            float f = Mathf.Clamp01((time.DayInSeason - 1 + hour / 24f) / Mathf.Max(1, time.DaysPerSeason));
            switch (time.currentSeason)
            {
                case TimeManager.Season.Fall:
                    autumn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, f));
                    leafLoss = 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, f));
                    break;
                case TimeManager.Season.Winter:
                    autumn = 1f;
                    leafLoss = 0.6f + 0.25f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.3f, f));
                    break;
                case TimeManager.Season.Spring:
                    autumn = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.4f, f));
                    leafLoss = 0.85f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.6f, f)));
                    break;
            }
        }
        Shader.SetGlobalFloat(AutumnId, autumn);
        Shader.SetGlobalFloat(LeafLossId, leafLoss);
        Shader.SetGlobalFloat(WindId, WeatherSystem.Instance != null ? WeatherSystem.Instance.Wind : 0.2f);
    }

    // ---------------- growing and spreading ---------------- //

    public void Grow(float hours)
    {
        if (hours <= 0f) return;
        Vector3 p = PlayerPosition;
        float seedRadius2 = seedDropRadius * seedDropRadius;
        float youngGrowth = youngGrowthPerDay * hours / 24f;
        for (int i = trees.Count - 1; i >= 0; i--)
        {
            var t = trees[i];
            if (t == null) { trees.RemoveAt(i); continue; }
            if (t.Falling) continue;
            var d = t.data;
            var info = t.Species;
            if (d.height < d.maxHeight)
            {
                // Like a real tree: slow while it is small, fastest at half its size, slow again near the top
                // (logistic growth). A sapling gets to 95% of its full size in about daysToGrowUp days.
                float rate = 6.8f / Mathf.Max(1f, info.daysToGrowUp * 24f);
                float decay = Mathf.Exp(-rate * hours);
                float h = Mathf.Max(0.3f, d.height);
                float grown = Mathf.Min(d.maxHeight, d.maxHeight / (1f + (d.maxHeight / h - 1f) * decay));
                // a young tree grows at least youngGrowthPerDay: on the curve alone a sapling stayed hidden in the
                // grass for days, so nobody saw the forest grow
                if (d.height < d.maxHeight * 0.3f) grown = Mathf.Min(d.maxHeight, Mathf.Max(grown, d.height + youngGrowth));
                growthSinceBillboards += grown - d.height;
                d.height = grown;
                if (t.ModelShown && t.NeedsApply) t.Apply();
            }
            if (d.height >= info.seedFromHeight)
            {
                d.coneHours -= hours;
                if (d.coneHours <= 0f)
                {
                    d.coneHours = RandomRange(info.seedEveryHours);
                    Vector3 off = d.position - p; off.y = 0f;
                    if (off.sqrMagnitude < seedRadius2) DropSeed(t); // only where the player can see it
                }
            }
        }

        // Seeds on the ground become young trees after 3-4 game days: about half of them (sproutChance; the others
        // rot, like in nature), where there is room (HasRoom), on their own spot or a free one close by. Before, at
        // most 160 seeds could lie on the whole island and they filled up in a few minutes where the player stood,
        // so no new seeds fell where the player went next. Now every seed is only data (the save keeps it) and a
        // crown holds at most seedsPerCrown seeds; only the seeds near the player have an object (UpdateSeedObjects).
        // A few sprouts per call at most (each looks for room around it): the rest wait for the next second.
        int sproutBudget = Mathf.Clamp(16 + Mathf.CeilToInt(hours * 8f), 16, 80);
        for (int i = lying.Count - 1; i >= 0; i--)
        {
            var s = lying[i];
            s.age += hours;
            if (s.age < s.sproutAt || sproutBudget <= 0) continue;
            sproutBudget--;
            if (trees.Count < maxTrees && random.NextDouble() < sproutChance && FindSproutSpot(s, out Vector3 spot))
            {
                AddTree(NewSapling(spot, s.species, saplingHeight));
                SeedsSprouted++;
            }
            else
            {
                SeedsRotted++;
            }
            RemoveSeedAt(i);
        }

        GrowPlanted(hours);
        SpreadFarAway(hours);
    }

    void RemoveSeedAt(int index)
    {
        var s = lying[index];
        lying.RemoveAt(index);
        seedGrid.Remove(s, s.position);
        HideSeed(s);
    }

    // the seed's own spot, or a free one within 3.5 m of it
    bool FindSproutSpot(SeedData seed, out Vector3 spot)
    {
        spot = seed.position;
        if (HasRoom(spot, seed.species)) return true;
        for (int k = 0; k < 8; k++)
        {
            float a = (k + RandomRange(0f, 0.6f)) * Mathf.PI * 0.25f;
            float d = RandomRange(1.2f, 3.5f);
            Vector3 p = seed.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (HasRoom(p, seed.species)) { spot = p; return true; }
        }
        return false;
    }

    // A seed falls under or around the crown of a grown tree (age: game hours it has already been lying there;
    // negative: a new game, it fell some time before, so the seeds come up on different days). Not when enough seeds
    // lie under this crown already.
    bool DropSeed(ForestTree tree, System.Random rng = null, float age = 0f)
    {
        System.Func<float, float, float> range = (a, b) => rng != null ? a + (float)rng.NextDouble() * (b - a) : RandomRange(a, b);
        var info = tree.Species;
        float crown = Mathf.Max(3f, tree.CrownRadius);
        float reach = crown * 1.15f + 1.5f;
        if (SeedsNear(tree.data.position, reach) >= seedsPerCrown) return false;
        if (lying.Count >= maxSeedsOnGround && !MakeRoomForSeed()) return false;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            float a = range(0f, Mathf.PI * 2f);
            // under the crown, some blown a bit farther by the wind
            float d = range(Mathf.Max(2.5f, crown * 0.35f), reach);
            Vector3 p = tree.data.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            float ground = GroundHeight(p);
            if (Ocean.Instance != null && ground < Ocean.Instance.Level + 0.3f) continue;
            if (River.IsWater(p, 0.5f)) continue; // (not into the river)
            p.y = ground + SeedLift(tree.data.species);
            float sproutAt = range(info.sproutAfterHours.x, info.sproutAfterHours.y);
            float lain = age >= 0f ? age : range(0f, Mathf.Max(0f, sproutAt - 2f));
            AddSeed(new SeedData { position = p, age = lain, sproutAt = sproutAt, species = tree.data.species });
            SeedsDropped++;
            return true;
        }
        return false;
    }

    // The island holds as many seeds as it may: one lying far from the player rots a bit early, so new seeds keep
    // falling where the player is.
    bool MakeRoomForSeed()
    {
        if (lying.Count == 0) return true;
        Vector3 p = PlayerPosition;
        for (int k = 0; k < 8; k++)
        {
            int i = random.Next(lying.Count);
            if (FlatDistance(lying[i].position, p) < seedDropRadius) continue;
            RemoveSeedAt(i);
            SeedsRotted++;
            return true;
        }
        return false;
    }

    // Far from the player nobody sees the seeds fall: there a young tree simply comes up next to a grown one now and
    // then (naturalSaplingsPerDay tries a game day for the whole island; a try only succeeds where there is room:
    // HasRoom), so the forests far away fill their gaps and spread at their edges too. Before, there were only 30
    // tries a day for ~5600 trees and most were wasted (a young or a near parent, one spot inside the parent's own
    // spacing): almost nothing grew. The last nearReserve trees of maxTrees are kept for what grows near the player.
    void SpreadFarAway(float hours)
    {
        int maxTries = Mathf.Clamp(6 + Mathf.CeilToInt(hours * 6f), 6, 40);
        spreadBudget = Mathf.Min(spreadBudget + naturalSaplingsPerDay * hours / 24f, maxTries);
        if (trees.Count == 0 || trees.Count >= maxTrees - nearReserve) { spreadBudget = 0f; return; }
        Vector3 p = PlayerPosition;
        int tries = 0;
        while (spreadBudget >= 1f && tries++ < maxTries)
        {
            spreadBudget -= 1f;
            // a grown tree far from the player (a few picks: most trees are grown)
            ForestTree parent = null;
            for (int k = 0; k < 8 && parent == null; k++)
            {
                var t = trees[random.Next(trees.Count)];
                if (t == null || t.Falling || t.data.height < t.Species.seedFromHeight) continue;
                if (FlatDistance(t.data.position, p) < seedDropRadius) continue; // near the player its seeds do it
                parent = t;
            }
            if (parent == null) continue;
            float crown = Mathf.Max(3f, parent.CrownRadius);
            for (int k = 0; k < 5; k++)
            {
                float a = RandomRange(0f, Mathf.PI * 2f);
                float d = RandomRange(crown * 0.7f, crown * 1.6f + 3f);
                Vector3 spot = parent.data.position + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
                if (!HasRoom(spot, parent.data.species)) continue;
                AddTree(NewSapling(spot, parent.data.species, saplingHeight));
                SaplingsFarAway++;
                break;
            }
        }
    }

    // ---------------- seeds planted by the player ---------------- //

    // A seed planted by the player (from the inventory: SeedPlanting): first only a little mound of brown earth
    // with the seed in it; after a while a tiny tree comes out, which then grows like the others.
    public bool CanPlantAt(Vector3 p, int species) => trees.Count + planted.Count < maxTrees + 200 && IsGoodSpot(p, 2.5f, -2f);

    public bool PlantAt(Vector3 p, int species)
    {
        if (!CanPlantAt(p, species)) return false;
        var info = TreeSpecies.Get(species);
        AddPlanted(new SeedData
        {
            position = new Vector3(p.x, GroundHeight(p), p.z),
            age = 0f,
            sproutAt = RandomRange(info.plantedSproutHours),
            species = species,
        });
        return true;
    }

    PlantedSeed AddPlanted(SeedData data)
    {
        var go = SeedModels.MakePlantedModel("Planted " + TreeSpecies.Get(data.species).seedName, data.species);
        go.transform.SetParent(plantedRoot, false);
        go.transform.position = data.position;
        go.transform.rotation = Quaternion.Euler(0f, Mathf.Repeat(data.position.x * 53f + data.position.z * 29f, 360f), 0f);
        var seedComponent = go.AddComponent<PlantedSeed>();
        seedComponent.data = data;
        planted.Add(seedComponent);
        return seedComponent;
    }

    // planted seeds that are ready: a tiny tree comes out
    void GrowPlanted(float hours)
    {
        for (int i = planted.Count - 1; i >= 0; i--)
        {
            var s = planted[i];
            if (s == null) { planted.RemoveAt(i); continue; }
            s.data.age += hours;
            if (s.data.age < s.data.sproutAt) continue;
            AddTree(NewSapling(s.data.position, s.data.species, sproutHeight));
            planted.RemoveAt(i);
            Destroy(s.gameObject);
        }
    }

    // ---------------- save / load ---------------- //

    public ForestData GetData()
    {
        var data = new ForestData { version = 2 };
        foreach (var t in trees) if (t != null && !t.Falling) data.trees.Add(t.data);
        foreach (var c in lying) data.cones.Add(c); // (all of them, near the player and far away)
        foreach (var s in stumps) if (s != null) data.stumps.Add(s.data);
        foreach (var s in planted) if (s != null) data.seeds.Add(s.data);
        return data;
    }

    // A loaded game: the forest as it was saved. A save from before the forests keeps the new forests; a save from
    // the pine forest (version 1) has only the pines: the other trees come from the scene.
    public void SetData(ForestData data)
    {
        if (terrain == null) terrain = Terrain.activeTerrain;
        if (!built) BuildNewForest(TakeSceneTrees());
        if (data != null && data.version >= 1 && data.trees != null)
        {
            TakeSceneTrees(); // (they are in the save already)
            if (data.version >= 2) Clear();
            else RemoveSpecies((int)TreeSpecies.Kind.Pine);
            foreach (var t in data.trees) if (!IslandPaths.KeepClear(t.position, 0.3f)) AddTree(t); // (a tree from an old save on a path: gone)
            if (data.cones != null) foreach (var c in data.cones) if (c != null && !IslandPaths.KeepClear(c.position, 0f)) AddSeed(c);
            if (data.stumps != null) foreach (var s in data.stumps) AddStump(s);
            if (data.seeds != null) foreach (var s in data.seeds) AddPlanted(s);
        }
        TakeLoadedBirches();
        RebuildBillboards();
        UpdateModels();
        UpdateSeedObjects(int.MaxValue);
    }

    // An old save brings back the old choppable birches (Tree_Parent) and their stumps: the birches become living
    // ones (unless one stands there already), and no living birch stands where an old one was chopped.
    void TakeLoadedBirches()
    {
        var oldStumps = new List<Vector3>();
        var manager = EnviromentManager.Instance;
        if (manager != null && manager.allTrees != null)
            foreach (Transform child in manager.allTrees.transform)
                if (child.name.StartsWith("ChoppedTree") || child.name.StartsWith("Stump")) oldStumps.Add(child.position);
        foreach (var p in oldStumps)
        {
            query.Clear();
            treeGrid.Query(p, 3f, query);
            foreach (var t in query)
                if (t != null && t.data.species == (int)TreeSpecies.Kind.Birch && FlatDistance(t.data.position, p) < 2.5f) RemoveTree(t);
        }

        var birches = variants[(int)TreeSpecies.Kind.Birch];
        if (birches.Count == 0) return;
        foreach (var c in FindObjectsOfType<ChoppableTree>())
        {
            if (c == null || c.GetComponent<ForestTree>() != null) continue;
            Transform root = c.transform.parent != null && c.transform.parent.parent != null ? c.transform.parent.parent : c.transform;
            Vector3 p = root.position;
            bool there = false;
            query.Clear();
            treeGrid.Query(p, 2f, query);
            foreach (var t in query) if (t != null && FlatDistance(t.data.position, p) < 1.5f) there = true;
            if (!there)
            {
                var r = c.transform.parent != null ? c.transform.parent.GetComponent<Renderer>() : null;
                float height = r != null ? Mathf.Clamp(r.bounds.size.y, 0.5f, TreeSpecies.Get(1).maxFull) : 14f;
                var data = NewSapling(p, (int)TreeSpecies.Kind.Birch, height);
                data.rotation = root.eulerAngles.y;
                data.maxHeight = Mathf.Max(height, data.maxHeight);
                AddTree(data);
            }
            root.gameObject.SetActive(false);
            Destroy(root.gameObject);
        }
    }

    void RemoveSpecies(int species)
    {
        foreach (var t in trees.ToArray()) if (t != null && t.data.species == species) RemoveTree(t);
        for (int i = lying.Count - 1; i >= 0; i--)
            if (lying[i].species == species) RemoveSeedAt(i);
        for (int i = stumps.Count - 1; i >= 0; i--)
            if (stumps[i] != null && stumps[i].data.species == species) { Destroy(stumps[i].gameObject); stumps.RemoveAt(i); }
        for (int i = planted.Count - 1; i >= 0; i--)
            if (planted[i] != null && planted[i].data.species == species) { Destroy(planted[i].gameObject); planted.RemoveAt(i); }
    }

    void Clear()
    {
        foreach (var t in trees) if (t != null) { PoolModel(t); Destroy(t.gameObject); }
        foreach (var c in seedsOnGround) if (c != null) Destroy(c.gameObject);
        foreach (var s in stumps) if (s != null) Destroy(s.gameObject);
        foreach (var s in planted) if (s != null) Destroy(s.gameObject);
        trees.Clear();
        treeGrid.Clear();
        seedsOnGround.Clear();
        seedObjects.Clear();
        lying.Clear();
        seedGrid.Clear();
        stumps.Clear();
        planted.Clear();
        shown.Clear();
        inReach.Clear();
        for (int s = 0; s < TreeSpecies.Count; s++) foreach (var v in variants[s]) v.trees.Clear();
        billboardsDirty = true;
    }
}
