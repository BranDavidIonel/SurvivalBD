using System.Collections;
using UnityEngine;

// One tree of the forest (made by Forest.cs): pine, birch, oak or beech. It grows over game time, shakes when the
// axe hits it, and when it is chopped down it falls over (away from the player) and leaves logs and a stump.
// Chopping goes through the game's usual ChoppableTree (on the same object, with the trunk's BoxCollider).
// Far from the player its 3D model is hidden and Forest draws a flat picture of it instead (phone friendly).
public class ForestTree : MonoBehaviour
{
    public ForestTreeData data;

    Forest forest;
    Forest.Variant variant;
    TreeSpecies.Info species;
    Transform model;
    Renderer[] renderers;
    CapsuleCollider trunk;
    ChoppableTree choppable;
    float appliedHeight = -1f;
    float widthFactor = 1f;
    Quaternion standing;
    Coroutine shaking;

    public bool Falling { get; private set; }
    public bool ModelShown { get; private set; }
    public ChoppableTree Choppable => choppable;
    public Forest.Variant Variant => variant;
    public TreeSpecies.Info Species => species;
    public Transform Model => model;
    public float WidthFactor => widthFactor;
    // as thick as the model's trunk (measured from its bark: Forest.MeasureTrunk), else the species' guess
    public float TrunkRadius => Mathf.Max(0.22f, data.height * (variant != null && variant.trunkRatio > 0f
        ? variant.trunkRatio * widthFactor * species.widthScale : species.trunkPerMetre));
    public float CrownRadius => variant.crownRatio * data.height * widthFactor * species.widthScale;
    public float Sink => Mathf.Min(0.25f, 0.02f * data.height);

    // reuse: a model already in the scene (or kept from before) is used instead of making a new one
    public void Setup(Forest forest, ForestTreeData data, Forest.Variant variant, GameObject reuse)
    {
        this.forest = forest;
        this.data = data;
        this.variant = variant;
        species = TreeSpecies.Get(data.species);
        transform.position = data.position;
        standing = Quaternion.Euler(0f, data.rotation, 0f);
        transform.rotation = standing;
        // each tree a little wider or slimmer than the next
        widthFactor = 0.88f + 0.24f * Mathf.Repeat(Mathf.Sin(data.position.x * 12.9898f + data.position.z * 78.233f) * 43758.5453f, 1f);

        if (reuse != null)
        {
            model = reuse.transform;
            model.SetParent(transform, false);
        }
        else
        {
            model = Instantiate(variant.prefab, transform).transform;
        }
        model.name = "Model";
        model.localRotation = Quaternion.identity;
        forest.PrepareModel(model.gameObject, data.species);
        renderers = model.GetComponentsInChildren<Renderer>(true);

        trunk = gameObject.AddComponent<CapsuleCollider>(); // round like the trunk (a box stuck out at its corners)
        trunk.direction = 1;
        choppable = gameObject.AddComponent<ChoppableTree>();
        choppable.caloriesSpentChoppingWood = 20f;

        ModelShown = true;
        ShowModel(false);
        Apply();
        choppable.treeHealth = choppable.treeMaxHealth;
    }

    // the model goes back to the forest's pool (a loaded game reuses it)
    public GameObject TakeModel()
    {
        if (model == null) return null;
        var m = model.gameObject;
        model = null;
        return m;
    }

    // the 3D tree near the player; far away Forest draws its picture instead
    public void ShowModel(bool show)
    {
        if (ModelShown == show) return;
        ModelShown = show;
        if (renderers == null) return;
        foreach (var r in renderers) if (r != null) r.enabled = show;
        if (show && NeedsApply) Apply();
    }

    // size of the model, the trunk and how many hits it takes, for the current height
    public void Apply()
    {
        float h = Mathf.Max(0.2f, data.height);
        float k = h / variant.nativeHeight;
        float wide = k * widthFactor * species.widthScale;
        model.localScale = new Vector3(variant.prefabScale.x * wide, variant.prefabScale.y * k, variant.prefabScale.z * wide);
        model.localPosition = new Vector3(0f, -variant.baseOffset * k - Sink, 0f);

        trunk.radius = Mathf.Max(0.22f, TrunkRadius);
        trunk.height = Mathf.Max(trunk.radius * 2f, h * 0.92f);
        trunk.center = new Vector3(0f, trunk.height * 0.5f, 0f);

        int hits = TreeSpecies.Hits(h);
        bool untouched = choppable.treeHealth >= choppable.treeMaxHealth || choppable.treeMaxHealth <= 0f;
        choppable.treeMaxHealth = hits;
        if (untouched) choppable.treeHealth = hits;
        choppable.caloriesSpentChoppingWood = 20f * Mathf.Clamp(h / 20f, 0.25f, 1f);
        appliedHeight = h;
    }

    public bool NeedsApply => Mathf.Abs(data.height - appliedHeight) > 0.08f;

    // the axe hit it: a short wobble
    public void Shake()
    {
        if (Falling) return;
        if (shaking != null) StopCoroutine(shaking);
        shaking = StartCoroutine(ShakeRoutine());
    }

    IEnumerator ShakeRoutine()
    {
        Vector3 away = AwayFromPlayer();
        Vector3 axis = Vector3.Cross(Vector3.up, away);
        float strength = Mathf.Lerp(4f, 1.2f, Mathf.InverseLerp(2f, 35f, data.height));
        for (float t = 0f; t < 0.45f; t += Time.deltaTime)
        {
            float angle = Mathf.Sin(t * 30f) * strength * (1f - t / 0.45f);
            model.rotation = Quaternion.AngleAxis(angle, axis) * standing;
            yield return null;
        }
        model.localRotation = Quaternion.identity;
        shaking = null;
    }

    Vector3 AwayFromPlayer()
    {
        Vector3 away = transform.position - forest.PlayerPosition;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        return away.normalized;
    }

    // chopped down: fall over, then logs and a stump
    public void Fall()
    {
        if (Falling) return;
        Falling = true;
        if (shaking != null) StopCoroutine(shaking);
        ShowModel(true);
        forest.OnFallStarted(this);
        StartCoroutine(FallRoutine(AwayFromPlayer()));
    }

    IEnumerator FallRoutine(Vector3 away)
    {
        trunk.enabled = false;
        choppable.playerInRange = false;
        choppable.canBeChopped = false;
        var sound = SoundManager.Instance;
        float h = Mathf.Max(0.5f, data.height);
        float loud = Mathf.Clamp01(0.35f + h / 35f); // big trees are louder
        if (sound != null) sound.PlayTreeCrack(transform.position + Vector3.up * Mathf.Min(h * 0.3f, 4f), loud);

        // a falling pole: slow at first, faster and faster (made a bit quicker than real life)
        Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;
        float pull = 2.2f * 1.5f * 9.81f / h; // rad/s² per sin(angle)
        float fallTime = FallTime(pull);
        bool rushed = false;
        float angle = 3f, speed = 8f;
        float time = 0f;
        Vector3 basePosition = model.position;
        while (angle < 86f && time < 5f)
        {
            float dt = Time.deltaTime;
            speed += pull * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;
            angle = Mathf.Min(86f, angle + speed * dt);
            model.rotation = Quaternion.AngleAxis(angle, axis) * standing;
            model.position = basePosition;
            time += dt;
            // the crown rushing through the air, ending when it lands
            if (!rushed && h > 4f && time >= fallTime - 1.75f)
            {
                rushed = true;
                if (sound != null) sound.PlayBranchRush(transform.position + away * h * 0.6f + Vector3.up * h * 0.3f, loud);
            }
            yield return null;
        }
        if (sound != null) sound.PlayTreeImpact(transform.position + away * h * 0.45f, loud);
        yield return new WaitForSeconds(0.35f);
        forest.OnFelled(this, away);
    }

    // how long the fall takes (the same steps as above, worked out in advance)
    static float FallTime(float pull)
    {
        float angle = 3f, speed = 8f, time = 0f;
        const float dt = 1f / 60f;
        while (angle < 86f && time < 5f)
        {
            speed += pull * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;
            angle += speed * dt;
            time += dt;
        }
        return time;
    }
}
