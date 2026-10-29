using UnityEngine;

// One rabbit (added in code by RabbitPopulation to every rabbit): male or female, its age (babies are small and grow
// up), mating, and how it moves: it nibbles the grass, hops around, walks to a partner, hops away when hit.
// It replaces the old AI_Movement (which used the animation's own motion and could stray).
// Walls stop it (a rabbit pen) and it hops up onto the foundations; an empty snare nearby may draw it in (RabbitSnare);
// an open pen door lets it out (PenDoor).
public class RabbitLife : MonoBehaviour
{
    public RabbitInfo.Sex sex;
    public float ageHours = 100f;       // game hours old
    public float pregnantHours = -1f;   // game hours until the babies come (-1: not expecting)
    public float restHours;             // game hours until it can mate again

    public Animal animal;
    public RabbitLife mate;             // the partner it walks to
    public RabbitSnare caughtIn;        // the snare that holds it
    public RabbitSnare lure;            // the snare it walks to

    Animator animator;
    Vector3 adultScale;
    Vector3 target;
    bool moving;
    float speed;
    float waitTimer;
    float fleeTimer;
    float lastHit = -100f;
    float appliedGrowth = -1f;
    Vector3[] path;                     // going out through an open door
    int pathIndex;
    float pathSpeed;
    float struggleTimer;
    float struggleYaw;

    public bool Alive => animal != null && !animal.isDead;
    public bool IsAdult => ageHours >= RabbitPopulation.AdultHours;
    public float Growth => Mathf.Clamp01(ageHours / RabbitPopulation.AdultHours);
    public bool Expecting => pregnantHours >= 0f;
    public bool ReadyToMate => Alive && IsAdult && restHours <= 0f && !Expecting && caughtIn == null;
    public bool Active { get; private set; } = true;
    public bool IsLeaving => path != null && pathIndex < path.Length;
    // its drawn parts (RabbitPopulation hides the far ones; looked up once, not twice a second for every rabbit)
    public SkinnedMeshRenderer[] Renderers => renderers != null ? renderers : (renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true));
    SkinnedMeshRenderer[] renderers;

    public void Setup(RabbitInfo.Sex sex, float ageHours, Vector3 adultScale)
    {
        this.sex = sex;
        this.ageHours = ageHours;
        this.adultScale = adultScale;
        animal = GetComponent<Animal>();
        animator = GetComponent<Animator>();
        if (animator != null) animator.applyRootMotion = false; // the script moves it, not the animation
        var oldAi = GetComponent<AI_Movement>();
        if (oldAi != null) oldAi.enabled = false;
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false; // it follows the ground in code
        FitColliders(adultScale.x);
        // two blows of the stone axe, one of the iron axe (chasing it four times on a phone was too much)
        if (animal != null && !animal.isDead) animal.SetMaxHealth(20);
        waitTimer = Random.Range(0.5f, 4f);
        ApplyGrowth();
        UpdateName();
        SnapToGround();
    }

    // The rabbit prefab came with a solid box of 5.5 x 4.2 m around it (and a trigger even bigger): it caught the
    // crosshair meant for the mushrooms and grass around it, and stood in the player's way. Now the box is about
    // the size of the rabbit (a bit bigger, to be easy to hit with a finger); the trigger is not needed any more.
    void FitColliders(float scale)
    {
        scale = Mathf.Max(0.01f, Mathf.Abs(scale));
        foreach (var box in GetComponents<BoxCollider>())
        {
            if (box.isTrigger) { box.enabled = false; continue; }
            box.center = new Vector3(0f, 0.45f, 0f) / scale;
            box.size = new Vector3(1.1f, 0.9f, 1.4f) / scale;
        }
    }

    // babies are small, they reach the full size when they are grown up
    public void ApplyGrowth()
    {
        float g = Growth;
        if (Mathf.Abs(g - appliedGrowth) < 0.02f) return;
        appliedGrowth = g;
        transform.localScale = adultScale * Mathf.Lerp(0.45f, 1f, g);
    }

    // the name shown by the crosshair
    public void UpdateName()
    {
        if (animal == null) return;
        string kind = sex == RabbitInfo.Sex.Male ? "male" : "female";
        animal.animalName = IsAdult ? "Rabbit (" + kind + ")" : "Baby rabbit (" + kind + ")";
    }

    public string LabelText
    {
        get
        {
            bool ro = Lang.Ro;
            string s = sex == RabbitInfo.Sex.Male ? (ro ? "Mascul" : "Male") : (ro ? "Femelă" : "Female");
            if (caughtIn != null) return s + (ro ? " - în laț" : " - in a snare");
            if (!IsAdult) return ro ? "Pui " + s.ToLowerInvariant() : "Baby " + s.ToLowerInvariant();
            if (Expecting) return s + (ro ? " (are pui)" : " (expecting)");
            return s;
        }
    }

    public void SetActive(bool on)
    {
        if (Active == on) return;
        Active = on;
        if (animator != null) animator.enabled = on || !Alive;
        if (!on) Stop();
    }

    public void OnHit()
    {
        lastHit = Time.time;
    }

    // a snare caught it: it stays in the loop
    public void OnCaught(RabbitSnare snare)
    {
        caughtIn = snare;
        lure = null;
        path = null;
        if (mate != null && mate.mate == this) mate.mate = null;
        mate = null;
        moving = false;
        Vector3 at = snare.Loop;
        at.y = Buildings.GroundAt(at);
        transform.position = at;
        struggleYaw = snare.transform.eulerAngles.y + 90f;
        transform.rotation = Quaternion.Euler(0f, struggleYaw, 0f);
    }

    // out through an open pen door: these points one after another
    public void FollowPath(Vector3[] points, float pathSpeed)
    {
        if (caughtIn != null || points == null || points.Length == 0) return;
        path = points;
        pathIndex = 0;
        this.pathSpeed = pathSpeed;
        lure = null;
    }

    // let go from the hands: it hops away from the player
    public void RunFrom(Vector3 player, float distance)
    {
        Vector3 away = transform.position - player; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = transform.forward;
        target = transform.position + away.normalized * distance;
        speed = 4.2f;
        moving = true;
        fleeTimer = 1.6f;
    }

    void SnapToGround()
    {
        Vector3 p = transform.position;
        p.y = Buildings.GroundAt(p);
        transform.position = p;
    }

    void Stop()
    {
        moving = false;
        if (animator != null && animator.isActiveAndEnabled && Alive) animator.SetBool("isRunning", false);
    }

    static bool IsGoodGround(Vector3 p)
    {
        float ground = Forest.GroundY(p);
        if (Ocean.Instance != null && (ground < Ocean.Instance.Level + 0.6f || Ocean.Instance.DistanceFromShore(p) > -2f)) return false;
        if (River.IsWater(p)) return false;
        return true;
    }

    void Update()
    {
        if (!Active && Alive) return;

        Vector3 here = transform.position;
        Vector3 player = RabbitPopulation.Instance != null ? RabbitPopulation.Instance.PlayerPosition : here;
        Vector3 toPlayer = player - here; toPlayer.y = 0f;

        // close enough for the axe, or to take its meat and hide (before, a trigger decided this and some rabbits
        // could never be hit)
        animal.playerInRange = toPlayer.sqrMagnitude < 4.5f * 4.5f;
        if (!Alive)
        {
            moving = false;
            return;
        }

        if (caughtIn != null)
        {
            Struggle();
            return;
        }

        // hit: a short hop away from the player (it stays close enough to be followed)
        if (Time.time - lastHit < 0.1f && fleeTimer <= 0f)
        {
            fleeTimer = 1.2f;
            Vector3 away = -toPlayer.normalized;
            if (away.sqrMagnitude < 0.01f) away = transform.forward;
            target = here + away * 5f;
            speed = 4f;
            moving = true;
            path = null;
        }
        if (fleeTimer > 0f) fleeTimer -= Time.deltaTime;

        if (fleeTimer <= 0f && IsLeaving)
        {
            // going out through the open door
            target = path[pathIndex];
            speed = pathSpeed;
            moving = true;
        }
        else if (fleeTimer <= 0f && mate != null && mate.Alive)
        {
            // walking to its partner
            Vector3 toMate = mate.transform.position - here; toMate.y = 0f;
            if (toMate.sqrMagnitude > 1.1f * 1.1f)
            {
                target = mate.transform.position;
                speed = 2.4f;
                moving = true;
            }
            else
            {
                Stop();
            }
        }
        else if (fleeTimer <= 0f && lure != null)
        {
            // to the snare (the loop)
            target = lure.Loop;
            speed = 2.2f;
            moving = true;
        }
        else if (!moving)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f) PickWanderTarget(here);
        }

        if (moving) Move(here);
    }

    // in the snare: it tugs and twists, now and then
    void Struggle()
    {
        struggleTimer -= Time.deltaTime;
        if (struggleTimer > 0f) return;
        bool tug = Random.value < 0.55f;
        struggleTimer = tug ? Random.Range(0.25f, 0.6f) : Random.Range(0.8f, 2.2f);
        float yaw = caughtIn != null ? caughtIn.transform.eulerAngles.y + 90f : struggleYaw;
        transform.rotation = Quaternion.Euler(0f, yaw + (tug ? Random.Range(-35f, 35f) : 0f), 0f);
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool("isRunning", tug);
    }

    void PickWanderTarget(Vector3 here)
    {
        for (int i = 0; i < 4; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float d = Random.Range(3f, IsAdult ? 12f : 5f);
            Vector3 p = here + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            if (!IsGoodGround(p) || Buildings.Blocks(here, p)) continue; // (in a pen: somewhere inside)
            target = p;
            speed = IsAdult ? Random.Range(1.4f, 2.2f) : 1.1f;
            moving = true;
            return;
        }
        waitTimer = Random.Range(1f, 3f);
    }

    void Move(Vector3 here)
    {
        Vector3 to = target - here; to.y = 0f;
        float dist = to.magnitude;
        if (dist < 0.3f)
        {
            if (path != null)
            {
                // the next point of the way out
                pathIndex++;
                if (pathIndex >= path.Length) { path = null; Stop(); waitTimer = Random.Range(1f, 3f); }
                return;
            }
            Stop();
            waitTimer = Random.Range(2f, 7f); // nibbling the grass
            return;
        }
        Vector3 dir = to / dist;
        Vector3 next = here + dir * Mathf.Min(dist, speed * Time.deltaTime);
        float nextY = Buildings.GroundAt(next);
        // water, the sea, a wall (or a shut door), or a step too high (the side of a foundation on a slope)
        if (!IsGoodGround(next) || nextY - here.y > 1.1f || Buildings.Blocks(here, next + dir * 0.35f))
        {
            Stop();
            waitTimer = Random.Range(0.5f, 2f);
            fleeTimer = 0f;
            path = null;
            if (lure != null) { lure.lured = null; lure = null; }
            // a wall between it and its partner: they give up
            if (mate != null) { if (mate.mate == this) mate.mate = null; mate = null; }
            return;
        }
        next.y = nextY;
        transform.position = next;
        var look = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, look, 360f * Time.deltaTime);
        if (animator != null && animator.isActiveAndEnabled) animator.SetBool("isRunning", true);
    }
}
