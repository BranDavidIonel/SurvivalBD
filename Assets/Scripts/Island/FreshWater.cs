using System.Collections.Generic;
using UnityEngine;

// Water the player can drink (made in code by EnvironmentBootstrap): the river, the lake it flows into, and the
// puddles the rain leaves. Never the sea (it is salty).
//  - SelectionManager shows "Drink water" when the player stands at fresh water and looks down at it
//  - rain puddles: while it rains, puddles fill up around the player (on flat ground); after the rain they dry up in
//    2-3 game days (smaller and smaller); every drink takes a little of a puddle's water
// The save keeps the puddles (EnviromentData.puddles).
public class FreshWater : MonoBehaviour
{
    public static FreshWater Instance { get; private set; }

    public enum Source { None, River, Lake, Puddle }

    // the story's goals listen to these
    public static event System.Action Drank;
    public static event System.Action<Source> Found; // the player came to the river or the lake (the first time this game)

    [Tooltip("Water bar gained by one drink (of 100)")]
    public float waterPerDrink = 25f;
    [Tooltip("The player can drink this close to the water's edge (metres)")]
    public float drinkReach = 2.5f;

    [Header("Rain puddles")]
    public int maxPuddles = 36;
    [Tooltip("Game hours a full puddle needs to dry up after the rain (2-3 days)")]
    public Vector2 dryHours = new Vector2(48f, 72f);
    public Vector2 puddleSize = new Vector2(1.3f, 3.2f);
    [Tooltip("Rain (0..1) above which puddles fill")]
    public float rainForPuddles = 0.3f;

    class Puddle
    {
        public PuddleData data;
        public GameObject go;
        public float shownSize = -1f;
    }

    readonly List<Puddle> puddles = new List<Puddle>();
    bool riverFound, lakeFound;
    float checkTimer, rainHours;
    Transform player;
    Material puddleMaterial;
    Mesh disc;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) player = movement.transform;
    }

    Vector3 PlayerPosition => player != null ? player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

    // which fresh water is at p (none: the player can't drink here)
    public Source At(Vector3 p)
    {
        var river = River.Instance;
        if (river != null)
        {
            if (river.DistanceToWater(p) < drinkReach) return Source.River;
            if (river.lakeRadius > 0f)
            {
                Vector3 d = p - river.lakeCentre; d.y = 0f;
                // standing at the water's edge (the ground there is about as low as the water) or in it
                if (d.magnitude < river.lakeRadius + drinkReach && Forest.GroundY(p) < river.lakeCentre.y + 1.2f) return Source.Lake;
            }
        }
        return NearPuddle(p) != null ? Source.Puddle : Source.None;
    }

    Puddle NearPuddle(Vector3 p)
    {
        foreach (var q in puddles)
        {
            Vector3 d = q.data.position - p; d.y = 0f;
            if (q.data.water > 0.05f && d.magnitude < SizeOf(q) + 1.4f) return q;
        }
        return null;
    }

    static float SizeOf(Puddle q) => q.data.size * Mathf.Sqrt(Mathf.Clamp01(q.data.water));

    public void Drink(Vector3 at)
    {
        var state = PlayerState.Instance;
        if (state == null) return;
        var source = At(at);
        if (source == Source.None) return;
        if (state.currentHydrationPercent >= state.maxHydrationPercent - 0.5f)
        {
            InventorySystem.Instance.ShowMessage("You are not thirsty");
            return;
        }
        state.setHydration(Mathf.Min(state.maxHydrationPercent, state.currentHydrationPercent + waterPerDrink));
        if (source == Source.Puddle)
        {
            var q = NearPuddle(at);
            if (q != null) q.data.water = Mathf.Max(0f, q.data.water - 0.2f);
        }
        if (SoundManager.Instance != null)
            SoundManager.Instance.PlayAt(ProceduralAudio.Splash, at + Vector3.up * 0.3f, 0.5f, 1.25f, 20f, 2f);
        InventorySystem.Instance.ShowMessage((source == Source.Puddle ? "Rain water" : "Fresh water") + "  +" + Mathf.RoundToInt(waterPerDrink) + " water");
        if (Drank != null) Drank();
    }

    void Update()
    {
        checkTimer -= Time.deltaTime;
        if (checkTimer <= 0f)
        {
            checkTimer = 0.5f;
            CheckFound();
        }
        UpdatePuddles(Time.deltaTime * DayNightSystem.WeatherHoursPerSecond);
    }

    void CheckFound()
    {
        var river = River.Instance;
        if (river == null) return;
        Vector3 p = PlayerPosition;
        if (!riverFound && river.DistanceToWater(p) < 12f)
        {
            riverFound = true;
            if (Found != null) Found(Source.River);
        }
        if (!lakeFound && river.lakeRadius > 0f)
        {
            Vector3 d = p - river.lakeCentre; d.y = 0f;
            if (d.magnitude < river.lakeRadius + 12f)
            {
                lakeFound = true;
                if (Found != null) Found(Source.Lake);
            }
        }
    }

    // ---------------- rain puddles ---------------- //

    void UpdatePuddles(float hours)
    {
        var weather = WeatherSystem.Instance;
        float rain = weather != null ? weather.Rain : 0f;
        bool raining = rain > rainForPuddles && (weather == null || weather.SnowCover < 0.3f);
        if (raining && hours > 0f)
        {
            // new puddles while it rains (two every half game hour of rain), around the player
            rainHours += hours;
            while (rainHours >= 0.5f)
            {
                rainHours -= 0.5f;
                for (int i = 0; i < 2 && puddles.Count < maxPuddles; i++) TryNewPuddle();
            }
        }
        for (int i = puddles.Count - 1; i >= 0; i--)
        {
            var q = puddles[i];
            var d = q.data;
            if (raining) d.water = Mathf.Min(1f, d.water + hours * 0.6f * rain);
            else d.water -= hours / Mathf.Max(1f, d.dryHours);
            if (d.water <= 0f && !raining)
            {
                if (q.go != null) Destroy(q.go);
                puddles.RemoveAt(i);
                continue;
            }
            ShowPuddle(q);
        }
    }

    void TryNewPuddle()
    {
        Vector3 c = PlayerPosition;
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Random.Range(12f, 110f);
            Vector3 p = c + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            if (!GoodPuddleSpot(p)) continue;
            p.y = Forest.GroundY(p);
            var data = new PuddleData
            {
                position = p,
                size = Random.Range(puddleSize.x, puddleSize.y),
                water = 0.15f,
                dryHours = Random.Range(dryHours.x, dryHours.y),
            };
            puddles.Add(new Puddle { data = data });
            return;
        }
    }

    bool GoodPuddleSpot(Vector3 p)
    {
        var terrain = Terrain.activeTerrain;
        if (terrain == null) return false;
        var td = terrain.terrainData;
        Vector3 local = p - terrain.transform.position;
        float u = local.x / td.size.x, v = local.z / td.size.z;
        if (u < 0.02f || v < 0.02f || u > 0.98f || v > 0.98f) return false;
        if (td.GetSteepness(u, v) > 6f) return false;
        var ocean = Ocean.Instance;
        if (ocean != null && (ocean.DistanceFromShore(p) > -32f || Forest.GroundY(p) < ocean.Level + 0.5f)) return false;
        if (At(p) != Source.None) return false; // not in the river, the lake or another puddle
        foreach (var q in puddles) { Vector3 d = q.data.position - p; d.y = 0f; if (d.magnitude < 9f) return false; }
        return true;
    }

    void ShowPuddle(Puddle q)
    {
        float size = SizeOf(q);
        if (q.go == null)
        {
            if (puddleMaterial == null) MakeLook();
            q.go = new GameObject("Rain puddle");
            q.go.transform.SetParent(transform, false);
            q.go.transform.position = q.data.position + Vector3.up * 0.04f;
            q.go.transform.rotation = Quaternion.Euler(0f, q.data.position.x * 37f % 360f, 0f);
            q.go.AddComponent<MeshFilter>().sharedMesh = disc;
            var r = q.go.AddComponent<MeshRenderer>();
            r.sharedMaterial = puddleMaterial;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        if (Mathf.Abs(size - q.shownSize) > 0.02f)
        {
            q.shownSize = size;
            q.go.transform.localScale = new Vector3(size, 1f, size * 0.75f);
            q.go.SetActive(size > 0.05f);
        }
    }

    void MakeLook()
    {
        var shader = Resources.Load<Shader>("Environment/SB_River");
        puddleMaterial = new Material(shader != null ? shader : Shader.Find("Standard")) { name = "Rain puddle (made in code)", hideFlags = HideFlags.DontSave };
        if (shader != null)
        {
            puddleMaterial.SetTexture("_Noise", EnvTextures.Noise);
            puddleMaterial.SetFloat("_Speed", 0.03f);
            puddleMaterial.SetFloat("_Alpha", 0.75f);
            puddleMaterial.SetColor("_Deep", new Color(0.12f, 0.14f, 0.13f));
            puddleMaterial.SetColor("_Shallow", new Color(0.2f, 0.18f, 0.13f));
        }
        // a flat disc whose edge fades out (vertex colour alpha), uneven like a real puddle
        const int segments = 20;
        var v = new List<Vector3> { Vector3.zero };
        var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var col = new List<Color> { new Color(0f, 0f, 0f, 1f) };
        var tri = new List<int>();
        for (int ring = 1; ring <= 2; ring++)
        {
            float rr = ring == 1 ? 0.75f : 1f;
            for (int s = 0; s < segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float bump = 1f + 0.18f * Mathf.Sin(a * 3f + 0.7f) + 0.1f * Mathf.Sin(a * 5f + 2f);
                v.Add(new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * rr * bump);
                uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f * rr, 0.5f + Mathf.Sin(a) * 0.5f * rr));
                col.Add(new Color(0f, 0f, 0f, ring == 1 ? 1f : 0f));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s, b = 1 + (s + 1) % segments;
            tri.Add(0); tri.Add(b); tri.Add(a);
            int c = a + segments, d = b + segments;
            tri.Add(a); tri.Add(b); tri.Add(c);
            tri.Add(b); tri.Add(d); tri.Add(c);
        }
        disc = new Mesh { name = "Puddle (made in code)" };
        disc.SetVertices(v);
        disc.SetUVs(0, uv);
        disc.SetColors(col);
        disc.SetTriangles(tri, 0);
        disc.RecalculateNormals();
        disc.RecalculateBounds();
    }

    // ---------------- save / load ---------------- //

    public List<PuddleData> GetData()
    {
        var list = new List<PuddleData>();
        foreach (var q in puddles) list.Add(q.data);
        return list;
    }

    public void SetData(List<PuddleData> saved)
    {
        foreach (var q in puddles) if (q.go != null) Destroy(q.go);
        puddles.Clear();
        if (saved == null) return;
        foreach (var d in saved) puddles.Add(new Puddle { data = d });
    }
}

[System.Serializable]
public class PuddleData
{
    public Vector3 position;
    public float size;       // metres across when full
    public float water;      // 0..1
    public float dryHours;   // game hours a full puddle needs to dry up
}
