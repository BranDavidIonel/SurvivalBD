using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 7. More Rivers (From The Mountains To The Sea)
// The owner drew five more rivers on a picture of the island (water was found only in the middle, so there was no
// reason to go anywhere else). Their lines, as points on the island, are kept in Assets/Island/River/RiverGuides.json
// (the mountain end first). For each one this tool:
//  - puts its spring high on the mountain where the line starts (away from Brother Ioan's steps, the cave mouth, the
//    old river and the stream of the miracle), so the river comes from the mountain and has water all the way
//  - finds the way water would take from there down to the sea, near the drawn line: downhill, around the trees
//    where it can, never into the old river, the lake, the stream's bed, the cave mouth or the steps; it crosses the
//    paths straight over
//  - digs its bed into the island's terrain (IslandTerrain.asset): a stony bed with low banks, shallow enough to wade
//    across, and takes the grass out of the water
//  - takes away the trees and plants standing in the water or on its edge, puts the others near it back on the
//    new ground
//  - makes its water (Assets/Island/River/River_<name>.asset) and a scene object under "Rivers" with a River
//    component (fresh water: drinking, rabbits and trees keep out, the map draws it)
// Running it again first puts back the ground it dug the time before (Assets/Island/River/RiversDug.bytes), so it can
// be run again after changing RiverGuides.json. (Trees it took away stay away: git has the old scene.)
public static class MoreRiversTool
{
    const string Dir = "Assets/Island/River";
    const string GuidesPath = Dir + "/RiverGuides.json";
    const string DugPath = Dir + "/RiversDug.bytes";
    const string ParentName = "Rivers";
    const float Cell = 4f;            // the grid the way down is searched on (metres)

    [System.Serializable]
    public class Guide
    {
        public string name;           // "East" -> River_East.asset, "River East"
        public string label;          // the name shown (English)
        public List<Vector3> points = new List<Vector3>(); // the drawn line: the mountain end first (y: not used)
    }

    [System.Serializable]
    public class GuideData { public List<Guide> rivers = new List<Guide>(); }

    [MenuItem("Tools/Nature/7. More Rivers (From The Mountains To The Sea)")]
    static void MakeFromMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("More rivers: stop Play first"); return; }
        if (!EditorUtility.DisplayDialog("More rivers",
            "Digs the five new rivers (Assets/Island/River/RiverGuides.json) into the island, takes away the trees in the water and saves the terrain and the scene.\n\n(git keeps the old ones.)",
            "Make them", "Cancel")) return;
        Debug.Log(Make());
    }

    // ---------------- the drawn lines (from the owner's picture, 2026-11-07) ---------------- //

    static GuideData DefaultGuides()
    {
        GuideData d = new GuideData();
        d.rivers.Add(G("East", "East river", 612, 363, 824, 250, 827, 277, 823, 295, 810, 318, 801, 339, 797, 356, 803, 366, 813, 374, 831, 377, 855, 376,
            882, 373, 908, 370, 936, 366, 963, 362, 986, 361, 1006, 362, 1028, 363, 1050, 363, 1072, 363, 1089, 366, 1106, 369, 1119, 375, 1128, 384,
            1135, 394, 1144, 402, 1162, 405, 1181, 407, 1206, 405, 1233, 401, 1257, 400, 1281, 399, 1306, 397, 1337, 391));
        d.rivers.Add(G("South", "South river", 611, 230, 613, 200, 743, 98, 732, 91, 718, 86, 703, 81, 692, 74, 681, 66, 669, 59, 659, 52, 648, 44,
            641, 34, 636, 23, 636, 8, 635, -6, 637, -22, 641, -40, 641, -54, 639, -67, 638, -81, 638, -96, 643, -114, 646, -131, 651, -149, 656, -167));
        d.rivers.Add(G("West", "West river", 437, 156, 431, 145, 424, 136, 418, 125, 353, 125, 320, 132, 298, 132, 279, 130, 244, 139, 219, 141, 195, 142,
            165, 148, 144, 147, 121, 148, 97, 149, 66, 155, 39, 159, 13, 161, -11, 163, -38, 166, -235, 107, -245, 99, -256, 92));
        d.rivers.Add(G("NorthEast", "North-east river", 785, 688, 798, 694, 813, 698, 893, 660, 911, 662, 930, 664, 949, 666, 967, 669, 985, 671,
            1002, 675, 1016, 680, 1030, 685, 1044, 690, 1055, 697, 1068, 704, 1079, 711, 1094, 715, 1113, 718, 1131, 720, 1151, 721, 1169, 724,
            1186, 727, 1203, 731, 1215, 737, 1239, 736, 1254, 741, 1269, 745, 1289, 747, 1305, 751));
        d.rivers.Add(G("North", "North river", 718, 732, 724, 758, 764, 746, 748, 860, 752, 872, 756, 884, 762, 894, 738, 925, 711, 958, 676, 996,
            678, 1010, 683, 1021, 650, 1057, 590, 1112, 573, 1212));
        return d;
    }

    static Guide G(string name, string label, params float[] xz)
    {
        var g = new Guide { name = name, label = label };
        for (int i = 0; i + 1 < xz.Length; i += 2) g.points.Add(new Vector3(xz[i], 0f, xz[i + 1]));
        return g;
    }

    public static GuideData LoadGuides()
    {
        if (!File.Exists(GuidesPath))
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(GuidesPath, JsonUtility.ToJson(DefaultGuides(), true));
            AssetDatabase.ImportAsset(GuidesPath);
        }
        return JsonUtility.FromJson<GuideData>(File.ReadAllText(GuidesPath));
    }

    // ---------------- making them ---------------- //

    class Plan
    {
        public Guide guide;
        public List<Vector3> path;    // the middle line of the water (y: the water's surface)
        public float[] hw, depth, surface, lengths;
        public float total;
        public Vector3 spring;
        public int treesTaken, plantsTaken;
    }

    public static string Make()
    {
        var report = new StringBuilder();
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        if (terrain == null || ocean == null) return "More rivers: no terrain or ocean in the open scene";
        var data = terrain.terrainData;
        float level = ocean.Level;
        Directory.CreateDirectory(Dir);
        var guides = LoadGuides();

        // the ground dug the last time is put back first, the old water taken away
        int restored = RestoreDug(terrain);
        if (restored > 0) report.AppendLine("ground of the last run put back: " + restored + " values");
        foreach (var old in Object.FindObjectsOfType<Transform>())
            if (old != null && old.name == ParentName && old.GetComponentInChildren<River>(true) != null) Object.DestroyImmediate(old.gameObject);

        var mainRiver = MainRiver();
        var stream = MiracleStream.SavedRoute();
        var cave = Object.FindObjectOfType<Cave>();
        var start = Object.FindObjectOfType<StoryStart>();
        var paths = IslandPaths.Data;
        var grid = new Grid(terrain, ocean);
        grid.Block(mainRiver, stream, cave, start, paths, report);
        grid.CountTrees();

        var plans = new List<Plan>();
        foreach (var guide in guides.rivers)
        {
            if (guide == null || guide.points.Count < 2) continue;
            var plan = Route(grid, terrain, ocean, guide, report);
            if (plan == null) continue;
            plans.Add(plan);
            grid.BlockAround(plan.path, 24f); // the next rivers keep away from this one (no meeting by chance)
        }
        if (plans.Count == 0) return report.Append("no river could be made").ToString();

        // the ground: dug, painted, the grass taken out of the water (what was there is kept to put it back)
        var dug = new DugRecord();
        foreach (var plan in plans) Carve(terrain, plan, level, dug);
        dug.Save(DugPath);
        AssetDatabase.ImportAsset(DugPath);
        EditorUtility.SetDirty(data);

        // the water and the scene objects
        var manager = Object.FindObjectOfType<EnviromentManager>();
        var parent = new GameObject(ParentName).transform;
        if (manager != null) parent.SetParent(manager.transform, false);
        parent.position = Vector3.zero;
        var material = AssetDatabase.LoadAssetAtPath<Material>(Dir + "/River.mat");
        if (material == null && mainRiver != null) material = mainRiver.GetComponent<MeshRenderer>().sharedMaterial;
        var rivers = new List<River>();
        foreach (var plan in plans)
        {
            var points = new List<Vector3>();
            var widths = new List<float>();
            for (int i = 0; i < plan.path.Count; i++) { points.Add(new Vector3(plan.path[i].x, plan.surface[i], plan.path[i].z)); widths.Add(plan.hw[i]); }
            var mesh = WaterMesh(points, widths, plan.lengths, plan.total);
            mesh.name = "River " + plan.guide.name;
            string meshPath = Dir + "/River_" + plan.guide.name + ".asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);
            var go = new GameObject("River " + plan.guide.label);
            go.transform.SetParent(parent, false);
            go.transform.position = Vector3.zero;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var river = go.AddComponent<River>();
            river.points = points;
            river.halfWidths = widths;
            river.lakeCentre = Vector3.zero;
            river.lakeRadius = 0f;
            SetName(river, plan.guide.label);
            river.BuildCells();
            rivers.Add(river);
        }

        // trees and plants in the water or on its edge: away; the others near the rivers back on the new ground
        TakeAwayAndSnap(terrain, rivers, plans, report);

        foreach (var plan in plans)
            report.AppendLine(plan.guide.label + ": " + Mathf.RoundToInt(plan.total) + " m, spring " + V(plan.spring) + " (" + Mathf.RoundToInt(plan.spring.y - level)
                + " m over the sea), mouth " + V(plan.path[plan.path.Count - 1]) + ", water " + plan.surface[0].ToString("F1") + " -> " + plan.surface[plan.surface.Length - 1].ToString("F1")
                + ", trees taken away " + plan.treesTaken + ", plants " + plan.plantsTaken);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(parent.gameObject.scene);
        EditorSceneManager.SaveScene(parent.gameObject.scene);
        return "More rivers: " + plans.Count + " made\n" + report;
    }

    static River MainRiver()
    {
        foreach (var r in Object.FindObjectsOfType<River>()) if (r.lakeRadius > 0f) return r;
        return null;
    }

    // (River.riverName is added by the game's side of this change: set it if it is there)
    static void SetName(River river, string label)
    {
        var field = typeof(River).GetField("riverName");
        if (field != null) field.SetValue(river, label);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static float Ground(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;
    static string V(Vector3 v) => "(" + v.x.ToString("F0") + ", " + v.y.ToString("F1") + ", " + v.z.ToString("F0") + ")";

    // ---------------- the grid: what the rivers keep away from ---------------- //

    class Grid
    {
        public readonly int n;
        public readonly Vector3 origin;
        public readonly float[] height;
        public readonly bool[] blocked;   // never
        public readonly bool[] shore;     // at the sea (where a river may end)
        public readonly float[] extra;    // more expensive (paths, trees)
        readonly Terrain terrain;
        readonly Ocean ocean;

        public Grid(Terrain terrain, Ocean ocean)
        {
            this.terrain = terrain;
            this.ocean = ocean;
            var size = terrain.terrainData.size;
            origin = terrain.transform.position;
            n = Mathf.RoundToInt(size.x / Cell);
            height = new float[n * n];
            blocked = new bool[n * n];
            shore = new bool[n * n];
            extra = new float[n * n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int i = z * n + x;
                    Vector3 p = Centre(i);
                    height[i] = Ground(terrain, p);
                    float fromShore = ocean.DistanceFromShore(p);
                    if (fromShore > 2f) blocked[i] = true;              // the sea
                    else if (fromShore > -5f) shore[i] = true;          // the edge of the sea
                    if (x < 2 || z < 2 || x > n - 3 || z > n - 3) blocked[i] = true;
                }
        }

        public Vector3 Centre(int i) => origin + new Vector3((i % n + 0.5f) * Cell, 0f, (i / n + 0.5f) * Cell);
        public int Index(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - origin.x) / Cell), 0, n - 1), z = Mathf.Clamp(Mathf.FloorToInt((p.z - origin.z) / Cell), 0, n - 1);
            return z * n + x;
        }

        void Disc(Vector3 c, float radius, bool hard, float cost = 0f)
        {
            int r = Mathf.CeilToInt(radius / Cell) + 1;
            int cx = Mathf.FloorToInt((c.x - origin.x) / Cell), cz = Mathf.FloorToInt((c.z - origin.z) / Cell);
            for (int z = cz - r; z <= cz + r; z++)
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || z < 0 || x >= n || z >= n) continue;
                    int i = z * n + x;
                    if (Flat(Centre(i) - c) > radius) continue;
                    if (hard) blocked[i] = true; else extra[i] = Mathf.Max(extra[i], cost);
                }
        }

        public void BlockAround(List<Vector3> line, float radius)
        {
            for (int i = 0; i < line.Count; i++)
            {
                if (i > 0 && Flat(line[i] - line[i - 1]) < 2f) continue;
                Disc(line[i], radius, true);
            }
        }

        public void Block(River mainRiver, List<Vector3> stream, Cave cave, StoryStart start, IslandPaths.PathsData paths, StringBuilder report)
        {
            if (mainRiver != null)
            {
                for (int i = 0; i < mainRiver.points.Count; i++) Disc(mainRiver.points[i], (i < mainRiver.halfWidths.Count ? mainRiver.halfWidths[i] : 4f) + 20f, true);
                if (mainRiver.lakeRadius > 0f) Disc(mainRiver.lakeCentre, mainRiver.lakeRadius + 30f, true);
            }
            if (stream != null) foreach (var p in stream) Disc(p, 20f, true);
            if (cave != null)
            {
                if (cave.path.Count > 0) Disc(cave.path[0], 48f, true);
                foreach (var p in cave.path) Disc(p, 14f, true);
            }
            if (start != null) Disc(start.transform.position, 30f, true);
            if (paths != null)
            {
                if (paths.landingSize > 0f) Disc(paths.landing, 26f, true);
                if (paths.stairs != null) foreach (var line in paths.stairs) foreach (var p in line.points) Disc(p, 14f, true);
                // paths: crossed straight over (each step on a path costs much)
                if (paths.paths != null) foreach (var line in paths.paths) foreach (var p in line.points) Disc(p, paths.halfWidth + 3f, false, 30f);
            }
            // buildings and things placed in the scene (not the nature groups)
            foreach (var p in RiverTool.ExistingObjects()) Disc(p, 6f, false, 25f);
        }

        // how many trunks stand in each cell (the way goes around them where it can)
        public void CountTrees()
        {
            foreach (var t in TreeRoots())
            {
                int i = Index(t.position);
                extra[i] += 7f;
                // the cells around a trunk a little too (a river 3-7 m wide)
                Disc(t.position, 4f, false, Mathf.Min(14f, extra[i]));
            }
        }
    }

    // every tree of the scene: SceneTree, ScenePine and the old choppable birches (Tree_Parent)
    static List<Transform> TreeRoots()
    {
        var list = new List<Transform>();
        foreach (var t in Object.FindObjectsOfType<SceneTree>()) list.Add(t.transform);
        foreach (var t in Object.FindObjectsOfType<ScenePine>()) list.Add(t.transform);
        foreach (var c in Object.FindObjectsOfType<ChoppableTree>())
        {
            Transform root = c.transform;
            while (root.parent != null && root.parent.GetComponent<Animator>() == null && root.parent.GetComponent<EnviromentManager>() == null
                   && root.parent.childCount == 1) root = root.parent;
            if (root.parent != null && root.parent.GetComponent<Animator>() != null) root = root.parent;
            if (root.GetComponent<SceneTree>() == null && root.GetComponent<ScenePine>() == null) list.Add(root);
        }
        return list;
    }

    // ---------------- the way down ---------------- //

    static Plan Route(Grid grid, Terrain terrain, Ocean ocean, Guide guide, StringBuilder report)
    {
        int n = grid.n;
        // how far each cell is from the drawn line
        var guideDistance = new float[n * n];
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in guide.points) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        const float corridor = 170f;
        for (int i = 0; i < guideDistance.Length; i++)
        {
            Vector3 c = grid.Centre(i);
            if (c.x < minX - corridor || c.x > maxX + corridor || c.z < minZ - corridor || c.z > maxZ + corridor) { guideDistance[i] = 1e6f; continue; }
            guideDistance[i] = DistanceToLine(guide.points, c);
        }

        // the spring: high on the mountain near where the line starts, on ground the river may use
        Vector3 drawnStart = guide.points[0];
        drawnStart.y = Ground(terrain, drawnStart);
        int springCell = -1;
        float bestScore = float.MinValue;
        int r = Mathf.CeilToInt(60f / Cell);
        int sc = grid.Index(drawnStart);
        for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int x = sc % n + dx, z = sc / n + dz;
                if (x < 0 || z < 0 || x >= n || z >= n) continue;
                int i = z * n + x;
                if (grid.blocked[i]) continue;
                float d = Flat(grid.Centre(i) - drawnStart);
                if (d > 60f) continue;
                float h = grid.height[i];
                if (h < -19.05f + 22f) continue;                       // a spring is up on the mountain
                // as high as where the line starts (not higher), as near to it as can be
                float score = -Mathf.Abs(h - Mathf.Min(drawnStart.y, h)) * 0.2f - Mathf.Max(0f, drawnStart.y - h) * 0.6f - d * 0.5f;
                if (score > bestScore) { bestScore = score; springCell = i; }
            }
        if (springCell < 0)
        {
            report.AppendLine(guide.label + ": no place for a spring near " + V(drawnStart) + " (all near the steps, the cave, the old river or too low)");
            return null;
        }

        // where it may end: the edge of the sea near the end of the drawn line
        Vector3 drawnEnd = guide.points[guide.points.Count - 1];
        var target = new bool[n * n];
        int targets = 0;
        for (int i = 0; i < target.Length; i++)
            if (grid.shore[i] && !grid.blocked[i] && Flat(grid.Centre(i) - drawnEnd) < 160f) { target[i] = true; targets++; }
        if (targets == 0) { report.AppendLine(guide.label + ": no shore near " + V(drawnEnd)); return null; }

        // the cheapest way: short, downhill (uphill costs very much), near the drawn line, around trees, few path cells
        var cost = new float[n * n];
        var from = new int[n * n];
        for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; from[i] = -1; }
        var heap = new Heap();
        cost[springCell] = 0f;
        heap.Push(0f, springCell);
        int end = -1;
        while (heap.Count > 0)
        {
            heap.Pop(out float c, out int i);
            if (c > cost[i]) continue;
            if (target[i]) { end = i; break; }
            int x = i % n, z = i / n;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int j = nz * n + nx;
                    if (grid.blocked[j] && !target[j]) continue;
                    float g = guideDistance[j];
                    if (g > 140f) continue;
                    float len = (dx != 0 && dz != 0 ? 1.4142f : 1f);
                    Vector3 cj = grid.Centre(j);
                    float wind = Mathf.PerlinNoise(cj.x * 0.013f + 3.7f, cj.z * 0.013f + 9.1f);
                    float near = g < 14f ? 0f : (g - 14f) * (g - 14f) / 90f;          // keep near the drawn line
                    float up = Mathf.Max(0f, grid.height[j] - grid.height[i]);        // water does not climb
                    float step = len * Cell * (1f + 0.5f * wind + near) + up * 160f + grid.extra[j] * len;
                    float total = c + step;
                    if (total < cost[j]) { cost[j] = total; from[j] = i; heap.Push(total, j); }
                }
        }
        if (end < 0) { report.AppendLine(guide.label + ": no way from the spring to the sea found"); return null; }

        var cells = new List<Vector3>();
        for (int i = end; i >= 0; i = from[i]) cells.Add(grid.Centre(i));
        cells.Reverse();
        // into the sea a little, so the river meets it
        Vector3 outDir = cells[cells.Count - 1] - cells[Mathf.Max(0, cells.Count - 5)]; outDir.y = 0f;
        outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward;
        for (int k = 1; k <= 3; k++) cells.Add(cells[cells.Count - 1] + outDir * 5f);

        // smooth, every 3 m, a gentle wave where the land is flat
        var path = cells;
        for (int k = 0; k < 4; k++) path = Chaikin(path);
        path = Resample(path, 3f);
        path = Meander(terrain, path);
        for (int k = 0; k < 2; k++) path = Chaikin(path);
        path = Resample(path, 3f);

        // width, depth, the water's surface (always going down; down to the sea at the end)
        int count = path.Count;
        var plan = new Plan { guide = guide, path = path, lengths = new float[count], hw = new float[count], depth = new float[count], surface = new float[count] };
        for (int i = 1; i < count; i++) plan.lengths[i] = plan.lengths[i - 1] + Flat(path[i] - path[i - 1]);
        plan.total = Mathf.Max(1f, plan.lengths[count - 1]);
        float level = ocean.Level;
        for (int i = 0; i < count; i++)
        {
            float t = plan.lengths[i] / plan.total;
            plan.hw[i] = Mathf.Lerp(1.5f, 3.8f, Mathf.Pow(t, 0.6f)); // (1.5 m at the spring: as wide as the ground's points, ~2 m apart)
            plan.depth[i] = Mathf.Lerp(0.45f, 0.9f, t);
            Vector3 side = Side(path, i);
            float low = Mathf.Min(Ground(terrain, path[i]), Mathf.Min(Ground(terrain, path[i] + side * plan.hw[i] * 1.3f), Ground(terrain, path[i] - side * plan.hw[i] * 1.3f)));
            float s = low - 0.28f;
            if (i > 0) s = Mathf.Min(s, plan.surface[i - 1] - 0.0012f * (plan.lengths[i] - plan.lengths[i - 1]));
            plan.surface[i] = Mathf.Max(s, level + 0.03f);
        }
        for (int i = 1; i < count; i++) plan.surface[i] = Mathf.Min(plan.surface[i], plan.surface[i - 1]);
        // where it falls steeply (down the mountain) the bed is dug deeper: the ground has a point only every ~2 m,
        // and on a steep slope the water would else hide under the rock between them
        for (int i = 0; i < count; i++)
        {
            int a = Mathf.Max(0, i - 2), b = Mathf.Min(count - 1, i + 2);
            float drop = (plan.surface[a] - plan.surface[b]) / Mathf.Max(0.5f, plan.lengths[b] - plan.lengths[a]);
            plan.depth[i] += Mathf.Clamp(drop * 1.3f, 0f, 2.5f);
        }
        plan.spring = new Vector3(path[0].x, plan.surface[0], path[0].z);
        return plan;
    }

    static float DistanceToLine(List<Vector3> line, Vector3 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            Vector2 a = new Vector2(line[i].x, line[i].z), b = new Vector2(line[i + 1].x, line[i + 1].z), q = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(a + ab * t, q));
        }
        return best;
    }

    // a gentle wave on flat land (none on the mountain, at the spring and at the sea)
    static List<Vector3> Meander(Terrain terrain, List<Vector3> path)
    {
        int count = path.Count;
        var lengths = new float[count];
        for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Flat(path[i] - path[i - 1]);
        float total = lengths[count - 1];
        var result = new List<Vector3>(count);
        for (int i = 0; i < count; i++)
        {
            Vector3 a = path[Mathf.Max(0, i - 4)], b = path[Mathf.Min(count - 1, i + 4)];
            float slope = Mathf.Abs(Ground(terrain, b) - Ground(terrain, a)) / Mathf.Max(1f, Flat(b - a));
            float flat = 1f - Mathf.Clamp01(slope / 0.07f);
            float ends = Mathf.SmoothStep(0f, 1f, lengths[i] / 80f) * Mathf.SmoothStep(0f, 1f, (total - lengths[i]) / 50f);
            float amplitude = 4.5f * flat * ends;
            float wave = 0.7f * Mathf.Sin(lengths[i] / 33f + 0.7f) + 0.3f * Mathf.Sin(lengths[i] / 13f + 2.1f);
            result.Add(path[i] + Side(path, i) * amplitude * wave);
        }
        return result;
    }

    static Vector3 Side(List<Vector3> path, int i)
    {
        Vector3 a = path[Mathf.Max(0, i - 1)], b = path[Mathf.Min(path.Count - 1, i + 1)];
        Vector3 d = b - a; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        d.Normalize();
        return new Vector3(d.z, 0f, -d.x);
    }

    static List<Vector3> Chaikin(List<Vector3> p)
    {
        var r = new List<Vector3> { p[0] };
        for (int i = 0; i + 1 < p.Count; i++)
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
        for (int i = 0; i + 1 < p.Count; i++)
        {
            Vector3 a = p[i], b = p[i + 1];
            float len = Flat(b - a);
            float d = step - carry;
            while (d <= len)
            {
                r.Add(Vector3.Lerp(a, b, d / len));
                d += step;
            }
            carry = len - (d - step);
        }
        if (Flat(r[r.Count - 1] - p[p.Count - 1]) > step * 0.3f) r.Add(p[p.Count - 1]);
        return r;
    }

    // ---------------- digging the bed (like the old river's: RiverTool.Carve) ---------------- //

    // what the ground was before (to put it back on the next run)
    class DugRecord
    {
        public readonly Dictionary<int, float> heights = new Dictionary<int, float>();          // heightmap index -> height (0..1)
        public readonly Dictionary<int, float[]> alphas = new Dictionary<int, float[]>();       // alphamap index -> weights
        public readonly Dictionary<long, int> details = new Dictionary<long, int>();            // (layer, index) -> count
        public int heightRes, alphaRes, detailRes, layers;

        public void Save(string path)
        {
            using (var file = File.Create(path))
            using (var zip = new GZipStream(file, System.IO.Compression.CompressionLevel.Optimal))
            using (var w = new BinaryWriter(zip))
            {
                w.Write(1); w.Write(heightRes); w.Write(alphaRes); w.Write(detailRes); w.Write(layers);
                w.Write(heights.Count); foreach (var kv in heights) { w.Write(kv.Key); w.Write(kv.Value); }
                w.Write(alphas.Count); foreach (var kv in alphas) { w.Write(kv.Key); foreach (var a in kv.Value) w.Write(a); }
                w.Write(details.Count); foreach (var kv in details) { w.Write(kv.Key); w.Write(kv.Value); }
            }
        }
    }

    // puts back what the last run dug (returns how many values)
    static int RestoreDug(Terrain terrain)
    {
        if (!File.Exists(DugPath)) return 0;
        var data = terrain.terrainData;
        int count = 0;
        using (var file = File.OpenRead(DugPath))
        using (var zip = new GZipStream(file, CompressionMode.Decompress))
        using (var r = new BinaryReader(zip))
        {
            r.ReadInt32();
            int hr = r.ReadInt32(), ar = r.ReadInt32(), dr = r.ReadInt32(), layers = r.ReadInt32();
            if (hr != data.heightmapResolution || ar != data.alphamapResolution || layers != data.terrainLayers.Length)
            {
                Debug.LogWarning("More rivers: the terrain changed since the last run: its dug ground is not put back");
                return 0;
            }
            var heights = data.GetHeights(0, 0, hr, hr);
            int hn = r.ReadInt32();
            for (int k = 0; k < hn; k++) { int i = r.ReadInt32(); float v = r.ReadSingle(); heights[i / hr, i % hr] = v; count++; }
            data.SetHeights(0, 0, heights);
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            int an = r.ReadInt32();
            for (int k = 0; k < an; k++)
            {
                int i = r.ReadInt32();
                for (int l = 0; l < layers; l++) maps[i / ar, i % ar, l] = r.ReadSingle();
                count++;
            }
            data.SetAlphamaps(0, 0, maps);
            int dn = r.ReadInt32();
            var byLayer = new Dictionary<int, List<(int, int)>>();
            for (int k = 0; k < dn; k++)
            {
                long key = r.ReadInt64(); int v = r.ReadInt32();
                int layer = (int)(key >> 32), i = (int)(key & 0xffffffff);
                if (!byLayer.TryGetValue(layer, out var list)) byLayer[layer] = list = new List<(int, int)>();
                list.Add((i, v));
            }
            if (dr == data.detailResolution)
                foreach (var kv in byLayer)
                {
                    if (kv.Key >= data.detailPrototypes.Length) continue;
                    var layer = data.GetDetailLayer(0, 0, dr, dr, kv.Key);
                    foreach (var (i, v) in kv.Value) { layer[i / dr, i % dr] = v; count++; }
                    data.SetDetailLayer(0, 0, kv.Key, layer);
                }
        }
        AssetDatabase.DeleteAsset(DugPath);
        EditorUtility.SetDirty(data);
        return count;
    }

    static void Carve(Terrain terrain, Plan plan, float level, DugRecord dug)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int res = data.heightmapResolution;
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1), sy = data.size.y;
        var path = plan.path;
        var hw = plan.hw;
        var heights = data.GetHeights(0, 0, res, res);
        dug.heightRes = res;

        // the segments sorted into squares
        const float cellSize = 24f;
        var cells = new Dictionary<long, List<int>>();
        float maxReach = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            float reach = Mathf.Max(hw[i], hw[i + 1]) * 2f + 12f;
            maxReach = Mathf.Max(maxReach, reach);
            int x0 = Mathf.FloorToInt((Mathf.Min(path[i].x, path[i + 1].x) - reach) / cellSize), x1 = Mathf.FloorToInt((Mathf.Max(path[i].x, path[i + 1].x) + reach) / cellSize);
            int z0 = Mathf.FloorToInt((Mathf.Min(path[i].z, path[i + 1].z) - reach) / cellSize), z1 = Mathf.FloorToInt((Mathf.Max(path[i].z, path[i + 1].z) + reach) / cellSize);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    long key = ((long)x << 32) ^ (uint)z;
                    if (!cells.TryGetValue(key, out var l)) cells[key] = l = new List<int>();
                    l.Add(i);
                }
        }
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in path) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        int xs = Mathf.Clamp(Mathf.FloorToInt((minX - maxReach - tp.x) / sx), 0, res - 1), xe = Mathf.Clamp(Mathf.CeilToInt((maxX + maxReach - tp.x) / sx), 0, res - 1);
        int zs = Mathf.Clamp(Mathf.FloorToInt((minZ - maxReach - tp.z) / sz), 0, res - 1), ze = Mathf.Clamp(Mathf.CeilToInt((maxZ + maxReach - tp.z) / sz), 0, res - 1);

        var bedAmount = new Dictionary<long, float>();
        for (int z = zs; z <= ze; z++)
            for (int x = xs; x <= xe; x++)
            {
                Vector3 w = new Vector3(tp.x + x * sx, 0f, tp.z + z * sz);
                if (!Nearest(cells, cellSize, path, w, out int seg, out float t, out float d)) continue;
                float h = Mathf.Lerp(hw[seg], hw[seg + 1], t);
                float s = Mathf.Lerp(plan.surface[seg], plan.surface[seg + 1], t);
                float dep = Mathf.Lerp(plan.depth[seg], plan.depth[seg + 1], t);
                float bank = 4f + h;
                // a shallow bed (wade across): bed -> a shelf 0.3 m under the water 0.8 m inside the edge -> the low
                // bank 0.45 m over the water 0.9 m outside the edge -> back to the land around
                // (at least ~1 point of the ground wide on each side: a narrow brook up on the mountain was narrower
                // than the ground's points, and its water hid under them)
                float inner = Mathf.Max(Mathf.Max(sx, sz) * 0.75f, h - 0.8f), outer = Mathf.Max(h + 0.9f, inner + 1.4f);
                float shelf = s - 0.3f, top = s + 0.45f;
                float orig = heights[z, x] * sy + tp.y;
                bank += Mathf.Clamp(top - orig, 0f, 3f) * 4f;  // land lower than the water: a gentle raised bank
                if (d > outer + bank) continue;
                float bed = s - dep;
                float target;
                if (d <= inner)
                {
                    float k = d / inner;
                    target = Mathf.Lerp(bed, shelf, k * k);
                }
                else if (d <= outer)
                {
                    target = Mathf.Lerp(shelf, top, Mathf.SmoothStep(0f, 1f, (d - inner) / (outer - inner)));
                    if (d > h) target = Mathf.Max(target, Mathf.Min(orig, top + 1.2f)); // a high bank stays high
                }
                else
                {
                    float f = Mathf.SmoothStep(0f, 1f, (d - outer) / bank);
                    target = Mathf.Lerp(top, orig, f);
                }
                // never under the sea floor around the mouth (only the bed itself goes down there)
                if (d > h && orig < level) target = Mathf.Min(target, Mathf.Max(orig, target));
                float value = Mathf.Clamp01((target - tp.y) / sy);
                if (Mathf.Abs(value - heights[z, x]) < 1e-7f) continue;
                int index = z * res + x;
                if (!dug.heights.ContainsKey(index)) dug.heights[index] = heights[z, x];
                heights[z, x] = value;
                bedAmount[((long)x << 32) | (uint)z] = d <= h + 0.8f ? 1f : 1f - Mathf.Clamp01((d - h - 0.8f) / 2.5f);
            }
        data.SetHeights(0, 0, heights);

        // paint: the stream's stones (or rock) and some sand in the bed
        int stone = -1, sand = -1, fallbackStone = -1;
        var layers = data.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string name = layers[i].name.ToLowerInvariant();
            if (name.Contains("streambed")) stone = i;
            if (fallbackStone < 0 && name.Contains("rock")) fallbackStone = i;
            if (name.Contains("sand")) sand = i;
        }
        if (stone < 0) stone = fallbackStone;
        int ar = data.alphamapResolution;
        dug.alphaRes = ar;
        dug.layers = layers.Length;
        if (stone >= 0)
        {
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            int layerCount = maps.GetLength(2);
            foreach (var kv in bedAmount)
            {
                int x = (int)(kv.Key >> 32), z = (int)(uint)kv.Key;
                int ax = Mathf.Clamp(Mathf.RoundToInt((float)x / (res - 1) * (ar - 1)), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt((float)z / (res - 1) * (ar - 1)), 0, ar - 1);
                int index = az * ar + ax;
                if (!dug.alphas.ContainsKey(index))
                {
                    var keep = new float[layerCount];
                    for (int l = 0; l < layerCount; l++) keep[l] = maps[az, ax, l];
                    dug.alphas[index] = keep;
                }
                float a = kv.Value;
                for (int l = 0; l < layerCount; l++) maps[az, ax, l] *= 1f - a;
                maps[az, ax, stone] += a * (sand >= 0 ? 0.7f : 1f);
                if (sand >= 0) maps[az, ax, sand] += a * 0.3f;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        // no grass in the water
        int dr = data.detailResolution;
        dug.detailRes = dr;
        int detailLayers = data.detailPrototypes.Length;
        if (dr > 0 && detailLayers > 0)
        {
            int dxs = Mathf.FloorToInt((float)xs / (res - 1) * dr), dxe = Mathf.Min(dr - 1, Mathf.CeilToInt((float)xe / (res - 1) * dr));
            int dzs = Mathf.FloorToInt((float)zs / (res - 1) * dr), dze = Mathf.Min(dr - 1, Mathf.CeilToInt((float)ze / (res - 1) * dr));
            int w = dxe - dxs + 1, h = dze - dzs + 1;
            for (int l = 0; l < detailLayers; l++)
            {
                var layer = data.GetDetailLayer(dxs, dzs, w, h, l);
                bool changed = false;
                for (int z = 0; z < h; z++)
                    for (int x = 0; x < w; x++)
                    {
                        if (layer[z, x] == 0) continue;
                        Vector3 p = new Vector3(tp.x + (dxs + x + 0.5f) / dr * data.size.x, 0f, tp.z + (dzs + z + 0.5f) / dr * data.size.z);
                        if (!Nearest(cells, cellSize, path, p, out int seg, out float t, out float d)) continue;
                        if (d >= Mathf.Lerp(hw[seg], hw[seg + 1], t) + 0.7f) continue;
                        long key = ((long)l << 32) | (uint)((dzs + z) * dr + dxs + x);
                        if (!dug.details.ContainsKey(key)) dug.details[key] = layer[z, x];
                        layer[z, x] = 0;
                        changed = true;
                    }
                if (changed) data.SetDetailLayer(dxs, dzs, l, layer);
            }
        }
    }

    static bool Nearest(Dictionary<long, List<int>> cells, float cellSize, List<Vector3> path, Vector3 p, out int seg, out float t, out float d)
    {
        seg = -1; t = 0f; d = float.MaxValue;
        long key = ((long)Mathf.FloorToInt(p.x / cellSize) << 32) ^ (uint)Mathf.FloorToInt(p.z / cellSize);
        if (!cells.TryGetValue(key, out var list)) return false;
        foreach (int i in list)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z), b = new Vector2(path[i + 1].x, path[i + 1].z), q = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float k = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            float dist = Vector2.Distance(a + ab * k, q);
            if (dist < d) { d = dist; seg = i; t = k; }
        }
        return seg >= 0;
    }

    // ---------------- trees and plants ---------------- //

    static void TakeAwayAndSnap(Terrain terrain, List<River> rivers, List<Plan> plans, StringBuilder report)
    {
        float Nearest(Vector3 p, out int which)
        {
            which = -1;
            float best = 999f;
            for (int k = 0; k < rivers.Count; k++)
            {
                float d = rivers[k].DistanceToWater(p);
                if (d < best) { best = d; which = k; }
            }
            return best;
        }
        int moved = 0, items = 0;
        foreach (var t in TreeRoots())
        {
            if (t == null) continue;
            float d = Nearest(t.position, out int k);
            if (d < 2.6f) { Object.DestroyImmediate(t.gameObject); plans[k].treesTaken++; }
        }
        foreach (var plant in Object.FindObjectsOfType<ScenePlant>())
        {
            float d = Nearest(plant.transform.position, out int k);
            if (d < 0.9f) { Object.DestroyImmediate(plant.gameObject); plans[k].plantsTaken++; }
        }
        // things to pick up lying in the water (stones...): away too
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (manager != null && manager.allItems != null)
            foreach (var pick in manager.allItems.GetComponentsInChildren<InteractableObject>(true))
                if (pick != null && Nearest(pick.transform.position, out _) < 0.3f) { Object.DestroyImmediate(pick.gameObject); items++; }

        // what stands near the new water: on the new ground
        void Snap(Transform t)
        {
            Vector3 p = t.position;
            if (Nearest(p, out _) > 22f) return;
            float g = Ground(terrain, p);
            if (Mathf.Abs(g - p.y) < 0.03f || Mathf.Abs(g - p.y) > 4.5f) return;
            t.position = new Vector3(p.x, g - 0.03f, p.z);
            moved++;
        }
        foreach (var plant in Object.FindObjectsOfType<ScenePlant>()) Snap(plant.transform);
        foreach (var t in TreeRoots()) if (t != null) Snap(t);
        report.AppendLine("put back on the new ground: " + moved + ", things to pick up taken out of the water: " + items);
    }

    // ---------------- the water: 5 vertices across, in world space (like the old river's) ---------------- //

    static Mesh WaterMesh(List<Vector3> points, List<float> widths, float[] lengths, float total)
    {
        const int across = 5;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color>();
        var tri = new List<int>();
        int count = points.Count;
        float mouthStart = total - 14f;
        for (int i = 0; i < count; i++)
        {
            Vector3 side = Side(points, i);
            int a0 = Mathf.Max(0, i - 5), a1 = Mathf.Min(count - 1, i + 5);
            float speed = Mathf.Clamp01((points[a0].y - points[a1].y) / Mathf.Max(0.1f, lengths[a1] - lengths[a0]) * 14f);
            float fade = Mathf.Clamp01(lengths[i] / 5f) * (lengths[i] > mouthStart ? 1f - Mathf.Clamp01((lengths[i] - mouthStart) / 14f) : 1f);
            for (int k = 0; k < across; k++)
            {
                float u = (float)k / (across - 1);
                v.Add(points[i] + side * widths[i] * (u * 2f - 1f));
                uv.Add(new Vector2(u, lengths[i] / 8f));
                float edge = k == 0 || k == across - 1 ? 0.85f : 1f;
                col.Add(new Color(speed, 0f, 0f, fade * edge));
            }
            if (i == 0) continue;
            int a = (i - 1) * across, b = i * across;
            for (int k = 0; k < across - 1; k++)
            {
                tri.Add(a + k); tri.Add(b + k); tri.Add(a + k + 1);
                tri.Add(a + k + 1); tri.Add(b + k); tri.Add(b + k + 1);
            }
        }
        var mesh = new Mesh { name = "River water" };
        mesh.indexFormat = v.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    class Heap
    {
        readonly List<float> keys = new List<float>();
        readonly List<int> values = new List<int>();
        public int Count => keys.Count;

        public void Push(float key, int value)
        {
            keys.Add(key); values.Add(value);
            int i = keys.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) / 2;
                if (keys[p] <= keys[i]) break;
                Swap(i, p); i = p;
            }
        }

        public void Pop(out float key, out int value)
        {
            key = keys[0]; value = values[0];
            int last = keys.Count - 1;
            keys[0] = keys[last]; values[0] = values[last];
            keys.RemoveAt(last); values.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1, r = l + 1, m = i;
                if (l < keys.Count && keys[l] < keys[m]) m = l;
                if (r < keys.Count && keys[r] < keys[m]) m = r;
                if (m == i) break;
                Swap(i, m); i = m;
            }
        }

        void Swap(int a, int b)
        {
            float k = keys[a]; keys[a] = keys[b]; keys[b] = k;
            int v = values[a]; values[a] = values[b]; values[b] = v;
        }
    }
}
