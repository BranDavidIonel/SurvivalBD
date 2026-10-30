using UnityEngine;

// A spear in the world (Spear.cs): flying, stuck, or lying on the ground.
//  - flying: it falls a little (gravity), always pointing along its flight; each frame a ray from its tip finds what it
//    hits (the player, the things lying around and other spears are passed through)
//  - it hits a rabbit (any Animal): Animal.TakeDamage(Spear.Damage) kills it and the spear stays stuck in the body;
//    when the body is looted (SelectionManager) the spear is moved out and falls on the ground
//  - it hits the ground, a tree, a rock or a building: it sticks in it at its angle
//  - stuck or lying, it is a thing to take back (a solid box, "Spear", the hand icon) and the save keeps it (DroppedItem)
public class ThrownSpear : MonoBehaviour
{
    // the last spear thrown, and what it hit (for a test from a script)
    public static ThrownSpear Last { get; private set; }
    public static Animal LastHitAnimal { get; private set; } // (stays after the spear is taken back)
    public bool Flying { get; private set; }
    public Animal HitAnimal { get; private set; }

    Vector3 velocity;
    float flightTime;
    Animal stuckIn;
    bool inAnimal;      // (stuckIn is null once the body is destroyed: this remembers it was in one)
    DroppedItem dropped;
    InteractableObject pick;
    BoxCollider box;

    static readonly RaycastHit[] hits = new RaycastHit[24];

    Vector3 Tip => transform.position + transform.forward * Spear.TipForward;

    void Awake()
    {
        foreach (var r in GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        // (kept in the save from the moment it leaves the hand)
        dropped = gameObject.AddComponent<DroppedItem>();
        dropped.item = Spear.Item;
    }

    public void Launch(Vector3 startVelocity)
    {
        velocity = startVelocity;
        Flying = true;
        Last = this;
        // something right in front of the player (a wall, a tree): the spear is already at it
        var cam = Camera.main;
        if (cam != null && FirstHit(cam.transform.position, Tip, out RaycastHit near))
        {
            Hit(near, (Tip - cam.transform.position).normalized);
            return;
        }
        if (velocity.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(velocity);
    }

    void Update()
    {
        if (Flying)
        {
            Fly(Time.deltaTime);
            return;
        }
        // stuck in an animal that was looted (the spear was moved out of the body) or that is gone: down to the ground
        if (inAnimal && (stuckIn == null || !stuckIn.gameObject.activeInHierarchy || transform.parent != stuckIn.transform))
            LieOnGround(transform.position, transform.eulerAngles.y);
    }

    void Fly(float dt)
    {
        if (dt <= 0f) return;
        dt = Mathf.Min(dt, 0.05f);
        flightTime += dt;
        Vector3 newVelocity = velocity + Vector3.down * Spear.Gravity * dt;
        Vector3 step = (velocity + newVelocity) * 0.5f * dt;
        velocity = newVelocity;
        Vector3 from = Tip;
        if (FirstHit(from, from + step, out RaycastHit hit))
        {
            Hit(hit, step.normalized);
            return;
        }
        transform.position += step;
        transform.rotation = Quaternion.LookRotation(velocity); // point first
        // (lost: far out over the sea, or under the island)
        if (flightTime > 6f || transform.position.y < -100f) LieOnGround(transform.position, transform.eulerAngles.y);
    }

    bool FirstHit(Vector3 from, Vector3 to, out RaycastHit best)
    {
        best = default;
        Vector3 d = to - from;
        float length = d.magnitude;
        if (length < 0.0001f) return false;
        int n = Physics.RaycastNonAlloc(from, d / length, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDistance = float.MaxValue;
        bool found = false;
        for (int i = 0; i < n; i++)
        {
            if (Spear.Ignored(hits[i].collider, transform)) continue;
            if (hits[i].distance < bestDistance) { bestDistance = hits[i].distance; best = hits[i]; found = true; }
        }
        return found;
    }

    // it hits something: an animal is hurt (a rabbit dies), and the spear sticks in it a little
    void Hit(RaycastHit hit, Vector3 direction)
    {
        Flying = false;
        if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
        var animal = hit.collider.GetComponentInParent<Animal>();
        float depth;
        if (animal != null)
        {
            HitAnimal = animal;
            LastHitAnimal = animal;
            if (!animal.isDead) animal.TakeDamage(Spear.Damage); // (its own cry and blood)
            depth = 0.28f;
        }
        else
        {
            depth = hit.collider is TerrainCollider ? 0.32f : 0.12f; // deep in the earth, a little in wood or stone
            PlayThud(hit);
        }
        transform.rotation = Quaternion.LookRotation(direction);
        transform.position = hit.point + direction * depth - direction * Spear.TipForward;
        if (animal != null)
        {
            transform.SetParent(animal.transform, true);
            stuckIn = animal;
            inAnimal = true;
        }
        MakePickable(animal == null);
    }

    void PlayThud(RaycastHit hit)
    {
        var sound = SoundManager.Instance;
        if (sound == null) return;
        bool earth = hit.collider is TerrainCollider;
        var wood = sound.chopSound != null ? sound.chopSound.clip : null;
        if (!earth && wood != null) sound.PlayAt(wood, hit.point, 0.7f, Random.Range(1.15f, 1.3f), 45f, 3f);
        else sound.PlayAt(ProceduralAudio.TreeImpact, hit.point, 0.45f, Random.Range(2.1f, 2.4f), 40f, 3f);
    }

    // a spear from a saved game, stuck at this exact place and angle
    public void StandAt(Vector3 at, Quaternion rotation)
    {
        Flying = false;
        transform.SetPositionAndRotation(at, rotation);
        MakePickable(true);
    }

    // flat on the ground under 'at' (dropped from the inventory, out of a looted body, or from a saved game)
    public void LieOnGround(Vector3 at, float yaw)
    {
        Flying = false;
        stuckIn = null;
        inAnimal = false;
        transform.SetParent(null, true);
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 tip = at + forward * Spear.TipForward, butt = at - forward * Spear.ButtBack;
        tip.y = DroppedItems.GroundUnder(tip) + 0.05f;
        butt.y = DroppedItems.GroundUnder(butt) + 0.03f;
        Vector3 along = (tip - butt).normalized;
        transform.rotation = Quaternion.LookRotation(along) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
        transform.position = butt + along * Spear.ButtBack;
        MakePickable(false);
    }

    // a solid box to aim at (the hand icon, "Spear"), taken back into the inventory with the usual tap or click
    void MakePickable(bool standing)
    {
        if (box == null)
        {
            box = gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0f, (Spear.TipForward - Spear.ButtBack) * 0.5f);
            box.size = new Vector3(0.4f, 0.4f, Spear.Length + 0.1f); // (wider than the shaft: easy to aim at on a phone)
            var player = FindObjectOfType<PlayerMovement>();
            var controller = player != null ? player.GetComponent<CharacterController>() : null;
            if (controller != null) Physics.IgnoreCollision(box, controller); // the player walks through it
        }
        if (pick == null)
        {
            pick = gameObject.AddComponent<InteractableObject>();
            pick.ItemName = Spear.Item;
            pick.displayName = Spear.Name;
            pick.onPicked = p => { if (p != null) Destroy(p.gameObject); };
        }
        dropped.pick = pick;
        dropped.standing = standing;
    }

    // back into the inventory, as when the player looks at it and taps / clicks (also for a test from a script);
    // false while it flies or when the inventory is full
    public bool TakeBack()
    {
        var inventory = InventorySystem.Instance;
        if (Flying || inventory == null || !inventory.CheckSlotsAvailable(1)) return false;
        if (!inventory.AddToIventory(Spear.Item)) return false;
        Destroy(gameObject);
        return true;
    }

    void OnDestroy()
    {
        if (Last == this) Last = null;
    }
}
