using System.Collections.Generic;
using UnityEngine;

// The door of a door wall (added when the door wall is built): look at it and click (phone: "Open" / "Close") to open or
// shut it; it swings away from the player. While it stands open the rabbits inside the pen find the way out, one after
// another, so shut it behind you when you go in or out. The save keeps whether it is open (Buildings).
public class PenDoor : MonoBehaviour
{
    public static readonly List<PenDoor> All = new List<PenDoor>();

    public Transform hinge;
    public float openAngle = 100f;
    public float turnSpeed = 260f;
    [Tooltip("Each second, the chance that a rabbit inside the pen goes out through the open door")]
    public float escapeChance = 0.18f;
    [Tooltip("Rabbits this close to an open door may find it")]
    public float escapeDistance = 14f;

    public bool IsOpen { get; private set; }
    public int Side { get; private set; } = 1;   // which way it swung open (+1 / -1)

    float angle;
    float tick;
    static AudioClip knock;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    // the door itself (not its frame) was looked at
    public static PenDoor FromCollider(Collider c)
    {
        if (c == null) return null;
        var door = c.GetComponentInParent<PenDoor>();
        return door != null && door.hinge != null && c.transform.IsChildOf(door.hinge) ? door : null;
    }

    public void Toggle(Vector3 player)
    {
        if (!IsOpen)
        {
            // a positive turn moves the door to the wall's +x side: open it on the other side from the player
            Side = transform.InverseTransformPoint(player).x > 0f ? -1 : 1;
        }
        IsOpen = !IsOpen;
        if (SoundManager.Instance != null)
        {
            if (knock == null) knock = Resources.Load<AudioClip>("Sounds/Craft/impactWood_medium_001");
            SoundManager.Instance.PlayAt(knock, transform.position, 0.55f, IsOpen ? 0.75f : 0.9f, 40f);
        }
    }

    // a loaded game
    public void SetOpen(bool open, int side)
    {
        IsOpen = open;
        Side = side >= 0 ? 1 : -1;
        angle = open ? openAngle * Side : 0f;
        Apply();
    }

    void Apply()
    {
        if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, angle, 0f);
    }

    public static bool AnyOpenNear(Vector3 p, float distance)
    {
        foreach (var d in All)
        {
            if (d == null || !d.IsOpen) continue;
            Vector3 gap = d.transform.position - p; gap.y = 0f;
            if (gap.sqrMagnitude < distance * distance) return true;
        }
        return false;
    }

    void Update()
    {
        float target = IsOpen ? openAngle * Side : 0f;
        if (!Mathf.Approximately(angle, target))
        {
            angle = Mathf.MoveTowards(angle, target, turnSpeed * Time.deltaTime);
            Apply();
        }
        if (!IsOpen) return;

        // the door stands open: the rabbits in the pen find it
        tick -= Time.deltaTime;
        if (tick > 0f) return;
        tick = 1f;
        var population = RabbitPopulation.Instance;
        if (population == null) return;
        Vector3 c = transform.position;
        Vector3 n = transform.right; n.y = 0f;
        if (n.sqrMagnitude < 0.01f) return;
        n.Normalize();
        Vector3 along = transform.forward; along.y = 0f; along.Normalize();
        foreach (var r in population.Rabbits)
        {
            if (r == null || !r.Alive || !r.Active || r.caughtIn != null || r.IsLeaving) continue;
            Vector3 d = r.transform.position - c; d.y = 0f;
            if (d.sqrMagnitude > escapeDistance * escapeDistance) continue;
            if (Random.value > escapeChance) continue;
            if (!Buildings.IsEnclosed(r.transform.position)) continue;
            float s = Vector3.Dot(d, n) >= 0f ? 1f : -1f;
            Vector3 inside = c + n * s * 1.3f;
            Vector3 doorway = c - n * s * 1.3f;
            Vector3 away = c - n * s * Random.Range(7f, 12f) + along * Random.Range(-4f, 4f);
            r.FollowPath(new[] { inside, doorway, away }, Random.Range(2.8f, 3.6f));
        }
    }
}
