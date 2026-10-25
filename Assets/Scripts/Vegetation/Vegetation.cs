using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// All the plants placed in the scene (every object with a ScenePlant, under Enviroment/Vegetation), made in code when
// the game scene loads (EnvironmentBootstrap). Phone friendly:
//  - only the plants near the player are drawn (small ones up to ~35 m, bushes up to ~80 m), many at once
//  - a plant that can be picked gets its "hand" (a collider and an InteractableObject) only when the player is
//    next to it, so thousands of plants cost nothing every frame
//  - a picked plant disappears (a berry bush only loses its berries) and grows back after its regrowHours
//  - under deep snow the small plants are hidden (and can't be picked: winter is hard)
// The save keeps the plants that were picked and are growing back (EnviromentData.vegetation).
public class Vegetation : MonoBehaviour
{
    public static Vegetation Instance { get; private set; }

    [Header("Drawn up to (metres)")]
    public float smallDistance = 35f;
    public float mediumDistance = 50f;
    public float largeDistance = 85f;
    [Header("Picking")]
    public float pickDistance = 3.4f;
    [Tooltip("Snow cover (0..1) above which the small plants are hidden")]
    public float snowHideAt = 0.55f;

    // a plant was picked (its inventory item): the story's goals listen to this
    public static event System.Action<string> Picked;

    class Plant
    {
        public ScenePlant source;
        public Transform transform;
        public Vector3 position;
        public Renderer[] renderers;
        public GameObject fruit;
        public InteractableObject pick;
        public Collider collider;
        public bool visible, picked, near;
        public float regrowAt;
    }

    readonly List<Plant> plants = new List<Plant>();
    readonly SpatialGrid<Plant> grid = new SpatialGrid<Plant>(32f);
    readonly List<Plant> visible = new List<Plant>();
    readonly List<Plant> near = new List<Plant>();
    readonly List<Plant> pickedPlants = new List<Plant>();
    readonly List<Plant> query = new List<Plant>();
    readonly Dictionary<InteractableObject, Plant> byPick = new Dictionary<InteractableObject, Plant>();
    readonly Dictionary<Material, Material> instanced = new Dictionary<Material, Material>();
    Transform player;
    Collider playerCollider;
    float clock;
    bool snowHidden;
    float visibleTick, nearTick, growTick;
    bool started;

    public int PlantCount => plants.Count;
    public int VisibleCount => visible.Count;
    public float Clock => clock;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (started) return;
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) { player = movement.transform; playerCollider = movement.GetComponent<CharacterController>(); }
        Register();
        started = true;
        // the terrain's grass and forest undergrowth: not so far on a phone
        var terrain = Terrain.activeTerrain;
        if (terrain != null && GameInput.TouchControls) terrain.detailObjectDistance = Mathf.Min(terrain.detailObjectDistance, 55f);
        UpdateVisible();
    }

    Vector3 PlayerPosition => player != null ? player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

    void Register()
    {
        var roots = new HashSet<Transform>();
        foreach (var source in FindObjectsOfType<ScenePlant>())
        {
            var t = source.transform;
            var plant = new Plant
            {
                source = source,
                transform = t,
                position = t.position,
                renderers = t.GetComponentsInChildren<Renderer>(true),
            };
            var fruit = t.Find("Fruit");
            if (fruit != null) plant.fruit = fruit.gameObject;
            bool small = source.size != ScenePlant.PlantSize.Large;
            foreach (var r in plant.renderers)
            {
                r.enabled = false;
                r.sharedMaterials = Instanced(r.sharedMaterials);
                if (small) r.shadowCastingMode = ShadowCastingMode.Off;
                r.lightProbeUsage = LightProbeUsage.Off;
                r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            // colliders of the scene copy are not needed (the hand gets its own when the player is near)
            foreach (var c in t.GetComponentsInChildren<Collider>(true)) Destroy(c);
            plants.Add(plant);
            grid.Add(plant, plant.position);
            if (t.parent != null) roots.Add(t.parent);
        }
        foreach (var r in roots) SnowCover.Exclude(r); // (they are hidden under snow instead)
    }

    // copies of the plant materials that can be drawn many at once (GPU instancing)
    Material[] Instanced(Material[] materials)
    {
        var result = new Material[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            var m = materials[i];
            if (m == null) continue;
            if (!instanced.TryGetValue(m, out var copy))
            {
                copy = new Material(m) { name = m.name + " (plants)", enableInstancing = true, hideFlags = HideFlags.DontSave };
                instanced[m] = copy;
            }
            result[i] = copy;
        }
        return result;
    }

    void Update()
    {
        if (!started) return;
        var weather = WeatherSystem.Instance;
        float snow = weather != null ? weather.SnowCover : 0f;
        bool hide = snowHidden ? snow > snowHideAt - 0.1f : snow > snowHideAt;
        if (hide != snowHidden)
        {
            snowHidden = hide;
            RefreshAllVisible();
        }

        visibleTick -= Time.deltaTime;
        if (visibleTick <= 0f) { visibleTick = 0.25f; UpdateVisible(); }
        nearTick -= Time.deltaTime;
        if (nearTick <= 0f) { nearTick = 0.1f; UpdateNear(); }
        growTick += Time.deltaTime;
        if (growTick >= 1f)
        {
            clock += DayNightSystem.GameHoursPerSecond * growTick;
            growTick = 0f;
            GrowBack();
        }
    }

    float DistanceFor(ScenePlant.PlantSize size)
    {
        switch (size)
        {
            case ScenePlant.PlantSize.Small: return smallDistance;
            case ScenePlant.PlantSize.Large: return largeDistance;
            default: return mediumDistance;
        }
    }

    bool ShouldShow(Plant p, float sqrDistance)
    {
        if (p.picked && !p.source.keepPlant) return false;
        if (snowHidden && p.source.hideUnderSnow) return false;
        float d = DistanceFor(p.source.size);
        return sqrDistance < d * d;
    }

    void SetVisible(Plant p, bool show)
    {
        if (p.visible == show) return;
        p.visible = show;
        foreach (var r in p.renderers) if (r != null) r.enabled = show;
    }

    void UpdateVisible()
    {
        Vector3 at = PlayerPosition;
        for (int i = visible.Count - 1; i >= 0; i--)
        {
            var p = visible[i];
            if (p.transform == null) { visible.RemoveAt(i); continue; }
            Vector3 d = p.position - at; d.y = 0f;
            if (!ShouldShow(p, d.sqrMagnitude))
            {
                SetVisible(p, false);
                visible[i] = visible[visible.Count - 1];
                visible.RemoveAt(visible.Count - 1);
            }
        }
        query.Clear();
        grid.Query(at, largeDistance, query);
        foreach (var p in query)
        {
            if (p.visible || p.transform == null) continue;
            Vector3 d = p.position - at; d.y = 0f;
            if (ShouldShow(p, d.sqrMagnitude))
            {
                SetVisible(p, true);
                visible.Add(p);
            }
        }
    }

    void RefreshAllVisible()
    {
        foreach (var p in visible) SetVisible(p, false);
        visible.Clear();
        UpdateVisible();
    }

    // the plants next to the player get a hand: a collider the crosshair can see, and an InteractableObject
    void UpdateNear()
    {
        Vector3 at = PlayerPosition;
        const float handReach = 6f;
        for (int i = near.Count - 1; i >= 0; i--)
        {
            var p = near[i];
            if (p.transform == null) { near.RemoveAt(i); continue; }
            Vector3 d = p.position - at;
            bool keep = !p.picked && p.visible && d.sqrMagnitude < handReach * handReach;
            if (!keep)
            {
                SetHand(p, false);
                near[i] = near[near.Count - 1];
                near.RemoveAt(near.Count - 1);
            }
            else
            {
                p.pick.playerInRange = d.sqrMagnitude < pickDistance * pickDistance;
            }
        }
        query.Clear();
        grid.Query(at, handReach, query);
        foreach (var p in query)
        {
            if (p.near || p.picked || !p.visible || p.transform == null || string.IsNullOrEmpty(p.source.item)) continue;
            Vector3 d = p.position - at;
            if (d.sqrMagnitude >= handReach * handReach) continue;
            SetHand(p, true);
            p.pick.playerInRange = d.sqrMagnitude < pickDistance * pickDistance;
            near.Add(p);
        }
    }

    void SetHand(Plant p, bool on)
    {
        p.near = on;
        if (on && p.pick == null)
        {
            var go = p.transform.gameObject;
            var box = go.AddComponent<BoxCollider>();
            var filter = go.GetComponent<MeshFilter>();
            Vector3 scale = p.transform.lossyScale;
            Vector3 minSize = new Vector3(0.5f / Mathf.Max(0.01f, Mathf.Abs(scale.x)), 0.4f / Mathf.Max(0.01f, Mathf.Abs(scale.y)), 0.5f / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
            if (filter != null && filter.sharedMesh != null)
            {
                var b = filter.sharedMesh.bounds;
                box.center = b.center;
                box.size = Vector3.Max(b.size, minSize);
            }
            else
            {
                box.center = Vector3.up * 0.25f / Mathf.Max(0.01f, scale.y);
                box.size = minSize * 1.4f;
            }
            if (playerCollider != null) Physics.IgnoreCollision(box, playerCollider);
            p.collider = box;
            var pick = go.AddComponent<InteractableObject>();
            pick.ItemName = p.source.item;
            pick.displayName = string.IsNullOrEmpty(p.source.displayName) ? p.source.item : p.source.displayName;
            pick.onPicked = OnPicked;
            p.pick = pick;
            byPick[pick] = p;
            if (go.tag == "Untagged") go.tag = "Pickable";
        }
        if (p.pick != null)
        {
            p.pick.enabled = on;
            if (!on) p.pick.playerInRange = false;
        }
        if (p.collider != null) p.collider.enabled = on;
    }

    // InteractableObject has put the item in the inventory
    void OnPicked(InteractableObject pick)
    {
        if (!byPick.TryGetValue(pick, out var p)) return;
        for (int i = 1; i < p.source.amount; i++)
        {
            if (!InventorySystem.Instance.CheckSlotsAvailable(1)) break;
            InventorySystem.Instance.AddToIventory(p.source.item, false, false);
        }
        SetPicked(p, p.source.regrowHours > 0f ? clock + p.source.regrowHours : -1f);
        if (Picked != null) Picked(p.source.item);
    }

    void SetPicked(Plant p, float regrowAt)
    {
        if (p.picked) return;
        p.picked = true;
        p.regrowAt = regrowAt;
        if (p.near) { SetHand(p, false); near.Remove(p); }
        if (p.source.keepPlant && p.fruit != null) p.fruit.SetActive(false);
        else if (p.visible) { SetVisible(p, false); visible.Remove(p); }
        pickedPlants.Add(p);
    }

    // picked plants grow back when their time has come (not under the snow)
    void GrowBack()
    {
        if (snowHidden) return;
        for (int i = pickedPlants.Count - 1; i >= 0; i--)
        {
            var p = pickedPlants[i];
            if (p.regrowAt < 0f || clock < p.regrowAt) continue;
            p.picked = false;
            if (p.fruit != null) p.fruit.SetActive(true);
            pickedPlants.RemoveAt(i);
        }
    }

    // ---------------- save / load ---------------- //

    public VegetationData GetData()
    {
        var data = new VegetationData { version = 1, clock = clock };
        foreach (var p in pickedPlants) data.picked.Add(new PickedPlantData { position = p.position, regrowAt = p.regrowAt });
        return data;
    }

    public void SetData(VegetationData data)
    {
        if (!started) Start();
        if (data == null || data.version < 1) return;
        clock = data.clock;
        foreach (var p in pickedPlants)
        {
            p.picked = false;
            if (p.fruit != null) p.fruit.SetActive(true);
        }
        pickedPlants.Clear();
        if (data.picked != null)
        {
            foreach (var entry in data.picked)
            {
                query.Clear();
                grid.Query(entry.position, 0.5f, query);
                Plant best = null;
                float bestDistance = 0.3f * 0.3f;
                foreach (var p in query)
                {
                    float d = (p.position - entry.position).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = p; }
                }
                if (best != null) SetPicked(best, entry.regrowAt);
            }
        }
        RefreshAllVisible();
    }
}
