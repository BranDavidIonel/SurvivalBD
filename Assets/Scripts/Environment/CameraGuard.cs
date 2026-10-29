using UnityEngine;

// The camera sits at the player's head, but the whole player tilts when looking up or down (MouseMovement), so looking
// down swung the camera about 1.5 m forward (looking up: back): next to a tree it went into the trunk, and the player
// saw through the tree (and through walls and rocks). Every frame, after the player moved and turned, this keeps the
// camera on the near side of anything solid between the head and the camera's place. Added by EnvironmentBootstrap.
[DefaultExecutionOrder(10000)] // after everything that moves or turns the player
public class CameraGuard : MonoBehaviour
{
    const float Radius = 0.32f;    // room kept around the camera (more than its near plane's corners)
    const float NearPlane = 0.18f; // it was 0.3: a trunk close by was cut open

    Transform parent, body;
    CharacterController controller;
    Vector3 localSpot;
    readonly RaycastHit[] hits = new RaycastHit[24];

    public static void AddTo(Camera cam)
    {
        if (cam == null || cam.GetComponent<CameraGuard>() != null) return;
        if (cam.GetComponentInParent<PlayerMovement>() == null) return; // only the player's own camera
        cam.gameObject.AddComponent<CameraGuard>();
    }

    void Start()
    {
        parent = transform.parent;
        localSpot = transform.localPosition;
        controller = GetComponentInParent<CharacterController>();
        body = controller != null ? controller.transform : parent;
        var cam = GetComponent<Camera>();
        if (cam != null && cam.nearClipPlane > NearPlane) cam.nearClipPlane = NearPlane;
    }

    void LateUpdate()
    {
        // (the raft's ending carries the camera away: then it is left alone)
        if (parent == null || transform.parent != parent || controller == null || !controller.enabled) return;
        transform.localPosition = localSpot;
        Vector3 want = transform.position;

        // from the middle of the body, at the camera's height (inside the player's capsule nothing solid can be)
        Bounds b = controller.bounds;
        float r = controller.radius;
        Vector3 head = new Vector3(b.center.x, Mathf.Clamp(want.y, b.min.y + r, b.max.y - r), b.center.z);
        Vector3 to = want - head;
        float distance = to.magnitude;
        if (distance < 0.001f) return;
        Vector3 dir = to / distance;

        int n = Physics.SphereCastNonAlloc(head, Radius, dir, hits, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float free = distance;
        for (int i = 0; i < n; i++)
        {
            var hit = hits[i];
            if (!Blocks(hit.collider)) continue;
            free = Mathf.Min(free, hit.distance); // (0 when it already touches it at the head)
        }
        if (free < distance) transform.position = head + dir * Mathf.Max(0f, free - 0.02f);
    }

    // trunks, the ground, walls, rocks: yes. The player, things lying around, plants and animals: no
    bool Blocks(Collider c)
    {
        if (c == null || c.isTrigger || !c.enabled) return false;
        if (body != null && c.transform.IsChildOf(body)) return false;
        if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) return false;
        if (c.CompareTag("Pickable")) return false;
        if (c.GetComponent<InteractableObject>() != null || c.GetComponent<TreeSeed>() != null) return false;
        if (c.GetComponentInParent<ScenePlant>() != null || c.GetComponentInParent<Animal>() != null) return false;
        return true;
    }
}
