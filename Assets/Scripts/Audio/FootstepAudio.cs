using System.Collections.Generic;
using UnityEngine;

// Footsteps and swimming sounds of the player. Added to the player in code (EnvironmentBootstrap).
//  - a step every few metres walked on the ground; the sound depends on what is under the feet:
//    snow (when snow lies on the ground), sand (the beach), wood (buildings), rock / dirt / grass (the terrain's
//    paint at that spot), splashes in shallow sea water
//  - while swimming: a stroke now and then, and a splash when you get into deep water
// Step sounds: free CC0 sounds in Assets/Resources/Sounds/Footsteps; water sounds are made in code.
public class FootstepAudio : MonoBehaviour
{
    public float stepDistance = 3.2f;     // metres per step (the player walks fast: 12 m/s)
    public float strokeSeconds = 0.95f;   // seconds between swimming strokes
    public float volume = 0.42f;

    CharacterController controller;
    PlayerWater water;
    AudioSource source;
    Vector3 lastPosition;
    float travelled;
    float strokeTimer;
    bool wasSwimming;

    readonly Dictionary<string, AudioClip[]> steps = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, float> loudness = new Dictionary<string, float>
    {
        { "Grass", 0.8f }, { "Dirt", 0.75f }, { "Sand", 1.1f }, { "Snow", 1f }, { "Rock", 0.7f }, { "Wood", 0.85f }, { "Water", 0.9f },
    };

    // which terrain paint (layer) sounds like what: found by the layer's name
    Terrain terrain;
    string[] layerSurface;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        water = GetComponent<PlayerWater>();
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        lastPosition = transform.position;

        foreach (var surface in new[] { "Grass", "Dirt", "Sand", "Snow", "Rock", "Wood" })
        {
            steps[surface] = Resources.LoadAll<AudioClip>("Sounds/Footsteps/" + surface);
        }
        steps["Water"] = ProceduralAudio.WadeSteps;

        terrain = Terrain.activeTerrain;
        if (terrain != null && terrain.terrainData != null)
        {
            var layers = terrain.terrainData.terrainLayers;
            layerSurface = new string[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                string name = layers[i] != null ? layers[i].name.ToLowerInvariant() : "";
                layerSurface[i] =
                    name.Contains("sand") ? "Sand" :
                    name.Contains("rock") || name.Contains("stone") ? "Rock" :
                    name.Contains("road") || name.Contains("ground") || name.Contains("dirt") || name.Contains("tidal") || name.Contains("mud") ? "Dirt" :
                    "Grass";
            }
        }
    }

    void Update()
    {
        if (Time.deltaTime <= 0f) return;
        Vector3 position = transform.position;
        Vector3 moved = position - lastPosition;
        moved.y = 0f;
        lastPosition = position;
        float distance = moved.magnitude;
        if (distance > 4f) { travelled = 0f; return; } // moved by the game (loading, respawn), not walked

        bool swimming = water != null && water.IsSwimming;
        if (swimming != wasSwimming)
        {
            if (swimming) Play(new[] { ProceduralAudio.Splash }, 0.55f, 0.05f);
            wasSwimming = swimming;
            travelled = 0f;
            strokeTimer = strokeSeconds * 0.6f;
        }

        if (swimming)
        {
            if (water.IsHeadUnderwater) return;
            if (distance / Time.deltaTime > 0.6f)
            {
                strokeTimer -= Time.deltaTime;
                if (strokeTimer <= 0f)
                {
                    Play(ProceduralAudio.SwimStrokes, 0.45f, 0.08f);
                    strokeTimer = strokeSeconds * Random.Range(0.9f, 1.1f);
                }
            }
            else
            {
                strokeTimer = Mathf.Min(strokeTimer, 0.25f); // the first stroke comes quickly when you start again
            }
            return;
        }

        if (controller != null && !controller.isGrounded) return; // in the air
        travelled += distance;
        if (travelled < stepDistance) return;
        travelled = 0f;

        string surface = SurfaceUnderFeet(position);
        if (steps.TryGetValue(surface, out var clips) && clips != null && clips.Length > 0)
        {
            Play(clips, volume * (loudness.TryGetValue(surface, out var l) ? l : 1f), 0.08f);
        }
    }

    int lastClip = -1;

    void Play(AudioClip[] clips, float clipVolume, float pitchJitter)
    {
        if (clips == null || clips.Length == 0) return;
        int i = Random.Range(0, clips.Length);
        if (clips.Length > 1 && i == lastClip) i = (i + 1) % clips.Length;
        lastClip = i;
        source.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        source.PlayOneShot(clips[i], clipVolume * SoundManager.EffectsVolume);
    }

    string SurfaceUnderFeet(Vector3 position)
    {
        float feet = controller != null ? controller.bounds.min.y : position.y - 1f;

        // shallow sea water
        var ocean = Ocean.Instance;
        if (ocean != null && ocean.DistanceFromShore(position) > -40f && ocean.SurfaceHeight(position) > feet + 0.08f) return "Water";

        // standing on something that is not the ground: a building is wood
        if (Physics.Raycast(new Vector3(position.x, feet + 0.4f, position.z), Vector3.down, out RaycastHit hit, 1.5f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!(hit.collider is TerrainCollider))
            {
                return hit.collider.GetComponentInParent<Constructable>() != null ? "Wood" : "Rock";
            }
        }

        // snow lying on the ground (not on the beach right next to the sea)
        var weather = WeatherSystem.Instance;
        float sea = ocean != null ? ocean.Level : float.NegativeInfinity;
        if (weather != null && weather.SnowCover > 0.35f && feet > sea + 0.25f) return "Snow";

        // the beach
        if (ocean != null && ocean.DistanceFromShore(position) > -26f) return "Sand";

        return TerrainSurface(position);
    }

    string TerrainSurface(Vector3 position)
    {
        if (terrain == null || layerSurface == null || layerSurface.Length == 0) return "Grass";
        var data = terrain.terrainData;
        Vector3 local = position - terrain.transform.position;
        int x = Mathf.Clamp(Mathf.RoundToInt(local.x / data.size.x * (data.alphamapWidth - 1)), 0, data.alphamapWidth - 1);
        int z = Mathf.Clamp(Mathf.RoundToInt(local.z / data.size.z * (data.alphamapHeight - 1)), 0, data.alphamapHeight - 1);
        float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
        int best = 0;
        for (int i = 1; i < weights.GetLength(2); i++)
        {
            if (weights[0, 0, i] > weights[0, 0, best]) best = i;
        }
        return best < layerSurface.Length ? layerSurface[best] : "Grass";
    }
}
