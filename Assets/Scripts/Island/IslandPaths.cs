using System.Collections.Generic;
using UnityEngine;

// Brother Ioan's paths and the bed of the miracle stream (made in the editor by Tools > Nature > 5. Brother Ioan's
// Paths And The Stream Bed, saved in Resources/Environment/IoanPaths.json and MiracleStreamRoute.json). In the story
// Brother Ioan made the paths himself, over many years: from the beach where the sea threw him to the lake, to his
// cave, up the other mountain and down to the northern shore. No tree grows on them (Forest), and a tree from an old
// save standing on one is gone.
public static class IslandPaths
{
    public const string PathsResource = "Environment/IoanPaths";

    [System.Serializable]
    public class PathLine { public List<Vector3> points = new List<Vector3>(); }

    [System.Serializable]
    public class PathsData
    {
        public float halfWidth = 1.7f;
        public List<PathLine> paths = new List<PathLine>();
        // his stone steps up the steep rock (each point: the middle of a step, its top), built by IoanSteps
        public List<PathLine> stairs = new List<PathLine>();
        // the flat stone at the top of the other mountain, where the cross goes (size 0: none)
        public Vector3 landing;
        public float landingSize;
    }

    const float Cell = 8f;
    static bool loaded;
    static PathsData data;
    static Dictionary<long, List<Vector3>> pathCells, bedCells;
    static Dictionary<long, List<float>> bedWidths;

    public static void Forget() { loaded = false; data = null; pathCells = bedCells = null; bedWidths = null; MiracleStream.ForgetSavedRoute(); }

    public static PathsData Data { get { Load(); return data; } }

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        pathCells = new Dictionary<long, List<Vector3>>();
        bedCells = new Dictionary<long, List<Vector3>>();
        bedWidths = new Dictionary<long, List<float>>();
        var text = Resources.Load<TextAsset>(PathsResource);
        if (text != null) data = JsonUtility.FromJson<PathsData>(text.text);
        if (data != null)
            foreach (var line in data.paths)
                if (line != null && line.points != null)
                    foreach (var p in line.points) Add(pathCells, p);
        if (data != null && data.stairs != null)
            foreach (var line in data.stairs)
                if (line != null && line.points != null)
                    foreach (var p in line.points) Add(pathCells, p);
        var route = MiracleStream.SavedRoute();
        if (route != null)
        {
            float total = 0f;
            for (int i = 1; i < route.Count; i++) total += Flat(route[i] - route[i - 1]);
            float run = 0f;
            for (int i = 0; i < route.Count; i++)
            {
                if (i > 0) run += Flat(route[i] - route[i - 1]);
                long key = Add(bedCells, route[i]);
                if (!bedWidths.TryGetValue(key, out var w)) bedWidths[key] = w = new List<float>();
                w.Add(MiracleStream.HalfWidthAt(total > 0f ? run / total : 0f));
            }
        }
    }

    static long Add(Dictionary<long, List<Vector3>> cells, Vector3 p)
    {
        long key = Key(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.z / Cell));
        if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Vector3>();
        list.Add(p);
        return key;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // on one of Brother Ioan's paths (margin: metres beyond its edge)
    public static bool OnPath(Vector3 p, float margin = 0f)
    {
        Load();
        if (data == null || pathCells.Count == 0) return false;
        float reach = data.halfWidth + margin;
        return Near(pathCells, null, p, reach, 0f);
    }

    // in the stream's bed (dug in the editor; dry until the miracle)
    public static bool InStreamBed(Vector3 p, float margin = 0f)
    {
        Load();
        if (bedCells.Count == 0) return false;
        return Near(bedCells, bedWidths, p, 0f, margin);
    }

    // a path or the stream's bed: no tree grows here
    public static bool KeepClear(Vector3 p, float margin = 0.8f) => OnPath(p, margin) || InStreamBed(p, margin + 0.6f);

    static bool Near(Dictionary<long, List<Vector3>> cells, Dictionary<long, List<float>> widths, Vector3 p, float reach, float margin)
    {
        int cx = Mathf.FloorToInt(p.x / Cell), cz = Mathf.FloorToInt(p.z / Cell);
        for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                long key = Key(cx + dx, cz + dz);
                if (!cells.TryGetValue(key, out var list)) continue;
                List<float> w = null;
                if (widths != null) widths.TryGetValue(key, out w);
                for (int i = 0; i < list.Count; i++)
                {
                    float r = w != null && i < w.Count ? w[i] + margin : reach;
                    Vector3 d = list[i] - p; d.y = 0f;
                    if (d.sqrMagnitude < r * r) return true;
                }
            }
        return false;
    }
}
