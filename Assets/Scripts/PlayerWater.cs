using Bitgem.VFX.StylisedWater;
using UnityEngine;
using UnityEngine.UI;

// Swimming, holding your breath and the darker look under water. Sits on the Player next to PlayerMovement,
// which asks it how to move up and down while swimming (Space = up, Left Ctrl = down, or look down and walk).
public class PlayerWater : MonoBehaviour
{
    public static PlayerWater Instance { get; private set; }

    [Header("Breath")]
    public float maxBreathSeconds = 180f;   // how long you can stay with your head under water
    public float breathRefillSeconds = 4f;  // how long it takes to fill up again above water
    public float drowningDamage = 10f;      // health lost every second once the air is gone
    public float currentBreath;

    [Header("Swimming")]
    public float swimSpeedMultiplier = 0.6f;
    public float swimVerticalSpeed = 3f;
    public float eyesAboveWater = 0.35f;    // where the eyes float when you are not diving

    [Header("Underwater look")]
    public Color underwaterColor = new Color(0.03f, 0.16f, 0.22f);
    public float underwaterFogDensity = 0.06f;
    [Range(0f, 1f)] public float underwaterDarkness = 0.45f;

    public bool IsSwimming { get; private set; }
    public bool IsHeadUnderwater { get; private set; }

    CharacterController controller;
    Camera cam;
    MeshRenderer[] waters = new MeshRenderer[0];
    float nextWaterSearch;
    float eyeHeight = 1.4f;   // camera height above the controller centre, used if there is no camera
    bool overWater;
    float surface;
    float drowningTimer;

    Slider breathBar;
    Text breathText;
    GameObject overlay;

    bool underwaterLookOn;
    bool savedFog;
    FogMode savedFogMode;
    Color savedFogColor;
    float savedFogDensity;
    CameraClearFlags savedClearFlags;
    Color savedBackground;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<CharacterController>();
        currentBreath = maxBreathSeconds;
    }

    void Start()
    {
        cam = Camera.main;
        CreateBreathBar();
        CreateOverlay();
    }

    void Update()
    {
        FindWaterSurface();

        var centre = Centre();
        IsSwimming = overWater && centre < FloatingCentre() + 0.25f;
        IsHeadUnderwater = overWater && cam != null && cam.transform.position.y < surface;

        UpdateBreath();
        SetUnderwaterLook(IsHeadUnderwater);
        UpdateBreathBar();
    }

    void OnDisable()
    {
        SetUnderwaterLook(false);
    }

    void OnDestroy()
    {
        if (overlay != null) Destroy(overlay);
        if (Instance == this) Instance = null;
    }

    // Vertical speed while swimming: dive, swim up, or drift back up to the surface.
    public float GetSwimVerticalSpeed()
    {
        var toSurface = FloatingCentre() - Centre();
        float speed;
        if (Input.GetKey(KeyCode.LeftControl)) speed = -swimVerticalSpeed;
        else if (Input.GetKey(KeyCode.Space)) speed = swimVerticalSpeed;
        else speed = toSurface * 2f;
        speed = Mathf.Clamp(speed, -swimVerticalSpeed, swimVerticalSpeed);

        // never pop out above the floating height
        if (speed > 0f && Time.deltaTime > 0f) speed = Mathf.Min(speed, Mathf.Max(toSurface, 0f) / Time.deltaTime);
        return speed;
    }

    public void ResetBreath()
    {
        currentBreath = maxBreathSeconds;
        drowningTimer = 0f;
    }

    float Centre()
    {
        return transform.position.y + (controller != null ? controller.center.y : 0f);
    }

    // controller centre height at which the eyes are just above the water
    float FloatingCentre()
    {
        return surface + eyesAboveWater - EyeHeight();
    }

    // the camera moves up and down around the body when looking up or down (the whole player tilts)
    float EyeHeight()
    {
        return cam != null ? cam.transform.position.y - Centre() : eyeHeight;
    }

    void FindWaterSurface()
    {
        if ((waters.Length == 0 || System.Array.Exists(waters, w => w == null)) && Time.time >= nextWaterSearch)
        {
            nextWaterSearch = Time.time + 1f;
            var volumes = FindObjectsOfType<WaterVolumeBase>();
            waters = System.Array.ConvertAll(volumes, v => v.GetComponent<MeshRenderer>());
            waters = System.Array.FindAll(waters, w => w != null);
        }

        // the water surface is the top of the water mesh
        overWater = false;
        var position = transform.position;
        foreach (var water in waters)
        {
            if (water == null) continue;
            var bounds = water.bounds;
            if (position.x < bounds.min.x || position.x > bounds.max.x || position.z < bounds.min.z || position.z > bounds.max.z) continue;
            if (overWater && bounds.max.y <= surface) continue;
            overWater = true;
            surface = bounds.max.y;
        }
    }

    void UpdateBreath()
    {
        if (IsHeadUnderwater)
        {
            currentBreath = Mathf.Max(0f, currentBreath - Time.deltaTime);
        }
        else
        {
            currentBreath = Mathf.Min(maxBreathSeconds, currentBreath + Time.deltaTime * maxBreathSeconds / breathRefillSeconds);
        }

        // out of air: lose health every second until you come up or die
        var state = PlayerState.Instance;
        if (IsHeadUnderwater && currentBreath <= 0f && state != null && state.currentHealth > 0f)
        {
            drowningTimer += Time.deltaTime;
            if (drowningTimer >= 1f)
            {
                drowningTimer -= 1f;
                state.currentHealth = Mathf.Max(0f, state.currentHealth - drowningDamage);
            }
        }
        else
        {
            drowningTimer = 0f;
        }
    }

    void SetUnderwaterLook(bool on)
    {
        if (on == underwaterLookOn) return;
        underwaterLookOn = on;

        if (on)
        {
            savedFog = RenderSettings.fog;
            savedFogMode = RenderSettings.fogMode;
            savedFogColor = RenderSettings.fogColor;
            savedFogDensity = RenderSettings.fogDensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = underwaterColor;
            RenderSettings.fogDensity = underwaterFogDensity;

            // the water surface is not drawn from below, so hide the sky behind the fog colour
            if (cam != null)
            {
                savedClearFlags = cam.clearFlags;
                savedBackground = cam.backgroundColor;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = underwaterColor;
            }
        }
        else
        {
            RenderSettings.fog = savedFog;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogDensity = savedFogDensity;
            if (cam != null)
            {
                cam.clearFlags = savedClearFlags;
                cam.backgroundColor = savedBackground;
            }
        }

        if (overlay != null) overlay.SetActive(on);
    }

    void UpdateBreathBar()
    {
        if (breathBar == null) return;

        var show = IsHeadUnderwater || currentBreath < maxBreathSeconds;
        if (breathBar.gameObject.activeSelf != show) breathBar.gameObject.SetActive(show);
        if (!show) return;

        breathBar.value = currentBreath / maxBreathSeconds;
        if (breathText != null)
        {
            var seconds = Mathf.CeilToInt(currentBreath);
            breathText.text = $"Air to breathe {seconds / 60}:{seconds % 60:00}";
        }
    }

    // A copy of the hydration bar, placed just above the hotbar.
    void CreateBreathBar()
    {
        var hydration = FindObjectOfType<HydrationBar>(true);
        if (hydration == null) return;
        var parentCanvas = hydration.GetComponentInParent<Canvas>(true);
        if (parentCanvas == null) return;

        var bar = Instantiate(hydration.gameObject, parentCanvas.rootCanvas.transform);
        bar.name = "BreathBar";
        DestroyImmediate(bar.GetComponent<HydrationBar>());

        var rect = (RectTransform)bar.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 165f);

        breathBar = bar.GetComponent<Slider>();
        breathText = bar.GetComponentInChildren<Text>(true);
        if (breathBar != null && breathBar.fillRect != null)
        {
            var fill = breathBar.fillRect.GetComponent<Image>();
            if (fill != null) fill.color = new Color(0.9f, 0.97f, 1f);
        }
        if (breathText != null) breathText.color = new Color(0.05f, 0.15f, 0.25f);
        bar.SetActive(false);
    }

    // Dark blue tint over the screen while the head is under water, drawn below the HUD.
    void CreateOverlay()
    {
        overlay = new GameObject("UnderwaterOverlay");
        var canvas = overlay.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var image = new GameObject("Tint").AddComponent<Image>();
        image.transform.SetParent(overlay.transform, false);
        image.raycastTarget = false;
        image.color = new Color(underwaterColor.r, underwaterColor.g, underwaterColor.b, underwaterDarkness);
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        overlay.SetActive(false);
    }
}
