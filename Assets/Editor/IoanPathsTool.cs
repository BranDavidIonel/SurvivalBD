using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 5. Brother Ioan's Paths And The Stream Bed
// In the story Brother Ioan made the island's paths with his own hands, over many years. This draws them where a man
// would walk, from place to place, and clears them:
//  - the beach where the sea threw him (where the game begins) -> the lake -> his cave in the mountain -> up the other
//    mountain (in turns where it is steep) -> down to the northern shore, where the new stream will meet the sea
//  - a path of trodden earth (the ground layer "IoanPath": the old yellow road layer is replaced; the old straight road
//    lines around the mountains are taken away and their grass comes back), no trees, bushes, plants or grass on it
//  - the bed of the miracle stream (level 9): its way down is worked out where water really runs (MiracleStream.Route),
//    a shallow stony channel is dug along it (dry until the miracle), trees and plants out of it
// Saved: the terrain (Assets/Island/IslandTerrain.asset), the scene, Resources/Environment/IoanPaths.json and
// MiracleStreamRoute.json. Running it again does it all again the same way (git keeps the old terrain and scene).
public static class IoanPathsTool
{
    const float PathHalf = 1.7f;      // half of the path's width (the player is 3.7 m tall)
    const float PathEdge = 1.5f;      // soft edge of the trodden earth
    const float Cell = 2f;            // the grid the way is looked for on
    const string Folder = "Assets/Island/Paths";
    // written once the stream's bed is dug: running the tool again keeps that bed (and its saved way) as it is
    const string DugMarker = Folder + "/StreamBedDug.txt";

    [MenuItem("Tools/Nature/5. Brother Ioan's Paths And The Stream Bed")]
    static void FromMenu()
    {
        if (!EditorUtility.DisplayDialog("Brother Ioan's paths",
            "Draws the paths (beach - lake - cave - other mountain - north shore) and digs the bed of the miracle stream.\n\n" +
            "The terrain and the GameScene are changed and saved (trees, plants and grass on the paths are removed).", "Make them", "Cancel")) return;
        Debug.Log(Make());
    }

    class Grid
    {
        public int n;
        public Vector3 origin;
        public float[] height, steep, shore;   // shore: metres from the waterline (negative: inland)
        public byte[] flags;     // 1 blocked, 2 sand, 4 near the stream, 8 on a path already, 16 the river (a ford)
        public byte[] trees;     // how many trees close by
        public float[] noise;
        public int Index(Vector3 p) => Mathf.Clamp(Mathf.FloorToInt((p.z - origin.z) / Cell), 0, n - 1) * n + Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / Cell), 0, n - 1);
        public Vector3 Centre(int i) => origin + new Vector3((i % n + 0.5f) * Cell, 0f, (i / n + 0.5f) * Cell);
    }

    public static string Make()
    {
        var report = new StringBuilder();
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return "Ioan's paths: no terrain (open the GameScene)";
        var data = terrain.terrainData;
        var river = Object.FindObjectOfType<River>();
        var cave = Object.FindObjectOfType<Cave>();
        var ocean = Ocean.Instance != null ? Ocean.Instance : Object.FindObjectOfType<Ocean>();
        var start = Object.FindObjectOfType<StoryStart>();
        if (river == null || cave == null || ocean == null || start == null) return "Ioan's paths: river, cave, ocean or story start missing";
        if (river.isActiveAndEnabled) river.BuildCells();
        Undo.RegisterCompleteObjectUndo(data, "Brother Ioan's paths");
        Directory.CreateDirectory(Folder);
        Directory.CreateDirectory("Assets/Resources/Environment");

        // ---- the ground layers: the trodden earth of the paths (instead of the yellow road) and the stream's stones ----
        int pathLayer = SwapLayer(data, l => l != null && (l.name.Contains("road") || l.name == "IoanPath"), "IoanPath",
            "Assets/TerrainSampleAssets/TerrainLayers/Muddy_TerrainLayer.terrainlayer", 4.5f);
        int bedLayer = SwapLayer(data, l => l != null && (l.name == "TerrainRock" || l.name == "StreamBed"), "StreamBed",
            "Assets/TerrainSampleAssets/TerrainLayers/Soil_Rocks_TerrainLayer.terrainlayer", 3.5f);
        report.AppendLine("layers: path " + pathLayer + ", stream bed " + bedLayer);
        if (pathLayer < 0 || bedLayer < 0) return report + "Ioan's paths: layers not found";

        // ---- the stream: its way, then its bed dug into the ground ----
        Vector3 summit = MiracleStream.FindSummit(terrain, river, cave);
        MiracleStream.ForgetSavedRoute();
        var savedRoute = MiracleStream.SavedRoute();
        bool alreadyDug = File.Exists(DugMarker) && savedRoute != null && savedRoute.Count > 1;
        List<Vector3> stream;
        if (alreadyDug) stream = new List<Vector3>(savedRoute);
        else
        {
            stream = MiracleStream.Route(summit, terrain, ocean, river);
            if (stream == null) return report + "no way for the stream";
            File.WriteAllText("Assets/Resources/Environment/MiracleStreamRoute.json", JsonUtility.ToJson(new MiracleStream.RouteData { points = stream }));
        }
        report.AppendLine("stream: " + stream.Count + " points from " + stream[0] + " to " + stream[stream.Count - 1]);
        var streamWidths = new List<float>();
        {
            float total = 0f;
            for (int i = 1; i < stream.Count; i++) total += Flat(stream[i] - stream[i - 1]);
            float run = 0f;
            for (int i = 0; i < stream.Count; i++)
            {
                if (i > 0) run += Flat(stream[i] - stream[i - 1]);
                streamWidths.Add(MiracleStream.HalfWidthAt(run / Mathf.Max(1f, total)));
            }
        }
        var heightsBefore = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        if (alreadyDug) report.AppendLine("stream bed: dug by an earlier run, kept (" + DugMarker + ")");
        else
        {
            DigStreamBed(terrain, stream, streamWidths, cave, report);
            File.WriteAllText(DugMarker, "The miracle stream's bed is dug into the terrain along Resources/Environment/MiracleStreamRoute.json.\n" +
                "Tools > Nature > 5 keeps it on later runs. To dig it again: put the terrain back as it was before (git), delete this file, run the tool.\n");
        }
        var heightsAfter = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);

        // ---- the places Brother Ioan's paths join ----
        Vector3 beach = start.transform.position + Flatten(start.transform.forward) * 14f;
        Vector3 toBeach = Flatten(beach - river.lakeCentre);
        Vector3 lake = river.lakeCentre + toBeach * (river.lakeRadius + 9f);
        Vector3 caveOut = Flatten(cave.path[0] - cave.path[Mathf.Min(3, cave.path.Count - 1)]);
        Vector3 caveMouth = cave.path[0] + caveOut * 9f;
        // the northern shore: on the beach beside the stream's mouth
        int mouthIndex = stream.Count - 1;
        for (int i = 0; i < stream.Count; i++) if (ocean.DistanceFromShore(stream[i]) > -4f) { mouthIndex = i; break; }
        int upstream = Mathf.Max(0, mouthIndex - 18);
        Vector3 along = Flatten(stream[mouthIndex] - stream[upstream]);
        Vector3 side = Vector3.Cross(Vector3.up, along);
        Vector3 shoreA = stream[upstream] + side * 16f, shoreB = stream[upstream] - side * 16f;
        Vector3 shore = Forest.GroundY(shoreA) > Forest.GroundY(shoreB) ? shoreA : shoreB;

        var grid = BuildGrid(terrain, river, ocean, stream, streamWidths);
        // the flat stone for the cross: near the other mountain's top, where it is least steep
        Vector3 landing = FindLanding(grid, summit);
        report.AppendLine("the stone for the cross: " + landing + ", " + Flat(landing - summit).ToString("0") + " m from the top, "
            + (summit.y - landing.y).ToString("0.0") + " m lower, ground " + grid.steep[grid.Index(landing)].ToString("0") + " deg");
        // beach -> lake -> his cave -> up the other mountain (steps where the rock is too steep), then from the foot of
        // that climb down to the northern shore
        var paths = new List<List<Vector3>>();
        var stairs = new List<List<Vector3>>();
        Vector3 climbFoot = landing;
        var legs = new List<(Vector3 from, Vector3 to, string name, bool climb)>
        {
            (beach, lake, "beach -> lake", false),
            (lake, caveMouth, "lake -> cave", true),
            (caveMouth, landing, "cave -> other mountain", true),
        };
        for (int k = 0; k <= legs.Count; k++)
        {
            (Vector3 from, Vector3 to, string name, bool climb) leg = k < legs.Count ? legs[k] : (climbFoot, shore, "foot of the other mountain -> north shore", false);
            var way = FindWay(grid, leg.from, leg.to, 34f, 0.5f, leg.climb) ?? FindWay(grid, leg.from, leg.to, 41f, 0.6f, leg.climb)
                ?? (leg.climb ? null : FindWay(grid, leg.from, leg.to, 41f, 0.6f, true));
            if (way == null && k == legs.Count - 1 && lastCost != null)
            {
                // the very top is a needle no steps can climb: the stone goes on the highest place the steps reach
                // near it (the cross may stand there: IslandPaths.Data.landing)
                var reached = lastCost;
                int high = -1;
                ForCells(grid, summit, CanonCross.SummitReach - 4f, c => { if (reached[c] < float.MaxValue && (high < 0 || grid.height[c] > grid.height[high])) high = c; });
                if (high >= 0)
                {
                    Vector3 p = grid.Centre(high);
                    landing = new Vector3(p.x, Forest.GroundY(p) + 0.3f, p.z);
                    report.AppendLine("the top is too steep for steps: the stone for the cross moved to " + landing + ", " + Flat(landing - summit).ToString("0") + " m from the top, "
                        + (summit.y - landing.y).ToString("0") + " m lower");
                    way = FindWay(grid, leg.from, landing, 34f, 0.5f, true) ?? FindWay(grid, leg.from, landing, 41f, 0.6f, true);
                }
            }
            if (way == null) { report.AppendLine("no path " + leg.name + " (" + Diagnose(grid, leg.from, leg.to) + ")"); continue; }
            foreach (var p in way) MarkPath(grid, p);
            if (k == legs.Count - 1)
            {
                // the foot of the climb: the lowest point between the cave and the top
                int low = 0;
                for (int i = 1; i < way.Count; i++) if (grid.height[grid.Index(way[i])] < grid.height[grid.Index(way[low])]) low = i;
                climbFoot = way[low];
            }
            int stairsBefore = stairs.Count;
            float walked = 0f, stepped = 0f, worstRise = 0f, steepest = 0f;
            foreach (var (run, onSteps) in SplitWay(grid, way))
            {
                if (onSteps)
                {
                    var steps = MakeSteps(run, out float rise);
                    stairs.Add(steps);
                    stepped += Length(steps);
                    worstRise = Mathf.Max(worstRise, rise);
                }
                else
                {
                    var smooth = run;
                    for (int s = 0; s < 3; s++) smooth = Chaikin(smooth);
                    smooth = Resample(smooth, 1f);
                    for (int i = 0; i < smooth.Count; i++) smooth[i] = new Vector3(smooth[i].x, Forest.GroundY(smooth[i]), smooth[i].z);
                    paths.Add(smooth);
                    walked += Length(smooth);
                    for (int i = 0; i < smooth.Count; i++)
                        steepest = Mathf.Max(steepest, data.GetSteepness((smooth[i].x - terrain.transform.position.x) / data.size.x, (smooth[i].z - terrain.transform.position.z) / data.size.z));
                }
            }
            report.AppendLine("path " + leg.name + ": " + walked.ToString("0") + " m walked (ground up to " + steepest.ToString("0") + " deg), "
                + stepped.ToString("0") + " m of steps in " + (stairs.Count - stairsBefore) + " flights (highest step " + worstRise.ToString("0.00") + " m), straight " + Flat(leg.to - leg.from).ToString("0") + " m");
        }
        var saved = new IslandPaths.PathsData { halfWidth = PathHalf, landing = landing, landingSize = 4.5f };
        foreach (var p in paths) saved.paths.Add(new IslandPaths.PathLine { points = p });
        foreach (var p in stairs) saved.stairs.Add(new IslandPaths.PathLine { points = p });
        File.WriteAllText("Assets/Resources/Environment/IoanPaths.json", JsonUtility.ToJson(saved));
        AssetDatabase.ImportAsset("Assets/Resources/Environment/IoanPaths.json");
        AssetDatabase.ImportAsset("Assets/Resources/Environment/MiracleStreamRoute.json");
        IslandPaths.Forget();

        // ---- the ground: old road lines away, the paths and the stream's stones painted ----
        var oldLines = PaintGround(terrain, pathLayer, bedLayer, paths, stream, streamWidths, report);

        // ---- the grass and the little plants of the terrain (also under the steps) ----
        var all = new List<List<Vector3>>(paths);
        all.AddRange(stairs);
        Details(terrain, all, stream, streamWidths, oldLines, report);

        // ---- trees, bushes and plants of the scene on the paths, the steps and in the bed ----
        ClearScene(terrain, all, stream, streamWidths, heightsBefore, heightsAfter, report);

        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        try { MobileSetupTool.FixShinyTerrainOnAndroid(); } catch (System.Exception e) { report.AppendLine("android textures: " + e.Message); }
        return "Brother Ioan's paths:\n" + report;
    }

    static Vector3 Flatten(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static float Length(List<Vector3> p) { float s = 0f; for (int i = 1; i < p.Count; i++) s += Flat(p[i] - p[i - 1]); return s; }

    // a ground layer replaced by a copy of another (the paint stays where it was)
    static int SwapLayer(TerrainData data, System.Func<TerrainLayer, bool> which, string name, string sourcePath, float tile)
    {
        var layers = data.terrainLayers;
        int index = System.Array.FindIndex(layers, l => which(l));
        if (index < 0) return -1;
        string path = Folder + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            if (!AssetDatabase.CopyAsset(sourcePath, path)) return -1;
            layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        }
        layer.tileSize = new Vector2(tile, tile);
        layer.smoothness = 0f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        layers[index] = layer;
        data.terrainLayers = layers;
        return index;
    }

    // ---------------- the stream's bed ---------------- //

    static void DigStreamBed(Terrain terrain, List<Vector3> stream, List<float> widths, Cave cave, StringBuilder report)
    {
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 tp = terrain.transform.position;
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1), sy = data.size.y;
        var h = data.GetHeights(0, 0, res, res);
        // the bottom of the bed along the way: under the ground, deeper on the flat land, never higher than upstream
        int count = stream.Count;
        var bed = new float[count];
        var depth = new float[count];
        float run = 0f;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) run += Flat(stream[i] - stream[i - 1]);
            int a0 = Mathf.Max(0, i - 3), a1 = Mathf.Min(count - 1, i + 3);
            float steep = Mathf.Max(0f, (stream[a0].y - stream[a1].y) / Mathf.Max(0.1f, Flat(stream[a1] - stream[a0])));
            depth[i] = run < 8f || steep > 0.7f ? 0f : Mathf.Lerp(0.85f, 0.35f, Mathf.InverseLerp(0.05f, 0.45f, steep));
            bed[i] = stream[i].y - depth[i];
            if (i > 0) bed[i] = Mathf.Min(bed[i], bed[i - 1] + (depth[i] <= 0f ? 100f : 0f));
        }
        var caveFar = new List<Vector3>(cave.path);
        int changed = 0;
        for (int i = 0; i < count; i++)
        {
            if (depth[i] <= 0f) continue;
            float reach = widths[i] + 3.2f;
            bool nearCave = false;
            foreach (var c in caveFar) if (Flat(c - stream[i]) < 150f) { nearCave = true; break; }
            if (nearCave) continue;
            int x0 = Mathf.FloorToInt((stream[i].x - reach - tp.x) / sx), x1 = Mathf.CeilToInt((stream[i].x + reach - tp.x) / sx);
            int z0 = Mathf.FloorToInt((stream[i].z - reach - tp.z) / sz), z1 = Mathf.CeilToInt((stream[i].z + reach - tp.z) / sz);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(res - 1, z1); z++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(res - 1, x1); x++)
                {
                    float wx = tp.x + x * sx, wz = tp.z + z * sz;
                    float d = new Vector2(wx - stream[i].x, wz - stream[i].z).magnitude;
                    if (d > reach) continue;
                    // a flat bottom, then the banks rise to the ground around
                    float target = bed[i] + (depth[i] + 0.4f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(widths[i] * 0.5f, reach, d));
                    float now = h[z, x] * sy + tp.y;
                    float want = Mathf.Max(Mathf.Min(now, target), now - 2.5f);
                    if (want < now - 0.001f) { h[z, x] = (want - tp.y) / sy; changed++; }
                }
        }
        data.SetHeights(0, 0, h);
        report.AppendLine("stream bed dug: " + changed + " ground points lowered");
    }

    // ---------------- where a man would walk ---------------- //

    static Grid BuildGrid(Terrain terrain, River river, Ocean ocean, List<Vector3> stream, List<float> widths)
    {
        var data = terrain.terrainData;
        var g = new Grid { origin = terrain.transform.position };
        g.n = Mathf.RoundToInt(data.size.x / Cell);
        int n = g.n;
        g.height = new float[n * n];
        g.steep = new float[n * n];
        g.flags = new byte[n * n];
        g.trees = new byte[n * n];
        g.noise = new float[n * n];
        g.shore = new float[n * n];
        // (by the shore, not by the height: the island's flat land is only a little over the sea)
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                Vector3 p = g.Centre(i);
                float u = (x + 0.5f) / n, v = (z + 0.5f) / n;
                float h = terrain.SampleHeight(p) + g.origin.y;
                g.height[i] = h;
                g.steep[i] = data.GetSteepness(u, v);
                g.noise[i] = Mathf.PerlinNoise(p.x * 0.017f + 5.1f, p.z * 0.017f + 1.7f);
                float shore = ocean.DistanceFromShore(p);
                g.shore[i] = shore;
                if (shore > -6f) g.flags[i] |= 1;          // the sea and the wet sand
                else if (shore > -30f) g.flags[i] |= 2;    // the beach
                if (river.lakeRadius > 0f && Flat(p - river.lakeCentre) < river.lakeRadius + 4f) g.flags[i] |= 1;
                else if (river.DistanceToWater(p) < 3.5f) g.flags[i] |= 16;
            }
        // near the stream's bed: rather not along it (crossing is all right)
        for (int i = 0; i < stream.Count; i++)
        {
            float r = widths[i] + 3.5f;
            ForCells(g, stream[i], r, c => g.flags[c] |= 4);
        }
        // the trees: the path goes rather through clearings (the trees left on it are taken away)
        foreach (var t in SceneTrees())
            ForCells(g, t.position, 3f, c => { if (g.trees[c] < 3) g.trees[c]++; });
        // rocks, buildings, the wreck...: around them
        foreach (var c in Object.FindObjectsOfType<Collider>())
        {
            if (c is TerrainCollider || c.isTrigger || !c.enabled) continue;
            var b = c.bounds;
            if (b.size.x > 40f || b.size.z > 40f || b.size.y < 0.6f) continue;
            if (IsNature(c.transform) || c.GetComponentInParent<PlayerMovement>() != null) continue;
            if (c.GetComponentInParent<Cave>() != null) continue;
            int x0 = Mathf.FloorToInt((b.min.x - 1f - g.origin.x) / Cell), x1 = Mathf.FloorToInt((b.max.x + 1f - g.origin.x) / Cell);
            int z0 = Mathf.FloorToInt((b.min.z - 1f - g.origin.z) / Cell), z1 = Mathf.FloorToInt((b.max.z + 1f - g.origin.z) / Cell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(n - 1, z1); z++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(n - 1, x1); x++) g.flags[z * n + x] |= 1;
        }
        return g;
    }

    // trees, plants and things to pick up are not obstacles
    static bool IsNature(Transform t)
    {
        if (t.GetComponentInParent<ScenePlant>() != null || t.GetComponentInParent<InteractableObject>() != null) return true;
        if (t.GetComponentInParent<Animal>() != null || t.GetComponentInParent<ScenePine>() != null || t.GetComponentInParent<SceneTree>() != null) return true;
        var f = t.GetComponent<MeshFilter>();
        return f != null && f.sharedMesh != null && TreeMeshes().Contains(f.sharedMesh);
    }

    static HashSet<Mesh> treeMeshes;
    static HashSet<Mesh> TreeMeshes()
    {
        if (treeMeshes != null) return treeMeshes;
        treeMeshes = new HashSet<Mesh>();
        Forest.ClearVariantCache();
        for (int s = 0; s < TreeSpecies.Count; s++) foreach (var v in Forest.VariantsOf(s)) if (v.mesh != null) treeMeshes.Add(v.mesh);
        return treeMeshes;
    }

    // the trees placed in the scene (the forests): each one's object, where and how tall
    static List<Transform> SceneTrees()
    {
        var list = new List<Transform>();
        var meshes = TreeMeshes();
        foreach (var f in Object.FindObjectsOfType<MeshFilter>())
            if (f.sharedMesh != null && meshes.Contains(f.sharedMesh)) list.Add(f.transform);
        return list;
    }

    static void ForCells(Grid g, Vector3 p, float r, System.Action<int> act)
    {
        int x0 = Mathf.FloorToInt((p.x - r - g.origin.x) / Cell), x1 = Mathf.FloorToInt((p.x + r - g.origin.x) / Cell);
        int z0 = Mathf.FloorToInt((p.z - r - g.origin.z) / Cell), z1 = Mathf.FloorToInt((p.z + r - g.origin.z) / Cell);
        for (int z = Mathf.Max(0, z0); z <= Mathf.Min(g.n - 1, z1); z++)
            for (int x = Mathf.Max(0, x0); x <= Mathf.Min(g.n - 1, x1); x++)
            {
                Vector3 c = g.Centre(z * g.n + x);
                if (Flat(c - p) <= r) act(z * g.n + x);
            }
    }

    static void MarkPath(Grid g, Vector3 p) => ForCells(g, p, 3f, c => g.flags[c] |= 8);

    // 32 directions (the first 8: the neighbours): on steep rock a gentle rise is only found close along the slope
    static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1, 2, 2, -2, -2, 1, 1, -1, -1, 3, 3, -3, -3, 1, 1, -1, -1, 3, 3, -3, -3, 2, 2, -2, -2 };
    static readonly int[] DZ = { 0, 0, 1, -1, 1, -1, 1, -1, 1, -1, 1, -1, 2, -2, 2, -2, 1, -1, 1, -1, 3, -3, 3, -3, 2, -2, 2, -2, 3, -3, 3, -3 };
    const float StairFrom = 42f;   // ground steeper than this (degrees): steps (the player walks up to 45)
    const float StairGrade = 1.15f; // the steps rise at most this much per metre (0.58 m a step, one every half metre)

    // The cheapest way for a walker: short, not steep (a steep slope is climbed in turns: a gentle rise along it costs
    // much less than straight up), round rocks and water, through clearings rather than the thick of the forest,
    // joining the paths already made. maxSteep: the ground's slope in degrees; maxGrade: how steep the path itself.
    static float[] lastCost; // the last search's cost to every cell (float.MaxValue: not reached)

    // stairs: where the ground is steeper than maxSteep the way may still go, on steps (rising at most StairGrade)
    static List<Vector3> FindWay(Grid g, Vector3 from, Vector3 to, float maxSteep, float maxGrade, bool stairs = false)
    {
        int n = g.n;
        int s = g.Index(from), goal = g.Index(to);
        // the ends may be in a hard place (the beach, a rocky top): a little room there where anything goes
        var freed = new List<int>();
        var easy = new HashSet<int>();
        foreach (int e in new[] { s, goal })
            ForCells(g, g.Centre(e), 7f, c =>
            {
                easy.Add(c);
                if ((g.flags[c] & 1) != 0 && g.shore[c] < -1f) { g.flags[c] &= 0xFE; freed.Add(c); }
            });
        var cost = new float[n * n];
        var back = new int[n * n];
        for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; back[i] = -1; }
        var heap = new MiracleStream.Heap();
        Vector3 goalPos = g.Centre(goal);
        cost[s] = 0f;
        heap.Push(Flat(g.Centre(s) - goalPos) * 0.4f, s);
        bool found = false;
        int popped = 0;
        while (heap.Count > 0)
        {
            heap.Pop(out float f, out int i);
            if (i == goal) { found = true; break; }
            Vector3 here = g.Centre(i);
            if (f > cost[i] + Flat(here - goalPos) * 0.4f + 0.01f) continue;
            if (++popped > 4000000) break;
            int x = i % n, z = i / n;
            for (int k = 0; k < DX.Length; k++)
            {
                int nx = x + DX[k], nz = z + DZ[k];
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                int j = nz * n + nx;
                bool free = easy.Contains(j);
                bool rock = g.steep[j] > maxSteep; // too steep to walk: steps (or not at all)
                if ((g.flags[j] & 1) != 0 || (rock && !stairs && !free)) continue;
                float len = Cell * Mathf.Sqrt(DX[k] * DX[k] + DZ[k] * DZ[k]);
                // the rise of the worst part of the move (a long move is looked at cell by cell, and must not jump
                // over a rock)
                int parts = Mathf.Max(Mathf.Abs(DX[k]), Mathf.Abs(DZ[k]));
                float grade = 0f, last = g.height[i];
                bool through = true;
                for (int q = 1; q <= parts; q++)
                {
                    int m = q == parts ? j : g.Index(Vector3.Lerp(here, g.Centre(j), (float)q / parts));
                    if ((g.flags[m] & 1) != 0) { through = false; break; }
                    grade = Mathf.Max(grade, Mathf.Abs(g.height[m] - last) / (len / parts));
                    last = g.height[m];
                }
                if (!through) continue;
                float limit = rock ? (stairs ? StairGrade : Mathf.Max(maxGrade, 1.2f)) : (free && !stairs ? Mathf.Max(maxGrade, 1.2f) : maxGrade);
                if (grade > limit) continue;
                float step;
                if (rock && stairs)
                    step = len * (1f + g.noise[j] * 0.3f) * (4f + 8f * grade) + len * (g.steep[j] - maxSteep) * 0.06f; // steps: hard work
                else
                    step = len * (1f + g.noise[j] * 0.6f) * (1f + 5f * (grade / 0.17f) * (grade / 0.17f))
                        + len * (g.steep[j] > 22f ? (g.steep[j] - 22f) * 0.12f : 0f);
                step += len * g.trees[j] * 0.5f;
                if ((g.flags[j] & 2) != 0) step *= 1.5f;   // soft sand
                if ((g.flags[j] & 4) != 0) step *= 6f;     // along the stream's bed
                if ((g.flags[j] & 16) != 0) step *= 14f;   // the river: crossed once, straight over (a ford)
                if ((g.flags[j] & 8) != 0) step *= 0.4f;   // a path already there
                float c = cost[i] + step;
                if (c < cost[j]) { cost[j] = c; back[j] = i; heap.Push(c + Flat(g.Centre(j) - goalPos) * 0.4f, j); }
            }
        }
        lastCost = cost;
        foreach (int c in freed) g.flags[c] |= 1;
        if (!found) return null;
        var way = new List<Vector3>();
        for (int i = goal; i >= 0; i = back[i]) way.Add(g.Centre(i));
        way.Reverse();
        way[0] = new Vector3(from.x, 0f, from.z);
        way[way.Count - 1] = new Vector3(to.x, 0f, to.z);
        return way;
    }

    // the way in pieces: walked (ground up to StairFrom) and on steps (steeper; each flight begins and ends one point
    // out on walkable ground, and shares its first and last point with the walked pieces around it)
    static List<(List<Vector3> run, bool steps)> SplitWay(Grid g, List<Vector3> way)
    {
        int n = way.Count;
        var rock = new bool[n];
        for (int i = 0; i < n; i++) rock[i] = g.steep[g.Index(way[i])] > StairFrom;
        var onSteps = (bool[])rock.Clone();
        for (int i = 0; i < n; i++)
            if (rock[i]) { if (i > 0) onSteps[i - 1] = true; if (i + 1 < n) onSteps[i + 1] = true; }
        var pieces = new List<(List<Vector3>, bool)>();
        int a = 0;
        while (a < n)
        {
            int b = a;
            while (b + 1 < n && onSteps[b + 1] == onSteps[a]) b++;
            var run = way.GetRange(a, b - a + 1);
            if (a > 0) run.Insert(0, way[a - 1]);
            if (run.Count >= 2) pieces.Add((run, onSteps[a]));
            a = b + 1;
        }
        return pieces;
    }

    // A flight of steps along these points: one every half metre on the straight lines between them (no smoothing: on
    // steep rock half a metre aside is another height), each at the rock's height, then raised where needed so no
    // step is more than StepRise over the one before or after (a step can stand over the rock, never in it).
    const float StepRise = 0.58f;  // (the player steps up to 0.7 m)
    const float StepSpacing = 0.5f;
    static List<Vector3> MakeSteps(List<Vector3> run, out float worst)
    {
        var flat = new List<Vector3>();
        foreach (var p in run) flat.Add(new Vector3(p.x, 0f, p.z));
        var steps = Resample(flat, StepSpacing);
        var y = new float[steps.Count];
        for (int i = 0; i < steps.Count; i++) y[i] = Forest.GroundY(steps[i]);
        for (int i = steps.Count - 2; i >= 0; i--) y[i] = Mathf.Max(y[i], y[i + 1] - StepRise);
        for (int i = 1; i < steps.Count; i++) y[i] = Mathf.Max(y[i], y[i - 1] - StepRise);
        worst = 0f;
        for (int i = 0; i < steps.Count; i++)
        {
            steps[i] = new Vector3(steps[i].x, y[i], steps[i].z);
            if (i > 0) worst = Mathf.Max(worst, Mathf.Abs(y[i] - y[i - 1]));
        }
        return steps;
    }

    // near the other mountain's top (16 m round, at most 11 m lower): the least steep spot, the nearer the better
    static Vector3 FindLanding(Grid g, Vector3 summit)
    {
        int best = -1;
        float bestScore = float.MaxValue;
        ForCells(g, summit, 16f, c =>
        {
            if (g.height[c] < summit.y - 11f) return;
            float score = g.steep[c] + Flat(g.Centre(c) - summit) * 0.5f;
            if (score < bestScore) { bestScore = score; best = c; }
        });
        Vector3 p = best >= 0 ? g.Centre(best) : summit;
        return new Vector3(p.x, Forest.GroundY(p) + 0.3f, p.z);
    }

    // why no way was found: how much ground the walk reaches from the start, and how close it comes to the goal
    static string Diagnose(Grid g, Vector3 from, Vector3 to)
    {
        int n = g.n, s = g.Index(from), goal = g.Index(to);
        var seen = new bool[n * n];
        var queue = new Queue<int>();
        queue.Enqueue(s);
        seen[s] = true;
        int reached = 0;
        float closest = float.MaxValue;
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            reached++;
            closest = Mathf.Min(closest, Flat(g.Centre(i) - g.Centre(goal)));
            int x = i % n, z = i / n;
            for (int k = 0; k < 8; k++)
            {
                int nx = x + DX[k], nz = z + DZ[k];
                if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                int j = nz * n + nx;
                if (seen[j] || (g.flags[j] & 1) != 0 || g.steep[j] > 55f) continue;
                seen[j] = true;
                queue.Enqueue(j);
            }
        }
        int blockedNear = 0, near = 0;
        ForCells(g, g.Centre(goal), 15f, c => { near++; if ((g.flags[c] & 1) != 0) blockedNear++; });
        return "start blocked " + ((g.flags[s] & 1) != 0) + ", goal blocked " + ((g.flags[goal] & 1) != 0) + " steep " + g.steep[goal].ToString("0") + ", blocked within 15 m of the goal " + blockedNear + " of " + near
            + ", reached " + reached + " cells, closest " + closest.ToString("0") + " m";
    }

    static List<Vector3> Chaikin(List<Vector3> p)
    {
        if (p.Count < 3) return p;
        var r = new List<Vector3> { p[0] };
        for (int i = 0; i < p.Count - 1; i++)
        {
            r.Add(Vector3.Lerp(p[i], p[i + 1], 0.25f));
            r.Add(Vector3.Lerp(p[i], p[i + 1], 0.75f));
        }
        r.Add(p[p.Count - 1]);
        return r;
    }

    static List<Vector3> Resample(List<Vector3> p, float step)
    {
        var r = new List<Vector3> { p[0] };
        float carry = 0f;
        for (int i = 1; i < p.Count; i++)
        {
            Vector3 a = p[i - 1], b = p[i];
            float len = Flat(b - a);
            float d = step - carry;
            while (d <= len) { r.Add(Vector3.Lerp(a, b, d / Mathf.Max(0.0001f, len))); d += step; }
            carry = len - (d - step);
        }
        if (Flat(r[r.Count - 1] - p[p.Count - 1]) > step * 0.3f) r.Add(p[p.Count - 1]);
        return r;
    }

    // ---------------- painting the ground ---------------- //

    // distance from each spot of a w x w map (cell metres) to the nearest of these points, up to reach (else reach)
    static float[] Distances(Vector3 origin, int w, float cell, List<List<Vector3>> lines, float reach)
    {
        var d = new float[w * w];
        for (int i = 0; i < d.Length; i++) d[i] = reach;
        foreach (var line in lines)
            foreach (var p in line)
            {
                int x0 = Mathf.FloorToInt((p.x - reach - origin.x) / cell), x1 = Mathf.CeilToInt((p.x + reach - origin.x) / cell);
                int z0 = Mathf.FloorToInt((p.z - reach - origin.z) / cell), z1 = Mathf.CeilToInt((p.z + reach - origin.z) / cell);
                for (int z = Mathf.Max(0, z0); z <= Mathf.Min(w - 1, z1); z++)
                    for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                    {
                        float dx = origin.x + (x + 0.5f) * cell - p.x, dz = origin.z + (z + 0.5f) * cell - p.z;
                        float e = Mathf.Sqrt(dx * dx + dz * dz);
                        int i = z * w + x;
                        if (e < d[i]) d[i] = e;
                    }
            }
        return d;
    }

    // the stream's bed: how far from its middle, as a share of its width there (0 middle, 1 its edge)
    static float[] BedDistances(Vector3 origin, int w, float cell, List<Vector3> stream, List<float> widths, float extra)
    {
        var d = new float[w * w];
        for (int i = 0; i < d.Length; i++) d[i] = 99f;
        for (int k = 0; k < stream.Count; k++)
        {
            var p = stream[k];
            float reach = widths[k] + extra;
            int x0 = Mathf.FloorToInt((p.x - reach - origin.x) / cell), x1 = Mathf.CeilToInt((p.x + reach - origin.x) / cell);
            int z0 = Mathf.FloorToInt((p.z - reach - origin.z) / cell), z1 = Mathf.CeilToInt((p.z + reach - origin.z) / cell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(w - 1, z1); z++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(w - 1, x1); x++)
                {
                    float dx = origin.x + (x + 0.5f) * cell - p.x, dz = origin.z + (z + 0.5f) * cell - p.z;
                    float e = Mathf.Sqrt(dx * dx + dz * dz) - widths[k]; // metres beyond the water's edge
                    int i = z * w + x;
                    if (e < d[i]) d[i] = e;
                }
        }
        return d;
    }

    // one ground layer up to "want" at this spot, the others less (all still adding up to 1)
    static void Raise(float[,,] maps, int z, int x, int layers, int layer, float want)
    {
        float now = maps[z, x, layer];
        if (want <= now) return;
        float rest = 1f - now;
        float scale = rest > 1e-5f ? (1f - want) / rest : 0f;
        for (int l = 0; l < layers; l++) if (l != layer) maps[z, x, l] *= scale;
        maps[z, x, layer] = want;
    }

    // distance (in spots) from every spot of a res x res map to the nearest "from" spot (two sweeps, 1 and 1.41 steps)
    static float[] Chamfer(bool[] from, int res)
    {
        var d = new float[res * res];
        for (int i = 0; i < d.Length; i++) d[i] = from[i] ? 0f : 1e6f;
        const float A = 1f, B = 1.4142f;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = z * res + x;
                float v = d[i];
                if (x > 0) v = Mathf.Min(v, d[i - 1] + A);
                if (z > 0)
                {
                    v = Mathf.Min(v, d[i - res] + A);
                    if (x > 0) v = Mathf.Min(v, d[i - res - 1] + B);
                    if (x < res - 1) v = Mathf.Min(v, d[i - res + 1] + B);
                }
                d[i] = v;
            }
        for (int z = res - 1; z >= 0; z--)
            for (int x = res - 1; x >= 0; x--)
            {
                int i = z * res + x;
                float v = d[i];
                if (x < res - 1) v = Mathf.Min(v, d[i + 1] + A);
                if (z < res - 1)
                {
                    v = Mathf.Min(v, d[i + res] + A);
                    if (x < res - 1) v = Mathf.Min(v, d[i + res + 1] + B);
                    if (x > 0) v = Mathf.Min(v, d[i + res - 1] + B);
                }
                d[i] = v;
            }
        return d;
    }

    // returns the old road lines (alphamap spots) taken away
    static bool[] PaintGround(Terrain terrain, int pathLayer, int bedLayer, List<List<Vector3>> paths, List<Vector3> stream, List<float> widths, StringBuilder report)
    {
        var data = terrain.terrainData;
        int res = data.alphamapResolution;
        float cell = data.size.x / res;
        Vector3 tp = terrain.transform.position;
        var maps = data.GetAlphamaps(0, 0, res, res);
        int layers = maps.GetLength(2);

        // The old straight road lines (and the paths of an earlier run): strips of the road layer up to ~18 m wide. The
        // big earth patches on the mountains stay: a spot belongs to one when it is near a spot that has road all
        // around it for WideR spots.
        var road = new float[res * res];
        for (int z = 0; z < res; z++) for (int x = 0; x < res; x++) road[z * res + x] = maps[z, x, pathLayer];
        const float WideR = 5f; // spots (~10 m)
        var notRoad = new bool[res * res];
        for (int i = 0; i < notRoad.Length; i++) notRoad[i] = road[i] < 0.3f;
        var inside = Chamfer(notRoad, res);          // how deep in the road layer each spot is
        var core = new bool[res * res];
        for (int i = 0; i < core.Length; i++) core[i] = inside[i] >= WideR;
        var fromCore = Chamfer(core, res);           // how far from the middle of a wide patch
        var oldLine = new bool[res * res];
        int removed = 0;
        for (int i = 0; i < oldLine.Length; i++)
        {
            if (road[i] < 0.02f || fromCore[i] <= WideR + 3.5f) continue; // no road, or a wide patch: stays
            oldLine[i] = true;
            removed++;
        }
        // the old lines get the ground around them back (the nearest spots without road)
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                if (!oldLine[z * res + x]) continue;
                float[] sum = new float[layers];
                float weight = 0f;
                for (int r = 2; r <= 8 && weight < 0.5f; r += 2)
                    for (int dz = -r; dz <= r; dz += r)
                        for (int dx = -r; dx <= r; dx += r)
                        {
                            int ax = x + dx, az = z + dz;
                            if (ax < 0 || az < 0 || ax >= res || az >= res || oldLine[az * res + ax]) continue;
                            for (int l = 0; l < layers; l++) sum[l] += maps[az, ax, l];
                            weight += 1f;
                        }
                if (weight <= 0f) continue;
                sum[pathLayer] = 0f;
                float total = 0f;
                for (int l = 0; l < layers; l++) total += sum[l];
                if (total <= 0f) { sum[0] = 1f; total = 1f; }
                for (int l = 0; l < layers; l++) maps[z, x, l] = sum[l] / total;
            }

        // the paths: trodden earth with soft, uneven edges
        var pathDistance = Distances(tp, res, cell, paths, PathHalf + PathEdge + 1f);
        var bedDistance = BedDistances(tp, res, cell, stream, widths, 3f);
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int i = z * res + x;
                float wx = tp.x + (x + 0.5f) * cell, wz = tp.z + (z + 0.5f) * cell;
                float noise = Mathf.PerlinNoise(wx * 0.21f + 3.3f, wz * 0.21f + 8.1f);
                float bed = bedDistance[i] < 3f ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 2.6f + noise * 0.6f, bedDistance[i]))) * (0.8f + 0.2f * noise) : 0f;
                float path = pathDistance[i] < PathHalf + PathEdge + 1f
                    ? (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(PathHalf * 0.45f, PathHalf + PathEdge * (0.6f + 0.6f * noise), pathDistance[i]))) * (0.82f + 0.16f * noise) : 0f;
                path *= 1f - bed; // the stream's stones where the path crosses it
                if (bed <= 0.001f && path <= 0.001f) continue;
                // at least this much of each (a second run paints the same, not twice as much)
                Raise(maps, z, x, layers, pathLayer, path);
                Raise(maps, z, x, layers, bedLayer, bed);
            }
        data.SetAlphamaps(0, 0, maps);
        report.AppendLine("ground: old road line spots taken away " + removed + ", paths and the stream's stones painted");
        return oldLine;
    }

    // ---------------- the grass and the little plants ---------------- //

    static void Details(Terrain terrain, List<List<Vector3>> paths, List<Vector3> stream, List<float> widths, bool[] oldLines, StringBuilder report)
    {
        var data = terrain.terrainData;
        int res = data.detailResolution;
        float cell = data.size.x / res;
        Vector3 tp = terrain.transform.position;
        int ares = data.alphamapResolution;
        var pathDistance = Distances(tp, res, cell, paths, PathHalf + 2f);
        var bedDistance = BedDistances(tp, res, cell, stream, widths, 3.5f);
        int cleared = 0, restored = 0;
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var d = data.GetDetailLayer(0, 0, res, res, layer);
            var before = (int[,])d.Clone();
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i = z * res + x;
                    float wx = tp.x + (x + 0.5f) * cell, wz = tp.z + (z + 0.5f) * cell;
                    float noise = Mathf.PerlinNoise(wx * 0.37f + 1.1f, wz * 0.37f + 6.4f);
                    // the old road lines: their grass comes back (from the ground next to them)
                    int ai = Mathf.Clamp((int)(z * (float)ares / res), 0, ares - 1) * ares + Mathf.Clamp((int)(x * (float)ares / res), 0, ares - 1);
                    if (oldLines[ai] && before[z, x] == 0)
                    {
                        for (int r = 3; r <= 12; r += 3)
                        {
                            int bx = Mathf.Clamp(x + ((x * 7 + z * 3) % 2 == 0 ? r : -r), 0, res - 1);
                            int bz = Mathf.Clamp(z + ((x * 5 + z) % 2 == 0 ? r : -r), 0, res - 1);
                            int bi = Mathf.Clamp((int)(bz * (float)ares / res), 0, ares - 1) * ares + Mathf.Clamp((int)(bx * (float)ares / res), 0, ares - 1);
                            if (oldLines[bi]) continue;
                            d[z, x] = before[bz, bx];
                            if (d[z, x] > 0) restored++;
                            break;
                        }
                    }
                    // the paths and the stream's bed: bare (a few blades at the very edge)
                    float edge = PathHalf + 0.3f + noise * 0.9f;
                    if (pathDistance[i] < edge || bedDistance[i] < 1.2f + noise * 0.8f) { if (d[z, x] > 0) cleared++; d[z, x] = 0; }
                    else if (pathDistance[i] < edge + 1.1f || bedDistance[i] < 2.4f + noise) d[z, x] = d[z, x] / 2;
                }
            data.SetDetailLayer(0, 0, layer, d);
        }
        report.AppendLine("grass: " + cleared + " spots cleared on the paths and the bed, " + restored + " given back on the old road lines");
    }

    // ---------------- the scene: trees and plants off the paths, the rest stays on the ground ----------------

    static void ClearScene(Terrain terrain, List<List<Vector3>> paths, List<Vector3> stream, List<float> widths, float[,] before, float[,] after, StringBuilder report)
    {
        int trees = 0, plants = 0, moved = 0;
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 tp = terrain.transform.position;
        float Lowered(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.RoundToInt((p.x - tp.x) / data.size.x * (res - 1)), 0, res - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((p.z - tp.z) / data.size.z * (res - 1)), 0, res - 1);
            return (before[z, x] - after[z, x]) * data.size.y;
        }
        float PathGap(Vector3 p)
        {
            float best = 999f;
            foreach (var line in paths) foreach (var q in line) { float d = Flat(q - p); if (d < best) best = d; }
            return best;
        }
        float BedGap(Vector3 p)
        {
            float best = 999f;
            for (int k = 0; k < stream.Count; k++) { float d = Flat(stream[k] - p) - widths[k]; if (d < best) best = d; }
            return best;
        }
        // trees: off the paths (their trunk clear of it) and out of the bed
        foreach (var t in SceneTrees())
        {
            if (t == null) continue;
            var r = t.GetComponent<Renderer>();
            float height = r != null ? r.bounds.size.y : 10f;
            float trunk = Mathf.Max(0.3f, height * 0.06f);
            if (PathGap(t.position) < PathHalf + 1.2f + trunk || BedGap(t.position) < 2.5f + trunk)
            {
                Undo.DestroyObjectImmediate(t.gameObject);
                trees++;
            }
        }
        // bushes, mushrooms, branches, flowers...: off the paths and out of the bed
        foreach (var p in Object.FindObjectsOfType<ScenePlant>())
        {
            if (p == null) continue;
            if (PathGap(p.transform.position) < PathHalf + 0.6f || BedGap(p.transform.position) < 1.6f)
            {
                Undo.DestroyObjectImmediate(p.gameObject);
                plants++;
            }
        }
        // what stands by the dug bed goes down with the ground (trees, plants, stones to pick up)
        var things = new List<Transform>(SceneTrees());
        foreach (var p in Object.FindObjectsOfType<ScenePlant>()) things.Add(p.transform);
        foreach (var p in Object.FindObjectsOfType<InteractableObject>()) if (p.GetComponentInParent<ScenePlant>() == null) things.Add(p.transform);
        foreach (var t in things)
        {
            if (t == null) continue;
            float down = Lowered(t.position);
            if (down < 0.02f) continue;
            Undo.RecordObject(t, "Brother Ioan's paths");
            t.position += Vector3.down * down;
            moved++;
        }
        report.AppendLine("scene: " + trees + " trees and " + plants + " plants taken off the paths and the bed, " + moved + " things lowered with the ground");
    }
}
