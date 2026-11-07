#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// TEMPORARY (Claude): the checks of the island's rivers (the 5 new ones of 2026-11-07 and the old one with the lake),
// run by the systems test when its Folder starts with "Rivers" (the driver's jobs RiversPhone15 / RiversPC15). The same
// class as __SystemsTest (partial): the same helpers and the same way of playing (the crosshair + the primary press with
// GameInput.TestDriven, the joystick through __WalkDriver), so it runs in the phone Simulator and in the PC Game view.
//   R1 the rivers: River.All (the old one + 5 named ones), their points and a drawn water mesh
//   R2 each new river springs on a mountain and runs down to the sea, its water never going uphill (+ pictures)
//   R3 the water lies in its bed: the bed under the middle lower than the water, no water over lower land beside it
//   R4 no tree in or at the water, no plant in it
//   R8 the map draws the rivers          R9 the level-2 map goal points to the nearest river of all
//   R5 drinking at 3 places of each new river (the crosshair offers "Drink water", the water bar rises)
//   R6 walking across each new river at 2 flat places with the joystick
//   R10 Brother Ioan's paths walked across the new rivers   R7 rabbits keep out of the water (the clock x20)
// Writes into the same systems.txt (PASS / FAIL / SKIP lines) and PNG screenshots. Removed after the tests.
public partial class __SystemsTest
{
    partial void AddRiverChecks(List<(string name, Func<IEnumerator> body, float timeout)> checks)
    {
        checks.Add(("R0 run setup", RiversRunSetup, 10f));
        checks.Add(("R1 the rivers", RiversListCheck, 15f));
        checks.Add(("R2 from the mountains to the sea", RiversFlowCheck, 45f));
        checks.Add(("R3 the water in its bed", RiversBedCheck, 15f));
        checks.Add(("R4 no trees or plants in the water", RiversTreesCheck, 30f));
        checks.Add(("R8 the map", RiversMapCheck, 20f));
        checks.Add(("R9 the level-2 map goal", RiversGoalCheck, 20f));
        checks.Add(("R5 drinking", RiversDrinkCheck, 80f));
        checks.Add(("R6 crossing on foot", RiversCrossCheck, 150f));
        checks.Add(("R10 Brother Ioan's paths", RiversPathsCheck, 90f));
        checks.Add(("R7 rabbits keep out of the water", RiversRabbitsCheck, 60f));
    }

    // what the river checks changed: between the checks (atEnd false) and at the end of the run (true)
    partial void RestoreRivers(bool atEnd)
    {
        foreach (var r in riverRabbits) if (r != null) { r.gameObject.SetActive(false); Destroy(r.gameObject); }
        riverRabbits.Clear();
        if (riverLevelSet && Data != null) { Data.level = riverLevel; riverLevelSet = false; }
        if (!atEnd) return;
        if (riverWeather != null)
        {
            riverWeather.SetTestWeather(null);
            Note("the weather is automatic again");
            riverWeather = null;
        }
        if (riverStartSet && player != null && look != null)
        {
            riverStartSet = false;
            Teleport(riverStartPos, riverStartYaw, 0f);
            Note("the player back at " + riverStartPos.ToString("F0"));
        }
    }

    readonly List<River> rvAll = new List<River>(), rvNew = new List<River>();
    River rvOld;
    readonly List<RabbitLife> riverRabbits = new List<RabbitLife>();
    WeatherSystem riverWeather;       // set to clear for the run (it was automatic): automatic again at the end
    Vector3 riverStartPos;
    float riverStartYaw;
    bool riverStartSet;
    int riverLevel;                   // the story's level while R9 looks at level 2's map goal
    bool riverLevelSet;

    // ---------------- the rivers: names, where their water is ---------------- //

    // every river of the scene: River.All, and any the list left out (R1 tells); the old one has the lake
    void FindRivers()
    {
        rvAll.Clear();
        rvNew.Clear();
        rvOld = null;
        foreach (var r in River.All) if (r != null && r.isActiveAndEnabled && !rvAll.Contains(r)) rvAll.Add(r);
        foreach (var r in FindObjectsOfType<River>()) if (r != null && r.isActiveAndEnabled && !rvAll.Contains(r)) rvAll.Add(r);
        foreach (var r in rvAll)
        {
            if (r.lakeRadius > 0f && rvOld == null) rvOld = r;
            else rvNew.Add(r);
        }
        rvNew.Sort((a, b) => string.CompareOrdinal(a.riverName, b.riverName));
    }

    static string RvName(River r)
    {
        if (r == null) return "no river";
        if (!string.IsNullOrEmpty(r.riverName)) return "\"" + r.riverName + "\"";
        return r.lakeRadius > 0f ? "the old river (lake)" : "'" + r.name + "' (no name)";
    }

    // for the screenshots' names
    static string RvFile(River r)
    {
        string s = r == null ? "none" : !string.IsNullOrEmpty(r.riverName) ? r.riverName : r.lakeRadius > 0f ? "old" : r.name;
        var sb = new System.Text.StringBuilder();
        foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
        return sb.ToString();
    }

    static float RvHW(River r, int i) => i < r.halfWidths.Count ? r.halfWidths[i] : 3f;
    static int RvIndex(River r, float f) => Mathf.Clamp(Mathf.RoundToInt(f * (r.points.Count - 1)), 0, Mathf.Max(0, r.points.Count - 1));

    static Vector3 RvAlong(River r, int i)
    {
        int n = r.points.Count;
        return FlatDir(r.points[Mathf.Min(i + 1, n - 1)] - r.points[Mathf.Max(i - 1, 0)], Vector3.forward);
    }

    static Vector3 RvSide(River r, int i) => Vector3.Cross(Vector3.up, RvAlong(r, i));
    static float RvSea => Ocean.Instance != null ? Ocean.Instance.Level : -19.05f;
    static float RvShore(Vector3 p) => Ocean.Instance != null ? Ocean.Instance.DistanceFromShore(p) : -999f;

    static float RvSteep(Vector3 p)
    {
        var t = Terrain.activeTerrain;
        if (t == null || t.terrainData == null) return 0f;
        var d = t.terrainData;
        Vector3 l = p - t.transform.position;
        return d.GetSteepness(Mathf.Clamp01(l.x / d.size.x), Mathf.Clamp01(l.z / d.size.z));
    }

    // metres to the nearest water's edge of all the rivers (negative: in the water), worked out by the test itself from
    // each river (999 when no river is near)
    float RvWater(Vector3 p, out River river, out Vector3 nearest)
    {
        float best = 999f;
        river = null;
        nearest = p;
        foreach (var r in rvAll)
        {
            if (r == null) continue;
            float d = r.DistanceToWater(p, out Vector3 at);
            if (d < best) { best = d; river = r; nearest = at; }
        }
        return best;
    }

    float RvWater(Vector3 p) => RvWater(p, out _, out _);

    // the water of another river (or the old river's lake) here
    bool RvOtherWater(River r, Vector3 p, float margin)
    {
        foreach (var o in rvAll) if (o != null && o != r && o.DistanceToWater(p) < margin) return true;
        return RvNearLake(p, margin);
    }

    bool RvNearLake(Vector3 p, float margin) => rvOld != null && rvOld.lakeRadius > 0f && Flat(p - rvOld.lakeCentre) < rvOld.lakeRadius + margin;

    // the edge distance from anywhere (the whole middle line looked at, by the test itself) and the nearest middle point
    static float RvEdgeAnywhere(River r, Vector3 p, out Vector3 at)
    {
        at = p;
        float best = float.MaxValue;
        var pts = r.points;
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector3 a = pts[i], b = pts[i + 1];
            Vector2 ab = new Vector2(b.x - a.x, b.z - a.z), ap = new Vector2(p.x - a.x, p.z - a.z);
            float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
            float d = (ap - ab * t).magnitude - Mathf.Lerp(RvHW(r, i), RvHW(r, i + 1), t);
            if (d < best) { best = d; at = Vector3.Lerp(a, b, t); }
        }
        return best;
    }

    // something solid here (a rock, scenery, a building) that a walking player would bump into
    static bool RvSolid(Vector3 p, float radius)
    {
        Vector3 c = new Vector3(p.x, Forest.GroundY(p) + 1.1f, p.z);
        foreach (var col in Physics.OverlapSphere(c, radius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (col is TerrainCollider || col is CharacterController) continue;
            if (col.GetComponentInParent<RabbitLife>() != null || col.GetComponentInParent<PlayerMovement>() != null) continue;
            if (col.GetComponentInParent<InteractableObject>() != null) continue; // (a plant's hand, a branch)
            return true;
        }
        return false;
    }

    // a place on land beside river r, 'metres' from its water's edge, at fraction f along it
    Vector3 RvBeside(River r, float f, float metres)
    {
        int i = RvIndex(r, f);
        Vector3 p = r.points[i], side = RvSide(r, i);
        float hw = RvHW(r, i);
        foreach (float s in new[] { 1f, -1f })
        {
            Vector3 q = p + side * s * (hw + metres);
            if (RvShore(q) < -5f && !RvOtherWater(r, q, 1f) && Forest.GroundY(q) > RvSea + 0.8f) return q;
        }
        return p + side * (hw + metres);
    }

    // ---------------- R0 the run ---------------- //

    IEnumerator RiversRunSetup()
    {
        // the player can't die meanwhile (the admin unlocked for this run, locked again at the end)
        SetAdmin(true);
        GameSettings.Immortal = true;
        runImmortal = GameSettings.Immortal;
        riverStartPos = player.position;
        riverStartYaw = player.eulerAngles.y;
        riverStartSet = true;
        // clear weather for the pictures (no rain puddles either: their water is not the river's)
        var weather = WeatherSystem.Instance;
        if (weather != null && weather.autoWeather)
        {
            weather.SetTestWeather(WeatherSystem.WeatherType.Clear);
            weather.SnapNow();
            riverWeather = weather;
        }
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        FindRivers();
        Note("Immortal for the run " + runImmortal + ", difficulty " + GameSettings.Difficulty + " (rabbits run from " + GameSettings.RabbitShyRadius
            + " m), weather " + (riverWeather != null ? "clear for the run (automatic again at the end)" : "left as it was (not automatic)")
            + ", " + rvAll.Count + " rivers (" + rvNew.Count + " without a lake)");
        yield break;
    }

    // ---------------- R1 the rivers ---------------- //

    IEnumerator RiversListCheck()
    {
        FindRivers();
        int scene = 0;
        foreach (var r in FindObjectsOfType<River>()) if (r != null && r.isActiveAndEnabled) scene++;
        int listed = River.All.Count;
        var names = new List<string>();
        foreach (var r in rvNew) names.Add(RvName(r));
        Check("R1.1 at least 6 rivers: the old one with the lake + 5 new ones", rvOld != null && rvNew.Count >= 5,
            rvAll.Count + " rivers in the scene, the old one " + (rvOld != null ? "found (lake radius " + rvOld.lakeRadius.ToString("0") + ")" : "MISSING")
            + ", " + rvNew.Count + " without a lake: " + string.Join(", ", names));
        Check("R1.2 River.All lists every river of the scene, River.Instance is the old river with the lake",
            listed == scene && River.Instance != null && River.Instance == rvOld,
            "River.All " + listed + ", rivers enabled in the scene " + scene + ", River.Instance " + RvName(River.Instance));
        foreach (var r in rvNew)
        {
            int n = r.points.Count;
            var filter = r.GetComponentInChildren<MeshFilter>();
            var renderer = r.GetComponentInChildren<MeshRenderer>();
            int verts = filter != null && filter.sharedMesh != null ? filter.sharedMesh.vertexCount : 0;
            bool drawn = renderer != null && renderer.enabled && renderer.gameObject.activeInHierarchy && renderer.sharedMaterial != null;
            bool covers = false;
            if (renderer != null && n > 0)
            {
                var b = renderer.bounds;
                b.Expand(new Vector3(12f, 1000f, 12f));
                covers = b.Contains(r.points[0]) && b.Contains(r.points[n / 2]) && b.Contains(r.points[n - 1]);
            }
            float length = 0f, wMin = float.MaxValue, wMax = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) length += Flat(r.points[i] - r.points[i - 1]);
                wMin = Mathf.Min(wMin, RvHW(r, i) * 2f);
                wMax = Mathf.Max(wMax, RvHW(r, i) * 2f);
            }
            Check("R1.3 " + RvName(r) + ": a name, at least 60 points, a water mesh drawn along it",
                !string.IsNullOrEmpty(r.riverName) && n >= 60 && verts > 0 && drawn && covers,
                "object '" + r.name + "', name '" + r.riverName + "', " + n + " points (" + r.halfWidths.Count + " widths), " + length.ToString("0") + " m long, "
                + (n > 0 ? wMin.ToString("0.0") + "-" + wMax.ToString("0.0") + " m wide, " : "") + "mesh " + (filter != null && filter.sharedMesh != null ? "'" + filter.sharedMesh.name + "' " + verts + " vertices" : "none")
                + ", renderer " + (renderer == null ? "none" : "drawn " + drawn + ", material " + (renderer.sharedMaterial != null ? "'" + renderer.sharedMaterial.name + "'" : "none"))
                + ", its bounds over the spring, the middle and the mouth " + covers);
        }
        yield break;
    }

    // ---------------- R2 from the mountains to the sea ---------------- //

    IEnumerator RiversFlowCheck()
    {
        if (rvNew.Count == 0) { Skip("R2 the new rivers flow down to the sea", "no new river found (R1)"); yield break; }
        float sea = RvSea;
        foreach (var r in rvNew)
        {
            int n = r.points.Count;
            if (n < 2) { Fail("R2 " + RvName(r), "only " + n + " points"); continue; }
            Vector3 src = r.points[0], end = r.points[n - 1];
            float rise = float.MinValue;
            int at = 0, ups = 0;
            for (int i = 0; i + 1 < n; i++)
            {
                float d = r.points[i + 1].y - r.points[i].y;
                if (d > 0.05f) ups++;
                if (d > rise) { rise = d; at = i; }
            }
            Check("R2.1 " + RvName(r) + " springs on a mountain (its first point 25 m or more above the sea)", src.y - sea >= 25f,
                "spring at " + src.ToString("F1") + ", " + (src.y - sea).ToString("0.0") + " m above the sea (" + sea.ToString("0.00") + "), the ground there " + Forest.GroundY(src).ToString("0.0"));
            Check("R2.2 " + RvName(r) + ": its water never goes up along the river (0.05 m allowed)", rise <= 0.05f,
                "the biggest rise " + rise.ToString("0.00") + " m (points " + at + "-" + (at + 1) + " at " + r.points[at].ToString("F0") + "), " + ups + " rises over 0.05 m; it drops "
                + (src.y - end.y).ToString("0.0") + " m from the spring to the mouth");
            float shore = RvShore(end);
            string lastOnes = "";
            for (int i = Mathf.Max(0, n - 3); i < n; i++) lastOnes += (lastOnes.Length > 0 ? ", " : "") + (r.points[i].y - sea).ToString("0.00") + " / " + RvShore(r.points[i]).ToString("0");
            Check("R2.3 " + RvName(r) + " ends at the sea (the last point near sea level, at or over the shore)", Mathf.Abs(end.y - sea) <= 3f && shore > -8f,
                "mouth at " + end.ToString("F1") + ", " + (end.y - sea).ToString("0.00") + " m above the sea, " + shore.ToString("0.0") + " m from the shore (> -8 needed); the last 3 points (height over the sea / from the shore): " + lastOnes);
        }
        // pictures: each spring looking down its river, each mouth looking out at the sea
        foreach (var r in rvNew)
        {
            yield return RiverView(r, true);
            yield return RiverView(r, false);
        }
    }

    // the index this many metres from point 'from' along the river (dir +1 down the river, -1 up it)
    static int RvIndexAway(River r, int from, float metres, int dir)
    {
        float run = 0f;
        int i = from;
        while (i + dir >= 0 && i + dir < r.points.Count && run < metres)
        {
            run += Flat(r.points[i + dir] - r.points[i]);
            i += dir;
        }
        return i;
    }

    IEnumerator RiverView(River r, bool spring)
    {
        int n = r.points.Count;
        if (n < 4) yield break;
        Vector3 from, target;
        if (spring)
        {
            // a little behind and beside the spring, looking down the river
            Vector3 along = RvAlong(r, 0), side = RvSide(r, 0);
            from = r.points[0] - along * 7f + side * 4f;
            target = r.points[RvIndexAway(r, 0, 30f, 1)];
        }
        else
        {
            // beside the river 40 m before the sea, looking at its mouth
            int i = RvIndexAway(r, n - 1, 40f, -1);
            from = r.points[i] + RvSide(r, i) * (RvHW(r, i) + 6f);
            if (RvShore(from) > -3f) from = r.points[i] - RvSide(r, i) * (RvHW(r, i) + 6f);
            target = r.points[n - 1];
        }
        Teleport(from, YawTo(from, target), 15f);
        yield return Wait(0.9f);
        AimAt(target);
        yield return Wait(0.2f);
        yield return Shot("R2_" + RvFile(r) + (spring ? "_spring" : "_mouth"));
    }

    // ---------------- R3 the water in its bed ---------------- //

    IEnumerator RiversBedCheck()
    {
        if (rvAll.Count == 0) { Skip("R3 the water in its bed", "no river found"); yield break; }
        var list = new List<River>(rvNew);
        if (rvOld != null) list.Add(rvOld);
        foreach (var r in list)
        {
            int n = r.points.Count;
            if (n < 4) { Fail("R3 " + RvName(r), "only " + n + " points"); continue; }
            float worstMid = float.MaxValue, worstBank = float.MaxValue, sumMid = 0f;
            string worstMidAt = "", worstBankAt = "";
            int samples = 0, badMid = 0, badBank = 0, banksSkipped = 0;
            var bad = new List<(float how, string what)>();
            for (int k = 0; k < 20; k++)
            {
                int i = Mathf.RoundToInt(Mathf.Lerp(1f, n - 2, k / 19f));
                Vector3 p = r.points[i];
                float hw = RvHW(r, i);
                Vector3 side = RvSide(r, i);
                if (RvNearLake(p, 8f)) continue; // (the old river's end in its lake)
                samples++;
                // the bed under the middle: lower than the water's surface (> 0)
                float mid = p.y - Forest.GroundY(p);
                sumMid += mid;
                if (mid < worstMid) { worstMid = mid; worstMidAt = "point " + i + " " + p.ToString("F0"); }
                if (mid <= 0.02f) { badMid++; bad.Add((mid, "point " + i + " " + p.ToString("F0") + ": the bed " + (-mid).ToString("0.00") + " m ABOVE the water")); }
                // 2 m outside the water's edge on both sides: the ground not lower than the water (>= 0)
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 q = p + side * s * (hw + 2f);
                    // (not at the sea, the lake, another river, or this river's own water again at a tight bend)
                    if (RvShore(q) > -12f || RvNearLake(q, 6f) || RvOtherWater(r, q, 0f) || r.DistanceToWater(q) < 1f) { banksSkipped++; continue; }
                    float bank = Forest.GroundY(q) - p.y;
                    if (bank < worstBank) { worstBank = bank; worstBankAt = "point " + i + " " + (s < 0 ? "left" : "right") + " " + q.ToString("F0"); }
                    if (bank < -0.15f) { badBank++; bad.Add((bank, "point " + i + " " + (s < 0 ? "left" : "right") + " bank " + q.ToString("F0") + ": the ground " + (-bank).ToString("0.00") + " m UNDER the water")); }
                }
            }
            bad.Sort((a, b) => a.how.CompareTo(b.how));
            var worst = new List<string>();
            for (int j = 0; j < bad.Count && j < 4; j++) worst.Add(bad[j].what);
            Check("R3 " + RvName(r) + ": the water lies in its bed (" + samples + " spots: the bed under the middle lower than the water, the ground 2 m outside its edges not lower)",
                samples > 0 && badMid == 0 && badBank == 0,
                "the bed under the middle " + (samples > 0 ? (sumMid / samples).ToString("0.00") : "-") + " m deep on average, the shallowest " + worstMid.ToString("0.00") + " m (" + worstMidAt + "); the banks 2 m out: the lowest "
                + (worstBank < float.MaxValue ? worstBank.ToString("0.00") + " m over the water (" + worstBankAt + ")" : "-") + "; " + badMid + " spots with the bed at or above the water, " + badBank
                + " banks lower than the water" + (worst.Count > 0 ? "; the worst: " + string.Join("; ", worst) : "") + (banksSkipped > 0 ? " (" + banksSkipped + " banks at the sea, the lake or another river not looked at)" : ""));
        }
        yield break;
    }

    // ---------------- R4 no trees or plants in the water ---------------- //

    IEnumerator RiversTreesCheck()
    {
        var forest = Forest.Instance;
        if (forest == null) Fail("R4.1 no tree in or at the water", "no Forest");
        else
        {
            int total = 0, near = 0, apiNear = 0, gameNear = 0;
            var perRiver = new Dictionary<River, int>();
            var examples = new List<string>();
            foreach (var t in forest.Trees)
            {
                if (t == null || t.data == null) continue;
                total++;
                Vector3 p = t.data.position;
                float d = RvWater(p, out River r, out Vector3 at);
                if (d < 1.5f)
                {
                    near++;
                    perRiver[r] = (perRiver.TryGetValue(r, out int c) ? c : 0) + 1;
                    if (examples.Count < 6) examples.Add((t.Species != null ? t.Species.name : "tree") + " " + t.data.height.ToString("0") + " m at " + p.ToString("F0") + " " + d.ToString("0.0") + " m from the water of " + RvName(r));
                }
                if (River.NearestWater(p, out _, out _) < 1.5f) apiNear++;
                if (River.IsWater(p, 1.5f)) gameNear++;
            }
            var counts = new List<string>();
            foreach (var pair in perRiver) counts.Add(RvName(pair.Key) + " " + pair.Value);
            Check("R4.1 no tree in or at the water (within its half width + 1.5 m of any river's middle line)", near == 0 && apiNear == 0 && gameNear == 0,
                total + " trees looked at: " + near + " in or at the water" + (counts.Count > 0 ? " (" + string.Join(", ", counts) + ")" : "")
                + "; River.NearestWater says " + apiNear + ", River.IsWater(p, 1.5) says " + gameNear + (examples.Count > 0 ? "; e.g. " + string.Join("; ", examples) : ""));
        }

        // the scene's plants (Vegetation draws them; one with an item can be picked)
        int plants = 0, pickIn = 0, otherIn = 0;
        var pickExamples = new List<string>();
        var otherExamples = new List<string>();
        foreach (var plant in FindObjectsOfType<ScenePlant>())
        {
            if (plant == null) continue;
            plants++;
            Vector3 p = plant.transform.position;
            float d = RvWater(p, out River r, out _);
            if (d >= 0f) continue;
            bool pickable = !string.IsNullOrEmpty(plant.item);
            if (pickable) pickIn++; else otherIn++;
            var list = pickable ? pickExamples : otherExamples;
            if (list.Count < 6) list.Add((pickable ? plant.item : plant.name) + " at " + p.ToString("F0") + " " + (-d).ToString("0.0") + " m inside the water of " + RvName(r));
        }
        Check("R4.2 no pickable plant in a river's water", pickIn == 0, plants + " scene plants looked at, " + pickIn + " pickable ones in the water" + (pickExamples.Count > 0 ? ": " + string.Join("; ", pickExamples) : ""));
        Check("R4.3 no other plant (bush, grass, flowers) in a river's water", otherIn == 0, otherIn + " in the water" + (otherExamples.Count > 0 ? ": " + string.Join("; ", otherExamples) : ""));
        yield break;
    }

    // ---------------- R8 the map ---------------- //

    // the pixels of the map's picture (copied through the GPU when it can't be read)
    static Color[] RvReadTexture(Texture tex, out int w, out int h, out string how)
    {
        w = h = 0;
        how = "";
        if (tex == null) { how = "no picture"; return null; }
        w = tex.width;
        h = tex.height;
        if (tex is Texture2D t2 && t2.isReadable)
        {
            try { how = "read directly"; return t2.GetPixels(); }
            catch (Exception) { }
        }
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        Graphics.Blit(tex, rt);
        RenderTexture.active = rt;
        var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        var px = copy.GetPixels();
        Destroy(copy);
        how = "copied through the GPU";
        return px;
    }

    static float RvBlue(Color c) => c.b - (c.r + c.g) * 0.5f;

    // the bluest pixel of the map within rad pixels of this place
    static float RvMapBlue(Color[] px, int w, int h, Vector3 origin, Vector3 size, Vector3 p, int rad)
    {
        int cx = Mathf.FloorToInt((p.x - origin.x) / size.x * w), cy = Mathf.FloorToInt((p.z - origin.z) / size.z * h);
        float best = -9f;
        for (int y = cy - rad; y <= cy + rad; y++)
            for (int x = cx - rad; x <= cx + rad; x++)
                if (x >= 0 && y >= 0 && x < w && y < h) best = Mathf.Max(best, RvBlue(px[y * w + x]));
        return best;
    }

    IEnumerator RiversMapCheck()
    {
        if (MapScreen.IsOpen) MapScreen.Toggle();
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        // (by the east river: the player's arrow near one of the new rivers)
        var east = rvNew.Find(r => r.riverName == "East river");
        if (east != null) { Vector3 at = RvBeside(east, 0.5f, 10f); Teleport(at, YawTo(at, east.points[RvIndex(east, 0.5f)]), 10f); yield return Wait(0.5f); }
        // the Map button / N calls this
        MapScreen.Toggle();
        yield return Wait(1f);
        Check("R8.1 the map opens (Map button / N)", MapScreen.IsOpen, "IsOpen " + MapScreen.IsOpen);
        yield return Shot("R8_map");
        Texture tex = null;
        var raw = MapScreen.Instance != null ? GetField<RawImage>(MapScreen.Instance, "map") : null;
        if (raw != null) tex = raw.texture;
        Color[] px = null;
        int w = 0, h = 0;
        string how = "";
        try { px = RvReadTexture(tex, out w, out h, out how); }
        catch (Exception e) { how = "could not be read: " + e.Message; }
        if (MapScreen.IsOpen) MapScreen.Toggle();
        yield return Wait(0.3f);
        var terrain = Terrain.activeTerrain;
        if (px == null || terrain == null) { Skip("R8.2 the map draws the rivers", "the map's picture " + how); yield break; }
        Vector3 origin = terrain.transform.position, size = terrain.terrainData.size;
        float metresPerPixel = size.x / w;
        Note("the map's picture '" + tex.name + "' " + w + " x " + h + " (" + metresPerPixel.ToString("0.0") + " m a pixel), " + how);
        foreach (var r in rvAll)
        {
            int n = r.points.Count, hits = 0, total = 0, sideN = 0;
            float sideBlue = 0f, blueSum = 0f;
            for (int k = 0; k < 30 && n > 1; k++)
            {
                int i = Mathf.RoundToInt(Mathf.Lerp(0f, n - 1, (k + 0.5f) / 30f));
                Vector3 p = r.points[i];
                if (RvShore(p) > -25f || RvNearLake(p, 10f)) continue; // (near the sea and in the lake the map is blue anyway)
                total++;
                int rad = Mathf.Clamp(Mathf.RoundToInt(RvHW(r, i) / metresPerPixel), 1, 4);
                float here = RvMapBlue(px, w, h, origin, size, p, rad);
                blueSum += here;
                // the land 40 m beside it
                float land = 0f;
                int landN = 0;
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 q = p + RvSide(r, i) * s * (RvHW(r, i) + 40f);
                    if (RvWater(q) < 5f || RvShore(q) > -25f || RvNearLake(q, 10f)) continue;
                    land += RvMapBlue(px, w, h, origin, size, q, 0);
                    landN++;
                }
                if (landN > 0) { sideBlue += land / landN; sideN++; }
                if (here > 0.04f || (landN > 0 && here > land / landN + 0.12f)) hits++;
            }
            float frac = total > 0 ? hits / (float)total : 0f;
            Check("R8.2 the map draws " + RvName(r) + " (bluer along its line than the land beside it)", total > 0 && frac >= 0.6f,
                hits + " of " + total + " spots along it blue on the map (" + (frac * 100f).ToString("0") + " %), their blueness " + (total > 0 ? (blueSum / total).ToString("0.00") : "-")
                + ", the land 40 m beside it " + (sideN > 0 ? (sideBlue / sideN).ToString("0.00") : "-") + " (blueness: blue minus the mean of red and green)");
        }
    }

    // ---------------- R9 the level-2 map goal ---------------- //

    IEnumerator RiversGoalCheck()
    {
        var data = Data;
        if (story == null || data == null || rvAll.Count == 0) { Fail("R9 the level-2 map goal", "no story data or no river"); yield break; }
        var spots = new List<(string what, Vector3 at)>();
        spots.Add(("the story's beach", S + F * 6f));
        if (rvOld != null) spots.Add(("20 m from the old river", RvBeside(rvOld, 0.45f, 20f)));
        foreach (var r in rvNew) spots.Add(("8 m from " + RvName(r), RvBeside(r, 0.4f, 8f)));
        var west = rvNew.Find(r => r.riverName == "West river");
        if (west != null) spots.Add(("70 m from " + RvName(west), RvBeside(west, 0.6f, 70f)));
        int n = 0;
        foreach (var (what, at) in spots)
        {
            n++;
            Teleport(at, 0f, 10f);
            yield return Wait(0.25f);
            var state = PlayerState.Instance;
            Vector3 pp = state != null && state.playerBody != null ? state.playerBody.transform.position : player.position;
            bool got;
            Vector3 goal;
            string label;
            // (level 2 only for this one call, in this frame: the story is held still)
            riverLevel = data.level;
            riverLevelSet = true;
            data.level = 2;
            try { got = story.MapGoal(out goal, out label); }
            finally { data.level = riverLevel; riverLevelSet = false; }
            // the nearest river of all from the player, by the test itself (the edge of the water, the whole lines)
            River best = null;
            Vector3 bestAt = pp;
            float bestEdge = float.MaxValue;
            foreach (var r in rvAll)
            {
                float d = RvEdgeAnywhere(r, pp, out Vector3 a);
                if (d < bestEdge) { bestEdge = d; best = r; bestAt = a; }
            }
            // which river the goal lies on, and its nearest point to the player
            River goalRiver = null;
            float goalIn = float.MaxValue;
            foreach (var r in rvAll)
            {
                float d = RvEdgeAnywhere(r, goal, out _);
                if (d < goalIn) { goalIn = d; goalRiver = r; }
            }
            Vector3 goalRiverAt = pp;
            float goalRiverEdge = goalRiver != null ? RvEdgeAnywhere(goalRiver, pp, out goalRiverAt) : float.MaxValue;
            float near = River.NearestWater(pp, out River nearRiver, out Vector3 nearAt);
            float any = River.NearestWaterAnywhere(pp, out River anyRiver, out Vector3 anyAt);
            bool onRiver = goalRiver != null && goalIn < 0.5f;
            bool nearest = goalRiverEdge <= bestEdge + 0.5f && Flat(goal - goalRiverAt) < 1.5f;
            bool apiAgrees = (near >= 999f || Flat(nearAt - goal) < 1.5f) && anyRiver != null && Flat(anyAt - goal) < 1.5f;
            Check("R9." + n + " level-2 map goal seen from " + what + ": the nearest river of all", got && onRiver && nearest && apiAgrees,
                "the player at " + pp.ToString("F0") + "; the goal " + (got ? goal.ToString("F0") + " '" + label + "' on " + RvName(goalRiver) + " (" + Flat(goal - pp).ToString("0") + " m away)" : "NONE")
                + "; the nearest river by the test: " + RvName(best) + ", its water " + bestEdge.ToString("0.0") + " m away (its middle at " + bestAt.ToString("F0") + ")"
                + "; River.NearestWater " + (near >= 999f ? "999 (none within its reach)" : near.ToString("0.0") + " m, " + RvName(nearRiver) + " at " + nearAt.ToString("F0"))
                + "; River.NearestWaterAnywhere " + any.ToString("0.0") + " m, " + RvName(anyRiver) + " at " + anyAt.ToString("F0"));
        }
    }

    // ---------------- R5 drinking ---------------- //

    // a place to stand at the edge of the water near fraction f of the river: on land (not lower than the water, not up
    // on a cliff), not too steep, not at the sea or another river, nothing solid there
    bool RvDrinkSpot(River r, float f, out int index, out Vector3 bank, out Vector3 water)
    {
        int n = r.points.Count, c = RvIndex(r, f);
        Vector3 fallback = Vector3.zero;
        int fallbackIndex = -1;
        for (int step = 0; step < 40; step++)
        {
            int i = c + (step % 2 == 0 ? step / 2 : -(step + 1) / 2) * 2;
            if (i < 1 || i > n - 2) continue;
            Vector3 p = r.points[i], side = RvSide(r, i);
            float hw = RvHW(r, i);
            foreach (float s in new[] { 1f, -1f })
            {
                Vector3 q = p + side * s * (hw + 1.2f);
                if (RvShore(q) > -6f || RvOtherWater(r, q, 1f)) continue;
                if (fallbackIndex < 0) { fallback = q; fallbackIndex = i; }
                float g = Forest.GroundY(q);
                if (g < p.y - 0.3f || g > p.y + 4f || RvSteep(q) > 35f || RvSolid(q, 0.5f)) continue;
                index = i;
                bank = q;
                water = p;
                return true;
            }
        }
        index = fallbackIndex >= 0 ? fallbackIndex : c;
        bank = fallbackIndex >= 0 ? fallback : r.points[c] + RvSide(r, c) * (RvHW(r, c) + 1.2f);
        water = r.points[index];
        return false;
    }

    IEnumerator RiversDrinkCheck()
    {
        if (rvNew.Count == 0) { Skip("R5 drinking at the new rivers", "no new river found (R1)"); yield break; }
        var state = PlayerState.Instance;
        var fresh = FreshWater.Instance;
        var selection = SelectionManager.Instance;
        if (state == null || fresh == null || selection == null) { Fail("R5 drinking", "no PlayerState / FreshWater / SelectionManager"); yield break; }
        EmptyHands();
        // (the admin's Immortal keeps the bars full: off while drinking, in the day)
        GameSettings.Immortal = false;
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        var text = GetField<Text>(selection, "interaction_text");
        string drinkText = Lang.T("Drink water", "Bea apă");
        foreach (var r in rvNew)
        {
            foreach (var (where, f) in new[] { ("upper", 0.15f), ("middle", 0.5f), ("near the sea", 0.88f) })
            {
                bool good = RvDrinkSpot(r, f, out int i, out Vector3 bank, out Vector3 water);
                Teleport(bank, YawTo(bank, water), 45f);
                yield return Wait(0.7f);
                state.setHydration(state.maxHydrationPercent * 0.4f);
                float w0 = state.currentHydrationPercent;
                AimAt(water);
                yield return Wait(0.35f);
                Vector3 body = state.playerBody != null ? state.playerBody.transform.position : player.position;
                var source = fresh.At(body);
                string offer = text != null && selection.interaction_Info_UI != null && selection.interaction_Info_UI.activeSelf ? text.text : "";
                yield return Tap(GameInput.TouchAction.Primary);
                yield return Wait(0.3f);
                float w1 = state.currentHydrationPercent;
                Check("R5 " + RvName(r) + " " + where + ": the game offers drinking at the edge and the water bar rises (look down + press)",
                    source == FreshWater.Source.River && offer == drinkText && w1 > w0 + 1f,
                    "standing " + RvWater(body).ToString("0.0") + " m from the water at " + body.ToString("F0") + " (point " + i + " of " + r.points.Count
                    + (good ? "" : ", no good bank found: the first one on land") + "), fresh water here: " + source + ", the crosshair says '" + offer + "', water "
                    + w0.ToString("0") + " -> " + w1.ToString("0") + " of " + state.maxHydrationPercent.ToString("0"));
                if (where == "middle") yield return Shot("R5_drink_" + RvFile(r));
            }
        }
        GameSettings.Immortal = runImmortal;
    }

    // ---------------- R6 crossing on foot ---------------- //

    class RvCrossing
    {
        public int index;
        public Vector3 a, b, mid;
        public float hw, bankA, bankB, depth;
    }

    readonly Dictionary<River, List<RvCrossing>> rvCrossings = new Dictionary<River, List<RvCrossing>>();

    // two flat places to cross (one in 18-50 % of the river, one in 50-86 %): land 8 m from the water on both sides,
    // not much higher than the water, away from the sea and the other rivers, no tree or rock on the way
    List<RvCrossing> RvFindCrossings(River r)
    {
        if (rvCrossings.TryGetValue(r, out var known)) return known;
        var list = new List<RvCrossing>();
        var trees = new List<ForestTree>();
        foreach (var (lo, hi) in new[] { (0.18f, 0.5f), (0.5f, 0.86f) })
        {
            RvCrossing best = null;
            float bestScore = float.MaxValue;
            int i1 = Mathf.Min(r.points.Count - 2, RvIndex(r, hi));
            for (int i = Mathf.Max(1, RvIndex(r, lo)); i <= i1; i += 2)
            {
                if (!RvCrossingAt(r, i, trees, out var c, out float score)) continue;
                if (score < bestScore) { bestScore = score; best = c; }
            }
            if (best != null) list.Add(best);
        }
        rvCrossings[r] = list;
        return list;
    }

    bool RvCrossingAt(River r, int i, List<ForestTree> trees, out RvCrossing c, out float score)
    {
        c = null;
        score = 0f;
        Vector3 p = r.points[i], side = RvSide(r, i);
        float hw = RvHW(r, i);
        Vector3 a = p - side * (hw + 8f), b = p + side * (hw + 8f);
        if (RvShore(a) > -20f || RvShore(b) > -20f || RvShore(p) > -20f) return false;
        float ga = Forest.GroundY(a), gb = Forest.GroundY(b);
        if (ga < p.y - 0.1f || gb < p.y - 0.1f || Mathf.Max(ga, gb) - p.y > 6f) return false;
        if (RvSteep(a) > 22f || RvSteep(b) > 22f) return false;
        for (int s = 0; s <= 10; s++)
        {
            Vector3 q = Vector3.Lerp(a, b, s / 10f);
            if (RvOtherWater(r, q, 2f)) return false;
            trees.Clear();
            if (Forest.Instance != null) Forest.Instance.TreesNear(q, 4f, trees);
            foreach (var t in trees) if (t != null && t.data != null && Flat(t.data.position - q) < t.TrunkRadius + 1.3f) return false;
            if (RvSolid(q, 0.6f)) return false;
        }
        c = new RvCrossing { index = i, a = a, b = b, mid = p, hw = hw, bankA = ga - p.y, bankB = gb - p.y, depth = p.y - Forest.GroundY(p) };
        score = Mathf.Abs(ga - gb) + 0.5f * (c.bankA + c.bankB) + 0.05f * (RvSteep(a) + RvSteep(b));
        return true;
    }

    // The player walks a route with the joystick (__WalkDriver forward, the view turned to the next point every frame,
    // as a player steers), until near its end, or no progress for 3.5 s, or maxSeconds. A picture when passing shotAt.
    bool walkReached, walkShotTaken, walkSwam;
    float walkSeconds, walkLowest, walkInWater;
    Vector3 walkStoppedAt;

    IEnumerator RvWalk(List<Vector3> route, float maxSeconds, Vector3 shotAt, string shotName, float pitch = 0f)
    {
        walkReached = walkShotTaken = walkSwam = false;
        walkSeconds = walkInWater = 0f;
        walkLowest = float.MaxValue;
        walkStoppedAt = player.position;
        if (route.Count < 2) yield break;
        Vector3 end = route[route.Count - 1];
        int idx = 1, lastIdx = 1;
        float t = 0f, lastProgress = 0f, bestGap = float.MaxValue;
        __WalkDriver.On = true;
        __WalkDriver.Move = new Vector2(0f, 1f);
        while (t < maxSeconds)
        {
            Vector3 at = player.position;
            while (idx < route.Count - 1 && Flat(route[idx] - at) < 2f) idx++;
            look.SetLook(YawTo(at, route[idx]), pitch);
            float gap = Flat(route[idx] - at);
            if (idx > lastIdx) { lastIdx = idx; bestGap = gap; lastProgress = t; }
            else if (gap < bestGap - 0.4f) { bestGap = gap; lastProgress = t; }
            walkLowest = Mathf.Min(walkLowest, Forest.GroundY(at));
            if (RvWater(at) < 0f) walkInWater += Time.unscaledDeltaTime;
            if (PlayerWater.Instance != null && PlayerWater.Instance.IsSwimming) walkSwam = true;
            if (idx >= route.Count - 1 && Flat(end - at) < 1.6f) { walkReached = true; break; }
            if (t - lastProgress > 3.5f) break;
            if (shotName != null && !walkShotTaken && Flat(shotAt - at) < 1.5f) { walkShotTaken = true; yield return Shot(shotName); }
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        __WalkDriver.Move = Vector2.zero;
        __WalkDriver.On = false;
        GameInput.touchMove = Vector2.zero;
        walkSeconds = t;
        walkStoppedAt = player.position;
    }

    string RvWalkInfo()
    {
        float d = RvWater(walkStoppedAt);
        return (walkReached ? "reached the far side in " + walkSeconds.ToString("0.0") + " s"
                : "NOT reached: stopped at " + walkStoppedAt.ToString("F1") + " after " + walkSeconds.ToString("0.0") + " s" + (d < 1f ? " (STUCK in the river's bed)" : ""))
            + ", the lowest ground walked on " + walkLowest.ToString("0.0") + ", " + walkInWater.ToString("0.0") + " s in the water" + (walkSwam ? ", swam" : "");
    }

    IEnumerator RiversCrossCheck()
    {
        if (rvNew.Count == 0) { Skip("R6 crossing the new rivers", "no new river found (R1)"); yield break; }
        EmptyHands();
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        bool shot = false;
        foreach (var r in rvNew)
        {
            var spots = RvFindCrossings(r);
            if (spots.Count == 0) { Fail("R6 " + RvName(r) + ": crossing on foot", "no flat place with land on both sides found (18-86 % along it)"); continue; }
            if (spots.Count < 2) Note(RvName(r) + ": only one flat place to cross found");
            int k = 0;
            foreach (var c in spots)
            {
                k++;
                Teleport(c.a, YawTo(c.a, c.b), 0f);
                yield return Wait(0.6f);
                yield return RvWalk(new List<Vector3> { c.a, c.b }, 12f, c.mid, shot ? null : "R6_crossing_" + RvFile(r), shot ? 0f : 12f);
                if (walkShotTaken) shot = true;
                Check("R6." + k + " " + RvName(r) + ": walked across with the joystick, from 8 m beside the water to 8 m on the other side, within 12 s", walkReached,
                    RvWalkInfo() + "; at point " + c.index + " of " + r.points.Count + " " + c.mid.ToString("F0") + ": the water " + (c.hw * 2f).ToString("0.0") + " m wide, "
                    + c.depth.ToString("0.0") + " m deep in the middle, the banks " + c.bankA.ToString("0.0") + " / " + c.bankB.ToString("0.0") + " m over the water");
            }
        }
    }

    // ---------------- R10 Brother Ioan's paths ---------------- //

    IEnumerator RiversPathsCheck()
    {
        var data = IslandPaths.Data;
        if (data == null || data.paths == null || data.paths.Count == 0) { Skip("R10 Brother Ioan's paths across the rivers", "no IslandPaths data"); yield break; }
        if (rvNew.Count == 0) { Skip("R10 Brother Ioan's paths across the rivers", "no new river found (R1)"); yield break; }
        var found = new List<(string what, List<Vector3> route, Vector3 mid, float minD, River r)>();
        int endsAtRiver = 0;
        void Scan(List<IslandPaths.PathLine> lines, string kind)
        {
            if (lines == null) return;
            for (int li = 0; li < lines.Count; li++)
            {
                var pts = lines[li] != null ? lines[li].points : null;
                if (pts == null || pts.Count < 2) continue;
                int n = pts.Count;
                foreach (var r in rvNew)
                {
                    int i = 0;
                    while (i < n)
                    {
                        float d = r.DistanceToWater(pts[i]);
                        if (d >= 6f) { i++; continue; }
                        // the points of the path within 6 m of this river's water (a gap of 3 points allowed)
                        int m = i, e = i, gap = 0;
                        float minD = d;
                        for (int j = i + 1; j < n; j++)
                        {
                            float dj = r.DistanceToWater(pts[j]);
                            if (dj < 6f) { e = j; gap = 0; if (dj < minD) { minD = dj; m = j; } }
                            else if (++gap > 3) break;
                        }
                        i = e + 4;
                        // the walk: from about 12 m before its nearest point to about 12 m after it, out of all water
                        int a = m, b = m;
                        float back = 0f, ahead = 0f;
                        while (a > 0 && (back < 12f || RvWater(pts[a]) < 1.5f) && back < 40f) { back += Flat(pts[a] - pts[a - 1]); a--; }
                        while (b < n - 1 && (ahead < 12f || RvWater(pts[b]) < 1.5f) && ahead < 40f) { ahead += Flat(pts[b + 1] - pts[b]); b++; }
                        if (back < 5f || ahead < 5f) { endsAtRiver++; Note(kind + " " + li + " ends at " + RvName(r) + " (point " + m + " of " + n + "): not walked"); continue; }
                        var route = new List<Vector3> { pts[a] };
                        for (int j = a + 1; j <= b; j++) if (j == b || Flat(pts[j] - route[route.Count - 1]) >= 1.5f) route.Add(pts[j]);
                        found.Add((kind + " " + li + " (points " + a + "-" + b + " of " + n + ")", route, pts[m], minD, r));
                    }
                }
            }
        }
        Scan(data.paths, "path");
        Scan(data.stairs, "steps");
        if (found.Count == 0) { Skip("R10 Brother Ioan's paths across the rivers", "no path point within 6 m of a new river's water" + (endsAtRiver > 0 ? " (" + endsAtRiver + " paths end at a river)" : "")); yield break; }
        // the ones that go through the water first; at most 8 walks
        found.Sort((x, y) => x.minD.CompareTo(y.minD));
        if (found.Count > 8) { Note(found.Count + " places where a path comes near a new river: the 8 nearest walked"); found.RemoveRange(8, found.Count - 8); }
        EmptyHands();
        if (IsDark()) DayNightSystem.Instance.TimeOfDay = 10f / 24f;
        bool shot = false;
        int k = 0;
        foreach (var f in found)
        {
            k++;
            var route = f.route;
            float length = 0f;
            for (int j = 1; j < route.Count; j++) length += Flat(route[j] - route[j - 1]);
            Teleport(route[0], YawTo(route[0], route[1]), 0f);
            yield return Wait(0.7f);
            float limit = Mathf.Clamp(length / 12f * 2.5f + 4f, 8f, 20f);
            yield return RvWalk(route, limit, f.mid, shot ? null : "R10_path_crossing_" + RvFile(f.r), shot ? 0f : 10f);
            if (walkShotTaken) shot = true;
            Check("R10." + k + " Brother Ioan's " + f.what + (f.minD < 0f ? " crosses " : " passes ") + RvName(f.r) + ": walked along it to the other side with the joystick", walkReached,
                RvWalkInfo() + " (" + length.ToString("0") + " m, " + limit.ToString("0") + " s allowed); the path comes " + f.minD.ToString("0.0") + " m to the water (negative: through it) at " + f.mid.ToString("F0"));
        }
    }

    // ---------------- R7 rabbits keep out of the water ---------------- //

    // a grown free rabbit let out here (RabbitPopulation's own Spawn), not mating meanwhile
    RabbitLife RvSpawnRabbit(Vector3 at, RabbitInfo.Sex sex)
    {
        var population = RabbitPopulation.Instance;
        var spawn = typeof(RabbitPopulation).GetMethod("Spawn", Any);
        if (population == null || spawn == null) return null;
        at.y = Buildings.GroundAt(at);
        var r = spawn.Invoke(population, new object[] { at, UnityEngine.Random.Range(0f, 360f), sex, RabbitPopulation.AdultHours + 20f }) as RabbitLife;
        if (r != null)
        {
            r.restHours = 500f;
            riverRabbits.Add(r);
        }
        return r;
    }

    IEnumerator RiversRabbitsCheck()
    {
        var population = RabbitPopulation.Instance;
        if (population == null || typeof(RabbitPopulation).GetMethod("Spawn", Any) == null) { Fail("R7 rabbits keep out of the water", "no RabbitPopulation (or its Spawn)"); yield break; }
        if (rvNew.Count == 0) { Skip("R7 rabbits keep out of the water", "no new river found (R1)"); yield break; }
        EmptyHands();
        var clock = DayNightSystem.Instance;
        float shy = GameSettings.RabbitShyRadius;
        int samples = 0, wetSamples = 0, spawned = 0, ran = 0;
        var wetRabbits = new HashSet<RabbitLife>();
        var wet = new List<string>();
        float closest = float.MaxValue;
        bool shot = false;
        foreach (var r in rvNew)
        {
            // a flat place by the river (where R6 crossed it, else its middle)
            var spots = RvFindCrossings(r);
            int i = spots.Count > 0 ? spots[0].index : RvIndex(r, 0.5f);
            Vector3 p = r.points[i], side = RvSide(r, i), along = RvAlong(r, i);
            float hw = RvHW(r, i);
            if (RvShore(p - side * (hw + 5.5f)) > -5f) side = -side;
            // the player 5.5 m from the water; 2 rabbits between the player and the water (they run from the player,
            // towards the river), 2 on the far side
            Vector3 stand = p - side * (hw + 5.5f);
            Teleport(stand, YawTo(stand, p), 25f);
            yield return Wait(0.3f);
            var mine = new List<RabbitLife>();
            foreach (var (s, o, sex) in new[] { (-1f, -2.5f, RabbitInfo.Sex.Male), (-1f, 2.5f, RabbitInfo.Sex.Female), (1f, -2.5f, RabbitInfo.Sex.Male), (1f, 2.5f, RabbitInfo.Sex.Female) })
            {
                Vector3 at = p + side * s * (hw + 1.8f) + along * o;
                if (RvWater(at) < 0.5f || RvShore(at) > -3f) continue;
                var rabbit = RvSpawnRabbit(at, sex);
                if (rabbit != null) { mine.Add(rabbit); spawned++; }
            }
            var runs0 = new Dictionary<RabbitLife, int>();
            foreach (var rabbit in mine) runs0[rabbit] = rabbit.ShyRuns;
            // the clock 20x faster for 4 s: every living rabbit near the player watched 4 times a second
            if (clock != null) clock.timeSpeed = 20f;
            for (float t = 0f; t < 4f; t += 0.25f)
            {
                yield return Wait(0.25f);
                foreach (var rabbit in population.Rabbits)
                {
                    if (rabbit == null || !rabbit.Alive || rabbit.caughtIn != null || !rabbit.gameObject.activeInHierarchy) continue;
                    Vector3 rp = rabbit.transform.position;
                    if (Flat(rp - player.position) > 60f) continue;
                    samples++;
                    float d = RvWater(rp, out River wr, out _);
                    if (mine.Contains(rabbit)) closest = Mathf.Min(closest, d);
                    if (d < 0f)
                    {
                        wetSamples++;
                        if (wetRabbits.Add(rabbit) && wet.Count < 6) wet.Add((mine.Contains(rabbit) ? "a test rabbit" : "a wild rabbit") + " at " + rp.ToString("F1") + " " + (-d).ToString("0.0") + " m inside " + RvName(wr));
                    }
                }
                if (!shot && t >= 1f && !IsDark()) { shot = true; AimAt(p); yield return Shot("R7_rabbits_" + RvFile(r)); }
            }
            if (clock != null) clock.timeSpeed = 1f;
            foreach (var rabbit in mine) if (rabbit != null && rabbit.ShyRuns > runs0[rabbit]) ran++;
            foreach (var rabbit in mine) if (rabbit != null) { riverRabbits.Remove(rabbit); rabbit.gameObject.SetActive(false); Destroy(rabbit.gameObject); }
            if (IsDark() && clock != null) clock.TimeOfDay = 10f / 24f;
        }
        Check("R7.1 near the new rivers no rabbit steps into the water (4 test rabbits at each, the player scaring them towards it, the clock x20, 20 s)",
            spawned > 0 && wetSamples == 0,
            spawned + " test rabbits let out 1.8 m from the water, " + ran + " of them ran from the player; " + samples + " looks at the rabbits near the player, "
            + wetSamples + " in the water (" + wetRabbits.Count + " rabbits)" + (wet.Count > 0 ? ": " + string.Join("; ", wet) : "")
            + (closest < float.MaxValue ? "; a test rabbit came " + closest.ToString("0.0") + " m to the water at the closest" : ""));
        // all the island's rabbits
        int alive = 0, inWater = 0;
        var island = new List<string>();
        foreach (var rabbit in population.Rabbits)
        {
            if (rabbit == null || !rabbit.Alive || rabbit.caughtIn != null || !rabbit.gameObject.activeInHierarchy) continue;
            alive++;
            float d = RvWater(rabbit.transform.position, out River wr, out _);
            if (d < 0f) { inWater++; if (island.Count < 6) island.Add(rabbit.transform.position.ToString("F0") + " in " + RvName(wr)); }
        }
        Check("R7.2 no living rabbit of the island stands in a river's water", alive > 0 && inWater == 0,
            alive + " living rabbits, " + inWater + " in a river's water" + (island.Count > 0 ? ": " + string.Join("; ", island) : ""));
    }
}
#endif
