using UnityEngine;

// A column of warm light over the cave's mouth (made in code by Cave), so the player can find the cave:
// it shines in Level 5 (find the cave) and Level 6 (go in with a torch), and goes out in the other levels.
// Seen from far away: a wide soft column with a bright core, 150 m high, and a big glow at its foot; the fog does
// not hide it (SB_Beacon shader). It fades when the player is right next to it.
public class CaveBeacon : MonoBehaviour
{
    public Cave cave;

    [Tooltip("Height of the column of light (metres)")]
    public float height = 150f;
    [Tooltip("Width of the soft column (metres); the bright core is a quarter of it")]
    public float width = 8f;
    public Color color = new Color(1f, 0.78f, 0.4f, 1f);
    [Tooltip("How bright it is")]
    public float brightness = 2.4f;

    GameObject root;
    Material soft, core, halo;
    Transform haloQuad;
    Light glow;
    float tick;

    void Start()
    {
        Build();
        Refresh();
    }

    void Build()
    {
        if (cave == null || cave.path.Count < 2) return;
        Vector3 into = cave.path[1] - cave.path[0]; into.y = 0f;
        into = into.sqrMagnitude > 0.0001f ? into.normalized : Vector3.forward;
        // just in front of the mouth, standing on the ground
        Vector3 at = cave.path[0] - into * 3f;
        at.y = Forest.GroundY(at) - 0.5f;
        transform.position = at;

        root = new GameObject("Light");
        root.transform.SetParent(transform, false);
        var shader = Resources.Load<Shader>("Environment/SB_Beacon");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        soft = NewMaterial(shader, SoftEdge(1.4f), 0.35f);
        core = NewMaterial(shader, SoftEdge(3f), 0.25f);
        halo = NewMaterial(shader, EnvTextures.Dot, 0.2f);
        Column("Soft column", width, height, soft, 0.75f);
        Column("Bright core", width * 0.25f, height * 0.85f, core, 1f);

        // a big glow at the foot (it turns to face the camera)
        var quad = new GameObject("Glow at the foot");
        quad.transform.SetParent(root.transform, false);
        quad.transform.localPosition = Vector3.up * 4f;
        quad.AddComponent<MeshFilter>().sharedMesh = QuadMesh(18f);
        var qr = quad.AddComponent<MeshRenderer>();
        qr.sharedMaterial = halo;
        qr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        haloQuad = quad.transform;

        // a warm light on the rocks of the mouth (at night it shows the way in)
        var lightObject = new GameObject("Glow light");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = Vector3.up * 3f;
        glow = lightObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 22f;
        glow.intensity = 2f;
        glow.shadows = LightShadows.None;
    }

    Material NewMaterial(Shader shader, Texture texture, float opacity)
    {
        var m = new Material(shader) { name = "Cave beacon (made in code)", hideFlags = HideFlags.DontSave };
        m.mainTexture = texture;
        m.SetColor("_Tint", color);
        m.SetFloat("_Intensity", brightness);
        m.SetFloat("_Opacity", opacity);
        return m;
    }

    void Column(string name, float w, float h, Material material, float alpha)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = ColumnMesh(w, h, alpha);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
    }

    void Update()
    {
        tick -= Time.deltaTime;
        if (tick <= 0f) { tick = 0.5f; Refresh(); }
        if (root == null || !root.activeSelf) return;
        // it breathes slowly, and fades when the player is next to it (so it does not blind him at the mouth)
        float near = 1f;
        var state = PlayerState.Instance;
        if (state != null && state.playerBody != null)
        {
            Vector3 d = state.playerBody.transform.position - transform.position; d.y = 0f;
            near = Mathf.Clamp01((d.magnitude - 8f) / 30f);
        }
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 1.3f);
        float a = Mathf.Lerp(0.12f, 1f, near) * pulse;
        var tint = new Color(color.r, color.g, color.b, a);
        soft.SetColor("_Tint", tint);
        core.SetColor("_Tint", tint);
        halo.SetColor("_Tint", tint);
        glow.intensity = 2f * pulse;
        var cam = Camera.main;
        if (cam != null && haloQuad != null)
        {
            Vector3 look = haloQuad.position - cam.transform.position;
            if (look.sqrMagnitude > 0.01f) haloQuad.rotation = Quaternion.LookRotation(look, Vector3.up);
        }
    }

    void Refresh()
    {
        if (root == null) return;
        var story = StoryManager.Instance;
        int level = story != null ? story.CurrentLevel : 0;
        bool show = cave != null && cave.IsOpen && (level == 5 || level == 6);
        if (root.activeSelf != show) root.SetActive(show);
    }

    // three planes crossed at 60 degrees, bright at the bottom and fading out at the top
    static Mesh ColumnMesh(float w, float h, float alpha)
    {
        var mesh = new Mesh { name = "Cave beacon" };
        var v = new Vector3[12];
        var uv = new Vector2[12];
        var col = new Color[12];
        var tri = new int[18];
        for (int p = 0; p < 3; p++)
        {
            float a = p * Mathf.PI / 3f;
            Vector3 side = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * w * 0.5f;
            int s = p * 4;
            v[s] = -side; v[s + 1] = side; v[s + 2] = side + Vector3.up * h; v[s + 3] = -side + Vector3.up * h;
            uv[s] = new Vector2(0f, 0f); uv[s + 1] = new Vector2(1f, 0f); uv[s + 2] = new Vector2(1f, 1f); uv[s + 3] = new Vector2(0f, 1f);
            col[s] = col[s + 1] = new Color(1f, 1f, 1f, alpha);
            col[s + 2] = col[s + 3] = new Color(1f, 1f, 1f, 0f);
            int t = p * 6;
            tri[t] = s; tri[t + 1] = s + 2; tri[t + 2] = s + 1;
            tri[t + 3] = s; tri[t + 4] = s + 3; tri[t + 5] = s + 2;
        }
        mesh.vertices = v;
        mesh.uv = uv;
        mesh.colors = col;
        mesh.triangles = tri;
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh QuadMesh(float size)
    {
        var mesh = new Mesh { name = "Cave beacon glow" };
        float s = size * 0.5f;
        mesh.vertices = new[] { new Vector3(-s, -s, 0f), new Vector3(s, -s, 0f), new Vector3(s, s, 0f), new Vector3(-s, s, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // soft at both sides of each plane (a higher sharpness: a thinner bright line)
    static Texture2D SoftEdge(float sharpness)
    {
        var tex = new Texture2D(64, 4, TextureFormat.RGBA32, false) { name = "Beacon edge", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
        for (int x = 0; x < 64; x++)
        {
            float u = (x + 0.5f) / 64f;
            float a = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(u * 2f - 1f)), sharpness);
            for (int y = 0; y < 4; y++) tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        tex.Apply();
        return tex;
    }
}
