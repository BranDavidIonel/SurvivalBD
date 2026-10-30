using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The long cave that goes down into the mountain (made in the editor: Tools > Nature > Make Cave). At its end lie the
// bones of an old monk, a wooden cross, his journal and his prayer book (story: Levels 5 and 6).
//  - until Level 5 a fall of rocks closes its mouth (the player does not know about it yet)
//  - inside it is pitch dark: only a burning torch in the hand gives light (TorchLight)
//  - deeper than the daylight goes, without a burning torch the shadow comes: a few seconds, then death
//    (with the admin's Immortal the warning still shows, but it ends without death)
//  - the rock's shader gets the daylight of the outside for the part near the mouth
public class Cave : MonoBehaviour
{
    public static Cave Instance { get; private set; }

    [Tooltip("The middle of the tunnel, from the mouth to the end")]
    public List<Vector3> path = new List<Vector3>();
    [Tooltip("Half of the tunnel's width (metres)")]
    public float radius = 3f;
    [Tooltip("The rocks that close the mouth until Level 5")]
    public GameObject rockfall;
    [Tooltip("Where the monk's things lie (the end of the cave)")]
    public Transform end;

    [Header("The dark")]
    [Tooltip("Metres from the mouth where the daylight ends")]
    public float daylightReach = 12f;
    [Tooltip("Seconds in the dark (no burning torch) before the shadow takes the player")]
    public float secondsInTheDark = 6f;
    float DarkSeconds => secondsInTheDark * GameSettings.ShadowTime; // (the difficulty in Settings)

    float darkTime;
    bool taken;
    Text warning;
    GameObject warningCanvas;
    AudioSource heartbeat;
    static readonly int DaylightId = Shader.PropertyToID("_SB_CaveDaylight");

    public bool IsOpen => rockfall == null || !rockfall.activeSelf;
    public bool PlayerInside { get; private set; }
    public float PlayerAlong { get; private set; }
    public Vector3 Mouth => path.Count > 0 ? path[0] : transform.position;

    void Awake()
    {
        Instance = this;
        SnowCover.Exclude(transform); // no snow in the cave
        RaisePrayerBook();
    }

    // The prayer book lay on the dark floor at the foot of the cross (small, hard to see, sometimes half under the
    // bumpy floor). Now it lies on a stone in front of the cross, bigger, with a faint warm light over it.
    // (In Awake: before Vegetation notes where the plants and books are.)
    void RaisePrayerBook()
    {
        var place = end != null ? end : transform.Find("Monk's resting place");
        if (place == null) return;
        Transform book = null;
        foreach (var plant in place.GetComponentsInChildren<ScenePlant>(true)) if (plant.item == "prayerbook") { book = plant.transform; break; }
        if (book == null || place.Find("Stone for the prayer book") != null) return;

        Vector3 at = book.position;
        Vector3 facing = place.forward; facing.y = 0f; // the place looks back at the one who comes in
        if (facing.sqrMagnitude < 0.001f) facing = Vector3.forward;
        facing.Normalize();
        var stone = GameObject.CreatePrimitive(PrimitiveType.Cube);
        stone.name = "Stone for the prayer book";
        stone.transform.SetParent(place, true);
        stone.transform.position = at + Vector3.up * 0.38f;
        stone.transform.rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(0f, 4f, 0f);
        stone.transform.localScale = new Vector3(0.75f, 0.8f, 0.55f);
        var rock = GetComponent<MeshRenderer>();
        if (rock != null)
        {
            // the cave's rock, but without the daylight of the mouth (a cube has no colours: it would be lit)
            var stoneMaterial = new Material(rock.sharedMaterial) { name = "Stone for the prayer book", hideFlags = HideFlags.DontSave };
            stoneMaterial.SetFloat("_VertexDaylight", 0f);
            stone.GetComponent<MeshRenderer>().sharedMaterial = stoneMaterial;
        }

        book.position = at + Vector3.up * 0.79f;
        book.rotation = Quaternion.LookRotation(facing, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
        book.localScale *= 1.5f;

        var lightObject = new GameObject("Light over the prayer book");
        lightObject.transform.SetParent(place, true);
        lightObject.transform.position = at + Vector3.up * 1.6f;
        var glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.82f, 0.5f);
        glow.range = 3.5f;
        glow.intensity = 1.1f;
        glow.shadows = LightShadows.None;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        // closed until the story gets to Level 5 (StoryManager opens it; a loaded game opens it there too)
        var story = StoryManager.Instance;
        SetOpen(story != null && story.CurrentLevel >= 5);
        // the column of light that shows where the cave is (Levels 5 and 6)
        var beacon = new GameObject("Cave beacon (made in code)").AddComponent<CaveBeacon>();
        beacon.transform.SetParent(transform, true);
        beacon.cave = this;
    }

    public void SetOpen(bool open)
    {
        if (rockfall != null && rockfall.activeSelf == open) rockfall.SetActive(!open);
    }

    // metres from p to the tunnel's middle line, and how far along the tunnel that is (0 = the mouth)
    public float DistanceToPath(Vector3 p, out float along)
    {
        along = 0f;
        float best = float.MaxValue, walked = 0f;
        for (int i = 0; i + 1 < path.Count; i++)
        {
            Vector3 a = path[i], b = path[i + 1];
            Vector3 ab = b - a;
            float len = ab.magnitude;
            float t = len > 0.001f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / (len * len)) : 0f;
            float d = Vector3.Distance(a + ab * t, p);
            if (d < best) { best = d; along = walked + len * t; }
            walked += len;
        }
        return best;
    }

    // inside the tunnel or its hall (the player may be under the terrain here)
    public bool Contains(Vector3 p)
    {
        if (path.Count < 2) return false;
        return DistanceToPath(p, out float along) < radius * 2.6f && along > 0.5f;
    }

    public float Length
    {
        get { float l = 0f; for (int i = 0; i + 1 < path.Count; i++) l += Vector3.Distance(path[i], path[i + 1]); return l; }
    }

    void Update()
    {
        Shader.SetGlobalFloat(DaylightId, SkyLighting.Instance != null ? SkyLighting.Instance.DayLight : 1f);
        var state = PlayerState.Instance;
        if (path.Count < 2 || state == null || state.playerBody == null) return;
        Vector3 p = state.playerBody.transform.position;
        float d = DistanceToPath(p, out float along);
        PlayerInside = d < radius * 2.6f && along > 1f;
        PlayerAlong = along;

        // in the dark without a burning torch: the shadow comes
        bool dead = state.currentHealth <= 0f;
        bool dark = PlayerInside && along > daylightReach && !TorchLight.Burning && !dead && !taken;
        darkTime = dark ? darkTime + Time.deltaTime : Mathf.Max(0f, darkTime - Time.deltaTime * 3f);
        ShowWarning(darkTime > 0.05f && !dead);
        if (dark && darkTime >= DarkSeconds) TakePlayer();
        if (!PlayerInside || TorchLight.Burning) taken = false;
    }

    void TakePlayer()
    {
        taken = true;
        if (GameSettings.Immortal)
        {
            // ADMIN: immortal (only while the admin is unlocked): the warning ran out, but the shadow cannot take
            // the player (quiet again until the torch burns or the player goes out)
            if (InventorySystem.Instance != null) InventorySystem.Instance.ShowMessage(Lang.T("Immortal (admin): the shadow cannot take you", "Nemuritor (admin): umbra nu te poate lua"));
            return;
        }
        var death = FindObjectOfType<PlayerDeath>();
        if (death != null) death.SetNextDeathMessage(Lang.T("The shadow took you in the dark...", "Umbra te-a luat în întuneric..."));
        var cam = Camera.main;
        if (cam != null) AudioSource.PlayClipAtPoint(ShadowSounds.Scream, cam.transform.position, SoundManager.EffectsVolume);
        PlayerState.Instance.currentHealth = 0f; // PlayerDeath takes it from here
    }

    void ShowWarning(bool show)
    {
        if (show && warning == null) BuildWarning();
        if (warningCanvas == null) return;
        if (warningCanvas.activeSelf != show) warningCanvas.SetActive(show);
        if (heartbeat != null)
        {
            if (show && !heartbeat.isPlaying) heartbeat.Play();
            heartbeat.volume = show ? Mathf.Clamp01(darkTime / DarkSeconds) * SoundManager.EffectsVolume : 0f;
            if (!show && heartbeat.isPlaying) heartbeat.Stop();
        }
        if (!show) return;
        int left = Mathf.Max(0, Mathf.CeilToInt(DarkSeconds - darkTime));
        warning.text = (TorchLight.HasTorch ? Lang.T("Your torch is out! The shadow is here... ", "Torța ți s-a stins! Umbra e aici... ") : Lang.T("It is pitch dark! The shadow is here... ", "E beznă! Umbra e aici... ")) + left;
        if (hint != null) hint.text = GameSettings.Immortal
            ? Lang.T("Immortal (admin): the shadow cannot take you", "Nemuritor (admin): umbra nu te poate lua")
            : Lang.T("Get out into the light, or hold a burning torch!", "Ieși la lumină sau ține o torță aprinsă în mână!");
        warning.color = new Color(1f, 0.35f, 0.25f, 0.6f + 0.4f * Mathf.PingPong(Time.time * 2f, 1f));
    }

    Text hint;

    void BuildWarning()
    {
        warningCanvas = new GameObject("Cave warning (made in code)");
        warningCanvas.transform.SetParent(transform, false);
        var canvas = warningCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 895;
        var scaler = warningCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        warning = UISkin.NewText("Warning", warningCanvas.transform, "", 46, Color.red, TextAnchor.MiddleCenter);
        UISkin.Centered(warning.rectTransform, new Vector2(0f, 120f), new Vector2(1500f, 120f));
        hint = UISkin.NewText("Hint", warningCanvas.transform, "", 30, UISkin.TextColor, TextAnchor.MiddleCenter);
        UISkin.Centered(hint.rectTransform, new Vector2(0f, 50f), new Vector2(1400f, 60f));
        heartbeat = warningCanvas.AddComponent<AudioSource>();
        heartbeat.clip = ShadowSounds.Heartbeat;
        heartbeat.loop = true;
        heartbeat.spatialBlend = 0f;
        heartbeat.volume = 0f;
        warningCanvas.SetActive(false);
    }
}
