using UnityEngine;

// Forest sounds, made in code when the game scene loads (EnvironmentBootstrap):
//  - birds and a light wind by day, crickets at night, blending at dawn and dusk (follows the sun: SkyLighting.DayLight)
//  - fewer birds in the rain and in winter, no crickets in winter or when snow lies on the ground
//  - now and then an owl at night, somewhere around you
//  - the sea when you are near the beach
//  - everything is muffled when your head is under water
// Day and night sounds are free CC0 recordings (Assets/Resources/Sounds/Ambience, see SOUND_CREDITS.txt);
// the owl and the sea are made in code. The "Music" volume of the Settings controls these sounds.
public class AmbienceAudio : MonoBehaviour
{
    public float dayVolume = 0.38f;
    public float nightVolume = 0.65f; // the crickets recording is quiet
    public float seaVolume = 0.32f;
    public float owlVolume = 0.55f;

    LoopPlayer day, night, sea;
    AudioSource owl;
    float nextOwl;
    float dayLight = 1f;

    void Start()
    {
        day = new LoopPlayer(gameObject, "Day birds", Resources.Load<AudioClip>("Sounds/Ambience/DayBirdsAndWind"), 2.5f);
        night = new LoopPlayer(gameObject, "Night crickets", Resources.Load<AudioClip>("Sounds/Ambience/NightCrickets"), 1.5f);
        sea = new LoopPlayer(gameObject, "Sea", ProceduralAudio.Waves, 0f); // already a seamless loop

        var owlObject = new GameObject("Owl");
        owlObject.transform.SetParent(transform, false);
        owl = owlObject.AddComponent<AudioSource>();
        owl.playOnAwake = false;
        owl.spatialBlend = 1f;
        owl.rolloffMode = AudioRolloffMode.Linear;
        owl.minDistance = 10f;
        owl.maxDistance = 90f;
        owl.dopplerLevel = 0f;
        owlObject.AddComponent<AudioLowPassFilter>().cutoffFrequency = 5000f;

        nextOwl = Time.time + Random.Range(20f, 45f);
        if (SkyLighting.Instance != null) dayLight = SkyLighting.Instance.DayLight;
    }

    void Update()
    {
        var sky = SkyLighting.Instance;
        var weather = WeatherSystem.Instance;
        var listener = Camera.main != null ? Camera.main.transform : transform;

        float target = sky != null ? sky.DayLight : 1f;
        dayLight = Mathf.MoveTowards(dayLight, target, Time.deltaTime * 0.2f);

        float rain = weather != null ? weather.Rain : 0f;
        float snowCover = weather != null ? weather.SnowCover : 0f;
        bool winter = TimeManager.Instance != null && TimeManager.Instance.currentSeason == TimeManager.Season.Winter;
        bool underwater = PlayerWater.Instance != null && PlayerWater.Instance.IsHeadUnderwater;
        float master = SoundManager.AmbienceVolume;

        float birds = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.15f, 0.7f, dayLight)) * (1f - 0.75f * rain) * (winter ? 0.45f : 1f);
        float crickets = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 0.05f, dayLight)) * (1f - 0.7f * rain)
            * (winter ? 0f : 1f) * (1f - Mathf.Clamp01(snowCover * 3f));

        float shore = 0f;
        var ocean = Ocean.Instance;
        if (ocean != null)
        {
            float distance = ocean.DistanceFromShore(listener.position); // negative on land
            shore = distance >= 0f ? 1f : Mathf.Clamp01(1f + distance / 80f);
        }

        float cutoff = underwater ? 500f : 22000f;
        // the forest goes quiet while the night shadow hunts (its music plays instead)
        float shadowQuiet = 1f - 0.8f * NightShadow.Presence;
        day.Update(dayVolume * birds * master * shadowQuiet, cutoff);
        night.Update(nightVolume * crickets * master * shadowQuiet, cutoff);
        sea.Update(seaVolume * shore * master, cutoff);

        UpdateOwl(listener, master, rain, underwater);
    }

    void UpdateOwl(Transform listener, float master, float rain, bool underwater)
    {
        if (Time.time < nextOwl) return;
        nextOwl = Time.time + Random.Range(25f, 70f);
        if (dayLight > 0.2f || rain > 0.4f || underwater || owl.isPlaying || NightShadow.Presence > 0.05f) return;

        // somewhere in the trees around the player
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(25f, 45f);
        owl.transform.position = listener.position + new Vector3(Mathf.Cos(angle) * distance, Random.Range(6f, 14f), Mathf.Sin(angle) * distance);
        var hoots = ProceduralAudio.OwlHoots;
        owl.clip = hoots[Random.Range(0, hoots.Length)];
        owl.pitch = Random.Range(0.95f, 1.05f);
        owl.volume = owlVolume * master;
        owl.Play();
    }

    // A looping sound without a click where it starts again: two sources take turns and blend for a moment
    // (crossfade seconds) at the end of the recording. With crossfade 0 it is a plain loop.
    class LoopPlayer
    {
        readonly AudioSource a, b;
        readonly AudioLowPassFilter filterA, filterB;
        readonly AudioClip clip;
        readonly double length;
        readonly float crossfade;
        AudioSource current, next;
        double startTime;
        bool nextScheduled;
        float volume;

        public LoopPlayer(GameObject owner, string name, AudioClip clip, float crossfade)
        {
            this.clip = clip;
            this.crossfade = clip != null ? Mathf.Min(crossfade, clip.length * 0.25f) : 0f;
            length = clip != null ? clip.length : 0;
            var holder = new GameObject(name);
            holder.transform.SetParent(owner.transform, false);
            a = NewSource(holder, "A", out filterA);
            if (this.crossfade > 0f) b = NewSource(holder, "B", out filterB);
        }

        // each source on its own object, with its own filter (a filter works on the source of its object)
        AudioSource NewSource(GameObject holder, string name, out AudioLowPassFilter filter)
        {
            var go = new GameObject(name);
            go.transform.SetParent(holder.transform, false);
            var source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = crossfade <= 0f;
            source.volume = 0f;
            filter = go.AddComponent<AudioLowPassFilter>();
            filter.cutoffFrequency = 22000f;
            return source;
        }

        public void Update(float targetVolume, float cutoff)
        {
            if (clip == null) return;
            volume = Mathf.MoveTowards(volume, targetVolume, Time.deltaTime * 0.25f);
            filterA.cutoffFrequency = Mathf.Lerp(filterA.cutoffFrequency, cutoff, Time.deltaTime * 6f);
            if (filterB != null) filterB.cutoffFrequency = filterA.cutoffFrequency;

            bool audible = volume > 0.002f;
            if (!audible)
            {
                if (a.isPlaying) a.Stop();
                if (b != null && b.isPlaying) b.Stop();
                current = null;
                return;
            }

            if (b == null) // plain loop
            {
                if (!a.isPlaying) a.Play();
                a.volume = volume;
                return;
            }

            double now = AudioSettings.dspTime;
            if (current == null)
            {
                current = a;
                next = b;
                // start somewhere in the recording, so it does not always begin the same way
                // (use the chosen offset itself: AudioSource.time does not change before the source plays)
                float offset = Random.Range(0f, (float)length * 0.6f);
                current.time = offset;
                startTime = now - offset;
                current.Play();
                nextScheduled = false;
            }

            double nextStart = startTime + length - crossfade;
            if (!nextScheduled && now > nextStart - 1.0)
            {
                next.time = 0f;
                next.PlayScheduled(System.Math.Max(nextStart, now + 0.02));
                nextScheduled = true;
            }

            float fade = nextScheduled ? Mathf.Clamp01((float)((now - nextStart) / crossfade)) : 0f;
            current.volume = volume * Mathf.Sqrt(1f - fade);
            next.volume = volume * Mathf.Sqrt(fade);

            if (now >= startTime + length) // the old one has finished: swap roles
            {
                startTime = nextStart;
                var finished = current;
                current = next;
                next = finished;
                next.Stop();
                nextScheduled = false;
            }
        }
    }
}
