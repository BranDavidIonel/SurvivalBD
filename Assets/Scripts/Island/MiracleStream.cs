using System.Collections.Generic;
using UnityEngine;

// The miracle of Brother Ioan's canon (level 9): when the cross is raised on the top of the other mountain, water
// springs from the rock under it and a new stream runs down to the sea. Fresh water: the player can drink from it.
// Where it flows into the sea the raft of the last level is built (Raft.MakeSite).
// Made in code when the game scene loads (EnvironmentBootstrap): the way down is found on the terrain (the cheapest
// way to the sea, uphill is very expensive, like the river), the water is a strip lying on the ground with the river's
// own water material. Nothing shows until the cross stands.
public class MiracleStream : MonoBehaviour
{
    public static MiracleStream Instance { get; private set; }

    [Tooltip("How fast the new water runs down when the cross is raised (metres per second)")]
    public float revealSpeed = 16f;
    public float drinkReach = 2.5f;

    public bool Flowing { get; private set; }
    public IReadOnlyList<Vector3> Points => points; // (the map draws it)

    readonly List<Vector3> points = new List<Vector3>();
    readonly List<float> halfWidths = new List<float>();
    readonly List<float> lengths = new List<float>();
    float total;
    float revealed;
    float rebuildTimer;
    MeshFilter filter;
    MeshRenderer meshRenderer;
    Mesh mesh;
    AudioSource sound;
    Transform listener;
    bool summitFound;
    Vector3 summit;

    public static MiracleStream Create()
    {
        if (Instance != null) return Instance;
        return new GameObject("Miracle stream (made in code)").AddComponent<MiracleStream>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------------- where ---------------- //

    // the top of the other mountain: the highest ground well away from where the old river begins (and from the cave)
    public Vector3 Summit
    {
        get
        {
            if (!summitFound) FindSummit();
            return summit;
        }
    }

    // where it meets the sea, and which way the sea is from there
    public Vector3 Mouth { get; private set; }
    public Vector3 SeaDirection { get; private set; } = Vector3.forward;

    void FindSummit()
    {
        summitFound = true;
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        Vector3 riverSource = River.Instance != null && River.Instance.points.Count > 0 ? River.Instance.points[0] : new Vector3(1e6f, 0f, 1e6f);
        Vector3 caveMouth = Cave.Instance != null && Cave.Instance.path.Count > 0 ? Cave.Instance.path[0] : new Vector3(1e6f, 0f, 1e6f);
        const float cell = 6f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        float best = float.MinValue;
        for (int z = 4; z < n - 4; z++)
            for (int x = 4; x < n - 4; x++)
            {
                Vector3 p = tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);
                if (Flat(p - riverSource) < 280f || Flat(p - caveMouth) < 120f) continue;
                float h = terrain.SampleHeight(p) + tp.y;
                if (h > best) { best = h; summit = new Vector3(p.x, h, p.z); }
            }
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ---------------- the water appears ---------------- //

    // from: the cross (the spring is just below it); animate: the water runs down now (else: it is there already)
    public void Begin(Vector3 from, bool animate)
    {
        if (Flowing) return;
        if (!FindWay(from)) { Debug.LogWarning("Miracle stream: no way to the sea found"); return; }
        Flowing = true;
        revealed = animate ? 0f : total;
        if (filter == null)
        {
            filter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = WaterMaterial();
            mesh = new Mesh { name = "Miracle stream" };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
        }
        BuildMesh();
        StartSound();
    }

    static Material WaterMaterial()
    {
        var river = River.Instance;
        var r = river != null ? river.GetComponent<MeshRenderer>() : null;
        if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
        var shader = Shader.Find("SurvivalBD/River");
        return new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Miracle stream", hideFlags = HideFlags.DontSave };
    }

    void Update()
    {
        if (!Flowing) return;
        if (revealed < total)
        {
            revealed = Mathf.Min(total, revealed + revealSpeed * Time.deltaTime);
            rebuildTimer -= Time.deltaTime;
            if (rebuildTimer <= 0f || revealed >= total) { rebuildTimer = 0.08f; BuildMesh(); }
        }
        if (sound != null)
        {
            Vector3 at = listener != null ? listener.position : (Camera.main != null ? Camera.main.transform.position : transform.position);
            float d = DistanceToWater(at, out Vector3 nearest);
            if (d > 45f) sound.volume = 0f;
            else
            {
                sound.transform.position = nearest;
                sound.volume = 0.5f * SoundManager.AmbienceVolume;
            }
        }
    }

    void StartSound()
    {
        if (sound != null) return;
        var go = new GameObject("Stream sound");
        go.transform.SetParent(transform, false);
        sound = go.AddComponent<AudioSource>();
        sound.clip = River.BrookSound();
        sound.loop = true;
        sound.spatialBlend = 1f;
        sound.rolloffMode = AudioRolloffMode.Linear;
        sound.minDistance = 5f;
        sound.maxDistance = 45f;
        sound.dopplerLevel = 0f;
        sound.volume = 0f;
        sound.Play();
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null) listener = player.transform;
    }

    // ---------------- drinking ---------------- //

    public float DistanceToWater(Vector3 p) => DistanceToWater(p, out _);

    public float DistanceToWater(Vector3 p, out Vector3 nearest)
    {
        nearest = p;
        if (!Flowing || points.Count < 2) return float.MaxValue;
        float best = float.MaxValue;
        for (int i = 1; i < points.Count && lengths[i - 1] <= revealed; i++)
        {
            Vector3 a = points[i - 1], b = points[i];
            Vector3 ab = b - a; ab.y = 0f;
            Vector3 ap = p - a; ap.y = 0f;
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            Vector3 q = a + (b - a) * t;
            float d = Flat(p - q) - Mathf.Lerp(halfWidths[i - 1], halfWidths[i], t);
            if (d < best) { best = d; nearest = q; }
        }
        return best;
    }

    // ---------------- the way down ---------------- //

    bool FindWay(Vector3 from)
    {
        points.Clear();
        halfWidths.Clear();
        lengths.Clear();
        var terrain = Terrain.activeTerrain;
        var ocean = Ocean.Instance;
        if (terrain == null || ocean == null) return false;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean.Level;
        const float cell = 8f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        var height = new float[n * n];
        var blocked = new bool[n * n];
        var sea = new bool[n * n];
        var river = River.Instance;
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                Vector3 p = tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);
                float h = terrain.SampleHeight(p) + tp.y;
                height[i] = h;
                sea[i] = h < level + 0.3f && ocean.DistanceFromShore(p) > -2f;
                // not into the old river or the lake
                if (river != null && (river.DistanceToWater(p) < 14f || (river.lakeRadius > 0f && Flat(p - river.lakeCentre) < river.lakeRadius + 20f))) blocked[i] = true;
            }
        int Cell(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - tp.x) / cell), 0, n - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z - tp.z) / cell), 0, n - 1);
            return z * n + x;
        }
        int source = Cell(from);
        blocked[source] = false;
        var cost = new float[n * n];
        var back = new int[n * n];
        for (int i = 0; i < cost.Length; i++) { cost[i] = float.MaxValue; back[i] = -1; }
        var heap = new Heap();
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
                    if (nx < 1 || nz < 1 || nx >= n - 1 || nz >= n - 1) continue;
                    int j = nz * n + nx;
                    if (blocked[j]) continue;
                    float len = (dx != 0 && dz != 0 ? 1.4142f : 1f) * cell;
                    float wx = tp.x + (nx + 0.5f) * cell, wz = tp.z + (nz + 0.5f) * cell;
                    float wind = Mathf.PerlinNoise(wx * 0.013f + 41.3f, wz * 0.013f + 3.9f);
                    float step = len * (1f + 1.1f * wind) + Mathf.Max(0f, height[j] - height[i]) * 90f;
                    float total2 = c + step;
                    if (total2 < cost[j]) { cost[j] = total2; back[j] = i; heap.Push(total2, j); }
                }
        }
        if (target < 0) return false;

        var cells = new List<Vector3>();
        for (int i = target; i >= 0; i = back[i]) cells.Add(tp + new Vector3((i % n + 0.5f) * cell, 0f, (i / n + 0.5f) * cell));
        cells.Reverse();
        cells[0] = new Vector3(from.x, 0f, from.z);
        // a little way into the sea
        Vector3 outDir = cells[cells.Count - 1] - cells[Mathf.Max(0, cells.Count - 4)]; outDir.y = 0f;
        outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward;
        cells.Add(cells[cells.Count - 1] + outDir * 12f);

        var path = cells;
        for (int k = 0; k < 4; k++) path = Chaikin(path);
        path = Resample(path, 2f);
        // the spring: just below the cross, on the downhill side
        if (path.Count > 2)
        {
            Vector3 down = path[2] - path[0]; down.y = 0f;
            if (down.sqrMagnitude > 0.01f) path[0] = new Vector3(from.x, 0f, from.z) + down.normalized * 1.6f;
        }

        float run = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) run += Flat(path[i] - path[i - 1]);
            lengths.Add(run);
        }
        total = Mathf.Max(1f, run);
        for (int i = 0; i < path.Count; i++)
        {
            float t = lengths[i] / total;
            halfWidths.Add(Mathf.Lerp(0.7f, 2.4f, Mathf.Pow(t, 0.7f)));
            points.Add(new Vector3(path[i].x, Forest.GroundY(path[i]), path[i].z));
        }
        // where it meets the sea: the first point at the water
        int mouth = points.Count - 1;
        for (int i = 0; i < points.Count; i++) if (points[i].y < level + 0.2f) { mouth = i; break; }
        Mouth = points[Mathf.Max(0, mouth - 1)];
        Vector3 dir = points[Mathf.Min(points.Count - 1, mouth + 3)] - points[Mathf.Max(0, mouth - 6)]; dir.y = 0f;
        SeaDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : outDir;
        return true;
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
            while (d <= len)
            {
                r.Add(Vector3.Lerp(a, b, d / Mathf.Max(0.0001f, len)));
                d += step;
            }
            carry = len - (d - step);
        }
        if (Flat(r[r.Count - 1] - p[p.Count - 1]) > step * 0.3f) r.Add(p[p.Count - 1]);
        return r;
    }

    // ---------------- the water's mesh ---------------- //

    // a strip on the ground (5 points across, each on the ground), only as far as the water has come
    void BuildMesh()
    {
        if (mesh == null || points.Count < 2) return;
        const int across = 5;
        float level = Ocean.Instance != null ? Ocean.Instance.Level : -1000f;
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var col = new List<Color>();
        var tri = new List<int>();
        int rows = 0;
        float mouthFade = total - 14f;
        for (int i = 0; i < points.Count; i++)
        {
            if (lengths[i] > revealed + 2f) break;
            Vector3 side = Side(i);
            int a0 = Mathf.Max(0, i - 5), a1 = Mathf.Min(points.Count - 1, i + 5);
            float speed = Mathf.Clamp((points[a0].y - points[a1].y) / Mathf.Max(0.1f, lengths[a1] - lengths[a0]) * 2.5f, 0f, 0.6f); // (less white foam on the steep rock)
            float fade = Mathf.Clamp01(lengths[i] / 3f)
                * (lengths[i] > mouthFade ? 1f - Mathf.Clamp01((lengths[i] - mouthFade) / 14f) : 1f)
                * Mathf.Clamp01((revealed - lengths[i]) / 6f + 0.35f); // the front of the new water
            for (int k = 0; k < across; k++)
            {
                float u = (float)k / (across - 1);
                Vector3 p = points[i] + side * halfWidths[i] * (u * 2f - 1f);
                p.y = Mathf.Max(Forest.GroundY(p) + 0.13f, level + 0.05f);
                v.Add(p);
                uv.Add(new Vector2(u, lengths[i] / 8f));
                float edge = k == 0 || k == across - 1 ? 0.75f : 1f;
                col.Add(new Color(speed, 0f, 0f, fade * edge));
            }
            if (rows > 0)
            {
                int a = (rows - 1) * across, b = rows * across;
                for (int k = 0; k < across - 1; k++)
                {
                    tri.Add(a + k); tri.Add(b + k); tri.Add(a + k + 1);
                    tri.Add(a + k + 1); tri.Add(b + k); tri.Add(b + k + 1);
                }
            }
            rows++;
        }
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetColors(col);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    Vector3 Side(int i)
    {
        Vector3 a = points[Mathf.Max(0, i - 1)], b = points[Mathf.Min(points.Count - 1, i + 1)];
        Vector3 d = b - a; d.y = 0f;
        if (d.sqrMagnitude < 0.0001f) d = Vector3.forward;
        return Vector3.Cross(Vector3.up, d.normalized);
    }

    // a small binary heap (cost, cell)
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
                int parent = (i - 1) / 2;
                if (keys[parent] <= keys[i]) break;
                Swap(i, parent);
                i = parent;
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
                Swap(i, m);
                i = m;
            }
        }

        void Swap(int a, int b)
        {
            float k = keys[a]; keys[a] = keys[b]; keys[b] = k;
            int v = values[a]; values[a] = values[b]; values[b] = v;
        }
    }
}
