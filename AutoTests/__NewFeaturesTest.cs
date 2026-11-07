#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// TEMPORARY (Claude): the checks of the new things of 2026-11-07, run by the systems test when its Folder starts with
// "NewFeatures" (the driver's jobs NewFeaturesPhone13 / NewFeaturesPC13). The same class as __SystemsTest (partial):
// the same helpers and the same way of playing (the crafting cards' buttons, the crosshair + the primary press with
// GameInput.TestDriven, the joystick through __WalkDriver), so it runs in the phone Simulator and in the PC Game view.
//   N5 dry branches under the trees (FallenBranches): shown, one taken, not shown again; a felled leafy tree's branches
//   N1 winter under full snow: grass picked (hay), a plant taken, a campfire lit with that hay
//   N6 the shy rabbits on Easy / Normal / Hard: when they run and how fast, wandering faster on Normal, calm in a
//      closed fenced yard, killed with the stone axe by chasing on Hard
//   N2 a loaded game holds only what was saved (the scene's stone axes do not come back) + N4 the loading screen
//      (no prayer, "Loading", gone soon after the load)
//   N3 the backpack (Crafting > Survival: 3 rabbit hides + 2 sticks): 32 -> 48 slots, its card, the window
//      (12 columns, on the screen, next to the crafting window), kept by a save and a load
// Writes into the same systems.txt (PASS / FAIL / SKIP lines) and PNG screenshots. Removed after the tests.
public partial class __SystemsTest
{
    partial void AddNewFeatureChecks(List<(string name, Func<IEnumerator> body, float timeout)> checks)
    {
        checks.Add(("N0 run setup", NewRunSetup, 10f));
        checks.Add(("N5 fallen branches", BranchesCheck, 60f));
        checks.Add(("N1 winter", WinterCheck, 60f));
        checks.Add(("N6 shy rabbits", ShyRabbitsCheck, 160f));
        checks.Add(("N2 load: no extra axes + N4 loading screen", LoadNoAxesCheck, 50f));
        checks.Add(("N3 backpack", BackpackCheck, 90f));
    }

    // put back whatever the new checks changed (also called at the end of the systems run: then nothing to do)
    partial void RestoreNewFeatures()
    {
        RestoreWinter();
        foreach (var r in testRabbits) if (r != null) Destroy(r.gameObject);
        testRabbits.Clear();
        DeleteTestSave();
    }

    // ---------------- N0 the run ---------------- //

    TimeManager.Season startSeason;
    float startSnow;
    bool startAutoWeather = true;

    IEnumerator NewRunSetup()
    {
        // the player can't die meanwhile (the admin unlocked for this run, locked again at the end)
        SetAdmin(true);
        GameSettings.Immortal = true;
        runImmortal = GameSettings.Immortal;
        var weather = WeatherSystem.Instance;
        var time = TimeManager.Instance;
        startSeason = time != null ? time.currentSeason : TimeManager.Season.Spring;
        startSnow = weather != null ? weather.SnowCover : 0f;
        startAutoWeather = weather == null || weather.autoWeather;
        Note("difficulty " + GameSettings.Difficulty + " (rabbits run from " + GameSettings.RabbitShyRadius + " m), season " + startSeason
            + ", snow cover " + startSnow.ToString("0.00") + ", Immortal for the run " + runImmortal + ", slots " + InventorySystem.Instance.slotList.Count);
        yield break;
    }

    // ---------------- N5 dry branches ---------------- //

    // a grown tree with others around it (a forest), nearest to 'near'
    static ForestTree FindForestTree(Vector3 near)
    {
        var forest = Forest.Instance;
        if (forest == null) return null;
        var list = new List<ForestTree>();
        foreach (var t in forest.Trees)
            if (t != null && !t.Falling && t.data != null && t.data.height >= 6f && IsLand(t.data.position) && Flat(t.data.position - near) < 600f) list.Add(t);
        list.Sort((a, b) => Flat(a.data.position - near).CompareTo(Flat(b.data.position - near)));
        var around = new List<ForestTree>();
        for (int i = 0; i < list.Count && i < 300; i++)
        {
            around.Clear();
            forest.TreesNear(list[i].data.position, 16f, around);
            int grown = 0;
            foreach (var o in around) if (o != null && o != list[i] && o.data != null && o.data.height >= 4f && Flat(o.data.position - list[i].data.position) < 16f) grown++;
            if (grown >= 4) return list[i];
        }
        return list.Count > 0 ? list[0] : null;
    }

    // a leafy tree (not a pine) of this height, nearest to 'near', with land around it
    static ForestTree FindLeafyTree(Vector3 near, float minHeight, float maxHeight)
    {
        var forest = Forest.Instance;
        if (forest == null) return null;
        ForestTree best = null;
        float bestD = float.MaxValue;
        foreach (var t in forest.Trees)
        {
            if (t == null || t.Falling || t.data == null || t.data.species == (int)TreeSpecies.Kind.Pine) continue;
            if (t.data.height < minHeight || t.data.height > maxHeight || !IsLand(t.data.position)) continue;
            float d = Flat(t.data.position - near);
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // the branches a felled tree left (Forest.SpawnBranch: "Branch", a stick), not the ones of FallenBranches (those
    // under its "Fallen branches" root, nor its unused ones parked far under the island with their pick switched off)
    static List<InteractableObject> FelledBranchesNear(Vector3 at, float radius)
    {
        var list = new List<InteractableObject>();
        foreach (var o in FindObjectsOfType<InteractableObject>())
        {
            if (o == null || !o.enabled || o.ItemName != "stick" || o.name != "Branch") continue;
            if (Flat(o.transform.position - at) > radius || Mathf.Abs(o.transform.position.y - Forest.GroundY(o.transform.position)) > 20f) continue;
            string parent = o.transform.parent != null ? o.transform.parent.name : "";
            if (parent == "Fallen branches" || parent == "Unused fallen branches") continue;
            list.Add(o);
        }
        return list;
    }

    IEnumerator BranchesCheck()
    {
        var forest = Forest.Instance;
        var branches = FallenBranches.Instance;
        Check("N5.1 FallenBranches is there (added by the forest)", branches != null && forest != null,
            "FallenBranches.Instance " + (branches != null) + (branches != null && forest != null ? ", on the forest's object " + (branches.gameObject == forest.gameObject) : ""));
        if (branches == null || forest == null) yield break;
        ClearQuickBar(Keep);
        ClearInventory();
        EmptyHands();
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;

        // in a forest: dry branches lie under the grown trees around the player
        var tree = FindForestTree(S + F * 40f);
        if (tree == null) { Fail("N5.2 dry branches under the trees", "no grown tree found"); yield break; }
        Vector3 tp = tree.data.position;
        Vector3 at = StandBy(tp, tree.TrunkRadius + 4.5f, c => c.GetComponentInParent<ForestTree>() == tree, 1.2f);
        Teleport(at, YawTo(at, tp), 25f);
        yield return Wait(0.7f);
        branches.RefreshNow();
        yield return Wait(0.2f);
        var near = new List<ForestTree>();
        forest.TreesNear(player.position, 28f, near);
        int grown = 0;
        foreach (var t in near) if (t != null && t.data != null && t.data.height >= branches.minTreeHeight && Flat(t.data.position - player.position) < 28f) grown++;
        int shown = branches.ShownCount;
        Check("N5.2 dry branches lie under the trees near the player", shown > 0,
            shown + " branches shown, " + grown + " grown trees within 28 m (about 1 branch a tree), taken so far " + branches.TakenCount);
        yield return Shot("N5_branches_under_trees");
        if (shown == 0) yield break;

        // one taken the player's way: the crosshair on it, the primary press -> a stick
        GameObject branch = null;
        float best = float.MaxValue;
        foreach (var go in branches.ShownObjects())
        {
            if (go == null || !IsLand(go.transform.position)) continue;
            float d = Flat(go.transform.position - player.position);
            if (d < best) { best = d; branch = go; }
        }
        if (branch == null) { Fail("N5.3 a branch taken", "no branch on land among the " + shown + " shown"); yield break; }
        Vector3 bp = branch.transform.position;
        // (from the side away from the nearest trunk, which would take the crosshair)
        ForestTree owner = null;
        float ownerD = float.MaxValue;
        near.Clear();
        forest.TreesNear(bp, 8f, near);
        foreach (var t in near) if (t != null && Flat(t.data.position - bp) < ownerD) { ownerD = Flat(t.data.position - bp); owner = t; }
        Vector3 away = FlatDir(bp - (owner != null ? owner.data.position : player.position), F);
        Vector3 side = bp + away * 5f;
        Teleport(side, YawTo(side, bp), 30f);
        yield return Wait(0.3f);
        int sticks = CountAll("stick"), taken0 = branches.TakenCount;
        yield return Take(branch.transform, "stick");
        branches.RefreshNow();
        yield return Wait(0.2f);
        bool shownAgain = false;
        foreach (var go in branches.ShownObjects()) if (go != null && Flat(go.transform.position - bp) < 0.05f) shownAgain = true;
        Check("N5.3 a branch taken (crosshair on it + press): +1 stick", lastTakeOk && CountAll("stick") == sticks + 1, "sticks " + sticks + " -> " + CountAll("stick"));
        Check("N5.4 the taken branch is remembered (TakenCount +1) and not shown again", branches.TakenCount == taken0 + 1 && !shownAgain,
            "TakenCount " + taken0 + " -> " + branches.TakenCount + ", shown again at its place " + shownAgain);

        // a grown leafy tree felled with the stone axe leaves 3-9 branches (Forest.OnFelled)
        if (CountAll("Axe") == 0) Give("Axe", 1);
        yield return Hold("Axe");
        var leafy = FindLeafyTree(player.position, 7f, 11f);
        if (leafy == null) { Fail("N5.5 a leafy tree felled", "no leafy tree 7-11 m high found"); yield break; }
        Vector3 basePos = leafy.data.position;
        float h = leafy.data.height;
        string species = leafy.Species != null ? leafy.Species.name : "species " + leafy.data.species;
        var branchesBefore = new HashSet<InteractableObject>(FelledBranchesNear(basePos, h + 8f));
        var logsBefore = new HashSet<InteractableObject>(LogsNear(basePos, h + 6f));
        yield return Chop(leafy, 6);
        Check("N5.5 a grown leafy tree felled with the stone axe", chopFell, species + ", " + chopInfo + ", blows that landed " + chopBlows);
        if (!chopFell) { EmptyHands(); yield break; }
        // (its logs and branches come when it lies on the ground)
        bool landed = false;
        for (float t = 0f; t < 8f && !landed; t += 0.25f)
        {
            foreach (var o in LogsNear(basePos, h + 6f)) if (!logsBefore.Contains(o)) landed = true;
            if (!landed) yield return Wait(0.25f);
        }
        yield return Wait(0.3f);
        var fresh = new List<InteractableObject>();
        foreach (var o in FelledBranchesNear(basePos, h + 8f)) if (!branchesBefore.Contains(o)) fresh.Add(o);
        Check("N5.6 the felled leafy tree leaves at least 3 branches (sticks)", fresh.Count >= 3,
            fresh.Count + " new branches from a " + h.ToString("0.0") + " m " + species + " (expected " + Mathf.Clamp(3 + Mathf.RoundToInt(h / 4f), 3, 9) + "), lying " + landed);
        yield return Shot("N5_felled_tree_branches");
        EmptyHands();
        if (fresh.Count > 0 && fresh[0] != null)
        {
            int s0 = CountAll("stick");
            yield return Take(fresh[0].transform, "stick");
            Check("N5.7 a branch of the felled tree taken: +1 stick", lastTakeOk, "sticks " + s0 + " -> " + CountAll("stick"));
        }
    }

    // ---------------- N1 winter ---------------- //

    WeatherSystem winterWeather; // the weather N1 turned to winter (put back afterwards)

    void SetWinter()
    {
        var weather = WeatherSystem.Instance;
        var time = TimeManager.Instance;
        winterWeather = weather;
        if (time != null) time.SetSeason(TimeManager.Season.Winter);
        if (weather != null)
        {
            weather.SetTestWeather(WeatherSystem.WeatherType.Snow);
            weather.SetSnowCover(1f);
            weather.SnapNow();
        }
    }

    void RestoreWinter()
    {
        if (winterWeather == null) return; // (never changed, or gone with its scene)
        var time = TimeManager.Instance;
        if (time != null) time.SetSeason(startSeason);
        winterWeather.SetSnowCover(startSnow);
        if (startAutoWeather) winterWeather.SetTestWeather(null);
        Note("winter put back: season " + startSeason + ", snow cover " + startSnow.ToString("0.00") + ", automatic weather " + startAutoWeather);
        winterWeather = null;
    }

    // a meadow (GrassPicking's rules): the terrain painted with grass (not the forest floor, sand or rock), above the
    // sea and away from the shore, the river and the trees, also all around it (the player stands 2.2 m away)
    static bool FindGrassSpot(Vector3 near, out Vector3 spot)
    {
        spot = near;
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return false;
        var data = terrain.terrainData;
        int grass = -1, floor = -1, sand = -1, rock = -1;
        var layers = data.terrainLayers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (layers[i] == null) continue;
            string name = layers[i].name.ToLowerInvariant();
            if (grass < 0 && name.Contains("grass")) grass = i;
            if (name.Contains("forestfloor")) floor = i;
            if (name.Contains("sand")) sand = i;
            if (rock < 0 && name.Contains("rock")) rock = i;
        }
        if (grass < 0) return false;
        int ar = data.alphamapResolution;
        Vector3 origin = terrain.transform.position;
        bool Meadow(Vector3 q)
        {
            float ground = Forest.GroundY(q);
            if (Ocean.Instance != null && (ground < Ocean.Instance.Level + 0.8f || Ocean.Instance.DistanceFromShore(q) > -10f)) return false;
            if (River.IsWater(q, 6f)) return false;
            Vector3 local = q - origin;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * ar), 0, ar - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * ar), 0, ar - 1);
            var a = data.GetAlphamaps(x, z, 1, 1);
            return a[0, 0, grass] >= 0.6f && (floor < 0 || a[0, 0, floor] < 0.2f) && (sand < 0 || a[0, 0, sand] < 0.2f) && (rock < 0 || a[0, 0, rock] < 0.3f);
        }
        var trees = new List<ForestTree>();
        for (float r = 0f; r <= 300f; r += 6f)
        {
            int steps = r < 1f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 8f);
            for (int s = 0; s < steps; s++)
            {
                float angle = s * Mathf.PI * 2f / steps;
                Vector3 p = near + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                if (!Meadow(p)) continue;
                bool all = true;
                for (int k = 0; k < 6 && all; k++) all = Meadow(p + Quaternion.Euler(0f, k * 60f, 0f) * Vector3.forward * 2.6f);
                if (!all) continue;
                trees.Clear();
                if (Forest.Instance != null) Forest.Instance.TreesNear(p, 8f, trees);
                bool tree = false;
                foreach (var t in trees) if (t != null && t.data != null && Flat(t.data.position - p) < 6f) tree = true;
                if (tree) continue;
                spot = new Vector3(p.x, Forest.GroundY(p), p.z);
                return true;
            }
        }
        return false;
    }

    // a plant the player can pick (a ScenePlant with an item, not the story's things), drawn now (Vegetation shows the
    // near ones): the nearest few are visited
    ScenePlant visiblePlant;
    IEnumerator FindVisiblePlant(float radius)
    {
        visiblePlant = null;
        var skip = new HashSet<string> { "prayerbook", "journal", "ironore", "plank", "hay", "stone", "stick" };
        var list = new List<ScenePlant>();
        foreach (var p in FindObjectsOfType<ScenePlant>())
            if (p != null && !string.IsNullOrEmpty(p.item) && !skip.Contains(p.item) && IsLand(p.transform.position) && Flat(p.transform.position - player.position) < radius) list.Add(p);
        // the ones a snow cover used to hide first (Vegetation.snowHideAt), then the nearest
        list.Sort((a, b) => a.hideUnderSnow != b.hideUnderSnow ? (a.hideUnderSnow ? -1 : 1)
            : Flat(a.transform.position - player.position).CompareTo(Flat(b.transform.position - player.position)));
        for (int i = 0; i < list.Count && i < 8; i++)
        {
            var plant = list[i];
            Vector3 pp = plant.transform.position;
            Vector3 from = pp + FlatDir(player.position - pp, -F) * 6f;
            if (!IsLand(from)) from = pp - FlatDir(player.position - pp, -F) * 6f;
            Teleport(from, YawTo(from, pp), 30f);
            yield return Wait(0.5f);
            bool drawn = false;
            foreach (var r in plant.GetComponentsInChildren<Renderer>(true)) if (r != null && r.enabled) drawn = true;
            if (drawn) { visiblePlant = plant; yield break; }
        }
    }

    // a campfire made from 6 stones (its card), put down where the player looks and its window opened (crosshair + press)
    Campfire madeFire;
    IEnumerator MakeCampfire(string what, Vector3 spot)
    {
        madeFire = null;
        Teleport(spot, YawTo(spot, spot + F), 38f);
        yield return Wait(0.5f);
        Give("stone", 6);
        yield return Craft(what, CraftingSystem.Instance.CampfireBLP, "craftCampfireBTN", "Campfire");
        if (FindItem("Campfire") == null) { Give("Campfire", 1); Note("(a campfire given, to go on)"); }
        var before = new HashSet<Campfire>(FindObjectsOfType<Campfire>());
        UseItem(FindItem("Campfire"));
        yield return Wait(0.8f);
        yield return TurnUntilValid(PlacementValid);
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(1.3f);
        foreach (var c in FindObjectsOfType<Campfire>()) if (!before.Contains(c) && !UnderHolding(c.transform)) madeFire = c;
        if (madeFire == null) yield break;
        Vector3 centre = madeFire.FireCentre;
        Vector3 from = centre + FlatDir(player.position - centre, -F) * 3f;
        Teleport(from, YawTo(from, centre), 30f);
        yield return Wait(0.5f);
        AimAt(centre + Vector3.up * 0.2f);
        yield return Wait(0.3f);
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Wait(0.3f);
        if (!CampfireUIManager.Instance.isUiOpen) { Note("(the campfire window opened by code: the press did not open it)"); madeFire.OpenUI(); yield return Wait(0.3f); }
    }

    IEnumerator WinterCheck()
    {
        var weather = WeatherSystem.Instance;
        var time = TimeManager.Instance;
        if (weather == null || time == null) { Fail("N1 winter", "no WeatherSystem or TimeManager"); yield break; }
        ClearQuickBar(Keep);
        ClearInventory();
        EmptyHands();
        DayNightSystem.Instance.TimeOfDay = 11f / 24f;
        SetWinter();
        yield return Wait(1.2f);
        Check("N1.1 winter, the ground under full snow (TEST: SetSeason, SetTestWeather(Snow), SetSnowCover(1))",
            time.currentSeason == TimeManager.Season.Winter && weather.SnowCover >= 0.99f,
            "season " + time.currentSeason + ", snow cover " + weather.SnowCover.ToString("0.00") + ", snowfall " + weather.Snow.ToString("0.00"));

        // hay: looking down at the grass of a meadow under the snow, the primary press
        int hay0 = Count("hay");
        string how = "no grassy meadow found on the terrain's paint";
        bool meadow = FindGrassSpot(S + F * 30f, out Vector3 grass);
        if (meadow)
        {
            for (int k = 0; k < 6 && Count("hay") == hay0; k++)
            {
                // (GrassPicking reaches 4.5 m from the eyes: the player stands 1.8 m from the spot)
                Vector3 dir = Quaternion.Euler(0f, k * 60f, 0f) * F;
                Vector3 stand = grass - dir * 1.8f;
                var fresh = FreshWater.Instance;
                if (fresh != null && fresh.At(new Vector3(stand.x, Forest.GroundY(stand) + 1.95f, stand.z)) != FreshWater.Source.None) continue; // (water to drink there)
                Teleport(stand, YawTo(stand, grass), 45f);
                yield return Wait(0.35f);
                AimAt(grass);
                yield return Wait(0.25f);
                bool canPick = GrassPicking.CanPick(GameInput.AimRay(), out _);
                bool hand = SelectionManager.Instance.handIsVisible;
                yield return Tap(GameInput.TouchAction.Primary);
                yield return Wait(0.3f);
                how = "at " + grass.ToString("F0") + " (try " + (k + 1) + "): the crosshair can pick grass " + canPick + ", hand shown " + hand;
            }
        }
        Check("N1.2 grass picked under the snow (looking down at a meadow + press): hay", Count("hay") > hay0, "hay " + hay0 + " -> " + Count("hay") + ", " + how);
        yield return Shot("N1_winter_grass");

        // a plant under the snow: drawn (not hidden by the snow) and taken (the crosshair on it + press)
        yield return FindVisiblePlant(250f);
        var plant = visiblePlant;
        Check("N1.3 a pickable plant is seen under the snow (Vegetation draws it)", plant != null,
            plant != null ? plant.item + " at " + plant.transform.position.ToString("F0") + " (hidden under snow before: " + plant.hideUnderSnow + ")" : "none of the nearest plants is drawn");
        if (plant != null)
        {
            string item = plant.item;
            yield return Take(plant.transform, item);
            Check("N1.4 the plant taken under the snow (crosshair on it + press)", lastTakeOk, item + " in the inventory: " + CountAll(item));
            yield return Shot("N1_winter_plant_taken");
        }

        // a campfire lit with that hay (wood in its fuel slot, "Light Fire" uses 1 hay)
        int hay = Count("hay");
        if (hay == 0) Skip("N1.6 a campfire lit with the picked hay", "no hay was picked");
        else
        {
            Vector3 spot = meadow ? grass + R * 3f : player.position + R * 4f;
            yield return MakeCampfire("N1.5 campfire crafted in winter (Crafting > Survival: 6 stones)", spot);
            var fire = madeFire;
            var ui = CampfireUIManager.Instance;
            if (fire == null) Fail("N1.6 a campfire lit with the picked hay", "the campfire was not placed");
            else
            {
                Give("log", 1);
                yield return Wait(0.2f);
                MoveInto(FindItem("log"), ui.fuelSlot);
                yield return Wait(0.3f);
                var light = GetField<Button>(ui, "lightFireButton");
                bool canLight = light != null && light.interactable;
                hay = Count("hay");
                if (canLight) light.onClick.Invoke(); else ui.LightFireButtonPressed();
                yield return Wait(0.4f);
                Check("N1.6 a campfire lit in the snow with the picked hay (wood + Light Fire)", canLight && fire.IsBurning && Count("hay") == hay - 1,
                    "button ready " + canLight + ", burning " + fire.IsBurning + ", hay " + hay + " -> " + Count("hay") + ", wood burns " + fire.BurnSpeed.ToString("0.0") + "x faster in the snow");
                if (ui.isUiOpen) ui.CloseUI();
                yield return Wait(0.3f);
                Vector3 c = fire.FireCentre;
                Vector3 view = c - F * 5f + R * 3f;
                Teleport(view, YawTo(view, c), 20f);
                yield return Wait(0.6f);
                yield return Shot("N1_winter_fire");
            }
        }
        RestoreWinter();
    }

    // ---------------- N6 the shy rabbits ---------------- //

    readonly List<RabbitLife> testRabbits = new List<RabbitLife>();

    // a grown, free rabbit let onto the grass here (RabbitPopulation's own Spawn); no partner meanwhile
    RabbitLife SpawnTestRabbit(Vector3 at, RabbitInfo.Sex sex)
    {
        var population = RabbitPopulation.Instance;
        var spawn = typeof(RabbitPopulation).GetMethod("Spawn", Any);
        if (population == null || spawn == null) return null;
        at.y = Buildings.GroundAt(at);
        var r = spawn.Invoke(population, new object[] { at, UnityEngine.Random.Range(0f, 360f), sex, RabbitPopulation.AdultHours + 20f }) as RabbitLife;
        if (r != null)
        {
            r.restHours = 500f;
            testRabbits.Add(r);
        }
        return r;
    }

    void RemoveTestRabbit(RabbitLife r)
    {
        if (r == null) return;
        testRabbits.Remove(r);
        r.gameObject.SetActive(false);
        Destroy(r.gameObject);
    }

    // ground a rabbit can run on (its own rules: above the sea, off the shore, out of the river)
    static bool RabbitGround(Vector3 p)
    {
        float ground = Forest.GroundY(p);
        if (Ocean.Instance != null && (ground < Ocean.Instance.Level + 0.8f || Ocean.Instance.DistanceFromShore(p) > -6f)) return false;
        return !River.IsWater(p, 3f);
    }

    // something solid there (a rock, scenery, a building): the player would bump into it
    static bool Solid(Vector3 p, float radius)
    {
        foreach (var c in Physics.OverlapSphere(p + Vector3.up * 1.5f, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (c is TerrainCollider || c is CharacterController) continue;
            if (c.GetComponentInParent<RabbitLife>() != null || c.GetComponentInParent<PlayerMovement>() != null) continue;
            if (c.GetComponentInParent<InteractableObject>() != null) continue; // (a plant's hand, a branch)
            return true;
        }
        return false;
    }

    // open grass: rabbit ground all around, nothing solid, no trees within 'clear' metres, not steep
    Vector3 FindOpenGround(Vector3 near, float clear, out bool ok)
    {
        var trees = new List<ForestTree>();
        for (float r = 0f; r <= 400f; r += 10f)
        {
            int steps = r < 1f ? 1 : Mathf.CeilToInt(2f * Mathf.PI * r / 14f);
            for (int s = 0; s < steps; s++)
            {
                float angle = s * Mathf.PI * 2f / steps;
                Vector3 p = near + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
                if (!RabbitGround(p)) continue;
                bool all = true;
                for (int k = 0; k < 8 && all; k++)
                {
                    Vector3 d = Quaternion.Euler(0f, k * 45f, 0f) * Vector3.forward;
                    all = RabbitGround(p + d * clear) && RabbitGround(p + d * clear * 0.5f);
                }
                if (!all) continue;
                if (Mathf.Abs(Forest.GroundY(p + R * clear) - Forest.GroundY(p - R * clear)) > clear * 0.35f) continue;
                if (Mathf.Abs(Forest.GroundY(p + F * clear) - Forest.GroundY(p - F * clear)) > clear * 0.35f) continue;
                trees.Clear();
                if (Forest.Instance != null) Forest.Instance.TreesNear(p, clear, trees);
                bool tree = false;
                foreach (var t in trees) if (t != null && t.data != null && Flat(t.data.position - p) < clear) tree = true;
                if (tree || Solid(p, clear)) continue;
                ok = true;
                return new Vector3(p.x, Forest.GroundY(p), p.z);
            }
        }
        ok = false;
        return near;
    }

    IEnumerator ShyRabbitsCheck()
    {
        var population = RabbitPopulation.Instance;
        if (population == null || typeof(RabbitPopulation).GetMethod("Spawn", Any) == null) { Fail("N6 shy rabbits", "no RabbitPopulation (or its Spawn)"); yield break; }
        ClearQuickBar(Keep);
        ClearInventory();
        EmptyHands();
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        var original = GameSettings.Difficulty;
        Vector3 open = FindOpenGround(S + F * 60f, 22f, out bool ok);
        if (!ok) open = FindOpenGround(S + F * 60f, 14f, out ok);
        Note("open grass for the rabbits at " + open.ToString("F0") + (ok ? "" : " (no clear place found: the nearest land)"));

        int n = 1;
        foreach (var level in new[] { GameSettings.Level.Easy, GameSettings.Level.Normal, GameSettings.Level.Hard })
        {
            GameSettings.Difficulty = level;
            yield return ShyRun(n++, open);
        }

        // hopping around: faster on Normal than on Easy (GameSettings.RabbitSpeed 1 / 1.3)
        yield return WanderSpeeds(GameSettings.Level.Easy, open);
        float easy = wanderMean, easyMin = wanderFieldMin, easyMax = wanderFieldMax;
        int easyN = wanderRabbits;
        yield return WanderSpeeds(GameSettings.Level.Normal, open);
        float normal = wanderMean, normalMin = wanderFieldMin, normalMax = wanderFieldMax;
        int normalN = wanderRabbits;
        float lo = RabbitLife.WanderSpeed.x, hi = RabbitLife.WanderSpeed.y;
        Check("N6.4 on Normal a wandering rabbit moves faster than on Easy (speeds x1 / x1.3)",
            easyN > 0 && normalN > 0 && normal > easy * 1.1f && easyMax <= hi + 0.02f && normalMin >= lo * 1.3f - 0.02f,
            "Easy " + easy.ToString("0.00") + " m/s (" + easyN + " rabbits, their speeds " + easyMin.ToString("0.0") + "-" + easyMax.ToString("0.0") + ", expected " + lo + "-" + hi + "), Normal "
            + normal.ToString("0.00") + " m/s (" + normalN + " rabbits, " + normalMin.ToString("0.0") + "-" + normalMax.ToString("0.0") + ", expected " + (lo * 1.3f).ToString("0.0") + "-" + (hi * 1.3f).ToString("0.0") + ")");

        // on Hard the player still catches one: the stone axe, running after it
        GameSettings.Difficulty = GameSettings.Level.Hard;
        if (CountAll("Axe") == 0) Give("Axe", 1);
        yield return Hold("Axe");
        Vector3 from = open - R * 16f;
        Teleport(from, YawTo(from, open), 8f);
        var prey = SpawnTestRabbit(open, RabbitInfo.Sex.Male);
        yield return Wait(0.5f);
        yield return Chase(prey, 30f);
        Check("N6.5 on Hard (runs from 10 m, 8.8 m/s) the player kills a rabbit with the stone axe by chasing it", chaseKilled, ChaseInfo() + ", held '" + HeldName() + "'");
        if (chaseKilled) yield return Shot("N6_hard_chase_killed");
        EmptyHands();

        // inside a closed fenced yard it stays calm (grown ones are taken there by hand)
        yield return PenCalm();

        GameSettings.Difficulty = original;
        Note("difficulty back to " + GameSettings.Difficulty);
    }

    // The player walks slowly (the joystick at half: 6 m/s) towards a calm rabbit on open grass: it must run when
    // the player comes within GameSettings.RabbitShyRadius (not much earlier), at about RabbitShySpeed for about
    // RabbitShyRun metres.
    IEnumerator ShyRun(int n, Vector3 open)
    {
        string L = GameSettings.Difficulty.ToString();
        float radius = GameSettings.RabbitShyRadius, runSpeed = GameSettings.RabbitShySpeed, runDistance = GameSettings.RabbitShyRun;
        Vector3 from = open - R * (radius + 9f);
        Teleport(from, YawTo(from, open), 0f);
        yield return Wait(0.3f);
        var rabbit = SpawnTestRabbit(open, n % 2 == 0 ? RabbitInfo.Sex.Female : RabbitInfo.Sex.Male);
        if (rabbit == null) { Fail("N6." + n + " " + L, "no rabbit could be let onto the grass"); yield break; }
        float t = 0f;
        while (t < 1.5f && rabbit != null && (GetField<float>(rabbit, "fleeTimer") > 0f || GetField<float>(rabbit, "shyCooldown") > 0f)) { t += Time.unscaledDeltaTime; yield return null; }
        yield return Wait(0.3f);
        if (rabbit == null) { Fail("N6." + n + " " + L, "the rabbit was gone"); yield break; }
        int runs0 = rabbit.ShyRuns;
        float startGap = Flat(rabbit.transform.position - player.position);

        float trigger = -1f, closest = startGap;
        __WalkDriver.On = true;
        __WalkDriver.Move = new Vector2(0f, 0.5f);
        float start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < 10f && rabbit != null)
        {
            Vector3 rp = rabbit.transform.position;
            float d = Flat(rp - player.position);
            look.SetLook(YawTo(player.position, rp), 0f);
            if (rabbit.ShyRuns > runs0) { trigger = d; break; } // (measured right after: one step of each later)
            closest = Mathf.Min(closest, d);
            if (d < radius - 3f) break; // (it did not run)
            yield return null;
        }
        __WalkDriver.Move = Vector2.zero;
        __WalkDriver.On = false;
        GameInput.touchMove = Vector2.zero;

        // its run: measured frame by frame
        float fieldSpeed = rabbit != null ? GetField<float>(rabbit, "speed") : 0f;
        float runFor = rabbit != null ? Mathf.Max(0.3f, GetField<float>(rabbit, "fleeTimer")) : 0f;
        Vector3 runStart = rabbit != null ? rabbit.transform.position : Vector3.zero, prev = runStart;
        float path = 0f, moving = 0f;
        for (t = 0f; trigger > 0f && t < runFor + 0.2f && rabbit != null; t += Time.unscaledDeltaTime)
        {
            yield return null;
            if (rabbit == null) break;
            Vector3 now = rabbit.transform.position;
            float step = Flat(now - prev);
            if (step > 0.0005f) { path += step; moving += Mathf.Min(Time.deltaTime, 0.1f); }
            prev = now;
        }
        float measured = moving > 0f ? path / moving : 0f;
        float ran = rabbit != null ? Flat(rabbit.transform.position - runStart) : 0f;
        Check("N6." + n + "a " + L + ": a rabbit runs when the player comes within " + radius + " m, not much earlier",
            trigger > 0f && runs0 == 0 && trigger <= radius + 0.6f && trigger >= radius - 1.5f,
            trigger > 0f ? "it ran with the player " + trigger.ToString("0.00") + " m away (the walk began " + startGap.ToString("0.0") + " m away, runs before " + runs0 + ")"
                : "it did not run; the player came to " + closest.ToString("0.0") + " m");
        Check("N6." + n + "b " + L + ": it runs at about " + runSpeed + " m/s for about " + runDistance + " m",
            trigger > 0f && Mathf.Abs(measured / runSpeed - 1f) < 0.2f && Mathf.Abs(fieldSpeed - runSpeed) < 0.05f && ran > runDistance * 0.6f && ran < runDistance * 1.2f,
            "measured " + measured.ToString("0.0") + " m/s (its speed " + fieldSpeed.ToString("0.0") + "), " + ran.ToString("0.0") + " m in " + moving.ToString("0.00") + " s");
        if (n == 3 && trigger > 0f) yield return Shot("N6_hard_rabbit_runs");
        RemoveTestRabbit(rabbit);
        yield return null;
    }

    // Rabbits hopping around on their own (the player far away): the speed each is given (RabbitLife.WanderSpeed x
    // GameSettings.RabbitSpeed) and the speed measured
    float wanderMean, wanderFieldMin, wanderFieldMax;
    int wanderRabbits;

    IEnumerator WanderSpeeds(GameSettings.Level level, Vector3 open)
    {
        GameSettings.Difficulty = level;
        // (far enough that they do not run from the player: Easy 4 m, Normal 7 m; they hop up to 12 m)
        Vector3 from = open - R * 26f;
        if (!IsLand(from)) from = open + R * 26f;
        Teleport(from, YawTo(from, open), 5f);
        var list = new List<RabbitLife>();
        for (int i = 0; i < 6; i++)
        {
            var r = SpawnTestRabbit(open + Quaternion.Euler(0f, i * 60f, 0f) * F * 5f, i % 2 == 0 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female);
            if (r != null) list.Add(r);
        }
        var prev = new Vector3[list.Count];
        var dist = new float[list.Count];
        var time = new float[list.Count];
        for (int i = 0; i < list.Count; i++) prev[i] = list[i].transform.position;
        wanderFieldMin = float.MaxValue;
        wanderFieldMax = 0f;
        for (float t = 0f; t < 6f; t += Time.unscaledDeltaTime)
        {
            yield return null;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;
                Vector3 p = r.transform.position;
                float step = Flat(p - prev[i]);
                prev[i] = p;
                bool wandering = GetField<bool>(r, "moving") && GetField<float>(r, "fleeTimer") <= 0f && r.mate == null && r.lure == null && !r.IsLeaving && r.ShyRuns == 0;
                if (!wandering || step < 0.0001f) continue;
                float field = GetField<float>(r, "speed");
                wanderFieldMin = Mathf.Min(wanderFieldMin, field);
                wanderFieldMax = Mathf.Max(wanderFieldMax, field);
                dist[i] += step;
                time[i] += dt;
            }
        }
        float sum = 0f;
        wanderRabbits = 0;
        for (int i = 0; i < list.Count; i++)
            if (time[i] > 0.3f) { sum += dist[i] / time[i]; wanderRabbits++; }
        wanderMean = wanderRabbits > 0 ? sum / wanderRabbits : 0f;
        if (wanderRabbits == 0) { wanderFieldMin = wanderFieldMax = 0f; }
        Note(level + ": " + wanderRabbits + " of " + list.Count + " rabbits hopped around, " + wanderMean.ToString("0.00") + " m/s on average");
        foreach (var r in list) RemoveTestRabbit(r);
        yield return null;
    }

    // a fenced yard built the player's way, a rabbit let in, the player inside with it: it does not run (Hard: 10 m)
    IEnumerator PenCalm()
    {
        GameSettings.Difficulty = GameSettings.Level.Hard;
        EmptyHands();
        ClearInventory(Keep);
        Vector3 c = S + F * 70f;
        yield return BuildYard(c, 4f);
        bool closed = Buildings.IsClosedAll(c, 65f, true);
        if (!closed) { Skip("N6.6 a rabbit in a closed fenced yard stays calm", "the yard did not close (" + yardPlaced + " of " + yardPanels + " panels placed)"); yield break; }
        var rabbit = SpawnTestRabbit(c + R * 1.5f, RabbitInfo.Sex.Female);
        if (rabbit == null) { Fail("N6.6 a rabbit in a closed fenced yard stays calm", "no rabbit could be let in"); yield break; }
        Vector3 inside = c - R * 2f - F * 1.5f;
        Teleport(inside, YawTo(inside, rabbit.transform.position), 20f);
        int runs0 = rabbit.ShyRuns;
        float closest = float.MaxValue, farthest = 0f;
        bool shot = false;
        for (float t = 0f; t < 3.5f && rabbit != null; t += Time.unscaledDeltaTime)
        {
            float d = Flat(rabbit.transform.position - player.position);
            closest = Mathf.Min(closest, d);
            farthest = Mathf.Max(farthest, d);
            look.SetLook(YawTo(player.position, rabbit.transform.position), 25f);
            if (!shot && t > 1.5f) { shot = true; yield return Shot("N6_pen_calm"); }
            yield return null;
        }
        bool inPen = rabbit != null && RabbitPopulation.InPen(rabbit);
        Check("N6.6 a rabbit in a closed fenced yard stays calm (Hard: it would run from 10 m)", rabbit != null && rabbit.ShyRuns == runs0 && inPen,
            "runs " + (rabbit != null ? rabbit.ShyRuns - runs0 : -1) + " with the player " + closest.ToString("0.0") + "-" + farthest.ToString("0.0") + " m away for 3.5 s, in the pen (InPen) " + inPen
            + ", yard " + yardPlaced + " of " + yardPanels + " panels");
        RemoveTestRabbit(rabbit);
    }

    // ---------------- loading a save (N2, N3, N4) ---------------- //

    bool loadOk;
    float loadSeconds, screenGoneAfter;
    bool screenSeen, screenLoadingText, screenPrayer;
    string screenTexts = "";

    // Loaded the way the Load Game screen does it (SaveManager.StartLoadedGame: the game scene again, then the save),
    // with an eye on the loading screen (LoadingScreen) meanwhile
    IEnumerator LoadTestSave(string shotName)
    {
        var save = SaveManager.Instance;
        var oldStory = story;
        loadOk = false;
        screenSeen = screenLoadingText = screenPrayer = false;
        screenTexts = "";
        screenGoneAfter = -1f;
        var names = new List<string>();
        float start = Time.realtimeSinceStartup, loadedAt = -1f;
        bool shot = false;
        save.StartLoadedGame(TestSlot);
        while (Time.realtimeSinceStartup - start < 30f)
        {
            var screen = LoadingScreen.Instance;
            if (screen != null)
            {
                screenSeen = true;
                foreach (var text in screen.GetComponentsInChildren<Text>(true))
                {
                    string name = text.gameObject.name;
                    if (!names.Contains(name)) names.Add(name);
                    if (name == "Prayer" || name == "Prayer title" || name == "Band") screenPrayer = true;
                    if (name == "Loading" && text.text.StartsWith(Lang.T("Loading", "Se încarcă"))) screenLoadingText = true;
                }
                foreach (Transform child in screen.transform) if (child.name == "Band" || child.name.StartsWith("Prayer")) screenPrayer = true;
                if (!shot && Time.realtimeSinceStartup - start > 0.6f) { shot = true; yield return Shot(shotName); }
            }
            var s = StoryManager.Instance;
            bool ready = s != null && s != oldStory && GetField<StoryData>(s, "data") != null && !GetField<bool>(s, "loading") && SceneSystem.PendingLoadSlot <= 0;
            if (ready && loadedAt < 0f) loadedAt = Time.realtimeSinceStartup;
            if (ready && (screen == null || Time.realtimeSinceStartup - loadedAt > 8f)) break;
            yield return null;
        }
        loadSeconds = (loadedAt > 0f ? loadedAt : Time.realtimeSinceStartup) - start;
        if (loadedAt > 0f && LoadingScreen.Instance == null) screenGoneAfter = Time.realtimeSinceStartup - loadedAt;
        screenTexts = string.Join(", ", names);
        AfterLoad(oldStory);
        yield return Wait(0.8f);
    }

    // the loaded game's player, view and story (held still again: its goals would finish levels and show pages)
    void AfterLoad(StoryManager oldStory)
    {
        story = StoryManager.Instance;
        loadOk = story != null && story != oldStory && GetField<StoryData>(story, "data") != null;
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) { player = movement.transform; look = player.GetComponent<MouseMovement>(); }
        if (look != null) look.enabled = false;
        if (story != null) { SetField(story, "modal", true); frozen = true; }
        else frozen = false;
        var start = FindObjectOfType<StoryStart>();
        if (start != null)
        {
            S = start.transform.position;
            F = FlatDir(start.transform.forward, Vector3.forward);
            R = Vector3.Cross(Vector3.up, F);
        }
    }

    // the test save away (files, picture, the slot's name)
    void DeleteTestSave()
    {
        try
        {
            string folder = Application.dataPath + "/";
            bool any = false;
            foreach (var name in new[] { "SaveGame" + TestSlot + ".json", "SaveGame" + TestSlot + ".jpg", "SaveGame" + TestSlot + ".bin" })
            {
                if (File.Exists(folder + name)) { File.Delete(folder + name); any = true; }
                if (File.Exists(folder + name + ".meta")) File.Delete(folder + name + ".meta");
            }
            string persistent = Application.persistentDataPath + "/SaveGame" + TestSlot + ".bin";
            if (File.Exists(persistent)) { File.Delete(persistent); any = true; }
            if (PlayerPrefs.HasKey("Slot" + TestSlot + "Description")) { PlayerPrefs.DeleteKey("Slot" + TestSlot + "Description"); PlayerPrefs.Save(); any = true; }
            if (any) Note("test save " + TestSlot + " deleted");
        }
        catch (Exception e) { Note("could not delete the test save: " + e.Message); }
    }

    static int OccupiedFrom(int index)
    {
        int n = 0;
        var slots = InventorySystem.Instance.slotList;
        for (int i = index; i < slots.Count; i++) if (slots[i] != null && slots[i].transform.childCount > 0) n++;
        return n;
    }

    // ---------------- N2 + N4: a load gives back only what was saved; the loading screen ---------------- //

    IEnumerator LoadNoAxesCheck()
    {
        var save = SaveManager.Instance;
        if (save == null) { Fail("N2 load", "no SaveManager in the scene"); yield break; }
        EmptyHands();
        ClearQuickBar();
        ClearInventory();
        Give("stone", 1);
        Give("hay", 1);
        yield return Wait(0.3f);
        string saved = ItemsText();
        var inv = InventorySystem.Instance;
        int slots = inv.slotList.Count;
        save.SaveGame(TestSlot);
        yield return Wait(0.5f);
        Check("N2.1 saved to slot " + TestSlot + ": 1 stone + 1 hay, nothing in the quick bar", save.DoesFileExist(TestSlot) && saved == "hay x1, stone x1",
            "saved [" + saved + "], " + slots + " slots, backpack " + inv.HasBackpack);
        // changed after saving: gone again after the load
        Give("ironore", 2);

        yield return LoadTestSave("N4_loading_screen");
        inv = InventorySystem.Instance;
        Check("N2.2 the save loads (the game scene again)", loadOk, "after " + loadSeconds.ToString("0.0") + " s");
        string loaded = ItemsText();
        Check("N2.3 the inventory and the quick bar hold exactly what was saved", loaded == saved, "saved [" + saved + "], loaded [" + loaded + "]");
        Check("N2.4 no stone axe came back from the scene (inventory, quick bar, hand)", FindItem("Axe") == null && !IsHeld("Axe") && FindItem("ironaxe") == null,
            "axes " + CountAll("Axe") + ", held '" + HeldName() + "', quick bar [" + QuickBarText() + "]");
        Check("N2.5 32 slots (no backpack in this save)", inv.slotList.Count == 32 && !inv.HasBackpack, inv.slotList.Count + " slots, HasBackpack " + inv.HasBackpack);

        Check("N4.1 the loading screen shows while the save loads", screenSeen, "texts on it: " + (screenTexts.Length > 0 ? screenTexts : "-"));
        Check("N4.2 no prayer on it (no \"Prayer\", \"Prayer title\" or \"Band\")", screenSeen && !screenPrayer, "texts on it: " + screenTexts);
        Check("N4.3 it says \"" + Lang.T("Loading", "Se încarcă") + "...\"", screenLoadingText, "texts on it: " + screenTexts);
        Check("N4.4 it goes away within 5 s after the load", screenGoneAfter >= 0f && screenGoneAfter <= 5f,
            screenGoneAfter >= 0f ? "gone " + screenGoneAfter.ToString("0.0") + " s after the save was put back (the load took " + loadSeconds.ToString("0.0") + " s)" : "still there 8 s after the load");
        yield return Shot("N2_after_load");
    }

    static string QuickBarText()
    {
        var names = new List<string>();
        foreach (var slot in EquipSystem.Instance.quickSlotsList)
            if (slot != null && slot.transform.childCount > 0) names.Add(InventorySystem.CleanName(slot.transform.GetChild(0).name));
        return string.Join(", ", names);
    }

    // ---------------- N3 the backpack ---------------- //

    IEnumerator OpenSurvival()
    {
        var cs = CraftingSystem.Instance;
        if (!cs.isOpen) yield return Tap(GameInput.TouchAction.Crafting);
        var button = cs.craftingScreenUI != null ? cs.craftingScreenUI.transform.Find("SurvivalButton") : null;
        if (button != null && button.GetComponent<Button>() != null) button.GetComponent<Button>().onClick.Invoke();
        yield return Wait(0.35f);
    }

    static string CardText(Transform card)
    {
        if (card == null) return "no card";
        var texts = new List<string>();
        foreach (var t in card.GetComponentsInChildren<Text>(false)) if (!string.IsNullOrEmpty(t.text)) texts.Add(t.text.Replace("\n", " "));
        return "the card: [" + string.Join(" | ", texts) + "]";
    }

    // where a UI rectangle is on the screen (pixels)
    static Rect ScreenRect(RectTransform rect)
    {
        var corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        var canvas = rect.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
        Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
        foreach (var c in corners)
        {
            Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    static bool Inside(Rect inner, Rect outer, float tolerance) =>
        inner.xMin >= outer.xMin - tolerance && inner.yMin >= outer.yMin - tolerance && inner.xMax <= outer.xMax + tolerance && inner.yMax <= outer.yMax + tolerance;

    static Rect ScreenArea => new Rect(0f, 0f, Screen.width, Screen.height);

    static string R0(Rect r) => "(" + r.xMin.ToString("0") + ", " + r.yMin.ToString("0") + ") - (" + r.xMax.ToString("0") + ", " + r.yMax.ToString("0") + ")";

    // the inventory window's slots: columns and rows, how many are outside the window or off the screen
    int layoutColumns, layoutRows, layoutOutside, layoutOffScreen;
    string layoutInfo = "";

    void MeasureInventory()
    {
        var inv = InventorySystem.Instance;
        var window = inv.inventoryScreenUI.transform as RectTransform;
        Rect w = ScreenRect(window);
        var xs = new List<float>();
        var ys = new List<float>();
        layoutOutside = layoutOffScreen = 0;
        foreach (var slot in inv.slotList)
        {
            if (slot == null) continue;
            Rect s = ScreenRect(slot.transform as RectTransform);
            AddDistinct(xs, s.center.x);
            AddDistinct(ys, s.center.y);
            if (!Inside(s, w, 2f)) layoutOutside++;
            if (!Inside(s, ScreenArea, 1f)) layoutOffScreen++;
        }
        layoutColumns = xs.Count;
        layoutRows = ys.Count;
        var title = window.Find("WindowTitle");
        string titleText = title != null && title.GetComponent<Text>() != null ? title.GetComponent<Text>().text : "-";
        layoutInfo = "window " + R0(w) + " on the " + Screen.width + " x " + Screen.height + " screen, title '" + titleText + "'";
    }

    static void AddDistinct(List<float> list, float v)
    {
        foreach (var x in list) if (Mathf.Abs(x - v) < 2f) return;
        list.Add(v);
    }

    IEnumerator BackpackCheck()
    {
        var inv = InventorySystem.Instance;
        var cs = CraftingSystem.Instance;
        var save = SaveManager.Instance;
        if (cs.BackpackBLP == null) { Fail("N3 backpack", "no CraftingSystem.BackpackBLP"); yield break; }
        EmptyHands();
        ClearQuickBar();
        ClearInventory();
        int slots0 = inv.slotList.Count;
        Check("N3.1 the inventory starts with 32 slots and no backpack", slots0 == 32 && !inv.HasBackpack, slots0 + " slots, HasBackpack " + inv.HasBackpack);
        if (inv.HasBackpack) { Skip("N3 backpack", "the backpack is there already"); yield break; }

        // Crafting > Survival: its card
        yield return OpenSurvival();
        var card = cs.survivalScreenUI != null ? cs.survivalScreenUI.transform.Find("Backpack") : null;
        var button = GetField<Button>(cs, "craftBackpackBTN");
        cs.RefreshNeededItems();
        yield return Wait(0.2f);
        Check("N3.2 the \"Backpack\" card is in Crafting > Survival", card != null && card.gameObject.activeInHierarchy && button != null,
            "card " + (card != null) + ", shown " + (card != null && card.gameObject.activeInHierarchy) + ", " + CardText(card));
        Check("N3.3 without materials its Craft button is greyed out", button != null && !button.interactable, CardText(card));
        yield return Shot("N3_backpack_card");

        Give("Rabbit Hide", 3);
        Give("stick", 2);
        cs.RefreshNeededItems();
        yield return Wait(0.2f);
        bool ready = button != null && button.interactable;
        Check("N3.4 with 3 \"Rabbit Hide\" + 2 \"stick\" the Craft button is ready", ready, CardText(card));
        int added = 0;
        Action onAdded = () => added++;
        InventorySystem.BackpackAdded += onAdded;
        if (ready) button.onClick.Invoke();
        yield return Wait(0.4f);
        InventorySystem.BackpackAdded -= onAdded;
        Check("N3.5 crafted (its card's Craft button): 32 -> 48 slots, HasBackpack, the hides and sticks used",
            inv.slotList.Count == 48 && inv.HasBackpack && Count("Rabbit Hide") == 0 && Count("stick") == 0 && added == 1,
            "slots " + slots0 + " -> " + inv.slotList.Count + ", HasBackpack " + inv.HasBackpack + ", BackpackAdded " + added + " time(s), hides left " + Count("Rabbit Hide") + ", sticks left " + Count("stick"));
        cs.RefreshNeededItems();
        yield return Wait(0.2f);
        var label = button != null ? button.GetComponentInChildren<Text>(true) : null;
        string have = Lang.T("You have it", "Îl ai deja");
        Check("N3.6 the card then says \"" + have + "\" and is greyed out", label != null && label.text == have && button != null && !button.interactable,
            "label '" + (label != null ? label.text : "-") + "', button ready " + (button != null && button.interactable));

        // not a second time, even with the materials
        Give("Rabbit Hide", 3);
        Give("stick", 2);
        cs.RefreshNeededItems();
        yield return Wait(0.2f);
        bool again = button != null && button.interactable;
        if (again) button.onClick.Invoke();
        bool tried = cs.TryCraft(cs.BackpackBLP);
        yield return Wait(0.2f);
        Check("N3.7 it can't be made a second time", !again && !tried && inv.slotList.Count == 48 && Count("Rabbit Hide") == 3,
            "button ready " + again + ", TryCraft " + tried + ", slots " + inv.slotList.Count + ", hides " + Count("Rabbit Hide") + ", " + CardText(card));
        yield return Shot("N3_backpack_card_made");
        if (cs.isOpen) yield return Tap(GameInput.TouchAction.Crafting);
        if (cs.isOpen) cs.CloseUI();
        ClearInventory();

        // the inventory window: 12 columns x 4 rows, every slot inside the window and on the screen
        yield return Tap(GameInput.TouchAction.Inventory);
        yield return Wait(0.4f);
        MeasureInventory();
        Check("N3.8 the inventory opens (Inventory button / I) with 12 columns x 4 rows", inv.isOpen && layoutColumns == 12 && layoutRows == 4,
            "open " + inv.isOpen + ", " + layoutColumns + " columns x " + layoutRows + " rows, " + layoutInfo);
        Check("N3.9 all 48 slots are inside the window and on the screen", inv.isOpen && layoutOutside == 0 && layoutOffScreen == 0,
            layoutOutside + " outside the window, " + layoutOffScreen + " off the screen; " + layoutInfo);
        yield return Shot("N3_inventory_backpack");

        // the crafting window next to it (Crafting > Survival)
        yield return OpenSurvival();
        yield return Wait(0.3f);
        var window = inv.inventoryScreenUI.transform as RectTransform;
        var survival = cs.survivalScreenUI != null ? cs.survivalScreenUI.transform as RectTransform : null;
        bool both = inv.isOpen && cs.isOpen && survival != null && survival.gameObject.activeInHierarchy;
        Rect a = ScreenRect(window), b = survival != null ? ScreenRect(survival) : new Rect();
        Rect shrunk = new Rect(a.xMin + 2f, a.yMin + 2f, a.width - 4f, a.height - 4f);
        bool overlap = both && shrunk.Overlaps(b);
        Check("N3.10 the inventory and Crafting > Survival side by side: both on the screen, not over each other",
            both && !overlap && Inside(a, ScreenArea, 1f) && Inside(b, ScreenArea, 1f),
            "both open " + both + ", inventory " + R0(a) + ", crafting " + R0(b) + ", overlap " + overlap + ", screen " + Screen.width + " x " + Screen.height);
        yield return Shot("N3_inventory_and_crafting");
        if (cs.isOpen) cs.CloseUI();
        if (inv.isOpen) yield return Tap(GameInput.TouchAction.Inventory);
        if (inv.isOpen) inv.CloseUI();

        // saved and loaded: the backpack stays, so do the things in its slots (index 32 and more)
        if (save == null) { Fail("N3.11 the backpack kept by a save", "no SaveManager"); yield break; }
        ClearInventory();
        Give("stone", 34);   // slots 0-33: two of them are the backpack's
        Give("ironore", 3);  // slots 34-36, moved further in (the player drags them there)
        for (int k = 0; k < 3; k++)
        {
            var from = inv.slotList[34 + k];
            if (from != null && from.transform.childCount > 0) MoveInto(from.transform.GetChild(0).gameObject, inv.slotList[44 + k]);
        }
        yield return Wait(0.3f);
        string saved = ItemsText();
        int beyond = OccupiedFrom(InventorySystem.Instance.slotList.Count - InventorySystem.BackpackSlots);
        Check("N3.11 37 things in the inventory, 5 of them in the backpack's slots (index 32 and more)", beyond == 5 && Count("stone") == 34 && Count("ironore") == 3,
            "[" + saved + "], in slots 32-47: " + beyond);
        save.SaveGame(TestSlot);
        yield return Wait(0.5f);
        yield return LoadTestSave("N4_loading_screen_backpack");
        inv = InventorySystem.Instance;
        Check("N3.12 after the load the backpack is kept: 48 slots", loadOk && inv.HasBackpack && inv.slotList.Count == 48,
            "loaded " + loadOk + " after " + loadSeconds.ToString("0.0") + " s, HasBackpack " + inv.HasBackpack + ", " + inv.slotList.Count + " slots");
        string loaded = ItemsText();
        Check("N3.13 everything is back, also what lay in the backpack's slots (37 things: more than 32 slots hold)", loaded == saved && OccupiedFrom(32) == 5,
            "saved [" + saved + "], loaded [" + loaded + "], in slots 32-47 now " + OccupiedFrom(32));
        Note("loading screen of this load: seen " + screenSeen + ", prayer " + screenPrayer + ", gone " + screenGoneAfter.ToString("0.0") + " s after the load, texts [" + screenTexts + "]");
        yield return Tap(GameInput.TouchAction.Inventory);
        yield return Wait(0.4f);
        MeasureInventory();
        Check("N3.14 the loaded game's inventory window: 12 columns x 4 rows, all inside it and on the screen",
            inv.isOpen && layoutColumns == 12 && layoutRows == 4 && layoutOutside == 0 && layoutOffScreen == 0,
            layoutColumns + " columns x " + layoutRows + " rows, " + layoutOutside + " outside, " + layoutOffScreen + " off the screen; " + layoutInfo);
        yield return Shot("N3_inventory_after_load");
        if (inv.isOpen) yield return Tap(GameInput.TouchAction.Inventory);
        if (inv.isOpen) inv.CloseUI();
        ClearInventory();
        DeleteTestSave();
    }
}
#endif
