using UnityEngine;

// Weather that changes by itself. Every few game hours the next weather is picked at random, with chances that
// depend on the season (autumn rains the most, winter snows, summer is mostly clear with a few storms).
// Nothing changes suddenly: the clouds gather first, then the rain or snow starts, and it all fades the
// same way. Snow stays on the ground, builds up over a few hours of snowfall and melts in spring.
//
// The other scripts only read the values below (SkyLighting: sky, light and fog, WeatherEffects: rain and
// snow particles and sounds, SnowCover: white ground, Campfire: burns faster in the rain).
public class WeatherSystem : MonoBehaviour
{
    public static WeatherSystem Instance { get; private set; }

    public enum WeatherType { Clear, Cloudy, Rain, Storm, Snow }

    [System.Serializable]
    public class SeasonChances
    {
        [Range(0f, 1f)] public float clear;
        [Range(0f, 1f)] public float cloudy;
        [Range(0f, 1f)] public float rain;
        [Range(0f, 1f)] public float storm;
        [Range(0f, 1f)] public float snow;

        public SeasonChances() { }
        public SeasonChances(float clear, float cloudy, float rain, float storm, float snow)
        {
            this.clear = clear; this.cloudy = cloudy; this.rain = rain; this.storm = storm; this.snow = snow;
        }
    }

    [Header("Chance of each weather, per season (they do not have to add up to 1)")]
    public SeasonChances spring = new SeasonChances(0.40f, 0.30f, 0.25f, 0.05f, 0f);
    public SeasonChances summer = new SeasonChances(0.60f, 0.22f, 0.08f, 0.10f, 0f);
    public SeasonChances autumn = new SeasonChances(0.18f, 0.30f, 0.40f, 0.12f, 0f);
    public SeasonChances winter = new SeasonChances(0.28f, 0.30f, 0f, 0f, 0.42f);

    [Header("Timing (game hours)")]
    [Tooltip("Shortest time one weather lasts")]
    public float minHours = 3f;
    [Tooltip("Longest time one weather lasts")]
    public float maxHours = 9f;
    [Tooltip("How long the sky takes to change from one weather to the next")]
    public float changeHours = 1.5f;
    [Tooltip("Hours of strong snowfall until everything is white")]
    public float hoursToCoverWithSnow = 3f;
    [Tooltip("Hours for a full snow cover to melt in spring (faster in rain, much faster in summer)")]
    public float springMeltHours = 16f;
    [Tooltip("Hours for the ground to dry after the rain")]
    public float dryHours = 3f;

    // Old fields of the first version (the scene still has them). The old rain object is switched off.
    public GameObject rainEffect;
    public Material rainSkyBox;
    [Tooltip("True while it rains or snows (read by the old day/night skyboxes)")]
    public bool isSpecialWeather;

    // ---------- TEST settings (set from the in-game Settings > Test panel; remove for release) ---------- //
    [Header("TEST")]
    [Tooltip("TEST: off = the weather below stays, it does not change by itself")]
    public bool autoWeather = true;
    [Tooltip("TEST: the weather kept while Auto Weather is off")]
    public WeatherType testWeather = WeatherType.Clear;

    // ---------- the weather now (read by the other scripts) ---------- //
    public WeatherType Current { get; private set; } = WeatherType.Clear;
    public float CloudCover { get; private set; } = 0.2f;    // 0 clear blue .. 1 covered
    public float CloudSheet { get; private set; }            // 0 .. 1 one grey sheet of cloud (rain, storm)
    public float Rain { get; private set; }                   // 0 .. 1 rain strength
    public float Snow { get; private set; }                   // 0 .. 1 snowfall strength
    public float Fog { get; private set; } = 0.05f;          // 0 .. 1 haze
    public float Wind { get; private set; } = 0.2f;          // 0 .. 1
    public Vector2 WindDirection { get; private set; } = new Vector2(0.8f, 0.6f);
    public float SnowCover { get; private set; }             // 0 .. 1 snow lying on the ground
    public float Wetness { get; private set; }               // 0 .. 1 wet ground after rain
    public float HoursLeft => periodLeft;                     // until the next change (auto weather)

    struct Look { public float cloud, sheet, rain, snow, fog, wind; }

    Look target;
    float periodLeft;
    float strength = 0.5f;          // how strong this weather is (picked at random)
    TimeManager.Season lastSeason;
    float windAngle;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        if (rainEffect != null) rainEffect.SetActive(false); // the old rain particles (WeatherEffects makes new ones)
        lastSeason = Season;
        windAngle = Random.Range(0f, 360f);

        // the first weather of a new game: mostly fair
        PickNext(Season == TimeManager.Season.Winter ? WeatherType.Cloudy : (Random.value < 0.7f ? WeatherType.Clear : WeatherType.Cloudy));
        Look now = LookOf(Current, strength);
        CloudCover = now.cloud; CloudSheet = now.sheet; Rain = now.rain; Snow = now.snow; Fog = now.fog; Wind = now.wind;
        if (Season == TimeManager.Season.Winter) SnowCover = 0.6f; // a game that starts in winter already has some snow
    }

    static TimeManager.Season Season => TimeManager.Instance != null ? TimeManager.Instance.currentSeason : TimeManager.Season.Spring;

    void Update()
    {
        float hours = Time.deltaTime * DayNightSystem.WeatherHoursPerSecond;

        if (autoWeather)
        {
            periodLeft -= hours;
            if (periodLeft <= 0f) PickNext(null);

            // the season changed in the middle of a rain or snow: rain turns to snow in winter and back
            if (Season != lastSeason)
            {
                if (Season == TimeManager.Season.Winter && (Current == WeatherType.Rain || Current == WeatherType.Storm)) PickNext(WeatherType.Snow);
                else if (Season != TimeManager.Season.Winter && Current == WeatherType.Snow) PickNext(WeatherType.Rain);
            }
        }
        else if (Current != testWeather)
        {
            SetCurrent(testWeather, 0.75f, 999f); // TEST
        }
        lastSeason = Season;

        // move slowly towards the look of the current weather (fast while testing: 4 real seconds)
        float step = autoWeather ? hours / Mathf.Max(0.05f, changeHours) : Time.deltaTime / 4f;
        CloudCover = Mathf.MoveTowards(CloudCover, target.cloud, step);
        CloudSheet = Mathf.MoveTowards(CloudSheet, target.sheet, step);
        Fog = Mathf.MoveTowards(Fog, target.fog, step);
        Wind = Mathf.MoveTowards(Wind, target.wind, step);
        // rain and snow only fall from thick clouds
        float thick = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 0.9f, CloudCover));
        Rain = Mathf.MoveTowards(Rain, target.rain * thick, step * 1.5f);
        Snow = Mathf.MoveTowards(Snow, target.snow * thick, step * 1.5f);

        // the wind turns slowly
        windAngle += hours * Random.Range(-12f, 18f);
        WindDirection = new Vector2(Mathf.Cos(windAngle * Mathf.Deg2Rad), Mathf.Sin(windAngle * Mathf.Deg2Rad));

        UpdateGround(hours);
        isSpecialWeather = Rain > 0.1f || Snow > 0.1f;
    }

    void UpdateGround(float hours)
    {
        bool sunUp = DayNightSystem.Instance != null && DayNightSystem.Instance.Hour > 7f && DayNightSystem.Instance.Hour < 18f;

        // snow builds up while it snows, and melts outside winter
        if (Snow > 0.02f)
        {
            SnowCover = Mathf.Min(1f, SnowCover + Snow * hours / Mathf.Max(0.1f, hoursToCoverWithSnow));
        }
        else if (SnowCover > 0f && Season != TimeManager.Season.Winter)
        {
            float meltHours = Season == TimeManager.Season.Spring ? springMeltHours : springMeltHours * 0.25f;
            float speed = (1f + Rain * 2f) * (sunUp && CloudCover < 0.6f ? 1.5f : 1f);
            float before = SnowCover;
            SnowCover = Mathf.Max(0f, SnowCover - speed * hours / Mathf.Max(0.1f, meltHours));
            Wetness = Mathf.Min(1f, Wetness + (before - SnowCover) * 3f); // melting snow leaves wet ground
        }

        // wet ground after rain, drying slowly (faster in the sun)
        if (Rain > 0.05f) Wetness = Mathf.Min(1f, Wetness + Rain * hours / 0.4f);
        else Wetness = Mathf.Max(0f, Wetness - hours / Mathf.Max(0.1f, dryHours) * (sunUp ? 1.5f : 0.6f));
    }

    // Picks the next weather (or uses 'forced'). Clear never turns into rain at once: clouds come first.
    void PickNext(WeatherType? forced)
    {
        WeatherType next = forced ?? RandomWeather();
        bool wet = next == WeatherType.Rain || next == WeatherType.Storm || next == WeatherType.Snow;
        if (!forced.HasValue && wet && Current == WeatherType.Clear)
        {
            SetCurrent(WeatherType.Cloudy, Random.Range(0.4f, 0.8f), Random.Range(1f, 2.5f));
            return;
        }
        SetCurrent(next, Random.Range(0.25f, 1f), Random.Range(minHours, maxHours));
    }

    void SetCurrent(WeatherType type, float newStrength, float hours)
    {
        Current = type;
        strength = newStrength;
        periodLeft = hours;
        target = LookOf(type, strength);
    }

    WeatherType RandomWeather()
    {
        SeasonChances c = ChancesFor(Season);
        float total = c.clear + c.cloudy + c.rain + c.storm + c.snow;
        if (total <= 0f) return WeatherType.Clear;
        float r = Random.value * total;
        if ((r -= c.clear) < 0f) return WeatherType.Clear;
        if ((r -= c.cloudy) < 0f) return WeatherType.Cloudy;
        if ((r -= c.rain) < 0f) return WeatherType.Rain;
        if ((r -= c.storm) < 0f) return WeatherType.Storm;
        return WeatherType.Snow;
    }

    SeasonChances ChancesFor(TimeManager.Season season)
    {
        switch (season)
        {
            case TimeManager.Season.Summer: return summer;
            case TimeManager.Season.Fall: return autumn;
            case TimeManager.Season.Winter: return winter;
            default: return spring;
        }
    }

    static Look LookOf(WeatherType type, float s)
    {
        switch (type)
        {
            case WeatherType.Cloudy: return new Look { cloud = 0.5f + 0.3f * s, sheet = 0.12f * s, fog = 0.1f + 0.08f * s, wind = 0.3f + 0.2f * s };
            case WeatherType.Rain: return new Look { cloud = 0.96f, sheet = 0.7f + 0.15f * s, rain = 0.4f + 0.4f * s, fog = 0.35f + 0.2f * s, wind = 0.35f + 0.25f * s };
            case WeatherType.Storm: return new Look { cloud = 1f, sheet = 0.95f, rain = 0.9f + 0.1f * s, fog = 0.55f + 0.1f * s, wind = 0.85f + 0.15f * s };
            case WeatherType.Snow: return new Look { cloud = 0.95f, sheet = 0.65f + 0.15f * s, snow = 0.45f + 0.55f * s, fog = 0.45f + 0.25f * s, wind = 0.2f + 0.3f * s };
            default: return new Look { cloud = 0.2f + 0.25f * s, sheet = 0f, fog = 0.04f, wind = 0.12f + 0.15f * s };
        }
    }

    // ---------- TEST helpers (used by the in-game Settings > Test panel) ---------- //

    // TEST: null = automatic weather again
    public void SetTestWeather(WeatherType? type)
    {
        if (type.HasValue)
        {
            autoWeather = false;
            testWeather = type.Value;
            SetCurrent(type.Value, 0.75f, 999f);
        }
        else
        {
            autoWeather = true;
            PickNext(null);
        }
    }

    // TEST: put snow on the ground at once (0..1)
    public void SetSnowCover(float value)
    {
        SnowCover = Mathf.Clamp01(value);
    }

    // TEST: jump straight to the look of the current weather (no slow change)
    public void SnapNow()
    {
        CloudCover = target.cloud; CloudSheet = target.sheet; Fog = target.fog; Wind = target.wind;
        Rain = target.rain; Snow = target.snow;
    }

    // TEST: set the wet ground at once (0..1)
    public void SetWetness(float value)
    {
        Wetness = Mathf.Clamp01(value);
    }
}
