using UnityEngine;

// The torch in the player's hand (on Resources/torch_Model, which the quick bar puts in the hand): a flame and a warm
// light that flickers. It burns only while it is held, about 6 game hours in all (TorchFuel on the inventory item);
// near the end it warns, then it is gone. The cave is pitch dark: only a burning torch lets the player go in (Cave).
public class TorchLight : MonoBehaviour
{
    public static bool Burning => active != null && active.fuel != null && active.fuel.hoursLeft > 0f;
    public static bool HasTorch => active != null;
    static TorchLight active;

    [Tooltip("Where the flame sits (the top of the torch)")]
    public Transform flamePoint;
    public float lightRange = 15f;
    public float lightIntensity = 1.7f;

    TorchFuel fuel;
    Light torchLight;
    float seed;
    bool warned;

    void OnEnable() => active = this;

    void OnDisable()
    {
        if (active == this) active = null;
    }

    void Start()
    {
        seed = Random.Range(0f, 100f);
        var equip = EquipSystem.Instance;
        if (equip != null && equip.selectedItem != null) fuel = equip.selectedItem.GetComponent<TorchFuel>();
        if (fuel == null) fuel = gameObject.AddComponent<TorchFuel>(); // (a torch without its item: it still burns)
        Transform at = flamePoint != null ? flamePoint : transform;

        var lightGo = new GameObject("Torch light");
        lightGo.transform.SetParent(at, false);
        torchLight = lightGo.AddComponent<Light>();
        torchLight.type = LightType.Point;
        torchLight.color = new Color(1f, 0.68f, 0.34f);
        torchLight.range = lightRange;
        torchLight.shadows = LightShadows.None;
        torchLight.renderMode = LightRenderMode.ForcePixel;

        MakeFlame(at);
    }

    void MakeFlame(Transform at)
    {
        var shader = Resources.Load<Shader>("Environment/SB_Particle");
        if (shader == null) return;
        var go = new GameObject("Flame");
        go.layer = gameObject.layer; // drawn by the camera that draws the things in the hand
        go.transform.SetParent(at, false);
        go.transform.localRotation = Quaternion.identity;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
        main.startColor = new Color(1f, 0.62f, 0.22f, 1f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 60;
        main.gravityModifier = -0.15f;
        var emission = ps.emission;
        emission.rateOverTime = 38f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.08f;
        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.9f, 0.55f), 0f), new GradientColorKey(new Color(1f, 0.5f, 0.12f), 0.45f), new GradientColorKey(new Color(0.6f, 0.15f, 0.05f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.7f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
        var renderer = go.GetComponent<ParticleSystemRenderer>();
        var material = new Material(shader) { name = "Torch flame (made in code)", mainTexture = EnvTextures.Dot, hideFlags = HideFlags.DontSave };
        material.SetFloat("_Opacity", 0.2f);
        material.SetFloat("_Intensity", 1.6f);
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ps.Play();
    }

    void Update()
    {
        if (fuel != null)
        {
            fuel.hoursLeft -= Time.deltaTime * DayNightSystem.WeatherHoursPerSecond;
            if (fuel.hoursLeft <= 0f) { BurnOut(); return; }
            if (fuel.hoursLeft < 0.75f && !warned)
            {
                warned = true;
                if (InventorySystem.Instance != null) InventorySystem.Instance.ShowMessage("Your torch is burning low!");
            }
        }
        if (torchLight != null)
        {
            float t = Time.time * 9f + seed;
            float low = fuel != null ? Mathf.Clamp01(fuel.hoursLeft / 0.75f) : 1f;
            torchLight.intensity = lightIntensity * (0.8f + 0.2f * Mathf.PerlinNoise(t, seed)) * Mathf.Lerp(0.55f, 1f, low);
        }
    }

    // burnt out: the torch is gone from the hand and the quick bar
    void BurnOut()
    {
        if (InventorySystem.Instance != null) InventorySystem.Instance.ShowMessage("Your torch burned out");
        var equip = EquipSystem.Instance;
        if (equip != null)
        {
            var item = equip.selectedItem;
            equip.selectedItem = null;
            equip.selectedNumber = -1;
            if (equip.selectedItemModel == gameObject) equip.selectedItemModel = null;
            if (item != null && item.GetComponent<TorchFuel>() != null) DestroyImmediate(item);
            if (InventorySystem.Instance != null) InventorySystem.Instance.RecalculateList();
            QuickBarUI.Changed();
        }
        Destroy(gameObject);
    }
}
