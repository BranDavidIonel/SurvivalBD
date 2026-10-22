using UnityEngine;
using UnityEngine.Rendering;

// Draws the sky and lights the world from the time of day (DayNightSystem), the season (TimeManager) and the
// weather (WeatherSystem):
//  - the sun crosses the sky by day (higher and longer in summer, low and short in winter)
//  - at night the same light becomes moonlight (only the moon lights the world, plus fires)
//  - the moon has phases (full moon on the first night), stars twinkle on clear nights and turn slowly
//  - clouds gather and drift with the wind; rain and snow make the sky grey, the light dim and add haze
//  - the sea reflects this sky (a small copy of it is rendered for the Ocean shader)
// Made in code at the start of the game (EnvironmentBootstrap), so the scene does not change.
public class SkyLighting : MonoBehaviour
{
    public static SkyLighting Instance { get; private set; }

    [Header("Sun and moon")]
    public float sunIntensity = 1.15f;
    public float moonIntensity = 0.5f;
    public Color moonLightColor = new Color(0.6f, 0.7f, 1f);
    [Tooltip("Days from one full moon to the next")]
    public int moonCycleDays = 8;
    [Tooltip("Size of the moon in the sky (degrees, the real one is 0.5)")]
    public float moonSizeDegrees = 3.2f;
    [Tooltip("Size of the sun in the sky (degrees)")]
    public float sunSizeDegrees = 2.2f;
    [Tooltip("Direction of sunrise (degrees around the world's up axis)")]
    public float eastAngle = -10f;

    [Header("Night")]
    [Tooltip("How much light is left at night without the moon (0 = pitch black)")]
    public float nightAmbient = 0.06f;

    // what the fire, rain and snow use to match the light of the scene (about 0 at night .. 1 at noon)
    public float DayLight { get; private set; }
    public Color LightLevel { get; private set; } = Color.white;
    public float MoonPhaseLight { get; private set; } = 1f;

    Material skyMaterial;
    Light mainLight;
    Camera skyCamera;
    RenderTexture skyCube;
    int skyFace;
    Vector4 cloudOffset;
    float dayHalfLength = 6f;   // hours from sunrise to noon (changes slowly with the season)
    float sunMaxHeight = 50f;    // degrees

    static readonly int SunDirId = Shader.PropertyToID("_SB_SunDir");
    static readonly int MoonDirId = Shader.PropertyToID("_SB_MoonDir");
    static readonly int MoonRightId = Shader.PropertyToID("_SB_MoonRight");
    static readonly int MoonUpId = Shader.PropertyToID("_SB_MoonUp");
    static readonly int SkyTopId = Shader.PropertyToID("_SB_SkyTop");
    static readonly int SkyHorizonId = Shader.PropertyToID("_SB_SkyHorizon");
    static readonly int SkyGroundId = Shader.PropertyToID("_SB_SkyGround");
    static readonly int SunColorId = Shader.PropertyToID("_SB_SunColor");
    static readonly int SunGlowId = Shader.PropertyToID("_SB_SunGlow");
    static readonly int MoonColorId = Shader.PropertyToID("_SB_MoonColor");
    static readonly int MoonLightId = Shader.PropertyToID("_SB_MoonLight");
    static readonly int StarsId = Shader.PropertyToID("_SB_Stars");
    static readonly int StarRotId = Shader.PropertyToID("_SB_StarRot");
    static readonly int CloudsId = Shader.PropertyToID("_SB_Clouds");
    static readonly int CloudLitId = Shader.PropertyToID("_SB_CloudLit");
    static readonly int CloudShadeId = Shader.PropertyToID("_SB_CloudShade");
    static readonly int CloudOffsetId = Shader.PropertyToID("_SB_CloudOffset");
    static readonly int FogId = Shader.PropertyToID("_SB_Fog");

    // ---------- sky colours by the height of the sun (sin of its angle above the horizon), sRGB ---------- //
    static readonly float[] SkyKeys = { -1f, -0.3f, -0.12f, -0.04f, 0.02f, 0.1f, 0.25f, 0.6f, 1f };
    static readonly Color[] SkyTop =
    {
        Hex(0x02040A), Hex(0x040812), Hex(0x0B1432), Hex(0x22356A), Hex(0x3F63A6), Hex(0x4A7CC6), Hex(0x3C7AD2), Hex(0x3271D0), Hex(0x2C68CC)
    };
    static readonly Color[] SkyHorizon =
    {
        Hex(0x070B15), Hex(0x0B1222), Hex(0x1C2242), Hex(0xA55841), Hex(0xF29452), Hex(0xF0C495), Hex(0xAACBEA), Hex(0xA2C8EE), Hex(0x9CC4EC)
    };
    static readonly Color[] SkyGround =
    {
        Hex(0x030406), Hex(0x04060A), Hex(0x0A0C16), Hex(0x2C2224), Hex(0x4A4240), Hex(0x5A5A5C), Hex(0x55606C), Hex(0x50606E), Hex(0x4E5E6E)
    };

    static Color Hex(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    static Color Curve(float x, float[] keys, Color[] colors)
    {
        if (x <= keys[0]) return colors[0];
        for (int i = 1; i < keys.Length; i++)
        {
            if (x <= keys[i]) return Color.Lerp(colors[i - 1], colors[i], (x - keys[i - 1]) / (keys[i] - keys[i - 1]));
        }
        return colors[colors.Length - 1];
    }

    static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, x));

    static Vector4 Lin(Color c) { var l = c.linear; return new Vector4(l.r, l.g, l.b, 1f); }
    static Vector4 Lin(Color c, float w) { var l = c.linear; return new Vector4(l.r, l.g, l.b, w); }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (skyCube != null) skyCube.Release();
        if (skyCamera != null) Destroy(skyCamera.gameObject);
        Ocean.ExternalSky = null;
    }

    void Start()
    {
        var shader = Resources.Load<Shader>("Environment/SB_Sky");
        if (shader == null || !shader.isSupported)
        {
            Debug.LogWarning("SkyLighting: the sky shader is missing or not supported here, keeping the old skyboxes.");
            if (DayNightSystem.Instance != null) DayNightSystem.Instance.useNewSky = false;
            enabled = false;
            return;
        }
        skyMaterial = new Material(shader) { name = "SB_Sky (made in code)" };
        skyMaterial.SetTexture("_CloudTex", EnvTextures.Noise);
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.useNewSky = true;
        RenderSettings.skybox = skyMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;

        cloudOffset = new Vector4(Random.value, Random.value, Random.value, Random.value);
        SetUpSkyReflection();
        UpdateSeason(true);
        LateUpdate();
    }

    // A small camera that renders only the sky into a cube texture (one side per frame) for the sea's reflection.
    void SetUpSkyReflection()
    {
        skyCube = new RenderTexture(64, 64, 16, RenderTextureFormat.ARGB32)
        {
            dimension = TextureDimension.Cube,
            name = "SB_SkyReflection",
            useMipMap = false,
            hideFlags = HideFlags.DontSave
        };
        skyCube.Create();
        var go = new GameObject("Sky reflection camera (made in code)");
        go.hideFlags = HideFlags.HideAndDontSave;
        go.transform.SetParent(transform, false);
        skyCamera = go.AddComponent<Camera>();
        skyCamera.enabled = false;
        skyCamera.cullingMask = 0;               // nothing but the sky
        skyCamera.clearFlags = CameraClearFlags.Skybox;
        skyCamera.nearClipPlane = 0.1f;
        skyCamera.farClipPlane = 10f;
        skyCamera.allowHDR = false;
        skyCamera.allowMSAA = false;
        skyCamera.RenderToCubemap(skyCube, 63);
        Ocean.ExternalSky = skyCube;
    }

    void UpdateSeason(bool instant)
    {
        float half = 6f, height = 50f;
        var season = TimeManager.Instance != null ? TimeManager.Instance.currentSeason : TimeManager.Season.Spring;
        switch (season)
        {
            case TimeManager.Season.Spring: half = 6.4f; height = 52f; break;
            case TimeManager.Season.Summer: half = 7.5f; height = 64f; break;
            case TimeManager.Season.Fall: half = 5.8f; height = 42f; break;
            case TimeManager.Season.Winter: half = 4.8f; height = 30f; break;
        }
        if (instant)
        {
            dayHalfLength = half;
            sunMaxHeight = height;
        }
        else
        {
            // a little every frame, so the sun and the moon never jump when the season changes
            float hours = Time.deltaTime * Mathf.Max(DayNightSystem.GameHoursPerSecond, 0.02f);
            dayHalfLength = Mathf.MoveTowards(dayHalfLength, half, hours * 0.5f);
            sunMaxHeight = Mathf.MoveTowards(sunMaxHeight, height, hours * 4f);
        }
    }

    // Where the sun (or the moon, half a turn later) is: 0 = rising in the east, 180 = setting in the west.
    Vector3 Orbit(float degrees, float maxHeight)
    {
        float a = degrees * Mathf.Deg2Rad;
        float h = maxHeight * Mathf.Deg2Rad;
        Vector3 east = Quaternion.Euler(0f, eastAngle, 0f) * Vector3.right;
        Vector3 south = Vector3.Cross(east, Vector3.up);
        return (Mathf.Cos(a) * east + Mathf.Sin(a) * (Mathf.Cos(h) * south + Mathf.Sin(h) * Vector3.up)).normalized;
    }

    void LateUpdate()
    {
        if (skyMaterial == null) return;
        var clock = DayNightSystem.Instance;
        var weather = WeatherSystem.Instance;
        if (clock == null) return;
        if (mainLight == null) mainLight = clock.directionlLight;
        UpdateSeason(false);

        // ---------- where the sun and the moon are ---------- //
        float hour = clock.Hour;
        float sunrise = 12f - dayHalfLength, sunset = 12f + dayHalfLength;
        float angle;
        if (hour >= sunrise && hour <= sunset) angle = (hour - sunrise) / (sunset - sunrise) * 180f;
        else angle = 180f + Mathf.Repeat(hour - sunset, 24f) / (24f - (sunset - sunrise)) * 180f;
        Vector3 sunDir = Orbit(angle, sunMaxHeight);
        Vector3 moonDir = Orbit(angle + 180f, Mathf.Clamp(88f - sunMaxHeight, 30f, 60f));
        float sunUp = sunDir.y, moonUp = moonDir.y;

        // moon phase: full on the first night of the game
        int day = TimeManager.Instance != null ? TimeManager.Instance.dayInGame : 1;
        float phase = Mathf.Repeat((day - 1 + hour / 24f) / Mathf.Max(1, moonCycleDays) + 0.5f, 1f);
        float phaseAngle = (phase * 2f - 1f) * Mathf.PI * 0.8f;          // never a fully dark new moon
        float lit = (1f + Mathf.Cos(phaseAngle)) * 0.5f;                // part of the disc that is lit
        MoonPhaseLight = 0.35f + 0.65f * lit;

        // ---------- weather ---------- //
        float cover = weather != null ? weather.CloudCover : 0.2f;
        float sheet = weather != null ? weather.CloudSheet : 0f;
        float fogAmount = weather != null ? weather.Fog : 0.05f;
        float snowFall = weather != null ? weather.Snow : 0f;
        float rain = weather != null ? weather.Rain : 0f;
        float snowCover = weather != null ? weather.SnowCover : 0f;
        float wind = weather != null ? weather.Wind : 0.2f;
        Vector2 windDir = weather != null ? weather.WindDirection : new Vector2(1f, 0f);
        float overcast = Mathf.Max(sheet, Smooth(0.55f, 1f, cover) * 0.6f);

        // ---------- sky colours ---------- //
        Color top = Curve(sunUp, SkyKeys, SkyTop);
        Color horizon = Curve(sunUp, SkyKeys, SkyHorizon);
        Color ground = Curve(sunUp, SkyKeys, SkyGround);
        float night = 1f - Smooth(-0.2f, -0.02f, sunUp);
        float moonVisible = Smooth(-0.02f, 0.15f, moonUp);
        float moonGlow = night * moonVisible * MoonPhaseLight;
        top += new Color(0.025f, 0.04f, 0.075f) * moonGlow;
        horizon += new Color(0.03f, 0.04f, 0.07f) * moonGlow;
        DayLight = Smooth(-0.1f, 0.25f, sunUp);

        // bad weather: grey sky, the same brightness as the light that gets through
        Color grey = Color.Lerp(new Color(0.05f, 0.055f, 0.07f), new Color(0.6f, 0.62f, 0.65f), DayLight);
        if (snowFall > 0.01f) grey = Color.Lerp(grey, grey * 1.12f, snowFall);
        top = Color.Lerp(top, grey * 0.92f, overcast);
        horizon = Color.Lerp(horizon, grey, overcast);

        // haze colour (also the fog colour of the world)
        Color haze = Color.Lerp(horizon, grey * 1.05f, Mathf.Clamp01(fogAmount * 1.4f));

        // ---------- sun glow, clouds ---------- //
        float sunset01 = 1f - Smooth(0.05f, 0.3f, Mathf.Abs(sunUp + 0.02f));   // near the horizon
        Color glowColor = Color.Lerp(new Color(1f, 0.92f, 0.75f), new Color(1f, 0.45f, 0.14f), sunset01);
        float glowStrength = Smooth(-0.15f, 0.0f, sunUp) * Mathf.Lerp(0.35f, 1.3f, sunset01) * (1f - overcast * 0.85f);

        Color cloudLit = Curve(sunUp, SkyKeys, new[]
        {
            Hex(0x0B0C11), Hex(0x0E1016), Hex(0x2A2530), Hex(0x8A5650), Hex(0xF2A880), Hex(0xFAD8C0), Hex(0xF4F4F6), Hex(0xF8F8FA), Hex(0xFAFAFC)
        });
        Color cloudShade = Curve(sunUp, SkyKeys, new[]
        {
            Hex(0x050608), Hex(0x07080C), Hex(0x14141E), Hex(0x4A3238), Hex(0x8A6A70), Hex(0x9A9098), Hex(0x9AA2AE), Hex(0xA2AAB6), Hex(0xA6AEBA)
        });
        cloudLit += new Color(0.06f, 0.07f, 0.1f) * moonGlow;
        cloudShade += new Color(0.02f, 0.025f, 0.035f) * moonGlow;
        cloudLit = Color.Lerp(cloudLit, grey * 1.08f, overcast * 0.8f);
        cloudShade = Color.Lerp(cloudShade, grey * 0.62f, overcast);

        // clouds drift with the wind (a bit faster when the clock runs fast, but not crazy)
        float speedUp = Mathf.Clamp(clock.autoTime ? clock.timeSpeed : 1f, 1f, 12f);
        float drift = (0.0012f + wind * 0.005f) * Time.deltaTime * speedUp;
        cloudOffset.x += windDir.x * drift; cloudOffset.y += windDir.y * drift;
        cloudOffset.z += windDir.x * drift * 1.7f; cloudOffset.w += windDir.y * drift * 1.7f;
        cloudOffset.x = Mathf.Repeat(cloudOffset.x, 1f); cloudOffset.y = Mathf.Repeat(cloudOffset.y, 1f);
        cloudOffset.z = Mathf.Repeat(cloudOffset.z, 1f); cloudOffset.w = Mathf.Repeat(cloudOffset.w, 1f);

        // ---------- send it to the sky shader ---------- //
        Vector3 moonRight = Vector3.Cross(Vector3.up, moonDir);
        if (moonRight.sqrMagnitude < 1e-4f) moonRight = Vector3.right;
        moonRight.Normalize();
        Vector3 moonUpV = Vector3.Cross(moonDir, moonRight).normalized;
        float stars = night * (1f - overcast) * (1f - Smooth(0.5f, 0.95f, cover)) * (1f - 0.35f * moonGlow);
        // the stars turn around the north star
        Vector3 east = Quaternion.Euler(0f, eastAngle, 0f) * Vector3.right;
        Vector3 north = -Vector3.Cross(east, Vector3.up);
        Vector3 pole = (Vector3.up * Mathf.Sin(50f * Mathf.Deg2Rad) + north * Mathf.Cos(50f * Mathf.Deg2Rad)).normalized;
        Matrix4x4 starRotation = Matrix4x4.Rotate(Quaternion.AngleAxis(-hour * 15f, pole));

        Shader.SetGlobalVector(SunDirId, sunDir);
        Shader.SetGlobalVector(MoonDirId, moonDir);
        Shader.SetGlobalVector(MoonRightId, moonRight);
        Shader.SetGlobalVector(MoonUpId, moonUpV);
        Shader.SetGlobalVector(SkyTopId, Lin(top));
        Shader.SetGlobalVector(SkyHorizonId, Lin(horizon));
        Shader.SetGlobalVector(SkyGroundId, Lin(ground));
        // brightness is scaled after the colour is made linear (scaling first would square it)
        Vector4 sunDisc = Lin(new Color(1f, 0.93f, 0.78f)) * (6f * Smooth(-0.04f, 0.02f, sunUp) * (1f - overcast));
        sunDisc.w = Mathf.Cos(sunSizeDegrees * 0.5f * Mathf.Deg2Rad);
        Vector4 moonDisc = Lin(new Color(0.92f, 0.94f, 1f)) * (Mathf.Lerp(0.25f, 0.9f, night) * (1f - overcast * 0.9f));
        moonDisc.w = Mathf.Tan(moonSizeDegrees * 0.5f * Mathf.Deg2Rad);
        Shader.SetGlobalVector(SunColorId, sunDisc);
        Shader.SetGlobalVector(SunGlowId, Lin(glowColor, glowStrength));
        Shader.SetGlobalVector(MoonColorId, moonDisc);
        Shader.SetGlobalVector(MoonLightId, new Vector4(Mathf.Sin(phaseAngle), 0f, Mathf.Cos(phaseAngle), MoonPhaseLight * night * (1f - overcast)));
        Shader.SetGlobalVector(StarsId, new Vector4(stars * 1.4f, 0f, 0f, 0f));
        Shader.SetGlobalMatrix(StarRotId, starRotation);
        Shader.SetGlobalVector(CloudsId, new Vector4(cover, sheet, overcast, 0f));
        Shader.SetGlobalVector(CloudLitId, Lin(cloudLit));
        Shader.SetGlobalVector(CloudShadeId, Lin(cloudShade));
        Shader.SetGlobalVector(CloudOffsetId, cloudOffset);
        Shader.SetGlobalVector(FogId, Lin(haze, Mathf.Clamp01(0.18f + fogAmount * 0.9f)));

        // ---------- the light: sun by day, moon by night ---------- //
        float dim = Mathf.Lerp(1f, 0.3f, overcast) * Mathf.Lerp(1f, 0.8f, Smooth(0.35f, 0.9f, cover));
        float sunStrength = sunIntensity * Smooth(-0.03f, 0.08f, sunUp) * Mathf.Lerp(0.55f, 1f, Smooth(0f, 0.4f, sunUp)) * dim;
        float moonStrength = moonIntensity * moonVisible * MoonPhaseLight * (1f - Smooth(-0.12f, 0.01f, sunUp))
                             * Mathf.Lerp(1f, 0.2f, overcast) * Mathf.Lerp(1f, 0.65f, Smooth(0.4f, 0.9f, cover));
        Color lightColor;
        float strength;
        if (sunStrength >= moonStrength)
        {
            Color sunColor = Color.Lerp(new Color(1f, 0.55f, 0.3f), new Color(1f, 0.95f, 0.87f), Smooth(0.0f, 0.35f, sunUp));
            lightColor = Color.Lerp(sunColor, new Color(0.82f, 0.86f, 0.92f), overcast);
            strength = sunStrength;
            if (mainLight != null)
            {
                mainLight.transform.rotation = Quaternion.LookRotation(-sunDir);
                mainLight.shadowStrength = 0.75f * (1f - overcast * 0.75f);
            }
        }
        else
        {
            lightColor = moonLightColor;
            strength = moonStrength;
            if (mainLight != null)
            {
                mainLight.transform.rotation = Quaternion.LookRotation(-moonDir);
                mainLight.shadowStrength = 0.6f * (1f - overcast);
            }
        }
        if (mainLight != null)
        {
            mainLight.color = lightColor;
            mainLight.intensity = strength;
            mainLight.enabled = strength > 0.002f;
        }

        // ---------- light from the whole sky (ambient), very low at night ---------- //
        Color daySky = new Color(0.52f, 0.6f, 0.7f), dayEquator = new Color(0.5f, 0.52f, 0.54f), dayGround = new Color(0.3f, 0.28f, 0.24f);
        Color duskSky = new Color(0.22f, 0.21f, 0.32f), duskEquator = new Color(0.34f, 0.22f, 0.18f), duskGround = new Color(0.1f, 0.08f, 0.08f);
        float n = nightAmbient;
        Color nightSky = new Color(n * 1.1f, n * 1.35f, n * 2.2f), nightEquator = new Color(n * 0.9f, n * 1.05f, n * 1.6f), nightGround = new Color(n * 0.35f, n * 0.38f, n * 0.5f);
        float dusk = Smooth(-0.2f, -0.03f, sunUp) * (1f - Smooth(0.02f, 0.25f, sunUp));
        float dayAmb = Smooth(-0.03f, 0.25f, sunUp);
        Color aSky = Color.Lerp(Color.Lerp(nightSky, duskSky, dusk), daySky, dayAmb);
        Color aEquator = Color.Lerp(Color.Lerp(nightEquator, duskEquator, dusk), dayEquator, dayAmb);
        Color aGround = Color.Lerp(Color.Lerp(nightGround, duskGround, dusk), dayGround, dayAmb);
        Color moonAmbient = new Color(0.035f, 0.045f, 0.08f) * moonGlow * (1f - overcast * 0.7f);
        aSky += moonAmbient; aEquator += moonAmbient * 0.8f; aGround += moonAmbient * 0.4f;
        // grey days: flat light from everywhere
        Color greyAmbient = Color.Lerp(nightEquator, new Color(0.5f, 0.52f, 0.56f), dayAmb);
        aSky = Color.Lerp(aSky, greyAmbient, overcast * 0.7f);
        aEquator = Color.Lerp(aEquator, greyAmbient * 0.95f, overcast * 0.7f);
        // snow on the ground throws light back up
        aGround = Color.Lerp(aGround, aEquator, snowCover * 0.7f);
        aSky.a = aEquator.a = aGround.a = 1f;
        RenderSettings.ambientSkyColor = aSky;
        RenderSettings.ambientEquatorColor = aEquator;
        RenderSettings.ambientGroundColor = aGround;
        RenderSettings.reflectionIntensity = Mathf.Lerp(0.08f, 1f, DayLight) * Mathf.Lerp(1f, 0.55f, overcast);

        Color lightAtGround = aEquator + lightColor * strength * 0.55f;
        LightLevel = new Color(Mathf.Clamp01(lightAtGround.r), Mathf.Clamp01(lightAtGround.g), Mathf.Clamp01(lightAtGround.b), 1f);

        // ---------- fog ---------- //
        bool underwater = PlayerWater.Instance != null && PlayerWater.Instance.IsHeadUnderwater;
        if (!underwater)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = haze;
            RenderSettings.fogDensity = Mathf.Lerp(0.00035f, 0.0065f, fogAmount) + snowFall * 0.0025f + rain * 0.001f;
        }
        else
        {
            // under water at night it is dark too
            Color water = PlayerWater.Instance.underwaterColor * Mathf.Lerp(0.15f, 1f, DayLight);
            RenderSettings.fogColor = water;
            var cam = Camera.main;
            if (cam != null && cam.clearFlags == CameraClearFlags.SolidColor) cam.backgroundColor = water;
        }

        // ---------- the sea's copy of the sky: one side per frame (every third frame on a phone) ---------- //
        bool phone = GameInput.TouchControls || Application.isMobilePlatform;
        if (skyCamera != null && skyCube != null && (!phone || Time.frameCount % 3 == 0))
        {
            skyCamera.RenderToCubemap(skyCube, 1 << skyFace);
            skyFace = (skyFace + 1) % 6;
        }
    }
}
