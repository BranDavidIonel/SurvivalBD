using System.Collections;
using UnityEngine;

// One pine of the pine forest (made by PineForest). It grows over game time, shakes when the axe hits it,
// and when it is chopped down it falls over (away from the player) and leaves dark pine logs and a stump.
// Chopping goes through the game's usual ChoppableTree (on the same object, with the trunk's BoxCollider).
public class PineTree : MonoBehaviour
{
    public PineData data;

    PineForest forest;
    PineForest.Variant variant;
    Transform pivot, model;
    BoxCollider trunk;
    ChoppableTree choppable;
    LODGroup lod;
    float appliedHeight = -1f;
    float widthFactor = 1f;
    Coroutine shaking;

    public bool Falling { get; private set; }
    public ChoppableTree Choppable => choppable;
    public float TrunkRadius => Mathf.Max(0.22f, 0.02f * data.height);

    public void Setup(PineForest forest, PineData data, PineForest.Variant variant)
    {
        this.forest = forest;
        this.data = data;
        this.variant = variant;
        transform.position = data.position;
        transform.rotation = Quaternion.Euler(0f, data.rotation, 0f);
        // each pine a little wider or slimmer than the next
        widthFactor = 0.88f + 0.24f * Mathf.Repeat(Mathf.Sin(data.position.x * 12.9898f + data.position.z * 78.233f) * 43758.5453f, 1f);

        pivot = new GameObject("Pivot").transform;
        pivot.SetParent(transform, false);

        model = Instantiate(variant.prefab, pivot).transform;
        model.name = "Model";
        model.localRotation = Quaternion.identity;
        foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c); // the trunk box below is enough
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            r.sharedMaterials = forest.InstancedMaterials(r.sharedMaterials);
        }

        trunk = gameObject.AddComponent<BoxCollider>();
        choppable = gameObject.AddComponent<ChoppableTree>();
        choppable.caloriesSpentChoppingWood = 20f;

        // far away and small: not drawn (mostly young pines)
        lod = gameObject.AddComponent<LODGroup>();
        lod.SetLODs(new[] { new LOD(0.012f, renderers) });

        Apply();
        choppable.treeHealth = choppable.treeMaxHealth;
    }

    // size of the model, the trunk and how many hits it takes, for the current height
    public void Apply()
    {
        float h = Mathf.Max(0.2f, data.height);
        float k = h / variant.nativeHeight;
        model.localScale = new Vector3(variant.prefabScale.x * k * widthFactor, variant.prefabScale.y * k, variant.prefabScale.z * k * widthFactor);
        model.localPosition = new Vector3(0f, -variant.baseOffset * k - Mathf.Min(0.25f, 0.02f * h), 0f);

        float width = Mathf.Max(0.45f, TrunkRadius * 2f);
        trunk.size = new Vector3(width, h * 0.92f, width);
        trunk.center = new Vector3(0f, h * 0.46f, 0f);

        int hits = Hits(h);
        bool untouched = choppable.treeHealth >= choppable.treeMaxHealth || choppable.treeMaxHealth <= 0f;
        choppable.treeMaxHealth = hits;
        if (untouched) choppable.treeHealth = hits;
        choppable.caloriesSpentChoppingWood = 20f * Mathf.Clamp(h / 20f, 0.25f, 1f);

        if (Mathf.Abs(h - appliedHeight) > 0.5f || appliedHeight < 0f) lod.RecalculateBounds();
        appliedHeight = h;
    }

    public bool NeedsApply => Mathf.Abs(data.height - appliedHeight) > 0.04f;

    // small pines fall with one or two hits, the biggest need 8
    public static int Hits(float height) => Mathf.Clamp(Mathf.RoundToInt(1f + height / 5.5f), 1, 8);

    // 1 log from a small pine, up to 5 from a very big one (30 m and more)
    public static int Logs(float height) => Mathf.Clamp(Mathf.RoundToInt(1f + 4f * (height - 3f) / 27f), 1, 5);

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
            pivot.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
            yield return null;
        }
        pivot.localRotation = Quaternion.identity;
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
        StartCoroutine(FallRoutine(AwayFromPlayer()));
    }

    IEnumerator FallRoutine(Vector3 away)
    {
        trunk.enabled = false;
        choppable.playerInRange = false;
        choppable.canBeChopped = false;
        var sound = SoundManager.Instance;
        float h = Mathf.Max(0.5f, data.height);
        float loud = Mathf.Clamp01(0.35f + h / 35f); // big pines are louder
        if (sound != null) sound.PlayTreeCrack(transform.position + Vector3.up * Mathf.Min(h * 0.3f, 4f), loud);

        // a falling pole: slow at first, faster and faster (made a bit quicker than real life)
        Vector3 axis = Vector3.Cross(Vector3.up, away).normalized;
        float pull = 2.2f * 1.5f * 9.81f / h; // rad/s² per sin(angle)
        float fallTime = FallTime(pull);
        bool rushed = false;
        float angle = 3f, speed = 8f;
        float time = 0f;
        while (angle < 86f && time < 5f)
        {
            float dt = Time.deltaTime;
            speed += pull * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Rad2Deg * dt;
            angle = Mathf.Min(86f, angle + speed * dt);
            pivot.rotation = Quaternion.AngleAxis(angle, axis) * transform.rotation;
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
