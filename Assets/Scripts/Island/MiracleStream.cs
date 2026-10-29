using System.Collections.Generic;
using UnityEngine;

// The miracle of Brother Ioan's canon (level 9): when the cross is raised on the top of the other mountain, water
// springs from the rock under it and a new stream runs down to the sea. Fresh water: the player can drink from it.
// Where it flows into the sea the raft of the last level is built (Raft.MakeSite).
//  - its way down: where water really runs, down the mountain's gullies and along the low ground of the plain to the
//    sea (Route: every spot drains to its lowest neighbour, hollows fill up and spill over). Worked out in the editor
//    (Tools > Nature > 5. Brother Ioan's Paths And The Stream Bed), which also dug its bed into the ground (a shallow
//    stony channel, dry until the miracle) and saved the way in Resources/Environment/MiracleStreamRoute.json; without
//    that file it is worked out here the same way
//  - the water lies level across in its bed (it floated as a thin strip on the slopes before), with soft edges under
//    the banks, white where it falls steeply; it uses the river's water, a little clearer
// Made in code when the game scene loads (EnvironmentBootstrap). Nothing shows until the cross stands.
public class MiracleStream : MonoBehaviour
{
    public static MiracleStream Instance { get; private set; }

    [Tooltip("How fast the new water runs down when the cross is raised (metres per second)")]
    public float revealSpeed = 16f;
    public float drinkReach = 2.5f;

    public bool Flowing { get; private set; }
    public IReadOnlyList<Vector3> Points => points; // (the map draws it)

    public const string RouteResource = "Environment/MiracleStreamRoute";
    public const float SourceWidth = 0.9f, MouthWidth = 2.6f; // half of the water's width at the spring and at the sea

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
            if (!summitFound)
            {
                summitFound = true;
                var cave = Cave.Instance != null ? Cave.Instance : FindObjectOfType<Cave>();
                summit = FindSummit(Terrain.activeTerrain, River.Instance, cave);
            }
            return summit;
        }
    }

    // where it meets the sea, and which way the sea is from there
    public Vector3 Mouth { get; private set; }
    public Vector3 SeaDirection { get; private set; } = Vector3.forward;

    public static Vector3 FindSummit(Terrain terrain, River river, Cave cave)
    {
        if (terrain == null) return Vector3.zero;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        Vector3 riverSource = river != null && river.points.Count > 0 ? river.points[0] : new Vector3(1e6f, 0f, 1e6f);
        Vector3 caveMouth = cave != null && cave.path.Count > 0 ? cave.path[0] : new Vector3(1e6f, 0f, 1e6f);
        const float cell = 6f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        float best = float.MinValue;
        Vector3 top = Vector3.zero;
        for (int z = 4; z < n - 4; z++)
            for (int x = 4; x < n - 4; x++)
            {
                Vector3 p = tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);
                if (Flat(p - riverSource) < 280f || Flat(p - caveMouth) < 120f) continue;
                float h = terrain.SampleHeight(p) + tp.y;
                if (h > best) { best = h; top = new Vector3(p.x, h, p.z); }
            }
        return top;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    public static float HalfWidthAt(float along01) => Mathf.Lerp(SourceWidth, MouthWidth, Mathf.Pow(Mathf.Clamp01(along01), 0.7f));

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

    // the river's water, a little clearer and lighter (a shallow mountain brook over stones)
    static Material WaterMaterial()
    {
        var river = River.Instance;
        var r = river != null ? river.GetComponent<MeshRenderer>() : null;
        Material m;
        if (r != null && r.sharedMaterial != null) m = new Material(r.sharedMaterial);
        else
        {
            var shader = Shader.Find("SurvivalBD/River");
            m = new Material(shader != null ? shader : Shader.Find("Standard"));
        }
        m.name = "Miracle stream";
        m.hideFlags = HideFlags.DontSave;
        if (m.HasProperty("_Deep")) m.SetColor("_Deep", new Color(0.1f, 0.27f, 0.29f, 1f));
        if (m.HasProperty("_Shallow")) m.SetColor("_Shallow", new Color(0.3f, 0.45f, 0.38f, 1f));
        if (m.HasProperty("_Alpha")) m.SetFloat("_Alpha", 0.74f);
        if (m.HasProperty("_Speed")) m.SetFloat("_Speed", 0.45f);
        return m;
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

    [System.Serializable]
    public class RouteData { public List<Vector3> points = new List<Vector3>(); }

    static List<Vector3> savedRoute;
    static bool savedLoaded;

    // the way worked out in the editor (its bed is dug into the ground along it); null: none saved
    public static List<Vector3> SavedRoute()
    {
        if (!savedLoaded)
        {
            savedLoaded = true;
            var text = Resources.Load<TextAsset>(RouteResource);
            if (text != null)
            {
                var data = JsonUtility.FromJson<RouteData>(text.text);
                if (data != null && data.points != null && data.points.Count > 3) savedRoute = data.points;
            }
        }
        return savedRoute;
    }

    public static void ForgetSavedRoute() { savedLoaded = false; savedRoute = null; }

    float[] surfaceY, steepness;

    bool FindWay(Vector3 from)
    {
        points.Clear();
        halfWidths.Clear();
        lengths.Clear();
        var terrain = Terrain.activeTerrain;
        var ocean = Ocean.Instance;
        if (terrain == null || ocean == null) return false;
        List<Vector3> path = null;
        var saved = SavedRoute();
        if (saved != null)
        {
            // the cross may stand anywhere on the top: from it the water joins the saved way at its nearest point
            int nearest = -1;
            float best = 45f;
            for (int i = 0; i < Mathf.Min(saved.Count, 80); i++)
            {
                float d = Flat(saved[i] - from);
                if (d < best) { best = d; nearest = i; }
            }
            if (nearest >= 0)
            {
                path = new List<Vector3>();
                Vector3 a = new Vector3(from.x, 0f, from.z), b = saved[nearest];
                int steps = Mathf.Max(1, Mathf.CeilToInt(Flat(b - a) / 2f));
                for (int s = 0; s < steps; s++) path.Add(Vector3.Lerp(a, new Vector3(b.x, 0f, b.z), (float)s / steps));
                for (int i = nearest; i < saved.Count; i++) path.Add(saved[i]);
            }
        }
        if (path == null) path = Route(from, terrain, ocean, River.Instance);
        if (path == null || path.Count < 3) return false;

        // the spring: just below the cross, on the downhill side
        Vector3 down = path[2] - path[0]; down.y = 0f;
        if (down.sqrMagnitude > 0.01f) path[0] = new Vector3(from.x, 0f, from.z) + down.normalized * 1.6f;

        float run = 0f;
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0) run += Flat(path[i] - path[i - 1]);
            lengths.Add(run);
        }
        total = Mathf.Max(1f, run);
        for (int i = 0; i < path.Count; i++)
        {
            halfWidths.Add(HalfWidthAt(lengths[i] / total));
            points.Add(new Vector3(path[i].x, Forest.GroundY(path[i]), path[i].z));
        }
        float level = ocean.Level;
        // the water's surface: in its bed, a little over the bottom, never higher than upstream; how steeply it falls
        int count = points.Count;
        surfaceY = new float[count];
        steepness = new float[count];
        for (int i = 0; i < count; i++)
        {
            int a0 = Mathf.Max(0, i - 3), a1 = Mathf.Min(count - 1, i + 3);
            steepness[i] = Mathf.Max(0f, (points[a0].y - points[a1].y) / Mathf.Max(0.1f, lengths[a1] - lengths[a0]));
            surfaceY[i] = Mathf.Max(points[i].y + 0.3f, level + 0.05f);
            if (i > 0) surfaceY[i] = Mathf.Min(surfaceY[i], surfaceY[i - 1]);
        }
        // where it meets the sea: the first point at the water
        int mouth = count - 1;
        // (by the shore, not by the height: the flat land is only a little over the sea and the dug bed goes under it)
        for (int i = 0; i < count; i++) if (ocean.DistanceFromShore(points[i]) > -3f) { mouth = i; break; }
        Mouth = points[Mathf.Max(0, mouth - 1)];
        Vector3 dir = points[Mathf.Min(count - 1, mouth + 3)] - points[Mathf.Max(0, mouth - 6)]; dir.y = 0f;
        SeaDirection = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.forward;
        return true;
    }

    // Where water from this spot really runs to the sea (also used by the editor tool that digs the bed).
    // The sea "fills" the island from its shores, the lowest ground first, so every spot is reached from its lowest
    // neighbour (hollows fill up to where they spill over): following that back from the spring is the way the water
    // goes down, along the mountain's gullies. On the flat land a gentle made-up unevenness makes it wind like a brook.
    // Not into the old river or the lake.
    public static List<Vector3> Route(Vector3 from, Terrain terrain, Ocean ocean, River river)
    {
        if (terrain == null || ocean == null) return null;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean.Level;
        const float cell = 4f;
        int n = Mathf.RoundToInt(data.size.x / cell);
        var h = new float[n * n];
        var sea = new bool[n * n];
        var blocked = new bool[n * n];
        for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                Vector3 p = tp + new Vector3((x + 0.5f) * cell, 0f, (z + 0.5f) * cell);
                float g = terrain.SampleHeight(p) + tp.y;
                float wobble = (Mathf.PerlinNoise(p.x * 0.012f + 17.3f, p.z * 0.012f + 4.1f) - 0.5f) * 0.9f
                             + (Mathf.PerlinNoise(p.x * 0.041f + 3.7f, p.z * 0.041f + 9.2f) - 0.5f) * 0.35f;
                h[i] = g + wobble;
                sea[i] = g < level + 0.3f && ocean.DistanceFromShore(p) > -2f;
                if (river != null && (river.DistanceToWater(p) < 16f || (river.lakeRadius > 0f && Flat(p - river.lakeCentre) < river.lakeRadius + 24f)))
                    blocked[i] = true;
            }
        int CellOf(Vector3 p)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt((p.x - tp.x) / cell), 0, n - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt((p.z - tp.z) / cell), 0, n - 1);
            return z * n + x;
        }
        int source = CellOf(from);
        blocked[source] = false;
        var downTo = new int[n * n];
        var seen = new bool[n * n];
        var heap = new Heap();
        for (int i = 0; i < sea.Length; i++) if (sea[i]) { seen[i] = true; downTo[i] = -1; heap.Push(h[i], i); }
        while (heap.Count > 0)
        {
            heap.Pop(out float f, out int i);
            int x = i % n, z = i / n;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 1 || nz < 1 || nx >= n - 1 || nz >= n - 1) continue;
                    int j = nz * n + nx;
                    if (seen[j] || blocked[j]) continue;
                    seen[j] = true;
                    downTo[j] = i;
                    heap.Push(Mathf.Max(h[j], f + 0.002f), j);
                }
        }
        if (!seen[source]) return null;
        var cells = new List<Vector3>();
        for (int i = source, guard = 0; i >= 0 && guard < n * 4; guard++)
        {
            cells.Add(tp + new Vector3((i % n + 0.5f) * cell, 0f, (i / n + 0.5f) * cell));
            if (sea[i]) break;
            i = downTo[i];
        }
        if (cells.Count < 2) return null;
        cells[0] = new Vector3(from.x, 0f, from.z);
        // a little way into the sea
        Vector3 outDir = cells[cells.Count - 1] - cells[Mathf.Max(0, cells.Count - 4)]; outDir.y = 0f;
        outDir = outDir.sqrMagnitude > 0.01f ? outDir.normalized : Vector3.forward;
        cells.Add(cells[cells.Count - 1] + outDir * 12f);
        var path = cells;
        for (int k = 0; k < 4; k++) path = Chaikin(path);
        path = Resample(path, 2f);
        for (int i = 0; i < path.Count; i++) path[i] = new Vector3(path[i].x, terrain.SampleHeight(path[i]) + tp.y, path[i].z);
        return path;
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

    // A strip of 7 points across, only as far as the water has come. In its bed the water lies level across (it goes
    // a little under the banks, with soft edges); where it falls steeply it follows the rock, narrower and white.
    void BuildMesh()
    {
        if (mesh == null || points.Count < 2 || surfaceY == null) return;
        const int across = 7;
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
            float steep = steepness[i];
            bool falling = steep > 0.42f;
            float reach = halfWidths[i] * (falling ? 0.72f : 1f) + (falling ? 0.25f : 0.9f);
            float fast = falling ? 0.8f : Mathf.Clamp(steep * 1.5f, 0.06f, 0.5f);
            float fade = Mathf.Clamp01(lengths[i] / 3f)
                * (lengths[i] > mouthFade ? 1f - Mathf.Clamp01((lengths[i] - mouthFade) / 14f) : 1f)
                * Mathf.Clamp01((revealed - lengths[i]) / 6f + 0.35f); // the front of the new water
            for (int k = 0; k < across; k++)
            {
                float u = (float)k / (across - 1);
                Vector3 p = points[i] + side * reach * (u * 2f - 1f);
                float g = Forest.GroundY(p);
                p.y = falling ? g + 0.14f : Mathf.Min(surfaceY[i], g + 0.24f);
                p.y = Mathf.Max(p.y, level + 0.05f);
                v.Add(p);
                uv.Add(new Vector2(u, lengths[i] / 8f));
                float edge = k == 0 || k == across - 1 ? 0f : (k == 1 || k == across - 2 ? 0.8f : 1f);
                col.Add(new Color(fast, 0f, 0f, fade * edge));
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
    public class Heap
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
