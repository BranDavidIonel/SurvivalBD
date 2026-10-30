#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// TEMPORARY (Claude): the end-to-end test of the game's systems, played the way a player plays: the crafting cards'
// Craft buttons, items used from the inventory (the phone's double tap), the crosshair on things and the big primary
// button (GameInput.PressTouch, with GameInput.TestDriven on PC), so the same test runs in the phone Simulator and in
// the PC Game view. Materials are given instead of walking far.
//   1 campfire (craft, place, light, cook)   2 stone axe / iron axe / planks   3 torch at night (the shadow)
//   4 snare (a rabbit caught, taken alive)   5 storage box   6 fenced yard, foundation + wall   7 seeds and forests
//   8 water and food   9 hunting and looting   10 spear   11 windows   12 save and load   13 admin safety
// Writes Temp/<Folder>/systems.txt (one line per check: PASS / FAIL / SKIP + what was seen), PNG screenshots, and
// Temp/<Folder>/finished.txt at the end. The story is held still while it runs (its goals are not checked: that is
// the level test). Added by the coordinator's editor driver when Play mode starts in GameScene. Removed after the tests.
public class __SystemsTest : MonoBehaviour
{
    public static string Folder = "Systems";
    const int TestSlot = 47;           // a save slot the game's screens never show (the owner's saves are 1-10)
    const float MaxRunSeconds = 330f;  // after this the checks left are skipped (the whole test is about 4 minutes)

    static __SystemsTest running;
    string Dir => Application.dataPath + "/../Temp/" + Folder + "/";
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    StreamWriter log;
    int passed, failed, skipped, shots;
    int errors;
    readonly List<string> errorTexts = new List<string>();
    string current = "";
    float runStart;
    bool runImmortal;   // the player is Immortal for the run (after check 13)
    bool frozen;        // the story is held still

    Transform player;
    MouseMovement look;
    StoryManager story;
    Vector3 S, F, R;    // the beach of the story start, its forward and right (the checks happen on the land in front)

    // ---------------- the run ---------------- //

    void OnEnable() => Application.logMessageReceived += OnLog;
    void OnDisable() => Application.logMessageReceived -= OnLog;

    void OnLog(string text, string stack, LogType type)
    {
        if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
        errors++;
        if (errorTexts.Count < 25) errorTexts.Add("[" + current + "] " + type + ": " + text + "\n      " + (stack.Length > 300 ? stack.Substring(0, 300) : stack));
    }

    IEnumerator Start()
    {
        // one test at a time, on its own object (it lives on through the scene reload of the save/load check)
        if (running != null && running != this) { Destroy(this); yield break; }
        if (transform.parent != null || GetComponents<Component>().Length > 2)
        {
            new GameObject("__SystemsTest").AddComponent<__SystemsTest>();
            Destroy(this);
            yield break;
        }
        running = this;
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Dir);
        foreach (var old in Directory.GetFiles(Dir, "*.png")) File.Delete(old);
        if (File.Exists(Dir + "finished.txt")) File.Delete(Dir + "finished.txt");
        log = new StreamWriter(Dir + "systems.txt", false) { AutoFlush = true };
        runStart = Time.realtimeSinceStartup;
        GameInput.TestDriven = true;
        Line("SurvivalBD systems test, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));

        var checks = new List<(string name, Func<IEnumerator> body, float timeout)>
        {
            ("setup", Setup, 45f),
            ("13 admin safety", AdminCheck, 10f),
            ("11 windows", WindowsCheck, 35f),
            ("1 campfire", CampfireCheck, 45f),
            ("2 axes and planks", AxeCheck, 70f),
            ("3 torch", TorchCheck, 40f),
            ("4 snare", SnareCheck, 75f),
            ("5 storage box", StorageCheck, 35f),
            ("6 building", BuildingCheck, 45f),
            ("7 seeds", SeedsCheck, 40f),
            ("8 water and food", WaterFoodCheck, 25f),
            ("9 hunting", HuntingCheck, 40f),
            ("10 spear", SpearCheck, 50f),
            ("12 save and load", SaveLoadCheck, 70f),
        };
        bool ready = false;
        foreach (var c in checks)
        {
            if (c.name != "setup" && !ready) { Skip(c.name, "the game scene was not ready (see setup)"); continue; }
            if (Time.realtimeSinceStartup - runStart > MaxRunSeconds) { Skip(c.name, "the test ran out of time"); continue; }
            Line("");
            Line("---- " + c.name + " ----");
            yield return Drive(c.name, c.body(), c.timeout);
            if (c.name == "setup") ready = player != null && story != null;
            yield return Tidy();
        }
        Finish();
    }

    // Runs one check: each step in a try/catch (an exception fails the check, the next one runs), never longer than
    // its timeout. Nested waits (IEnumerator) are run here too, so a timeout or an exception inside them is caught.
    IEnumerator Drive(string name, IEnumerator body, float timeout)
    {
        current = name;
        float start = Time.realtimeSinceStartup;
        var stack = new Stack<IEnumerator>();
        stack.Push(body);
        while (stack.Count > 0)
        {
            if (Time.realtimeSinceStartup - start > timeout)
            {
                Fail(name + ": timeout", "stopped after " + timeout.ToString("0") + " s");
                yield break;
            }
            var top = stack.Peek();
            bool more;
            try { more = top.MoveNext(); }
            catch (Exception e)
            {
                Fail(name + ": exception", e.GetType().Name + ": " + e.Message + "  at " + FirstLines(e.StackTrace, 2));
                yield break;
            }
            if (!more) { stack.Pop(); continue; }
            object y = top.Current;
            if (y is IEnumerator inner && !(y is CustomYieldInstruction)) { stack.Push(inner); continue; }
            yield return y;
        }
    }

    void Finish()
    {
        try { RestoreGame(); } catch (Exception e) { Line("restore: " + e.Message); }
        Line("");
        Line("SUMMARY: " + passed + " passed, " + failed + " failed, " + skipped + " skipped, " + shots + " screenshots, "
            + (Time.realtimeSinceStartup - runStart).ToString("0") + " s");
        Line("errors and exceptions in the console: " + errors);
        foreach (var e in errorTexts) Line("  " + e);
        log.Close();
        File.WriteAllText(Dir + "finished.txt", passed + " passed, " + failed + " failed, " + skipped + " skipped");
        running = null;
    }

    // back to a normal game: the admin locked, the clock normal, the story going on, the player's own controls
    void RestoreGame()
    {
        GameInput.TestDriven = false;
        GameInput.ResetTouch();
        GameSettings.Immortal = false;
        SetAdmin(false);
        var clock = DayNightSystem.Instance;
        if (clock != null) { clock.timeSpeed = 1f; clock.autoTime = true; }
        if (story != null && frozen) SetField(story, "modal", false);
        frozen = false;
        if (look != null) look.enabled = true;
        if (Time.timeScale <= 0f && !HelpScreen.IsOpen) Time.timeScale = 1f;
        Line("restored: admin locked " + !DebugEnvironmentPanel.AdminUnlocked + ", Immortal " + GameSettings.Immortal + ", TestDriven " + GameInput.TestDriven);
    }

    // ---------------- the log ---------------- //

    void Line(string s) { if (log != null) log.WriteLine(s); }
    void Pass(string what, string seen) { passed++; Line("PASS  " + what + "  -  " + seen); }
    void Fail(string what, string seen) { failed++; Line("FAIL  " + what + "  -  " + seen); }
    void Skip(string what, string why) { skipped++; Line("SKIP  " + what + "  -  " + why); }
    void Check(string what, bool ok, string seen) { if (ok) Pass(what, seen); else Fail(what, seen); }
    void Note(string s) { Line("      " + s); }

    static string FirstLines(string text, int n)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var lines = text.Split('\n');
        return string.Join(" | ", lines, 0, Mathf.Min(n, lines.Length)).Trim();
    }

    // ---------------- helpers (most from __LevelTest) ---------------- //

    static IEnumerator Wait(float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end) yield return null;
    }

    // a press of an on-screen button (the phone's buttons, the PC's buttons and keys go the same way with TestDriven)
    static IEnumerator Tap(GameInput.TouchAction action)
    {
        GameInput.PressTouch(action);
        GameInput.ReleaseTouch(action);
        yield return null;
        yield return null;
        yield return Wait(0.2f);
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D tex = null;
        try
        {
            tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Dir + name + ".png", tex.EncodeToPNG());
            shots++;
        }
        catch (Exception e) { Note("screenshot " + name + " failed: " + e.Message); }
        finally { if (tex != null) Destroy(tex); }
    }

    static T GetField<T>(object o, string name)
    {
        if (o == null) return default;
        var f = o.GetType().GetField(name, Any);
        return f != null && f.GetValue(o) is T v ? v : default;
    }

    static void SetField(object o, string name, object value)
    {
        var f = o != null ? o.GetType().GetField(name, Any) : null;
        if (f != null) f.SetValue(o, value);
    }

    static void SetAdmin(bool unlocked)
    {
        var p = typeof(DebugEnvironmentPanel).GetProperty("AdminUnlocked", Any);
        var set = p != null ? p.GetSetMethod(true) : null;
        if (set != null) set.Invoke(null, new object[] { unlocked });
    }

    StoryData Data => story != null ? GetField<StoryData>(story, "data") : null;

    void Teleport(Vector3 p, float yaw, float pitch)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = new Vector3(p.x, Forest.GroundY(p) + 1.95f, p.z);
        if (cc != null) cc.enabled = true;
        look.SetLook(yaw, pitch);
    }

    static float YawTo(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
    static Vector3 FlatDir(Vector3 v, Vector3 fallback) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : fallback; }

    // the crosshair (the middle of the screen) on this point
    void AimAt(Vector3 target)
    {
        var cam = Camera.main;
        if (cam == null) return;
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = target - cam.transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            look.SetLook(yaw, Mathf.Clamp(pitch, -89f, 89f));
        }
    }

    // the middle of what is seen of a thing
    static Vector3 Centre(Transform t)
    {
        if (t == null) return Vector3.zero;
        bool any = false;
        var b = new Bounds(t.position, Vector3.zero);
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        if (!any)
        {
            var c = t.GetComponentInChildren<Collider>();
            if (c != null) return c.bounds.center;
        }
        return any ? b.center : t.position;
    }

    // the middle of a rabbit's body (its solid box; its renderers move with the running animation)
    static Vector3 RabbitCentre(RabbitLife r)
    {
        if (r == null) return Vector3.zero;
        var box = r.GetComponent<Collider>();
        return box != null && box.enabled ? box.bounds.center : Centre(r.transform);
    }

    static bool IsLand(Vector3 p)
    {
        float y = Forest.GroundY(p);
        if (Ocean.Instance != null && y < Ocean.Instance.Level + 0.8f) return false;
        return !River.IsWater(p, 1.5f);
    }

    static bool IsDark() => SkyLighting.Instance != null ? SkyLighting.Instance.DayLight < 0.1f
        : DayNightSystem.Instance != null && (DayNightSystem.Instance.Hour >= 20.5f || DayNightSystem.Instance.Hour < 4.5f);

    static void Give(string item, int n) { for (int i = 0; i < n; i++) InventorySystem.Instance.AddToIventory(item, false, false); }
    static int Count(string item) => InventorySystem.Instance.CountItem(item);

    static string HeldName()
    {
        var equip = EquipSystem.Instance;
        return equip != null && equip.selectedItem != null ? InventorySystem.CleanName(equip.selectedItem.name) : "";
    }
    static bool IsHeld(string item) => HeldName() == item;
    static int CountAll(string item) => Count(item) + (IsHeld(item) ? 1 : 0); // (CountItem leaves out the item in the hand)

    static GameObject FindItem(string name)
    {
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        foreach (var slot in EquipSystem.Instance.quickSlotsList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        return null;
    }

    static GameObject EmptyInventorySlot()
    {
        foreach (var slot in InventorySystem.Instance.slotList) if (slot != null && slot.transform.childCount == 0) return slot;
        return null;
    }

    // an item dragged into a slot (a campfire's slot, a storage box's slot, the inventory)
    static void MoveInto(GameObject item, GameObject slot)
    {
        if (item == null || slot == null) return;
        item.transform.SetParent(slot.transform, false);
        var rect = item.transform as RectTransform;
        if (rect != null) rect.anchoredPosition = Vector2.zero; else item.transform.localPosition = Vector3.zero;
        InventorySystem.Instance.RecalculateList();
    }

    static bool SlotHas(GameObject slot, string item)
    {
        return slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == item;
    }

    static void Invoke(object o, string method)
    {
        o.GetType().GetMethod(method, Any).Invoke(o, null);
    }

    // the phone's double tap on an inventory item (eat it, use it / place it, put a tool in the quick bar)
    void UseItem(GameObject item)
    {
        if (item == null) { Note("no item to use"); return; }
        var info = item.GetComponent<InventoryItem>();
        if (info.isConsumable)
        {
            Invoke(info, "UseLikeRightClick");
            Invoke(info, "FinishConsumption");
        }
        else if (info.isUseable)
        {
            item.SetActive(false);
            Invoke(info, "UseItem");
        }
        else if (info.isEquippable)
        {
            Invoke(info, "UseLikeRightClick");
            if (item.transform.parent != null) EquipSystem.Instance.SelectQuickSlotOf(item.transform.parent.gameObject);
        }
    }

    // a tool in the hand: double tap in the inventory (into the quick bar), then its quick slot
    IEnumerator Hold(string name)
    {
        if (IsHeld(name)) yield break;
        EmptyHands();
        var item = FindItem(name);
        if (item == null) { Note("nothing to hold: no " + name); yield break; }
        var equip = EquipSystem.Instance;
        bool inQuick = item.transform.parent != null && equip.quickSlotsList.Contains(item.transform.parent.gameObject);
        if (!inQuick)
        {
            if (equip.CheckIfFull()) ClearQuickBar("Axe", "ironaxe");
            Invoke(item.GetComponent<InventoryItem>(), "UseLikeRightClick");
        }
        if (!IsHeld(name) && item.transform.parent != null) equip.SelectQuickSlotOf(item.transform.parent.gameObject);
        yield return Wait(0.45f);
    }

    static void EmptyHands()
    {
        var equip = EquipSystem.Instance;
        if (equip == null || equip.selectedItem == null) return;
        if (equip.selectedItem.transform.parent != null) equip.SelectQuickSlotOf(equip.selectedItem.transform.parent.gameObject);
        if (equip.selectedItem != null)
        {
            var info = equip.selectedItem.GetComponent<InventoryItem>();
            if (info != null) info.isSelected = false;
            equip.selectedItem = null;
            equip.selectedNumber = -1;
            if (equip.selectedItemModel != null) { DestroyImmediate(equip.selectedItemModel); equip.selectedItemModel = null; }
            QuickBarUI.Changed();
        }
    }

    static void ClearInventory(params string[] keep)
    {
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot != null && slot.transform.childCount > 0 && Array.IndexOf(keep, InventorySystem.CleanName(slot.transform.GetChild(0).name)) < 0)
                DestroyImmediate(slot.transform.GetChild(0).gameObject);
        InventorySystem.Instance.RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();
    }

    static void ClearQuickBar(params string[] keep)
    {
        var equip = EquipSystem.Instance;
        foreach (var slot in equip.quickSlotsList)
        {
            if (slot == null || slot.transform.childCount == 0) continue;
            var item = slot.transform.GetChild(0).gameObject;
            if (Array.IndexOf(keep, InventorySystem.CleanName(item.name)) >= 0) continue;
            if (equip.selectedItem == item) EmptyHands();
            DestroyImmediate(item);
        }
        InventorySystem.Instance.RecalculateList();
        QuickBarUI.Changed();
    }

    static readonly string[] Keep = { "Axe", "ironaxe" };

    // crafting the player's way: the Crafting button, the card's Craft button, the Crafting button again
    bool lastCraftOk;
    IEnumerator Craft(string what, Blueprint bp, string buttonField, string countAs, int expected = 1)
    {
        var cs = CraftingSystem.Instance;
        int before = CountAll(countAs);
        if (!cs.isOpen) yield return Tap(GameInput.TouchAction.Crafting);
        bool opened = cs.isOpen;
        cs.RefreshNeededItems();
        var button = GetField<Button>(cs, buttonField);
        bool ready = button != null && button.interactable;
        string how;
        if (ready) { button.onClick.Invoke(); how = "its card's Craft button"; }
        else how = (button == null ? "no card button " + buttonField : "the card's button was greyed out") + ", TryCraft " + cs.TryCraft(bp);
        yield return Wait(0.2f);
        if (cs.isOpen) yield return Tap(GameInput.TouchAction.Crafting);
        if (cs.isOpen) cs.CloseUI();
        int after = CountAll(countAs);
        lastCraftOk = after >= before + expected;
        Check(what, ready && lastCraftOk, countAs + " " + before + " -> " + after + " (by " + how + "; window opened by the Crafting button: " + opened + ")");
    }

    // take a thing lying in the world: stand by it, the crosshair on it, the primary press
    bool lastTakeOk;
    IEnumerator Take(Transform thing, string item, float distance = 2.3f, Func<Vector3> aimPoint = null)
    {
        lastTakeOk = false;
        if (thing == null) yield break;
        if (aimPoint == null) aimPoint = () => Centre(thing);
        int before = CountAll(item);
        Vector3 p = aimPoint();
        Vector3 from = p + FlatDir(player.position - p, Vector3.forward) * distance;
        Teleport(from, YawTo(from, p), 30f);
        yield return Wait(0.5f);
        var go = thing.gameObject;
        for (float t = 0f; t < 0.8f && thing != null; t += Time.unscaledDeltaTime)
        {
            AimAt(aimPoint());
            if (SelectionManager.Instance.selectedObject == go) break;
            yield return null;
        }
        if (thing != null) AimAt(aimPoint());
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.4f);
        lastTakeOk = CountAll(item) > before;
    }

    // a place to stand 'distance' from a target, on land, with nothing in between (isTarget: what may be hit)
    Vector3 StandBy(Vector3 target, float distance, Func<Collider, bool> isTarget, float aimHeight = 1.2f)
    {
        Vector3 first = FlatDir(player.position - target, Vector3.forward);
        Vector3 fallback = target + first * distance;
        for (int i = 0; i < 12; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 30f, 0f) * first;
            Vector3 p = target + dir * distance;
            if (!IsLand(p)) continue;
            Vector3 eye = new Vector3(p.x, Forest.GroundY(p) + 2.4f, p.z);
            Vector3 aim = new Vector3(target.x, Forest.GroundY(target) + aimHeight, target.z);
            if (Physics.Raycast(eye, (aim - eye).normalized, out RaycastHit hit, Vector3.Distance(eye, aim) + 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.GetComponentInParent<PlayerMovement>() != null || isTarget(hit.collider)) return p;
                continue;
            }
            return p;
        }
        return fallback;
    }

    static bool UnderHolding(Transform t)
    {
        var placing = PlacementSystem.Instance;
        return placing != null && placing.placementHoldingSpot != null && t.IsChildOf(placing.placementHoldingSpot.transform);
    }

    // placing a campfire / a box: turn until the outline is green (not on a tree or a thing lying around)
    IEnumerator TurnUntilValid(Func<bool> valid)
    {
        for (int i = 0; i < 8 && !valid(); i++)
        {
            var cam = Camera.main;
            float yaw = cam != null ? cam.transform.eulerAngles.y : 0f;
            look.SetLook(yaw + 45f, 38f);
            yield return Wait(0.45f);
        }
    }

    static bool PlacementValid() => GetField<bool>(PlacementSystem.Instance, "isValidPlacement");

    // a forest tree of this height, nearest to 'near', with land around it
    static ForestTree FindTree(Vector3 near, float minHeight, float maxHeight)
    {
        var forest = Forest.Instance;
        if (forest == null) return null;
        ForestTree best = null;
        float bestD = float.MaxValue;
        foreach (var t in forest.Trees)
        {
            if (t == null || t.Falling || t.data == null || t.data.height < minHeight || t.data.height > maxHeight) continue;
            if (!IsLand(t.data.position)) continue;
            float d = Flat(t.data.position - near);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    static RabbitLife FindRabbit(Vector3 near, float radius, RabbitLife not = null)
    {
        var population = RabbitPopulation.Instance;
        if (population == null) return null;
        RabbitLife best = null;
        float bestD = radius;
        foreach (var r in population.Rabbits)
        {
            if (r == null || r == not || !r.Alive || !r.IsAdult || r.caughtIn != null || r.lure != null || RabbitPopulation.InPen(r)) continue;
            if (!r.gameObject.activeInHierarchy || !IsLand(r.transform.position)) continue;
            float d = Flat(r.transform.position - near);
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }

    // the stillest of the grown rabbits near a point (for the spear)
    RabbitLife stillRabbit;
    IEnumerator FindStillRabbit(Vector3 near, float radius)
    {
        stillRabbit = null;
        var population = RabbitPopulation.Instance;
        if (population == null) yield break;
        var list = new List<RabbitLife>();
        foreach (var r in population.Rabbits)
        {
            if (r == null || !r.Alive || !r.IsAdult || r.caughtIn != null || r.lure != null || RabbitPopulation.InPen(r)) continue;
            if (!r.gameObject.activeInHierarchy || Flat(r.transform.position - near) > radius || !IsLand(r.transform.position)) continue;
            list.Add(r);
        }
        list.Sort((a, b) => Flat(a.transform.position - near).CompareTo(Flat(b.transform.position - near)));
        if (list.Count > 12) list.RemoveRange(12, list.Count - 12);
        var at = new List<Vector3>();
        foreach (var r in list) at.Add(r.transform.position);
        yield return Wait(0.4f);
        float best = float.MaxValue;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null || !list[i].Alive) continue;
            float moved = Flat(list[i].transform.position - at[i]);
            if (moved < best) { best = moved; stillRabbit = list[i]; }
        }
    }

    // ---------------- setup ---------------- //

    IEnumerator Setup()
    {
        float t = 0f;
        while ((StoryManager.Instance == null || FindObjectOfType<PlayerMovement>() == null || InventorySystem.Instance == null
            || CraftingSystem.Instance == null || EquipSystem.Instance == null) && t < 30f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        story = StoryManager.Instance;
        var movement = FindObjectOfType<PlayerMovement>();
        if (story == null || movement == null) { Fail("setup: the game scene", "no StoryManager or player after 30 s"); yield break; }
        player = movement.transform;
        look = player.GetComponent<MouseMovement>();
        Note("scene " + SceneManager.GetActiveScene().name + ", screen " + Screen.width + "x" + Screen.height + ", phone controls " + GameInput.TouchControls
            + ", TestDriven " + GameInput.TestDriven + ", difficulty " + GameSettings.Difficulty + ", language " + Lang.Current);

        // the shipwreck story (told once per device) and the first banner: skipped like a player does
        for (int i = 0; i < 40; i++)
        {
            var skip = GameObject.Find("Skip");
            if (skip != null && skip.GetComponent<Button>() != null) skip.GetComponent<Button>().onClick.Invoke();
            var d = Data;
            if (i >= 4 && d != null && d.introSeen && !GetField<bool>(story, "modal") && Time.timeScale > 0f && GameObject.Find("Skip") == null) break;
            yield return Wait(0.5f);
        }
        yield return Wait(0.8f); // (a new game empties the scene's test items from the hands)

        // the story is held still while the systems are tested (its goals would finish levels and tell their stories)
        SetField(story, "modal", true);
        frozen = true;
        if (look != null) look.enabled = false; // (the mouse in the editor does not turn the view)
        var start = FindObjectOfType<StoryStart>();
        S = start != null ? start.transform.position : player.position;
        F = start != null ? FlatDir(start.transform.forward, Vector3.forward) : Vector3.forward;
        R = Vector3.Cross(Vector3.up, F);
        var data = Data;
        Check("setup: game scene ready", look != null && data != null,
            "player at " + player.position.ToString("F0") + ", story level " + (data != null ? data.level : -1) + " (held still for the test), hour " + DayNightSystem.Instance.Hour.ToString("0.0"));
    }

    // ---------------- 13 admin safety ---------------- //

    IEnumerator AdminCheck()
    {
        var clock = DayNightSystem.Instance;
        bool unlocked = DebugEnvironmentPanel.AdminUnlocked;
        Check("13.1 admin locked at start, Immortal off", !unlocked && !GameSettings.Immortal, "admin unlocked " + unlocked + ", Immortal " + GameSettings.Immortal);
        GameSettings.Immortal = true;
        Check("13.2 Immortal can't be turned on without the password", !GameSettings.Immortal, "after setting it: Immortal " + GameSettings.Immortal);
        Check("13.3 time speed 1, the clock running", clock != null && Mathf.Approximately(clock.timeSpeed, 1f) && clock.autoTime,
            clock != null ? "timeSpeed " + clock.timeSpeed + ", autoTime " + clock.autoTime : "no clock");
        var weather = WeatherSystem.Instance;
        Check("13.4 the weather changes by itself", weather == null || weather.autoWeather, weather != null ? "autoWeather " + weather.autoWeather : "no weather system");
        // for the rest of the run the player can't die (nights with the shadow, the clock running fast)
        SetAdmin(true);
        GameSettings.Immortal = true;
        runImmortal = GameSettings.Immortal;
        Note("the test unlocked the admin for this run: Immortal " + runImmortal + " (locked again at the end)");
        yield break;
    }

    // ---------------- 11 windows ---------------- //

    IEnumerator WindowsCheck()
    {
        var inventory = InventorySystem.Instance;
        yield return Tap(GameInput.TouchAction.Inventory);
        Check("11.1 inventory opens (Inventory button / I)", inventory.isOpen, "isOpen " + inventory.isOpen);
        yield return Wait(0.3f);
        yield return Shot("11_inventory");
        yield return Tap(GameInput.TouchAction.Inventory);
        Check("11.2 inventory closes", !inventory.isOpen, "isOpen " + inventory.isOpen);

        var crafting = CraftingSystem.Instance;
        yield return Tap(GameInput.TouchAction.Crafting);
        Check("11.3 crafting opens (Crafting button / C)", crafting.isOpen, "isOpen " + crafting.isOpen);
        var tools = crafting.craftingScreenUI != null ? crafting.craftingScreenUI.transform.Find("ToolsButton") : null;
        if (tools != null && tools.GetComponent<Button>() != null) tools.GetComponent<Button>().onClick.Invoke();
        yield return Wait(0.4f);
        Check("11.4 crafting: the Tools page opens", crafting.toolsScreenUI != null && crafting.toolsScreenUI.activeInHierarchy, "tools page shown " + (crafting.toolsScreenUI != null && crafting.toolsScreenUI.activeInHierarchy));
        yield return Shot("11_crafting_tools");
        yield return Tap(GameInput.TouchAction.Crafting);
        Check("11.5 crafting closes", !crafting.isOpen, "isOpen " + crafting.isOpen);

        // the Map and Help buttons (and N / H) call these
        MapScreen.Toggle();
        yield return Wait(1f);
        Check("11.6 map opens (Map button / N)", MapScreen.IsOpen, "IsOpen " + MapScreen.IsOpen);
        yield return Shot("11_map");
        MapScreen.Toggle();
        yield return Wait(0.4f);
        Check("11.7 map closes", !MapScreen.IsOpen, "IsOpen " + MapScreen.IsOpen);

        HelpScreen.Toggle();
        yield return Wait(0.6f);
        Check("11.8 help opens (Help button / H), the game waits", HelpScreen.IsOpen && Time.timeScale == 0f, "IsOpen " + HelpScreen.IsOpen + ", timeScale " + Time.timeScale);
        yield return Shot("11_help");
        if (HelpScreen.Instance != null)
        {
            var tab = typeof(HelpScreen).GetMethod("ShowTab", Any);
            if (tab != null) { tab.Invoke(HelpScreen.Instance, new object[] { 2 }); yield return Wait(0.3f); yield return Shot("11_help_tips"); }
        }
        HelpScreen.Toggle();
        yield return Wait(0.4f);
        Check("11.9 help closes, the game goes on", !HelpScreen.IsOpen && Time.timeScale > 0f, "IsOpen " + HelpScreen.IsOpen + ", timeScale " + Time.timeScale);

        var menu = MenuManager.Instance;
        yield return Tap(GameInput.TouchAction.Menu);
        Check("11.10 menu opens (Menu button / M)", menu != null && menu.isMenuOpen, "isMenuOpen " + (menu != null && menu.isMenuOpen));
        yield return Wait(0.3f);
        yield return Shot("11_menu");
        yield return Tap(GameInput.TouchAction.Menu);
        Check("11.11 menu closes", menu != null && !menu.isMenuOpen, "isMenuOpen " + (menu != null && menu.isMenuOpen));
    }

    // ---------------- 1 campfire ---------------- //

    Campfire testFire;

    IEnumerator CampfireCheck()
    {
        ClearInventory(Keep);
        EmptyHands();
        Vector3 spot = S + F * 30f;
        Teleport(spot, YawTo(spot, spot + F), 38f);
        yield return Wait(0.6f);
        Give("stone", 6);
        yield return Craft("1.1 campfire crafted (Crafting > Survival: 6 stones)", CraftingSystem.Instance.CampfireBLP, "craftCampfireBTN", "Campfire");
        if (FindItem("Campfire") == null) { Give("Campfire", 1); Note("(a campfire given, to go on)"); }

        var before = new HashSet<Campfire>(FindObjectsOfType<Campfire>());
        UseItem(FindItem("Campfire"));
        yield return Wait(0.8f);
        bool placing = PlacementSystem.Instance.inPlacementMode && PlacementSystem.Instance.IsHoldingItem;
        yield return TurnUntilValid(PlacementValid);
        bool valid = PlacementValid();
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(1.3f);
        Campfire fire = null;
        foreach (var c in FindObjectsOfType<Campfire>()) if (!before.Contains(c) && !UnderHolding(c.transform)) fire = c;
        Check("1.2 campfire placed with the primary press", fire != null, "placing shown " + placing + ", outline green " + valid + ", placed " + (fire != null));
        if (fire == null) yield break;
        testFire = fire;

        // its window: the crosshair on it and the primary press
        Vector3 centre = fire.FireCentre;
        Vector3 from = centre + FlatDir(player.position - centre, -F) * 3f;
        Teleport(from, YawTo(from, centre), 30f);
        yield return Wait(0.5f);
        AimAt(centre + Vector3.up * 0.2f);
        yield return Wait(0.3f);
        var ui = CampfireUIManager.Instance;
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.3f);
        Check("1.3 campfire window opens (crosshair on it + press)", ui.isUiOpen, "isUiOpen " + ui.isUiOpen + ", in range " + fire.playerInRange);
        if (!ui.isUiOpen) { fire.OpenUI(); yield return Wait(0.3f); }

        // wood into the fuel slot and "Light Fire" (1 hay lights it). A log: 3 game hours, 45 s (a stick burns
        // out in 7 s, before the meat is cooked)
        Give("log", 1);
        Give("hay", 1);
        Give("Raw Meat", 1);
        yield return Wait(0.2f);
        MoveInto(FindItem("log"), ui.fuelSlot);
        yield return Wait(0.3f);
        var light = GetField<Button>(ui, "lightFireButton");
        bool canLight = light != null && light.interactable;
        int hay = Count("hay");
        if (canLight) light.onClick.Invoke(); else ui.LightFireButtonPressed();
        yield return Wait(0.4f);
        Check("1.4 fire lit: wood in the fuel slot + Light Fire (uses 1 hay)", canLight && fire.IsBurning && Count("hay") < hay,
            "button ready " + canLight + ", burning " + fire.IsBurning + ", " + fire.fuelMinutes.ToString("0") + " min of wood, hay " + hay + " -> " + Count("hay"));

        // raw meat into the food slot and "Cook"
        MoveInto(FindItem("Raw Meat"), ui.foodSlot);
        yield return Wait(0.3f);
        bool canCook = ui.cookButton != null && ui.cookButton.interactable;
        if (canCook) ui.cookButton.onClick.Invoke(); else ui.CookButtonPressed();
        yield return Wait(0.4f);
        Check("1.5 cooking starts (Cook)", canCook && fire.isCooking, "button ready " + canCook + ", cooking " + fire.isCooking);
        yield return Shot("01_campfire_cooking");
        float t = 0f;
        while (t < 16f && !SlotHas(ui.foodSlot, "Cooked Meat")) { t += 0.25f; yield return Wait(0.25f); }
        bool cooked = SlotHas(ui.foodSlot, "Cooked Meat");
        // its X: what lies in its slots goes back into the inventory
        if (ui.exitButton != null) ui.exitButton.onClick.Invoke();
        yield return Wait(0.4f);
        if (ui.isUiOpen) ui.CloseUI();
        yield return Wait(0.2f);
        Check("1.6 meat cooked, in the inventory after the window closed", cooked && Count("Cooked Meat") > 0,
            "cooked meat in the food slot after " + t.ToString("0.0") + " s: " + cooked + ", in the inventory " + Count("Cooked Meat") + ", window closed " + !ui.isUiOpen);
    }

    // ---------------- 2 axes and planks ---------------- //

    bool chopFell;
    int chopSwings, chopBlows;
    string chopInfo = "";

    // swings at a tree until it falls (the crosshair on the trunk, the primary press, one swing a second)
    IEnumerator Chop(ForestTree tree, int maxSwings)
    {
        chopFell = false; chopSwings = 0; chopBlows = 0; chopInfo = "";
        if (tree == null) yield break;
        var choppable = tree.Choppable;
        Vector3 basePos = tree.data.position;
        float h = tree.data.height;
        Vector3 trunk = basePos + Vector3.up * 1.3f;
        Vector3 stand = StandBy(basePos, tree.TrunkRadius + 1.9f, c => c.GetComponent<ChoppableTree>() == choppable, 1.3f);
        Teleport(stand, YawTo(stand, basePos), 10f);
        yield return Wait(0.6f);
        chopInfo = "height " + h.ToString("0.0") + " m, health " + choppable.treeHealth + "/" + choppable.treeMaxHealth;
        for (int i = 0; i < maxSwings; i++)
        {
            if (tree == null || tree.Falling) break;
            AimAt(trunk);
            yield return Wait(0.15f);
            bool selected = SelectionManager.Instance.selectedTree == tree.gameObject;
            if (i == 0) chopInfo += ", crosshair on it " + selected + ", in reach " + choppable.playerInRange;
            float health = choppable.treeHealth;
            yield return Tap(GameInput.TouchAction.Primary);
            for (float t = 0f; t < 1.05f; t += Time.unscaledDeltaTime) { if (tree != null && !tree.Falling) AimAt(trunk); yield return null; }
            chopSwings++;
            if (tree == null || tree.Falling || choppable.treeHealth < health) chopBlows++;
        }
        chopFell = tree != null && tree.Falling;
    }

    static List<InteractableObject> LogsNear(Vector3 at, float radius)
    {
        var list = new List<InteractableObject>();
        foreach (var o in FindObjectsOfType<InteractableObject>())
            if ((o.ItemName == "log" || o.ItemName == "pinelog") && Flat(o.transform.position - at) < radius) list.Add(o);
        return list;
    }

    Vector3 axeArea;

    IEnumerator AxeCheck()
    {
        ClearInventory(Keep);
        EmptyHands();
        Give("stone", 1);
        Give("stick", 2);
        yield return Craft("2.1 stone axe crafted (Crafting > Tools: 1 stone + 2 sticks)", CraftingSystem.Instance.AxeBLP, "craftAxeBTN", "Axe");
        if (CountAll("Axe") == 0) { Give("Axe", 1); Note("(a stone axe given, to go on)"); }
        yield return Hold("Axe");
        var equip = EquipSystem.Instance;
        Check("2.2 stone axe in the hand (double tap, then its quick slot)", IsHeld("Axe") && equip.selectedItemModel != null,
            "held '" + HeldName() + "', model " + (equip.selectedItemModel != null ? equip.selectedItemModel.name : "none"));

        var tree = FindTree(S + F * 30f, 5f, 11f);
        if (tree == null) { Fail("2.3 stone axe fells a tree", "no tree 5-11 m high found"); }
        else
        {
            Vector3 basePos = tree.data.position;
            float h = tree.data.height;
            axeArea = basePos;
            var logsBefore = new HashSet<InteractableObject>(LogsNear(basePos, h + 6f));
            yield return Chop(tree, 6);
            Check("2.3 stone axe fells a tree (crosshair on the trunk + press)", chopFell, chopInfo + ", swings " + chopSwings + ", blows that landed " + chopBlows);
            if (chopFell)
            {
                yield return Shot("02_tree_falling");
                InteractableObject log = null;
                for (float t = 0f; t < 8f && log == null; t += 0.25f)
                {
                    foreach (var o in LogsNear(basePos, h + 6f)) if (!logsBefore.Contains(o)) { log = o; break; }
                    if (log == null) yield return Wait(0.25f);
                }
                int newLogs = 0;
                foreach (var o in LogsNear(basePos, h + 6f)) if (!logsBefore.Contains(o)) newLogs++;
                Check("2.4 the fallen tree leaves logs", log != null, newLogs + " new logs");
                if (log != null)
                {
                    string item = log.ItemName;
                    yield return Take(log.transform, item);
                    Check("2.5 a log taken (crosshair on it + press)", lastTakeOk, item + " in the inventory: " + CountAll(item));
                }
            }
        }

        // the iron axe: any tree in 3 blows
        Give("ironaxe", 1);
        yield return Hold("ironaxe");
        var big = FindTree(axeArea != Vector3.zero ? axeArea : S + F * 30f, 16f, 100f);
        if (big == null) Fail("2.6 iron axe fells a big tree in 3 blows", "no tree over 16 m found");
        else
        {
            int stoneHits = TreeSpecies.Hits(big.data.height);
            yield return Chop(big, 5);
            Check("2.6 iron axe fells a big tree in 3 blows", chopFell && chopBlows <= 3,
                chopInfo + " (" + stoneHits + " blows with the stone axe), held '" + HeldName() + "', blows " + chopBlows + ", fell " + chopFell);
        }
        EmptyHands();

        // planks from a log
        if (Count("log") == 0) { Give("log", 1); Note("(a log given for the planks)"); }
        yield return Craft("2.7 planks from a log (Crafting > Refine: 1 log = 2 planks)", CraftingSystem.Instance.PlankBLP, "craftPlankBTN", "plank", 2);
    }

    // ---------------- 3 torch ---------------- //

    IEnumerator TorchCheck()
    {
        ClearQuickBar(Keep);
        ClearInventory(Keep);
        EmptyHands();
        Give("stick", 1);
        Give("hay", 2);
        yield return Craft("3.1 torch crafted (Crafting > Tools: 1 stick + 2 hay)", CraftingSystem.Instance.TorchBLP, "craftTorchBTN", "torch");
        if (CountAll("torch") == 0) { Give("torch", 1); Note("(a torch given, to go on)"); }

        // far from every burning fire, at night, empty hands: the shadow comes
        Vector3 spot = Vector3.zero;
        var tries = new[] { S + F * 120f, S + F * 100f + R * 40f, S + F * 100f - R * 40f, S + F * 150f, S + F * 70f + R * 60f };
        foreach (var p in tries)
        {
            if (!IsLand(p)) continue;
            bool fireNear = false;
            foreach (var c in FindObjectsOfType<Campfire>()) if (c.IsBurning && Flat(c.FireCentre - p) < 40f) fireNear = true;
            if (!fireNear) { spot = p; break; }
        }
        if (spot == Vector3.zero) spot = tries[0];
        Teleport(spot, YawTo(spot, spot + F), 0f);
        DayNightSystem.Instance.TimeOfDay = 23f / 24f;
        var shadow = NightShadow.Instance;
        float t = 0f;
        while (t < 12f && !(shadow != null && shadow.IsHunting)) { t += 0.25f; yield return Wait(0.25f); }
        bool came = shadow != null && shadow.IsHunting;
        Check("3.2 at night, without a fire or a torch, the shadow comes", came, "hunting after " + t.ToString("0.0") + " s (dark " + IsDark() + ", Immortal " + GameSettings.Immortal + ")");
        if (came) yield return Shot("03_shadow_comes");

        yield return Hold("torch");
        yield return Wait(0.3f);
        Check("3.3 torch in the hand burns", TorchLight.Burning, "held '" + HeldName() + "', TorchLight.Burning " + TorchLight.Burning);
        t = 0f;
        while (t < 5f && shadow != null && shadow.IsHunting) { t += 0.25f; yield return Wait(0.25f); }
        yield return Wait(3f); // (and it stays away)
        Check("3.4 with the torch burning, the shadow does not hunt", shadow == null || !shadow.IsHunting,
            "hunting " + (shadow != null && shadow.IsHunting) + " (" + (came ? "it went away after " + t.ToString("0.0") + " s" : "it had not come") + ")");
        yield return Shot("03_torch_night");
        EmptyHands();
        DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        var torch = FindItem("torch");
        if (torch != null) DestroyImmediate(torch); // (a free quick slot for the next tools)
        InventorySystem.Instance.RecalculateList();
        QuickBarUI.Changed();
        yield return Wait(0.5f);
    }

    // ---------------- 4 snare ---------------- //

    IEnumerator SnareCheck()
    {
        ClearQuickBar(Keep);
        ClearInventory(Keep);
        EmptyHands();
        Give("stick", 1);
        Give("hemp", 5);
        yield return Craft("4.1 snare crafted (Crafting > Survival: 1 stick + 5 hemp)", CraftingSystem.Instance.SnareBLP, "craftSnareBTN", "snare");
        if (CountAll("snare") == 0) { Give("snare", 1); Note("(a snare given, to go on)"); }

        var near = FindRabbit(S + F * 40f, 600f);
        if (near == null) { Fail("4.2 snare set near a rabbit", "no grown rabbit found on the island"); yield break; }
        Vector3 rp = near.transform.position;
        Vector3 by = rp + R * 5f;
        if (!IsLand(by)) by = rp - R * 5f;
        Teleport(by, YawTo(by, rp), 30f);
        yield return Wait(0.6f);
        var before = new HashSet<RabbitSnare>(RabbitSnare.All);
        UseItem(FindItem("snare"));
        yield return Wait(0.4f);
        Vector3 aim = Vector3.Lerp(by, rp, 0.6f);
        aim.y = Forest.GroundY(aim);
        AimAt(aim);
        yield return Wait(0.4f);
        bool active = SnarePlacing.Active;
        bool valid = GetField<bool>(SnarePlacing.Instance, "valid");
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.5f);
        RabbitSnare snare = null;
        foreach (var s in RabbitSnare.All) if (s != null && !before.Contains(s)) snare = s;
        Check("4.2 snare set where the crosshair is (primary press)", snare != null, "placing shown " + active + ", outline green " + valid + ", snares " + before.Count + " -> " + RabbitSnare.All.Count);
        if (snare == null) yield break;

        // the clock 20x faster until a grown rabbit walks into it
        var clock = DayNightSystem.Instance;
        clock.timeSpeed = 20f;
        float t = 0f;
        while (t < 45f && (snare == null || snare.caught == null || !snare.caught.Alive)) { t += 0.5f; yield return Wait(0.5f); }
        clock.timeSpeed = 1f;
        var caught = snare != null ? snare.caught : null;
        Check("4.3 a rabbit walks into the snare (clock x20)", caught != null && caught.Alive, "after " + t.ToString("0") + " s" + (caught != null ? ", a " + caught.sex : ""));
        if (caught == null) { clock.TimeOfDay = 10f / 24f; yield break; }

        // empty hands, the crosshair on it, the primary press: taken alive
        EmptyHands();
        int live = Count(RabbitPopulation.LiveMale) + Count(RabbitPopulation.LiveFemale);
        Vector3 cp = caught.transform.position;
        Vector3 from = cp + FlatDir(player.position - cp, R) * 2.4f;
        Teleport(from, YawTo(from, cp), 35f);
        if (IsDark()) clock.TimeOfDay = 10f / 24f; // (daylight for the picture)
        yield return Wait(0.5f);
        AimAt(RabbitCentre(caught));
        yield return Wait(0.3f);
        yield return Shot("04_snare_caught");
        AimAt(RabbitCentre(caught));
        bool inReach = caught.animal != null && caught.animal.playerInRange;
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.5f);
        int liveAfter = Count(RabbitPopulation.LiveMale) + Count(RabbitPopulation.LiveFemale);
        Check("4.4 caught rabbit taken alive with empty hands (crosshair on it + press)", liveAfter > live, "in reach " + inReach + ", live rabbits in the inventory " + live + " -> " + liveAfter);
        clock.TimeOfDay = 10f / 24f;
    }

    // ---------------- 5 storage box ---------------- //

    static List<GameObject> BoxSlots()
    {
        var list = new List<GameObject>();
        var ui = StorageManager.Instance != null ? StorageManager.Instance.StorageUI : null;
        if (ui == null) return list;
        foreach (Transform child in ui.transform) if (child.GetComponent<ItemSlot>() != null) list.Add(child.gameObject);
        return list;
    }

    IEnumerator OpenBox(StorageBox box)
    {
        Vector3 c = Centre(box.transform);
        Vector3 from = c + FlatDir(player.position - c, -F) * 3f;
        Teleport(from, YawTo(from, c), 30f);
        yield return Wait(0.5f);
        AimAt(c);
        yield return Wait(0.3f);
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.3f);
    }

    IEnumerator StorageCheck()
    {
        ClearInventory(Keep);
        EmptyHands();
        Give("plank", 2);
        yield return Craft("5.1 storage box crafted (Crafting > Survival: 2 planks)", CraftingSystem.Instance.StorageBoxBLP, "craftStorageBoxBTN", "StorageBox");
        if (FindItem("StorageBox") == null) { Give("StorageBox", 1); Note("(a storage box given, to go on)"); }

        Vector3 spot = S + F * 30f + R * 12f;
        Teleport(spot, YawTo(spot, spot + F), 38f);
        yield return Wait(0.6f);
        var before = new HashSet<StorageBox>(FindObjectsOfType<StorageBox>());
        UseItem(FindItem("StorageBox"));
        yield return Wait(0.8f);
        yield return TurnUntilValid(PlacementValid);
        bool valid = PlacementValid();
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(1.3f);
        StorageBox box = null;
        foreach (var b in FindObjectsOfType<StorageBox>()) if (!before.Contains(b) && !UnderHolding(b.transform)) box = b;
        Check("5.2 storage box placed with the primary press", box != null, "outline green " + valid + ", placed " + (box != null));
        if (box == null) yield break;

        var storage = StorageManager.Instance;
        yield return OpenBox(box);
        Check("5.3 the box opens (crosshair on it + press)", storage.storageUIOpen, "storageUIOpen " + storage.storageUIOpen);
        if (!storage.storageUIOpen) { storage.OpenBox(box); yield return Wait(0.3f); }

        Give("ironore", 1);
        GameObject free = null;
        foreach (var s in BoxSlots()) if (s.transform.childCount == 0) { free = s; break; }
        MoveInto(FindItem("ironore"), free);
        yield return Wait(0.3f);
        yield return Shot("05_storage_box");
        storage.CloseBox();
        yield return Wait(0.4f);
        Check("5.4 an item put in the box is kept when it closes", box.items.Contains("ironore") && Count("ironore") == 0,
            "box: [" + string.Join(", ", box.items) + "], iron ore in the inventory " + Count("ironore"));

        yield return OpenBox(box);
        if (!storage.storageUIOpen) { storage.OpenBox(box); yield return Wait(0.3f); }
        GameObject inBox = null;
        foreach (var s in BoxSlots()) if (SlotHas(s, "ironore")) inBox = s.transform.GetChild(0).gameObject;
        Check("5.5 opened again: the item is still there", inBox != null, "found in a slot of the box " + (inBox != null));
        MoveInto(inBox, EmptyInventorySlot());
        yield return Wait(0.3f);
        storage.CloseBox();
        yield return Wait(0.4f);
        Check("5.6 taken back: in the inventory, the box empty", Count("ironore") == 1 && box.items.Count == 0,
            "iron ore in the inventory " + Count("ironore") + ", box: [" + string.Join(", ", box.items) + "]");
    }

    // ---------------- 6 building ---------------- //

    IEnumerator BuildingCheck()
    {
        ClearInventory(Keep);
        EmptyHands();
        // a small fenced yard (2 x 2 panels a side), the gate second, put up panel after panel the player's way
        int fences0 = Buildings.Count(BuildingKind.Fence), gates0 = Buildings.Count(BuildingKind.FenceGate);
        Give(Fences.FenceItem, 7);
        Give(Fences.GateItem, 1);
        Vector3 c = S + F * 70f;
        var corners = new[] { c - R * 4f - F * 4f, c + R * 4f - F * 4f, c + R * 4f + F * 4f, c - R * 4f + F * 4f };
        var panels = new List<(Vector3 a, Vector3 b, Vector3 outward)>();
        for (int k = 0; k < 4; k++)
        {
            Vector3 a = corners[k], b = corners[(k + 1) % 4];
            Vector3 outward = FlatDir((a + b) * 0.5f - c, F);
            int n = Mathf.Max(1, Mathf.RoundToInt(Flat(b - a) / Fences.Length));
            for (int i = 0; i < n; i++) panels.Add((Vector3.Lerp(a, b, (float)i / n), Vector3.Lerp(a, b, (float)(i + 1) / n), outward));
        }
        int placed = 0;
        for (int i = 0; i < panels.Count; i++)
        {
            var pn = panels[i];
            string item = i == 1 ? Fences.GateItem : Fences.FenceItem;
            Vector3 target = i == 0 ? (pn.a + pn.b) * 0.5f : pn.b;
            Vector3 stand = target + pn.outward * 8f;
            Teleport(stand, YawTo(stand, target), 20f);
            yield return Wait(0.3f);
            if (!FencePlacing.Active || i == 1 || i == 2)
            {
                if (FencePlacing.Active) FencePlacing.Instance.Cancel();
                var it = FindItem(item);
                if (it == null) { Note("no " + item + " left for panel " + i); break; }
                UseItem(it);
            }
            AimAt(new Vector3(target.x, Forest.GroundY(target), target.z));
            yield return Wait(0.3f);
            int before = Buildings.Count(BuildingKind.Fence) + Buildings.Count(BuildingKind.FenceGate);
            yield return Tap(GameInput.TouchAction.Primary);
            yield return Wait(0.2f);
            if (Buildings.Count(BuildingKind.Fence) + Buildings.Count(BuildingKind.FenceGate) > before) placed++;
            else Note("panel " + i + " (" + item + ") not placed: '" + GetField<string>(FencePlacing.Instance, "why") + "'");
        }
        if (FencePlacing.Active) FencePlacing.Instance.Cancel();
        yield return Wait(0.6f);
        Check("6.1 a fenced yard with a gate closes (Fences.YardBuilt)", Fences.YardBuilt() && Buildings.Count(BuildingKind.FenceGate) > gates0,
            placed + " of " + panels.Count + " panels placed, fences " + fences0 + " -> " + Buildings.Count(BuildingKind.Fence) + ", gates " + gates0 + " -> " + Buildings.Count(BuildingKind.FenceGate)
            + ", free ends " + Fences.FreeEnds().Count + ", closed " + Fences.YardBuilt());
        Vector3 view = c - F * 14f - R * 9f;
        Teleport(view, YawTo(view, c), 14f);
        yield return Wait(0.8f);
        yield return Shot("06_yard");

        // a foundation where the player looks, then a wall on one of its places (the ghosts)
        var cm = ConstructionManager.Instance;
        Give("foundation", 1);
        Give("wall", 1);
        Vector3 spot = S + F * 95f;
        if (!IsLand(spot)) spot = S + F * 50f - R * 20f;
        Teleport(spot, YawTo(spot, spot + F), 30f);
        yield return Wait(0.6f);
        int foundations = Buildings.Count(BuildingKind.Foundation), walls = Buildings.Count(BuildingKind.Wall);
        var placedBefore = new HashSet<GameObject>(GameObject.FindGameObjectsWithTag("placedFoundation"));
        UseItem(FindItem("foundation"));
        yield return Wait(0.8f);
        bool building = cm.inConstructionMode;
        for (int i = 0; i < 8 && !cm.isValidPlacement; i++)
        {
            look.SetLook(YawTo(spot, spot + F) + (i + 1) * 45f, 30f);
            yield return Wait(0.45f);
        }
        bool valid = cm.isValidPlacement;
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.6f);
        Check("6.2 foundation placed where the player looks (primary press)", Buildings.Count(BuildingKind.Foundation) > foundations,
            "building mode " + building + ", outline green " + valid + ", foundations " + foundations + " -> " + Buildings.Count(BuildingKind.Foundation));
        if (cm.inConstructionMode) yield return Tap(GameInput.TouchAction.Cancel);
        GameObject foundation = null;
        foreach (var go in GameObject.FindGameObjectsWithTag("placedFoundation")) if (!placedBefore.Contains(go)) foundation = go;
        if (foundation == null) yield break;

        UseItem(FindItem("wall"));
        yield return Wait(0.6f);
        GameObject ghost = null;
        float best = 9f;
        foreach (var g in GameObject.FindGameObjectsWithTag("wallGhost"))
        {
            float d = Flat(g.transform.position - foundation.transform.position);
            if (d < best) { best = d; ghost = g; }
        }
        bool selecting = false;
        if (ghost != null)
        {
            var col = ghost.GetComponent<Collider>();
            Vector3 g = col != null ? col.bounds.center : ghost.transform.position;
            Vector3 stand = g + FlatDir(g - foundation.transform.position, F) * 4.5f;
            Teleport(stand, YawTo(stand, g), 0f);
            yield return Wait(0.4f);
            for (float t = 0f; t < 0.6f && !cm.selectingAGhost; t += Time.unscaledDeltaTime) { AimAt(g); yield return null; }
            selecting = cm.selectingAGhost;
            yield return Tap(GameInput.TouchAction.Primary);
            yield return Wait(0.6f);
        }
        Check("6.3 a wall placed on the foundation (crosshair on its place + press)", Buildings.Count(BuildingKind.Wall) > walls,
            "wall places found " + (ghost != null) + ", crosshair on one " + selecting + ", walls " + walls + " -> " + Buildings.Count(BuildingKind.Wall));
        if (cm.inConstructionMode) yield return Tap(GameInput.TouchAction.Cancel);
        Vector3 v = foundation.transform.position - F * 9f + R * 6f;
        Teleport(v, YawTo(v, foundation.transform.position), 15f);
        yield return Wait(0.6f);
        yield return Shot("06_foundation_wall");
    }

    // ---------------- 7 seeds ---------------- //

    IEnumerator SeedsCheck()
    {
        var forest = Forest.Instance;
        if (forest == null) { Fail("7 seeds", "no Forest"); yield break; }
        ClearInventory(Keep);
        EmptyHands();
        Give("acorn", 1);
        // at the edge of a forest: a grown tree and open ground beside it
        var tree = FindTree(axeArea != Vector3.zero ? axeArea : S + F * 30f, 12f, 100f);
        Vector3 basePos = tree != null ? tree.data.position : S + F * 40f;
        Vector3 away = FlatDir(S - basePos, -F);
        Vector3 stand = basePos + away * 9f;
        if (!IsLand(stand)) stand = basePos + Vector3.Cross(Vector3.up, away) * 9f;
        Teleport(stand, YawTo(stand, stand + away), 30f);
        yield return Wait(0.6f);

        int plantedEvents = 0;
        Action onPlanted = () => plantedEvents++;
        SeedPlanting.Planted += onPlanted;
        UseItem(FindItem("acorn"));
        yield return Wait(0.3f);
        bool valid = false;
        Vector3 spot = Vector3.zero;
        for (int i = 0; i < 12 && SeedPlanting.Active && !valid; i++)
        {
            Vector3 aim = stand + Quaternion.Euler(0f, YawTo(stand, stand + away) + i * 30f, 0f) * Vector3.forward * (4f + i % 3);
            aim.y = Forest.GroundY(aim);
            AimAt(aim);
            yield return Wait(0.25f);
            valid = GetField<bool>(SeedPlanting.Instance, "valid");
            spot = GetField<Vector3>(SeedPlanting.Instance, "spot");
        }
        bool active = SeedPlanting.Active;
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.3f);
        SeedPlanting.Planted -= onPlanted;
        if (SeedPlanting.Active) SeedPlanting.Instance.Cancel();
        Check("7.1 acorn planted where the crosshair is (SeedPlanting)", plantedEvents > 0 && Count("acorn") == 0,
            "planting shown " + active + ", outline green " + valid + ", planted " + plantedEvents + ", acorns left " + Count("acorn"));

        // 100 game hours for the trees at once (Forest.FastForward)
        int trees0 = forest.TreeCount, dropped0 = forest.SeedsDropped, sprouted0 = forest.SeedsSprouted, far0 = forest.SaplingsFarAway;
        forest.CountNear(stand, 80f, out int young0, out int seeds0, out int all0);
        float t0 = Time.realtimeSinceStartup;
        forest.FastForward(100f);
        float took = Time.realtimeSinceStartup - t0;
        yield return Wait(1f);
        forest.CountNear(stand, 80f, out int young1, out int seeds1, out int all1);
        forest.CountNear(spot, 2f, out int youngAtSpot, out _, out int atSpot);
        Note("FastForward(100 h) took " + took.ToString("0.0") + " s; within 80 m: young trees " + young0 + " -> " + young1 + ", seeds on the ground " + seeds0 + " -> " + seeds1 + ", trees " + all0 + " -> " + all1);
        Check("7.2 seeds fall from the trees near the player", forest.SeedsDropped > dropped0, "SeedsDropped " + dropped0 + " -> " + forest.SeedsDropped);
        Check("7.3 fallen seeds sprout into young trees", forest.SeedsSprouted > sprouted0, "SeedsSprouted " + sprouted0 + " -> " + forest.SeedsSprouted + " (far away by themselves " + far0 + " -> " + forest.SaplingsFarAway + ")");
        Check("7.4 the number of trees grows", forest.TreeCount > trees0, "TreeCount " + trees0 + " -> " + forest.TreeCount);
        Check("7.5 the planted acorn became a young tree", plantedEvents > 0 && atSpot > 0, "trees at the planted spot " + atSpot + " (young " + youngAtSpot + ")");
        Vector3 view = spot != Vector3.zero ? spot : stand;
        Vector3 from = view - away * 7f + Vector3.Cross(Vector3.up, away) * 3f;
        Teleport(from, YawTo(from, view), 20f);
        yield return Wait(0.8f);
        yield return Shot("07_seeds_after_100h");
    }

    // ---------------- 8 water and food ---------------- //

    IEnumerator WaterFoodCheck()
    {
        ClearInventory(Keep);
        EmptyHands();
        var state = PlayerState.Instance;
        // (the admin's Immortal keeps the bars full: off for this check, in the day)
        GameSettings.Immortal = false;
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;

        var river = River.Instance;
        if (river == null || river.points.Count < 4) Fail("8.1 drinking at the river", "no river");
        else
        {
            int k = river.points.Count * 2 / 3;
            Vector3 p = river.points[k];
            Vector3 along = FlatDir(river.points[k + 1] - p, F);
            Vector3 side = Vector3.Cross(Vector3.up, along);
            float hw = river.halfWidths.Count > k ? river.halfWidths[k] : 3f;
            Vector3 bank = p + side * (hw + 1.2f);
            Teleport(bank, YawTo(bank, p), 45f);
            yield return Wait(1f);
            state.setHydration(state.maxHydrationPercent * 0.4f);
            float w0 = state.currentHydrationPercent;
            AimAt(p);
            yield return Wait(0.3f);
            var source = FreshWater.Instance != null ? FreshWater.Instance.At(state.playerBody.transform.position) : FreshWater.Source.None;
            yield return Tap(GameInput.TouchAction.Primary);
            yield return Wait(0.3f);
            float w1 = state.currentHydrationPercent;
            Check("8.1 drinking at the river (look down at the water + press) fills the water bar", w1 > w0 + 1f,
                "water " + w0.ToString("0") + " -> " + w1.ToString("0") + " of " + state.maxHydrationPercent.ToString("0") + ", fresh water here: " + source);
            yield return Shot("08_river");
        }

        state.setCalories(state.maxCalories * 0.4f);
        float c0 = state.currentCalories;
        Give("berries", 1);
        UseItem(FindItem("berries"));
        yield return Wait(0.3f);
        float c1 = state.currentCalories;
        Check("8.2 eating berries (double tap) fills the food bar", c1 > c0 && Count("berries") == 0, "food " + c0.ToString("0") + " -> " + c1.ToString("0") + ", berries left " + Count("berries"));
        Give("porcini", 1);
        UseItem(FindItem("porcini"));
        yield return Wait(0.3f);
        float c2 = state.currentCalories;
        Check("8.3 eating a porcini mushroom fills the food bar", c2 > c1, "food " + c1.ToString("0") + " -> " + c2.ToString("0"));
        GameSettings.Immortal = runImmortal;
    }

    // ---------------- 9 hunting and looting ---------------- //

    IEnumerator HuntingCheck()
    {
        ClearInventory(Keep);
        if (CountAll("Axe") == 0) { Give("Axe", 1); Note("(a stone axe given)"); }
        yield return Hold("Axe");
        var rabbit = FindRabbit(player.position, 300f);
        if (rabbit == null) rabbit = FindRabbit(S, 800f);
        if (rabbit == null) { Fail("9.1 a rabbit killed with the stone axe", "no grown rabbit found"); yield break; }
        var animal = rabbit.animal;
        int swings = 0;
        float t0 = Time.realtimeSinceStartup;
        while (!animal.isDead && swings < 8 && Time.realtimeSinceStartup - t0 < 22f)
        {
            Vector3 rp = rabbit.transform.position;
            Vector3 from = rp + FlatDir(player.position - rp, R) * 2.3f;
            if (!IsLand(from)) from = rp - FlatDir(player.position - rp, R) * 2.3f;
            Teleport(from, YawTo(from, rp), 35f);
            yield return Wait(0.35f);
            AimAt(RabbitCentre(rabbit));
            yield return null;
            AimAt(RabbitCentre(rabbit));
            yield return Tap(GameInput.TouchAction.Primary);
            swings++;
            for (float t = 0f; t < 1.05f; t += Time.unscaledDeltaTime) { if (rabbit != null && !animal.isDead) AimAt(RabbitCentre(rabbit)); yield return null; }
        }
        Check("9.1 a rabbit killed with the stone axe (two blows)", animal != null && animal.isDead, "swings " + swings + ", held '" + HeldName() + "'");
        if (animal == null || !animal.isDead) yield break;
        yield return Shot("09_rabbit_killed");

        // one press on the body: the meat and the hide go into the inventory
        int meat = Count("Raw Meat"), hide = Count("Rabbit Hide");
        Vector3 body = rabbit.transform.position;
        Vector3 at = body + FlatDir(player.position - body, R) * 2.2f;
        Teleport(at, YawTo(at, body), 40f);
        yield return Wait(0.4f);
        AimAt(body + Vector3.up * 0.25f);
        yield return Wait(0.3f);
        bool hand = SelectionManager.Instance.handIsVisible;
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.4f);
        Check("9.2 looting: one press on the body gives meat and a hide", Count("Raw Meat") > meat && Count("Rabbit Hide") > hide,
            "hand icon " + hand + ", raw meat " + meat + " -> " + Count("Raw Meat") + ", hides " + hide + " -> " + Count("Rabbit Hide") + ", body gone " + (rabbit == null));
        EmptyHands();
    }

    // ---------------- 10 spear ---------------- //

    IEnumerator SpearCheck()
    {
        var cs = CraftingSystem.Instance;
        if (cs.SpearBLP == null) { Skip("10 spear", "no spear recipe (CraftingSystem.SpearBLP)"); yield break; }
        if (Resources.Load<GameObject>(Spear.Item) == null) { Skip("10 spear", "no Resources/spear item yet: run Tools > Nature > 6. Make The Spear Item (SpearAssetsTool.MakeSpearItem) before the test"); yield break; }
        ClearQuickBar(Keep);
        ClearInventory(Keep);
        EmptyHands();
        if (CountAll("Axe") + CountAll("ironaxe") == 0) Give("Axe", 1);
        Give("stick", 2);
        yield return Craft("10.1 spear crafted (Crafting > Tools: 2 sticks, the axe kept)", cs.SpearBLP, "craftSpearBTN", Spear.Item);
        Check("10.2 the axe is kept", CountAll("Axe") + CountAll("ironaxe") > 0, "axes " + (CountAll("Axe") + CountAll("ironaxe")));
        if (CountAll(Spear.Item) == 0) { Give(Spear.Item, 1); Note("(a spear given, to go on)"); }

        yield return Hold(Spear.Item);
        yield return Wait(0.4f);
        look.SetLook(player.eulerAngles.y, -20f); // (the cross shows where nothing to take is under the crosshair: look a little up)
        yield return Wait(0.4f);
        var cross = GetField<GameObject>(SelectionManager.Instance, "aimCross");
        Check("10.3 spear in the hand (double tap, its quick slot)", Spear.IsHeld && SpearHeld.Current != null, "held '" + HeldName() + "', SpearHeld " + (SpearHeld.Current != null));
        Check("10.4 the \"+\" aiming cross shows instead of the dot", cross != null && cross.activeInHierarchy, "aim cross " + (cross == null ? "not made" : "active " + cross.activeInHierarchy));
        yield return Shot("10_spear_aim");

        // a rabbit about 12 m away, the "+" on it (a little ahead of a moving one), the primary press
        bool hit = false, pressed = false;
        RabbitLife target = null;
        for (int attempt = 0; attempt < 3 && !hit; attempt++)
        {
            yield return FindStillRabbit(player.position, 250f);
            target = stillRabbit != null ? stillRabbit : FindRabbit(S, 800f);
            if (target == null) { Fail("10.5 a thrown spear kills a rabbit about 12 m away", "no grown rabbit found"); yield break; }
            Vector3 rp = target.transform.position;
            Vector3 stand = StandBy(rp, 12f, col => col.GetComponentInParent<RabbitLife>() == target, 0.45f);
            Teleport(stand, YawTo(stand, rp), 5f);
            if (!Spear.IsHeld) yield return Hold(Spear.Item);
            yield return Wait(0.45f); // (the spear comes up into the hand)
            // the rabbit's speed, to aim where it will be
            Vector3 p0 = RabbitCentre(target);
            yield return Wait(0.15f);
            if (target == null) continue;
            Vector3 velocity = (RabbitCentre(target) - p0) / 0.15f;
            velocity.y = 0f;
            float flight = Vector3.Distance(Camera.main.transform.position, RabbitCentre(target)) / Spear.ThrowSpeed;
            float release = Time.realtimeSinceStartup + 0.36f;
            AimAt(RabbitCentre(target) + velocity * (0.36f + flight));
            var lastBefore = ThrownSpear.Last;
            yield return Tap(GameInput.TouchAction.Primary);
            // keep the "+" on the point until the spear leaves the hand (it flies to where the "+" is then)
            for (float t = 0f; t < 0.8f && (ThrownSpear.Last == lastBefore || ThrownSpear.Last == null); t += Time.unscaledDeltaTime)
            {
                if (target != null) AimAt(RabbitCentre(target) + velocity * (Mathf.Max(0f, release - Time.realtimeSinceStartup) + flight));
                yield return null;
            }
            if (ThrownSpear.Last == lastBefore || ThrownSpear.Last == null)
            {
                Note("throw " + (attempt + 1) + ": the press did not throw (crosshair hand " + SelectionManager.Instance.handIsVisible + "); Spear.ThrowNow() " + Spear.ThrowNow());
                for (float t = 0f; t < 0.8f && (ThrownSpear.Last == lastBefore || ThrownSpear.Last == null); t += Time.unscaledDeltaTime) yield return null;
            }
            else pressed = true;
            var thrown = ThrownSpear.Last;
            for (float t = 0f; t < 3f && thrown != null && thrown.Flying; t += Time.unscaledDeltaTime) yield return null;
            hit = target != null && target.animal != null && target.animal.isDead;
            Note("throw " + (attempt + 1) + " from " + Flat(stand - rp).ToString("0.0") + " m (rabbit moving " + velocity.magnitude.ToString("0.0") + " m/s): "
                + (hit ? "the rabbit is dead" : "missed" + (thrown != null && thrown.HitAnimal != null ? " (hit another animal)" : "")));
            if (!hit && thrown != null)
            {
                yield return Take(thrown.transform, Spear.Item); // back into the inventory for the next throw
                if (!lastTakeOk && ThrownSpear.Last != null) ThrownSpear.Last.TakeBack();
            }
        }
        Check("10.5 a thrown spear kills a rabbit about 12 m away (primary press)", hit && pressed,
            "killed " + hit + ", thrown by the press " + pressed + ", last hit " + (ThrownSpear.LastHitAnimal != null ? ThrownSpear.LastHitAnimal.name : "none"));
        yield return Shot("10_spear_hit");

        // the spear is taken back from the body (or the ground): the crosshair on its shaft, away from the body (on the
        // body itself the press loots the rabbit, and the spear falls to the ground: then it is taken from there)
        var spear = ThrownSpear.Last;
        if (spear == null) { Fail("10.6 the spear is taken back", "no thrown spear to take"); yield break; }
        int spears = CountAll(Spear.Item);
        lastTakeOk = false;
        for (int i = 0; i < 2 && !lastTakeOk && spear != null; i++)
        {
            var s = spear.transform;
            lastTakeOk = false;
            yield return Take(s, Spear.Item, 2.3f, () => s != null ? s.position - s.forward * 0.5f : Vector3.zero);
        }
        Check("10.6 the spear is taken back (crosshair on it + press)", CountAll(Spear.Item) > spears, "spears " + spears + " -> " + CountAll(Spear.Item));
        EmptyHands();
    }

    // ---------------- 12 save and load ---------------- //

    IEnumerator SaveLoadCheck()
    {
        var save = SaveManager.Instance;
        if (save == null) { Fail("12 save and load", "no SaveManager in the scene"); yield break; }
        EmptyHands();
        ClearInventory(Keep);
        Give("ironore", 3);
        var data = Data;
        int level = data != null ? data.level : -1;
        int ore = Count("ironore");
        Vector3 at = player.position;
        save.SaveGame(TestSlot);
        yield return Wait(0.5f);
        Check("12.1 saved to slot " + TestSlot + " (SaveManager.SaveGame)", save.DoesFileExist(TestSlot), "save file " + save.DoesFileExist(TestSlot) + ", level " + level + ", iron ore " + ore);

        // something changes: the ore is used, the story moves on
        InventorySystem.Instance.RemoveItem("ironore", 3);
        if (data != null) data.level = level + 1;
        Note("changed after saving: iron ore " + Count("ironore") + ", level " + (data != null ? data.level : -1));

        // loaded the way the Load Game screen does it: the game scene again, then the save
        var oldStory = story;
        save.StartLoadedGame(TestSlot);
        float t = 0f;
        while (t < 30f && (StoryManager.Instance == null || StoryManager.Instance == oldStory || GetField<StoryData>(StoryManager.Instance, "data") == null
            || GetField<bool>(StoryManager.Instance, "loading") || SceneSystem.PendingLoadSlot > 0))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return Wait(1.5f);
        story = StoryManager.Instance;
        frozen = false; // (the loaded game's story goes on by itself)
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) { player = movement.transform; look = player.GetComponent<MouseMovement>(); }
        var loaded = Data;
        Check("12.2 the save loads (the game scene again)", loaded != null && story != oldStory, "after " + t.ToString("0.0") + " s");
        Check("12.3 the story level is back", loaded != null && loaded.level == level, "level " + (loaded != null ? loaded.level : -1) + " (saved " + level + ")");
        Check("12.4 the inventory is back", Count("ironore") >= ore, "iron ore " + Count("ironore") + " (saved " + ore + ")");
        Check("12.5 the player is back where the game was saved", player != null && Flat(player.position - at) < 3f, "distance " + (player != null ? Flat(player.position - at).ToString("0.0") : "-") + " m");
        // (the loading picture with its prayer stays a few seconds, to be read)
        for (float w = 0f; w < 12f && LoadingScreen.Instance != null; w += 0.25f) yield return Wait(0.25f);
        yield return Wait(1f);
        yield return Shot("12_after_load");

        // the test save is removed (files, picture, the slot's name)
        try
        {
            string folder = Application.dataPath + "/";
            foreach (var name in new[] { "SaveGame" + TestSlot + ".json", "SaveGame" + TestSlot + ".jpg", "SaveGame" + TestSlot + ".bin" })
            {
                if (File.Exists(folder + name)) File.Delete(folder + name);
                if (File.Exists(folder + name + ".meta")) File.Delete(folder + name + ".meta");
            }
            string persistent = Application.persistentDataPath + "/SaveGame" + TestSlot + ".bin";
            if (File.Exists(persistent)) File.Delete(persistent);
            PlayerPrefs.DeleteKey("Slot" + TestSlot + "Description");
            PlayerPrefs.Save();
            Note("test save deleted: " + !save.DoesFileExist(TestSlot));
        }
        catch (Exception e) { Note("could not delete the test save: " + e.Message); }
    }

    // ---------------- between the checks ---------------- //

    IEnumerator Tidy()
    {
        try { TidyNow(); } catch (Exception e) { Note("tidy: " + e.Message); }
        yield return Wait(0.3f);
    }

    void TidyNow()
    {
        var inventory = InventorySystem.Instance;
        if (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen) CampfireUIManager.Instance.CloseUI();
        if (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen) StorageManager.Instance.CloseBox();
        if (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen) CraftingSystem.Instance.CloseUI();
        if (inventory != null && inventory.isOpen) inventory.CloseUI();
        if (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen) MenuManager.Instance.CloseMenu();
        if (MapScreen.IsOpen) MapScreen.Toggle();
        if (HelpScreen.IsOpen) HelpScreen.Toggle();
        if (SeedPlanting.Active) SeedPlanting.Instance.Cancel();
        if (SnarePlacing.Active) SnarePlacing.Instance.Cancel();
        if (FencePlacing.Active) FencePlacing.Instance.Cancel();
        // placing a campfire or a box, building: the Cancel button
        if ((PlacementSystem.Instance != null && PlacementSystem.Instance.IsHoldingItem) || (ConstructionManager.Instance != null && ConstructionManager.Instance.inConstructionMode))
        {
            GameInput.PressTouch(GameInput.TouchAction.Cancel);
            GameInput.ReleaseTouch(GameInput.TouchAction.Cancel);
        }
        if (EquipSystem.Instance != null) EmptyHands();
        var clock = DayNightSystem.Instance;
        if (clock != null)
        {
            clock.timeSpeed = 1f;
            if (IsDark()) clock.TimeOfDay = 10f / 24f;
        }
        GameSettings.Immortal = runImmortal;
        if (story != null && frozen) SetField(story, "modal", true);
        if (Time.timeScale <= 0f && !HelpScreen.IsOpen) Time.timeScale = 1f;
        if (SelectionManager.Instance != null && !SelectionManager.Instance.enabled && inventory != null && !inventory.isOpen) SelectionManager.Instance.enabled = true;
    }
}
#endif
