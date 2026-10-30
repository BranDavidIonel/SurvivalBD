#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// TEMPORARY (Claude): runs the checks one after another, each in Play mode, then stops Play and puts the editor's
// view back. Writes into Temp/<folder>. Removed after the tests.
//  1 Probe2:   (edit mode) the fence items, Brother Ioan's paths and the stream bed, the map picture; then a look
//              around in Play (phone)
//  2 LevelTest: the 10 levels from a new game (phone)
//  3 Probe3a:  the screens on a PC 1920 x 1080 (Game view)
//  4 Probe3b:  the screens on a narrow PC 1280 x 1024
//  then back to the phone simulator
[InitializeOnLoad]
static class __ProbeDriver
{
    static readonly string[] Jobs = { "LevelTest12" };
    const string JobKey = "__ProbeDriver.job12", PhaseKey = "__ProbeDriver.jobphase12";
    const string AllDone = "Temp/__ProbeJobs12_done.txt";
    static int quiet;
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    static __ProbeDriver()
    {
        if (File.Exists(AllDone)) return;
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static int Job => SessionState.GetInt(JobKey, 0);
    static int Phase { get => SessionState.GetInt(PhaseKey, 0); set => SessionState.SetInt(PhaseKey, value); }
    static string Dir => "Temp/" + Jobs[Mathf.Min(Job, Jobs.Length - 1)];

    static void OnPlayMode(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.EnteredPlayMode || Phase != 1 || Job >= Jobs.Length) return;
        string job = Jobs[Job];
        if (job.StartsWith("LevelTest"))
        {
            __LevelTest.Folder = job;
            new GameObject("__LevelTest").AddComponent<__LevelTest>();
        }
        else if (job.StartsWith("Systems"))
        {
            // (found by name: the systems test is written by another agent)
            var type = Type.GetType("__SystemsTest, Assembly-CSharp");
            if (type == null) { File.WriteAllText(Dir + "/finished.txt", "no __SystemsTest"); Phase = 2; return; }
            type.GetField("Folder", Any)?.SetValue(null, job);
            new GameObject("__SystemsTest").AddComponent(type);
        }
        else
        {
            __Probe.Folder = job;
            __Probe.Mode = job == "Probe7" ? "paths" : job.EndsWith("Phone") ? "phone" : "pc";
            new GameObject("__Probe").AddComponent<__Probe>();
        }
        Phase = 2;
    }

    static bool Busy => EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating;

    static void Tick()
    {
        if (File.Exists(AllDone)) { EditorApplication.update -= Tick; return; }
        if (Job >= Jobs.Length)
        {
            if (Busy) return;
            try { SwapView(SimulatorType()); } catch (Exception e) { File.WriteAllText("Temp/__ProbeJobs_view_error.txt", e.ToString()); }
            File.WriteAllText(AllDone, "ok");
            EditorApplication.update -= Tick;
            return;
        }
        string job = Jobs[Job];
        if (Phase == 0)
        {
            if (Busy) { quiet = 0; return; }
            if (++quiet < 300) return;
            quiet = 0;
            Directory.CreateDirectory(Dir);
            if (job == "SpearItem")
            {
                try { SpearAssetsTool.MakeSpearItem(); AssetDatabase.Refresh(); File.WriteAllText(Dir + "/report.txt", "spear prefab " + File.Exists("Assets/Resources/spear.prefab") + ", icon " + File.Exists("Assets/ART/Icons/spear.png")); }
                catch (Exception e) { File.WriteAllText(Dir + "/tool_error.txt", e.ToString()); }
                File.WriteAllText(Dir + "/done.txt", "ok");
                SessionState.SetInt(JobKey, Job + 1);
                return;
            }
            if (job == "Walk" || job == "Profile" || job == "Colliders")
            {
                try { File.WriteAllText(Dir + "/walk.txt", job == "Walk" ? Walkable() : job == "Profile" ? Profile() : Colliders()); } catch (Exception e) { File.WriteAllText(Dir + "/walk_error.txt", e.ToString()); }
                File.WriteAllText(Dir + "/done.txt", "ok");
                SessionState.SetInt(JobKey, Job + 1);
                return;
            }
            if (job == "Probe7")
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
                if (scene.name != "GameScene" || scene.isDirty)
                {
                    File.WriteAllText(Dir + "/stopped.txt", "scene " + scene.name + " dirty " + scene.isDirty);
                    File.WriteAllText(AllDone, "stopped");
                    EditorApplication.update -= Tick;
                    return;
                }
                try
                {

                    File.WriteAllText(Dir + "/paths_report.txt", IoanPathsTool.Make());
                    PenAssetsTool.MakeMapPicture();
                    Overview();
                    AssetDatabase.Refresh();
                }
                catch (Exception e) { File.WriteAllText(Dir + "/tool_error.txt", e.ToString()); }
            }
            try
            {
                if (job == "Probe3a" || job.EndsWith("PC")) { SwapView(GameViewType()); SetGameViewSize(1920, 1080); }
                else if (job != "Probe3b") SwapView(SimulatorType()); // the phone
                if (job == "Probe3b") { SwapView(GameViewType()); SetGameViewSize(1280, 1024); }
            }
            catch (Exception e) { File.WriteAllText(Dir + "/view_error.txt", e.ToString()); }
            ShowPlayView();
            Phase = 1;
            EditorApplication.EnterPlaymode();
            return;
        }
        if (Phase == 2 && EditorApplication.isPlaying && File.Exists(Dir + "/finished.txt"))
        {
            Phase = 3;
            EditorApplication.ExitPlaymode();
            return;
        }
        if (Phase == 3 && !Busy)
        {
            File.WriteAllText(Dir + "/done.txt", "ok");
            SessionState.SetInt(JobKey, Job + 1);
            Phase = 0;
        }
    }

    // ---------------- the editor's play view: the phone simulator or the PC's Game view ---------------- //

    static Type GameViewType() => typeof(Editor).Assembly.GetType("UnityEditor.GameView");

    static Type SimulatorType()
    {
        foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = a.GetType("UnityEditor.DeviceSimulation.SimulatorWindow");
            if (t != null) return t;
        }
        return null;
    }

    // the play view's tab in front (a hidden Game view or Simulator draws nothing: the tests wait for frames)
    static void ShowPlayView()
    {
        try
        {
            var pmv = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
            var main = pmv.GetMethod("GetMainPlayModeView", Any).Invoke(null, null) as EditorWindow;
            if (main != null) { main.Show(); main.Focus(); }
        }
        catch (Exception e) { File.WriteAllText("Temp/__ProbeJobs_show_error.txt", e.ToString()); }
    }

    static void SwapView(Type to)
    {
        if (to == null) return;
        var pmv = typeof(Editor).Assembly.GetType("UnityEditor.PlayModeView");
        var main = pmv.GetMethod("GetMainPlayModeView", Any).Invoke(null, null);
        if (main == null || main.GetType() == to) return;
        pmv.GetMethod("SwapMainWindow", Any).Invoke(main, new object[] { to });
    }

    static void SetGameViewSize(int w, int h)
    {
        var asm = typeof(Editor).Assembly;
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = single.GetProperty("instance").GetValue(null);
        var group = sizesType.GetProperty("currentGroup").GetValue(sizes);
        var groupType = group.GetType();
        int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            var st = size.GetType();
            if ((int)st.GetProperty("width").GetValue(size) == w && (int)st.GetProperty("height").GetValue(size) == h
                && st.GetProperty("sizeType").GetValue(size).ToString() == "FixedResolution") { index = i; break; }
        }
        if (index < 0)
        {
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var kind = asm.GetType("UnityEditor.GameViewSizeType");
            var ctor = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) });
            var made = ctor.Invoke(new object[] { Enum.Parse(kind, "FixedResolution"), w, h, "Test " + w + "x" + h });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { made });
            index = total;
        }
        var gameView = EditorWindow.GetWindow(GameViewType());
        GameViewType().GetProperty("selectedSizeIndex", Any).SetValue(gameView, index);
    }

    // Where a player can walk from the lake: ground no steeper than 44 deg (the player's slope limit is 45), on the
    // heightmap's own grid. How near that comes to the cave's mouth and the other mountain's top, and both drawn from
    // above: green walkable and reached, yellow walkable but not reached, red too steep, dark lines every 10 m.
    static string Walkable()
    {
        var sb = new System.Text.StringBuilder();
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Vector3 tp = terrain.transform.position;
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1);
        var heights = data.GetHeights(0, 0, res, res);
        var river = UnityEngine.Object.FindObjectOfType<River>();
        var cave = UnityEngine.Object.FindObjectOfType<Cave>();
        Vector3 summit = MiracleStream.FindSummit(terrain, river, cave);
        var steep = new float[res * res];
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
                steep[z * res + x] = data.GetSteepness((float)x / (res - 1), (float)z / (res - 1));
        Vector3 W(int i) => new Vector3(tp.x + (i % res) * sx, heights[i / res, i % res] * data.size.y + tp.y, tp.z + (i / res) * sz);
        int I(Vector3 p) => Mathf.Clamp(Mathf.RoundToInt((p.z - tp.z) / sz), 0, res - 1) * res + Mathf.Clamp(Mathf.RoundToInt((p.x - tp.x) / sx), 0, res - 1);
        Vector3 from = river.lakeCentre + Vector3.right * (river.lakeRadius + 20f);
        var seen = new bool[res * res];
        var queue = new System.Collections.Generic.Queue<int>();
        int s0 = I(from);
        seen[s0] = true;
        queue.Enqueue(s0);
        int count = 0;
        while (queue.Count > 0)
        {
            int i = queue.Dequeue();
            count++;
            int x = i % res, z = i / res;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, nz = z + dz;
                    if ((dx == 0 && dz == 0) || nx < 0 || nz < 0 || nx >= res || nz >= res) continue;
                    int j = nz * res + nx;
                    if (seen[j] || steep[j] > 44f) continue;
                    seen[j] = true;
                    queue.Enqueue(j);
                }
        }
        sb.AppendLine("walkable from the lake: " + count + " spots of " + (res * res) + ", start steep " + steep[s0].ToString("0"));
        float bestD = 1e9f, highNear = -1e9f;
        Vector3 bestP = Vector3.zero, highP = Vector3.zero;
        bool canCross = false;
        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i]) continue;
            Vector3 p = W(i);
            float d = new Vector2(p.x - summit.x, p.z - summit.z).magnitude;
            if (d < bestD) { bestD = d; bestP = p; }
            if (d <= CanonCross.SummitReach && p.y > highNear) { highNear = p.y; highP = p; }
            if (d <= CanonCross.SummitReach && p.y >= summit.y - 14f) canCross = true;
        }
        sb.AppendLine("summit " + summit + ": nearest walkable " + bestD.ToString("0") + " m away at height " + bestP.y.ToString("0")
            + "; highest walkable within " + CanonCross.SummitReach + " m: " + highNear.ToString("0") + " at " + highP + "; the cross can be raised walking: " + canCross);
        Vector3 mouth = cave.path[0];
        float caveD = 1e9f;
        Vector3 caveP = Vector3.zero;
        for (int i = 0; i < seen.Length; i++)
        {
            if (!seen[i]) continue;
            Vector3 p = W(i);
            float d = Vector3.Distance(p, mouth);
            if (d < caveD) { caveD = d; caveP = p; }
        }
        sb.AppendLine("cave mouth " + mouth + ": nearest walkable " + caveD.ToString("0.0") + " m (" + caveP + ")");
        // the steepness right around the top
        int top = I(summit);
        float around = 0f;
        for (int dz = -3; dz <= 3; dz++) for (int dx = -3; dx <= 3; dx++) around = Mathf.Max(around, steep[Mathf.Clamp(top + dz * res + dx, 0, steep.Length - 1)]);
        sb.AppendLine("steepest within 6 m of the top: " + around.ToString("0") + " deg");
        Picture(Dir + "/summit.png", summit, 360f, seen, steep, heights, data, tp, res);
        Picture(Dir + "/cave.png", mouth, 240f, seen, steep, heights, data, tp, res);
        return sb.ToString();
    }

    // the solid things near the other mountain's top (they block Brother Ioan's way up), and the paths' ends
    static string Colliders()
    {
        var sb = new System.Text.StringBuilder();
        var terrain = Terrain.activeTerrain;
        var river = UnityEngine.Object.FindObjectOfType<River>();
        var cave = UnityEngine.Object.FindObjectOfType<Cave>();
        Vector3 summit = MiracleStream.FindSummit(terrain, river, cave);
        foreach (var c in UnityEngine.Object.FindObjectsOfType<Collider>())
        {
            if (c is TerrainCollider || !c.enabled) continue;
            var b = c.bounds;
            Vector3 d = b.ClosestPoint(summit) - summit; d.y = 0f;
            if (d.magnitude > 30f) continue;
            string path = c.name;
            for (var t = c.transform.parent; t != null; t = t.parent) path = t.name + "/" + path;
            sb.AppendLine(path + "  " + c.GetType().Name + " trigger " + c.isTrigger + " bounds " + b.center.ToString("F1") + " size " + b.size.ToString("F1") + " active " + c.gameObject.activeInHierarchy);
        }
        var data = IslandPaths.Data;
        if (data != null)
        {
            foreach (var l in data.paths) sb.AppendLine("path " + l.points.Count + ": " + l.points[0].ToString("F0") + " -> " + l.points[l.points.Count - 1].ToString("F0"));
            foreach (var l in data.stairs) sb.AppendLine("steps " + l.points.Count + ": " + l.points[0].ToString("F0") + " -> " + l.points[l.points.Count - 1].ToString("F0"));
        }
        return sb.ToString();
    }

    // the ground's height around the other mountain's top and the cave's mouth: 16 directions, every 4 m
    static string Profile()
    {
        var sb = new System.Text.StringBuilder();
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        Vector3 tp = terrain.transform.position;
        var river = UnityEngine.Object.FindObjectOfType<River>();
        var cave = UnityEngine.Object.FindObjectOfType<Cave>();
        Vector3 summit = MiracleStream.FindSummit(terrain, river, cave);
        float H(Vector3 p) => terrain.SampleHeight(p) + tp.y;
        float S(Vector3 p) => data.GetSteepness((p.x - tp.x) / data.size.x, (p.z - tp.z) / data.size.z);
        foreach (var (name, centre) in new[] { ("summit", summit), ("cave mouth", cave.path[0]) })
        {
            sb.AppendLine(name + " " + centre + " ground " + H(centre).ToString("0.0"));
            for (int a = 0; a < 16; a++)
            {
                float ang = a * 22.5f;
                Vector3 dir = Quaternion.Euler(0f, ang, 0f) * Vector3.forward;
                sb.Append("  " + ang.ToString("000") + ":");
                for (int r = 0; r <= 120; r += 4)
                {
                    Vector3 p = centre + dir * r;
                    sb.Append(" " + H(p).ToString("0") + "/" + S(p).ToString("0"));
                }
                sb.AppendLine();
            }
        }
        if (cave.path.Count > 4) sb.AppendLine("cave path 0..4: " + cave.path[0] + " " + cave.path[1] + " " + cave.path[2] + " " + cave.path[4] + ", radius " + cave.radius);
        return sb.ToString();
    }

    static void Picture(string file, Vector3 centre, float size, bool[] seen, float[] steep, float[,] heights, TerrainData data, Vector3 tp, int res)
    {
        const int px = 512;
        var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
        float sx = data.size.x / (res - 1), sz = data.size.z / (res - 1);
        for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float wx = centre.x - size / 2f + (x + 0.5f) * size / px, wz = centre.z - size / 2f + (y + 0.5f) * size / px;
                int ix = Mathf.Clamp(Mathf.RoundToInt((wx - tp.x) / sx), 0, res - 1), iz = Mathf.Clamp(Mathf.RoundToInt((wz - tp.z) / sz), 0, res - 1);
                int i = iz * res + ix;
                float h = heights[iz, ix] * data.size.y + tp.y;
                Color c = steep[i] > 44f ? new Color(0.75f, 0.15f, 0.1f) : seen[i] ? new Color(0.2f, 0.7f, 0.25f) : new Color(0.85f, 0.8f, 0.2f);
                c *= 0.55f + 0.45f * Mathf.Clamp01((h + 20f) / 180f);
                if (Mathf.Repeat(h, 10f) < 0.6f) c *= 0.5f;
                tex.SetPixel(x, y, c);
            }
        for (int d = -4; d <= 4; d++) { tex.SetPixel(px / 2 + d, px / 2, Color.white); tex.SetPixel(px / 2, px / 2 + d, Color.white); }
        tex.Apply();
        File.WriteAllBytes(file, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }

    // the island from above: the paths (red), the stream's bed (cyan), the trees (dark)
    static void Overview()
    {
        var terrain = Terrain.activeTerrain;
        var data = terrain.terrainData;
        int res = data.alphamapResolution;
        var maps = data.GetAlphamaps(0, 0, res, res);
        int path = Array.FindIndex(data.terrainLayers, l => l != null && l.name == "IoanPath");
        int bed = Array.FindIndex(data.terrainLayers, l => l != null && l.name == "StreamBed");
        var tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        Vector3 tp = terrain.transform.position;
        for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                float h = data.GetInterpolatedHeight(u, v) + tp.y;
                Vector3 n = data.GetInterpolatedNormal(u, v);
                float shade = Mathf.Clamp01(0.3f + 0.7f * Vector3.Dot(n, new Vector3(-0.5f, 0.7f, 0.5f).normalized));
                Color c = h < -19.05f ? new Color(0.1f, 0.25f, 0.5f) : new Color(0.35f, 0.55f, 0.25f) * shade;
                if (path >= 0) c = Color.Lerp(c, new Color(1f, 0.15f, 0.05f), maps[z, x, path]);
                if (bed >= 0) c = Color.Lerp(c, new Color(0f, 1f, 1f), maps[z, x, bed]);
                tex.SetPixel(x, z, c);
            }
        foreach (var f in UnityEngine.Object.FindObjectsOfType<MeshFilter>())
        {
            if (f.GetComponentInParent<ScenePine>() == null && f.GetComponentInParent<SceneTree>() == null) continue;
            Vector3 p = f.transform.position;
            tex.SetPixel((int)((p.x - tp.x) / data.size.x * res), (int)((p.z - tp.z) / data.size.z * res), new Color(0.05f, 0.12f, 0.05f));
        }
        tex.Apply();
        File.WriteAllBytes(Dir + "/overview.png", tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
    }
}
#endif
