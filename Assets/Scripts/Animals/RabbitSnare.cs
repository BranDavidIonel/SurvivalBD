using System.Collections.Generic;
using UnityEngine;

// A snare for rabbits (Crafting > Survival: 1 stick + 5 hemp): a stick pushed into the ground holds a loop of hemp cord.
// Set it on the ground (SnarePlacing). Now and then a grown rabbit nearby comes to it and is caught (the little ones
// slip through the loop). A caught rabbit stays there, struggling:
//  - with an axe in the hand: hit it (meat and hide, like hunting)
//  - with empty hands: take it alive into the inventory ("Rabbit (male)" / "Rabbit (female)"). Drop it or use it
//    and it is let go: it runs away... unless it is in a pen with the door shut (then it stays and makes babies).
// An empty snare can be taken back (look at it, click). The save keeps the snares and their rabbits.
public class RabbitSnare : MonoBehaviour
{
    public static readonly List<RabbitSnare> All = new List<RabbitSnare>();

    [Tooltip("A grown rabbit this close may come to the snare")]
    public const float LureDistance = 40f;
    [Tooltip("Chance per game hour that a rabbit nearby comes to an empty snare")]
    public const float LurePerHour = 0.35f;
    // where a caught rabbit stands: in the loop
    public Vector3 Loop => transform.position + transform.forward * 0.15f;

    public RabbitLife caught;     // the rabbit in it (alive or killed, until its body is taken)
    public RabbitLife lured;      // a rabbit on its way to it
    BoxCollider box;
    InteractableObject pick;
    Transform player;
    float checkTimer;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    public bool Empty => caught == null;

    // ---------------- making one ---------------- //

    public static RabbitSnare Spawn(Vector3 at, float yaw)
    {
        var go = BuildModel();
        go.name = "Snare";
        at.y = Buildings.GroundAt(at);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        var snare = go.AddComponent<RabbitSnare>();
        snare.box = go.AddComponent<BoxCollider>();
        snare.box.center = new Vector3(0f, 0.45f, 0f);
        snare.box.size = new Vector3(1.0f, 0.9f, 1.0f);
        var playerMovement = FindObjectOfType<PlayerMovement>();
        var controller = playerMovement != null ? playerMovement.GetComponent<CharacterController>() : null;
        if (controller != null) Physics.IgnoreCollision(snare.box, controller);
        snare.pick = go.AddComponent<InteractableObject>();
        snare.pick.ItemName = "snare";
        snare.pick.displayName = "Snare (empty)";
        snare.pick.onPicked = p => { if (p != null) Destroy(p.gameObject); };
        return snare;
    }

    // the model: a stick in the ground, a loop of hemp cord in front of it, tied to it (also the inventory icon)
    public static GameObject BuildModel()
    {
        var root = new GameObject("Snare model");
        var stick = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyCollider(stick);
        stick.name = "Stick";
        stick.transform.SetParent(root.transform, false);
        stick.transform.localPosition = new Vector3(-0.42f, 0.45f, -0.05f);
        stick.transform.localRotation = Quaternion.Euler(0f, 0f, -7f);
        stick.transform.localScale = new Vector3(0.07f, 0.55f, 0.07f);
        stick.GetComponent<MeshRenderer>().sharedMaterial = Buildings.Stick;

        var loop = new GameObject("Loop");
        loop.transform.SetParent(root.transform, false);
        loop.transform.localPosition = new Vector3(0f, 0.36f, 0.15f);
        loop.AddComponent<MeshFilter>().sharedMesh = Buildings.RingMesh(0.27f, 0.02f, 28, 5);
        loop.AddComponent<MeshRenderer>().sharedMaterial = Buildings.Cord;

        // the cord from the loop to the top of the stick, and the knot
        var cord = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        DestroyCollider(cord);
        cord.name = "Cord";
        cord.transform.SetParent(root.transform, false);
        Vector3 a = new Vector3(-0.25f, 0.55f, 0.15f), b = new Vector3(-0.47f, 0.86f, -0.05f);
        cord.transform.localPosition = (a + b) * 0.5f;
        cord.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
        cord.transform.localScale = new Vector3(0.025f, (b - a).magnitude * 0.5f, 0.025f);
        cord.GetComponent<MeshRenderer>().sharedMaterial = Buildings.Cord;
        var knot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        DestroyCollider(knot);
        knot.name = "Knot";
        knot.transform.SetParent(root.transform, false);
        knot.transform.localPosition = b;
        knot.transform.localScale = Vector3.one * 0.1f;
        knot.GetComponent<MeshRenderer>().sharedMaterial = Buildings.Cord;
        foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return root;
    }

    static void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
    }

    // ---------------- catching ---------------- //

    // can this rabbit be caught? (grown up, free, not already in a snare)
    static bool Catchable(RabbitLife r) => r != null && r.Alive && r.IsAdult && r.caughtIn == null;

    public void Catch(RabbitLife r)
    {
        if (!Empty || !Catchable(r)) return;
        caught = r;
        if (lured != null && lured != r && lured.lure == this) lured.lure = null;
        lured = null;
        r.OnCaught(this);
        if (box != null) box.enabled = false; // the crosshair finds the rabbit now
    }

    // the rabbit was taken alive (or its body taken): the snare is empty again
    public void Release()
    {
        caught = null;
        if (box != null) box.enabled = true;
    }

    void Update()
    {
        // the rabbit in it was taken away (alive or its body)
        if (caught == null && box != null && !box.enabled) Release();
        if (caught != null && caught.caughtIn != this) Release();

        if (pick != null)
        {
            if (player == null)
            {
                var movement = FindObjectOfType<PlayerMovement>();
                if (movement != null) player = movement.transform;
            }
            if (player != null) pick.playerInRange = Empty && (player.position - transform.position).sqrMagnitude < 4f * 4f;
        }

        if (!Empty) return;
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.2f;
        var population = RabbitPopulation.Instance;
        if (population == null) return;
        // a grown rabbit that walks into the loop is caught
        Vector3 loop = Loop;
        foreach (var r in population.Rabbits)
        {
            if (!Catchable(r) || !r.Active) continue;
            Vector3 d = r.transform.position - loop; d.y = 0f;
            if (d.sqrMagnitude < 0.6f * 0.6f) { Catch(r); return; }
        }
    }

    // RabbitPopulation, once a second (hours: game hours since the last time): rabbits come to the empty snares
    public static void Tick(float hours)
    {
        var population = RabbitPopulation.Instance;
        if (population == null || All.Count == 0) return;
        float chance = 1f - Mathf.Exp(-LurePerHour * hours);
        foreach (var snare in All)
        {
            if (snare == null || !snare.Empty) continue;
            var r = snare.lured;
            if (r != null && (!Catchable(r) || r.lure != snare)) { snare.lured = null; r = null; }
            if (r == null)
            {
                if (Random.value > chance) continue;
                snare.lured = r = Nearest(population, snare);
                if (r != null) r.lure = snare;
            }
            // far from the player nobody sees it walk there: caught right away
            if (r != null && !r.Active) snare.Catch(r);
        }
    }

    static RabbitLife Nearest(RabbitPopulation population, RabbitSnare snare)
    {
        RabbitLife best = null;
        float bestDistance = LureDistance * LureDistance;
        Vector3 at = snare.transform.position;
        foreach (var r in population.Rabbits)
        {
            if (!Catchable(r) || r.lure != null || r.IsLeaving) continue;
            Vector3 d = r.transform.position - at; d.y = 0f;
            float dist = d.sqrMagnitude;
            if (dist >= bestDistance) continue;
            if (Buildings.Blocks(r.transform.position, at)) continue; // a wall between them
            bestDistance = dist;
            best = r;
        }
        return best;
    }

    // ---------------- save / load ---------------- //

    public static List<SnareData> GetData()
    {
        var list = new List<SnareData>();
        foreach (var s in All)
        {
            if (s == null) continue;
            list.Add(new SnareData
            {
                position = s.transform.position,
                yaw = s.transform.eulerAngles.y,
                caught = s.caught != null && s.caught.Alive,
                sex = s.caught != null ? (int)s.caught.sex : 0,
            });
        }
        return list;
    }

    // after the rabbits were loaded: the caught ones go back into their snares
    public static void SetData(List<SnareData> saved)
    {
        foreach (var s in All.ToArray()) if (s != null) Destroy(s.gameObject);
        All.Clear();
        if (saved == null) return;
        var population = RabbitPopulation.Instance;
        foreach (var d in saved)
        {
            var snare = Spawn(d.position, d.yaw);
            if (!d.caught || population == null) continue;
            RabbitLife best = null;
            float bestDistance = 2f * 2f;
            foreach (var r in population.Rabbits)
            {
                if (!Catchable(r) || (int)r.sex != d.sex) continue;
                float dist = (r.transform.position - snare.Loop).sqrMagnitude;
                if (dist < bestDistance) { bestDistance = dist; best = r; }
            }
            if (best != null) snare.Catch(best);
        }
    }
}

[System.Serializable]
public class SnareData
{
    public Vector3 position;
    public float yaw;
    public bool caught;   // a live rabbit was in it
    public int sex;       // 0 male, 1 female
}
