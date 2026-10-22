using TMPro;
using UnityEngine;

// The look and sound of a burning campfire, made in code (Campfire adds it):
//  - flames: several layers of flickering, randomly shaped tongues that lean with the wind
//  - a bright core at the bottom and a soft glow around the fire (it looks good at night)
//  - sparks flying up in random bursts, smoke rising and drifting with the wind
//  - glowing coals that stay a while after the fire goes out
//  - a warm, flickering light and a crackling sound
// The size of the fire follows Campfire.FireStrength (small when the wood is almost gone or in the rain).
// Phones get fewer particles and a light without shadows.
[DisallowMultipleComponent]
public class CampfireFX : MonoBehaviour
{
    Campfire campfire;
    Transform root;
    ParticleSystem flames, core, glow, sparks, smoke, coals;
    Light fireLight;
    AudioSource crackle;
    float size = 1f;          // 1 = a campfire about 1.2 m wide
    float groundOffset;       // the ground, measured from the fire's base (the base sits on the lowest logs)
    Transform ash;
    float ashLevel;
    bool ashPlaced;
    bool grassCleared;
    static Material ashMaterial;
    public float Size => size;
    public Vector3 Centre => root != null ? root.position : transform.position; // bottom middle of the fire
    TextMeshPro label, labelShadow;
    int shownSeconds = int.MinValue;
    string shownWarning = "";
    float level;              // how big the fire is now (0 = out)
    float embers;             // glowing coals left after the fire went out (1 .. 0)
    float sparkTimer;
    float seed;
    bool phone;
    bool playing;

    static Material flameMat, glowMat, sparkMat, smokeMat, coalMat;
    static readonly int TintId = Shader.PropertyToID("_Tint");

    public void Init(Campfire owner)
    {
        campfire = owner;
        phone = GameInput.TouchControls || Application.isMobilePlatform;
        seed = Random.value * 100f;
        MakeMaterials();
        FindPlaceAndSize(out Vector3 basePosition);

        // the effects do not scale with the model (it is scaled 2x), so 1 unit = 1 metre here
        var go = new GameObject("Fire FX (made in code)");
        root = go.transform;
        root.SetParent(transform, false);
        root.position = basePosition;
        root.rotation = Quaternion.identity;
        Vector3 lossy = transform.lossyScale;
        root.localScale = new Vector3(1f / Mathf.Max(0.001f, lossy.x), 1f / Mathf.Max(0.001f, lossy.y), 1f / Mathf.Max(0.001f, lossy.z));

        flames = MakeFlames();
        core = MakeCore();
        glow = MakeGlow();
        sparks = MakeSparks();
        smoke = MakeSmoke();
        coals = MakeCoals();
        MakeLight();
        MakeSound();
        MakeLabel();
        MakeAsh();
        SetPlaying(false);
    }

    // The fire sits on the wood of the model (the "Stick..." parts), at the old effect's place.
    void FindPlaceAndSize(out Vector3 basePosition)
    {
        bool found = false;
        var bounds = new Bounds();
        foreach (var r in GetComponentsInChildren<MeshRenderer>(true))
        {
            if (!r.name.StartsWith("Stick")) continue;
            if (!found) { bounds = r.bounds; found = true; }
            else bounds.Encapsulate(r.bounds);
        }
        Vector3 centre = campfire.fire != null ? campfire.fire.transform.position : transform.position;
        if (found)
        {
            size = Mathf.Clamp(Mathf.Max(bounds.size.x, bounds.size.z) / 1.2f, 0.5f, 2.2f);
            basePosition = new Vector3(bounds.center.x, bounds.min.y + 0.14f * size, bounds.center.z); // on top of the lowest logs
            groundOffset = -0.14f * size;
        }
        else
        {
            size = 1f;
            basePosition = centre;
        }
    }

    static void MakeMaterials()
    {
        if (flameMat != null) return;
        var shader = Resources.Load<Shader>("Environment/SB_Particle");
        flameMat = NewMaterial(shader, EnvTextures.Flames, 1.6f, 0.42f, 0.25f); // half see-through: still visible on snow and in daylight
        glowMat = NewMaterial(shader, EnvTextures.Dot, 1f, 0f, 0.6f);
        sparkMat = NewMaterial(shader, EnvTextures.Dot, 3f, 0f, 0f);
        smokeMat = NewMaterial(shader, EnvTextures.Smoke, 1f, 1f, 0.8f);
        coalMat = NewMaterial(shader, EnvTextures.Dot, 1.7f, 0f, 0.05f);
    }

    static Material NewMaterial(Shader shader, Texture texture, float intensity, float opacity, float softFade)
    {
        var m = new Material(shader) { name = "SB_Fire_" + texture.name, hideFlags = HideFlags.DontSave };
        m.SetTexture("_MainTex", texture);
        m.SetFloat("_Intensity", intensity);
        m.SetFloat("_Opacity", opacity);
        m.SetFloat("_SoftFade", softFade);
        return m;
    }

    ParticleSystem NewSystem(string name, Material material, int max, float sortingFudge, ParticleSystemRenderMode mode)
    {
        var go = new GameObject(name);
        go.SetActive(false); // set up first, so it never shows Unity's default particles
        go.transform.SetParent(root, false);
        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = mode;
        renderer.sortingFudge = sortingFudge;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        renderer.maxParticleSize = 1f;

        var main = ps.main;
        main.maxParticles = max;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.loop = true;
        main.playOnAwake = false;
        main.startSpeed = 0f;
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.rotation = new Vector3(90f, 0f, 0f); // lying flat
        shape.radiusThickness = 1f;
        return ps;
    }

    static ParticleSystem.MinMaxGradient Gradient(Color[] colors, float[] colorTimes, float[] alphas, float[] alphaTimes)
    {
        var g = new Gradient();
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], colorTimes[i]);
        var ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i], alphaTimes[i]);
        g.SetKeys(ck, ak);
        return new ParticleSystem.MinMaxGradient(g);
    }

    static AnimationCurve Curve(params float[] timeValue)
    {
        var keys = new Keyframe[timeValue.Length / 2];
        for (int i = 0; i < keys.Length; i++) keys[i] = new Keyframe(timeValue[i * 2], timeValue[i * 2 + 1]);
        var c = new AnimationCurve(keys);
        for (int i = 0; i < keys.Length; i++) c.SmoothTangents(i, 0f);
        return c;
    }

    // flame shapes: a random one of the 8 drawn in EnvTextures.Flames, kept for the particle's life
    static void UseFlameShapes(ParticleSystem ps)
    {
        var sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.numTilesX = EnvTextures.FlameColumns;
        sheet.numTilesY = EnvTextures.FlameRows;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, 0.999f);
        sheet.cycleCount = 1;
    }

    ParticleSystem MakeFlames()
    {
        var ps = NewSystem("Flames", flameMat, phone ? 40 : 60, -5f, ParticleSystemRenderMode.VerticalBillboard);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.4f * size, 0.75f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        var shape = ps.shape;
        shape.radius = 0.26f * size;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.9f * size, 1.7f * size);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f * size;
        noise.frequency = 1.6f / size;
        noise.scrollSpeed = 1.8f;
        noise.octaveCount = phone ? 1 : 2;
        noise.quality = phone ? ParticleSystemNoiseQuality.Low : ParticleSystemNoiseQuality.Medium;
        noise.damping = true;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.6f, 0.25f, 1f, 0.7f, 0.55f, 1f, 0.1f));
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.9f, 0.9f);
        var color = ps.colorOverLifetime;
        color.enabled = true;
        // bright yellow at the bottom, orange, then a deep red tip that is gone before it rises too high
        color.color = Gradient(
            new[] { new Color(1f, 0.92f, 0.72f), new Color(1f, 0.64f, 0.24f), new Color(0.95f, 0.32f, 0.07f), new Color(0.5f, 0.08f, 0.02f) },
            new[] { 0f, 0.3f, 0.65f, 1f },
            new[] { 0f, 1f, 0.8f, 0.12f, 0f },
            new[] { 0f, 0.08f, 0.45f, 0.75f, 1f });
        UseFlameShapes(ps);
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeCore()
    {
        var ps = NewSystem("Flame core", flameMat, phone ? 16 : 24, -6f, ParticleSystemRenderMode.VerticalBillboard);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f * size, 0.55f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        var shape = ps.shape;
        shape.radius = 0.16f * size;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.6f * size, 1.2f * size);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.7f, 0.3f, 1f, 1f, 0.4f));
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Gradient(
            new[] { new Color(1f, 0.97f, 0.85f), new Color(1f, 0.8f, 0.4f), new Color(1f, 0.5f, 0.15f) },
            new[] { 0f, 0.5f, 1f },
            new[] { 0f, 1f, 0f },
            new[] { 0f, 0.15f, 1f });
        UseFlameShapes(ps);
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeGlow()
    {
        var ps = NewSystem("Glow", glowMat, 8, 5f, ParticleSystemRenderMode.Billboard);
        ps.transform.localPosition = new Vector3(0f, 0.4f * size, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(2.4f * size, 3.0f * size);
        main.startColor = new Color(1f, 0.45f, 0.12f, 0.26f);
        var shape = ps.shape;
        shape.radius = 0.1f * size;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = WeatherEffects.Fade(0.3f, 0.6f);
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeSparks()
    {
        var ps = NewSystem("Sparks", sparkMat, phone ? 40 : 70, -10f, ParticleSystemRenderMode.Stretch);
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.velocityScale = 0.012f;
        renderer.lengthScale = 1.6f;
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * size, 3.8f * size);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f * size, 0.065f * size);
        main.gravityModifier = -0.04f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(-90f, 0f, 0f); // pointing up
        shape.angle = 24f;
        shape.radius = 0.2f * size;
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 1.1f * size;
        noise.frequency = 1.2f;
        noise.scrollSpeed = 0.8f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var limit = ps.limitVelocityOverLifetime;
        limit.enabled = true;
        limit.limit = 8f;
        limit.drag = 0.7f;
        limit.multiplyDragByParticleSize = false;
        limit.multiplyDragByParticleVelocity = false;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Gradient(
            new[] { new Color(1f, 0.92f, 0.6f), new Color(1f, 0.55f, 0.15f), new Color(0.9f, 0.25f, 0.05f) },
            new[] { 0f, 0.45f, 1f },
            new[] { 1f, 1f, 0f },
            new[] { 0f, 0.6f, 1f });
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 1f, 1f, 0.3f));
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeSmoke()
    {
        var ps = NewSystem("Smoke", smokeMat, phone ? 16 : 26, 10f, ParticleSystemRenderMode.Billboard);
        ps.transform.localPosition = new Vector3(0f, 0.9f * size, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f * size, 0.75f * size);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0.6f, 0.59f, 0.57f, 1f);
        var shape = ps.shape;
        shape.radius = 0.25f * size;
        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.6f * size, 1.0f * size);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f * size;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.3f;
        noise.octaveCount = 1;
        noise.quality = ParticleSystemNoiseQuality.Low;
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, Curve(0f, 0.5f, 1f, 2.4f));
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);
        var color = ps.colorOverLifetime;
        color.enabled = true;
        // thin, light smoke
        color.color = Gradient(
            new[] { Color.white, Color.white },
            new[] { 0f, 1f },
            new[] { 0f, 0.16f, 0.1f, 0f },
            new[] { 0f, 0.15f, 0.6f, 1f });
        ps.gameObject.SetActive(true);
        return ps;
    }

    ParticleSystem MakeCoals()
    {
        var ps = NewSystem("Coals", coalMat, phone ? 20 : 30, 2f, ParticleSystemRenderMode.HorizontalBillboard);
        ps.transform.localPosition = new Vector3(0f, 0.04f, 0f);
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.22f * size, 0.4f * size);
        var shape = ps.shape;
        shape.radius = 0.32f * size;
        var color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = Gradient(
            new[] { new Color(0.6f, 0.08f, 0f), new Color(1f, 0.42f, 0.08f), new Color(0.7f, 0.12f, 0.02f) },
            new[] { 0f, 0.45f, 1f },
            new[] { 0f, 1f, 0f },
            new[] { 0f, 0.45f, 1f });
        ps.gameObject.SetActive(true);
        return ps;
    }

    void MakeLight()
    {
        var go = new GameObject("Fire light");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 0.7f * size, 0f);
        fireLight = go.AddComponent<Light>();
        fireLight.type = LightType.Point;
        fireLight.color = new Color(1f, 0.5f, 0.2f);
        fireLight.range = 9f * size;
        fireLight.intensity = 0f;
        fireLight.renderMode = LightRenderMode.ForcePixel;
        // shadows from the fire on PC only (they cost too much on a phone)
        fireLight.shadows = phone ? LightShadows.None : LightShadows.Soft;
        fireLight.shadowStrength = 0.85f;
        fireLight.shadowBias = 0.05f;
        fireLight.shadowNormalBias = 0.4f;
        fireLight.shadowNearPlane = 0.3f;
        fireLight.enabled = false;
    }

    // ---------------- the label above the fire: seconds left, and a warning in bad weather ---------------- //

    const float LabelDistance = 22f;   // shown when the player is this close (metres)

    // ---------------- ash left in the fire pit ---------------- //

    // A low, uneven mound of ash and charcoal where the wood burned. It grows while the fire burns (under
    // the wood and flames) and stays after the fire goes out, with the last coals glowing on it.
    void MakeAsh()
    {
        var go = new GameObject("Ash");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, groundOffset + 0.005f, 0f);
        go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        go.AddComponent<MeshFilter>().sharedMesh = AshMesh(0.46f * size, 0.1f * size, Random.Range(0, 1000));
        var r = go.AddComponent<MeshRenderer>();
        if (ashMaterial == null)
        {
            ashMaterial = new Material(Shader.Find("Standard")) { name = "SB_Ash", hideFlags = HideFlags.DontSave };
            ashMaterial.SetTexture("_MainTex", EnvTextures.Ash);
            ashMaterial.SetFloat("_Glossiness", 0.04f);
            ashMaterial.SetFloat("_Metallic", 0f);
        }
        r.sharedMaterial = ashMaterial;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = true;
        ash = go.transform;
        go.SetActive(false);
    }

    // The ash lies on the real ground in the middle of the stones (measured once the campfire is placed,
    // the first time the ash shows), tilted like the ground there.
    void PlaceAshOnGround()
    {
        ashPlaced = true;
        // in the middle of the ring of stones (the wood of the model lies a bit off-centre)
        Vector3 stonesCentre = StonesCentre();
        Vector3 centre = new Vector3(stonesCentre.x, root.position.y, stonesCentre.z);
        Vector3 from = centre + Vector3.up * 5f;
        float groundY = centre.y + groundOffset;
        Vector3 normal = Vector3.up;
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 20f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore))
        {
            groundY = hit.point.y;
            normal = hit.normal;
        }
        else if (Terrain.activeTerrain != null)
        {
            var t = Terrain.activeTerrain;
            groundY = t.SampleHeight(centre) + t.transform.position.y;
        }
        ash.position = new Vector3(centre.x, groundY + 0.01f, centre.z);
        ash.rotation = Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    // ---------------- no grass growing through the fire ---------------- //

    // The terrain's grass (up to 2 m tall) would grow through the fire and hide the ash. The first time the
    // fire is lit, the grass inside the ring of stones is taken away; it is put back when the campfire is
    // gone (also when the game stops, so the terrain file is never changed).
    // The original grass of every cell is kept once (several campfires can share cells) and all of it is put
    // back when the last campfire that took grass away is gone.
    static readonly System.Collections.Generic.Dictionary<long, int> originalGrass = new System.Collections.Generic.Dictionary<long, int>();
    static TerrainData grassData;
    static int grassUsers;
    static bool grassDataWasDirty;
    bool tookGrass;

    static long GrassKey(int layer, int x, int z) => ((long)layer << 40) | ((long)x << 20) | (long)z;

    Vector3 StonesCentre()
    {
        Vector3 sum = Vector3.zero;
        int stones = 0;
        foreach (Transform child in campfire.transform)
        {
            var r = child.GetComponent<MeshRenderer>();
            if (r != null && child.name.StartsWith("Stone")) { sum += r.bounds.center; stones++; }
        }
        return stones >= 3 ? sum / stones : root.position;
    }

    void ClearGrass()
    {
        grassCleared = true;
        var t = Terrain.activeTerrain;
        if (t == null || t.terrainData == null) return;
        var data = t.terrainData;
        int layers = data.detailPrototypes.Length;
        if (layers == 0) return;
        Vector3 centre = StonesCentre();
        float radius = 0.72f * size;
        Vector3 local = centre - t.transform.position;
        float cellX = data.size.x / data.detailWidth, cellZ = data.size.z / data.detailHeight;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((local.x - radius) / cellX), 0, data.detailWidth - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((local.x + radius) / cellX), 0, data.detailWidth - 1);
        int z0 = Mathf.Clamp(Mathf.FloorToInt((local.z - radius) / cellZ), 0, data.detailHeight - 1);
        int z1 = Mathf.Clamp(Mathf.CeilToInt((local.z + radius) / cellZ), 0, data.detailHeight - 1);
        int width = x1 - x0 + 1, height = z1 - z0 + 1;
        if (grassData != data)
        {
            originalGrass.Clear();
            grassData = data;
#if UNITY_EDITOR
            grassDataWasDirty = UnityEditor.EditorUtility.IsDirty(data);
#endif
        }
        for (int layer = 0; layer < layers; layer++)
        {
            var cells = data.GetDetailLayer(x0, z0, width, height, layer);
            for (int z = 0; z < height; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    float wx = (x0 + x + 0.5f) * cellX - local.x;
                    float wz = (z0 + z + 0.5f) * cellZ - local.z;
                    if (wx * wx + wz * wz > radius * radius) continue;
                    long key = GrassKey(layer, x0 + x, z0 + z);
                    if (!originalGrass.ContainsKey(key)) originalGrass[key] = cells[z, x];
                    cells[z, x] = 0;
                }
            }
            data.SetDetailLayer(x0, z0, layer, cells);
        }
        tookGrass = true;
        grassUsers++;
    }

    void OnDestroy()
    {
        if (!tookGrass) return;
        tookGrass = false;
        grassUsers--;
        if (grassUsers > 0) return;
        // the last one: all the grass comes back (the terrain is a shared file, it must end up as it was)
        grassUsers = 0;
        if (grassData != null)
        {
            var one = new int[1, 1];
            foreach (var cell in originalGrass)
            {
                int layer = (int)(cell.Key >> 40);
                int x = (int)((cell.Key >> 20) & 0xFFFFF);
                int z = (int)(cell.Key & 0xFFFFF);
                one[0, 0] = cell.Value;
                grassData.SetDetailLayer(x, z, layer, one);
            }
#if UNITY_EDITOR
            // the data is exactly as before: do not leave the terrain file marked as changed
            if (!grassDataWasDirty) UnityEditor.EditorUtility.ClearDirty(grassData);
#endif
        }
        originalGrass.Clear();
        grassData = null;
    }

    // a flat disc that is a bit higher in the middle, with a wobbly edge
    static Mesh AshMesh(float radius, float height, int seed)
    {
        const int rings = 6, segments = 28;
        var vertices = new System.Collections.Generic.List<Vector3>();
        var uvs = new System.Collections.Generic.List<Vector2>();
        var triangles = new System.Collections.Generic.List<int>();
        vertices.Add(new Vector3(0f, height, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));
        var random = new System.Random(seed);
        var edge = new float[segments];
        for (int s = 0; s < segments; s++) edge[s] = 0.82f + (float)random.NextDouble() * 0.3f;
        for (int ring = 1; ring <= rings; ring++)
        {
            float t = (float)ring / rings;
            for (int s = 0; s < segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2f;
                // smooth the wobble between neighbours
                float e = (edge[s] * 2f + edge[(s + 1) % segments] + edge[(s + segments - 1) % segments]) / 4f;
                float r = radius * t * Mathf.Lerp(1f, e, t);
                float bump = 1f + ((float)random.NextDouble() - 0.5f) * 0.5f;
                float h = height * Mathf.Pow(1f - t * t, 1.5f) * bump - (ring == rings ? 0.02f : 0f); // the edge goes into the ground
                vertices.Add(new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f * t, 0.5f + Mathf.Sin(a) * 0.5f * t));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            triangles.Add(0); triangles.Add(1 + (s + 1) % segments); triangles.Add(1 + s);
        }
        for (int ring = 1; ring < rings; ring++)
        {
            int a0 = 1 + (ring - 1) * segments, b0 = 1 + ring * segments;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                triangles.Add(a0 + s); triangles.Add(a0 + s1); triangles.Add(b0 + s);
                triangles.Add(a0 + s1); triangles.Add(b0 + s1); triangles.Add(b0 + s);
            }
        }
        var mesh = new Mesh { name = "Ash (made in code)" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    void UpdateAsh()
    {
        if (ash == null) return;
        // full after about an hour and a half of burnt wood
        float target = Mathf.Clamp01(campfire.burnedMinutes / 90f);
        ashLevel = Mathf.MoveTowards(ashLevel, target, Time.deltaTime * 0.5f);
        bool on = ashLevel > 0.01f;
        if (on && !ashPlaced) PlaceAshOnGround();
        if (ash.gameObject.activeSelf != on) ash.gameObject.SetActive(on);
        if (on) ash.localScale = new Vector3(Mathf.Lerp(0.55f, 1f, ashLevel), ashLevel, Mathf.Lerp(0.55f, 1f, ashLevel));
    }

    void MakeLabel()
    {
        var go = new GameObject("Fire label");
        go.transform.SetParent(root, false);
        go.transform.localPosition = new Vector3(0f, 1.2f * size, 0f);
        label = NewLabelText(go, 50);
        // a dark copy just behind it, so the text can be read on a bright sky or on snow
        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(go.transform, false);
        shadowGo.transform.localPosition = new Vector3(0.05f, -0.05f, 0.03f);
        labelShadow = NewLabelText(shadowGo, 49);
        labelShadow.color = new Color(0f, 0f, 0f, 0.7f);
        go.SetActive(false);
    }

    static TextMeshPro NewLabelText(GameObject go, int sortingOrder)
    {
        var text = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = 4f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.richText = true;
        text.rectTransform.sizeDelta = new Vector2(10f, 2f);
        text.text = "";
        var r = text.GetComponent<MeshRenderer>();
        r.sortingOrder = sortingOrder; // drawn over the flames
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return text;
    }

    void UpdateLabel()
    {
        if (label == null) return;
        var cam = Camera.main;
        float distance = cam != null ? Vector3.Distance(cam.transform.position, label.transform.position) : 999f;
        // not while a window or the menu is open (the campfire window shows the same)
        bool windowOpen = (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen) ||
                          (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen);
        bool show = campfire.IsBurning && distance < LabelDistance && !windowOpen;
        if (label.gameObject.activeSelf != show) label.gameObject.SetActive(show);
        if (!show) return;

        // always facing the camera, a bit bigger further away so it stays readable on a phone
        label.transform.rotation = Quaternion.LookRotation(label.transform.position - cam.transform.position, Vector3.up);
        float scale = Mathf.Clamp(distance / 7f, 0.7f, 2.2f);
        label.transform.localScale = Vector3.one * scale;

        // the text changes only when the seconds (or the warning) change
        float left = campfire.RealSecondsLeft;
        int seconds = float.IsInfinity(left) ? -1 : Mathf.CeilToInt(left);
        string warning = "";
        float speed = campfire.BurnSpeed;
        var w = WeatherSystem.Instance;
        if (speed > 1.05f && w != null)
        {
            string why = w.Rain > 0.6f && w.Wind > 0.7f ? "Storm" : w.Rain > 0.1f ? "Rain" : w.Snow > 0.1f ? "Snow" : "Wind";
            warning = why + "! The fire burns " + speed.ToString("0.0") + "x faster";
        }
        if (seconds == shownSeconds && warning == shownWarning) return;
        shownSeconds = seconds;
        shownWarning = warning;

        string time = seconds < 0 ? "Fire" : "Fire: " + seconds + " s";
        bool low = seconds >= 0 && seconds <= 20;
        string colour = low ? "#FF5A3C" : "#FFD27A"; // red when it is about to go out
        string text = "<color=" + colour + ">" + time + "</color>";
        string plain = time;
        if (low) { text += "\n<size=70%><color=#FF8A6A>Add wood!</color></size>"; plain += "\n<size=70%>Add wood!</size>"; }
        if (warning != "") { text += "\n<size=70%><color=#8FD0FF>" + warning + "</color></size>"; plain += "\n<size=70%>" + warning + "</size>"; }
        label.text = text;
        labelShadow.text = plain;
    }

    void MakeSound()
    {
        crackle = root.gameObject.AddComponent<AudioSource>();
        crackle.clip = ProceduralAudio.FireCrackle;
        crackle.loop = true;
        crackle.playOnAwake = false;
        crackle.spatialBlend = 1f;
        crackle.rolloffMode = AudioRolloffMode.Linear;
        crackle.minDistance = 1.5f;
        crackle.maxDistance = 22f;
        crackle.dopplerLevel = 0f;
        crackle.volume = 0f;
        crackle.time = Random.Range(0f, 4f);
    }

    void SetPlaying(bool on)
    {
        if (on == playing) return;
        playing = on;
        foreach (var ps in new[] { flames, core, glow, sparks, smoke, coals })
        {
            if (ps == null) continue;
            if (on) ps.Play();
            else ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    static void Rate(ParticleSystem ps, float perSecond)
    {
        var emission = ps.emission;
        emission.rateOverTime = Mathf.Max(0f, perSecond);
    }

    void Update()
    {
        if (campfire == null || root == null) return;
        float dt = Time.deltaTime;

        // grow when lit (about a second), die down slowly when it goes out
        float target = campfire.IsBurning ? campfire.FireStrength : 0f;
        level = Mathf.MoveTowards(level, target, dt * (target > level ? 0.9f : 0.35f));
        if (campfire.IsBurning) embers = 1f;
        else embers = Mathf.MoveTowards(embers, 0f, dt / 50f);
        float coalLevel = Mathf.Max(level, embers * 0.45f);

        UpdateLabel();
        UpdateAsh();
        if (!grassCleared && campfire.IsBurning) ClearGrass(); // lit: it has been placed

        bool visible = level > 0.001f || embers > 0.001f;
        SetPlaying(visible);
        if (!visible)
        {
            if (fireLight.enabled) fireLight.enabled = false;
            if (crackle.isPlaying) crackle.Stop();
            return;
        }

        // far away: fewer details (saves time on phones)
        var cam = Camera.main;
        float distance = cam != null ? Vector3.Distance(cam.transform.position, root.position) : 0f;
        bool near = distance < 60f;

        // the fire breathes: slow gusts and fast flicker
        float t = Time.time;
        float gust = Mathf.PerlinNoise(t * 0.7f, seed);
        float flicker = Mathf.PerlinNoise(t * 9f, seed + 3.1f) * 0.6f + Mathf.PerlinNoise(t * 23f, seed + 7.7f) * 0.4f;
        float big = 0.5f + 0.5f * level;

        Rate(flames, (phone ? 40f : 52f) * level * (0.75f + 0.5f * gust));
        Rate(core, 16f * level * (0.8f + 0.4f * flicker));
        Rate(glow, 6f * coalLevel);
        Rate(coals, 11f * coalLevel);
        float rain = WeatherSystem.Instance != null ? WeatherSystem.Instance.Rain : 0f;
        Rate(smoke, near ? (3f + 6f * rain) * level + 2.5f * embers * (1f - level) : 0f);

        var flameMain = flames.main;
        flameMain.startSize = new ParticleSystem.MinMaxCurve(0.4f * size * big, 0.75f * size * big);
        var coreMain = core.main;
        coreMain.startSize = new ParticleSystem.MinMaxCurve(0.32f * size * big, 0.55f * size * big);

        // flames and smoke lean with the wind
        Vector2 wind = WeatherSystem.Instance != null ? WeatherSystem.Instance.WindDirection * WeatherSystem.Instance.Wind : Vector2.zero;
        wind *= 0.6f + 0.6f * gust;
        var fv = flames.velocityOverLifetime;
        fv.x = new ParticleSystem.MinMaxCurve(wind.x * 0.9f, wind.x * 1.3f);
        fv.z = new ParticleSystem.MinMaxCurve(wind.y * 0.9f, wind.y * 1.3f);
        var sv = smoke.velocityOverLifetime;
        sv.x = new ParticleSystem.MinMaxCurve(wind.x * 1.6f, wind.x * 2.4f);
        sv.z = new ParticleSystem.MinMaxCurve(wind.y * 1.6f, wind.y * 2.4f);

        // smoke is lit like the rest of the scene, a little orange from the fire at night (and fainter, it is not lit much)
        Color light = SkyLighting.Instance != null ? SkyLighting.Instance.LightLevel : Color.white;
        float day = SkyLighting.Instance != null ? SkyLighting.Instance.DayLight : 1f;
        Color smokeTint = new Color(light.r * 0.9f + 0.14f * level, light.g * 0.9f + 0.06f * level, light.b * 0.9f + 0.02f * level, Mathf.Lerp(0.55f, 1f, day));
        smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial.SetColor(TintId, smokeTint);

        // sparks come in little bursts, now and then a bigger pop
        if (near && level > 0.2f)
        {
            sparkTimer -= dt;
            if (sparkTimer <= 0f)
            {
                int count = Random.value < 0.12f ? Random.Range(8, 16) : Random.Range(1, 5);
                sparks.Emit(Mathf.Max(1, Mathf.RoundToInt(count * level)));
                sparkTimer = Random.Range(0.12f, 0.9f) / Mathf.Max(0.3f, level);
            }
        }

        // light: flickers with the flames, only the coals' glow is left after it goes out
        bool lightOn = distance < 90f;
        fireLight.enabled = lightOn;
        if (lightOn)
        {
            float strength = level * (0.78f + 0.3f * flicker + 0.12f * gust) + embers * 0.18f * (1f - level);
            // in daylight a fire hardly lights anything around it
            fireLight.intensity = 1.9f * strength * Mathf.Lerp(1f, 0.3f, day);
            fireLight.range = 9f * size * (0.6f + 0.4f * Mathf.Max(level, embers * 0.3f));
            fireLight.transform.localPosition = new Vector3((flicker - 0.5f) * 0.08f * size, (0.65f + 0.1f * flicker) * size, (gust - 0.5f) * 0.08f * size);
            // shadows only when the player is close (PC)
            fireLight.shadows = phone || distance > 30f ? LightShadows.None : LightShadows.Soft;
        }

        // crackling sound
        crackle.volume = Mathf.Lerp(crackle.volume, 0.6f * level + 0.05f * embers * (1f - level), dt * 3f);
        if (!crackle.isPlaying && crackle.volume > 0.01f) crackle.Play();
    }
}
