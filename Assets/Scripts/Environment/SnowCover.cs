using System.Collections.Generic;
using UnityEngine;

// Snow lying on the world, and wet ground after rain (amounts from WeatherSystem):
//  - the terrain gets the SurvivalBD/Terrain Snow shader (it looks the same as before when dry and without snow)
//  - the grass gets shorter and white under snow (SB_GrassSnow replaces Unity's grass shader by name)
//  - objects with the Standard shader (rocks, pine trees, campfire stones, walls...) get snow on top: while
//    there is snow their materials are swapped to copies with the SurvivalBD/Standard Snow shader
// Only while the game runs: the scene and the material files are never changed.
public class SnowCover : MonoBehaviour
{
    [Tooltip("Colour of fresh snow")]
    public Color snowColor = new Color(0.86f, 0.89f, 0.94f);

    Terrain terrain;
    Material originalTerrainMaterial;
    Material snowTerrainMaterial;
    Shader objectSnowShader;
    readonly Dictionary<Material, Material> snowCopies = new Dictionary<Material, Material>();
    readonly Dictionary<Material, Material> originals = new Dictionary<Material, Material>();
    readonly List<Renderer> changed = new List<Renderer>();
    bool objectsHaveSnow;
    float rescanTimer;
    float grassDensity = -1f;   // the terrain's own grass density (before the snow)

    static readonly int SnowAmountId = Shader.PropertyToID("_SB_SnowAmount");
    static readonly int WetnessId = Shader.PropertyToID("_SB_Wetness");
    static readonly int SnowMinHeightId = Shader.PropertyToID("_SB_SnowMinHeight");
    static readonly int SnowColorId = Shader.PropertyToID("_SB_SnowColor");
    static readonly int SnowNoiseId = Shader.PropertyToID("_SB_SnowNoise");

    void Start()
    {
        Shader.SetGlobalTexture(SnowNoiseId, EnvTextures.Noise);
        Shader.SetGlobalFloat(SnowAmountId, 0f);
        Shader.SetGlobalFloat(WetnessId, 0f);

        terrain = Terrain.activeTerrain;
        var terrainShader = Resources.Load<Shader>("Environment/SB_TerrainSnow");
        if (terrain != null && terrainShader != null && terrainShader.isSupported)
        {
            originalTerrainMaterial = terrain.materialTemplate;
            snowTerrainMaterial = new Material(terrainShader) { name = "Terrain with snow (made in code)", hideFlags = HideFlags.DontSave };
            if (originalTerrainMaterial != null) snowTerrainMaterial.CopyPropertiesFromMaterial(originalTerrainMaterial);
            terrain.materialTemplate = snowTerrainMaterial;
        }
        objectSnowShader = Resources.Load<Shader>("Environment/SB_StandardSnow");
        if (objectSnowShader != null && !objectSnowShader.isSupported) objectSnowShader = null;
    }

    void OnDestroy()
    {
        if (terrain != null && originalTerrainMaterial != null && terrain.materialTemplate == snowTerrainMaterial)
            terrain.materialTemplate = originalTerrainMaterial;
        if (terrain != null && grassDensity >= 0f) terrain.detailObjectDensity = grassDensity;
        RemoveObjectSnow();
        Shader.SetGlobalFloat(SnowAmountId, 0f);
        Shader.SetGlobalFloat(WetnessId, 0f);
    }

    void Update()
    {
        var weather = WeatherSystem.Instance;
        float snow = weather != null ? weather.SnowCover : 0f;
        float wet = weather != null ? weather.Wetness : 0f;
        // snow sitting on top of wet ground hides the wetness
        Shader.SetGlobalFloat(SnowAmountId, snow);
        Shader.SetGlobalFloat(WetnessId, wet * (1f - snow));
        Shader.SetGlobalColor(SnowColorId, snowColor);
        float sea = Ocean.Instance != null ? Ocean.Instance.Level : -1000f;
        Shader.SetGlobalFloat(SnowMinHeightId, sea + 0.25f); // the plain is only about 1.2 m above the sea

        // the grass disappears under deep snow (in steps, each change makes the terrain rebuild its grass)
        if (terrain != null)
        {
            if (grassDensity < 0f) grassDensity = terrain.detailObjectDensity;
            float target = grassDensity * Mathf.Lerp(1f, 0.1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.95f, snow)));
            float stepped = Mathf.Round(target * 10f) / 10f;
            if (Mathf.Abs(stepped - terrain.detailObjectDensity) > 0.001f) terrain.detailObjectDensity = stepped;
        }

        if (objectSnowShader == null) return;
        if (snow > 0.003f)
        {
            rescanTimer -= Time.deltaTime;
            if (!objectsHaveSnow || rescanTimer <= 0f)
            {
                AddObjectSnow(); // again every few seconds, for things built or placed since
                objectsHaveSnow = true;
                rescanTimer = 4f;
            }
        }
        else if (objectsHaveSnow)
        {
            RemoveObjectSnow();
            objectsHaveSnow = false;
        }
    }

    // Which objects get snow: the world's Standard materials, not the player, animals, the UI or things
    // that are being placed (they have a coloured outline then).
    bool Skip(Renderer r)
    {
        if (r is ParticleSystemRenderer || r is SkinnedMeshRenderer || r is LineRenderer || r is TrailRenderer) return true;
        if (r.gameObject.layer == 5) return true; // UI
        if (r.GetComponentInParent<PlayerMovement>() != null) return true;
        if (r.GetComponentInParent<Animal>() != null) return true;
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.placementHoldingSpot != null &&
            r.transform.IsChildOf(PlacementSystem.Instance.placementHoldingSpot.transform)) return true;
        if (ConstructionManager.Instance != null && r.CompareTag("ghost")) return true;
        if ((r.name.StartsWith("Stick") || r.name == "Ash") && r.GetComponentInParent<Campfire>() != null) return true; // the wood and ash in a fire stay dark
        return false;
    }

    // The forest and the plants handle their snow themselves (their own shaders, or hidden under the snow): their
    // thousands of objects are not searched every few seconds.
    static readonly HashSet<Transform> excluded = new HashSet<Transform>();
    public static void Exclude(Transform root) { if (root != null) excluded.Add(root); }

    readonly List<MeshRenderer> found = new List<MeshRenderer>();

    void Collect(Transform t)
    {
        if (excluded.Contains(t) || !t.gameObject.activeInHierarchy) return;
        var r = t.GetComponent<MeshRenderer>();
        if (r != null) found.Add(r);
        for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i));
    }

    void AddObjectSnow()
    {
        excluded.RemoveWhere(t => t == null);
        found.Clear();
        foreach (var root in gameObject.scene.GetRootGameObjects()) Collect(root.transform);
        foreach (var r in found)
        {
            if (Skip(r)) continue;
            var materials = r.sharedMaterials;
            bool any = false;
            for (int i = 0; i < materials.Length; i++)
            {
                var copy = SnowCopyOf(materials[i]);
                if (copy != null)
                {
                    materials[i] = copy;
                    any = true;
                }
            }
            if (any)
            {
                r.sharedMaterials = materials;
                if (!changed.Contains(r)) changed.Add(r);
            }
        }
    }

    void RemoveObjectSnow()
    {
        foreach (var r in changed)
        {
            if (r == null) continue;
            var materials = r.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null && originals.TryGetValue(materials[i], out var original)) materials[i] = original;
            }
            r.sharedMaterials = materials;
        }
        changed.Clear();
    }

    // A copy of a Standard material with the snow shader (made once per material). Null = leave it as it is.
    Material SnowCopyOf(Material original)
    {
        if (original == null || original.shader == null) return null;
        if (originals.ContainsKey(original)) return null; // already a snow copy
        if (snowCopies.TryGetValue(original, out var copy)) return copy;
        if (original.shader.name != "Standard") return null;
        // see-through materials (glass, fading) stay as they are
        if (original.IsKeywordEnabled("_ALPHABLEND_ON") || original.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) return null;

        copy = new Material(objectSnowShader) { name = original.name + " (snow)", hideFlags = HideFlags.DontSave };
        copy.CopyPropertiesFromMaterial(original);
        copy.renderQueue = original.renderQueue;
        if (original.IsKeywordEnabled("_ALPHATEST_ON")) copy.EnableKeyword("_SB_CUTOUT");
        copy.SetFloat("_SB_UseMetalMap", original.IsKeywordEnabled("_METALLICGLOSSMAP") ? 1f : 0f);
        copy.SetFloat("_SB_SmoothFromAlbedo", original.IsKeywordEnabled("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A") ? 1f : 0f);
        copy.SetFloat("_SB_UseNormalMap", original.IsKeywordEnabled("_NORMALMAP") && original.GetTexture("_BumpMap") != null ? 1f : 0f);
        if (!original.IsKeywordEnabled("_EMISSION")) copy.SetColor("_EmissionColor", Color.black);
        snowCopies[original] = copy;
        originals[copy] = original;
        return copy;
    }
}
