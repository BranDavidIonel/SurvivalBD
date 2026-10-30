#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TEMPORARY (Claude): plays the 10 levels from a new game, as a player would (the game's own buttons, aiming and
// clicks where it can, the craft button's code, items given where a long walk would only take time). Logs every goal
// into Temp/<folder>/levels.txt, with screenshots and the errors seen. Removed after the test.
public class __LevelTest : MonoBehaviour
{
    public static string Folder = "LevelTest";
    string Dir => Application.dataPath + "/../Temp/" + Folder + "/";
    StreamWriter log;
    Transform player;
    MouseMovement look;
    StoryManager story;
    int errors;
    readonly List<string> errorTexts = new List<string>();
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    void Log(string s) { log.WriteLine(Time.realtimeSinceStartup.ToString("0.0") + "  " + s); }
    static IEnumerator Real(float s) { yield return new WaitForSecondsRealtime(s); }

    void OnEnable() => Application.logMessageReceived += OnLog;
    void OnDisable() => Application.logMessageReceived -= OnLog;

    void OnLog(string text, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++;
        if (errorTexts.Count < 25) errorTexts.Add(type + ": " + text + "\n    " + (stack.Length > 400 ? stack.Substring(0, 400) : stack));
    }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Dir);
        log = new StreamWriter(Dir + "levels.txt", false) { AutoFlush = true };
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
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.timeSpeed = 1f;
        Log("errors seen: " + errors);
        foreach (var e in errorTexts) Log("  " + e);
        Log("finished");
        File.WriteAllText(Dir + "finished.txt", "ok");
    }

    // ---------------- helpers ---------------- //

    StoryData Data => (StoryData)typeof(StoryManager).GetField("data", Any).GetValue(story);
    bool Modal => (bool)typeof(StoryManager).GetField("modal", Any).GetValue(story);
    string Steps() { var d = Data; return d == null ? "-" : "level " + d.level + " steps " + System.Convert.ToString(d.stepsDone, 2); }
    string PoseInfo()
    {
        var fp = FencePlacing.Instance;
        if (fp == null || !FencePlacing.Active) return "not placing";
        var t = typeof(FencePlacing);
        return "centre " + ((Vector3)t.GetField("centre", Any).GetValue(fp)).ToString("F1") + " len " + ((float)t.GetField("length", Any).GetValue(fp)).ToString("0.0")
            + " yaw " + ((float)t.GetField("yaw", Any).GetValue(fp)).ToString("0") + " valid " + t.GetField("valid", Any).GetValue(fp) + " '" + t.GetField("why", Any).GetValue(fp) + "'";
    }

    // the way up the other mountain from its foot: the paths and steps that end at the stone for the cross, joined end
    // to end backwards, from the lowest point on
    static List<Vector3> ClimbRoute(IslandPaths.PathsData d)
    {
        var route = new List<Vector3>();
        if (d == null || d.landingSize <= 0f) return route;
        var lines = new List<List<Vector3>>();
        foreach (var l in d.paths) if (l != null && l.points.Count > 1) lines.Add(l.points);
        foreach (var l in d.stairs) if (l != null && l.points.Count > 1) lines.Add(l.points);
        var used = new HashSet<int>();
        Vector3 at = d.landing;
        for (int guard = 0; guard < 60; guard++)
        {
            int found = -1;
            bool reversed = false;
            for (int k = 0; k < lines.Count && found < 0; k++)
            {
                if (used.Contains(k)) continue;
                if (Flat(lines[k][lines[k].Count - 1] - at) < 2f) found = k;
                else if (Flat(lines[k][0] - at) < 2f) { found = k; reversed = true; }
            }
            if (found < 0) break;
            used.Add(found);
            var pts = new List<Vector3>(lines[found]);
            if (reversed) pts.Reverse();
            route.InsertRange(0, pts);
            at = pts[0];
            bool valley = false;
            foreach (var p in pts) if (p.y < -12f) valley = true;
            if (valley) break;
        }
        int low = 0;
        for (int i = 1; i < route.Count; i++) if (route[i].y < route[low].y) low = i;
        return route.GetRange(low, route.Count - low);
    }

    static void Press(GameInput.TouchAction a) { GameInput.PressTouch(a); GameInput.ReleaseTouch(a); }

    void Teleport(Vector3 p, float yaw, float pitch)
    {
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = new Vector3(p.x, Forest.GroundY(p) + 1.95f, p.z);
        cc.enabled = true;
        look.SetLook(yaw, pitch);
    }

    static float YawTo(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // the crosshair (the middle of the screen) on this point
    void AimAt(Vector3 target)
    {
        for (int i = 0; i < 4; i++)
        {
            Vector3 cam = Camera.main.transform.position;
            Vector3 d = target - cam;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            look.SetLook(yaw, Mathf.Clamp(pitch, -89f, 89f));
        }
    }

    void Give(string item, int n) { for (int i = 0; i < n; i++) InventorySystem.Instance.AddToIventory(item, false, false); }

    void Craft(Blueprint bp)
    {
        int before = InventorySystem.Instance.CountItem(bp.itemName);
        typeof(CraftingSystem).GetMethod("CraftAnyItem", Any).Invoke(CraftingSystem.Instance, new object[] { bp });
        Log("craft " + bp.itemName + ": " + (InventorySystem.Instance.CountItem(bp.itemName) > before ? "ok" : "FAILED"));
    }

    static GameObject FindItem(string name)
    {
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        foreach (var slot in EquipSystem.Instance.quickSlotsList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        return null;
    }

    // the phone's double tap on an inventory item (eat it, use it, put a tool in the quick bar)
    void UseItem(GameObject item)
    {
        if (item == null) { Log("  no item to use"); return; }
        var info = item.GetComponent<InventoryItem>();
        if (info.isConsumable)
        {
            typeof(InventoryItem).GetMethod("UseLikeRightClick", Any).Invoke(info, null);
            typeof(InventoryItem).GetMethod("FinishConsumption", Any).Invoke(info, null);
        }
        else if (info.isUseable)
        {
            item.SetActive(false);
            typeof(InventoryItem).GetMethod("UseItem", Any).Invoke(info, null);
        }
        else if (info.isEquippable)
        {
            typeof(InventoryItem).GetMethod("UseLikeRightClick", Any).Invoke(info, null);
            if (item.transform.parent != null) EquipSystem.Instance.SelectQuickSlotOf(item.transform.parent.gameObject);
        }
    }

    // take a thing lying in the world: stand by it, the crosshair on it, the Take button
    IEnumerator Take(Transform thing, string item)
    {
        int before = InventorySystem.Instance.CountItem(item);
        Vector3 p = thing.position;
        Vector3 from = p + (Flat(player.position - p) > 0.1f ? Vector3.ProjectOnPlane(player.position - p, Vector3.up).normalized : Vector3.forward) * 2.4f;
        Teleport(from, YawTo(from, p), 30f);
        yield return Real(0.6f);
        var r = thing.GetComponentInChildren<Renderer>();
        AimAt(r != null ? r.bounds.center : p);
        yield return Real(0.4f);
        Press(GameInput.TouchAction.Primary);
        yield return Real(0.6f);
        bool ok = InventorySystem.Instance.CountItem(item) > before;
        Log("  take " + item + ": " + (ok ? "ok" : "not taken"));
        lastTakeOk = ok;
    }
    bool lastTakeOk;

    IEnumerator NextLevel(int level, float timeout = 60f, int tries = 0)
    {
        float t = 0f;
        while (t < timeout)
        {
            var skip = GameObject.Find("Skip");
            if (skip != null && skip.GetComponent<Button>() != null) skip.GetComponent<Button>().onClick.Invoke();
            var d = Data;
            if (d != null && d.level > level && !Modal && Time.timeScale > 0f && GameObject.Find("Skip") == null) break;
            if (d != null && d.level > 10) break;
            t += 0.5f;
            yield return Real(0.5f);
        }
        var now = Data;
        bool passed = now != null && now.level > level;
        Log((passed ? "LEVEL " + level + " PASSED" : "LEVEL " + level + " NOT PASSED (" + Steps() + ") - skipped") + "  (" + t.ToString("0") + " s)");
        if (!passed && tries < 2) { story.SkipLevel(); yield return NextLevel(level, 30f, tries + 1); }
        yield return Real(1f);
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Dir + name + ".jpg", tex.EncodeToJPG(80));
        Destroy(tex);
    }

    void RenderWorld(string name, Vector3 from, Vector3 at)
    {
        var go = new GameObject("Test camera");
        var cam = go.AddComponent<Camera>();
        cam.CopyFrom(Camera.main);
        go.transform.SetPositionAndRotation(from, Quaternion.LookRotation(at - from, Vector3.up));
        var rt = new RenderTexture(1280, 720, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
        RenderTexture.active = null;
        File.WriteAllBytes(Dir + name + ".jpg", tex.EncodeToJPG(85));
        cam.targetTexture = null;
        Destroy(rt); Destroy(tex); Destroy(go);
    }

    void ClearInventory(params string[] keep)
    {
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot != null && slot.transform.childCount > 0 && System.Array.IndexOf(keep, InventorySystem.CleanName(slot.transform.GetChild(0).name)) < 0)
                DestroyImmediate(slot.transform.GetChild(0).gameObject);
        InventorySystem.Instance.RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();
    }

    // ---------------- the levels ---------------- //

    IEnumerator Run()
    {
        float t = 0f;
        while ((StoryManager.Instance == null || FindObjectOfType<PlayerMovement>() == null) && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
        story = StoryManager.Instance;
        player = FindObjectOfType<PlayerMovement>().transform;
        look = player.GetComponent<MouseMovement>();
        Log("screen " + Screen.width + "x" + Screen.height + " touch " + GameInput.TouchControls);
        // the intro (if it is told) and the first banner
        for (int i = 0; i < 20; i++)
        {
            var skip = GameObject.Find("Skip");
            if (skip != null) skip.GetComponent<Button>().onClick.Invoke();
            yield return Real(0.5f);
        }
        look.enabled = false;
        Log("start: " + Steps() + " (no goal may be done yet)");
        yield return Shot("L1_start");

        yield return Level1();
        yield return Level2();
        yield return Level3();
        yield return Level4();
        yield return Level5();
        yield return Level6();
        yield return Level7();
        yield return Level8();
        yield return Level9();
        yield return Level10();
    }

    Campfire fire;

    IEnumerator Level1()
    {
        var start = FindObjectOfType<StoryStart>();
        Vector3 f = Vector3.ProjectOnPlane(start.transform.forward, Vector3.up).normalized;
        Give("stone", 6);
        yield return Real(1.2f);
        Log("6 stones: " + Steps());
        Craft(CraftingSystem.Instance.CampfireBLP);
        yield return Real(1f);
        Log("campfire crafted: " + Steps());
        Vector3 spot = start.transform.position + f * 30f;
        Teleport(spot, YawTo(spot, spot + f), 38f);
        yield return Real(0.8f);
        UseItem(FindItem("Campfire"));
        yield return Real(1f);
        Press(GameInput.TouchAction.Primary);
        yield return Real(1f);
        foreach (var c in FindObjectsOfType<Campfire>())
            if (PlacementSystem.Instance == null || !c.transform.IsChildOf(PlacementSystem.Instance.placementHoldingSpot.transform)) fire = c;
        Log("campfire placed: " + (fire != null) + "  " + Steps());
        Give("stick", 3);
        Give("hay", 2);
        yield return Real(1.2f);
        Log("wood and hay: " + Steps());
        if (fire != null)
        {
            // the "Light Fire" button: a stick in the fire and one hay
            InventorySystem.Instance.RemoveItem("stick", 1);
            InventorySystem.Instance.RemoveItem("hay", 1);
            fire.AddFuel(30f);
        }
        yield return Real(1.2f);
        Log("fire lit (" + (fire != null && fire.IsBurning) + "): " + Steps());
        yield return Shot("L1_fire");
        // the owner's bug: a night spent by the fire already now must make Level 4 ("The first night") pass at once
        if (fire != null)
        {
            Vector3 by = fire.transform.position + Vector3.right * 4f;
            Teleport(by, YawTo(by, fire.transform.position), 20f);
            fire.AddFuel(10f * 60f);
            DayNightSystem.Instance.TimeOfDay = 22f / 24f;
            yield return Real(2f);
            DayNightSystem.Instance.TimeOfDay = 7f / 24f;
            yield return Real(2f);
            Log("a night by the fire at level 1: nightByFire " + typeof(StoryData).GetField("nightByFire")?.GetValue(Data) + "  " + Steps());
        }
        yield return NextLevel(1);
    }

    IEnumerator Level2()
    {
        var river = River.Instance;
        Vector3 p = river.points[river.points.Count * 2 / 3];
        Vector3 along = Vector3.ProjectOnPlane(river.points[river.points.Count * 2 / 3 + 1] - p, Vector3.up).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, along);
        float hw = river.halfWidths.Count > 0 ? river.halfWidths[river.points.Count * 2 / 3] : 3f;
        Vector3 bank = p + side * (hw + 1.2f);
        Teleport(bank, YawTo(bank, p), 45f);
        yield return Real(2f);
        Log("at the river: " + Steps());
        FreshWater.Instance.Drink(player.position);
        yield return Real(1.2f);
        Log("drank: " + Steps());
        yield return Shot("L2_river");
        // the new steps: a stone axe and a torch (they help at night)
        Give("stone", 1);
        Give("stick", 3);
        Give("hay", 2);
        Craft(CraftingSystem.Instance.AxeBLP);
        yield return Real(0.8f);
        Log("stone axe made: " + Steps());
        Craft(CraftingSystem.Instance.TorchBLP);
        yield return Real(1.2f);
        Log("torch made: " + Steps());
        yield return NextLevel(2);
    }

    IEnumerator Level3()
    {
        var picked = new List<ScenePlant>();
        string[] mushrooms = { "porcini", "redmushroom", "fieldmushroom" };
        var all = new List<ScenePlant>(FindObjectsOfType<ScenePlant>());
        all.Sort((a, b) => Flat(a.transform.position - player.position).CompareTo(Flat(b.transform.position - player.position)));
        foreach (var plant in all)
        {
            if (picked.Count >= 2) break;
            if (System.Array.IndexOf(mushrooms, plant.item) < 0) continue;
            var r = plant.GetComponentInChildren<Renderer>();
            if (r == null || !r.enabled) continue;
            yield return Take(plant.transform, plant.item);
            if (lastTakeOk) picked.Add(plant);
        }
        if (picked.Count < 2) { Log("  (mushrooms given)"); Give("porcini", 2 - picked.Count); }
        yield return Real(1.2f);
        Log("2 mushrooms: " + Steps());
        foreach (var m in mushrooms) for (int i = 0; i < 2; i++) { var item = FindItem(m); if (item != null && Data.mushroomsEaten < 2) { UseItem(item); yield return Real(0.3f); } }
        yield return Real(1.2f);
        Log("eaten: " + Steps());
        yield return NextLevel(3);
    }

    IEnumerator Level4()
    {
        // the night at level 1 counts: the level must be over already (or right after its banner)
        float waited = 0f;
        while (Data.level == 4 && waited < 8f) { waited += 0.5f; yield return Real(0.5f); }
        if (Data.level > 4) { Log("LEVEL 4 passed by itself (the night by the fire at level 1 counted) after " + waited.ToString("0.0") + " s"); yield break; }
        Log("  level 4 did NOT pass by itself: " + Steps());
        // a stone axe and a tree: logs (the way a player gets them)
        Give("stone", 1);
        Give("stick", 2);
        Craft(CraftingSystem.Instance.AxeBLP);
        Give("log", 3);
        yield return Real(1.2f);
        Log("firewood: " + Steps());
        if (fire != null)
        {
            Vector3 by = fire.transform.position + Vector3.right * 4f;
            Teleport(by, YawTo(by, fire.transform.position), 20f);
            fire.AddFuel(14f * 60f);
        }
        DayNightSystem.Instance.TimeOfDay = 21f / 24f;
        yield return Real(2f);
        Log("night, fire burning (" + (fire != null && fire.IsBurning) + "): " + Steps());
        yield return Shot("L4_night");
        DayNightSystem.Instance.TimeOfDay = 6.5f / 24f;
        yield return Real(2f);
        Log("morning: " + Steps());
        yield return NextLevel(4);
    }

    IEnumerator Level5()
    {
        var cave = Cave.Instance;
        Vector3 outward = Vector3.ProjectOnPlane(cave.path[0] - cave.path[3], Vector3.up).normalized;
        Vector3 at = cave.Mouth + outward * 6f;
        Teleport(at, YawTo(at, cave.Mouth), 5f);
        yield return Real(2f);
        Log("at the cave (open " + cave.IsOpen + "): " + Steps());
        yield return Shot("L5_cave");
        yield return NextLevel(5);
    }

    IEnumerator Level6()
    {
        Give("stick", 1);
        Give("hay", 2);
        Craft(CraftingSystem.Instance.TorchBLP);
        yield return Real(1f);
        UseItem(FindItem("torch"));
        yield return Real(1.2f);
        Log("torch in the hand (burning " + TorchLight.Burning + "): " + Steps());
        var cave = Cave.Instance;
        Vector3 deep = cave.path[cave.path.Count - 4];
        Teleport(deep, YawTo(deep, cave.path[cave.path.Count - 1]), 10f);
        yield return Real(2f);
        Log("deep in the cave: " + Steps());
        yield return Shot("L6_cave_end");
        BookReader.Open(BookReader.Book.Journal);
        yield return Real(1.5f);
        yield return Shot("L6_journal");
        if (BookReader.Instance != null) typeof(BookReader).GetMethod("Close", Any).Invoke(BookReader.Instance, null);
        yield return Real(1.2f);
        Log("journal read: " + Steps());
        ScenePlant book = null;
        foreach (var p in FindObjectsOfType<ScenePlant>()) if (p.item == "prayerbook") book = p;
        if (book != null) yield return Take(book.transform, "prayerbook");
        if (InventorySystem.Instance.CountItem("prayerbook") == 0) { Log("  (prayer book given)"); Give("prayerbook", 1); }
        yield return Real(1.5f);
        Log("prayer book: " + Steps());
        yield return NextLevel(6);
    }

    IEnumerator Level7()
    {
        // put the torch away, far from the fire, at night
        var equip = EquipSystem.Instance;
        if (equip.selectedItemModel != null) Destroy(equip.selectedItemModel);
        equip.selectedItemModel = null;
        var torch = FindItem("torch");
        if (torch != null) DestroyImmediate(torch);
        equip.selectedItem = null;
        equip.selectedNumber = -1;
        InventorySystem.Instance.RecalculateList();
        var start = FindObjectOfType<StoryStart>();
        Vector3 away = start.transform.position + Vector3.ProjectOnPlane(start.transform.forward, Vector3.up).normalized * 120f;
        Teleport(away, 30f, 0f);
        DayNightSystem.Instance.TimeOfDay = 22.5f / 24f;
        yield return Real(1.5f);
        Log("night, alone: " + Steps());
        float t = 0f;
        while (!(NightShadow.Instance != null && NightShadow.Instance.IsHunting) && t < 60f) { t += 0.5f; yield return Real(0.5f); }
        Log("the shadow comes (" + (NightShadow.Instance != null && NightShadow.Instance.IsHunting) + " after " + t.ToString("0") + " s)");
        yield return Real(2f);
        yield return Shot("L7_shadow");
        BookReader.Open(BookReader.Book.PrayerBook);
        yield return Real(1.5f);
        yield return Shot("L7_prayer");
        if (BookReader.Instance != null) typeof(BookReader).GetMethod("Close", Any).Invoke(BookReader.Instance, null);
        yield return Real(1.5f);
        Log("prayer book opened (drove it away " + BookReader.DroveShadowAway + "): " + Steps());
        yield return NextLevel(7);
        DayNightSystem.Instance.TimeOfDay = 9f / 24f;
    }

    IEnumerator Level8()
    {
        // the iron ore lies in the cave: take one the player's way, the rest given
        var cave = Cave.Instance;
        ScenePlant ore = null;
        foreach (var p in FindObjectsOfType<ScenePlant>()) if (p.item == "ironore") { ore = p; break; }
        if (ore != null) yield return Take(ore.transform, "ironore");
        Give("ironore", Mathf.Max(0, 10 - InventorySystem.Instance.CountItem("ironore")));
        Give("log", 1);
        yield return Real(1.2f);
        Log("ore and log: " + Steps());
        Craft(CraftingSystem.Instance.IronAxeBLP);
        yield return Real(1.5f);
        Log("iron axe: " + Steps());
        yield return NextLevel(8);
    }

    IEnumerator Level9()
    {
        ClearInventory("prayerbook", "journal", "ironaxe");
        var population = RabbitPopulation.Instance;
        var start = FindObjectOfType<StoryStart>();
        Vector3 f = Vector3.ProjectOnPlane(start.transform.forward, Vector3.up).normalized;
        Vector3 r = Vector3.Cross(Vector3.up, f);
        // a snare, set the player's way near rabbits
        Give("stick", 1);
        Give("hemp", 5);
        Craft(CraftingSystem.Instance.SnareBLP);
        RabbitLife near = null;
        foreach (var rb in population.Rabbits) if (rb != null && rb.Alive && rb.IsAdult && (near == null || Flat(rb.transform.position - player.position) < Flat(near.transform.position - player.position))) near = rb;
        if (near != null)
        {
            Vector3 by = near.transform.position + Vector3.right * 5f;
            Teleport(by, YawTo(by, near.transform.position), 30f);
            yield return Real(0.6f);
            UseItem(FindItem("snare"));
            yield return Real(0.6f);
            AimAt(Vector3.Lerp(player.position, near.transform.position, 0.7f));
            yield return Real(0.4f);
            Press(GameInput.TouchAction.Primary);
            yield return Real(0.8f);
            Log("snare set: " + RabbitSnare.All.Count);
            DayNightSystem.Instance.timeSpeed = 20f;
            float t = 0f;
            RabbitSnare caught = null;
            while (t < 40f && caught == null) { foreach (var s in RabbitSnare.All) if (s != null && s.caught != null && s.caught.Alive) caught = s; t += 0.5f; yield return Real(0.5f); }
            DayNightSystem.Instance.timeSpeed = 1f;
            if (caught != null)
            {
                Log("a rabbit in the snare after " + t.ToString("0") + " s: " + caught.caught.sex);
                yield return Shot("L9_snare");
                population.TakeAlive(caught.caught); // (empty hands: Take)
            }
            else Log("  nothing in the snare after 40 s");
        }
        // the other one(s) from the wild, as from a snare
        foreach (var sex in new[] { RabbitInfo.Sex.Male, RabbitInfo.Sex.Female })
        {
            string item = sex == RabbitInfo.Sex.Male ? RabbitPopulation.LiveMale : RabbitPopulation.LiveFemale;
            if (InventorySystem.Instance.CountItem(item) > 0) continue;
            foreach (var rb in population.Rabbits) if (rb != null && rb.Alive && rb.IsAdult && rb.sex == sex) { population.TakeAlive(rb); break; }
        }
        yield return Real(1.2f);
        Log("male and female alive: " + Steps());

        // the fenced yard, put up panel after panel the player's way
        Give(Fences.FenceItem, 9);
        Give(Fences.GateItem, 1);
        Vector3 c = start.transform.position + f * 70f;
        var corners = new[] { c - r * 6f - f * 4f, c + r * 6f - f * 4f, c + r * 6f + f * 4f, c - r * 6f + f * 4f };
        var panels = new List<(Vector3 a, Vector3 b, Vector3 outward)>();
        for (int k = 0; k < 4; k++)
        {
            Vector3 a = corners[k], b = corners[(k + 1) % 4];
            Vector3 outward = Vector3.ProjectOnPlane((a + b) * 0.5f - c, Vector3.up).normalized;
            int n = Mathf.RoundToInt(Flat(b - a) / Fences.Length);
            for (int i = 0; i < n; i++) panels.Add((Vector3.Lerp(a, b, (float)i / n), Vector3.Lerp(a, b, (float)(i + 1) / n), outward));
        }
        for (int i = 0; i < panels.Count; i++)
        {
            var pn = panels[i];
            string item = i == 1 ? Fences.GateItem : Fences.FenceItem;
            Vector3 target = i == 0 ? (pn.a + pn.b) * 0.5f : pn.b;
            Vector3 stand = target + pn.outward * 8f;
            Teleport(stand, YawTo(stand, target), 20f);
            yield return Real(0.3f);
            if (!FencePlacing.Active || i == 1 || i == 2) { if (FencePlacing.Active) FencePlacing.Instance.Cancel(); FencePlacing.Instance.Begin(FindItem(item)); }
            AimAt(new Vector3(target.x, Forest.GroundY(target), target.z));
            yield return Real(0.3f);
            string pose = PoseInfo();
            int before = Buildings.Count(BuildingKind.Fence) + Buildings.Count(BuildingKind.FenceGate);
            Press(GameInput.TouchAction.Primary);
            yield return Real(0.4f);
            int after = Buildings.Count(BuildingKind.Fence) + Buildings.Count(BuildingKind.FenceGate);
            Log("    panel " + i + " (" + item + ") aim " + target.ToString("F1") + ": " + pose + (after > before ? "  placed" : "  NOT placed"));
        }
        if (FencePlacing.Active) FencePlacing.Instance.Cancel();
        yield return Real(1.2f);
        Log("yard: fence pieces " + Buildings.Count(BuildingKind.Fence) + " + gates " + Buildings.Count(BuildingKind.FenceGate) + ", free ends " + Fences.FreeEnds().Count + ", closed " + Fences.YardBuilt() + "  " + Steps());
        Vector3 viewFrom = c - f * 16f - r * 10f;
        Teleport(viewFrom, YawTo(viewFrom, c), 14f);
        yield return Real(1f);
        yield return Shot("L9_yard");
        // the two rabbits let go inside
        Vector3 inside = c - f * 2f;
        Teleport(inside, YawTo(inside, c + f * 3f), 10f);
        yield return Real(0.5f);
        UseItem(FindItem(RabbitPopulation.LiveMale));
        yield return Real(0.4f);
        look.SetLook(YawTo(inside, c + r * 3f), 10f);
        UseItem(FindItem(RabbitPopulation.LiveFemale));
        yield return Real(1f);
        Log("pair in the yard: " + StoryManager.PairInPen() + "  " + Steps());
        yield return Shot("L9_rabbits");
        // the gate opened: the goal stays done; a grown rabbit in the yard can be taken alive by hand
        foreach (var door in PenDoor.All) if (door != null && !door.IsOpen) door.SetOpen(true, 1);
        yield return Real(1.5f);
        Log("gate open: pair in the yard " + StoryManager.PairInPen() + ", goal kept " + ((Data.stepsDone & 4) != 0));
        RabbitLife penned = null;
        foreach (var rb in population.Rabbits) if (rb != null && RabbitPopulation.InPen(rb)) { penned = rb; break; }
        Log("a rabbit in the yard can be taken by hand: " + (penned != null));
        foreach (var door in PenDoor.All) if (door != null && door.IsOpen) door.SetOpen(false, 1);

        // 10 trees planted the player's way
        Give("acorn", 10);
        Vector3 field = c + f * 40f;
        Teleport(field, 0f, 0f);
        yield return Real(0.5f);
        for (int i = 0; i < 10; i++)
        {
            var seed = FindItem("acorn");
            if (seed == null) break;
            UseItem(seed);
            yield return Real(0.2f);
            Vector3 spot = field + Quaternion.Euler(0f, i * 36f, 0f) * Vector3.forward * 5f;
            AimAt(new Vector3(spot.x, Forest.GroundY(spot), spot.z));
            yield return Real(0.3f);
            Press(GameInput.TouchAction.Primary);
            yield return Real(0.3f);
            if (SeedPlanting.Active) SeedPlanting.Instance.Cancel();
        }
        yield return Real(1.2f);
        Log("trees planted " + Data.treesPlanted + ": " + Steps());

        // the cross, raised last on the other mountain
        Give("plank", 5);
        Craft(CraftingSystem.Instance.CrossBLP);
        yield return Real(1f);
        Log("cross made: " + Steps());
        var stream = MiracleStream.Instance;
        Vector3 top = stream.Summit;
        // up the other mountain on foot: Brother Ioan's path and steps from the foot of the climb to the stone
        var pathsData = IslandPaths.Data;
        var route = ClimbRoute(pathsData);
        Log("the climb: " + route.Count + " points, steps built " + (IoanSteps.Instance != null) + ", stone " + pathsData.landing);
        if (route.Count > 2)
        {
            Teleport(route[0] + Vector3.up * 2f, YawTo(route[0], route[1]), 0f);
            yield return Real(1f);
            int idx = 1;
            float t = 0f, lastProgress = 0f, bestY = player.position.y;
            int lastIdx = idx;
            __WalkDriver.On = true;
            __WalkDriver.Move = new Vector2(0f, 1f);
            while (t < 150f)
            {
                Vector3 at = Camera.main != null ? Camera.main.transform.position : player.position; // (a player steers by what they see: the body is around the camera, not the root)
                while (idx < route.Count - 1 && Flat(route[idx] - at) < 2.2f) idx++;
                look.SetLook(YawTo(at, route[idx]), 0f);
                if (idx > lastIdx) { lastIdx = idx; lastProgress = t; }
                bestY = Mathf.Max(bestY, at.y);
                if (idx >= route.Count - 1 && Flat(route[route.Count - 1] - at) < 2.5f) break;
                if (t - lastProgress > 8f) { Log("  stuck at " + at.ToString("F1") + " (point " + idx + " of " + route.Count + ", " + route[idx].ToString("F1") + ")"); break; }
                if (Mathf.FloorToInt(t / 5f) != Mathf.FloorToInt((t - Time.unscaledDeltaTime) / 5f)) Log("  climbing: " + t.ToString("0") + " s, height " + at.y.ToString("0.0") + ", point " + idx + " of " + route.Count);
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            __WalkDriver.On = false;
            Log("the climb on foot: " + (idx >= route.Count - 1 ? "at the top" : "NOT at the top") + " after " + t.ToString("0") + " s, highest " + bestY.ToString("0.0") + " m (the stone " + pathsData.landing.y.ToString("0.0") + ")");
            yield return Shot("L9_climb_top");
        }
        Vector3 crossAt = pathsData.landingSize > 0f ? pathsData.landing : top;
        if (Flat(player.position - crossAt) > 6f)
        {
            Vector3 below = crossAt + Vector3.right * 4f;
            Teleport(below + Vector3.up * 2f, YawTo(below, crossAt), 20f);
        }
        yield return Real(1f);
        UseItem(FindItem("cross"));
        yield return Real(0.5f);
        AimAt(crossAt);
        yield return Real(0.4f);
        Press(GameInput.TouchAction.Primary);
        yield return Real(3f);
        Log("cross raised (" + Data.crossPlaced + "), stream flowing " + stream.Flowing + ": " + Steps());
        yield return Real(6f);
        RenderWorld("L9_stream_top", top + new Vector3(10f, 8f, 10f), stream.Points.Count > 20 ? stream.Points[20] : top);
        yield return NextLevel(9);
    }

    IEnumerator Level10()
    {
        ClearInventory("prayerbook", "journal", "ironaxe");
        Give("hemp", 18);
        yield return Real(1.2f);
        Log("hemp: " + Steps());
        for (int i = 0; i < 6; i++) Craft(CraftingSystem.Instance.RopeBLP);
        yield return Real(1.2f);
        Log("ropes: " + Steps());
        var raft = Raft.Instance != null ? Raft.Instance : Raft.MakeSite(null);
        Vector3 at = raft.transform.position + Vector3.right * 15f;
        Teleport(at, YawTo(at, raft.transform.position), 15f);
        yield return Real(1.5f);
        Log("at the raft: " + Steps());
        yield return Shot("L10_raft");
        // many trips: what it needs, a few at a time (the inventory has 18 places)
        var needs = new List<string>();
        for (int k = 0; k < Raft.Needs.Length; k++)
            for (int a = 0; a < Raft.Amounts[k]; a++)
                needs.Add(Raft.Needs[k] == Raft.LiveRabbits ? RabbitPopulation.LiveMale : Raft.Needs[k]);
        needs.RemoveAll(x => x == "rope"); // (the 6 ropes are carried already)
        int trips = 0;
        while (needs.Count > 0 && trips < 12)
        {
            int room = InventorySystem.Instance.EmptySlotCount();
            for (int i = 0; i < room && needs.Count > 0; i++) { Give(needs[0], 1); needs.RemoveAt(0); }
            raft.Use();
            trips++;
            yield return Real(0.6f);
        }
        Log("raft complete " + raft.IsComplete + " after " + trips + " trips: " + Steps());
        float t = 0f;
        while (t < 30f && !(bool)(typeof(Raft).GetProperty("InWater", Any)?.GetValue(raft) ?? true)) { t += 0.5f; yield return Real(0.5f); }
        yield return Shot("L10_launched");
        bool ended = false;
        System.Action onBoard = () => ended = true;
        Raft.Boarded += onBoard;
        raft.Use(); // in the water: board it
        yield return Real(3f);
        Raft.Boarded -= onBoard;
        Log("boarded: " + ended + "  " + Steps());
        yield return Shot("L10_ending");
        yield return Real(9f);
        yield return Shot("L10_ending_line2");
        yield return Real(17f);
        yield return Shot("L10_the_end");
        t = 0f;
        while (SceneManager.GetActiveScene().name != "MainMenu" && t < 90f) { t += 1f; yield return Real(1f); }
        Log("the end: back in " + SceneManager.GetActiveScene().name + " after " + t.ToString("0") + " s");
        yield return Real(2f);
        yield return Shot("L10_main_menu");
    }
}
#endif
