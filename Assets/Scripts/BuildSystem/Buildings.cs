using System.Collections.Generic;
using UnityEngine;

// What can be built on a foundation (Crafting > Construction), and what the built pieces mean for the rabbits:
//  - Foundation and Wall: the old prefabs (Resources/FoundationModel, WallModel)
//  - Door wall, Window wall, Roof: made here in code from boxes (the same planks as the wall). A door wall and a
//    window wall go where a wall goes (on a wall's place around a foundation), a roof goes on top of a foundation's
//    walls (its place is a "roof ghost" every foundation gets when it is built).
//  - walls, door walls (with the door shut) and window walls stop the rabbits; rabbits walk on the foundations
//  - the save keeps every built piece and whether the doors are open (before, a loaded game had no buildings).
//  - Fence and FenceGate (Fences.cs): panels standing on the ground, no foundation: a yard of any size
public enum BuildingKind { Foundation, Wall, DoorWall, WindowWall, Roof, Fence, FenceGate }

public static class Buildings
{
    public const string DoorWallModel = "DoorWallModel", WindowWallModel = "WindowWallModel", RoofModel = "RoofModel";

    // A wall is 5 m wide, 5 m tall and 0.5 m thick (like WallModel). Seen from its own middle: x = thickness,
    // y = height, z = width.
    const float W = 5f, H = 5f, T = 0.5f;
    // the top of the walls, above the middle of their foundation: the roof sits there
    public const float WallTop = 5.2f;
    // the doorway (the player is 3.7 m tall) and the window
    const float DoorWidth = 1.8f, DoorHeight = 4.2f;
    const float WindowWidth = 2f, WindowBottom = 2.1f, WindowTop = 3.5f;

    public static bool IsCodeModel(string model) => model == DoorWallModel || model == WindowWallModel || model == RoofModel;
    public static bool IsWallModel(string model) => model == "WallModel" || model == DoorWallModel || model == WindowWallModel;

    // ---------------- the pieces in the world ---------------- //

    public static int Count(BuildingKind kind)
    {
        int n = 0;
        foreach (var p in BuildingPiece.All) if (p != null && p.kind == kind) n++;
        return n;
    }

    public static bool AnyNear(Vector3 p, float distance)
    {
        float d2 = (distance + 4f) * (distance + 4f); // (a piece is up to 5 m wide: measured from its middle)
        foreach (var piece in BuildingPiece.All)
        {
            if (piece == null) continue;
            Vector3 d = piece.transform.position - p; d.y = 0f;
            if (d.sqrMagnitude < d2) return true;
        }
        return false;
    }

    static readonly RaycastHit[] hits = new RaycastHit[16];

    // Is a wall (or a shut door) between these two points, at this height above the ground? (rabbits walking)
    public static bool Blocks(Vector3 from, Vector3 to, float height = 0.5f)
    {
        if (BuildingPiece.All.Count == 0) return false;
        Vector3 a = from + Vector3.up * height, b = to + Vector3.up * height;
        Vector3 d = b - a;
        float length = d.magnitude;
        if (length < 0.001f) return false;
        if (!AnyNear(from, length + 1f)) return false;
        int n = Physics.RaycastNonAlloc(a, d / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++) if (IsBlocking(hits[i].collider)) return true;
        return false;
    }

    static bool IsBlocking(Collider c)
    {
        if (c == null) return false;
        var piece = c.GetComponentInParent<BuildingPiece>();
        return piece != null && piece.BlocksAnimals;
    }

    // The ground for a rabbit: the island, or the floor of a foundation on it.
    public static float GroundAt(Vector3 p)
    {
        float ground = Forest.GroundY(p);
        if (BuildingPiece.All.Count == 0 || !AnyNear(p, 3f)) return ground;
        Vector3 from = new Vector3(p.x, Mathf.Max(ground, p.y) + 2.5f, p.z);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < n; i++)
        {
            var piece = hits[i].collider.GetComponentInParent<BuildingPiece>();
            if (piece != null && piece.kind == BuildingKind.Foundation && hits[i].point.y > ground) ground = hits[i].point.y;
        }
        return ground;
    }

    static readonly Vector3[] Around =
    {
        new Vector3(1f, 0f, 0f), new Vector3(0.7071f, 0f, 0.7071f), new Vector3(0f, 0f, 1f), new Vector3(-0.7071f, 0f, 0.7071f),
        new Vector3(-1f, 0f, 0f), new Vector3(-0.7071f, 0f, -0.7071f), new Vector3(0f, 0f, -1f), new Vector3(0.7071f, 0f, -0.7071f),
    };

    // Inside a pen or a fenced yard: walls / fence all around (looking in 8 directions, at least 7 meet one: an open
    // doorway or gate lets one through). Outside, next to a pen, at most half of the directions meet it.
    // Worked out on the map (the walls and panels as lines seen from above), not with rays at one height: in a big
    // yard on a slope a ray from a rabbit went under the fence on the higher side, or into the hill.
    // doorsShut: count open doors and gates as shut (is the yard closed at all?).
    public static bool IsEnclosed(Vector3 p, float reach = 45f, bool doorsShut = false)
    {
        if (BuildingPiece.All.Count == 0) return false;
        var lines = Lines(p, reach + 6f, doorsShut);
        if (lines.Count < 2) return false;
        int walls = 0;
        for (int k = 0; k < Around.Length; k++)
        {
            Vector2 o = new Vector2(p.x, p.z), d = new Vector2(Around[k].x, Around[k].z);
            foreach (var l in lines) if (RayMeets(o, d, reach, l.a, l.b)) { walls++; break; }
            if (walls + (Around.Length - 1 - k) < 7) return false; // can't reach 7 any more
        }
        return walls >= 7;
    }

    // Closed all around, with no gap at all (a missing panel, an open door): in 24 directions every look meets a
    // wall, a fence panel or a shut door. (IsEnclosed above lets one gap through: it says where a rabbit is.)
    public static bool IsClosedAll(Vector3 p, float reach = 80f, bool doorsShut = false)
    {
        if (BuildingPiece.All.Count == 0) return false;
        var lines = Lines(p, reach + 6f, doorsShut);
        if (lines.Count < 3) return false;
        Vector2 o = new Vector2(p.x, p.z);
        for (int k = 0; k < 24; k++)
        {
            float a = k * Mathf.PI * 2f / 24f;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            bool hit = false;
            foreach (var l in lines) if (RayMeets(o, d, reach, l.a, l.b)) { hit = true; break; }
            if (!hit) return false;
        }
        return true;
    }

    static readonly List<(Vector2 a, Vector2 b)> lineList = new List<(Vector2, Vector2)>();

    // the walls and fence panels near p that stop animals, as lines on the map
    static List<(Vector2 a, Vector2 b)> Lines(Vector3 p, float near, bool doorsShut)
    {
        lineList.Clear();
        foreach (var piece in BuildingPiece.All)
        {
            if (piece == null || !piece.BlocksAnimals) continue;
            var t = piece.transform;
            Vector3 gap = t.position - p; gap.y = 0f;
            float half = piece.HalfWidth;
            if (gap.magnitude > near + half) continue;
            Vector3 along = t.forward; along.y = 0f;
            if (along.sqrMagnitude < 0.0001f) continue;
            along.Normalize();
            var door = piece.kind == BuildingKind.DoorWall || piece.kind == BuildingKind.FenceGate ? piece.GetComponent<PenDoor>() : null;
            bool open = door != null && door.IsOpen && !doorsShut;
            if (!open) { AddLine(t.position, along, -half, half); continue; }
            // an open doorway: only the wall on its two sides
            float doorway = piece.kind == BuildingKind.DoorWall ? DoorWidth * 0.5f : half - 0.2f;
            AddLine(t.position, along, -half, -doorway);
            AddLine(t.position, along, doorway, half);
        }
        return lineList;
    }

    static void AddLine(Vector3 c, Vector3 along, float from, float to)
    {
        Vector3 a = c + along * from, b = c + along * to;
        lineList.Add((new Vector2(a.x, a.z), new Vector2(b.x, b.z)));
    }

    // a ray from o along d (length reach) crosses the line a-b
    static bool RayMeets(Vector2 o, Vector2 d, float reach, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a;
        float den = d.x * e.y - d.y * e.x;
        if (Mathf.Abs(den) < 1e-6f) return false;
        Vector2 w = a - o;
        float t = (w.x * e.y - w.y * e.x) / den;   // along the ray
        float s = (w.x * d.y - w.y * d.x) / den;   // along the line
        return t >= 0f && t <= reach && s >= -0.02f && s <= 1.02f;
    }

    // ---------------- building (ConstructionManager) ---------------- //

    // A piece made in code, held in front of the player while it is being built (its colliders are off until placed).
    public static GameObject Create(string model)
    {
        switch (model)
        {
            case DoorWallModel: return MakeDoorWall();
            case WindowWallModel: return MakeWindowWall();
            case RoofModel: return MakeRoof();
        }
        return null;
    }

    // ConstructionManager: a piece was built (on its place, or a foundation anywhere)
    public static void OnPlaced(GameObject item, string model)
    {
        if (item == null) return;
        var piece = item.GetComponent<BuildingPiece>();
        if (piece == null) piece = item.AddComponent<BuildingPiece>();
        piece.kind = KindOf(model);
        // the pieces made in code: all their walls are solid now (they were off while held in front of the player)
        if (IsCodeModel(model)) foreach (var c in item.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        if (piece.kind == BuildingKind.DoorWall && item.GetComponent<PenDoor>() == null)
        {
            var door = item.AddComponent<PenDoor>();
            door.hinge = item.transform.Find("Door");
        }
        if (piece.kind == BuildingKind.Foundation) AddRoofGhost(item.transform);
    }

    // FencePlacing / a loaded game: a fence panel or a gate stands
    public static void OnPlaced(GameObject item, BuildingKind kind, float length)
    {
        if (item == null) return;
        var piece = item.GetComponent<BuildingPiece>();
        if (piece == null) piece = item.AddComponent<BuildingPiece>();
        piece.kind = kind;
        piece.length = length;
        foreach (var c in item.GetComponentsInChildren<Collider>(true)) c.enabled = true;
        if (kind == BuildingKind.FenceGate && item.GetComponent<PenDoor>() == null)
        {
            var door = item.AddComponent<PenDoor>();
            door.hinge = item.transform.Find("Door");
        }
    }

    static BuildingKind KindOf(string model)
    {
        switch (model)
        {
            case "FoundationModel": return BuildingKind.Foundation;
            case DoorWallModel: return BuildingKind.DoorWall;
            case WindowWallModel: return BuildingKind.WindowWall;
            case RoofModel: return BuildingKind.Roof;
        }
        return BuildingKind.Wall;
    }

    static string ModelOf(BuildingKind kind)
    {
        switch (kind)
        {
            case BuildingKind.Foundation: return "FoundationModel";
            case BuildingKind.DoorWall: return DoorWallModel;
            case BuildingKind.WindowWall: return WindowWallModel;
            case BuildingKind.Roof: return RoofModel;
        }
        return "WallModel";
    }

    // the place of a roof over a foundation: an invisible box at the top of its walls (green when looked at
    // while building a roof)
    static readonly List<GameObject> roofGhosts = new List<GameObject>();

    public static GameObject AddRoofGhost(Transform foundation)
    {
        Vector3 at = foundation.position + Vector3.up * (WallTop + 0.25f);
        roofGhosts.RemoveAll(g => g == null);
        foreach (var g in roofGhosts)
        {
            Vector3 d = g.transform.position - at;
            if (d.sqrMagnitude < 1f) return g; // already one there
        }
        foreach (var piece in BuildingPiece.All)
            if (piece != null && piece.kind == BuildingKind.Roof && (piece.transform.position - (at + Vector3.down * 0.25f)).sqrMagnitude < 1f) return null;

        var ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ghost.name = "RoofGhost";
        ghost.transform.SetParent(foundation.parent, true);
        ghost.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, foundation.eulerAngles.y, 0f));
        ghost.transform.localScale = new Vector3(W, 0.5f, W);
        ghost.AddComponent<RoofGhost>();
        var item = ghost.AddComponent<GhostItem>();
        item.solidCollider = ghost.GetComponent<BoxCollider>();
        item.isPlaced = true;
        var r = ghost.GetComponent<MeshRenderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        if (ConstructionManager.Instance != null && ConstructionManager.Instance.ghostFullTransparentMat != null) r.sharedMaterial = ConstructionManager.Instance.ghostFullTransparentMat;
        roofGhosts.Add(ghost);
        return ghost;
    }

    // ---------------- save / load ---------------- //

    public static BuildingSaveData GetData()
    {
        var data = new BuildingSaveData { version = 1 };
        foreach (var piece in BuildingPiece.All)
        {
            if (piece == null) continue;
            var door = piece.GetComponent<PenDoor>();
            data.pieces.Add(new BuildingData
            {
                kind = (int)piece.kind,
                position = piece.transform.position,
                rotation = piece.transform.eulerAngles,
                open = door != null && door.IsOpen,
                openSide = door != null ? door.Side : 1,
                length = piece.length,
            });
        }
        return data;
    }

    public static void SetData(BuildingSaveData data)
    {
        if (data == null || data.version < 1 || data.pieces == null) return;
        var manager = ConstructionManager.Instance;
        Transform parent = manager != null && manager.transform.parent != null ? manager.transform.parent.parent : null;
        var foundations = new List<Transform>();
        var built = new List<BuildingPiece>();
        foreach (var d in data.pieces)
        {
            var kind = (BuildingKind)d.kind;
            if (Fences.IsFence(kind))
            {
                // a fence panel or a gate: made again on the ground where it stood
                float length = d.length > 0f ? d.length : Fences.Length;
                var fence = Fences.Create(kind, d.position, d.rotation.y, length, true);
                fence.transform.SetParent(parent, true);
                fence.tag = "placedWall";
                OnPlaced(fence, kind, length);
                var gate = fence.GetComponent<PenDoor>();
                if (gate != null && d.open) gate.SetOpen(true, d.openSide);
                continue;
            }
            string model = ModelOf(kind);
            GameObject go;
            if (IsCodeModel(model)) go = Create(model);
            else
            {
                var prefab = Resources.Load<GameObject>(model);
                if (prefab == null) continue;
                go = Object.Instantiate(prefab);
            }
            go.name = model;
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(d.position, Quaternion.Euler(d.rotation));
            var constructable = go.GetComponent<Constructable>();
            if (kind == BuildingKind.Foundation)
            {
                go.tag = "placedFoundation";
                // its places for the next foundations and the walls (like a foundation just built)
                var ghosts = new List<GameObject>();
                foreach (var ghost in go.GetComponentsInChildren<GhostItem>(true)) ghosts.Add(ghost.gameObject);
                foreach (var ghost in ghosts)
                {
                    ghost.transform.SetParent(go.transform.parent, true);
                    var g = ghost.GetComponent<GhostItem>();
                    if (g.solidCollider != null) g.solidCollider.enabled = false;
                    g.isPlaced = true;
                    if (manager != null) manager.allGhostsInExistence.Add(ghost);
                }
                if (constructable != null) constructable.enabled = false;
                foundations.Add(go.transform);
            }
            else go.tag = "placedWall";
            if (constructable != null && constructable.solidCollider != null) constructable.solidCollider.enabled = true;
            OnPlaced(go, model);
            var piece = go.GetComponent<BuildingPiece>();
            built.Add(piece);
            var door = go.GetComponent<PenDoor>();
            if (door != null && d.open) door.SetOpen(true, d.openSide);
        }

        // the places already taken: no ghost there any more
        if (manager != null)
        {
            foreach (var ghost in manager.allGhostsInExistence)
            {
                if (ghost == null || !ghost.activeSelf) continue;
                bool wallPlace = ghost.CompareTag("wallGhost");
                foreach (var piece in built)
                {
                    if (piece == null) continue;
                    bool wall = piece.kind == BuildingKind.Wall || piece.kind == BuildingKind.DoorWall || piece.kind == BuildingKind.WindowWall;
                    bool floor = piece.kind == BuildingKind.Foundation;
                    if ((wallPlace && !wall) || (!wallPlace && !floor)) continue;
                    Vector3 gap = ghost.transform.position - piece.transform.position;
                    float flat = new Vector2(gap.x, gap.z).magnitude;
                    if (flat < (wallPlace ? 0.6f : 1.2f) && Mathf.Abs(gap.y) < 1.5f) { ghost.SetActive(false); break; }
                }
            }
        }
        foreach (var f in foundations) AddRoofGhost(f);
        roofGhosts.RemoveAll(g => g == null);
        foreach (var ghost in roofGhosts)
            foreach (var piece in built)
                if (piece != null && piece.kind == BuildingKind.Roof && (piece.transform.position - ghost.transform.position).sqrMagnitude < 1f)
                    ghost.SetActive(false);
    }

    // ---------------- the models (boxes with the wall's planks) ---------------- //

    static Material wood, doorWood, thatch, cord, stickWood;

    static Constructable WallSettings
    {
        get
        {
            var wall = Resources.Load<GameObject>("WallModel");
            return wall != null ? wall.GetComponent<Constructable>() : null;
        }
    }

    public static Material Wood
    {
        get
        {
            if (wood == null)
            {
                var settings = WallSettings;
                wood = settings != null && settings.defaultMaterial != null ? settings.defaultMaterial
                    : new Material(Shader.Find("Standard")) { color = new Color(0.55f, 0.4f, 0.26f), hideFlags = HideFlags.DontSave };
            }
            return wood;
        }
    }

    static Material Tinted(Material source, string name, Color color, Vector2 tiling)
    {
        var m = new Material(source) { name = name, hideFlags = HideFlags.DontSave };
        m.color = color;
        m.mainTextureScale = tiling;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.08f);
        return m;
    }

    // dark, old wood (the crosses on the mountains)
    public static Material OldWood => oldWood != null ? oldWood : (oldWood = Tinted(Wood, "Old cross wood", new Color(0.45f, 0.31f, 0.21f), Vector2.one));
    static Material oldWood;
    static Material DoorWood => doorWood != null ? doorWood : (doorWood = Tinted(Wood, "Door planks", new Color(0.68f, 0.52f, 0.38f), Vector2.one));
    static Material Thatch => thatch != null ? thatch : (thatch = Tinted(Wood, "Thatch (hay)", new Color(0.93f, 0.8f, 0.45f), new Vector2(3f, 6f)));
    public static Material Cord
    {
        get
        {
            if (cord == null)
            {
                cord = new Material(Shader.Find("Standard")) { name = "Hemp cord", hideFlags = HideFlags.DontSave, color = new Color(0.72f, 0.62f, 0.42f) };
                cord.SetFloat("_Glossiness", 0.05f);
            }
            return cord;
        }
    }
    public static Material Stick
    {
        get
        {
            if (stickWood == null)
            {
                stickWood = new Material(Shader.Find("Standard")) { name = "Stick", hideFlags = HideFlags.DontSave, color = new Color(0.36f, 0.24f, 0.14f) };
                stickWood.SetFloat("_Glossiness", 0.1f);
            }
            return stickWood;
        }
    }

    class MeshMaker
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector3> n = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int> t = new List<int>();
        readonly float uvScale;
        public MeshMaker(float uvScale = 1f / 5f) { this.uvScale = uvScale; }

        // a box from its middle and size; the planks' picture covers 5 m like on the old wall
        public void Box(Vector3 c, Vector3 s)
        {
            Vector3 h = s * 0.5f;
            Face(c, new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), new Vector3(0, h.y, 0), 2, 1);   // +x
            Face(c, new Vector3(-h.x, 0, 0), new Vector3(0, 0, -h.z), new Vector3(0, h.y, 0), 2, 1); // -x
            Face(c, new Vector3(0, h.y, 0), new Vector3(h.x, 0, 0), new Vector3(0, 0, h.z), 0, 2);   // +y
            Face(c, new Vector3(0, -h.y, 0), new Vector3(-h.x, 0, 0), new Vector3(0, 0, h.z), 0, 2); // -y
            Face(c, new Vector3(0, 0, h.z), new Vector3(-h.x, 0, 0), new Vector3(0, h.y, 0), 0, 1);  // +z
            Face(c, new Vector3(0, 0, -h.z), new Vector3(h.x, 0, 0), new Vector3(0, h.y, 0), 0, 1);  // -z
        }

        // one side: its middle is c + normal, it spans +-right and +-up; the picture is laid on the axes a and b
        void Face(Vector3 c, Vector3 normal, Vector3 right, Vector3 up, int a, int b)
        {
            int start = v.Count;
            Vector3 mid = c + normal;
            Vector3[] corners = { mid - right - up, mid + right - up, mid + right + up, mid - right + up };
            foreach (var p in corners)
            {
                v.Add(p);
                n.Add(normal.normalized);
                uv.Add(new Vector2(p[a] * uvScale, p[b] * uvScale));
            }
            t.Add(start); t.Add(start + 2); t.Add(start + 1);
            t.Add(start); t.Add(start + 3); t.Add(start + 2);
        }

        // a slab with sloping sides (the thatch): bottom w x w, top wTop x wTop
        public void Frustum(float y0, float y1, float w, float wTop)
        {
            float a = w * 0.5f, b = wTop * 0.5f;
            Vector3[] bottom = { new Vector3(-a, y0, -a), new Vector3(a, y0, -a), new Vector3(a, y0, a), new Vector3(-a, y0, a) };
            Vector3[] top = { new Vector3(-b, y1, -b), new Vector3(b, y1, -b), new Vector3(b, y1, b), new Vector3(-b, y1, b) };
            Quad(top[0], top[3], top[2], top[1]);
            Quad(bottom[0], bottom[1], bottom[2], bottom[3]);
            for (int i = 0; i < 4; i++)
            {
                int j = (i + 1) % 4;
                Quad(bottom[i], top[i], top[j], bottom[j]);
            }
        }

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            int start = v.Count;
            Vector3 normal = Vector3.Cross(p1 - p0, p3 - p0).normalized;
            foreach (var p in new[] { p0, p1, p2, p3 })
            {
                v.Add(p);
                n.Add(normal);
                bool flat = Mathf.Abs(normal.y) > 0.7f;
                uv.Add(flat ? new Vector2(p.x * uvScale, p.z * uvScale) : new Vector2((p.x + p.z) * uvScale, p.y * uvScale * 3f));
            }
            t.Add(start); t.Add(start + 1); t.Add(start + 2);
            t.Add(start); t.Add(start + 2); t.Add(start + 3);
        }

        public Mesh Make(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }

    static GameObject NewPiece(string name, Mesh mesh, Material material)
    {
        var go = new GameObject(name);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    static GameObject Child(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition)
    {
        var go = NewPiece(name, mesh, material);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    static BoxCollider AddBox(GameObject go, Vector3 center, Vector3 size)
    {
        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
        box.enabled = false; // on when it is built
        return box;
    }

    // what ConstructionManager needs on a piece it builds (the colours of the old wall)
    static void AddConstructable(GameObject go, BoxCollider solid)
    {
        var c = go.AddComponent<Constructable>();
        var settings = WallSettings;
        c.defaultMaterial = Wood;
        c.redMaterial = settings != null ? settings.redMaterial : Wood;
        c.greenMaterial = settings != null ? settings.greenMaterial : Wood;
        c.solidCollider = solid;
    }

    // A wall with a doorway and a plank door on two rope hinges (the player opens and shuts it: PenDoor).
    static GameObject MakeDoorWall()
    {
        float side = (W - DoorWidth) * 0.5f;           // width of the wall on each side of the door
        float lintel = H - DoorHeight;
        float doorTop = -H * 0.5f + DoorHeight;
        var m = new MeshMaker();
        m.Box(new Vector3(0f, 0f, -W * 0.5f + side * 0.5f), new Vector3(T, H, side));
        m.Box(new Vector3(0f, 0f, W * 0.5f - side * 0.5f), new Vector3(T, H, side));
        m.Box(new Vector3(0f, doorTop + lintel * 0.5f, 0f), new Vector3(T, lintel, DoorWidth));
        // the door frame: two posts and a beam, standing out a little
        m.Box(new Vector3(0f, -H * 0.5f + DoorHeight * 0.5f, -DoorWidth * 0.5f - 0.09f), new Vector3(T + 0.1f, DoorHeight, 0.18f));
        m.Box(new Vector3(0f, -H * 0.5f + DoorHeight * 0.5f, DoorWidth * 0.5f + 0.09f), new Vector3(T + 0.1f, DoorHeight, 0.18f));
        m.Box(new Vector3(0f, doorTop + 0.09f, 0f), new Vector3(T + 0.1f, 0.18f, DoorWidth + 0.36f));
        var go = NewPiece(DoorWallModel, m.Make("Door wall"), Wood);
        var left = AddBox(go, new Vector3(0f, 0f, -W * 0.5f + side * 0.5f), new Vector3(T, H, side));
        AddBox(go, new Vector3(0f, 0f, W * 0.5f - side * 0.5f), new Vector3(T, H, side));
        AddBox(go, new Vector3(0f, doorTop + lintel * 0.5f, 0f), new Vector3(T, lintel, DoorWidth));
        AddConstructable(go, left);

        // the door turns on its hinge side (z = -DoorWidth / 2)
        var hinge = new GameObject("Door").transform;
        hinge.SetParent(go.transform, false);
        hinge.localPosition = new Vector3(0f, 0f, -DoorWidth * 0.5f + 0.02f);
        float leafWidth = DoorWidth - 0.06f, leafHeight = DoorHeight - 0.06f, leafBottom = -H * 0.5f + 0.03f;
        var leaf = new MeshMaker();
        int boards = 4;
        float board = leafWidth / boards;
        for (int i = 0; i < boards; i++)
            leaf.Box(new Vector3(0f, leafBottom + leafHeight * 0.5f, board * (i + 0.5f)), new Vector3(0.12f, leafHeight, board - 0.03f));
        // battens and a brace on both sides
        foreach (float x in new[] { -0.09f, 0.09f })
        {
            leaf.Box(new Vector3(x, leafBottom + 0.7f, leafWidth * 0.5f), new Vector3(0.06f, 0.26f, leafWidth - 0.1f));
            leaf.Box(new Vector3(x, leafBottom + leafHeight - 0.7f, leafWidth * 0.5f), new Vector3(0.06f, 0.26f, leafWidth - 0.1f));
            leaf.Box(new Vector3(x, leafBottom + leafHeight * 0.5f, leafWidth * 0.5f), new Vector3(0.06f, 0.22f, leafWidth - 0.1f));
        }
        var leafObject = Child(hinge, "Leaf", leaf.Make("Door leaf"), DoorWood, Vector3.zero);
        AddBox(leafObject, new Vector3(0f, leafBottom + leafHeight * 0.5f, leafWidth * 0.5f), new Vector3(0.16f, leafHeight, leafWidth));
        // a rope loop to pull it, on both sides
        foreach (float x in new[] { -0.12f, 0.12f })
        {
            var handle = Child(hinge, "Rope handle", RingMesh(0.16f, 0.025f), Cord, new Vector3(x, leafBottom + 1.65f, leafWidth - 0.3f));
            handle.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }
        // the rope hinges
        foreach (float y in new[] { leafBottom + 0.7f, leafBottom + leafHeight - 0.7f })
        {
            var rope = Child(hinge, "Rope hinge", RingMesh(0.13f, 0.03f), Cord, new Vector3(0f, y, -0.02f));
            rope.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        return go;
    }

    // A wall with a window (two sticks across it: nothing gets in or out through it).
    static GameObject MakeWindowWall()
    {
        float yb = -H * 0.5f + WindowBottom, yt = -H * 0.5f + WindowTop;
        float side = (W - WindowWidth) * 0.5f;
        var m = new MeshMaker();
        Vector3[] centers =
        {
            new Vector3(0f, (-H * 0.5f + yb) * 0.5f, 0f),                          // below
            new Vector3(0f, (yt + H * 0.5f) * 0.5f, 0f),                           // above
            new Vector3(0f, (yb + yt) * 0.5f, -W * 0.5f + side * 0.5f),            // left
            new Vector3(0f, (yb + yt) * 0.5f, W * 0.5f - side * 0.5f),             // right
        };
        Vector3[] sizes =
        {
            new Vector3(T, yb + H * 0.5f, W),
            new Vector3(T, H * 0.5f - yt, W),
            new Vector3(T, yt - yb, side),
            new Vector3(T, yt - yb, side),
        };
        for (int i = 0; i < 4; i++) m.Box(centers[i], sizes[i]);
        // the frame and the sill
        m.Box(new Vector3(0f, yb - 0.08f, 0f), new Vector3(T + 0.24f, 0.16f, WindowWidth + 0.5f));
        m.Box(new Vector3(0f, yt + 0.07f, 0f), new Vector3(T + 0.1f, 0.14f, WindowWidth + 0.3f));
        m.Box(new Vector3(0f, (yb + yt) * 0.5f, -WindowWidth * 0.5f - 0.07f), new Vector3(T + 0.1f, yt - yb, 0.14f));
        m.Box(new Vector3(0f, (yb + yt) * 0.5f, WindowWidth * 0.5f + 0.07f), new Vector3(T + 0.1f, yt - yb, 0.14f));
        var go = NewPiece(WindowWallModel, m.Make("Window wall"), Wood);
        BoxCollider first = null;
        for (int i = 0; i < 4; i++)
        {
            var box = AddBox(go, centers[i], sizes[i]);
            if (first == null) first = box;
        }
        // the sticks across the window, and an invisible pane so nobody climbs through
        var bars = new MeshMaker();
        bars.Box(new Vector3(0f, (yb + yt) * 0.5f, 0f), new Vector3(0.1f, yt - yb, 0.1f));
        bars.Box(new Vector3(0f, (yb + yt) * 0.5f, 0f), new Vector3(0.1f, 0.1f, WindowWidth));
        Child(go.transform, "Sticks", bars.Make("Window sticks"), Stick, Vector3.zero);
        AddBox(go, new Vector3(0f, (yb + yt) * 0.5f, 0f), new Vector3(0.1f, yt - yb, WindowWidth));
        AddConstructable(go, first);
        return go;
    }

    // A roof of planks with a thick layer of hay (thatch) on top, 5 x 5 m: one for every foundation.
    static GameObject MakeRoof()
    {
        var boards = new MeshMaker();
        boards.Box(new Vector3(0f, -0.19f, 0f), new Vector3(W, 0.12f, W));
        // beams under it
        foreach (float z in new[] { -1.6f, 0f, 1.6f }) boards.Box(new Vector3(0f, -0.33f, z), new Vector3(W - 0.1f, 0.16f, 0.2f));
        var go = NewPiece(RoofModel, boards.Make("Roof boards"), Wood);
        var hay = new MeshMaker(1f / 2.5f);
        hay.Frustum(-0.13f, 0.32f, W, W - 0.5f);
        Child(go.transform, "Thatch", hay.Make("Thatch"), Thatch, Vector3.zero);
        var solid = AddBox(go, new Vector3(0f, 0f, 0f), new Vector3(W, 0.55f, W));
        AddConstructable(go, solid);
        return go;
    }

    // a ring (torus) lying in the x-y plane: rope loops, the snare
    public static Mesh RingMesh(float radius, float thickness, int around = 24, int tube = 6)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int i = 0; i <= around; i++)
        {
            float a = i * Mathf.PI * 2f / around;
            Vector3 dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
            for (int j = 0; j <= tube; j++)
            {
                float b = j * Mathf.PI * 2f / tube;
                Vector3 normal = dir * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                verts.Add(dir * radius + normal * thickness);
                normals.Add(normal);
                uvs.Add(new Vector2(i / (float)around * 4f, j / (float)tube));
            }
        }
        for (int i = 0; i < around; i++)
            for (int j = 0; j < tube; j++)
            {
                int a = i * (tube + 1) + j, b = (i + 1) * (tube + 1) + j;
                tris.Add(a); tris.Add(b); tris.Add(a + 1);
                tris.Add(a + 1); tris.Add(b); tris.Add(b + 1);
            }
        var mesh = new Mesh { name = "Ring" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}

// On every built piece (added when it is built): what it is.
public class BuildingPiece : MonoBehaviour
{
    public static readonly List<BuildingPiece> All = new List<BuildingPiece>();
    public BuildingKind kind;
    public float length;   // a fence panel: how long it is (the walls are 5 m)

    // walls and fences stop the rabbits (a door wall or a gate only when shut: the open door turns away from the doorway)
    public bool BlocksAnimals => kind == BuildingKind.Wall || kind == BuildingKind.DoorWall || kind == BuildingKind.WindowWall
        || kind == BuildingKind.Fence || kind == BuildingKind.FenceGate;

    // half of its width along its forward axis (walls: 5 m; fence panels: their length)
    public float HalfWidth => Fences.IsFence(kind) ? Mathf.Max(0.5f, length) * 0.5f : 2.5f;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }
}

// marks the place of a roof (Buildings.AddRoofGhost)
public class RoofGhost : MonoBehaviour { }

[System.Serializable]
public class BuildingSaveData
{
    public int version;
    public List<BuildingData> pieces = new List<BuildingData>();
}

[System.Serializable]
public class BuildingData
{
    public int kind;          // BuildingKind
    public Vector3 position;
    public Vector3 rotation;
    public bool open;         // a door: open
    public int openSide = 1;  // which way it opened
    public float length;      // a fence panel's length (0: the usual)
}
