#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

// TEMPORARY (Claude): a look around the game in Play mode (screenshots + checks into Temp/<dir>). Removed after the test.
public class __Probe : MonoBehaviour
{
    public static string Folder = "Probe1";
    public static string Mode = "phone";   // "phone": everything; "pc": only the screens
    string Dir => Application.dataPath + "/../Temp/" + Folder + "/";
    StreamWriter log;
    Transform player;
    MouseMovement look;
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    void Log(string s) { log.WriteLine(Time.realtimeSinceStartup.ToString("0.0") + " " + s); }
    static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Dir);
        log = new StreamWriter(Dir + "log.txt", false) { AutoFlush = true };
        // (Immortal works only while the admin is unlocked)
        var adminProp = typeof(DebugEnvironmentPanel).GetProperty("AdminUnlocked", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        adminProp?.GetSetMethod(true)?.Invoke(null, new object[] { true });
        GameSettings.Immortal = true;
        IEnumerator run = Run();
        while (true)
        {
            object current;
            try { if (!run.MoveNext()) break; current = run.Current; }
            catch (System.Exception e) { Log("EXCEPTION " + e); break; }
            yield return current;
        }
        GameSettings.Immortal = false;
        adminProp?.GetSetMethod(true)?.Invoke(null, new object[] { false });
        Log("finished");
        File.WriteAllText(Dir + "finished.txt", "ok");
    }

    IEnumerator Run()
    {
        Log("screen " + Screen.width + "x" + Screen.height + " touch " + GameInput.TouchControls + " mode " + Mode);
        float t = 0f;
        while ((StoryManager.Instance == null || FindObjectOfType<PlayerMovement>() == null) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
        var movement = FindObjectOfType<PlayerMovement>();
        player = movement.transform;
        look = player.GetComponent<MouseMovement>();
        yield return Real(2f);
        yield return SkipStory();
        yield return Real(4f);
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        look.enabled = false;
        look.SetLook(60f, 4f);
        yield return Real(1.5f);
        yield return Shot("01_hud");

        // the game menu
        Press(GameInput.TouchAction.Menu);
        yield return Real(1.2f);
        yield return Shot("02_menu");
        if (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen) MenuManager.Instance.CloseMenu();
        yield return Real(0.5f);

        // the inventory and the construction cards
        var inventory = InventorySystem.Instance;
        foreach (var item in new[] { "fence", "fence", "fence", "fence", "fence", "fence", "fence", "fence", "fence", "fencegate", "plank", "plank", "stick", "stick", "rope" })
            inventory.AddToIventory(item, false, false);
        inventory.OpenUI();
        yield return Real(0.8f);
        yield return Shot("03_inventory");
        inventory.CloseUI();
        yield return Real(0.3f);
        CraftingSystem.Instance.OpenUI();
        yield return Real(0.4f);
        var constructionButton = CraftingSystem.Instance.craftingScreenUI.transform.Find("ConstructionButton");
        if (constructionButton != null) constructionButton.GetComponent<Button>().onClick.Invoke();
        yield return Real(0.8f);
        yield return Shot("04_construction");
        var toolsButton = CraftingSystem.Instance.craftingScreenUI.transform.Find("ToolsButton");
        CraftingSystem.Instance.ShowCategories();
        if (toolsButton != null) toolsButton.GetComponent<Button>().onClick.Invoke();
        yield return Real(0.6f);
        yield return Shot("05_tools");
        CraftingSystem.Instance.CloseUI();
        yield return Real(0.5f);
        HelpScreen.Open();
        yield return Real(0.8f);
        yield return Shot("05b_help");
        HelpScreen.Toggle();
        yield return Real(0.5f);
        MapScreen.Open();
        yield return Real(1.2f);
        yield return Shot("05c_map");
        MapScreen.Toggle();
        yield return Real(0.5f);
        if (Mode == "pc") { yield return MainMenuShot(); yield break; }
        if (Mode == "paths")
        {
            yield return Fence();
            yield return Stream();
            yield return PathViews();
            yield break;
        }

        yield return Fence();
        yield return TreeGuard();
        yield return Seeds();
        yield return Stream();
        yield return Rabbits();
    }

    IEnumerator MainMenuShot()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        yield return Real(4f);
        yield return Shot("05d_main_menu");
    }

    IEnumerator SkipStory()
    {
        for (int i = 0; i < 30; i++)
        {
            var skip = GameObject.Find("Skip");
            if (skip != null && skip.GetComponent<Button>() != null) { skip.GetComponent<Button>().onClick.Invoke(); Log("skip story"); }
            yield return Real(0.4f);
            if (Time.timeScale > 0f && GameObject.Find("Skip") == null && i > 2) break;
        }
    }

    static void Press(GameInput.TouchAction a)
    {
        GameInput.PressTouch(a);
        GameInput.ReleaseTouch(a);
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Dir + name + ".png", tex.EncodeToJPG(82));
        Destroy(tex);
        Log("shot " + name);
    }

    void Teleport(Vector3 p, float yaw, float pitch)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = new Vector3(p.x, Forest.GroundY(p) + 1.95f, p.z);
        cc.enabled = true;
        look.SetLook(yaw, pitch);
    }

    static float YawTo(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    // the world from any place (no HUD)
    void RenderWorld(string name, Vector3 from, Vector3 at, float fov = 60f)
    {
        var main = Camera.main;
        var go = new GameObject("Probe camera");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(main);
        cam.fieldOfView = fov;
        go.transform.position = from;
        go.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up);
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes(Dir + name + ".jpg", tex.EncodeToJPG(85));
        cam.targetTexture = null;
        Destroy(rt); Destroy(tex); Destroy(go);
        Log("render " + name);
    }

    // ---------------- the fenced yard ---------------- //

    IEnumerator Fence()
    {
        var start = FindObjectOfType<StoryStart>();
        Vector3 f = start.transform.forward; f.y = 0f; f.Normalize();
        Vector3 r = Vector3.Cross(Vector3.up, f);
        Vector3 c = start.transform.position + f * 45f;
        // 12 x 8 m: 3 panels along r, 2 along f; one panel left open, then closed with FencePlacing
        var corners = new[] { c - r * 6f - f * 4f, c + r * 6f - f * 4f, c + r * 6f + f * 4f, c - r * 6f + f * 4f };
        var panels = new List<(Vector3 a, Vector3 b)>();
        for (int k = 0; k < 4; k++)
        {
            Vector3 a = corners[k], b = corners[(k + 1) % 4];
            int n = Mathf.RoundToInt(Vector3.Distance(a, b) / Fences.Length);
            for (int i = 0; i < n; i++) panels.Add((Vector3.Lerp(a, b, (float)i / n), Vector3.Lerp(a, b, (float)(i + 1) / n)));
        }
        Vector3 gapA = panels[7].a, gapB = panels[7].b;
        for (int i = 0; i < panels.Count; i++)
        {
            if (i == 7) continue;
            var kind = i == 1 ? BuildingKind.FenceGate : BuildingKind.Fence;
            Vector3 m = (panels[i].a + panels[i].b) * 0.5f;
            m.y = Forest.GroundY(m);
            var go = Fences.Create(kind, m, YawTo(panels[i].a, panels[i].b), Vector3.Distance(panels[i].a, panels[i].b), true);
            Buildings.OnPlaced(go, kind, Vector3.Distance(panels[i].a, panels[i].b));
        }
        yield return null;
        Log("fence: free ends " + Fences.FreeEnds().Count + " (2 expected), yard built " + Fences.YardBuilt() + " (false expected), centre enclosed " + Buildings.IsEnclosed(c));
        // close the gap the way the player does: stand outside, look at the gap, use a fence panel
        Vector3 gapMid = (gapA + gapB) * 0.5f;
        Vector3 outward = gapMid - c; outward.y = 0f; outward.Normalize();
        Vector3 stand = gapMid + outward * 7f;
        Teleport(stand, YawTo(stand, gapMid), 18f);
        yield return Real(1f);
        GameObject fenceItem = null;
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == "fence") { fenceItem = slot.transform.GetChild(0).gameObject; break; }
        Log("fence item " + (fenceItem != null));
        // aim at the far end of the gap's line (as a player would: the new panel goes from the free end to there)
        float bestYaw = 0f, bestPitch = 0f, bestD = 999f;
        for (float yaw = -40f; yaw <= 40f; yaw += 1f)
            for (float pitch = 5f; pitch <= 60f; pitch += 1f)
            {
                var q = Quaternion.Euler(pitch, YawTo(stand, gapMid) + yaw, 0f);
                var camPos = Camera.main.transform.position;
                if (Physics.Raycast(camPos, q * Vector3.forward, out RaycastHit hit, 40f))
                {
                    float d = Vector3.Distance(new Vector3(hit.point.x, 0f, hit.point.z), new Vector3(gapB.x, 0f, gapB.z));
                    if (d < bestD) { bestD = d; bestYaw = YawTo(stand, gapMid) + yaw; bestPitch = pitch; }
                }
            }
        look.SetLook(bestYaw, bestPitch);
        yield return null;
        FencePlacing.Instance.Begin(fenceItem);
        yield return null; yield return null;
        var fp = FencePlacing.Instance;
        Log("placing: length " + Field(fp, "length") + " valid " + Field(fp, "valid") + " why '" + Field(fp, "why") + "'");
        yield return Shot("06_fence_placing");
        if ((bool)Field(fp, "valid")) fp.GetType().GetMethod("Place", Any).Invoke(fp, null);
        yield return null;
        fp.Cancel();
        yield return null;
        Log("after closing: free ends " + Fences.FreeEnds().Count + " yard built " + Fences.YardBuilt() + " centre enclosed " + Buildings.IsEnclosed(c)
            + " outside enclosed " + Buildings.IsEnclosed(c + f * 14f));
        // two rabbits in the yard, the gate shut: counted as penned
        var population = RabbitPopulation.Instance;
        var spawn = population.GetType().GetMethod("Spawn", Any);
        spawn.Invoke(population, new object[] { c + r * 2f, 0f, RabbitInfo.Sex.Male, 60f });
        spawn.Invoke(population, new object[] { c - r * 2f, 0f, RabbitInfo.Sex.Female, 60f });
        yield return Real(0.6f);
        Log("pair in pen " + StoryManager.PairInPen() + " (True expected)");
        var gate = FindObjectsOfType<PenDoor>();
        foreach (var g in gate) if (g.GetComponent<BuildingPiece>() != null && g.GetComponent<BuildingPiece>().kind == BuildingKind.FenceGate) { g.Toggle(player.position); break; }
        yield return Real(1f);
        Log("gate open: pair in pen " + StoryManager.PairInPen() + " (True expected), yard built " + Fences.YardBuilt());
        foreach (var g in gate) if (g.IsOpen) g.Toggle(player.position);
        yield return Real(1f);
        Vector3 view = c - f * 16f - r * 9f;
        Teleport(view, YawTo(view, c), 14f);
        yield return Real(1.2f);
        yield return Shot("07_fence_yard");
        RenderWorld("07b_fence_close", c - f * 6.5f + r * 3f + Vector3.up * (Forest.GroundY(c) - c.y + 3.2f), c + Vector3.up * (Forest.GroundY(c) - c.y + 0.8f) + r * 5f);
        // the save keeps the fence
        var data = Buildings.GetData();
        int fences = 0; foreach (var p in data.pieces) if (p.kind >= (int)BuildingKind.Fence) fences++;
        Log("saved fence pieces " + fences);
    }

    static object Field(object o, string name) => o.GetType().GetField(name, Any).GetValue(o);

    // ---------------- seeing through trees ---------------- //

    IEnumerator TreeGuard()
    {
        var forest = Forest.Instance;
        ForestTree oak = null;
        float best = float.MaxValue;
        foreach (var t in forest.Trees)
        {
            if (t == null || t.data.species != (int)TreeSpecies.Kind.Oak || t.data.height < 14f) continue;
            float d = (t.data.position - player.position).sqrMagnitude;
            if (d < best) { best = d; oak = t; }
        }
        if (oak == null) { Log("no oak"); yield break; }
        Vector3 p = oak.data.position;
        Vector3 from = p + new Vector3(oak.TrunkRadius + 0.45f + 0.75f, 0f, 0f); // (the player's capsule sits 1 m off its centre)
        Teleport(from + new Vector3(3f, 0f, 0f), YawTo(from, p), 0f);
        yield return Real(1f);
        // walk into it
        var cc = player.GetComponent<CharacterController>();
        for (int i = 0; i < 90; i++) { cc.Move((p - player.position).normalized * 0.08f + Vector3.down * 0.2f); yield return null; }
        look.SetLook(YawTo(player.position, p), 0f);
        yield return Real(0.4f);
        var cam = Camera.main.transform;
        Log("oak h " + oak.data.height.ToString("0.0") + " trunk r " + oak.TrunkRadius.ToString("0.00") + " camera from axis " + Flat(cam.position - p).ToString("0.00") + " body from axis " + Flat(cc.bounds.center - p).ToString("0.00"));
        yield return Shot("08_oak_level");
        look.SetLook(YawTo(player.position, p), 70f);
        yield return Real(0.4f);
        Log("looking down: camera from axis " + Flat(cam.position - p).ToString("0.00") + " near " + Camera.main.nearClipPlane);
        yield return Shot("09_oak_down");
        look.SetLook(YawTo(player.position, p) + 180f, -70f);
        yield return Real(0.4f);
        Log("looking up, back to it: camera from axis " + Flat(cam.position - p).ToString("0.00"));
        yield return Shot("10_oak_up_back");
        look.SetLook(YawTo(player.position, p), 0f);
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ---------------- seeds ---------------- //

    IEnumerator Seeds()
    {
        var forest = Forest.Instance;
        var list = (IList)typeof(Forest).GetField("seedsOnGround", Any).GetValue(forest);
        int seeds = 0;
        foreach (var s in list) if (s != null) { ((TreeSeed)s).data.age = ((TreeSeed)s).data.sproutAt; seeds++; }
        int before = forest.TreeCount;
        forest.Grow(0.01f);
        int after = forest.TreeCount;
        Log("seeds on the ground " + seeds + " -> new young trees " + (after - before));
        // a grown tree felled: its seeds fall
        ForestTree tall = null;
        float best = float.MaxValue;
        foreach (var t in forest.Trees)
        {
            if (t == null || t.Falling || t.data.height < t.Species.seedFromHeight + 1f) continue;
            float d = (t.data.position - player.position).sqrMagnitude;
            if (d < best) { best = d; tall = t; }
        }
        if (tall == null) yield break;
        int seedsBefore = forest.SeedCount;
        Teleport(tall.data.position + new Vector3(tall.TrunkRadius + 4f, 0f, 0f), YawTo(tall.data.position + new Vector3(tall.TrunkRadius + 4f, 0f, 0f), tall.data.position), 10f);
        yield return Real(0.5f);
        tall.Fall();
        yield return Real(7f);
        Log("felled a " + tall.Species.name + " " + tall.data.height.ToString("0") + " m: seeds on the ground " + seedsBefore + " -> " + forest.SeedCount);
        yield return Shot("11_felled");
    }

    // ---------------- the miracle stream ---------------- //

    IEnumerator Stream()
    {
        var stream = MiracleStream.Instance;
        Vector3 summit = stream.Summit;
        CanonCross.Show(summit, 0f);
        stream.Begin(summit, false);
        yield return null;
        var pts = stream.Points;
        Log("stream points " + pts.Count + " summit " + summit + " mouth " + stream.Mouth);
        using (var w = new StreamWriter(Dir + "stream.txt"))
            for (int i = 0; i < pts.Count; i += 5) w.WriteLine(i + " " + pts[i].x.ToString("0.0") + " " + pts[i].y.ToString("0.0") + " " + pts[i].z.ToString("0.0"));
        float[] at = { 0.02f, 0.08f, 0.16f, 0.28f, 0.45f, 0.7f, 0.92f };
        for (int k = 0; k < at.Length; k++)
        {
            int i = Mathf.Clamp(Mathf.RoundToInt(at[k] * (pts.Count - 1)), 0, pts.Count - 1);
            Vector3 p = pts[i];
            Vector3 along = pts[Mathf.Min(pts.Count - 1, i + 6)] - pts[Mathf.Max(0, i - 6)]; along.y = 0f; along.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, along);
            Teleport(p + side * 8f, 0f, 0f);
            yield return Real(0.8f);
            Vector3 eye = p + side * 7f - along * 9f; eye.y = Mathf.Max(Forest.GroundY(eye), p.y) + 3.2f;
            RenderWorld("12_stream_" + k, eye, p + along * 6f + Vector3.up * 0.3f);
        }
        // from above, the upper part
        int up = Mathf.Min(pts.Count - 1, pts.Count / 5);
        Teleport(pts[up], 0f, 0f);
        yield return Real(0.8f);
        RenderWorld("13_stream_above", pts[up] + new Vector3(-60f, 90f, -60f), pts[up], 55f);
    }

    // ---------------- Brother Ioan's paths ---------------- //

    IEnumerator PathViews()
    {
        var paths = IslandPaths.Data;
        if (paths == null) { Log("no paths saved"); yield break; }
        int line = 0;
        // the steps: each longer flight seen from out over the drop, and the stone for the cross
        int flight = 0;
        foreach (var l in paths.stairs)
        {
            var pts = l.points;
            Log("steps " + flight + ": " + pts.Count + " from " + pts[0].ToString("F0") + " to " + pts[pts.Count - 1].ToString("F0"));
            if (pts.Count >= 12 && flight < 8)
            {
                Vector3 mid = pts[pts.Count / 2];
                Vector3 along = pts[Mathf.Min(pts.Count - 1, pts.Count / 2 + 3)] - pts[Mathf.Max(0, pts.Count / 2 - 3)]; along.y = 0f; along.Normalize();
                Vector3 side = Vector3.Cross(Vector3.up, along);
                if (Forest.GroundY(mid + side * 3f) > Forest.GroundY(mid - side * 3f)) side = -side;
                Teleport(mid + Vector3.up * 1f, YawTo(mid, mid + along), 0f);
                yield return Real(1.2f);
                RenderWorld("15_steps_" + flight + "_side", mid + side * 16f + Vector3.up * 5f, mid);
                RenderWorld("15_steps_" + flight + "_on", mid - along * 5f + Vector3.up * 3.4f, mid + along * 8f + Vector3.up * 2f);
            }
            flight++;
        }
        if (paths.landingSize > 0f)
        {
            Vector3 s = paths.landing;
            Teleport(s + Vector3.up * 1f, 0f, 0f);
            yield return Real(1.2f);
            RenderWorld("16_stone_near", s + new Vector3(7f, 4f, -7f), s);
            RenderWorld("16_stone_far", s + new Vector3(60f, 25f, -60f), s + Vector3.down * 20f);
        }
        foreach (var l in paths.paths)
        {
            var pts = l.points;
            Log("path " + line + ": " + pts.Count + " points");
            if (pts.Count < 60 || line > 8) { line++; continue; }
            foreach (float at in new[] { 0.15f, 0.5f, 0.85f })
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(at * (pts.Count - 1)), 2, pts.Count - 3);
                Vector3 p = pts[i];
                Vector3 dir = pts[Mathf.Min(pts.Count - 1, i + 8)] - pts[Mathf.Max(0, i - 8)]; dir.y = 0f; dir.Normalize();
                Teleport(p - dir * 4f, YawTo(p, p + dir), 12f);
                yield return Real(1.2f);
                Vector3 eye = p - dir * 6f; eye.y = Forest.GroundY(eye) + 3.3f;
                RenderWorld("14_path_" + line + "_" + Mathf.RoundToInt(at * 100f), eye, p + dir * 14f + Vector3.up * (Forest.GroundY(p + dir * 14f) - p.y + 0.5f));
            }
            line++;
        }
    }

    // ---------------- rabbits ---------------- //

    IEnumerator Rabbits()
    {
        var population = RabbitPopulation.Instance;
        var live = population.GetType().GetMethod("Live", Any);
        var start = FindObjectOfType<StoryStart>();
        Teleport(start.transform.position + start.transform.forward * 30f, start.transform.eulerAngles.y, 5f);
        yield return Real(1f);
        int born = 0;
        System.Action<Vector3> onBorn = _ => born++;
        RabbitPopulation.BabyBorn += onBorn;
        int before = population.Count;
        int expecting0 = 0; foreach (var r in population.Rabbits) if (r != null && r.Alive && r.Expecting) expecting0++;
        for (int i = 0; i < 80; i++)
        {
            live.Invoke(population, new object[] { 1f });
            yield return Real(0.5f);
        }
        RabbitPopulation.BabyBorn -= onBorn;
        int expecting = 0, near = 0, nearBabies = 0;
        foreach (var r in population.Rabbits)
        {
            if (r == null || !r.Alive) continue;
            if (r.Expecting) expecting++;
            if (Flat(r.transform.position - player.position) < 140f) { near++; if (!r.IsAdult) nearBabies++; }
        }
        Log("rabbits " + before + " -> " + population.Count + " after 80 game hours, born " + born + ", expecting " + expecting0 + " -> " + expecting
            + ", near the player " + near + " (babies " + nearBabies + ")");
    }
}
#endif
