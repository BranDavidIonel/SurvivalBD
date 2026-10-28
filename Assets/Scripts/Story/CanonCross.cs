using UnityEngine;

// The cross of Brother Ioan's canon (level 9): made of 5 planks (Crafting > Construction), raised last, on the top of
// the other mountain (MiracleStream.Summit). Then water springs from the rock under it (MiracleStream).
// An Orthodox cross of planks: a short bar at the top, the long bar, and the slanted foot bar; stones around its foot.
// The story keeps where it stands (StoryData.crossPosition).
public class CanonCross : MonoBehaviour
{
    public static CanonCross Instance { get; private set; }

    [Tooltip("The cross must stand this close to the top of the other mountain (metres)")]
    public const float SummitReach = 45f;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // it stands there (made once)
    public static CanonCross Show(Vector3 at, float yaw)
    {
        if (Instance != null) return Instance;
        var go = BuildModel();
        go.name = "Brother Ioan's canon cross";
        at.y = Forest.GroundY(at);
        go.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
        // solid: the player walks around it
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, 2.8f, 0f);
        box.size = new Vector3(0.5f, 5.6f, 0.5f);
        return go.AddComponent<CanonCross>();
    }

    // is this the top of the other mountain?
    public static bool CanStandAt(Vector3 p, out string why)
    {
        why = "";
        var stream = MiracleStream.Instance;
        if (stream == null) return true;
        Vector3 top = stream.Summit;
        Vector3 d = p - top; d.y = 0f;
        if (d.magnitude > SummitReach || p.y < top.y - 14f)
        {
            why = Lang.T("Not here: on the top of the other mountain (Menu > Map)", "Nu aici: pe vârful celuilalt munte (Meniu > Hartă)");
            return false;
        }
        return true;
    }

    public static GameObject BuildModel()
    {
        var root = new GameObject("Cross model");
        var wood = Buildings.OldWood; // dark brown, like old wood (the crosses on the mountains)
        Beam(root.transform, wood, new Vector3(0f, 2.75f, 0f), new Vector3(0.32f, 5.5f, 0.26f), 0f);    // the post
        Beam(root.transform, wood, new Vector3(0f, 4.05f, 0f), new Vector3(2.9f, 0.3f, 0.24f), 0f);     // the long bar
        Beam(root.transform, wood, new Vector3(0f, 4.95f, 0f), new Vector3(1.2f, 0.22f, 0.22f), 0f);    // the short bar at the top
        Beam(root.transform, wood, new Vector3(0f, 1.55f, 0f), new Vector3(1.5f, 0.22f, 0.22f), -22f);  // the slanted foot bar
        // stones at its foot
        var stone = new Material(Shader.Find("Standard")) { name = "Cross stones", hideFlags = HideFlags.DontSave, color = new Color(0.46f, 0.45f, 0.43f) };
        stone.SetFloat("_Glossiness", 0.12f);
        var rng = new System.Random(9);
        for (int i = 0; i < 9; i++)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyCollider(s);
            s.name = "Stone";
            s.transform.SetParent(root.transform, false);
            float a = i * Mathf.PI * 2f / 9f + (float)rng.NextDouble() * 0.4f;
            float r = 0.45f + (float)rng.NextDouble() * 0.2f;
            float size = 0.35f + (float)rng.NextDouble() * 0.25f;
            s.transform.localPosition = new Vector3(Mathf.Cos(a) * r, size * 0.25f, Mathf.Sin(a) * r);
            s.transform.localScale = new Vector3(size, size * 0.7f, size * 0.9f);
            s.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f);
            s.GetComponent<MeshRenderer>().sharedMaterial = stone;
        }
        return root;
    }

    static void Beam(Transform parent, Material material, Vector3 at, Vector3 size, float tilt)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        DestroyCollider(b);
        b.name = "Beam";
        b.transform.SetParent(parent, false);
        b.transform.localPosition = at;
        b.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        b.transform.localScale = size;
        b.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    static void DestroyCollider(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c == null) return;
        if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
    }
}
