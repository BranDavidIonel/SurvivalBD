using System.Collections.Generic;
using UnityEngine;

// Brother Ioan's stone steps: where his paths climb rock too steep to walk (the way up to the cave's mouth and up the
// other mountain), he cut steps into it, and at the top of the other mountain a flat stone for the cross. Built when
// the game starts from Resources/Environment/IoanPaths.json (Tools > Nature > 5 works out where they go): one block of
// stone per step, about half a metre higher than the one before (the player steps up to 0.7 m), set into the rock on
// the uphill side and standing out over the drop. One mesh with one collider, on the Ground layer (jumping works).
public class IoanSteps : MonoBehaviour
{
    public static IoanSteps Instance { get; private set; }

    const float StepLength = 0.62f;  // along the way (the steps are half a metre apart: each a little over the next)
    const float StepWidth = 2.8f;    // across
    const float Outward = 0.5f;      // the middle of the step, out from the way's line (the rock is on the other side)
    const float MaxDepth = 9f;       // how far a block reaches down to the rock under it

    MeshCollider meshCollider;
    Mesh mesh;
    Material material;
    readonly Dictionary<long, List<Vector3>> stepCells = new Dictionary<long, List<Vector3>>(); // where the steps are (4 m cells)
    CharacterController player;
    Vector3 lastSpot;
    float blocked;

    public static void Make()
    {
        if (Instance != null) return;
        var data = IslandPaths.Data;
        if (data == null || ((data.stairs == null || data.stairs.Count == 0) && data.landingSize <= 0f)) return;
        var go = new GameObject("Brother Ioan's steps (made in code)");
        go.AddComponent<IoanSteps>().Build(data);
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }

    // the top of the steps or of the stone under this spot (float.MinValue: none here)
    public static float SurfaceAt(Vector3 p)
    {
        if (Instance == null || Instance.meshCollider == null) return float.MinValue;
        var ray = new Ray(new Vector3(p.x, p.y + 6f, p.z), Vector3.down);
        return Instance.meshCollider.Raycast(ray, out RaycastHit hit, 14f) ? hit.point.y : float.MinValue;
    }

    void Build(IslandPaths.PathsData data)
    {
        gameObject.layer = 8; // Ground: the player stands on it (and can jump)
        var v = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();
        if (data.stairs != null)
            foreach (var line in data.stairs)
            {
                if (line == null || line.points == null || line.points.Count < 2) continue;
                var p = line.points;
                for (int i = 0; i < p.Count; i++)
                {
                    Vector3 along = p[Mathf.Min(p.Count - 1, i + 1)] - p[Mathf.Max(0, i - 1)];
                    along.y = 0f;
                    if (along.sqrMagnitude < 1e-4f) continue;
                    along.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, along);
                    // out: the lower side (the rock rises on the other)
                    if (Forest.GroundY(p[i] + side * 1.5f) > Forest.GroundY(p[i] - side * 1.5f)) side = -side;
                    Vector3 centre = p[i] + side * Outward;
                    Box(v, uv, tri, centre, along, side, StepLength, StepWidth, p[i].y);
                    long key = Key(centre);
                    if (!stepCells.TryGetValue(key, out var list)) stepCells[key] = list = new List<Vector3>();
                    list.Add(centre);
                }
            }
        if (data.landingSize > 0f)
            Box(v, uv, tri, data.landing, Vector3.forward, Vector3.right, data.landingSize, data.landingSize, data.landing.y);

        mesh = new Mesh { name = "Brother Ioan's steps" };
        if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tri, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material = StoneMaterial();
        meshCollider = gameObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;
    }

    static long Key(Vector3 p) => ((long)Mathf.FloorToInt(p.x / 4f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 4f);

    bool NearSteps(Vector3 p)
    {
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!stepCells.TryGetValue(Key(p + new Vector3(dx * 4f, 0f, dz * 4f)), out var list)) continue;
                foreach (var c in list)
                {
                    Vector3 d = c - p; d.y = 0f;
                    if (d.sqrMagnitude < 3f * 3f) return true;
                }
            }
        return false;
    }

    // A helping hand on the steps: where the way turns sharply, the flight above can stand right beside the one below
    // like a wall. Pushing forward against such a step (up to 2.7 m high), the player is lifted onto it.
    void Update()
    {
        if (meshCollider == null || stepCells.Count == 0) return;
        if (player == null)
        {
            var body = FindObjectOfType<PlayerMovement>();
            if (body == null) return;
            player = body.GetComponent<CharacterController>();
            if (player == null) return;
        }
        Bounds b = player.bounds;
        Vector3 spot = b.center;
        Vector3 moved = spot - lastSpot; moved.y = 0f;
        lastSpot = spot;
        if (!player.enabled || GameInput.MoveZ < 0.3f || moved.magnitude > 2f * Time.deltaTime || !NearSteps(spot)) { blocked = 0f; return; }
        blocked += Time.deltaTime;
        if (blocked < 0.25f) return; // pushing forward a quarter of a second without moving
        blocked = 0f;
        Vector3 forward = player.transform.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 1e-4f) return;
        forward.Normalize();
        float feet = b.min.y;
        Vector3 ahead = new Vector3(spot.x, 0f, spot.z) + forward * (player.radius + 0.55f);
        if (!meshCollider.Raycast(new Ray(new Vector3(ahead.x, feet + 3.2f, ahead.z), Vector3.down), out RaycastHit hit, 3.2f)) return;
        float rise = hit.point.y - feet;
        if (rise < 0.3f || rise > 2.7f) return;
        player.enabled = false;
        player.transform.position += Vector3.up * (rise + 0.1f) + forward * 0.3f;
        player.enabled = true;
    }

    // one block: its top at "top", down to the rock under its corners (a little into it)
    static void Box(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 centre, Vector3 along, Vector3 side, float length, float width, float top)
    {
        Vector3 a = along * (length * 0.5f), s = side * (width * 0.5f);
        Vector3[] corners = { centre - a - s, centre + a - s, centre + a + s, centre - a + s };
        float bottom = top - 0.5f;
        foreach (var c in corners) bottom = Mathf.Min(bottom, Forest.GroundY(c) - 0.4f);
        bottom = Mathf.Max(bottom, top - MaxDepth);
        var up = new Vector3[4];
        var down = new Vector3[4];
        for (int k = 0; k < 4; k++)
        {
            up[k] = new Vector3(corners[k].x, top, corners[k].z);
            down[k] = new Vector3(corners[k].x, bottom, corners[k].z);
        }
        Vector3 middle = new Vector3(centre.x, (top + bottom) * 0.5f, centre.z);
        Face(v, uv, tri, up[0], up[1], up[2], up[3], middle);              // the top
        for (int k = 0; k < 4; k++)
        {
            int n = (k + 1) % 4;
            Face(v, uv, tri, down[k], down[n], up[n], up[k], middle);       // the sides
        }
    }

    // a quad a-b-c-d facing out of the block (turned round if it faced its middle), mapped in metres
    static void Face(List<Vector3> v, List<Vector2> uv, List<int> tri, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 middle)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(normal, (a + b + c + d) * 0.25f - middle) < 0f) { var t = b; b = d; d = t; normal = -normal; }
        normal.Normalize();
        Vector3 u = (b - a).sqrMagnitude > 1e-6f ? (b - a).normalized : Vector3.right;
        Vector3 w = Vector3.Cross(normal, u);
        int i = v.Count;
        foreach (var p in new[] { a, b, c, d })
        {
            v.Add(p);
            uv.Add(new Vector2(Vector3.Dot(p, u), Vector3.Dot(p, w)) * 0.45f);
        }
        tri.Add(i); tri.Add(i + 1); tri.Add(i + 2);
        tri.Add(i); tri.Add(i + 2); tri.Add(i + 3);
    }

    // the island's rock (the terrain's rock layer), a little lighter: worked stone
    static Material StoneMaterial()
    {
        var m = new Material(Shader.Find("Standard")) { name = "Brother Ioan's stone", hideFlags = HideFlags.DontSave };
        m.color = new Color(0.86f, 0.83f, 0.78f);
        m.SetFloat("_Glossiness", 0.08f);
        var terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
            foreach (var layer in terrain.terrainData.terrainLayers)
            {
                if (layer == null || !layer.name.StartsWith("Rock")) continue;
                if (layer.diffuseTexture != null) m.mainTexture = layer.diffuseTexture;
                if (layer.normalMapTexture != null)
                {
                    m.SetTexture("_BumpMap", layer.normalMapTexture);
                    m.EnableKeyword("_NORMALMAP");
                }
                break;
            }
        return m;
    }
}
