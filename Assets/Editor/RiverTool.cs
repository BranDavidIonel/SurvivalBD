using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 2. Make River (From The Mountain To The Sea)
// Finds the way water would take from the top of the mountain down to the sea (downhill, winding through the plain,
// around the lake), digs the river bed into the island's terrain (IslandTerrain.asset: the old terrain is in git),
// paints its bed with stones and sand, takes the grass out of the water, and makes the water surface (a River object
// in the scene with the River script: the only fresh water of the island).
public static class RiverTool
{
    const string Dir = "Assets/Island/River";

    [MenuItem("Tools/Nature/2. Make River (From The Mountain To The Lake)")]
    static void MakeFromMenu()
    {
        if (Object.FindObjectOfType<River>() != null &&
            !EditorUtility.DisplayDialog("River", "There is a river already. Make another one (the terrain is dug again)?", "Yes", "No")) return;
        Make();
    }

    public static River Make()
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        if (terrain == null || ocean == null) { Debug.LogWarning("River: no terrain or ocean"); return null; }
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean.Level;
        Directory.CreateDirectory(Dir);

        // ---- the way down: the cheapest path on an 8 m grid, uphill is very expensive ----
        const float cell = 8f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        var height = new float[n * n];
        var blocked = new bool[n * n];
        var sea = new bool[n * n];   // here: the lake, where the river ends
        Bounds lakeWater = LakeBounds(false);
        if (lakeWater.size.x <= 0f) { Debug.LogWarning("River: no lake (WaterVolume) found"); return null; }
        Vector3 lakeCentre = lakeWater.center;
        float lakeRadius = Mathf.Min(lakeWater.extents.x, lakeWater.extents.z);
        float lakeLevel = lakeWater.max.y;
        var objects = ExistingObjects();
        int source = -1;
        float best = -1e9f;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                Vector3 p = tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);
                float h = terrain.SampleHeight(p) + tp.y;
                height[i] = h;
                if (h < level + 0.4f || ocean.DistanceFromShore(p) > -30f) blocked[i] = true; // not to the sea
                sea[i] = Flat(p - lakeCentre) < lakeRadius * 0.75f;
                if (sea[i]) blocked[i] = false;
                if (x < 3 || z < 3 || x > n - 4 || z > n - 4) continue;
                if (h > best) { best = h; source = i; }
            }
        if (source < 0) return null;

        var cost = new float[n * n];
        var from = new int[n * n];
        for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; from[i] = -1; }
        var heap = new MinHeap();
        cost[source] = 0f;
        heap.Push(0f, source);
        int target = -1;
        while (heap.Count > 0)
        {
            heap.Pop(out float c, out int i);
            if (c > cost[i]) continue;
            if (sea[i]) { target = i; break; }
            int x = i % n, z = i / n;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
                    int j = nz * n + nx;
                    if (blocked[j]) continue;
                    float len = (dx != 0 && dz != 0 ? 1.4142f : 1f) * cell;
                    float wx = tp.x + (nx + 0.5f) * cell, wz = tp.z + (nz + 0.5f) * cell;
                    float wind = Mathf.PerlinNoise(wx * 0.011f + 13.1f, wz * 0.011f + 7.7f);
                    float step = len * (1f + 1.1f * wind) + Mathf.Max(0f, height[j] - height[i]) * 90f;
                    if (NearObject(objects, new Vector3(wx, 0f, wz), 12f)) step += 150f;
                    float total = c + step;
                    if (total < cost[j]) { cost[j] = total; from[j] = i; heap.Push(total, j); }
                }
        }
        if (target < 0) { Debug.LogWarning("River: no way to the lake found"); return null; }

        var cells = new List<Vector3>();
        for (int i = target; i >= 0; i = from[i])
            cells.Add(tp + new Vector3((i % n + 0.5f) * cell, 0f, (i / n + 0.5f) * cell));
        cells.Reverse();
        // a bit further into the lake for the mouth
        Vector3 outDir = (cells[cells.Count - 1] - cells[Mathf.Max(0, cells.Count - 4)]).normalized;
        cells.Add(cells[cells.Count - 1] + outDir * 8f);

        // ---- smooth it, every 3 m, winding where the land is flat ----
        var path = cells;
        for (int k = 0; k < 4; k++) path = Chaikin(path);
        path = Resample(path, 3f);
        path = Meander(terrain, path, lakeCentre, lakeRadius);
        for (int k = 0; k < 2; k++) path = Chaikin(path);
        path = Resample(path, 3f);

        // ---- width, depth, the water's surface (always going down) ----
        int count = path.Count;
        var lengths = new float[count];
        for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Flat(path[i] - path[i - 1]);
        float total2 = Mathf.Max(1f, lengths[count - 1]);
        var hw = new float[count];
        var depth = new float[count];
        var surface = new float[count];
        for (int i = 0; i < count; i++)
        {
            float t = lengths[i] / total2;
            hw[i] = Mathf.Lerp(1.5f, 5.2f, Mathf.Pow(t, 0.6f));
            depth[i] = Mathf.Lerp(0.8f, 1.7f, t);
            Vector3 side = Side(path, i);
            float low = Mathf.Min(Ground(terrain, path[i]), Mathf.Min(Ground(terrain, path[i] + side * hw[i] * 1.3f), Ground(terrain, path[i] - side * hw[i] * 1.3f)));
            float s = low - 0.3f;
            if (i > 0) s = Mathf.Min(s, surface[i - 1] - 0.0015f * (lengths[i] - lengths[i - 1]));
            surface[i] = Mathf.Max(s, lakeLevel + 0.04f);
        }
        for (int i = 1; i < count; i++) surface[i] = Mathf.Min(surface[i], surface[i - 1]);

        Carve(terrain, path, hw, depth, surface, lakeLevel, lakeCentre, lakeRadius);

        // ---- the water ----
        var points = new List<Vector3>();
        var widths = new List<float>();
        for (int i = 0; i < count; i++) { points.Add(new Vector3(path[i].x, surface[i], path[i].z)); widths.Add(hw[i]); }
        var mesh = WaterMesh(points, widths, lengths, total2);
        string meshPath = Dir + "/RiverWater.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var material = WaterMaterial();

        var old = Object.FindObjectOfType<River>();
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var go = new GameObject("River");
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (manager != null) go.transform.SetParent(manager.transform, false);
        go.transform.position = Vector3.zero;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var river = go.AddComponent<River>();
        river.points = points;
        river.halfWidths = widths;
        river.lakeCentre = new Vector3(lakeCentre.x, lakeLevel, lakeCentre.z);
        river.lakeRadius = lakeRadius;
        river.BuildCells();
        Undo.RegisterCreatedObjectUndo(go, "Make River");
        EditorSceneManager.MarkSceneDirty(go.scene);
        Debug.Log("River: " + Mathf.RoundToInt(total2) + " m from " + points[0] + " (height " + points[0].y.ToString("F0") + ") to the sea at " + points[count - 1]);
        return river;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // Rivers on flat land wind left and right: the path gets a gentle wave where the ground is flat (none near
    // the spring, on the steep mountain and at the lake).
    static List<Vector3> Meander(Terrain terrain, List<Vector3> path, Vector3 lakeCentre, float lakeRadius)
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
            float ends = Mathf.SmoothStep(0f, 1f, lengths[i] / 90f) * Mathf.SmoothStep(0f, 1f, (total - lengths[i]) / 70f);
            float nearLake = Mathf.Clamp01((Flat(path[i] - lakeCentre) - lakeRadius - 10f) / 40f);
            float amplitude = 11f * flat * ends * nearLake;
            float wave = 0.7f * Mathf.Sin(lengths[i] / 42f + 1.3f) + 0.3f * Mathf.Sin(lengths[i] / 17f + 0.4f);
            result.Add(path[i] + Side(path, i) * amplitude * wave);
        }
        return result;
    }

    static float Ground(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;

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

    // the lake's water (expand: with a margin around it)
    public static Bounds LakeBounds(bool expand = true)
    {
        bool any = false;
        var b = new Bounds();
        foreach (var t in Object.FindObjectsOfType<Transform>())
        {
            if (!t.name.StartsWith("WaterVolume") && t.name != "WaterBox") continue;
            foreach (var r in t.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            foreach (var c in t.GetComponentsInChildren<Collider>())
            {
                if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
            }
        }
        if (!any) return new Bounds(Vector3.zero, Vector3.zero);
        if (expand) b.Expand(new Vector3(24f, 200f, 24f));
        return b;
    }

    // trees and things already in the scene (the river goes around them)
    public static List<Vector3> ExistingObjects()
    {
        var list = new List<Vector3>();
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (manager == null) return list;
        foreach (var root in new[] { manager.allTrees, manager.allItems, manager.placeables })
        {
            if (root == null) continue;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (r.GetComponentInParent<NatureGroup>() != null) continue; // (made by the island tool)
                list.Add(r.transform.position);
            }
        }
        return list;
    }

    static bool NearObject(List<Vector3> objects, Vector3 p, float radius)
    {
        foreach (var o in objects)
        {
            float dx = o.x - p.x, dz = o.z - p.z;
            if (dx * dx + dz * dz < radius * radius) return true;
        }
        return false;
    }

    // ---- digging the bed ----

    static void Carve(Terrain terrain, List<Vector3> path, float[] hw, float[] depth, float[] surface, float level, Vector3 lakeCentre, float lakeRadius)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int res = data.heightmapResolution;
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1), sy = data.size.y;
        var heights = data.GetHeights(0, 0, res, res);

        // segments sorted into squares
        const float cellSize = 24f;
        var cells = new Dictionary<long, List<int>>();
        float maxReach = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            float reach = Mathf.Max(hw[i], hw[i + 1]) * 2f + 24f; // (the raised land can reach far)
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

        // the area to look at
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in path) { minX = Mathf.Min(minX, p.x); maxX = Mathf.Max(maxX, p.x); minZ = Mathf.Min(minZ, p.z); maxZ = Mathf.Max(maxZ, p.z); }
        int xs = Mathf.Clamp(Mathf.FloorToInt((minX - maxReach - tp.x) / sx), 0, res - 1), xe = Mathf.Clamp(Mathf.CeilToInt((maxX + maxReach - tp.x) / sx), 0, res - 1);
        int zs = Mathf.Clamp(Mathf.FloorToInt((minZ - maxReach - tp.z) / sz), 0, res - 1), ze = Mathf.Clamp(Mathf.CeilToInt((maxZ + maxReach - tp.z) / sz), 0, res - 1);

        // how much of each terrain sample is river bed (for the paint and the grass)
        var bedAmount = new Dictionary<long, float>();
        for (int z = zs; z <= ze; z++)
            for (int x = xs; x <= xe; x++)
            {
                Vector3 w = new Vector3(tp.x + x * sx, 0f, tp.z + z * sz);
                if (!Nearest(cells, cellSize, path, w, out int seg, out float t, out float d)) continue;
                float h = Mathf.Lerp(hw[seg], hw[seg + 1], t);
                float s = Mathf.Lerp(surface[seg], surface[seg + 1], t);
                float dep = Mathf.Lerp(depth[seg], depth[seg + 1], t);
                float bank = 5f + h;
                // The bed lies in a hollow: the water's edge (h) is buried in a bank that rises above the water,
                // so the water never hangs over lower ground (the terrain has a point only every ~2 m).
                //   bed in the middle -> a shelf 0.35 m under the water 1 m inside the edge -> the bank's top
                //   0.6 m over the water 0.8 m outside the edge -> back to the land around
                float inner = Mathf.Max(0.3f, h - 1f), outer = h + 0.8f;
                float shelf = s - 0.35f, top = s + 0.6f;
                float orig = heights[z, x] * sy + tp.y;
                // Where the land is lower than the water (near the lake the water stays at the lake's height), the
                // land along the river is raised: the wider, the more it has to rise (a gentle slope, not a wall).
                bank += Mathf.Clamp(top - orig, 0f, 4f) * 4f;
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
                    if (d > h) target = Mathf.Max(target, Mathf.Min(orig, top + 1.5f)); // a high bank stays high
                }
                else
                {
                    float f = Mathf.SmoothStep(0f, 1f, (d - outer) / bank);
                    target = Mathf.Lerp(top, orig, f); // (a low bank is raised)
                }
                // in the lake itself nothing is raised (no dams in the lake)
                if (lakeRadius > 0f && Flat(w - lakeCentre) < lakeRadius - 1f) target = Mathf.Min(target, orig);
                heights[z, x] = Mathf.Clamp01((target - tp.y) / sy);
                bedAmount[((long)x << 32) | (uint)z] = d <= h + 1f ? 1f : 1f - Mathf.Clamp01((d - h - 1f) / 3f);
            }
        Undo.RegisterCompleteObjectUndo(data, "River bed");
        data.SetHeights(0, 0, heights);

        // paint: stones and sand in the bed
        int stone = -1, sand = -1;
        var layers = data.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string name = layers[i].name.ToLowerInvariant();
            if (stone < 0 && name.Contains("rock")) stone = i;
            if (name.Contains("sand")) sand = i;
        }
        if (stone >= 0)
        {
            int ar = data.alphamapResolution;
            var maps = data.GetAlphamaps(0, 0, ar, ar);
            int layerCount = maps.GetLength(2);
            foreach (var kv in bedAmount)
            {
                int x = (int)(kv.Key >> 32), z = (int)(uint)kv.Key;
                int ax = Mathf.Clamp(Mathf.RoundToInt((float)x / (res - 1) * (ar - 1)), 0, ar - 1);
                int az = Mathf.Clamp(Mathf.RoundToInt((float)z / (res - 1) * (ar - 1)), 0, ar - 1);
                float a = kv.Value;
                for (int l = 0; l < layerCount; l++) maps[az, ax, l] *= 1f - a;
                maps[az, ax, stone] += a * (sand >= 0 ? 0.65f : 1f);
                if (sand >= 0) maps[az, ax, sand] += a * 0.35f;
            }
            data.SetAlphamaps(0, 0, maps);
        }

        // no grass in the water
        int dr = data.detailResolution;
        int detailLayers = data.detailPrototypes.Length;
        if (dr > 0 && detailLayers > 0)
        {
            int dxs = Mathf.FloorToInt((float)xs / (res - 1) * dr), dxe = Mathf.Min(dr - 1, Mathf.CeilToInt((float)xe / (res - 1) * dr));
            int dzs = Mathf.FloorToInt((float)zs / (res - 1) * dr), dze = Mathf.Min(dr - 1, Mathf.CeilToInt((float)ze / (res - 1) * dr));
            int w = dxe - dxs + 1, h = dze - dzs + 1;
            for (int l = 0; l < detailLayers; l++)
            {
                var layer = data.GetDetailLayer(dxs, dzs, w, h, l);
                for (int z = 0; z < h; z++)
                    for (int x = 0; x < w; x++)
                    {
                        Vector3 p = new Vector3(tp.x + (dxs + x + 0.5f) / dr * data.size.x, 0f, tp.z + (dzs + z + 0.5f) / dr * data.size.z);
                        if (!Nearest(cells, cellSize, path, p, out int seg, out float t, out float d)) continue;
                        if (d < Mathf.Lerp(hw[seg], hw[seg + 1], t) + 0.6f) layer[z, x] = 0;
                    }
                data.SetDetailLayer(dxs, dzs, l, layer);
            }
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
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

    [MenuItem("Tools/Nature/2d. Dig The River Bed Again (deeper)")]
    static void RecarveFromMenu()
    {
        var river = Object.FindObjectOfType<River>();
        if (river == null) { Debug.LogWarning("River: there is no river in the scene"); return; }
        Debug.Log("River: bed dug again, " + Recarve(river) + " plants and trees put back on the ground");
    }

    // digs the bed of the river that is in the scene again (the water stays where it is): a deeper hollow with
    // banks over the water's edge; the plants and trees near it are put back on the new ground. Returns how many.
    public static int Recarve(River river)
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        int count = river.points.Count;
        if (terrain == null || count < 2) return 0;
        var path = new List<Vector3>(river.points);
        var lengths = new float[count];
        for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Flat(path[i] - path[i - 1]);
        float total = Mathf.Max(1f, lengths[count - 1]);
        var hw = new float[count];
        var depth = new float[count];
        var surface = new float[count];
        for (int i = 0; i < count; i++)
        {
            hw[i] = river.halfWidths[i];
            depth[i] = Mathf.Lerp(0.8f, 1.7f, lengths[i] / total);
            surface[i] = path[i].y;
        }
        float lakeLevel = river.lakeCentre.y;
        Carve(terrain, path, hw, depth, surface, lakeLevel, river.lakeCentre, river.lakeRadius);
        RebuildWater(river);

        // what stands near the river: back on the ground
        river.BuildCells();
        int moved = 0;
        void Snap(Transform t)
        {
            Vector3 p = t.position;
            if (river.DistanceToWater(p) > 26f) return;
            float g = terrain.SampleHeight(p) + terrain.transform.position.y;
            if (Mathf.Abs(g - p.y) < 0.03f || Mathf.Abs(g - p.y) > 4.5f) return;
            Undo.RecordObject(t, "River bed");
            t.position = new Vector3(p.x, g - 0.03f, p.z);
            moved++;
        }
        foreach (var plant in Object.FindObjectsOfType<ScenePlant>()) Snap(plant.transform);
        foreach (var tree in Object.FindObjectsOfType<SceneTree>()) Snap(tree.transform);
        foreach (var pine in Object.FindObjectsOfType<ScenePine>()) Snap(pine.transform);
        EditorSceneManager.MarkSceneDirty(river.gameObject.scene);
        return moved;
    }

    // ---- the water surface: 5 vertices across, in world space ----

    // makes the water surface again from the River's points (after changing how it looks)
    public static void RebuildWater(River river)
    {
        int count = river.points.Count;
        var lengths = new float[count];
        for (int i = 1; i < count; i++) lengths[i] = lengths[i - 1] + Flat(river.points[i] - river.points[i - 1]);
        var mesh = WaterMesh(river.points, river.halfWidths, lengths, lengths[count - 1]);
        string meshPath = Dir + "/RiverWater.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        river.GetComponent<MeshFilter>().sharedMesh = mesh;
        river.transform.position = Vector3.zero;
        EditorUtility.SetDirty(river.gameObject);
    }

    static Mesh WaterMesh(List<Vector3> points, List<float> widths, float[] lengths, float total)
    {
        const int across = 5;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color>();
        var tri = new List<int>();
        int count = points.Count;
        float mouthStart = total - 12f;
        for (int i = 0; i < count; i++)
        {
            Vector3 side = Side(points, i);
            // how fast it flows: the slope of the water over ~30 m (smooth, no stripes)
            int a0 = Mathf.Max(0, i - 5), a1 = Mathf.Min(count - 1, i + 5);
            float speed = Mathf.Clamp01((points[a0].y - points[a1].y) / Mathf.Max(0.1f, lengths[a1] - lengths[a0]) * 14f);
            float fade = Mathf.Clamp01(lengths[i] / 6f) * (lengths[i] > mouthStart ? 1f - Mathf.Clamp01((lengths[i] - mouthStart) / 12f) : 1f);
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

    static Material WaterMaterial()
    {
        string noisePath = Dir + "/RiverNoise.png";
        if (!File.Exists(noisePath))
        {
            File.WriteAllBytes(noisePath, TileableNoise(256).EncodeToPNG());
            AssetDatabase.ImportAsset(noisePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(noisePath);
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.sRGBTexture = false;
            importer.SaveAndReimport();
        }
        string matPath = Dir + "/River.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (m == null)
        {
            m = new Material(Shader.Find("SurvivalBD/River")) { name = "River" };
            AssetDatabase.CreateAsset(m, matPath);
        }
        m.SetTexture("_Noise", AssetDatabase.LoadAssetAtPath<Texture2D>(noisePath));
        EditorUtility.SetDirty(m);
        return m;
    }

    // value noise that repeats seamlessly, in two channels
    static Texture2D TileableNoise(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var rng = new System.Random(5);
        var lattice = new float[2, 64, 64];
        for (int c = 0; c < 2; c++) for (int i = 0; i < 64; i++) for (int j = 0; j < 64; j++) lattice[c, i, j] = (float)rng.NextDouble();
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float[] v = new float[2];
                for (int c = 0; c < 2; c++)
                {
                    float sum = 0f, amp = 0.5f;
                    for (int o = 0; o < 4; o++)
                    {
                        int period = 4 << o;
                        float fx = (float)x / size * period, fy = (float)y / size * period;
                        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
                        float tx = Mathf.SmoothStep(0f, 1f, fx - x0), ty = Mathf.SmoothStep(0f, 1f, fy - y0);
                        float a = lattice[c, x0 % period, y0 % period], b = lattice[c, (x0 + 1) % period, y0 % period];
                        float d = lattice[c, x0 % period, (y0 + 1) % period], e = lattice[c, (x0 + 1) % period, (y0 + 1) % period];
                        sum += Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(d, e, tx), ty) * amp;
                        amp *= 0.5f;
                    }
                    v[c] = sum / 0.9375f;
                }
                tex.SetPixel(x, y, new Color(v[0], v[1], 0.5f, 1f));
            }
        tex.Apply();
        return tex;
    }

    class MinHeap
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
