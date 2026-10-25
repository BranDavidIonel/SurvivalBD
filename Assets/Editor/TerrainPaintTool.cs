using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Paints the island's terrain to go with its forests (called by Tools > Nature > 3. Fill The Island):
//  - under the trees a dark forest floor (old leaves, needles, earth) instead of the light green grass
//  - no grass under the trees (it does not grow in the shade); grass on the open meadows
//  - a dense undergrowth in the forests: ferns, bushes and small plants, painted as terrain details (cheap to draw on
//    a phone, and they can be painted by hand with the terrain's "Paint Details" tool)
public static class TerrainPaintTool
{
    const string Dir = "Assets/Island/ForestFloor";
    const string LayerPath = Dir + "/ForestFloor.terrainlayer";

    static readonly string[] Undergrowth =
    {
        "Assets/TerrainSampleAssets/Prefabs/Fern_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Fern_B.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Fern_C.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Bush_B.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Plant_A.prefab",
        "Assets/TerrainSampleAssets/Prefabs/Grass_C.prefab",
    };
    // how many of each (per square metre, where the canopy is full) and how big
    static readonly float[] UndergrowthDensity = { 0.05f, 0.045f, 0.04f, 0.012f, 0.02f, 0.03f };
    static readonly Vector2[] UndergrowthSize = { new Vector2(1.6f, 2.6f), new Vector2(1.8f, 2.8f), new Vector2(1.4f, 2.2f), new Vector2(1.2f, 1.9f), new Vector2(1f, 1.6f), new Vector2(0.9f, 1.4f) };

    public static TerrainLayer MakeForestFloorLayer()
    {
        Directory.CreateDirectory(Dir);
        string png = Dir + "/ForestFloor.png";
        if (!File.Exists(png))
        {
            File.WriteAllBytes(png, ForestFloorTexture(512).EncodeToPNG());
            AssetDatabase.ImportAsset(png);
        }
        // no alpha on Android (ETC2 RGB): with ASTC the terrain shader reads the alpha as shine and the ground
        // looks like a mirror of the sky (see Tools > Mobile > Fix Shiny Terrain On Android)
        var importer = (TextureImporter)AssetImporter.GetAtPath(png);
        importer.wrapMode = TextureWrapMode.Repeat;
        var android = importer.GetPlatformTextureSettings("Android");
        android.overridden = true;
        android.format = TextureImporterFormat.ETC2_RGB4;
        android.maxTextureSize = 512;
        importer.SetPlatformTextureSettings(android);
        importer.SaveAndReimport();
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(LayerPath);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, LayerPath);
        }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(png);
        layer.tileSize = new Vector2(7f, 7f);
        layer.smoothness = 0.05f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        AssetDatabase.SaveAssets();
        return layer;
    }

    // dark earth with old leaves and needles (tiles without seams)
    static Texture2D ForestFloorTexture(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
        var rng = new System.Random(11);
        var px = new Color[size * size];
        Color earth = new Color(0.17f, 0.13f, 0.08f), moss = new Color(0.16f, 0.2f, 0.08f), leaf = new Color(0.33f, 0.21f, 0.09f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (float)x / size, v = (float)y / size;
                float n = Periodic(u, v, 6, 1) * 0.6f + Periodic(u, v, 18, 2) * 0.3f + Periodic(u, v, 48, 3) * 0.1f;
                float m = Periodic(u, v, 4, 7);
                Color c = Color.Lerp(earth, moss, Mathf.SmoothStep(0.45f, 0.75f, m));
                c *= 0.75f + 0.5f * n;
                px[y * size + x] = c;
            }
        // fallen leaves and needles
        for (int k = 0; k < 2600; k++)
        {
            int cx = rng.Next(size), cy = rng.Next(size);
            bool needle = rng.NextDouble() < 0.5;
            float a = (float)(rng.NextDouble() * Mathf.PI);
            int len = needle ? 6 + rng.Next(8) : 3 + rng.Next(4);
            Color c = needle ? new Color(0.28f, 0.2f, 0.11f) : Color.Lerp(leaf, new Color(0.45f, 0.3f, 0.12f), (float)rng.NextDouble());
            for (int i = -len; i <= len; i++)
                for (int w = needle ? 0 : -2; w <= (needle ? 0 : 2); w++)
                {
                    int x = (cx + Mathf.RoundToInt(Mathf.Cos(a) * i - Mathf.Sin(a) * w) + size) % size;
                    int y = (cy + Mathf.RoundToInt(Mathf.Sin(a) * i + Mathf.Cos(a) * w) + size) % size;
                    if (!needle && Mathf.Abs(i) + Mathf.Abs(w) * 1.5f > len) continue;
                    px[y * size + x] = Color.Lerp(px[y * size + x], c, 0.8f);
                }
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static float Periodic(float u, float v, int period, int seed)
    {
        float x = u * period, y = v * period;
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float tx = Mathf.SmoothStep(0f, 1f, x - x0), ty = Mathf.SmoothStep(0f, 1f, y - y0);
        float a = Hash(x0 % period, y0 % period, seed), b = Hash((x0 + 1) % period, y0 % period, seed);
        float c = Hash(x0 % period, (y0 + 1) % period, seed), d = Hash((x0 + 1) % period, (y0 + 1) % period, seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
    }

    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 2147483647;
            h = (h ^ (h >> 13)) * 1274126177;
            return (h & 0xffff) / 65535f;
        }
    }

    // paints again from the trees that are in the scene now (after moving or adding forests by hand)
    [MenuItem("Tools/Nature/Paint The Ground Under The Trees Again")]
    public static void RepaintFromScene() => RepaintFromScene(20261026);

    public static void RepaintFromScene(int seed)
    {
        var terrain = Terrain.activeTerrain;
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (terrain == null || manager == null) return;
        var trees = new List<Vector3>();
        var crowns = new List<float>();
        foreach (var r in manager.allTrees.GetComponentsInChildren<Renderer>())
        {
            trees.Add(r.transform.position);
            crowns.Add(Mathf.Clamp(Mathf.Max(r.bounds.extents.x, r.bounds.extents.z) * 0.85f, 2.5f, 12f));
        }
        var river = Object.FindObjectOfType<River>();
        var cave = Object.FindObjectOfType<Cave>();
        Bounds lake = RiverTool.LakeBounds(false);
        lake.Expand(new Vector3(12f, 200f, 12f));
        Vector3 mouth = cave != null && cave.path.Count > 0 ? cave.path[0] : new Vector3(1e6f, 0f, 1e6f);
        Paint(terrain, trees, crowns, p =>
            (lake.size.x > 0f && lake.Contains(new Vector3(p.x, lake.center.y, p.z)))
            || (river != null && river.DistanceToWater(p) < 1.5f)
            || (new Vector2(p.x - mouth.x, p.z - mouth.z).magnitude < 10f), seed);
    }

    // trees: where they stand and how wide their crowns are
    public static void Paint(Terrain terrain, List<Vector3> trees, List<float> crowns, System.Func<Vector3, bool> keepOut, int seed)
    {
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        Undo.RegisterCompleteObjectUndo(data, "Paint forests");

        // ---- how much canopy covers each spot (2 m squares) ----
        const float cell = 2f;
        int n = Mathf.CeilToInt(data.size.x / cell);
        var cover = new float[n * n];
        for (int t = 0; t < trees.Count; t++)
        {
            float r = Mathf.Max(3f, crowns[t]);
            int x0 = Mathf.FloorToInt((trees[t].x - r - tp.x) / cell), x1 = Mathf.CeilToInt((trees[t].x + r - tp.x) / cell);
            int z0 = Mathf.FloorToInt((trees[t].z - r - tp.z) / cell), z1 = Mathf.CeilToInt((trees[t].z + r - tp.z) / cell);
            for (int z = Mathf.Max(0, z0); z <= Mathf.Min(n - 1, z1); z++)
                for (int x = Mathf.Max(0, x0); x <= Mathf.Min(n - 1, x1); x++)
                {
                    float dx = tp.x + (x + 0.5f) * cell - trees[t].x, dz = tp.z + (z + 0.5f) * cell - trees[t].z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) / r;
                    if (d >= 1f) continue;
                    float v = 1f - Mathf.SmoothStep(0.45f, 1f, d);
                    int i = z * n + x;
                    cover[i] = Mathf.Min(1f, cover[i] + v);
                }
        }
        // soften the edges a little
        var soft = new float[n * n];
        for (int z = 1; z < n - 1; z++)
            for (int x = 1; x < n - 1; x++)
            {
                int i = z * n + x;
                soft[i] = (cover[i] * 4f + cover[i - 1] + cover[i + 1] + cover[i - n] + cover[i + n]) / 8f;
            }
        cover = soft;
        float CoverAt(float wx, float wz)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((wx - tp.x) / cell), 0, n - 1), z = Mathf.Clamp(Mathf.FloorToInt((wz - tp.z) / cell), 0, n - 1);
            return cover[z * n + x];
        }

        // ---- the dark forest floor under the trees ----
        var floorLayer = MakeForestFloorLayer();
        var layers = new List<TerrainLayer>(data.terrainLayers);
        int floorIndex = layers.IndexOf(floorLayer);
        int ar = data.alphamapResolution;
        float[,,] maps;
        if (floorIndex < 0)
        {
            var old = data.GetAlphamaps(0, 0, ar, ar);
            layers.Add(floorLayer);
            data.terrainLayers = layers.ToArray();
            floorIndex = layers.Count - 1;
            maps = new float[ar, ar, layers.Count];
            for (int z = 0; z < ar; z++) for (int x = 0; x < ar; x++) for (int l = 0; l < layers.Count - 1; l++) maps[z, x, l] = old[z, x, l];
        }
        else maps = data.GetAlphamaps(0, 0, ar, ar);
        int count = maps.GetLength(2);
        int sand = -1, grass = -1;
        for (int l = 0; l < layers.Count; l++)
        {
            if (layers[l] == null) continue;
            string name = layers[l].name.ToLowerInvariant();
            if (name.Contains("sand")) sand = l;
            if (grass < 0 && name.Contains("grass")) grass = l;
        }
        for (int z = 0; z < ar; z++)
            for (int x = 0; x < ar; x++)
            {
                float wx = tp.x + (x + 0.5f) / ar * data.size.x, wz = tp.z + (z + 0.5f) / ar * data.size.z;
                float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(CoverAt(wx, wz) * 1.7f)) * 0.96f;
                if (sand >= 0) w *= 1f - maps[z, x, sand];
                if (w <= 0.001f) continue;
                for (int l = 0; l < count; l++) if (l != floorIndex) maps[z, x, l] *= 1f - w;
                maps[z, x, floorIndex] = maps[z, x, floorIndex] * (1f - w) + w;
            }
        data.SetAlphamaps(0, 0, maps);

        // ---- details: no grass in the shade, grass on the meadows, undergrowth in the forests ----
        var prototypes = new List<DetailPrototype>(data.detailPrototypes);
        int grassDetails = prototypes.Count; // the terrain's own grass and flowers
        var undergrowthIndex = new int[Undergrowth.Length];
        for (int u = 0; u < Undergrowth.Length; u++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Undergrowth[u]);
            undergrowthIndex[u] = -1;
            if (prefab == null) continue;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>())
                foreach (var m in r.sharedMaterials) if (m != null && !m.enableInstancing) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
            int existing = prototypes.FindIndex(p => p.prototype == prefab);
            if (existing >= 0) { undergrowthIndex[u] = existing; continue; }
            prototypes.Add(new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                useInstancing = true,
                renderMode = DetailRenderMode.VertexLit,
                minWidth = UndergrowthSize[u].x,
                maxWidth = UndergrowthSize[u].y,
                minHeight = UndergrowthSize[u].x,
                maxHeight = UndergrowthSize[u].y,
                noiseSpread = 0.4f,
                healthyColor = Color.white,
                dryColor = new Color(0.85f, 0.8f, 0.7f),
            });
            undergrowthIndex[u] = prototypes.Count - 1;
        }
        data.detailPrototypes = prototypes.ToArray();
        int dr = data.detailResolution;
        float cellArea = (data.size.x / dr) * (data.size.z / dr);
        var rng = new System.Random(seed);
        var grassLayers = new List<int[,]>();
        for (int l = 0; l < grassDetails; l++) grassLayers.Add(data.GetDetailLayer(0, 0, dr, dr, l));
        var under = new int[Undergrowth.Length][,];
        for (int u = 0; u < Undergrowth.Length; u++) under[u] = new int[dr, dr];
        float level = Ocean.Instance != null ? Ocean.Instance.Level : -19.05f;
        for (int z = 0; z < dr; z++)
            for (int x = 0; x < dr; x++)
            {
                float wx = tp.x + (x + 0.5f) / dr * data.size.x, wz = tp.z + (z + 0.5f) / dr * data.size.z;
                float c = CoverAt(wx, wz);
                // the grass: none in the shade, some more on the meadows that had none
                if (c > 0.35f) { foreach (var g in grassLayers) g[z, x] = 0; }
                else if (grassDetails > 0 && grassLayers[0][z, x] == 0 && c < 0.15f && rng.NextDouble() < 0.18)
                {
                    Vector3 p = new Vector3(wx, 0f, wz);
                    int ax = Mathf.Clamp(Mathf.FloorToInt((float)x / dr * ar), 0, ar - 1), az = Mathf.Clamp(Mathf.FloorToInt((float)z / dr * ar), 0, ar - 1);
                    if (grass >= 0 && maps[az, ax, grass] > 0.5f && terrain.SampleHeight(p) + tp.y > level + 0.6f && !keepOut(p))
                        grassLayers[0][z, x] = 1 + rng.Next(2);
                }
                if (c < 0.3f) continue;
                for (int u = 0; u < Undergrowth.Length; u++)
                {
                    if (undergrowthIndex[u] < 0) continue;
                    if (rng.NextDouble() > UndergrowthDensity[u] * cellArea * Mathf.SmoothStep(0.3f, 0.9f, c)) continue;
                    if (keepOut(new Vector3(wx, 0f, wz))) continue;
                    under[u][z, x] = 1;
                }
            }
        for (int l = 0; l < grassDetails; l++) data.SetDetailLayer(0, 0, l, grassLayers[l]);
        for (int u = 0; u < Undergrowth.Length; u++) if (undergrowthIndex[u] >= 0) data.SetDetailLayer(0, 0, undergrowthIndex[u], under[u]);
        terrain.detailObjectDistance = Mathf.Max(terrain.detailObjectDistance, 80f);
        EditorUtility.SetDirty(data);
        EditorUtility.SetDirty(terrain);
        AssetDatabase.SaveAssets();
    }
}
