using System.Collections.Generic;
using UnityEngine;

// A river of the island (fresh water: the player can drink there, and from rain puddles: FreshWater; the sea is salty).
// The old one comes down from the big mountain into the crater lake (made in the editor: Tools > Nature > Make River,
// which also digs its bed into the terrain): it is the main river (River.Instance, the lake belongs to it). The others
// (Tools > Nature > 7. More Rivers, each with a riverName) come down from the mountains to the sea.
//  - points / halfWidths: the middle line of the water (on its surface) and how wide it is
//  - a flowing water sound follows the player along each river (only the rivers near the player play)
//  - River.All: every river; River.IsWater / NearestWater look at all of them
[ExecuteAlways]
public class River : MonoBehaviour
{
    // every enabled river (also in the editor: [ExecuteAlways])
    static readonly List<River> all = new List<River>();
    public static IReadOnlyList<River> All => all;

    // the main river: the old one that flows into the lake (lakeRadius > 0), else the first one
    public static River Instance { get; private set; }

    [Tooltip("The river's name (empty for the old river that flows into the lake)")]
    public string riverName = "";
    [Tooltip("The middle of the water, on its surface, from the spring to the sea")]
    public List<Vector3> points = new List<Vector3>();
    [Tooltip("Half of the water's width at each point (metres)")]
    public List<float> halfWidths = new List<float>();
    [Tooltip("The lake the river flows into: the middle of its water surface")]
    public Vector3 lakeCentre;
    [Tooltip("How far the lake's water reaches from its middle (metres; 0: no lake, the river runs into the sea)")]
    public float lakeRadius;

    [Header("Drinking")]
    [Tooltip("The player can drink when standing this close to the water (metres)")]
    public float drinkReach = 2.5f;

    [Header("Sound")]
    public float soundVolume = 0.55f;
    public float soundDistance = 45f;

    const float CellSize = 32f;
    readonly Dictionary<long, List<int>> cells = new Dictionary<long, List<int>>();
    AudioSource sound;
    Transform listener;

    void OnEnable()
    {
        if (!all.Contains(this)) all.Add(this);
        BuildCells();
        PickMain();
    }

    void OnDisable()
    {
        all.Remove(this);
        PickMain();
    }

    void OnValidate() => PickMain(); // (the lake's size changed in the Inspector)

    // Which river is the main one. The one already chosen stays while it is still right, so turning another river
    // on or off never changes it.
    static void PickMain()
    {
        all.RemoveAll(r => r == null);
        var current = Instance != null && all.Contains(Instance) ? Instance : null;
        if (current != null && current.lakeRadius > 0f) return;
        foreach (var r in all)
            if (r.lakeRadius > 0f) { Instance = r; return; }
        Instance = current != null ? current : (all.Count > 0 ? all[0] : null);
    }

    static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;

    // the segments sorted into squares of the map (to find the nearest bit of river quickly)
    public void BuildCells()
    {
        cells.Clear();
        for (int i = 0; i + 1 < points.Count; i++)
        {
            float w = Mathf.Max(HalfWidth(i), HalfWidth(i + 1)) + 12f;
            Vector3 a = points[i], b = points[i + 1];
            int x0 = Mathf.FloorToInt((Mathf.Min(a.x, b.x) - w) / CellSize), x1 = Mathf.FloorToInt((Mathf.Max(a.x, b.x) + w) / CellSize);
            int z0 = Mathf.FloorToInt((Mathf.Min(a.z, b.z) - w) / CellSize), z1 = Mathf.FloorToInt((Mathf.Max(a.z, b.z) + w) / CellSize);
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    long key = Key(x, z);
                    if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<int>();
                    list.Add(i);
                }
        }
    }

    // half of the water's width at point i (metres)
    public float HalfWidth(int i) => i < halfWidths.Count ? halfWidths[i] : 3f;

    // metres from p to the water's edge along segment i (negative: in the water), and the nearest point of its middle
    float SegmentDistance(int i, Vector3 p, out Vector3 nearest)
    {
        Vector3 a = points[i], b = points[i + 1];
        Vector2 ab = new Vector2(b.x - a.x, b.z - a.z);
        Vector2 ap = new Vector2(p.x - a.x, p.z - a.z);
        float t = ab.sqrMagnitude > 0.0001f ? Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude) : 0f;
        Vector2 closest = new Vector2(a.x, a.z) + ab * t;
        nearest = Vector3.Lerp(a, b, t);
        return Vector2.Distance(closest, new Vector2(p.x, p.z)) - Mathf.Lerp(HalfWidth(i), HalfWidth(i + 1), t);
    }

    // Metres from p to the edge of the water (negative: in the water). 999 far away from the river (more than about
    // 12 m from its banks: only the nearby bits are looked at, it is asked very often).
    public float DistanceToWater(Vector3 p, out Vector3 nearest)
    {
        nearest = p;
        if (!cells.TryGetValue(Key(Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.z / CellSize)), out var list)) return 999f;
        float best = 999f;
        foreach (int i in list)
        {
            float d = SegmentDistance(i, p, out var at);
            if (d < best)
            {
                best = d;
                nearest = at;
            }
        }
        return best;
    }

    public float DistanceToWater(Vector3 p) => DistanceToWater(p, out _);

    // Like DistanceToWater, but from anywhere on the island (looks at the whole river: slower, for the map)
    public float DistanceToWaterAnywhere(Vector3 p, out Vector3 nearest)
    {
        nearest = p;
        if (points.Count == 0) return 999999f;
        nearest = points[0];
        float best = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(nearest.x, nearest.z)) - HalfWidth(0);
        for (int i = 0; i + 1 < points.Count; i++)
        {
            float d = SegmentDistance(i, p, out var at);
            if (d < best)
            {
                best = d;
                nearest = at;
            }
        }
        return best;
    }

    // in the water of any river (rabbits, trees, snares and fences keep out of it)
    public static bool IsWater(Vector3 p, float margin = 0f)
    {
        for (int i = 0; i < all.Count; i++)
            if (all[i] != null && all[i].DistanceToWater(p) < margin) return true;
        return false;
    }

    // Metres from p to the nearest water's edge of all the rivers (negative: in the water; 999 if no river is near),
    // which river it is and the nearest point of its middle line.
    public static float NearestWater(Vector3 p, out River river, out Vector3 nearest)
    {
        river = null;
        nearest = p;
        float best = 999f;
        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            if (r == null) continue;
            float d = r.DistanceToWater(p, out var at);
            if (d < best) { best = d; river = r; nearest = at; }
        }
        return best;
    }

    // The same from anywhere on the island (the map's goal: the nearest river). null if there is no river.
    public static float NearestWaterAnywhere(Vector3 p, out River river, out Vector3 nearest)
    {
        river = null;
        nearest = p;
        float best = 999999f;
        for (int i = 0; i < all.Count; i++)
        {
            var r = all[i];
            if (r == null || r.points.Count == 0) continue;
            float d = r.DistanceToWaterAnywhere(p, out var at);
            if (d < best) { best = d; river = r; nearest = at; }
        }
        return best;
    }

    // the player stands at the river (or in it) and can drink
    public bool CanDrink(Vector3 playerPosition)
    {
        return DistanceToWater(playerPosition) < drinkReach;
    }


    // ---------------- the sound of flowing water ---------------- //

    void Start()
    {
        if (!Application.isPlaying) return;
        var go = new GameObject("River sound");
        go.transform.SetParent(transform, false);
        sound = go.AddComponent<AudioSource>();
        sound.clip = BrookSound();
        sound.loop = true;
        sound.spatialBlend = 1f;
        sound.rolloffMode = AudioRolloffMode.Linear;
        sound.minDistance = 6f;
        sound.maxDistance = soundDistance;
        sound.dopplerLevel = 0f;
        sound.volume = 0f;
        sound.playOnAwake = false; // (it starts when the player comes near: Update)
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null) listener = player.transform;
    }

    void Update()
    {
        if (sound == null) return;
        Vector3 at = listener != null ? listener.position : (Camera.main != null ? Camera.main.transform.position : transform.position);
        float d = DistanceToWater(at, out var nearest);
        if (d > soundDistance)
        {
            // far away: silent and not playing at all (with several rivers only the near ones play; phone)
            sound.volume = 0f;
            if (sound.isPlaying) sound.Pause();
            return;
        }
        sound.transform.position = nearest;
        sound.volume = soundVolume * SoundManager.AmbienceVolume; // the "Ambience" slider of the Settings
        if (!sound.isPlaying)
        {
            // (each river starts its loop at another place: two rivers near each other don't sound the same)
            if (sound.time <= 0f && sound.clip != null && points.Count > 0) sound.time = Mathf.Repeat(points[0].x * 0.37f + points[0].z * 0.11f, sound.clip.length * 0.9f);
            sound.Play();
        }
    }

    // a few seconds of murmuring water, made in code (loops without a click)
    static AudioClip brook;
    public static AudioClip BrookSound()
    {
        if (brook != null) return brook;
        const int rate = 22050;
        const float seconds = 4f;
        int n = (int)(rate * seconds);
        var s = new float[n];
        var random = new System.Random(77);
        float lp1 = 0f, lp2 = 0f, hp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)random.NextDouble() * 2f - 1f;
            lp1 += (white - lp1) * 0.18f;
            lp2 += (lp1 - lp2) * 0.35f;
            hp = lp2 - prev; prev = lp2;
            float t = (float)i / rate;
            float swell = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 2f / seconds * 2f) * Mathf.Sin(t * Mathf.PI * 2f / seconds * 3f + 1f);
            s[i] = (lp2 * 0.55f + hp * 2.2f) * swell;
        }
        // little bubbles
        for (int b = 0; b < 46; b++)
        {
            int start = random.Next(0, n - rate / 10);
            float f0 = 500f + (float)random.NextDouble() * 900f;
            float len = 0.02f + (float)random.NextDouble() * 0.05f;
            int count = (int)(len * rate);
            float amp = 0.08f + (float)random.NextDouble() * 0.12f;
            for (int k = 0; k < count && start + k < n; k++)
            {
                float t = (float)k / rate;
                float f = f0 * (1f + 2.5f * t / len);
                s[start + k] += Mathf.Sin(2f * Mathf.PI * f * t) * amp * Mathf.Sin(Mathf.PI * k / count);
            }
        }
        // fade the ends into each other so it loops smoothly
        int fade = rate / 4;
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            float a = s[i], z = s[n - fade + i];
            s[i] = a * k + z * (1f - k);
        }
        var trimmed = new float[n - fade];
        System.Array.Copy(s, trimmed, n - fade);
        float peak = 0.001f;
        foreach (var v in trimmed) peak = Mathf.Max(peak, Mathf.Abs(v));
        for (int i = 0; i < trimmed.Length; i++) trimmed[i] = trimmed[i] / peak * 0.5f;
        brook = AudioClip.Create("River (made in code)", trimmed.Length, 1, rate, false);
        brook.SetData(trimmed, 0);
        return brook;
    }
}
