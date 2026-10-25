using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 2b. Make Cave In The Mountain
// A long cave that goes down into the mountain (story: Level 5):
//  - its mouth on a slope of the mountain, away from the river; a tunnel of ~200 m that winds and goes down ~30 m,
//    ending in a small hall
//  - the terrain is raised where the mountain is too thin above the tunnel, and opened (terrain holes) at the mouth
//  - rocks around the mouth, and a fall of rocks that closes it until Level 5
//  - at the end: the bones of the old monk, an Orthodox wooden cross, candles, his journal and his prayer book;
//    mushrooms (and glowing ones) along the way
// The old terrain is in git (Assets/Island/IslandTerrain.asset); making the cave again digs again.
public static class CaveTool
{
    const string Dir = "Assets/Island/Cave";
    const float Step = 4f;
    // the player is a tall capsule (3.7 m): the tunnel is wide and high, so a phone player walks in without bumping
    const float RadiusWide = 5.6f, RadiusHigh = 4.6f, FloorBelow = 1.6f;

    [MenuItem("Tools/Nature/2c. Make The Cave Wider")]
    static void WidenFromMenu()
    {
        var cave = Object.FindObjectOfType<Cave>();
        if (cave == null) { Debug.LogWarning("Cave: there is no cave in the scene"); return; }
        Widen(cave);
    }

    [MenuItem("Tools/Nature/2b. Make Cave In The Mountain")]
    static void MakeFromMenu()
    {
        if (!EditorUtility.DisplayDialog("Cave", "Dig a cave into the mountain? (the terrain changes; the old one is in git)", "Dig", "Cancel")) return;
        Make(20261025);
    }

    public static Cave Make(int seed)
    {
        var terrain = Terrain.activeTerrain;
        var ocean = Object.FindObjectOfType<Ocean>();
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (terrain == null || ocean == null || manager == null) return null;
        Directory.CreateDirectory(Dir);
        MakeBookPrefabs();
        foreach (var old in Object.FindObjectsOfType<Cave>()) Undo.DestroyObjectImmediate(old.gameObject);
        var river = Object.FindObjectOfType<River>();
        if (river != null) river.BuildCells();
        float level = ocean.Level;
        var rng = new System.Random(seed);

        // ---- the mouth: a slope of the mountain that gets thicker inward ----
        Vector3 mouth = Vector3.zero, inward = Vector3.forward;
        float bestScore = float.MinValue;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        for (float z = tp.z + 40f; z < tp.z + data.size.z - 40f; z += 6f)
            for (float x = tp.x + 40f; x < tp.x + data.size.x - 40f; x += 6f)
            {
                Vector3 p = new Vector3(x, 0f, z);
                float h = Ground(terrain, p);
                if (h < level + 30f || h > level + 105f) continue;
                if (ocean.DistanceFromShore(p) > -120f) continue;
                float steep = data.GetSteepness((x - tp.x) / data.size.x, (z - tp.z) / data.size.z);
                if (steep < 16f || steep > 40f) continue;
                if (river != null && river.DistanceToWater(p) < 90f) continue;
                Vector3 up = Uphill(terrain, p);
                if (up == Vector3.zero) continue;
                float h30 = Ground(terrain, p + up * 30f), h60 = Ground(terrain, p + up * 60f), h100 = Ground(terrain, p + up * 100f);
                if (h30 < h + 8f || h60 < h + 14f) continue;
                if (river != null && (river.DistanceToWater(p + up * 60f) < 60f || river.DistanceToWater(p + up * 120f) < 60f)) continue;
                float score = Mathf.Min(h60 - h, 45f) + Mathf.Min(h100 - h, 60f) * 0.5f - Mathf.Abs(h - (level + 55f)) * 0.3f + (float)rng.NextDouble() * 3f;
                if (score > bestScore) { bestScore = score; mouth = new Vector3(x, h, z); inward = up; }
            }
        if (bestScore == float.MinValue) { Debug.LogWarning("Cave: no good slope found"); return null; }

        // ---- the tunnel: winds and goes down ----
        var path = new List<Vector3>();
        float centreAbove = FloorBelow - 0.25f; // the floor a little below the ground at the mouth
        Vector3 start = mouth - inward * 4f + Vector3.up * centreAbove;
        path.Add(start);
        Vector3 heading = inward;
        Vector3 p0 = start;
        float noiseSeed = (float)rng.NextDouble() * 100f;
        for (int i = 1; i <= 54; i++)
        {
            if (i > 4)
            {
                // wander, pulled towards the inside of the mountain
                float turn = (Mathf.PerlinNoise(noiseSeed, i * 0.23f) - 0.5f) * 70f;
                Vector3 up = Uphill(terrain, p0);
                if (up != Vector3.zero) heading = Vector3.Slerp(heading, up, 0.18f);
                heading = Quaternion.Euler(0f, turn * 0.5f, 0f) * heading;
                // do not come back near the tunnel's own earlier parts
                for (int k = 0; k < 6; k++)
                {
                    Vector3 test = p0 + heading * Step;
                    bool close = false;
                    for (int j = 0; j < path.Count - 4; j++) if (Flat(path[j] - test) < 14f) { close = true; break; }
                    if (river != null && river.DistanceToWater(test) < 35f) close = true;
                    if (!close) break;
                    heading = Quaternion.Euler(0f, k % 2 == 0 ? 35f : -70f, 0f) * heading;
                }
                heading.y = 0f;
                heading.Normalize();
            }
            float drop = i <= 4 ? 0f : i > 48 ? 0.1f : 0.62f;
            p0 = p0 + heading * Step + Vector3.down * drop;
            path.Add(p0);
        }

        // ---- the mountain above it: thick enough everywhere (raised where it is not) ----
        RaiseCover(terrain, path, 5);
        // ---- the mouth: holes in the terrain where the tunnel comes out ----
        CutMouth(terrain, path);

        // ---- the rock of the tunnel ----
        var mesh = TunnelMesh(path, rng);
        string meshPath = Dir + "/CaveTunnel.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var rock = RockMaterial(terrain);

        var root = new GameObject("Monk's Cave");
        root.transform.SetParent(manager.transform, false);
        // the tunnel's mesh is in world space: the cave object sits at the world's origin (Enviroment is not there)
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = rock;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
        root.AddComponent<MeshCollider>().sharedMesh = mesh;
        var cave = root.AddComponent<Cave>();
        cave.path = path;
        cave.radius = RadiusWide;
        Undo.RegisterCreatedObjectUndo(root, "Cave");

        MouthRocks(root.transform, terrain, path, rng);
        cave.rockfall = Rockfall(root.transform, path, rng);
        cave.end = MonksPlace(root.transform, path, rng);
        CaveMushrooms(root.transform, path, rng);
        AddFormations(cave);
        AddOres(cave, 50);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("Cave: mouth at " + mouth + ", " + path.Count * Step + " m long, ends at " + path[path.Count - 1]);
        return cave;
    }

    static float Ground(Terrain t, Vector3 p) => t.SampleHeight(p) + t.transform.position.y;
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    static Vector3 Uphill(Terrain t, Vector3 p)
    {
        float dx = Ground(t, p + Vector3.right * 4f) - Ground(t, p - Vector3.right * 4f);
        float dz = Ground(t, p + Vector3.forward * 4f) - Ground(t, p - Vector3.forward * 4f);
        var g = new Vector3(dx, 0f, dz);
        return g.sqrMagnitude < 0.0001f ? Vector3.zero : g.normalized;
    }

    static Vector3 Side(List<Vector3> path, int i)
    {
        Vector3 a = path[Mathf.Max(0, i - 1)], b = path[Mathf.Min(path.Count - 1, i + 1)];
        Vector3 d = b - a; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        d.Normalize();
        return new Vector3(d.z, 0f, -d.x);
    }

    static Vector3 Along(List<Vector3> path, int i)
    {
        Vector3 a = path[Mathf.Max(0, i - 1)], b = path[Mathf.Min(path.Count - 1, i + 1)];
        Vector3 d = b - a; d.y = 0f;
        return d.sqrMagnitude < 0.0001f ? Vector3.forward : d.normalized;
    }

    // ---- terrain ----

    static void RaiseCover(Terrain terrain, List<Vector3> path, int skip)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int res = data.heightmapResolution;
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1), sy = data.size.y;
        var heights = data.GetHeights(0, 0, res, res);
        const float reach = 20f, cover = 7f;
        bool changed = false;
        for (int i = skip; i < path.Count; i++)
        {
            float ceiling = path[i].y + RadiusHigh * (i > path.Count - 6 ? 1.6f : 1f);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((path[i].x - reach - tp.x) / sx)), x1 = Mathf.Min(res - 1, Mathf.CeilToInt((path[i].x + reach - tp.x) / sx));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((path[i].z - reach - tp.z) / sz)), z1 = Mathf.Min(res - 1, Mathf.CeilToInt((path[i].z + reach - tp.z) / sz));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 w = new Vector3(tp.x + x * sx, 0f, tp.z + z * sz);
                    float d = Flat(w - path[i]);
                    if (d > reach) continue;
                    float h = heights[z, x] * sy + tp.y;
                    float need = ceiling + cover - h;
                    if (need <= 0f) continue;
                    float k = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01((d - RadiusWide * 2f) / (reach - RadiusWide * 2f)));
                    float raised = h + need * k;
                    if (raised > h) { heights[z, x] = Mathf.Clamp01((raised - tp.y) / sy); changed = true; }
                }
        }
        if (changed)
        {
            Undo.RegisterCompleteObjectUndo(data, "Cave cover");
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
        }
    }

    // terrain holes where the tunnel passes through the slope (the terrain's surface between its floor and roof)
    static void CutMouth(Terrain terrain, List<Vector3> path)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int hr = data.holesResolution;
        float cx = data.size.x / hr, cz = data.size.z / hr;
        var holes = data.GetHoles(0, 0, hr, hr);
        int cut = 0;
        for (int i = 0; i < Mathf.Min(path.Count, 8); i++)
        {
            Vector3 c = path[i];
            float floor = c.y - FloorBelow, roof = c.y + RadiusHigh;
            float scan = RadiusWide * 1.7f;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - scan - tp.x) / cx)), x1 = Mathf.Min(hr - 1, Mathf.CeilToInt((c.x + scan - tp.x) / cx));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - scan - tp.z) / cz)), z1 = Mathf.Min(hr - 1, Mathf.CeilToInt((c.z + scan - tp.z) / cz));
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 w = new Vector3(tp.x + (x + 0.5f) * cx, 0f, tp.z + (z + 0.5f) * cz);
                    // only inside the tunnel's shape: its width at the height of the ground there
                    float h = Ground(terrain, w);
                    float y = h - c.y;
                    if (y < -FloorBelow + 0.3f || y > RadiusHigh + 0.4f) continue;
                    // not in front of the mouth (the tunnel starts there)
                    Vector2 a0 = new Vector2(path[0].x, path[0].z), b0 = new Vector2(path[1].x, path[1].z), q0 = new Vector2(w.x, w.z);
                    if (Vector2.Dot(q0 - a0, b0 - a0) < 0f) continue;
                    float k = Mathf.Clamp(y, -RadiusHigh * 0.99f, RadiusHigh * 0.99f) / RadiusHigh;
                    float halfWidth = RadiusWide * Mathf.Sqrt(1f - k * k);
                    if (DistanceToSegments(path, 0, Mathf.Min(path.Count - 1, 8), w) > halfWidth * 0.92f) continue;
                    if (holes[z, x]) { holes[z, x] = false; cut++; }
                }
        }
        if (cut > 0)
        {
            Undo.RegisterCompleteObjectUndo(data, "Cave mouth");
            data.SetHoles(0, 0, holes);
            EditorUtility.SetDirty(data);
        }
        Debug.Log("Cave: " + cut + " terrain holes at the mouth");
    }

    static float DistanceToSegments(List<Vector3> path, int from, int to, Vector3 p)
    {
        float best = float.MaxValue;
        for (int i = from; i < to; i++)
        {
            Vector2 a = new Vector2(path[i].x, path[i].z), b = new Vector2(path[i + 1].x, path[i + 1].z), q = new Vector2(p.x, p.z);
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude) : 0f;
            best = Mathf.Min(best, Vector2.Distance(a + ab * t, q));
        }
        return best;
    }

    // ---- the tunnel mesh: rings of rough rock, a flat floor, a hall at the end, inside faces ----

    static float Scale(int i, int count)
    {
        if (i == 0) return 1.45f; // the mouth opens wider (its rock covers the edge of the hole in the terrain)
        if (i == 1) return 1.12f;
        int hallStart = count - 7;
        if (i < hallStart) return 1f;
        float t = (float)(i - hallStart) / 6f;
        return 1f + 0.85f * Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.PI * 0.5f) * (i == count - 1 ? 0.25f : 1f);
    }

    static Mesh TunnelMesh(List<Vector3> path, System.Random rng)
    {
        const int around = 20;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color>();
        var tri = new List<int>();
        float along = 0f;
        float seed = (float)rng.NextDouble() * 50f;
        int count = path.Count;
        for (int i = 0; i < count; i++)
        {
            if (i > 0) along += Vector3.Distance(path[i], path[i - 1]);
            Vector3 side = Side(path, i);
            float s = Scale(i, count);
            float daylight = Mathf.Clamp01(1f - (along - 4f) / 14f);
            for (int k = 0; k <= around; k++)
            {
                float a = (float)k / around * Mathf.PI * 2f;
                float n = Mathf.PerlinNoise(path[i].x * 0.21f + a * 1.7f + seed, path[i].z * 0.21f + path[i].y * 0.3f) - 0.5f;
                float rough = k == 0 || k == around ? 0f : n * 0.55f;
                float x = Mathf.Cos(a) * (RadiusWide * s + rough);
                float y = Mathf.Sin(a) * (RadiusHigh * Mathf.Lerp(1f, 1.35f, s - 1f) + rough * 0.8f);
                float floor = -FloorBelow;
                if (y < floor) y = floor + n * 0.08f; // a walkable floor
                v.Add(path[i] + side * x + Vector3.up * y);
                uv.Add(new Vector2((float)k / around * 5f, along / 3.5f));
                col.Add(new Color(daylight, 0f, 0f, 1f));
            }
        }
        for (int i = 0; i + 1 < count; i++)
            for (int k = 0; k < around; k++)
            {
                int a = i * (around + 1) + k, b = a + 1, c = a + around + 1, d = c + 1;
                tri.Add(a); tri.Add(b); tri.Add(c);
                tri.Add(b); tri.Add(d); tri.Add(c);
            }
        // close the end
        int last = (count - 1) * (around + 1);
        int centre = v.Count;
        v.Add(path[count - 1] + Along(path, count - 1) * 1.2f);
        uv.Add(new Vector2(0.5f, along / 3.5f));
        col.Add(new Color(0f, 0f, 0f, 1f));
        for (int k = 0; k < around; k++) { tri.Add(last + k); tri.Add(centre); tri.Add(last + k + 1); }

        var mesh = new Mesh { name = "Cave tunnel" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        // the faces must look inward: the floor's normals point up
        int floorVertex = (count / 2) * (around + 1) + around * 3 / 4;
        if (mesh.normals[floorVertex].y < 0f)
        {
            for (int t = 0; t < tri.Count; t += 3) { int x = tri[t]; tri[t] = tri[t + 1]; tri[t + 1] = x; }
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();
        return mesh;
    }

    static Material RockMaterial(Terrain terrain)
    {
        string path = Dir + "/CaveRock.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("SurvivalBD/Cave")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("SurvivalBD/Cave");
        Texture2D rockTexture = null;
        foreach (var layer in terrain.terrainData.terrainLayers)
            if (layer != null && layer.name.ToLowerInvariant().Contains("rock") && layer.diffuseTexture != null) { rockTexture = layer.diffuseTexture; break; }
        m.mainTexture = rockTexture;
        m.color = new Color(0.56f, 0.56f, 0.54f);
        m.SetFloat("_Ambient", 0.012f); // pitch dark: only a torch lights it
        m.SetFloat("_VertexDaylight", 1f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material PropMaterial(string name, Color color, Color glow)
    {
        string path = Dir + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("SurvivalBD/Cave")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("SurvivalBD/Cave");
        m.color = color;
        m.SetFloat("_Ambient", 0.012f);
        m.SetFloat("_VertexDaylight", 0f);
        m.SetColor("_Glow", glow);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---- rocks ----

    static readonly string[] RockPrefabs =
    {
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Big Rock 1.prefab",
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Big Rock 2.prefab",
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Big Rock 3.prefab",
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Rock 1.prefab",
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Rock 2.prefab",
        "Assets/Low Poly Free Vegetation Kit/Prefabs/Rocks/Rock 3.prefab",
    };

    static GameObject Rock(Transform parent, int kind, Vector3 at, float scale, System.Random rng)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RockPrefabs[Mathf.Clamp(kind, 0, RockPrefabs.Length - 1)]);
        if (prefab == null) return null;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 20f - 10f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f - 10f);
        go.transform.localScale = prefab.transform.localScale * scale;
        if (go.GetComponentInChildren<Collider>() == null)
            foreach (var f in go.GetComponentsInChildren<MeshFilter>()) f.gameObject.AddComponent<MeshCollider>().sharedMesh = f.sharedMesh;
        return go;
    }

    // rocks around the mouth (they hide the rough edge of the hole in the terrain)
    static void MouthRocks(Transform root, Terrain terrain, List<Vector3> path, System.Random rng)
    {
        var group = new GameObject("Mouth rocks").transform;
        group.SetParent(root, false);
        Vector3 c = path[1], side = Side(path, 1), dir = Along(path, 1);
        for (int s = -1; s <= 1; s += 2)
            for (int j = 0; j < 3; j++)
            {
                Vector3 at = c + side * s * (RadiusWide * 1.35f + 0.6f + j * 0.7f) + dir * (j * 2.5f - 2f);
                at.y = Ground(terrain, at) - 0.45f;
                var rock = Rock(group, 3 + rng.Next(3), at, 1.1f + (float)rng.NextDouble() * 0.7f, rng);
                LieOnSlope(rock, terrain, rng);
            }
        // over the mouth
        Vector3 top = path[1] + dir * 1.5f;
        top.y = Ground(terrain, top) - 0.8f;
        if (top.y < path[1].y + RadiusHigh) top.y = path[1].y + RadiusHigh + 0.4f;
        LieOnSlope(Rock(group, rng.Next(3), top, 0.45f, rng), terrain, rng);
        // at the foot of the mouth, on both sides: low stones that hide the rough edge of the ground
        // (none in the middle: the way in stays free)
        float floor = path[0].y - FloorBelow;
        for (int s = -1; s <= 1; s += 2)
        {
            Vector3 at = path[0] + side * s * RadiusWide * 1.05f - dir * 0.4f;
            at.y = floor - 0.45f;
            Rock(group, 3 + rng.Next(3), at, 1.3f, rng);
        }
    }

    // a rock lying on the slope, not standing straight on it
    static void LieOnSlope(GameObject rock, Terrain terrain, System.Random rng)
    {
        if (rock == null) return;
        var data = terrain.terrainData;
        Vector3 local = rock.transform.position - terrain.transform.position;
        Vector3 normal = data.GetInterpolatedNormal(local.x / data.size.x, local.z / data.size.z);
        rock.transform.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
    }

    // makes the mouth again (after the tool was improved): fills the old holes, cuts them again, the tunnel's
    // mesh with its wider mouth, the rocks around it
    public static void FixMouth(Cave cave)
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        int hr = data.holesResolution;
        var holes = data.GetHoles(0, 0, hr, hr);
        float cx = data.size.x / hr, cz = data.size.z / hr;
        Vector3 m = cave.path[2];
        for (int z = Mathf.Max(0, Mathf.FloorToInt((m.z - 40f - tp.z) / cz)); z <= Mathf.Min(hr - 1, Mathf.CeilToInt((m.z + 40f - tp.z) / cz)); z++)
            for (int x = Mathf.Max(0, Mathf.FloorToInt((m.x - 40f - tp.x) / cx)); x <= Mathf.Min(hr - 1, Mathf.CeilToInt((m.x + 40f - tp.x) / cx)); x++)
                holes[z, x] = true;
        data.SetHoles(0, 0, holes);
        CutMouth(terrain, cave.path);
        var mesh = TunnelMesh(cave.path, new System.Random(20261025));
        string meshPath = Dir + "/CaveTunnel.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        cave.GetComponent<MeshFilter>().sharedMesh = mesh;
        cave.GetComponent<MeshCollider>().sharedMesh = mesh;
        var old = cave.transform.Find("Mouth rocks");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        MouthRocks(cave.transform, terrain, cave.path, new System.Random(7));
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(cave.gameObject);
    }

    // The tunnel made wider and higher (the player could not get in on the phone): the mountain above it, the mouth,
    // the tunnel's rock, the rocks at the mouth and the rockfall, the mushrooms, the ore and the stalactites again.
    // The monk's place at the end stays where it is.
    public static void Widen(Cave cave)
    {
        var terrain = Terrain.activeTerrain;
        RaiseCover(terrain, cave.path, 5);
        FixMouth(cave);
        bool closed = cave.rockfall == null || cave.rockfall.activeSelf;
        if (cave.rockfall != null) Object.DestroyImmediate(cave.rockfall);
        cave.rockfall = Rockfall(cave.transform, cave.path, new System.Random(20261025));
        cave.rockfall.SetActive(closed);
        var mushrooms = cave.transform.Find("Cave mushrooms");
        if (mushrooms != null) Object.DestroyImmediate(mushrooms.gameObject);
        CaveMushrooms(cave.transform, cave.path, new System.Random(41));
        AddFormations(cave);
        AddOres(cave, 50);
        cave.radius = RadiusWide;
        EditorUtility.SetDirty(cave);
        EditorUtility.SetDirty(cave.gameObject);
        EditorSceneManager.MarkSceneDirty(cave.gameObject.scene);
    }

    // the fall of rocks that closes the mouth until Level 5
    static GameObject Rockfall(Transform root, List<Vector3> path, System.Random rng)
    {
        var group = new GameObject("Rockfall (open in Level 5)");
        group.transform.SetParent(root, false);
        Vector3 c = path[1], side = Side(path, 1), dir = Along(path, 1);
        float floor = c.y - FloorBelow;
        float k = RadiusWide / 3f; // (made for a tunnel 3 m wide; it grows with it)
        float up = RadiusHigh / 2.6f;
        Rock(group.transform, 0, new Vector3(c.x, floor - 0.5f, c.z) + side * -1.3f * k, 0.42f * k, rng);
        Rock(group.transform, 1, new Vector3(c.x, floor - 0.5f, c.z) + side * 1.4f * k + dir * 0.8f, 0.4f * k, rng);
        Rock(group.transform, 2, new Vector3(c.x, floor + 1.2f * up, c.z) + dir * 1.5f, 0.38f * k, rng);
        Rock(group.transform, 4, new Vector3(c.x, floor - 0.2f, c.z) - dir * 1.2f + side * 0.3f * k, 1.4f * k, rng);
        Rock(group.transform, 5, new Vector3(c.x, floor - 0.1f, c.z) - dir * 0.8f - side * 2f * k, 1.2f * k, rng);
        Rock(group.transform, 3, new Vector3(c.x, floor + 2.2f * up, c.z) + dir * 0.4f + side * 0.6f * k, 1.3f * k, rng);
        Rock(group.transform, 1, new Vector3(c.x, floor + 3.4f * up, c.z) + dir * 1.0f - side * 0.8f * k, 0.36f * k, rng);
        return group;
    }

    // ---- the monk's resting place at the end ----

    static Transform MonksPlace(Transform root, List<Vector3> path, System.Random rng)
    {
        int n = path.Count;
        Vector3 c = path[n - 3];
        Vector3 dir = Along(path, n - 3), side = Side(path, n - 3);
        Vector3 floor = new Vector3(c.x, c.y - FloorBelow, c.z);
        var place = new GameObject("Monk's resting place").transform;
        place.SetParent(root, false);
        place.position = floor;
        place.rotation = Quaternion.LookRotation(-dir, Vector3.up);

        var bone = PropMaterial("Bones", new Color(0.86f, 0.82f, 0.7f), Color.clear);
        var wood = PropMaterial("Cross Wood", new Color(0.36f, 0.22f, 0.12f), Color.clear);
        var wax = PropMaterial("Candle Wax", new Color(0.93f, 0.89f, 0.76f), new Color(0.06f, 0.04f, 0.02f));
        var flame = PropMaterial("Candle Flame", new Color(1f, 0.7f, 0.3f), new Color(1.6f, 0.9f, 0.35f));
        var robe = PropMaterial("Old Robe", new Color(0.12f, 0.1f, 0.09f), Color.clear);

        // the cross, near the back wall, looking at the one who comes in
        var cross = Prop(place, "Orthodox cross", CrossMesh(), wood, floor + dir * 2.6f, Quaternion.LookRotation(-dir, Vector3.up));
        cross.AddComponent<MeshCollider>().sharedMesh = cross.GetComponent<MeshFilter>().sharedMesh;
        // the monk: his bones on what is left of his black robe, lying in front of the cross
        Prop(place, "Old robe", RobeMesh(), robe, floor + side * 0.2f + Vector3.up * 0.01f, Quaternion.LookRotation(side, Vector3.up));
        Prop(place, "Bones of Brother Ioan", BonesMesh(), bone, floor + side * -0.6f + dir * 0.3f + Vector3.up * 0.02f, Quaternion.LookRotation(side, Vector3.up));
        // candles
        foreach (var at in new[] { floor + dir * 2.3f + side * 0.75f, floor + dir * 2.3f - side * 0.75f, floor - side * 1.6f + dir * 0.9f })
        {
            var candle = Prop(place, "Candle", CandleMesh(), wax, at, Quaternion.identity);
            var f = Prop(candle.transform, "Flame", FlameMesh(), flame, at + Vector3.up * 0.25f, Quaternion.identity);
            f.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            candle.AddComponent<CaveCandle>();
        }
        // the journal next to him, the prayer book at the foot of the cross
        PutPlant(place, "Old Journal", floor - side * 0.9f - dir * 0.4f + Vector3.up * 0.01f, (float)rng.NextDouble() * 40f + 20f);
        PutPlant(place, "Prayer Book", floor + dir * 2.0f + Vector3.up * 0.01f, 180f + Quaternion.LookRotation(-dir).eulerAngles.y);
        return place;
    }

    static GameObject Prop(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, true);
        go.transform.position = position;
        go.transform.rotation = rotation;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static void PutPlant(Transform parent, string name, Vector3 at, float yaw)
    {
        var prefab = Resources.Load<GameObject>("Vegetation/" + name);
        if (prefab == null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        var plant = go.GetComponent<ScenePlant>();
        if (plant != null) plant.hideUnderSnow = false;
    }

    // mushrooms along the way and in the hall, a few that glow
    static void CaveMushrooms(Transform root, List<Vector3> path, System.Random rng)
    {
        var group = new GameObject("Cave mushrooms").transform;
        group.SetParent(root, false);
        string[] kinds = { "Porcini", "Field Mushroom", "Red Mushroom", "Field Mushroom", "Porcini" };
        int n = path.Count;
        for (int i = 6; i < n - 1; i++)
        {
            Vector3 side = Side(path, i);
            float s = Scale(i, n);
            int count = i >= n - 7 ? 3 : (rng.NextDouble() < 0.6 ? 1 : 0);
            for (int j = 0; j < count; j++)
            {
                float across = (rng.NextDouble() < 0.5 ? -1f : 1f) * RadiusWide * s * Mathf.Lerp(0.55f, 0.8f, (float)rng.NextDouble());
                Vector3 at = path[i] + side * across + Vector3.up * (-FloorBelow + 0.02f);
                PutPlant(group, kinds[rng.Next(kinds.Length)], at, (float)rng.NextDouble() * 360f);
            }
            if (i % 3 == 0 || i >= n - 6)
            {
                float across = (rng.NextDouble() < 0.5 ? -1f : 1f) * RadiusWide * s * 0.85f;
                PutPlant(group, "Glow Mushroom", path[i] + side * across + Vector3.up * (-FloorBelow + 0.02f), (float)rng.NextDouble() * 360f);
            }
        }
    }

    // ---- meshes made of simple shapes ----

    static Mesh Combine(string name, List<CombineInstance> parts)
    {
        var mesh = new Mesh { name = name };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        mesh.RecalculateBounds();
        string path = Dir + "/" + name + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Mesh Builtin(string name) => Resources.GetBuiltinResource<Mesh>(name);

    static CombineInstance Part(string shape, Vector3 position, Vector3 euler, Vector3 size)
    {
        return new CombineInstance { mesh = Builtin(shape), transform = Matrix4x4.TRS(position, Quaternion.Euler(euler), size) };
    }

    // an Orthodox cross: the short bar on top, the long bar, the slanted bar at the foot
    static Mesh CrossMesh()
    {
        var p = new List<CombineInstance>
        {
            Part("Cube.fbx", new Vector3(0f, 0.95f, 0f), Vector3.zero, new Vector3(0.12f, 2.2f, 0.1f)),
            Part("Cube.fbx", new Vector3(0f, 1.82f, 0f), Vector3.zero, new Vector3(0.42f, 0.08f, 0.09f)),
            Part("Cube.fbx", new Vector3(0f, 1.52f, 0f), Vector3.zero, new Vector3(1.05f, 0.11f, 0.1f)),
            Part("Cube.fbx", new Vector3(0f, 0.55f, 0f), new Vector3(0f, 0f, -22f), new Vector3(0.58f, 0.08f, 0.09f)),
        };
        // stones at its foot
        for (int i = 0; i < 7; i++)
        {
            float a = i / 7f * Mathf.PI * 2f;
            p.Add(Part("Sphere.fbx", new Vector3(Mathf.Cos(a) * 0.32f, 0.03f, Mathf.Sin(a) * 0.32f), new Vector3(0f, i * 40f, 0f), new Vector3(0.22f, 0.13f, 0.18f)));
        }
        return Combine("Orthodox Cross", p);
    }

    // a skeleton lying on its back (head at +z)
    static Mesh BonesMesh()
    {
        var p = new List<CombineInstance>();
        p.Add(Part("Sphere.fbx", new Vector3(0f, 0.1f, 0.88f), new Vector3(-15f, 12f, 0f), new Vector3(0.18f, 0.17f, 0.22f)));  // skull
        p.Add(Part("Cube.fbx", new Vector3(0f, 0.04f, 0.97f), new Vector3(0f, 12f, 0f), new Vector3(0.11f, 0.04f, 0.07f)));      // jaw
        for (int i = 0; i < 12; i++) p.Add(Part("Cylinder.fbx", new Vector3(0f, 0.045f, 0.74f - i * 0.055f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.02f, 0.05f))); // spine
        for (int i = 0; i < 6; i++)
        {
            float z = 0.66f - i * 0.065f;
            float w = 0.13f - i * 0.006f;
            for (int s = -1; s <= 1; s += 2)
                p.Add(Part("Capsule.fbx", new Vector3(s * w * 0.75f, 0.08f, z), new Vector3(0f, 0f, s * 62f), new Vector3(0.022f, w * 0.75f, 0.022f))); // ribs
        }
        p.Add(Part("Sphere.fbx", new Vector3(0f, 0.05f, 0.05f), Vector3.zero, new Vector3(0.3f, 0.08f, 0.15f)));                 // pelvis
        foreach (int s in new[] { -1, 1 })
        {
            p.Add(Part("Capsule.fbx", new Vector3(s * 0.21f, 0.04f, 0.5f), new Vector3(90f, s * 8f, 0f), new Vector3(0.035f, 0.15f, 0.035f)));   // upper arm
            p.Add(Part("Capsule.fbx", new Vector3(s * 0.18f, 0.035f, 0.22f), new Vector3(90f, s * -25f, 0f), new Vector3(0.03f, 0.13f, 0.03f))); // forearm
            p.Add(Part("Capsule.fbx", new Vector3(s * 0.1f, 0.045f, -0.2f), new Vector3(90f, s * 4f, 0f), new Vector3(0.045f, 0.22f, 0.045f)));  // thigh
        }
        p.Add(Part("Capsule.fbx", new Vector3(-0.12f, 0.04f, -0.62f), new Vector3(90f, -6f, 0f), new Vector3(0.04f, 0.2f, 0.04f)));   // shin
        p.Add(Part("Capsule.fbx", new Vector3(0.24f, 0.04f, -0.55f), new Vector3(90f, 40f, 0f), new Vector3(0.04f, 0.2f, 0.04f)));    // the other shin, fallen aside
        return Combine("Bones", p);
    }

    static Mesh RobeMesh()
    {
        var p = new List<CombineInstance>
        {
            Part("Sphere.fbx", new Vector3(0f, 0.02f, 0.15f), Vector3.zero, new Vector3(0.75f, 0.05f, 1.7f)),
            Part("Sphere.fbx", new Vector3(0.15f, 0.025f, -0.35f), new Vector3(0f, 20f, 0f), new Vector3(0.6f, 0.05f, 0.9f)),
        };
        return Combine("Old Robe", p);
    }

    static Mesh CandleMesh()
    {
        return Combine("Candle", new List<CombineInstance>
        {
            Part("Cylinder.fbx", new Vector3(0f, 0.1f, 0f), Vector3.zero, new Vector3(0.07f, 0.1f, 0.07f)),
            Part("Cylinder.fbx", new Vector3(0f, 0.012f, 0f), Vector3.zero, new Vector3(0.13f, 0.012f, 0.13f)),
        });
    }

    static Mesh FlameMesh()
    {
        return Combine("Candle Flame", new List<CombineInstance> { Part("Sphere.fbx", Vector3.zero, Vector3.zero, new Vector3(0.035f, 0.07f, 0.035f)) });
    }

    // ---- iron ore along the tunnel (Level 8: 10 of them make the iron axe) ----

    public static void AddOres(Cave cave, int count)
    {
        var old = cave.transform.Find("Iron ore");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var group = new GameObject("Iron ore").transform;
        group.SetParent(cave.transform, false);
        var rng = new System.Random(77);
        var path = cave.path;
        int n = path.Count;
        Vector3 restingPlace = cave.end != null ? cave.end.position : path[n - 3];
        for (int k = 0, tries = 0; k < count && tries < count * 10; tries++)
        {
            int i = 5 + rng.Next(n - 6);
            Vector3 side = Side(path, i), along = Along(path, i);
            float s = Scale(i, n);
            float across = (rng.NextDouble() < 0.5 ? -1f : 1f) * RadiusWide * s * Mathf.Lerp(0.6f, 0.82f, (float)rng.NextDouble());
            Vector3 at = path[i] + side * across + along * (((float)rng.NextDouble() - 0.5f) * Step) + Vector3.up * (-FloorBelow + 0.05f);
            if (Flat(at - restingPlace) < 3f) continue;
            PutPlant(group, "Iron Ore", at, (float)rng.NextDouble() * 360f);
            k++;
        }
        EditorUtility.SetDirty(cave.gameObject);
    }

    // ---- stalactites (hanging from the roof) and stalagmites (growing from the floor) ----

    public static void AddFormations(Cave cave)
    {
        var old = cave.transform.Find("Stalactites and stalagmites");
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var path = cave.path;
        int n = path.Count;
        var rng = new System.Random(31);
        var v = new List<Vector3>();
        var tri = new List<int>();
        Vector3 restingPlace = cave.end != null ? cave.end.position : path[n - 3];
        for (int i = 3; i < n - 1; i++)
        {
            Vector3 side = Side(path, i), along = Along(path, i);
            float s = Scale(i, n);
            bool hall = i >= n - 7;
            float high = RadiusHigh * Mathf.Lerp(1f, 1.35f, s - 1f);
            int roof = hall ? 7 : 2 + rng.Next(3);
            for (int k = 0; k < roof; k++)
            {
                float a = Mathf.Lerp(45f, 135f, (float)rng.NextDouble()) * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * RadiusWide * s * 0.9f;
                float y = Mathf.Sin(a) * high * 0.92f;
                Vector3 top = path[i] + side * x + Vector3.up * y + along * (((float)rng.NextDouble() - 0.5f) * Step);
                float floorY = path[i].y - FloorBelow;
                float length = Mathf.Lerp(0.3f, hall ? 2.2f : 1.2f, (float)rng.NextDouble() * (float)rng.NextDouble());
                if (Mathf.Abs(x) < RadiusWide * 0.6f) length = Mathf.Min(length, top.y - floorY - 4.2f); // room to walk under it
                if (length < 0.2f) continue;
                Cone(v, tri, top, Vector3.down, length, Mathf.Lerp(0.07f, 0.22f, (float)rng.NextDouble()) * Mathf.Sqrt(length), rng);
            }
            int floor = hall ? 4 : rng.Next(3);
            for (int k = 0; k < floor; k++)
            {
                float sign = rng.NextDouble() < 0.5 ? -1f : 1f;
                float x = sign * Mathf.Lerp(2.4f, RadiusWide * s * 0.72f, (float)rng.NextDouble());
                Vector3 bottom = path[i] + side * x + Vector3.up * (-FloorBelow - 0.05f) + along * (((float)rng.NextDouble() - 0.5f) * Step);
                if (Flat(bottom - restingPlace) < 3.5f) continue; // not on the monk's place
                float length = Mathf.Lerp(0.25f, hall ? 1.7f : 1f, (float)rng.NextDouble());
                Cone(v, tri, bottom, Vector3.up, length, Mathf.Lerp(0.12f, 0.3f, (float)rng.NextDouble()) * Mathf.Sqrt(length), rng);
            }
        }
        var mesh = new Mesh { name = "Cave formations" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        string meshPath = Dir + "/CaveFormations.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(mesh, meshPath);
        var go = new GameObject("Stalactites and stalagmites");
        go.transform.SetParent(cave.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = PropMaterial("Limestone", new Color(0.6f, 0.59f, 0.56f), Color.clear);
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
        EditorUtility.SetDirty(cave.gameObject);
    }

    // a lumpy cone from base along dir
    static void Cone(List<Vector3> v, List<int> tri, Vector3 basePoint, Vector3 dir, float length, float radius, System.Random rng)
    {
        const int segments = 9, rings = 5;
        Vector3 a = Vector3.Cross(dir, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(dir, Vector3.forward).normalized : Vector3.right;
        Vector3 b = Vector3.Cross(dir, a).normalized;
        float twist = (float)rng.NextDouble() * 6.28f;
        int start = v.Count;
        for (int r = 0; r < rings; r++)
        {
            float t = (float)r / rings;
            float rr = radius * Mathf.Pow(1f - t, 1.3f) * (1f + 0.25f * Mathf.Sin(t * 9f + twist));
            Vector3 c = basePoint + dir * (length * t) + (a * Mathf.Sin(twist + t * 3f) + b * Mathf.Cos(twist * 1.7f + t * 2f)) * radius * 0.15f * t;
            for (int k = 0; k < segments; k++)
            {
                float ang = (float)k / segments * Mathf.PI * 2f;
                float lump = 1f + 0.18f * Mathf.Sin(ang * 3f + twist + r);
                v.Add(c + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * rr * lump);
            }
        }
        int tip = v.Count;
        v.Add(basePoint + dir * length);
        for (int r = 0; r < rings - 1; r++)
            for (int k = 0; k < segments; k++)
            {
                int i0 = start + r * segments + k, i1 = start + r * segments + (k + 1) % segments;
                int j0 = i0 + segments, j1 = i1 + segments;
                tri.Add(i0); tri.Add(j0); tri.Add(i1);
                tri.Add(i1); tri.Add(j0); tri.Add(j1);
            }
        int last = start + (rings - 1) * segments;
        for (int k = 0; k < segments; k++) { tri.Add(last + k); tri.Add(tip); tri.Add(last + (k + 1) % segments); }
    }

    // ---- the books (pickable: ScenePlant) and the glowing mushroom ----

    public static void MakeBookPrefabs()
    {
        Directory.CreateDirectory(Dir);
        var standard = Shader.Find("Standard");
        Material Mat(string name, Color c, float gloss)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(standard); AssetDatabase.CreateAsset(m, path); }
            m.color = c;
            m.SetFloat("_Glossiness", gloss);
            EditorUtility.SetDirty(m);
            return m;
        }
        var leather = Mat("Journal Leather", new Color(0.33f, 0.19f, 0.1f), 0.25f);
        var paper = Mat("Old Paper", new Color(0.86f, 0.8f, 0.64f), 0.05f);
        var red = Mat("Prayer Book Cover", new Color(0.36f, 0.05f, 0.05f), 0.3f);
        var gold = Mat("Gold", new Color(0.95f, 0.75f, 0.3f), 0.7f);
        gold.SetFloat("_Metallic", 0.8f);

        Book("Old Journal", "journal", "Old Journal", leather, paper, null, new Vector3(0.24f, 0.05f, 0.32f));
        Book("Prayer Book", "prayerbook", "Prayer Book", red, paper, gold, new Vector3(0.2f, 0.06f, 0.27f));

        // a mushroom that glows a little in the dark (only to be seen)
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Vegetation/Red Mushroom.prefab");
        if (source != null)
        {
            var glow = Mat("Glow Mushroom", new Color(0.65f, 0.95f, 0.9f), 0.4f);
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.15f, 0.75f, 0.65f) * 1.4f);
            glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            var root = PrefabUtility.LoadPrefabContents("Assets/Resources/Vegetation/Red Mushroom.prefab");
            try
            {
                root.name = "Glow Mushroom";
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = glow;
                    r.sharedMaterials = mats;
                }
                var plant = root.GetComponent<ScenePlant>();
                plant.item = "";
                plant.displayName = "";
                plant.hideUnderSnow = false;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Resources/Vegetation/Glow Mushroom.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    static void Book(string name, string item, string display, Material cover, Material pages, Material decoration, Vector3 size)
    {
        var parts = new List<CombineInstance>
        {
            Part("Cube.fbx", new Vector3(0f, size.y * 0.5f, 0f), Vector3.zero, size),
        };
        var pageParts = new List<CombineInstance>
        {
            Part("Cube.fbx", new Vector3(0.006f, size.y * 0.5f, 0f), Vector3.zero, new Vector3(size.x - 0.012f, size.y * 0.8f, size.z - 0.014f) + new Vector3(0.004f, 0f, 0f)),
        };
        var go = new GameObject(name);
        var coverGo = new GameObject("Cover");
        coverGo.transform.SetParent(go.transform, false);
        coverGo.AddComponent<MeshFilter>().sharedMesh = Combine(name + " cover", parts);
        coverGo.AddComponent<MeshRenderer>().sharedMaterial = cover;
        var pagesGo = new GameObject("Pages");
        pagesGo.transform.SetParent(go.transform, false);
        pagesGo.transform.localPosition = new Vector3(0.004f, 0f, 0f);
        pagesGo.AddComponent<MeshFilter>().sharedMesh = Combine(name + " pages", pageParts);
        pagesGo.AddComponent<MeshRenderer>().sharedMaterial = pages;
        if (decoration != null)
        {
            // a small Orthodox cross on the cover
            var crossParts = new List<CombineInstance>
            {
                Part("Cube.fbx", new Vector3(0f, size.y + 0.002f, 0f), Vector3.zero, new Vector3(0.018f, 0.004f, 0.15f)),
                Part("Cube.fbx", new Vector3(0f, size.y + 0.002f, 0.045f), Vector3.zero, new Vector3(0.05f, 0.004f, 0.014f)),
                Part("Cube.fbx", new Vector3(0f, size.y + 0.002f, 0.02f), Vector3.zero, new Vector3(0.1f, 0.004f, 0.016f)),
                Part("Cube.fbx", new Vector3(0f, size.y + 0.002f, -0.045f), new Vector3(0f, 22f, 0f), new Vector3(0.06f, 0.004f, 0.014f)),
            };
            var cross = new GameObject("Cross");
            cross.transform.SetParent(go.transform, false);
            cross.AddComponent<MeshFilter>().sharedMesh = Combine(name + " cross", crossParts);
            cross.AddComponent<MeshRenderer>().sharedMaterial = decoration;
        }
        go.transform.localScale = Vector3.one * 1.6f; // a bit big, so it is found in the dark
        var plant = go.AddComponent<ScenePlant>();
        plant.item = item;
        plant.displayName = display;
        plant.regrowHours = 0f;
        plant.size = ScenePlant.PlantSize.Small;
        plant.hideUnderSnow = false;
        PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/Vegetation/" + name + ".prefab");
        Object.DestroyImmediate(go);
    }
}
