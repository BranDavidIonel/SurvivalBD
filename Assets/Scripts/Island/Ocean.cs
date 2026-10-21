using UnityEngine;

// The sea around the island. The object sits at the middle of the island at sea level; the water mesh is a big
// ring that starts just under the beach and reaches kilometres out, so the island looks surrounded by sea from
// anywhere (the shader keeps the far water at the horizon). The shape of the island is stored as the distance
// from the middle to the waterline in every direction (made by Tools > Island > Build Island And Ocean).
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class Ocean : MonoBehaviour
{
    public static Ocean Instance { get; private set; }

    [Tooltip("Distance from this object to the waterline, evenly spread around the island (first one towards +X, then counter-clockwise).")]
    public float[] shoreRadius = new float[0];
    [Tooltip("How far under the beach the water mesh starts (metres).")]
    public float startUnderBeach = 15f;
    [Tooltip("How far out the sea goes (metres).")]
    public float seaRadius = 20000f;

    // sea level in the world
    public float Level => transform.position.y;

    // rings close together near the beach (where the big waves are seen up close), then further and further apart
    static readonly float[] RingOffsets = MakeRingOffsets();
    const int Segments = 768; // keeps the mesh under 65535 vertices

    static float[] MakeRingOffsets()
    {
        var list = new System.Collections.Generic.List<float> { -10f, -5f, 0f };
        for (var o = 3f; o <= 120f; o += 3f) list.Add(o);
        for (var o = 125f; o <= 250f; o += 5f) list.Add(o);
        list.AddRange(new[] { 300f, 350f, 500f, 700f, 950f, 1300f, 1800f, 2500f, 3500f, 5000f, 7000f, 10000f, 14000f });
        return list.ToArray();
    }

    static readonly int WaterLevelId = Shader.PropertyToID("_WaterLevel");
    static readonly int SkyTexAId = Shader.PropertyToID("_SkyTexA");
    static readonly int SkyTexBId = Shader.PropertyToID("_SkyTexB");
    static readonly int SkyBlendId = Shader.PropertyToID("_SkyBlend");
    static readonly int SkyMulId = Shader.PropertyToID("_SkyMul");
    static readonly int SkyRotationId = Shader.PropertyToID("_SkyRotation");
    static readonly int HasSkyId = Shader.PropertyToID("_HasSky");

    Mesh mesh;
    MeshRenderer meshRenderer;
    MaterialPropertyBlock block;
    int builtFor;

    void OnEnable()
    {
        Instance = this;
        meshRenderer = GetComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        BuildMesh();
        UpdateMaterial();
        Camera.onPreCull -= PrepareCamera;
        Camera.onPreCull += PrepareCamera;
    }

    void OnDisable()
    {
        Camera.onPreCull -= PrepareCamera;
        if (Instance == this) Instance = null;
    }

    void OnDestroy()
    {
        if (mesh != null) DestroyImmediate(mesh);
    }

    void OnValidate()
    {
        builtFor = 0;
    }

    void LateUpdate()
    {
        if (builtFor != ShapeHash()) BuildMesh();
        UpdateMaterial();
    }

    // How far the point is from the waterline, measured outwards from the middle of the island:
    // negative on the island, 0 at the waterline, positive out at sea.
    public float DistanceFromShore(Vector3 position)
    {
        var centre = transform.position;
        var x = position.x - centre.x;
        var z = position.z - centre.z;
        return Mathf.Sqrt(x * x + z * z) - ShoreRadius(Mathf.Atan2(z, x));
    }

    // Height of the water surface here, big waves included (same maths as the shader, so swimmers ride the waves).
    public float SurfaceHeight(Vector3 position)
    {
        var material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
        if (material == null || !material.HasProperty(BigWaveHeightId)) return Level;
        var height = material.GetFloat(BigWaveHeightId);
        var length = Mathf.Max(0.1f, material.GetFloat(BigWaveLengthId));
        var wind = material.GetFloat(BigWaveDirectionId) * Mathf.Deg2Rad;
        var speed = material.GetFloat(BigWaveSpeedId);
        var calm = material.GetFloat(BigWaveShoreCalmId);
        var fade = calm > 0f ? Smooth01(DistanceFromShore(position) / calm) : 1f;
        if (height <= 0f || fade <= 0f) return Level;

        var time = Time.timeSinceLevelLoad; // the shader's _Time.y
        var sum = 0f;
        for (var n = 0; n < BigWaveSet.Length; n++)
        {
            var w = BigWaveSet[n];
            var a = wind + w.x;
            var k = 2f * Mathf.PI / (length * w.y);
            var phase = k * (Mathf.Cos(a) * position.x + Mathf.Sin(a) * position.z) - Mathf.Sqrt(9.81f * k) * speed * time + n * 1.7f;
            sum += height * w.z * Mathf.Sin(phase);
        }
        return Level + sum * fade;
    }

    // keep in step with BigWaveSet in IslandOcean.shader
    static readonly Vector3[] BigWaveSet = { new Vector3(0f, 1f, 0.5f), new Vector3(0.6f, 0.62f, 0.27f), new Vector3(-0.45f, 0.41f, 0.15f), new Vector3(1.2f, 0.27f, 0.08f) };
    static readonly int BigWaveHeightId = Shader.PropertyToID("_BigWaveHeight");
    static readonly int BigWaveLengthId = Shader.PropertyToID("_BigWaveLength");
    static readonly int BigWaveDirectionId = Shader.PropertyToID("_BigWaveDirection");
    static readonly int BigWaveSpeedId = Shader.PropertyToID("_BigWaveSpeed");
    static readonly int BigWaveShoreCalmId = Shader.PropertyToID("_BigWaveShoreCalm");

    static float Smooth01(float x)
    {
        x = Mathf.Clamp01(x);
        return x * x * (3f - 2f * x);
    }

    public float ShoreRadius(float angle)
    {
        var count = shoreRadius != null ? shoreRadius.Length : 0;
        if (count == 0) return 0f;
        var f = Mathf.Repeat(angle / (2f * Mathf.PI), 1f) * count;
        var i = Mathf.FloorToInt(f);
        return Mathf.Lerp(shoreRadius[i % count], shoreRadius[(i + 1) % count], f - i);
    }

    int ShapeHash()
    {
        unchecked
        {
            var hash = 17 + Mathf.RoundToInt(startUnderBeach * 10f) * 31 + Mathf.RoundToInt(seaRadius);
            if (shoreRadius != null) foreach (var r in shoreRadius) hash = hash * 31 + Mathf.RoundToInt(r * 10f);
            return hash;
        }
    }

    void BuildMesh()
    {
        builtFor = ShapeHash();
        if (mesh == null)
        {
            mesh = new Mesh { name = "Ocean (generated)", hideFlags = HideFlags.DontSave };
        }
        mesh.Clear();
        if (shoreRadius == null || shoreRadius.Length == 0)
        {
            GetComponent<MeshFilter>().sharedMesh = mesh;
            return;
        }

        // rings around the island: the first one under the beach, the last one far out at sea
        var rings = new System.Collections.Generic.List<float> { -startUnderBeach };
        foreach (var offset in RingOffsets) if (offset > -startUnderBeach && offset < seaRadius) rings.Add(offset);
        rings.Add(seaRadius);

        var vertices = new Vector3[rings.Count * Segments];
        var uvs = new Vector2[vertices.Length]; // x: metres out from the waterline (the shader keeps the beach calm)
        for (var s = 0; s < Segments; s++)
        {
            var angle = s * 2f * Mathf.PI / Segments;
            var shore = ShoreRadius(angle);
            var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            for (var r = 0; r < rings.Count; r++)
            {
                vertices[r * Segments + s] = direction * Mathf.Max(1f, shore + rings[r]);
                uvs[r * Segments + s] = new Vector2(rings[r], 0f);
            }
        }

        var triangles = new int[(rings.Count - 1) * Segments * 6];
        var t = 0;
        for (var r = 0; r < rings.Count - 1; r++)
        {
            for (var s = 0; s < Segments; s++)
            {
                int a = r * Segments + s, b = r * Segments + (s + 1) % Segments;
                int c = a + Segments, d = b + Segments;
                // facing up (clockwise seen from above)
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = b; triangles[t++] = d; triangles[t++] = c;
            }
        }

        var normals = new Vector3[vertices.Length];
        for (var i = 0; i < normals.Length; i++) normals[i] = Vector3.up;

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        // never culled: it is always around the camera
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(seaRadius * 2f, 200f, seaRadius * 2f));
        GetComponent<MeshFilter>().sharedMesh = mesh;
    }

    // The water reflects whatever sky is showing (the day/night system swaps skyboxes).
    void UpdateMaterial()
    {
        if (meshRenderer == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        meshRenderer.GetPropertyBlock(block);
        block.SetFloat(WaterLevelId, Level);

        var sky = RenderSettings.skybox;
        Texture a = null, b = null;
        var blend = 0f;
        var multiplier = Color.white;
        var rotation = 0f;
        if (sky != null)
        {
            if (sky.HasProperty("_Tex") && sky.GetTexture("_Tex") is Cubemap cube)
            {
                // Skybox/Cubemap: colour = texture * tint * 2 (in gamma) * exposure
                a = cube;
                var tint = sky.HasProperty("_Tint") ? sky.GetColor("_Tint") : new Color(0.5f, 0.5f, 0.5f);
                var exposure = sky.HasProperty("_Exposure") ? sky.GetFloat("_Exposure") : 1f;
                var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                multiplier = (linear ? tint.linear * 4.594794f : tint * 2f) * exposure;
                rotation = sky.HasProperty("_Rotation") ? sky.GetFloat("_Rotation") : 0f;
            }
            else if (sky.HasProperty("_AtmosphereTex") && sky.HasProperty("_SpaceTex"))
            {
                // Custom/SkyboxTransition (day to night)
                a = sky.GetTexture("_AtmosphereTex");
                b = sky.GetTexture("_SpaceTex");
                blend = sky.HasProperty("_TransitionFactor") ? sky.GetFloat("_TransitionFactor") : 0f;
            }
        }

        var cubeA = a as Cubemap;
        var cubeB = b as Cubemap;
        if (cubeA == null)
        {
            cubeA = cubeB;
            cubeB = null;
        }
        if (cubeA != null)
        {
            block.SetTexture(SkyTexAId, cubeA);
            block.SetTexture(SkyTexBId, cubeB != null ? cubeB : cubeA);
        }
        block.SetFloat(SkyBlendId, cubeB != null ? blend : 0f);
        block.SetColor(SkyMulId, multiplier);
        block.SetFloat(SkyRotationId, rotation);
        block.SetFloat(HasSkyId, cubeA != null ? 1f : 0f);
        meshRenderer.SetPropertyBlock(block);
    }

    // The shader needs the depth of what is under the water (for the colours and the foam on the beach).
    void PrepareCamera(Camera cam)
    {
        if (cam.cameraType != CameraType.Game && cam.cameraType != CameraType.SceneView) return;
        if ((cam.cullingMask & (1 << gameObject.layer)) == 0) return;
        cam.depthTextureMode |= DepthTextureMode.Depth;
    }
}
