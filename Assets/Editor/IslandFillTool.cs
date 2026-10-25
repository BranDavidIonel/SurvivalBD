using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 3. Fill The Island (Forests, Plants, Rabbits)
// Fills the whole island with objects you can see and change in the scene:
//  - forests: the island is cut into regions; each is a pine, birch, oak, beech or mixed forest (with clearings) or a
//    meadow with a few lone trees. Every forest is a group under Enviroment/Trees (Oak_forest, Birch_forest...),
//    every tree its own object (SceneTree / ScenePine: its height and how tall it grows)
//  - plants under Enviroment/Vegetation, one group per region: ferns, mushrooms, bushes, berry bushes in the
//    forests; grass (hay), chicory, heather, flowers in the meadows; stones and driftwood on the beach; branches
//  - rabbits in pairs (male + female) all over the island
//  - the story's start on the beach (Story Start) with the wreck of the ship, stones, branches and dry grass
// Running it again replaces what it made before (the groups marked with NatureGroup); the old forests stay.
public static class IslandFillTool
{
    const float Cell = 4f;
    public static int lastTrees, lastPlants, lastRabbits;

    enum Kind { Pine, Birch, Oak, Beech, Mixed, Meadow }

    class Region
    {
        public Vector3 centre;
        public Kind kind;
        public Transform trees, plants;
        public int index;
    }

    [MenuItem("Tools/Nature/3. Fill The Island (Forests, Plants, Rabbits)")]
    static void FillFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Fill the island", "Fill the island with forests, plants and rabbits? What this tool made before is replaced (it can take a minute).", "Fill", "Cancel")) return;
        Fill(20261025);
    }

    [MenuItem("Tools/Nature/Remove What The Island Tool Made")]
    static void RemoveFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Remove", "Remove the forests, plants and rabbits made by the island tool?", "Remove", "Cancel")) return;
        RemoveGenerated();
    }

    public static void RemoveGenerated()
    {
        foreach (var g in Object.FindObjectsOfType<NatureGroup>()) if (g != null) Undo.DestroyObjectImmediate(g.gameObject);
        foreach (var r in Object.FindObjectsOfType<RabbitInfo>())
            if (r != null && (r.name.StartsWith("Rabbit M ") || r.name.StartsWith("Rabbit F "))) Undo.DestroyObjectImmediate(r.gameObject);
    }

    public static void Fill(int seed)
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (terrain == null || ocean == null || manager == null) { Debug.LogWarning("Island: terrain / ocean / Enviroment missing"); return; }
        RemoveGenerated();
        var river = Object.FindObjectOfType<River>();
        if (river != null) river.BuildCells();
        var cave = Object.FindObjectOfType<Cave>();
        Vector3 caveMouth = cave != null && cave.path.Count > 0 ? cave.path[0] : new Vector3(1e6f, 0f, 1e6f);
        Forest.ClearVariantCache();

        var rng = new System.Random(seed);
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean.Level;
        int n = Mathf.RoundToInt(data.size.x / Cell);
        var ground = new float[n * n];
        var shore = new float[n * n];
        var steep = new float[n * n];
        var land = new bool[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                Vector3 p = CellCentre(tp, x, z);
                ground[i] = terrain.SampleHeight(p) + tp.y;
                shore[i] = ocean.DistanceFromShore(p);
                steep[i] = data.GetSteepness((x + 0.5f) / n, (z + 0.5f) / n);
                land[i] = ground[i] > level + 0.25f && shore[i] < -2f && x > 4 && z > 4 && x < n - 5 && z < n - 5;
            }
        Bounds lake = LakeBounds();
        var existing = new Hash(16f);
        foreach (var p in RiverTool.ExistingObjects()) existing.Add(p);

        // ---- the story's start on the beach and the river's mouth ----
        Vector3 islandCentre = ocean.transform.position;
        Vector3 start, startForward;
        FindStart(terrain, ocean, river, islandCentre, out start, out startForward);

        // ---- regions ----
        var regions = new List<Region>();
        var treeRoot = manager.allTrees.transform;
        var vegetationRoot = VegetationRoot(manager);
        for (int attempt = 0; attempt < 6000 && regions.Count < 80; attempt++)
        {
            int x = rng.Next(n), z = rng.Next(n);
            int i = z * n + x;
            if (!land[i] || shore[i] > -40f) continue;
            Vector3 p = CellCentre(tp, x, z);
            bool tooClose = false;
            foreach (var r in regions) if (Flat(r.centre - p) < 165f) { tooClose = true; break; }
            if (tooClose) continue;
            Kind kind;
            if (Flat(p - start) < 260f) kind = Kind.Meadow;
            else if (ground[i] > level + 22f) kind = Kind.Pine; // up on the mountain
            else
            {
                double k = rng.NextDouble();
                kind = k < 0.21 ? Kind.Pine : k < 0.39 ? Kind.Birch : k < 0.56 ? Kind.Oak : k < 0.72 ? Kind.Beech : k < 0.84 ? Kind.Mixed : Kind.Meadow;
            }
            regions.Add(new Region { centre = new Vector3(p.x, ground[i], p.z), kind = kind, index = regions.Count });
        }

        Region RegionAt(Vector3 p)
        {
            Region best = null; float bd = float.MaxValue;
            foreach (var r in regions) { float d = Flat(r.centre - p); if (d < bd) { bd = d; best = r; } }
            return best;
        }

        float ForestDensity(Vector3 p, int i)
        {
            float a = Mathf.PerlinNoise(p.x / 150f + 31.7f, p.z / 150f + 11.3f);
            float b = Mathf.PerlinNoise(p.x / 55f + 3.1f, p.z / 55f + 91.9f);
            float noise = a * 0.75f + b * 0.25f;
            float d = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.48f, noise));
            if (shore[i] > -60f) d *= Mathf.InverseLerp(-28f, -60f, shore[i]) * 0.6f; // thinner near the beach
            return d;
        }

        bool InLake(Vector3 p) => lake.size.x > 0f && lake.Contains(new Vector3(p.x, lake.center.y, p.z));
        bool InRiver(Vector3 p, float margin) => river != null && river.DistanceToWater(p) < margin;
        bool NearCave(Vector3 p, float margin) => Flat(p - caveMouth) < margin;

        // ---- trees ----
        var trees = new Hash(16f);
        var treeList = new List<Vector3>();
        var crownList = new List<float>();
        int treeCount = 0;
        const float tc = 6f;
        int tn = Mathf.RoundToInt(data.size.x / tc);
        for (int tz = 0; tz < tn; tz++)
            for (int tx = 0; tx < tn; tx++)
            {
                Vector3 p = tp + new Vector3((tx + (float)rng.NextDouble()) * tc, 0f, (tz + (float)rng.NextDouble()) * tc);
                int ci = Index(tp, n, p);
                if (ci < 0 || !land[ci] || shore[ci] > -28f || steep[ci] > 30f) continue;
                var region = RegionAt(p);
                if (region == null) continue;
                int species;
                float spacing;
                float chance;
                double roll = rng.NextDouble();
                if (region.kind == Kind.Meadow)
                {
                    chance = 0.012f;
                    species = rng.NextDouble() < 0.5 ? (int)TreeSpecies.Kind.Oak : (rng.NextDouble() < 0.5 ? (int)TreeSpecies.Kind.Birch : (int)TreeSpecies.Kind.Beech);
                    spacing = 20f;
                }
                else
                {
                    species = SpeciesOf(region.kind, rng);
                    spacing = Spacing(region.kind);
                    chance = ForestDensity(p, ci) * (tc * tc) / (spacing * spacing) * 1.1f;
                }
                if (roll > chance) continue;
                if (Flat(p - start) < 70f) continue;
                if (InLake(p) || InRiver(p, 4f) || existing.Near(p, 7f) || NearCave(p, 26f)) continue;
                if (trees.Near(p, spacing * 0.62f)) continue;
                p.y = terrain.SampleHeight(p) + tp.y;
                if (p.y < level + 0.8f) continue;
                if (region.trees == null) region.trees = NewGroup(treeRoot, GroupName(region.kind) + "_forest", region, "trees");
                float crown = PlaceTree(region.trees, species, p, rng);
                if (crown > 0f) { trees.Add(p); treeList.Add(p); crownList.Add(crown); treeCount++; }
            }

        // ---- plants ----
        var plantDefs = new Dictionary<string, GameObject>();
        foreach (var def in NatureAssetsTool.Plants)
        {
            var prefab = Resources.Load<GameObject>("Vegetation/" + def.name);
            if (prefab != null) plantDefs[def.name] = prefab;
        }
        int plantCount = 0;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                if (!land[i] || steep[i] > 38f) continue;
                Vector3 c = CellCentre(tp, x, z);
                if (InLake(c)) continue;
                var region = RegionAt(c);
                if (region == null) continue;
                bool beach = shore[i] > -28f;
                bool forest = !beach && region.kind != Kind.Meadow && ForestDensity(c, i) > 0.4f;
                float riverDistance = river != null ? river.DistanceToWater(c) : 999f;
                foreach (var (name, density) in PlantsFor(region.kind, beach, forest, shore[i], riverDistance, rng))
                {
                    if (rng.NextDouble() > density * Cell * Cell) continue;
                    Vector3 p = c + new Vector3(((float)rng.NextDouble() - 0.5f) * Cell, 0f, ((float)rng.NextDouble() - 0.5f) * Cell);
                    if (InRiver(p, 0.8f) || trees.Near(p, 1.6f) || existing.Near(p, 2.5f) || Flat(p - start) < 4f || NearCave(p, 22f)) continue;
                    if (!plantDefs.TryGetValue(name, out var prefab)) continue;
                    if (region.plants == null) region.plants = NewGroup(vegetationRoot, GroupName(region.kind) + (beach ? "_shore" : "") + "_plants", region, "plants");
                    PlacePlant(region.plants, prefab, p, terrain, rng);
                    plantCount++;
                }
            }

        PlaceStart(terrain, ocean, river, manager, vegetationRoot, start, startForward, plantDefs, rng);
        int rabbits = PlaceRabbits(terrain, manager, land, shore, n, tp, start, river, rng);

        // the trees that were there before (Fir_forest, Birch_forest...) get a forest floor too
        foreach (var r in manager.allTrees.GetComponentsInChildren<Renderer>())
        {
            if (r.GetComponentInParent<NatureGroup>() != null) continue;
            treeList.Add(r.transform.position);
            crownList.Add(Mathf.Clamp(Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) * 0.8f, 2.5f, 9f));
        }
        // the dark forest floor under the trees, no grass in their shade, the undergrowth (terrain details)
        TerrainPaintTool.Paint(terrain, treeList, crownList, p => InLake(p) || InRiver(p, 1.5f) || NearCave(p, 10f), seed);

        lastTrees = treeCount; lastPlants = plantCount; lastRabbits = rabbits;
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
        Debug.Log("Island: " + regions.Count + " regions, " + treeCount + " trees, " + plantCount + " plants, " + rabbits + " new rabbits");
    }

    // ---------------- helpers ---------------- //

    static Vector3 CellCentre(Vector3 tp, int x, int z) => tp + new Vector3((x + 0.5f) * Cell, 0f, (z + 0.5f) * Cell);

    static int Index(Vector3 tp, int n, Vector3 p)
    {
        int x = Mathf.FloorToInt((p.x - tp.x) / Cell), z = Mathf.FloorToInt((p.z - tp.z) / Cell);
        if (x < 0 || z < 0 || x >= n || z >= n) return -1;
        return z * n + x;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    class Hash
    {
        readonly float size;
        readonly Dictionary<long, List<Vector3>> cells = new Dictionary<long, List<Vector3>>();
        public Hash(float size) { this.size = size; }
        long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        public void Add(Vector3 p)
        {
            long k = Key(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
            if (!cells.TryGetValue(k, out var l)) cells[k] = l = new List<Vector3>();
            l.Add(p);
        }
        public bool Near(Vector3 p, float r)
        {
            int x0 = Mathf.FloorToInt((p.x - r) / size), x1 = Mathf.FloorToInt((p.x + r) / size);
            int z0 = Mathf.FloorToInt((p.z - r) / size), z1 = Mathf.FloorToInt((p.z + r) / size);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                    if (cells.TryGetValue(Key(x, z), out var l))
                        foreach (var q in l) { float dx = q.x - p.x, dz = q.z - p.z; if (dx * dx + dz * dz < r * r) return true; }
            return false;
        }
    }

    static Bounds LakeBounds()
    {
        bool any = false;
        var b = new Bounds();
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("WaterVolume") && t.name != "WaterBox") continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            foreach (var c in t.GetComponentsInChildren<Collider>()) { if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds); }
        }
        if (!any) return new Bounds(Vector3.zero, Vector3.zero);
        b.Expand(new Vector3(8f, 200f, 8f));
        return b;
    }

    static Transform VegetationRoot(EnviromentManager manager)
    {
        var t = manager.transform.Find("Vegetation");
        if (t == null)
        {
            t = new GameObject("Vegetation").transform;
            t.SetParent(manager.transform, false);
            Undo.RegisterCreatedObjectUndo(t.gameObject, "Vegetation");
        }
        return t;
    }

    static string GroupName(Kind k)
    {
        switch (k)
        {
            case Kind.Pine: return "Fir";
            case Kind.Birch: return "Birch";
            case Kind.Oak: return "Oak";
            case Kind.Beech: return "Beech";
            case Kind.Mixed: return "Mixed";
            default: return "Meadow";
        }
    }

    static Transform NewGroup(Transform parent, string name, Region region, string what)
    {
        var go = new GameObject(UniqueName(parent, name));
        go.transform.SetParent(parent, false);
        go.transform.position = region.centre;
        var marker = go.AddComponent<NatureGroup>();
        marker.kind = GroupName(region.kind) + " " + what;
        Undo.RegisterCreatedObjectUndo(go, "Fill island");
        return go.transform;
    }

    static string UniqueName(Transform parent, string name)
    {
        var used = new HashSet<string>();
        foreach (Transform child in parent) used.Add(child.name);
        if (!used.Contains(name)) return name;
        for (int i = 1; ; i++) { string c = name + " (" + i + ")"; if (!used.Contains(c)) return c; }
    }

    static float Spacing(Kind k)
    {
        switch (k)
        {
            case Kind.Pine: return 10.5f;
            case Kind.Birch: return 9.5f;
            case Kind.Oak: return 15f;
            case Kind.Beech: return 13f;
            default: return 12f;
        }
    }

    static int SpeciesOf(Kind k, System.Random rng)
    {
        switch (k)
        {
            case Kind.Pine: return rng.NextDouble() < 0.9 ? (int)TreeSpecies.Kind.Pine : (int)TreeSpecies.Kind.Birch;
            case Kind.Birch: return rng.NextDouble() < 0.85 ? (int)TreeSpecies.Kind.Birch : (int)TreeSpecies.Kind.Pine;
            case Kind.Oak: return rng.NextDouble() < 0.85 ? (int)TreeSpecies.Kind.Oak : (int)TreeSpecies.Kind.Beech;
            case Kind.Beech: return rng.NextDouble() < 0.85 ? (int)TreeSpecies.Kind.Beech : (int)TreeSpecies.Kind.Oak;
            default: return rng.Next(TreeSpecies.Count);
        }
    }

    static readonly string[] PinePrefabs = { "PineForest/Pine 1", "PineForest/Pine 3", "PineForest/Pine 5" };

    // returns the crown's radius (0: nothing placed)
    static float PlaceTree(Transform group, int species, Vector3 p, System.Random rng)
    {
        var variants = Forest.VariantsOf(species);
        if (variants.Count == 0) return 0f;
        int vi = rng.Next(variants.Count);
        var v = variants[vi];
        var info = TreeSpecies.Get(species);
        float growsUpTo = Mathf.Round(Mathf.Lerp(info.minFull, info.maxFull, (float)rng.NextDouble()));
        bool young = rng.NextDouble() < 0.25;
        float height = growsUpTo * (young ? Mathf.Lerp(0.12f, 0.5f, (float)rng.NextDouble()) : Mathf.Lerp(0.62f, 0.97f, (float)rng.NextDouble()));
        height = Mathf.Round(height * 10f) / 10f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(v.prefab, group);
        float k = height / v.nativeHeight;
        go.transform.localScale = v.prefabScale * k;
        go.transform.position = new Vector3(p.x, p.y - 0.15f, p.z);
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        var pine = go.GetComponent<ScenePine>();
        if (pine != null) { pine.height = Mathf.Clamp(height, 0.7f, 40f); pine.growsUpTo = Mathf.Clamp(growsUpTo, 15f, 40f); }
        var tree = go.GetComponent<SceneTree>();
        if (tree != null) { tree.species = (TreeSpecies.Kind)species; tree.height = Mathf.Clamp(height, 0.5f, 30f); tree.growsUpTo = Mathf.Clamp(growsUpTo, 10f, 30f); }
        return Mathf.Max(2.5f, v.crownRatio * height * info.widthScale);
    }

    static void PlacePlant(Transform group, GameObject prefab, Vector3 p, Terrain terrain, System.Random rng)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        p.y = terrain.SampleHeight(p) + terrain.transform.position.y - 0.03f;
        go.transform.position = p;
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
        // (the prefab's own size: a different size for each would make the scene file much bigger)
    }

    // which plants grow here and how many per square metre
    static IEnumerable<(string, float)> PlantsFor(Kind kind, bool beach, bool forest, float shore, float riverDistance, System.Random rng)
    {
        if (beach)
        {
            yield return ("Stone " + (1 + rng.Next(2)), 1f / 800f);
            yield return ("Branch " + (1 + rng.Next(3)), 1f / 850f);
            if (shore < -16f) yield return ("Dry Grass " + (1 + rng.Next(3)), 1f / 450f);
            yield return ("Driftwood", 1f / 9000f);
            yield break;
        }
        if (riverDistance < 7f)
        {
            // lush river banks
            yield return ("Grass Tuft " + (1 + rng.Next(3)), 1f / 160f);
            yield return ("Chicory", 1f / 700f);
            yield return ("Fern Plant " + (1 + rng.Next(3)), 1f / 500f);
        }
        if (forest)
        {
            // (the dense ferns and bushes are terrain details: TerrainPaintTool; these are a few bigger ones)
            yield return (rng.NextDouble() < 0.5 ? "Fern " + (1 + rng.Next(3)) : "Fern Plant " + (1 + rng.Next(3)), 1f / 1100f);
            yield return ("Bush " + (1 + rng.Next(2)), 1f / 2400f);
            yield return ("Fallen Log", 1f / 4500f);
            yield return ("Old Stump " + (1 + rng.Next(3)), 1f / 4000f);
            yield return ("Berry Bush " + (1 + rng.Next(2)), 1f / 3200f);
            yield return ("Branch " + (1 + rng.Next(3)), 1f / 1100f);
            yield return ("Grass Tuft " + (1 + rng.Next(3)), 1f / 3200f);
            yield return ("Stone " + (1 + rng.Next(2)), 1f / 5500f);
            if (kind == Kind.Pine) yield return ("Heather " + (1 + rng.Next(2)), 1f / 2600f);
            // mushrooms in the shade of the trees
            double m = rng.NextDouble();
            string mushroom;
            switch (kind)
            {
                case Kind.Birch: mushroom = m < 0.5 ? "Red Mushroom" : m < 0.8 ? "Field Mushroom" : "Porcini"; break;
                case Kind.Pine: mushroom = m < 0.3 ? "Red Mushroom" : m < 0.7 ? "Field Mushroom" : "Porcini"; break;
                default: mushroom = m < 0.5 ? "Porcini" : m < 0.85 ? "Field Mushroom" : "Red Mushroom"; break;
            }
            yield return (mushroom, 1f / 750f);
            yield break;
        }
        // meadow (and clearings in the forests)
        string grass = rng.NextDouble() < 0.5 ? "Dry Grass " + (1 + rng.Next(3)) : rng.NextDouble() < 0.8 ? "Grass Tuft " + (1 + rng.Next(3)) : "Tall Grass";
        yield return (grass, 1f / 650f); // (and grass can be picked for hay anywhere on the meadows: GrassPicking)
        yield return ("Chicory", 1f / 2200f);
        yield return ("Heather " + (1 + rng.Next(2)), 1f / 2800f);
        yield return ("Wild Flower " + (1 + rng.Next(3)), 1f / 2400f);
        yield return ("Dry Weed " + (1 + rng.Next(4)), 1f / 2600f);
        yield return ("Bush " + (1 + rng.Next(2)), 1f / 3800f);
        yield return ("Berry Bush " + (1 + rng.Next(2)), 1f / 4200f);
        yield return ("Branch " + (1 + rng.Next(3)), 1f / 3600f);
        yield return ("Stone " + (1 + rng.Next(2)), 1f / 3600f);
        yield return ("Field Mushroom", 1f / 14000f);
    }

    // ---------------- the start on the beach ---------------- //

    // A sandy beach a few hundred metres along the coast from the river's mouth (towards the old part of the island).
    static void FindStart(Terrain terrain, Ocean ocean, River river, Vector3 centre, out Vector3 start, out Vector3 forward)
    {
        float level = ocean.Level;
        Vector3 mouth = river != null && river.points.Count > 0 ? river.points[river.points.Count - 1] : centre + Vector3.left * 700f;
        float mouthAngle = Mathf.Atan2(mouth.z - centre.z, mouth.x - centre.x);
        Vector3 oldPart = new Vector3(140f, 0f, 90f);
        start = centre; forward = Vector3.forward;
        float best = float.MaxValue;
        foreach (float offset in new[] { 0.42f, -0.42f, 0.55f, -0.55f, 0.32f, -0.32f })
        {
            float a = mouthAngle + offset;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 water = centre;
            for (float r = 300f; r < 1000f; r += 2f)
            {
                Vector3 p = centre + dir * r;
                if (terrain.SampleHeight(p) + terrain.transform.position.y < level) { water = p; break; }
            }
            Vector3 s = water - dir * 15f;
            s.y = terrain.SampleHeight(s) + terrain.transform.position.y;
            if (s.y < level + 0.3f) continue;
            if (river != null && river.DistanceToWater(s) < 120f) continue;
            float score = Flat(s - oldPart);
            if (score < best)
            {
                best = score;
                start = s;
                Vector3 inland = -dir;
                Vector3 toMouth = mouth - s; toMouth.y = 0f; toMouth.Normalize();
                forward = (inland * 0.75f + toMouth * 0.25f).normalized;
            }
        }
    }

    static void PlaceStart(Terrain terrain, Ocean ocean, River river, EnviromentManager manager, Transform vegetationRoot,
        Vector3 start, Vector3 forward, Dictionary<string, GameObject> plants, System.Random rng)
    {
        foreach (var old in Object.FindObjectsOfType<StoryStart>()) Undo.DestroyObjectImmediate(old.gameObject);
        var marker = new GameObject("Story Start");
        marker.transform.SetParent(manager.transform, false);
        marker.transform.position = start;
        marker.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        marker.AddComponent<StoryStart>();
        Undo.RegisterCreatedObjectUndo(marker, "Story start");

        var group = new GameObject("Shipwreck");
        group.transform.SetParent(vegetationRoot, false);
        group.transform.position = start;
        group.AddComponent<NatureGroup>().kind = "Shipwreck and the first things to pick";
        Undo.RegisterCreatedObjectUndo(group, "Shipwreck");
        Vector3 seaward = -forward; seaward.y = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, seaward).normalized;

        void Put(string name, Vector3 at, float yaw, float tilt = 0f, float scale = 1f)
        {
            if (!plants.TryGetValue(name, out var prefab)) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
            at.y = terrain.SampleHeight(at) + terrain.transform.position.y - 0.02f;
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(tilt, yaw, 0f);
            go.transform.localScale = prefab.transform.localScale * scale;
        }
        Vector3 Around(float minR, float maxR, float forwardBias)
        {
            for (int i = 0; i < 20; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Lerp(minR, maxR, (float)rng.NextDouble());
                Vector3 p = start + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r + forward * forwardBias;
                float g = terrain.SampleHeight(p) + terrain.transform.position.y;
                if (g > ocean.Level + 0.25f && (river == null || river.DistanceToWater(p) > 2f)) return p;
            }
            return start + forward * (minR + forwardBias);
        }

        // pieces of the ship near the water
        for (int i = 0; i < 5; i++) Put("Wreck Plank", start + seaward * Mathf.Lerp(6f, 10f, (float)rng.NextDouble()) + side * ((i - 2) * 4f + (float)rng.NextDouble() * 2f), (float)rng.NextDouble() * 360f, 0f);
        Put("Driftwood", start + seaward * 7f + side * 11f, 30f, 0f, 1.6f);
        Put("Driftwood", start + seaward * 4f - side * 13f, 110f, 0f, 1.2f);
        var chestPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/3D Low-Poly Chest/Prefabs/Gold Chest.prefab");
        if (chestPrefab != null)
        {
            var chest = (GameObject)PrefabUtility.InstantiatePrefab(chestPrefab, group.transform);
            Vector3 at = start + seaward * 9f - side * 6f;
            at.y = terrain.SampleHeight(at) + terrain.transform.position.y + 0.15f;
            chest.transform.position = at;
            chest.transform.rotation = Quaternion.Euler(8f, 70f, -14f); // half buried in the sand
        }
        // the first things to pick: stones, branches and dry grass
        for (int i = 0; i < 8; i++) Put("Stone " + (1 + rng.Next(2)), Around(4f, 22f, 2f), (float)rng.NextDouble() * 360f);
        for (int i = 0; i < 5; i++) Put("Branch " + (1 + rng.Next(3)), Around(5f, 20f, 3f), (float)rng.NextDouble() * 360f);
        for (int i = 0; i < 6; i++) Put("Dry Grass " + (1 + rng.Next(3)), Around(14f, 26f, 10f), (float)rng.NextDouble() * 360f);
    }

    // ---------------- rabbits ---------------- //

    // ---------------- hemp near the mountain (Level 9: ropes for the raft) ---------------- //

    [MenuItem("Tools/Nature/Add Hemp Near The Mountain")]
    static void AddHempFromMenu() => AddHemp(48, 515);

    // small patches of hemp on the gentle slopes at the foot of the mountain
    public static int AddHemp(int count, int seed)
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        var manager = Object.FindObjectOfType<EnviromentManager>();
        var prefab = Resources.Load<GameObject>("Vegetation/Hemp");
        if (terrain == null || ocean == null || manager == null || prefab == null) return 0;
        foreach (var g in Object.FindObjectsOfType<NatureGroup>()) if (g.kind == "Hemp") Undo.DestroyObjectImmediate(g.gameObject);
        var river = Object.FindObjectOfType<River>();
        var cave = Object.FindObjectOfType<Cave>();
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean.Level;
        // the mountain: ground high above the sea (8 m squares)
        const float cell = 8f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        var mountain = new bool[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
                mountain[z * n + x] = terrain.SampleHeight(tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell)) + tp.y > level + 22f;
        bool NearMountain(Vector3 p)
        {
            int cx = Mathf.FloorToInt((p.x - tp.x) / cell), cz = Mathf.FloorToInt((p.z - tp.z) / cell);
            for (int dz = -8; dz <= 8; dz++)
                for (int dx = -8; dx <= 8; dx++)
                {
                    int x = cx + dx, z = cz + dz;
                    if (x >= 0 && z >= 0 && x < n && z < n && mountain[z * n + x]) return true;
                }
            return false;
        }
        var vegetation = manager.transform.Find("Vegetation");
        var group = new GameObject("Hemp (near the mountain)");
        group.transform.SetParent(vegetation != null ? vegetation : manager.transform, false);
        group.AddComponent<NatureGroup>().kind = "Hemp";
        Undo.RegisterCreatedObjectUndo(group, "Hemp");
        var rng = new System.Random(seed);
        var patches = new List<Vector3>();
        int made = 0;
        for (int attempt = 0; attempt < 40000 && made < count; attempt++)
        {
            Vector3 p = tp + new Vector3((float)rng.NextDouble() * data.size.x, 0f, (float)rng.NextDouble() * data.size.z);
            float h = terrain.SampleHeight(p) + tp.y;
            if (h < level + 1.2f || h > level + 18f) continue;
            if (data.GetSteepness((p.x - tp.x) / data.size.x, (p.z - tp.z) / data.size.z) > 22f) continue;
            if (river != null && (river.DistanceToWater(p) < 4f || Flat(p - river.lakeCentre) < river.lakeRadius + 8f)) continue;
            if (cave != null && cave.path.Count > 0 && Flat(p - cave.path[0]) < 15f) continue;
            if (!NearMountain(p)) continue;
            bool close = false;
            foreach (var q in patches) if (Flat(q - p) < 35f) { close = true; break; }
            if (close) continue;
            patches.Add(p);
            int inPatch = 3 + rng.Next(3);
            for (int j = 0; j < inPatch && made < count; j++)
            {
                Vector3 at = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 5f, 0f, ((float)rng.NextDouble() - 0.5f) * 5f);
                at.y = terrain.SampleHeight(at) + tp.y - 0.03f;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group.transform);
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                made++;
            }
        }
        EditorSceneManager.MarkSceneDirty(group.scene);
        return made;
    }

    [MenuItem("Tools/Nature/Add 50 More Rabbits")]
    static void AddRabbitsFromMenu() => AddRabbits(22, System.Environment.TickCount);

    // More rabbits (pairs and small families) on the land, away from the ones already there. Returns how many.
    public static int AddRabbits(int groups, int seed)
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        var manager = Object.FindObjectOfType<EnviromentManager>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Animals/WhiteRabbitBig.prefab");
        if (terrain == null || ocean == null || manager == null || prefab == null) return 0;
        var river = Object.FindObjectOfType<River>();
        var cave = Object.FindObjectOfType<Cave>();
        var start = Object.FindObjectOfType<StoryStart>();
        Transform parent = manager.allAnimals.transform.Find("Rabbits");
        if (parent == null) parent = manager.allAnimals.transform;
        var anchors = new List<Vector3>();
        int males = 0, females = 0;
        foreach (var a in parent.GetComponentsInChildren<Animal>(true))
        {
            anchors.Add(a.transform.position);
            if (a.name.StartsWith("Rabbit M ")) males++;
            if (a.name.StartsWith("Rabbit F ")) females++;
        }
        Vector3 scale = anchors.Count > 0 ? parent.GetComponentInChildren<Animal>().transform.localScale : Vector3.one * 1.4f;
        var rng = new System.Random(seed);
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int made = 0, placed = 0;
        for (int attempt = 0; attempt < 30000 && placed < groups; attempt++)
        {
            Vector3 p = tp + new Vector3((float)rng.NextDouble() * data.size.x, 0f, (float)rng.NextDouble() * data.size.z);
            p.y = terrain.SampleHeight(p) + tp.y;
            if (p.y < ocean.Level + 0.8f || ocean.DistanceFromShore(p) > -35f) continue;
            if (data.GetSteepness((p.x - tp.x) / data.size.x, (p.z - tp.z) / data.size.z) > 25f) continue;
            if (river != null && (river.DistanceToWater(p) < 5f || Flat(p - river.lakeCentre) < river.lakeRadius + 6f)) continue;
            if (cave != null && cave.path.Count > 0 && Flat(p - cave.path[0]) < 30f) continue;
            if (start != null && Flat(p - start.transform.position) < 50f) continue;
            bool close = false;
            foreach (var q in anchors) if (Flat(q - p) < 55f) { close = true; break; }
            if (close) continue;
            anchors.Add(p);
            placed++;
            int count = rng.NextDouble() < 0.35 ? 3 : 2;
            for (int j = 0; j < count; j++)
            {
                Vector3 at = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 6f, 0f, ((float)rng.NextDouble() - 0.5f) * 6f);
                at.y = terrain.SampleHeight(at) + tp.y + 0.05f;
                var sex = j == 0 ? RabbitInfo.Sex.Male : j == 1 ? RabbitInfo.Sex.Female : (rng.NextDouble() < 0.5 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = sex == RabbitInfo.Sex.Male ? "Rabbit M (" + (++males) + ")" : "Rabbit F (" + (++females) + ")";
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = scale;
                go.AddComponent<RabbitInfo>().sex = sex;
                Undo.RegisterCreatedObjectUndo(go, "Rabbits");
                made++;
            }
        }
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        return made;
    }

    static int PlaceRabbits(Terrain terrain, EnviromentManager manager, bool[] land, float[] shore, int n, Vector3 tp, Vector3 start, River river, System.Random rng)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Animals/WhiteRabbitBig.prefab");
        if (prefab == null || manager.allAnimals == null) return 0;
        Transform parent = manager.allAnimals.transform.Find("Rabbits");
        if (parent == null) parent = manager.allAnimals.transform;
        // the rabbits that were already there: half males, half females
        int k = 0;
        foreach (var a in parent.GetComponentsInChildren<Animal>(true))
        {
            if (a.GetComponent<RabbitInfo>() != null) continue;
            var info = Undo.AddComponent<RabbitInfo>(a.gameObject);
            info.sex = k++ % 2 == 0 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female;
        }
        Vector3 scale = new Vector3(1.4f, 1.4f, 1.4f);
        var first = parent.GetComponentInChildren<Animal>();
        if (first != null) scale = first.transform.localScale;

        var pairs = new List<Vector3>();
        int made = 0, males = 0, females = 0;
        for (int attempt = 0; attempt < 30000 && pairs.Count < 50; attempt++)
        {
            int x = rng.Next(n), z = rng.Next(n);
            int i = z * n + x;
            if (!land[i] || shore[i] > -35f) continue;
            Vector3 p = CellCentre(tp, x, z);
            if (Flat(p - start) < 60f) continue;
            if (river != null && river.DistanceToWater(p) < 4f) continue;
            var cave = Object.FindObjectOfType<Cave>();
            if (cave != null && cave.path.Count > 0 && Flat(p - cave.path[0]) < 30f) continue;
            bool close = false;
            foreach (var q in pairs) if (Flat(q - p) < 70f) { close = true; break; }
            if (close) continue;
            pairs.Add(p);
            int count = rng.NextDouble() < 0.25 ? 3 : 2;
            for (int j = 0; j < count; j++)
            {
                Vector3 at = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 6f, 0f, ((float)rng.NextDouble() - 0.5f) * 6f);
                at.y = terrain.SampleHeight(at) + terrain.transform.position.y + 0.05f;
                var sex = j == 0 ? RabbitInfo.Sex.Male : j == 1 ? RabbitInfo.Sex.Female : (rng.NextDouble() < 0.5 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                go.name = sex == RabbitInfo.Sex.Male ? "Rabbit M (" + (++males) + ")" : "Rabbit F (" + (++females) + ")";
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
                go.transform.localScale = scale;
                go.AddComponent<RabbitInfo>().sex = sex;
                Undo.RegisterCreatedObjectUndo(go, "Rabbits");
                made++;
            }
        }
        return made;
    }
}
