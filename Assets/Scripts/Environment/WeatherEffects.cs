using UnityEngine;

// What you see and hear of the weather around the player: rain streaks with little splashes on the ground,
// falling snowflakes drifting in the wind, the sound of rain and of the wind. The amounts come from WeatherSystem.
// Phones get fewer particles than PCs.
public class WeatherEffects : MonoBehaviour
{
    ParticleSystem rain, splashes, snow;
    ParticleSystemRenderer rainRenderer, snowRenderer, splashRenderer;
    Material rainMat, snowMat, splashMat;
    AudioSource rainAudio, windAudio;
    Transform follow;
    Terrain terrain;
    bool phone;
    float splashCarry;

    // particles per second at full strength
    float RainRate => phone ? 1100f : 2400f;
    float SnowRate => phone ? 380f : 800f;
    float SplashRate => phone ? 45f : 110f;

    static readonly int TintId = Shader.PropertyToID("_Tint");

    void Start()
    {
        phone = GameInput.TouchControls || Application.isMobilePlatform;
        terrain = Terrain.activeTerrain;

        var shader = Resources.Load<Shader>("Environment/SB_Particle");
        rainMat = NewMaterial(shader, EnvTextures.Streak, 1f, 1f, 0f);
        snowMat = NewMaterial(shader, EnvTextures.Flake, 1f, 1f, 0.3f);
        splashMat = NewMaterial(shader, EnvTextures.Splash, 1f, 1f, 0f);

        rain = MakeRain();
        snow = MakeSnow();
        splashes = MakeSplashes();

        rainAudio = MakeLoop("Rain sound", ProceduralAudio.Rain);
        windAudio = MakeLoop("Wind sound", ProceduralAudio.Wind);
    }

    static Material NewMaterial(Shader shader, Texture texture, float intensity, float opacity, float softFade)
    {
        var m = new Material(shader) { name = "SB_Weather_" + texture.name, hideFlags = HideFlags.DontSave };
        m.SetTexture("_MainTex", texture);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Opacity", opacity);
        m.SetFloat("_SoftFade", softFade);
        return m;
    }

    ParticleSystem NewSystem(string name, Material material, int max, out ParticleSystemRenderer renderer)
    {
        var go = new GameObject(name);
        go.SetActive(false); // set up before it starts, so it never shows Unity's default particles
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        var main = ps.main;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;
        main.playOnAwake = true;
        main.startSpeed = 0f;
        main.scalingMode = ParticleSystemScalingMode.Local;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        return ps;
    }

    ParticleSystem MakeRain()
    {
        var ps = NewSystem("Rain", rainMat, phone ? 1500 : 3200, out rainRenderer);
        var main = ps.main;
        main.startLifetime = 1.25f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.045f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.8f, 0.85f, 0.95f, 0.45f), new Color(0.9f, 0.95f, 1f, 0.7f));
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(32f, 0.1f, 32f);
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-15f, -12f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        rainRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        rainRenderer.lengthScale = 1f;
        rainRenderer.velocityScale = 0.06f;
        rainRenderer.sortingFudge = 20f;
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeSnow()
    {
        var ps = NewSystem("Snow", snowMat, phone ? 2600 : 6000, out snowRenderer);
        var main = ps.main;
        main.startLifetime = 8f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 1f, 1f, 0.75f), new Color(1f, 1f, 1f, 1f));
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(40f, 0.1f, 40f);
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-1.7f, -1.0f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.6f;
        noise.frequency = 0.35f;
        noise.scrollSpeed = 0.25f;
        noise.octaveCount = 1;
        noise.quality = phone ? ParticleSystemNoiseQuality.Low : ParticleSystemNoiseQuality.Medium;
        noise.damping = true;
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = Fade(0.08f, 0.9f);
        snowRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        snowRenderer.sortingFudge = 20f;
        snowRenderer.maxParticleSize = 0.05f;
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeSplashes()
    {
        var ps = NewSystem("Rain splashes", splashMat, phone ? 120 : 300, out splashRenderer);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.26f);
        main.startColor = new Color(0.85f, 0.9f, 1f, 0.55f);
        var shape = ps.shape;
        shape.enabled = false;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.2f)));
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        colorOverLife.color = Fade(0.05f, 0.4f);
        splashRenderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
        splashRenderer.pivot = new Vector3(0f, 0.5f, 0f); // stands on the ground
        splashRenderer.sortingFudge = 15f;
        ps.gameObject.SetActive(true);
        return ps;
    }

    // fades in during the first part of its life and out after 'outStart'
    public static ParticleSystem.MinMaxGradient Fade(float inEnd, float outStart)
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, inEnd), new GradientAlphaKey(1f, outStart), new GradientAlphaKey(0f, 1f) });
        return new ParticleSystem.MinMaxGradient(g);
    }

    AudioSource MakeLoop(string name, AudioClip clip)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    void LateUpdate()
    {
        var weather = WeatherSystem.Instance;
        if (weather == null || rain == null) return;
        if (follow == null)
        {
            var cam = Camera.main;
            if (cam == null) return;
            follow = cam.transform;
        }

        bool underwater = PlayerWater.Instance != null && PlayerWater.Instance.IsHeadUnderwater;
        float rainAmount = underwater ? 0f : weather.Rain;
        float snowAmount = underwater ? 0f : weather.Snow;
        Vector2 wind = weather.WindDirection * weather.Wind;

        // the rain and snow fall around the camera, a little ahead of it (where you look)
        Vector3 ahead = follow.forward;
        ahead.y = 0f;
        ahead = ahead.sqrMagnitude > 0.001f ? ahead.normalized : Vector3.zero;
        Vector3 basePos = follow.position + ahead * 7f;
        rain.transform.position = basePos + Vector3.up * 12f - new Vector3(wind.x, 0f, wind.y) * 6f;
        snow.transform.position = basePos + Vector3.up * 10f - new Vector3(wind.x, 0f, wind.y) * 10f;

        var rainEmission = rain.emission;
        rainEmission.rateOverTime = RainRate * rainAmount;
        var rainVelocity = rain.velocityOverLifetime;
        rainVelocity.x = new ParticleSystem.MinMaxCurve(wind.x * 5f, wind.x * 5f); // all three axes must use the same kind of value
        rainVelocity.z = new ParticleSystem.MinMaxCurve(wind.y * 5f, wind.y * 5f);

        var snowEmission = snow.emission;
        snowEmission.rateOverTime = SnowRate * snowAmount;
        var snowVelocity = snow.velocityOverLifetime;
        snowVelocity.x = new ParticleSystem.MinMaxCurve(wind.x * 2.2f, wind.x * 2.2f);
        snowVelocity.z = new ParticleSystem.MinMaxCurve(wind.y * 2.2f, wind.y * 2.2f);

        // particles take the light of the scene (dark at night)
        Color light = SkyLighting.Instance != null ? SkyLighting.Instance.LightLevel : Color.white;
        rainMat.SetColor(TintId, new Color(light.r * 0.9f + 0.05f, light.g * 0.92f + 0.05f, light.b + 0.06f, 1f));
        snowMat.SetColor(TintId, new Color(light.r * 1.1f + 0.06f, light.g * 1.1f + 0.06f, light.b * 1.1f + 0.08f, 1f));
        splashMat.SetColor(TintId, new Color(light.r + 0.05f, light.g + 0.05f, light.b + 0.06f, 1f));

        EmitSplashes(rainAmount);

        // sounds
        SetLoop(rainAudio, underwater ? 0f : Mathf.Clamp01(rainAmount * 1.2f) * 0.55f);
        float windLoud = Mathf.Clamp01((weather.Wind - 0.3f) / 0.7f) * (underwater ? 0f : 0.35f);
        SetLoop(windAudio, windLoud);
    }

    // little splashes where the rain hits the ground (or the sea) near the player
    void EmitSplashes(float rainAmount)
    {
        if (rainAmount < 0.05f) return;
        splashCarry += SplashRate * rainAmount * Time.deltaTime;
        int count = Mathf.FloorToInt(splashCarry);
        splashCarry -= count;
        if (count <= 0) return;
        if (terrain == null) terrain = Terrain.activeTerrain;
        float sea = Ocean.Instance != null ? Ocean.Instance.Level : float.MinValue;
        Vector3 forward = follow.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;
        var emit = new ParticleSystem.EmitParams();
        for (int i = 0; i < count; i++)
        {
            // mostly in front of the camera, where they can be seen
            Vector2 r = Random.insideUnitCircle * 14f;
            Vector3 p = follow.position + forward * (r.y * 0.6f + 8f) + Vector3.Cross(Vector3.up, forward) * r.x;
            float ground = terrain != null ? terrain.SampleHeight(p) + terrain.transform.position.y : follow.position.y - 1.7f;
            p.y = Mathf.Max(ground, sea) + 0.02f;
            emit.position = p;
            emit.applyShapeToPosition = false;
            splashes.Emit(emit, 1);
        }
    }

    static void SetLoop(AudioSource source, float volume)
    {
        if (source == null) return;
        source.volume = Mathf.MoveTowards(source.volume, volume, Time.deltaTime * 0.5f);
        if (source.volume > 0.001f && !source.isPlaying) source.Play();
        else if (source.volume <= 0.001f && source.isPlaying) source.Stop();
    }
}
