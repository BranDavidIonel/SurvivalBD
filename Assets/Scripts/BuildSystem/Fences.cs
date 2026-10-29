using System.Collections.Generic;
using UnityEngine;

// A fence of plank panels, with a gate (Crafting > Construction): a yard for the rabbits (later maybe for other
// animals too), as big as the player wants and of any shape, closed or not. No foundation and no roof: every panel
// stands on the ground (its planks follow the ground under it), the next panel joins the free end of the last one,
// and a panel that ends near another free end closes the yard (it gets just as long as needed).
//  - "fence" (2 planks + 2 sticks): a 4 m panel; "fencegate" (2 planks + 1 rope): a panel with a gate (PenDoor: look at
//    it and click / "Open" to open or shut it; while it stands open the rabbits find the way out)
//  - the fence stops the rabbits like the walls do (Buildings.Blocks / IsEnclosed); in a closed yard they multiply
//  - saved with the other buildings (Buildings.GetData / SetData), with each panel's length
// Placing: FencePlacing. The models are made here in code from boxes with the walls' planks.
public static class Fences
{
    public const string FenceItem = "fence", GateItem = "fencegate";
    public const float Length = 4f;      // one panel (the player is 3.7 m tall)
    public const float Height = 1.85f;   // rabbits can't jump it
    public const float MinLength = 1.5f, MaxLength = 5.5f;
    const float GateInset = 0.2f;        // the gate's posts

    public static bool IsFence(BuildingKind kind) => kind == BuildingKind.Fence || kind == BuildingKind.FenceGate;

    // ---------------- where the panels are ---------------- //

    // the two ends of a panel, on the ground line through its middle
    public static void Ends(Transform t, float length, out Vector3 a, out Vector3 b)
    {
        Vector3 along = t.forward; along.y = 0f;
        along = along.sqrMagnitude > 0.0001f ? along.normalized : Vector3.forward;
        a = t.position - along * length * 0.5f;
        b = t.position + along * length * 0.5f;
    }

    // the panels' ends where no other panel joins (the next one can start there)
    public static List<(Vector3 at, Vector3 outward, BuildingPiece piece)> FreeEnds()
    {
        var ends = new List<(Vector3, Vector3, BuildingPiece)>();
        var all = new List<(Vector3 at, Vector3 outward, BuildingPiece piece)>();
        foreach (var p in BuildingPiece.All)
        {
            if (p == null || !IsFence(p.kind)) continue;
            Ends(p.transform, p.length, out Vector3 a, out Vector3 b);
            Vector3 d = (b - a); d.y = 0f; d.Normalize();
            all.Add((a, -d, p));
            all.Add((b, d, p));
        }
        for (int i = 0; i < all.Count; i++)
        {
            bool joined = false;
            for (int j = 0; j < all.Count && !joined; j++)
                if (j != i && (i ^ 1) != j && Flat(all[i].at - all[j].at) < 0.35f) joined = true;
            if (!joined) ends.Add(all[i]);
        }
        return ends;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // A closed yard: a gate, and fence all around the middle of the panels near it (or around a spot inside the gate).
    public static bool YardBuilt()
    {
        foreach (var gate in BuildingPiece.All)
        {
            if (gate == null || gate.kind != BuildingKind.FenceGate) continue;
            Vector3 g = gate.transform.position;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var p in BuildingPiece.All)
            {
                if (p == null || !IsFence(p.kind) || Flat(p.transform.position - g) > 70f) continue;
                sum += p.transform.position;
                n++;
            }
            if (n < 3) continue;
            if (Buildings.IsClosedAll(sum / n, 80f, true)) return true;
            Vector3 side = gate.transform.right; side.y = 0f; side.Normalize();
            if (Buildings.IsClosedAll(g + side * 2.5f, 80f, true) || Buildings.IsClosedAll(g - side * 2.5f, 80f, true)) return true;
        }
        return false;
    }

    // ---------------- the models ---------------- //

    // A new panel standing at centre (on the ground), turned to yaw. solid: its colliders on (off for the one being
    // placed). The planks follow the ground under it.
    public static GameObject Create(BuildingKind kind, Vector3 centre, float yaw, float length, bool solid)
    {
        bool gate = kind == BuildingKind.FenceGate;
        var go = new GameObject(gate ? "Fence gate" : "Fence");
        go.transform.SetPositionAndRotation(centre, Quaternion.Euler(0f, yaw, 0f));
        length = Mathf.Clamp(length, MinLength, MaxLength);
        if (gate) BuildGate(go, length, solid); else BuildFence(go, length, solid);
        return go;
    }

    // the ground under the panel at its local z, measured from the panel's middle
    static float Ground(Transform t, float z) => Forest.GroundY(t.TransformPoint(new Vector3(0f, 0f, z))) - t.position.y;

    static float Jitter(Transform t, int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + t.position.x * 0.731f + t.position.z * 0.419f) * 43758.5453f, 1f);

    static void BuildFence(GameObject go, float length, bool solid)
    {
        var t = go.transform;
        var m = new Maker();
        float half = length * 0.5f;
        float low = float.MaxValue, high = float.MinValue;
        // the planks, side by side, each standing on the ground under it (a hand-made look: not all the same height)
        float inner = length - 0.3f;
        int pickets = Mathf.Max(3, Mathf.RoundToInt(inner / 0.36f));
        float pitch = inner / pickets;
        for (int i = 0; i < pickets; i++)
        {
            float z = -half + 0.15f + pitch * (i + 0.5f);
            float g = Ground(t, z);
            low = Mathf.Min(low, g); high = Mathf.Max(high, g);
            float top = g + Height - 0.12f + (Jitter(t, i) - 0.5f) * 0.1f, bottom = g - 0.3f;
            m.Box(0, new Vector3(0f, (top + bottom) * 0.5f, z), new Vector3(0.07f, top - bottom, pitch - 0.05f));
            // a pointed top
            m.Roof(0, new Vector3(0f, top, z), 0.07f, pitch - 0.05f, 0.12f);
        }
        // a post at each end and two rails along one side (they follow the slope)
        float ga = Ground(t, -half + 0.08f), gb = Ground(t, half - 0.08f);
        low = Mathf.Min(low, Mathf.Min(ga, gb)); high = Mathf.Max(high, Mathf.Max(ga, gb));
        foreach (var (z, g) in new[] { (-half + 0.08f, ga), (half - 0.08f, gb) })
            m.Box(1, new Vector3(0f, g + (Height + 0.1f - 0.5f) * 0.5f, z), new Vector3(0.18f, Height + 0.1f + 0.5f, 0.16f));
        foreach (float h in new[] { 0.45f, Height - 0.42f })
            m.Beam(1, new Vector3(0.075f, ga + h, -half + 0.1f), new Vector3(0.075f, gb + h, half - 0.1f), 0.06f, 0.14f);
        AddModel(go, m.Make("Fence panel"));

        var box = go.AddComponent<BoxCollider>();
        float bottomY = low - 0.6f, topY = high + Height;
        box.center = new Vector3(0f, (bottomY + topY) * 0.5f, 0f);
        box.size = new Vector3(0.24f, topY - bottomY, length);
        box.enabled = solid;
    }

    static void BuildGate(GameObject go, float length, bool solid)
    {
        var t = go.transform;
        float half = length * 0.5f;
        var frame = new Maker();
        float ga = Ground(t, -half + GateInset * 0.5f), gb = Ground(t, half - GateInset * 0.5f);
        // two thick posts and a sill log between them
        foreach (var (z, g) in new[] { (-half + GateInset * 0.5f, ga), (half - GateInset * 0.5f, gb) })
            frame.Box(1, new Vector3(0f, g + (Height + 0.3f - 0.6f) * 0.5f, z), new Vector3(0.24f, Height + 0.3f + 0.6f, GateInset));
        frame.Beam(1, new Vector3(0f, ga + 0.02f, -half + GateInset), new Vector3(0f, gb + 0.02f, half - GateInset), 0.22f, 0.16f);
        AddModel(go, frame.Make("Gate posts"));
        foreach (var (z, g) in new[] { (-half + GateInset * 0.5f, ga), (half - GateInset * 0.5f, gb) })
        {
            var post = go.AddComponent<BoxCollider>();
            post.center = new Vector3(0f, g + (Height + 0.3f - 1.2f) * 0.5f, z);
            post.size = new Vector3(0.3f, Height + 0.3f + 1.2f, GateInset + 0.06f);
            post.enabled = solid;
        }

        // the gate turns on its hinge at the first post (z = -length / 2 + inset): PenDoor turns "Door"
        var hinge = new GameObject("Door").transform;
        hinge.SetParent(t, false);
        hinge.localPosition = new Vector3(0f, 0f, -half + GateInset + 0.02f);
        float width = length - 2f * GateInset - 0.06f;
        float groundHigh = float.MinValue, groundLow = float.MaxValue;
        for (int i = 0; i <= 6; i++)
        {
            float g = Ground(t, -half + GateInset + width * i / 6f);
            groundHigh = Mathf.Max(groundHigh, g);
            groundLow = Mathf.Min(groundLow, g);
        }
        float bottom = groundHigh + 0.05f, top = bottom + Height - 0.15f;
        var leaf = new Maker();
        int boards = Mathf.Max(3, Mathf.RoundToInt(width / 0.34f));
        float pitch = width / boards;
        for (int i = 0; i < boards; i++)
        {
            float z = pitch * (i + 0.5f);
            leaf.Box(0, new Vector3(0f, (top + bottom) * 0.5f, z), new Vector3(0.07f, top - bottom, pitch - 0.04f));
        }
        foreach (float x in new[] { -0.075f, 0.075f })
        {
            leaf.Box(1, new Vector3(x, bottom + 0.4f, width * 0.5f), new Vector3(0.06f, 0.16f, width - 0.06f));
            leaf.Box(1, new Vector3(x, top - 0.4f, width * 0.5f), new Vector3(0.06f, 0.16f, width - 0.06f));
            leaf.Beam(1, new Vector3(x, bottom + 0.48f, 0.12f), new Vector3(x, top - 0.48f, width - 0.12f), 0.06f, 0.14f);
        }
        var leafObject = AddModel(hinge.gameObject, leaf.Make("Gate leaf"), "Leaf");
        var leafBox = leafObject.AddComponent<BoxCollider>();
        // (down to the lowest ground under it: no gap for a rabbit to slip under)
        float boxBottom = Mathf.Min(bottom, groundLow + 0.25f);
        leafBox.center = new Vector3(0f, (top + boxBottom) * 0.5f, width * 0.5f);
        leafBox.size = new Vector3(0.2f, top - boxBottom, width);
        leafBox.enabled = solid;
        // a rope loop to pull it
        foreach (float x in new[] { -0.12f, 0.12f })
        {
            var ring = new GameObject("Rope handle");
            ring.transform.SetParent(hinge, false);
            ring.transform.localPosition = new Vector3(x, bottom + Height * 0.55f, width - 0.3f);
            ring.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            ring.AddComponent<MeshFilter>().sharedMesh = Buildings.RingMesh(0.16f, 0.025f);
            ring.AddComponent<MeshRenderer>().sharedMaterial = Buildings.Cord;
        }
    }

    static GameObject AddModel(GameObject owner, Mesh mesh, string childName = null)
    {
        var go = owner;
        if (childName != null)
        {
            go = new GameObject(childName);
            go.transform.SetParent(owner.transform, false);
        }
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterials = new[] { Buildings.Wood, Posts };
        return go;
    }

    // the posts and the rails: a darker, older wood than the planks
    static Material posts;
    static Material Posts
    {
        get
        {
            if (posts == null)
            {
                posts = new Material(Buildings.Wood) { name = "Fence posts", hideFlags = HideFlags.DontSave };
                posts.color = new Color(0.62f, 0.47f, 0.34f);
                if (posts.HasProperty("_Glossiness")) posts.SetFloat("_Glossiness", 0.06f);
            }
            return posts;
        }
    }

    // the meshes of the panels (each panel has its own: its planks follow the ground)
    public static void DestroyMeshes(GameObject go)
    {
        if (go == null) return;
        foreach (var f in go.GetComponentsInChildren<MeshFilter>(true))
            if (f.sharedMesh != null && f.sharedMesh.name != "Ring") Object.Destroy(f.sharedMesh);
    }

    // boxes and beams in two parts (0: planks, 1: posts and rails)
    class Maker
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] tris = { new List<int>(), new List<int>() };

        public void Box(int part, Vector3 c, Vector3 size) => Oriented(part, c, Vector3.right, Vector3.up, Vector3.forward, size * 0.5f);

        // a beam from a to b (a rail that follows the slope)
        public void Beam(int part, Vector3 a, Vector3 b, float thick, float tall)
        {
            Vector3 along = b - a;
            float length = along.magnitude;
            if (length < 0.01f) return;
            along /= length;
            Vector3 side = Vector3.Cross(Vector3.up, along);
            if (side.sqrMagnitude < 0.001f) side = Vector3.right;
            side.Normalize();
            Vector3 up = Vector3.Cross(along, side).normalized;
            Oriented(part, (a + b) * 0.5f, side, up, along, new Vector3(thick * 0.5f, tall * 0.5f, length * 0.5f));
        }

        // the pointed top of a plank (a small gable)
        public void Roof(int part, Vector3 at, float thick, float wide, float tall)
        {
            Vector3 peak = at + Vector3.up * tall;
            float x = thick * 0.5f, z = wide * 0.5f;
            Vector3 a = at + new Vector3(-x, 0f, -z), b = at + new Vector3(x, 0f, -z), c = at + new Vector3(x, 0f, z), d = at + new Vector3(-x, 0f, z);
            Vector3 p1 = peak + new Vector3(-x, 0f, 0f), p2 = peak + new Vector3(x, 0f, 0f);
            Quad(part, a, p1, p2, b);  // the two slopes
            Quad(part, d, c, p2, p1);
            Quad(part, a, d, p1, p1);  // the two ends (triangles: one corner twice)
            Quad(part, b, p2, c, c);
        }

        void Oriented(int part, Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 h)
        {
            Face(part, c + r * h.x, r, f * h.z, u * h.y);
            Face(part, c - r * h.x, -r, f * h.z, u * h.y);
            Face(part, c + u * h.y, u, r * h.x, f * h.z);
            Face(part, c - u * h.y, -u, r * h.x, f * h.z);
            Face(part, c + f * h.z, f, r * h.x, u * h.y);
            Face(part, c - f * h.z, -f, r * h.x, u * h.y);
        }

        // one side of a box: corners mid +- a +- b, facing normal
        void Face(int part, Vector3 mid, Vector3 normal, Vector3 a, Vector3 b)
        {
            if (Vector3.Dot(Vector3.Cross(a, b), normal) < 0f) { var s = a; a = b; b = s; }
            int start = v.Count;
            Vector3 ua = a.normalized, ub = b.normalized;
            foreach (var p in new[] { mid - a - b, mid + a - b, mid + a + b, mid - a + b })
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(new Vector2(Vector3.Dot(p, ua), Vector3.Dot(p, ub)) / 2.5f);
            }
            var t = tris[part];
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
            t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }

        void Quad(int part, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Vector3 normal = Vector3.Cross(p1 - p0, p2 - p0);
            if (normal.sqrMagnitude < 1e-8f) normal = Vector3.Cross(p2 - p0, p3 - p0);
            normal.Normalize();
            int start = v.Count;
            foreach (var p in new[] { p0, p1, p2, p3 })
            {
                v.Add(p);
                n.Add(normal);
                uv.Add(new Vector2(p.z + p.x, p.y) / 2.5f);
            }
            var t = tris[part];
            // (the corners are given so that this side faces out: the normal above)
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
            t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }

        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(tris[0], 0);
            mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
