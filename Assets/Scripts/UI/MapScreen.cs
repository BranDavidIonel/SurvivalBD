using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The map of the island (made in code): Menu > Map, the "Map [N]" button at the top right (PC) or N.
// Drawn from the island itself the first time it is opened: the sea, the beaches, the meadows, the forests, the
// mountains with their shadows, the river, the lake and the new stream. On it: a red arrow where the player is and
// which way they look, and a pulsing gold mark on the place of the current goal (the cave, the top of the other
// mountain, the raft...), plus the places already known. The game waits while it is open.
public class MapScreen : MonoBehaviour
{
    public static MapScreen Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

    const int MapPixels = 512;
    const float MapSize = 900f;   // on the screen (1080 high)

    GameObject root;
    RawImage map;
    RectTransform mapRect, arrow, goal;
    Text goalLabel, info, title;
    readonly List<GameObject> places = new List<GameObject>();
    float oldTimeScale = 1f;
    bool menuWasOpen;
    int openedFrame;
    static Texture2D mapTexture;
    static bool streamDrawn;
    static Sprite arrowSprite;

    public static void Toggle()
    {
        if (IsOpen) Instance.Close();
        else Open();
    }

    public static void Open()
    {
        if (Instance == null) new GameObject("Map screen (made in code)").AddComponent<MapScreen>();
        Instance.Show();
    }

    void Awake()
    {
        Instance = this;
        Build();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (root != null && root.activeSelf) Time.timeScale = 1f;
    }

    void Show()
    {
        if (root.activeSelf) return;
        var stream = MiracleStream.Instance;
        if (mapTexture == null || (stream != null && stream.Flowing && !streamDrawn)) mapTexture = Picture();
        map.texture = mapTexture;
        oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        root.SetActive(true);
        openedFrame = Time.frameCount;
        menuWasOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = false;
        title.text = Lang.T("MAP OF THE ISLAND", "HARTA INSULEI");
        PutPlaces();
        Update();
    }

    void Close()
    {
        root.SetActive(false);
        Time.timeScale = oldTimeScale;
        bool windows = menuWasOpen || (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen);
        if (!windows)
        {
            if (!GameInput.TouchControls) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (SelectionManager.Instance != null) { SelectionManager.Instance.enabled = true; SelectionManager.Instance.EnableSelection(); }
        }
    }

    // ---------------- where things are on the map ---------------- //

    static bool Area(out Vector3 origin, out Vector2 size)
    {
        var t = Terrain.activeTerrain;
        origin = t != null ? t.transform.position : Vector3.zero;
        size = t != null && t.terrainData != null ? new Vector2(t.terrainData.size.x, t.terrainData.size.z) : Vector2.one;
        return t != null;
    }

    // a place of the world -> a point on the map (the map's middle is 0, 0)
    static Vector2 OnMap(Vector3 p)
    {
        Area(out Vector3 origin, out Vector2 size);
        return new Vector2(((p.x - origin.x) / size.x - 0.5f) * MapSize, ((p.z - origin.z) / size.y - 0.5f) * MapSize);
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Time.frameCount != openedFrame && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.N))) { Close(); return; }

        // the player: a red arrow, turned the way they look
        var state = PlayerState.Instance;
        var cam = Camera.main;
        Vector3 player = state != null && state.playerBody != null ? state.playerBody.transform.position : (cam != null ? cam.transform.position : Vector3.zero);
        arrow.anchoredPosition = OnMap(player);
        Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
        float heading = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        arrow.localRotation = Quaternion.Euler(0f, 0f, -heading);
        float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 6f);
        arrow.localScale = Vector3.one * pulse;

        // the current goal: a gold ring that beats, with what it is
        var story = StoryManager.Instance;
        if (story != null && story.MapGoal(out Vector3 at, out string label))
        {
            goal.gameObject.SetActive(true);
            goal.anchoredPosition = OnMap(at);
            goal.localScale = Vector3.one * (1f + 0.25f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.5f)));
            goalLabel.text = label;
            Vector3 d = at - player; d.y = 0f;
            info.text = Lang.T("Goal: ", "Ținta: ") + label + "\n" + Lang.T("Distance: ", "Distanța: ") + Mathf.RoundToInt(d.magnitude) + " m";
        }
        else
        {
            goal.gameObject.SetActive(false);
            info.text = story != null && story.CurrentLevel >= 1 && story.CurrentLevel <= StoryManager.LevelCount
                ? Lang.T("Level ", "Nivelul ") + story.CurrentLevel + ": " + StoryManager.LevelTitle(story.CurrentLevel)
                : "";
        }
    }

    // the places known so far (small marks with their names)
    void PutPlaces()
    {
        foreach (var p in places) if (p != null) Destroy(p);
        places.Clear();
        var story = StoryManager.Instance;
        int level = story != null ? story.CurrentLevel : 1;
        var start = FindObjectOfType<StoryStart>();
        if (start != null) Place(start.transform.position, Lang.T("Wreck", "Epava"), new Color(0.85f, 0.75f, 0.55f));
        var river = River.Instance;
        if (river != null && river.lakeRadius > 0f) Place(river.lakeCentre, Lang.T("Lake (hemp)", "Lac (cânepă)"), new Color(0.55f, 0.85f, 1f));
        if (Cave.Instance != null && level >= 5) Place(Cave.Instance.Mouth, Lang.T("Cave", "Peștera"), new Color(0.8f, 0.7f, 0.6f));
        if (MiracleStream.Instance != null && level >= 9)
            Place(MiracleStream.Instance.Summit, Lang.T("The other mountain", "Celălalt munte"), new Color(1f, 0.95f, 0.8f));
        foreach (var door in PenDoor.All) if (door != null) { Place(door.transform.position, Lang.T("Pen", "Țarc"), new Color(0.95f, 0.7f, 0.45f)); break; }
        if (Raft.Instance != null) Place(Raft.Instance.transform.position, Lang.T("Raft", "Pluta"), new Color(0.62f, 0.86f, 1f));
        foreach (var s in RabbitSnare.All) if (s != null) Place(s.transform.position, "", new Color(0.9f, 0.85f, 0.6f), 10f);
        goal.SetAsLastSibling();
        arrow.SetAsLastSibling();
    }

    void Place(Vector3 at, string name, Color color, float dot = 16f)
    {
        var mark = UISkin.NewImage("Place " + name, mapRect, UISkin.Disc, color, false);
        UISkin.Centered(mark.rectTransform, OnMap(at), new Vector2(dot, dot));
        var edge = mark.gameObject.AddComponent<UnityEngine.UI.Outline>();
        edge.effectColor = new Color(0f, 0f, 0f, 0.7f);
        edge.effectDistance = new Vector2(2f, -2f);
        places.Add(mark.gameObject);
        if (string.IsNullOrEmpty(name)) return;
        var label = UISkin.NewText("Name", mark.transform, name, 22, Color.white, TextAnchor.MiddleCenter);
        UISkin.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(320f, 30f));
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        var shadow = label.gameObject.AddComponent<UnityEngine.UI.Outline>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // ---------------- the screen ---------------- //

    void Build()
    {
        root = new GameObject("Map");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000; // over the goals panel and the phone buttons
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();

        var dark = UISkin.NewImage("Dark", root.transform, UISkin.White, new Color(0.02f, 0.02f, 0.03f, 0.975f), true); // (linear colours: it needs a high alpha to look dark)
        UISkin.Fill(dark.rectTransform);
        var frame = UISkin.NewImage("Frame", root.transform, UISkin.White, new Color(0.36f, 0.27f, 0.17f, 1f), false);
        UISkin.Centered(frame.rectTransform, new Vector2(-170f, -14f), new Vector2(MapSize + 24f, MapSize + 24f));

        map = new GameObject("Map picture").AddComponent<RawImage>();
        mapRect = map.rectTransform;
        mapRect.SetParent(root.transform, false);
        UISkin.Centered(mapRect, new Vector2(-170f, -14f), new Vector2(MapSize, MapSize));
        map.raycastTarget = false;

        title = UISkin.NewText("Title", root.transform, "MAP", 40, UISkin.Gold, TextAnchor.MiddleLeft);
        UISkin.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(MapSize * 0.5f - 130f, -40f), new Vector2(560f, 60f));
        info = UISkin.NewText("Goal", root.transform, "", 28, UISkin.TextColor, TextAnchor.UpperLeft, false);
        UISkin.Place(info.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(MapSize * 0.5f - 130f, -120f), new Vector2(520f, 300f));
        var legend = UISkin.NewText("Legend", root.transform,
            "", 24, UISkin.TextDim, TextAnchor.UpperLeft, false);
        UISkin.Place(legend.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(MapSize * 0.5f - 130f, -440f), new Vector2(520f, 300f));
        legend.text = Lang.T("<color=#ff5040>Red arrow</color>: you (the way you look)\n<color=#ffd780>Gold ring</color>: where to go now\nThe light columns on the island show the way too.",
            "<color=#ff5040>Săgeata roșie</color>: tu (încotro privești)\n<color=#ffd780>Cercul auriu</color>: unde trebuie să mergi\nȘi coloanele de lumină de pe insulă arată drumul.");

        var close = UISkin.NewButton("Close", root.transform, UISkin.Pill, new Color(0.55f, 0.12f, 0.1f), "X", 36, Color.white, Close);
        UISkin.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -24f), new Vector2(96f, 80f));

        // the goal: a gold ring with its name
        var ring = UISkin.NewImage("Goal", mapRect, UISkin.Ring, new Color(1f, 0.84f, 0.4f, 1f), false);
        goal = ring.rectTransform;
        UISkin.Centered(goal, Vector2.zero, new Vector2(46f, 46f));
        var inner = UISkin.NewImage("Dot", goal, UISkin.Disc, new Color(1f, 0.84f, 0.4f, 0.9f), false);
        UISkin.Centered(inner.rectTransform, Vector2.zero, new Vector2(12f, 12f));
        goalLabel = UISkin.NewText("Name", goal, "", 24, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Place(goalLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 38f), new Vector2(420f, 32f));
        goalLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        var goalShadow = goalLabel.gameObject.AddComponent<UnityEngine.UI.Outline>();
        goalShadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
        goalShadow.effectDistance = new Vector2(1.5f, -1.5f);

        // the player: a red arrow with a white edge
        var a = UISkin.NewImage("You", mapRect, ArrowSprite(), Color.white, false);
        arrow = a.rectTransform;
        UISkin.Centered(arrow, Vector2.zero, new Vector2(40f, 40f));
    }

    static Sprite ArrowSprite()
    {
        if (arrowSprite != null) return arrowSprite;
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Map arrow", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        // a pointed arrow head, pointing up, with a notch at the back: inside it red, a white edge around it
        Vector2 tip = new Vector2(32f, 61f), l = new Vector2(9f, 5f), r = new Vector2(55f, 5f), notch = new Vector2(32f, 19f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                // < 0 inside the triangle tip-r-l, minus the small triangle l-notch-r at the back
                float triangle = Mathf.Max(Side(p, tip, r), Mathf.Max(Side(p, r, l), Side(p, l, tip)));
                float cut = Mathf.Max(Side(p, l, notch), Mathf.Max(Side(p, notch, r), Side(p, r, l)));
                float d = Mathf.Max(triangle, -cut);
                Color c = d < -3f ? new Color(0.92f, 0.16f, 0.1f, 1f) : d < 0.5f ? new Color(1f, 1f, 1f, 1f) : d < 2f ? new Color(0f, 0f, 0f, 0.6f) : new Color(0f, 0f, 0f, 0f);
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        arrowSprite = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
        return arrowSprite;
    }

    // how far p is outside the edge a -> b of a shape that goes around clockwise (< 0: on the inner side)
    static float Side(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = (b - a).normalized;
        Vector2 outward = new Vector2(-e.y, e.x);
        return Vector2.Dot(p - a, outward);
    }

    // ---------------- the picture of the island ---------------- //

    static int drawSize = MapPixels;

    // The photo of the island from above (Resources/UI/IslandMap, made by Tools > Nature > Make The Map Picture), with the
    // new stream drawn on it once it flows. Without the photo: a map drawn from the terrain (Draw).
    static Texture2D Picture()
    {
        var photo = Resources.Load<Texture2D>("UI/IslandMap");
        if (photo == null) return Draw();
        streamDrawn = false;
        var stream = MiracleStream.Instance;
        var terrain = Terrain.activeTerrain;
        if (stream == null || !stream.Flowing || !photo.isReadable || terrain == null) return photo;
        var copy = new Texture2D(photo.width, photo.height, TextureFormat.RGB24, false) { name = "Island map with the new stream", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = photo.GetPixels();
        drawSize = photo.width;
        var pts = stream.Points;
        float width = Mathf.Max(1.5f, photo.width / 400f);
        for (int i = 1; i < pts.Count; i++)
            Line(pixels, pts[i - 1], pts[i], width, new Color(0.25f, 0.55f, 0.9f), terrain.transform.position, terrain.terrainData.size);
        copy.SetPixels(pixels);
        copy.Apply();
        streamDrawn = true;
        return copy;
    }

    // ---------------- drawing the island ---------------- //

    public static Texture2D Draw() // (also used by the editor tools to look at the map)
    {
        drawSize = MapPixels;
        var tex = new Texture2D(MapPixels, MapPixels, TextureFormat.RGB24, false) { name = "Island map (made in code)", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var terrain = Terrain.activeTerrain;
        var ocean = Ocean.Instance;
        var pixels = new Color[MapPixels * MapPixels];
        if (terrain == null || terrain.terrainData == null)
        {
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(0.3f, 0.5f, 0.7f);
            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        float level = ocean != null ? ocean.Level : tp.y;
        // the height of every pixel (one read of the terrain each)
        int res = data.heightmapResolution;
        var grid = new float[MapPixels * MapPixels];
        for (int py = 0; py < MapPixels; py++)
            for (int px = 0; px < MapPixels; px++)
            {
                int hx = Mathf.Clamp(Mathf.RoundToInt((px + 0.5f) / MapPixels * (res - 1)), 0, res - 1);
                int hy = Mathf.Clamp(Mathf.RoundToInt((py + 0.5f) / MapPixels * (res - 1)), 0, res - 1);
                grid[py * MapPixels + px] = data.GetHeight(hx, hy) + tp.y;
            }
        float H(int px, int py) => grid[py * MapPixels + px];
        float metres = data.size.x / MapPixels;
        // the forests: how many trees in each pixel
        var trees = new float[MapPixels * MapPixels];
        if (Forest.Instance != null)
            foreach (var t in Forest.Instance.Trees)
            {
                if (t == null) continue;
                Vector3 p = t.transform.position;
                int x = Mathf.FloorToInt((p.x - tp.x) / data.size.x * MapPixels), y = Mathf.FloorToInt((p.z - tp.z) / data.size.z * MapPixels);
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy;
                        if (xx >= 0 && yy >= 0 && xx < MapPixels && yy < MapPixels) trees[yy * MapPixels + xx] += dx == 0 && dy == 0 ? 0.6f : 0.15f;
                    }
            }
        Vector3 light = new Vector3(-0.6f, 0.75f, 0.35f).normalized; // from the north-west, like on old maps
        for (int y = 0; y < MapPixels; y++)
            for (int x = 0; x < MapPixels; x++)
            {
                float h = H(x, y);
                float dzx = H(Mathf.Min(x + 1, MapPixels - 1), y) - H(Mathf.Max(x - 1, 0), y);
                float dzy = H(x, Mathf.Min(y + 1, MapPixels - 1)) - H(x, Mathf.Max(y - 1, 0));
                Vector3 normal = new Vector3(-dzx / (2f * metres), 1f, -dzy / (2f * metres)).normalized;
                float shade = Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(normal, light), 0.55f, 1.2f);
                float above = h - level;
                Color c;
                if (above < 0f)
                {
                    float deep = Mathf.Clamp01(-above / 12f);
                    c = Color.Lerp(new Color(0.42f, 0.7f, 0.82f), new Color(0.13f, 0.32f, 0.56f), deep);
                    shade = 1f;
                }
                else if (above < 0.9f) c = new Color(0.88f, 0.81f, 0.6f);                                   // beach
                else if (above < 20f) c = Color.Lerp(new Color(0.5f, 0.68f, 0.33f), new Color(0.42f, 0.56f, 0.3f), above / 20f);
                else if (above < 42f) c = Color.Lerp(new Color(0.48f, 0.5f, 0.38f), new Color(0.58f, 0.56f, 0.52f), (above - 20f) / 22f); // rock
                else c = Color.Lerp(new Color(0.66f, 0.64f, 0.6f), new Color(0.86f, 0.85f, 0.83f), Mathf.Clamp01((above - 42f) / 25f));
                float forest = Mathf.Clamp01(trees[y * MapPixels + x] * 0.8f);
                if (above >= 0.9f) c = Color.Lerp(c, new Color(0.2f, 0.38f, 0.18f), forest * 0.75f);
                c *= shade;
                // a warm paper tint and darker edges
                float ex = Mathf.Abs(x / (float)MapPixels - 0.5f) * 2f, ey = Mathf.Abs(y / (float)MapPixels - 0.5f) * 2f;
                float vignette = 1f - 0.25f * Mathf.Pow(Mathf.Max(ex, ey), 4f);
                c = Color.Lerp(c, new Color(0.93f, 0.86f, 0.7f), 0.12f) * vignette;
                c.a = 1f;
                pixels[y * MapPixels + x] = c;
            }

        // the fresh water: the river, the lake and the new stream
        var waterColor = new Color(0.22f, 0.5f, 0.85f);
        var river = River.Instance;
        if (river != null)
        {
            for (int i = 1; i < river.points.Count; i++) Line(pixels, river.points[i - 1], river.points[i], Mathf.Max(1.2f, river.halfWidths[i] / metres), waterColor, tp, data.size);
            if (river.lakeRadius > 0f) Disc(pixels, river.lakeCentre, river.lakeRadius / metres, waterColor, tp, data.size);
        }
        var stream = MiracleStream.Instance;
        streamDrawn = false;
        if (stream != null && stream.Flowing)
        {
            var pts = stream.Points;
            for (int i = 1; i < pts.Count; i++) Line(pixels, pts[i - 1], pts[i], 1.3f, waterColor, tp, data.size);
            streamDrawn = true;
        }
        tex.SetPixels(pixels);
        tex.Apply();
        return tex;
    }

    static void Line(Color[] pixels, Vector3 a, Vector3 b, float radius, Color color, Vector3 origin, Vector3 size)
    {
        float ax = (a.x - origin.x) / size.x * drawSize, ay = (a.z - origin.z) / size.z * drawSize;
        float bx = (b.x - origin.x) / size.x * drawSize, by = (b.z - origin.z) / size.z * drawSize;
        int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay)) * 2f) + 1;
        for (int s = 0; s <= steps; s++)
        {
            float t = s / (float)steps;
            Dot(pixels, Mathf.Lerp(ax, bx, t), Mathf.Lerp(ay, by, t), radius, color);
        }
    }

    static void Disc(Color[] pixels, Vector3 c, float radius, Color color, Vector3 origin, Vector3 size)
    {
        Dot(pixels, (c.x - origin.x) / size.x * drawSize, (c.z - origin.z) / size.z * drawSize, radius, color);
    }

    static void Dot(Color[] pixels, float cx, float cy, float radius, Color color)
    {
        int r = Mathf.CeilToInt(radius);
        for (int y = Mathf.FloorToInt(cy) - r; y <= Mathf.CeilToInt(cy) + r; y++)
            for (int x = Mathf.FloorToInt(cx) - r; x <= Mathf.CeilToInt(cx) + r; x++)
            {
                if (x < 0 || y < 0 || x >= drawSize || y >= drawSize) continue;
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                if (d <= radius) pixels[y * drawSize + x] = color;
            }
    }
}
