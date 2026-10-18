using UnityEngine;

// While placing (campfire, storage box) or building (foundation, wall), the item hangs on a holding spot
// in front of the player, at the player's height, and it tilts when the player looks up or down.
// That left it floating in the air, so it was never valid to place. These helpers keep it upright and
// standing on the terrain wherever the player looks.
public static class GroundSnap
{
    const float Clearance = 0.02f;

    static int GroundMask => LayerMask.GetMask("Ground");

    public static void KeepOnGround(Transform item, Transform holdingSpot)
    {
        // stand upright, facing the same way as the player
        item.rotation = Quaternion.Euler(0f, holdingSpot.eulerAngles.y, 0f);

        if (!TryGetBounds(item, out Bounds bounds)) return;
        Vector3 from = new Vector3(bounds.center.x, bounds.max.y + 10f, bounds.center.z);
        if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 80f, GroundMask, QueryTriggerInteraction.Ignore))
        {
            item.position += Vector3.up * (hit.point.y + Clearance - bounds.min.y);
        }
    }

    // Is the bottom of the item touching the terrain?
    public static bool IsOnGround(Transform item)
    {
        if (!TryGetBounds(item, out Bounds bounds)) return false;
        Vector3 from = new Vector3(bounds.center.x, bounds.min.y + 0.5f, bounds.center.z);
        return Physics.Raycast(from, Vector3.down, 1f, GroundMask, QueryTriggerInteraction.Ignore);
    }

    // The visible size of the item (its own mesh if it has one, otherwise all its child meshes).
    static bool TryGetBounds(Transform item, out Bounds bounds)
    {
        var own = item.GetComponent<Renderer>();
        if (own != null && own.enabled)
        {
            bounds = own.bounds;
            return true;
        }

        bounds = default;
        bool found = false;
        foreach (var renderer in item.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer is ParticleSystemRenderer) continue;
            if (found) bounds.Encapsulate(renderer.bounds);
            else { bounds = renderer.bounds; found = true; }
        }
        return found;
    }
}
