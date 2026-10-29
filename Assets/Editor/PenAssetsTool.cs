using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Nature > 4. Make Snare, Rabbit, Pen And Cross Items
// The inventory items of Brother Ioan's canon (level 9), with their icons in Assets/ART/Icons (drawn from the models
// the game makes in code):
//  - snare (1 stick + 5 hemp), rabbitmale / rabbitfemale (live rabbits taken from a snare)
//  - doorwall, windowwall, roof (the pen), cross (5 planks, raised on the other mountain)
// Tools > Nature > Add Hemp (Lake, Mountain, River): most hemp around the lake, some on the slopes near the mountain and
// along the river (scene groups marked "Hemp": running it again replaces them).
public static class PenAssetsTool
{
    const string IconDir = "Assets/ART/Icons";

    class Def
    {
        public string item, name, description, use;
        public System.Func<GameObject> model;
        public Color badge;   // the live rabbits: a blue or pink mark
        public Quaternion view = Quaternion.Euler(-24f, 35f, 0f); // (a negative tilt: seen from above)
    }

    static readonly Def[] Items =
    {
        new Def { item = "snare", name = "Snare", description = "A loop of hemp cord, tied to a stick pushed into the ground.",
            use = "Use it (double tap / right click) to set it near rabbits. A grown rabbit that walks into it is caught alive.", model = RabbitSnare.BuildModel },
        new Def { item = RabbitPopulation.LiveMale, name = "Rabbit (male)", description = "A live rabbit, caught in a snare.",
            use = "Let it go in your pen (drop it or use it), then shut the door. Outside a pen it runs away!", model = Rabbit, badge = new Color(0.35f, 0.65f, 1f) },
        new Def { item = RabbitPopulation.LiveFemale, name = "Rabbit (female)", description = "A live rabbit, caught in a snare.",
            use = "Let it go in your pen (drop it or use it), then shut the door. Outside a pen it runs away!", model = Rabbit, badge = new Color(1f, 0.45f, 0.75f) },
        new Def { item = "doorwall", name = "Door Wall", description = "A wall with a plank door on rope hinges.",
            use = "Use it, then look at a wall's place on a foundation. Look at the door and click to open or shut it.", model = () => Buildings.Create(Buildings.DoorWallModel), view = Quaternion.Euler(-10f, 55f, 0f) },
        new Def { item = "windowwall", name = "Window Wall", description = "A wall with a window, two sticks across it.",
            use = "Use it, then look at a wall's place on a foundation.", model = () => Buildings.Create(Buildings.WindowWallModel), view = Quaternion.Euler(-10f, 55f, 0f) },
        new Def { item = "roof", name = "Roof", description = "Planks with a thick layer of hay on top.",
            use = "Use it, then look at the top of a foundation's walls.", model = () => Buildings.Create(Buildings.RoofModel), view = Quaternion.Euler(-38f, 35f, 0f) },
        new Def { item = Fences.FenceItem, name = "Fence", description = "A fence panel of planks, 4 m long.",
            use = "Use it and look where it goes: next to the end of another fence it goes on from there. Make a yard as big as you want for rabbits and close it with a gate.",
            model = () => Fences.Create(BuildingKind.Fence, Vector3.zero, 0f, Fences.Length, false), view = Quaternion.Euler(-14f, 60f, 0f) },
        new Def { item = Fences.GateItem, name = "Gate", description = "A fence panel with a gate that opens.",
            use = "Use it to put it in the fence. Look at the gate and click to open or shut it: open, the rabbits leave the yard.",
            model = () => Fences.Create(BuildingKind.FenceGate, Vector3.zero, 0f, Fences.Length, false), view = Quaternion.Euler(-14f, 60f, 0f) },
        new Def { item = "cross", name = "Cross", description = "A cross of five planks, like the one Brother Ioan raised.",
            use = "Brother Ioan's canon: raise it last, on the top of the other mountain (Menu > Map).", model = CanonCross.BuildModel, view = Quaternion.Euler(-6f, 20f, 0f) },
    };

    [MenuItem("Tools/Nature/4. Make Snare, Rabbit, Pen And Cross Items")]
    public static void MakeItems()
    {
        const string template = "Assets/Resources/pinecone.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(template) == null) { Debug.LogWarning("Pen items: no " + template); return; }
        Directory.CreateDirectory(IconDir);
        foreach (var def in Items)
        {
            var sprite = MakeIcon(def);
            string path = "Assets/Resources/" + def.item + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) AssetDatabase.CopyAsset(template, path);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                root.name = def.item;
                root.tag = "Untagged";
                var image = root.GetComponent<UnityEngine.UI.Image>();
                if (image != null && sprite != null) image.sprite = sprite;
                var info = root.GetComponent<InventoryItem>();
                info.thisName = def.name;
                info.thisDescription = def.description;
                info.thisFunctionality = def.use;
                info.isConsumable = false;
                info.healthEffect = info.caloriesEffect = info.hydrationEffect = 0f;
                info.isUseable = true;
                info.isEquippable = false;
                info.isTrashable = true;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // the hemp: it grows back sooner, and the game says where it grows most
        const string hempPlant = "Assets/Resources/Vegetation/Hemp.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(hempPlant) != null)
        {
            var plant = PrefabUtility.LoadPrefabContents(hempPlant);
            try { var p = plant.GetComponent<ScenePlant>(); if (p != null) p.regrowHours = 48f; PrefabUtility.SaveAsPrefabAsset(plant, hempPlant); }
            finally { PrefabUtility.UnloadPrefabContents(plant); }
        }
        const string hempItem = "Assets/Resources/hemp.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(hempItem) != null)
        {
            var hemp = PrefabUtility.LoadPrefabContents(hempItem);
            try
            {
                var info = hemp.GetComponent<InventoryItem>();
                info.thisDescription = "A tall plant with strong fibres. It grows thickest around the lake.";
                info.thisFunctionality = "3 hemp make a rope (Crafting > Refine); 5 hemp and a stick make a snare (Crafting > Survival).";
                PrefabUtility.SaveAsPrefabAsset(hemp, hempItem);
            }
            finally { PrefabUtility.UnloadPrefabContents(hemp); }
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Pen items: snare, live rabbits, door wall, window wall, roof and cross made");
    }

    static GameObject Rabbit()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PREFABS/Animals/WhiteRabbitBig.prefab");
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab);
        foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true)) Object.DestroyImmediate(ps.gameObject);
        foreach (var s in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(s);
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) Object.DestroyImmediate(r);
        // only what is drawn of the rabbit itself (the blood puddle and such stay out)
        foreach (var r in go.GetComponentsInChildren<Renderer>(true)) if (!r.gameObject.activeInHierarchy || r.name.ToLower().Contains("blood")) Object.DestroyImmediate(r.gameObject);
        go.transform.rotation = Quaternion.Euler(0f, -60f, 0f);
        return go;
    }

    static Sprite MakeIcon(Def def)
    {
        string target = IconDir + "/" + def.item + ".png";
        var model = def.model != null ? def.model() : null;
        if (model == null) return File.Exists(target) ? NatureAssetsTool.ImportSprite(target) : null;
        model.transform.position = Vector3.zero;
        var picture = NatureAssetsTool.RenderModel(model, 256, 256, def.view, false);
        Object.DestroyImmediate(model);
        if (def.badge.a > 0f) Badge(picture, def.badge, def.item == RabbitPopulation.LiveFemale);
        File.WriteAllBytes(target, picture.EncodeToPNG());
        Object.DestroyImmediate(picture);
        AssetDatabase.ImportAsset(target);
        return NatureAssetsTool.ImportSprite(target);
    }

    // a round mark at the bottom right: the male sign (a ring with an arrow) or the female sign (a ring with a cross)
    static void Badge(Texture2D tex, Color color, bool female)
    {
        int n = tex.width;
        Vector2 c = new Vector2(n * 0.78f, n * 0.22f);
        float r = n * 0.2f;
        Vector2 ringC = female ? c + new Vector2(0f, n * 0.04f) : c + new Vector2(-n * 0.025f, -n * 0.025f);
        float ringR = n * 0.065f, line = n * 0.018f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Vector2.Distance(p, c);
                if (d > r + 1f) continue;
                Color bg = color;
                bg.a = Mathf.Clamp01(r + 0.5f - d);
                bool sign = Mathf.Abs(Vector2.Distance(p, ringC) - ringR) < line;
                if (female)
                {
                    Vector2 a = ringC + Vector2.down * ringR, b = ringC + Vector2.down * (ringR + n * 0.075f);
                    sign |= Segment(p, a, b) < line;
                    Vector2 m = ringC + Vector2.down * (ringR + n * 0.04f);
                    sign |= Segment(p, m + Vector2.left * n * 0.03f, m + Vector2.right * n * 0.03f) < line;
                }
                else
                {
                    Vector2 dir = new Vector2(1f, 1f).normalized;
                    Vector2 a = ringC + dir * ringR, b = ringC + dir * (ringR + n * 0.07f);
                    sign |= Segment(p, a, b) < line;
                    sign |= Segment(p, b, b - new Vector2(n * 0.04f, 0f)) < line;
                    sign |= Segment(p, b, b - new Vector2(0f, n * 0.04f)) < line;
                }
                Color under = tex.GetPixel(x, y);
                Color top = sign ? Color.white : bg;
                float alpha = sign ? 1f : bg.a;
                Color mixed = Color.Lerp(under, top, alpha);
                mixed.a = Mathf.Max(under.a, alpha);
                tex.SetPixel(x, y, mixed);
            }
        tex.Apply();
    }

    static float Segment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
        return Vector2.Distance(p, a + ab * t);
    }

    // ---------------- the map: a picture of the island from straight above ---------------- //

    public const string MapPath = "Assets/Resources/UI/IslandMap.png";

    // Tools > Nature > Make The Map Picture (From Above): a camera high over the island, looking straight down, takes
    // its picture (the trees, the mountains, the lake, the river, the beach, the sea) for the Map (MapScreen). It covers
    // the whole terrain, north at the top. Run it again after changing the island.
    [MenuItem("Tools/Nature/Make The Map Picture (From Above)")]
    public static void MakeMapPicture()
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) { Debug.LogWarning("Map: no terrain"); return; }
        Vector3 size = terrain.terrainData.size;
        Vector3 centre = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
        var go = new GameObject("map cam");
        var cam = go.AddComponent<Camera>();
        cam.enabled = false;
        cam.orthographic = true;
        cam.orthographicSize = size.z * 0.5f;
        cam.aspect = 1f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.16f, 0.36f, 0.55f);
        cam.nearClipPlane = 1f;
        cam.farClipPlane = 3000f;
        go.transform.SetPositionAndRotation(centre + Vector3.up * (terrain.transform.position.y + size.y + 600f - centre.y), Quaternion.Euler(90f, 0f, 0f));
        bool fog = RenderSettings.fog;
        RenderSettings.fog = false;
        // the sea's shader does not work seen straight from above (it covered the island with a pale haze): it is
        // hidden for the picture and the sea is painted afterwards from the depth of the water
        var hidden = new List<Renderer>();
        foreach (var ocean in Object.FindObjectsOfType<Ocean>())
            foreach (var r in ocean.GetComponentsInChildren<Renderer>())
                if (r.enabled) { r.enabled = false; hidden.Add(r); }
        const int big = 2048, small = 1024;
        var rt = new RenderTexture(big, big, 24);
        cam.targetTexture = rt;
        try { cam.Render(); }
        finally
        {
            RenderSettings.fog = fog;
            foreach (var r in hidden) if (r != null) r.enabled = true;
        }
        RenderTexture.active = rt;
        var raw = new Texture2D(big, big, TextureFormat.RGB24, false);
        raw.ReadPixels(new Rect(0, 0, big, big), 0, 0);
        raw.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(go);

        // smaller (smooth); the sea painted blue by its depth (light by the beach, dark far out)
        var oceanNow = Object.FindObjectOfType<Ocean>();
        float level = oceanNow != null ? oceanNow.Level : float.MinValue;
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        var map = new Texture2D(small, small, TextureFormat.RGB24, false);
        var px = new Color[small * small];
        for (int y = 0; y < small; y++)
            for (int x = 0; x < small; x++)
            {
                Color s = (raw.GetPixel(x * 2, y * 2) + raw.GetPixel(x * 2 + 1, y * 2) + raw.GetPixel(x * 2, y * 2 + 1) + raw.GetPixel(x * 2 + 1, y * 2 + 1)) * 0.25f;
                s = new Color(Mathf.Pow(s.r, 0.95f) * 1.03f, Mathf.Pow(s.g, 0.95f) * 1.03f, Mathf.Pow(s.b, 0.95f), 1f);
                int hx = Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / small * (res - 1)), 0, res - 1);
                int hy = Mathf.Clamp(Mathf.RoundToInt((y + 0.5f) / small * (res - 1)), 0, res - 1);
                float depth = level - (data.GetHeight(hx, hy) + terrain.transform.position.y);
                if (depth > 0f)
                {
                    Color sea = Color.Lerp(new Color(0.36f, 0.74f, 0.8f), new Color(0.07f, 0.25f, 0.5f), Mathf.Clamp01(depth / 9f));
                    s = Color.Lerp(s, sea, Mathf.Clamp01(0.6f + depth * 0.6f));
                    s.a = 1f;
                }
                px[y * small + x] = s;
            }
        map.SetPixels(px);
        map.Apply();
        Object.DestroyImmediate(raw);
        Directory.CreateDirectory(Path.GetDirectoryName(MapPath));
        File.WriteAllBytes(MapPath, map.EncodeToPNG());
        Object.DestroyImmediate(map);
        AssetDatabase.ImportAsset(MapPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(MapPath);
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;       // the game draws the new stream onto it
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        Debug.Log("Map: picture of the island made (" + MapPath + ")");
    }

    // ---------------- hemp: most of it around the lake ---------------- //

    [MenuItem("Tools/Nature/Add Hemp (Lake, Mountain, River)")]
    static void AddHempFromMenu()
    {
        var made = AddHempEverywhere();
        Debug.Log("Hemp: " + made);
    }

    public static string AddHempEverywhere()
    {
        int mountain = IslandFillTool.AddHemp(48, 515);   // (removes the old hemp groups first)
        int lake = AddHempAroundLake(150, 731);
        int river = AddHempAlongRiver(36, 977);
        return "around the lake " + lake + ", near the mountain " + mountain + ", along the river " + river;
    }

    static GameObject NewGroup(string name)
    {
        var manager = Object.FindObjectOfType<EnviromentManager>();
        if (manager == null) return null;
        var vegetation = manager.transform.Find("Vegetation");
        var group = new GameObject(name);
        group.transform.SetParent(vegetation != null ? vegetation : manager.transform, false);
        group.AddComponent<NatureGroup>().kind = "Hemp";
        Undo.RegisterCreatedObjectUndo(group, "Hemp");
        return group;
    }

    static void Plant(GameObject prefab, Transform group, Terrain terrain, Vector3 at, System.Random rng)
    {
        at.y = terrain.SampleHeight(at) + terrain.transform.position.y - 0.03f;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
        go.transform.position = at;
        go.transform.rotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // patches of hemp in a ring around the lake's shore
    public static int AddHempAroundLake(int count, int seed)
    {
        var terrain = Terrain.activeTerrain;
        var river = Object.FindObjectOfType<River>();
        var prefab = Resources.Load<GameObject>("Vegetation/Hemp");
        if (terrain == null || river == null || river.lakeRadius <= 0f || prefab == null) return 0;
        river.BuildCells();
        var group = NewGroup("Hemp (around the lake)");
        if (group == null) return 0;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        var rng = new System.Random(seed);
        var patches = new List<Vector3>();
        int made = 0;
        for (int attempt = 0; attempt < 20000 && made < count; attempt++)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float d = river.lakeRadius + 3f + (float)rng.NextDouble() * 24f;
            Vector3 p = river.lakeCentre + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            float h = terrain.SampleHeight(p) + tp.y;
            if (h < river.lakeCentre.y + 0.3f) continue;                                   // in the water
            if (data.GetSteepness((p.x - tp.x) / data.size.x, (p.z - tp.z) / data.size.z) > 34f) continue;
            if (river.DistanceToWater(p) < 3f) continue;
            bool close = false;
            foreach (var q in patches) if (Flat(q - p) < 8f) { close = true; break; }
            if (close) continue;
            patches.Add(p);
            int inPatch = 4 + rng.Next(4);
            for (int j = 0; j < inPatch && made < count; j++)
            {
                Vector3 at = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 4.5f, 0f, ((float)rng.NextDouble() - 0.5f) * 4.5f);
                if (terrain.SampleHeight(at) + tp.y < river.lakeCentre.y + 0.25f) continue;
                Plant(prefab, group.transform, terrain, at, rng);
                made++;
            }
        }
        EditorSceneManager.MarkSceneDirty(group.scene);
        return made;
    }

    // a few patches beside the river (on its banks, not in the water)
    public static int AddHempAlongRiver(int count, int seed)
    {
        var terrain = Terrain.activeTerrain;
        var river = Object.FindObjectOfType<River>();
        var prefab = Resources.Load<GameObject>("Vegetation/Hemp");
        if (terrain == null || river == null || river.points.Count < 4 || prefab == null) return 0;
        var group = NewGroup("Hemp (along the river)");
        if (group == null) return 0;
        Vector3 tp = terrain.transform.position;
        var data = terrain.terrainData;
        var rng = new System.Random(seed);
        int made = 0;
        float run = 0f, next = 25f;
        for (int i = 1; i < river.points.Count - 1 && made < count; i++)
        {
            run += Flat(river.points[i] - river.points[i - 1]);
            if (run < next) continue;
            next += 45f + (float)rng.NextDouble() * 30f;
            Vector3 along = river.points[i + 1] - river.points[i - 1]; along.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, along.normalized) * (rng.Next(2) == 0 ? 1f : -1f);
            Vector3 p = river.points[i] + side * (river.halfWidths[i] + 5f + (float)rng.NextDouble() * 4f);
            if (data.GetSteepness((p.x - tp.x) / data.size.x, (p.z - tp.z) / data.size.z) > 30f) continue;
            int inPatch = 3 + rng.Next(2);
            for (int j = 0; j < inPatch && made < count; j++)
            {
                Vector3 at = p + new Vector3(((float)rng.NextDouble() - 0.5f) * 3.5f, 0f, ((float)rng.NextDouble() - 0.5f) * 3.5f);
                if (river.DistanceToWater(at) < 2f) continue;
                Plant(prefab, group.transform, terrain, at, rng);
                made++;
            }
        }
        EditorSceneManager.MarkSceneDirty(group.scene);
        return made;
    }
}
