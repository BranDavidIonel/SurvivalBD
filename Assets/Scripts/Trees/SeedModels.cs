using System.Collections.Generic;
using UnityEngine;

// The seeds of the trees, made in code (no model files): pine cone, birch catkin, acorn, beechnut, and the little
// mound of earth of a planted seed. They are much bigger than real ones (about 0.3-0.6 m) so they are easy to see.
// Each seed has a gradient texture: the mesh's V coordinate picks the colour along it.
public static class SeedModels
{
    static Mesh[] meshes;
    static Material[] materials;
    static Mesh mound;
    static Material soil;

    public static Mesh MeshOf(int species)
    {
        if (meshes == null) Make();
        return meshes[Mathf.Clamp(species, 0, meshes.Length - 1)];
    }

    public static Material MaterialOf(int species)
    {
        if (materials == null) Make();
        return materials[Mathf.Clamp(species, 0, materials.Length - 1)];
    }

    // how big it is shown lying on the ground
    public static float ScaleOf(int species)
    {
        switch ((TreeSpecies.Kind)species)
        {
            case TreeSpecies.Kind.Pine: return 1.2f;   // 0.6 m cone
            case TreeSpecies.Kind.Birch: return 1.1f;
            default: return 1f;
        }
    }

    public static Mesh Mound { get { if (mound == null) Make(); return mound; } }
    public static Material Soil { get { if (soil == null) Make(); return soil; } }

    static void Make()
    {
        var standard = Shader.Find("Standard");
        meshes = new Mesh[TreeSpecies.Count];
        materials = new Material[TreeSpecies.Count];

        meshes[0] = ScaleCone(0.5f, 1f, 90, "Pine cone (made in code)");
        materials[0] = NewMaterial(standard, "Pine cone", Gradient(new Color(0.12f, 0.07f, 0.035f), new Color(0.27f, 0.16f, 0.085f), new Color(0.38f, 0.25f, 0.14f), new Color(0.2f, 0.12f, 0.06f)));

        meshes[1] = ScaleCone(0.36f, 0.42f, 110, "Birch catkin (made in code)");
        materials[1] = NewMaterial(standard, "Birch catkin", Gradient(new Color(0.18f, 0.14f, 0.06f), new Color(0.36f, 0.3f, 0.13f), new Color(0.52f, 0.45f, 0.22f), new Color(0.3f, 0.24f, 0.1f)));

        meshes[2] = Acorn();
        materials[2] = NewMaterial(standard, "Acorn", AcornTexture());

        meshes[3] = Beechnut();
        materials[3] = NewMaterial(standard, "Beechnut", BeechTexture());

        mound = MakeMound();
        soil = new Material(standard) { name = "Planted seed soil", color = new Color(0.2f, 0.13f, 0.07f), hideFlags = HideFlags.DontSave };
        if (soil.HasProperty("_Glossiness")) soil.SetFloat("_Glossiness", 0.05f);
    }

    static Material NewMaterial(Shader shader, string name, Texture2D texture)
    {
        var m = new Material(shader) { name = name, color = Color.white, mainTexture = texture, hideFlags = HideFlags.DontSave, enableInstancing = true };
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
        return m;
    }

    // ---------------- cones (pine cone, birch catkin) ---------------- //

    // A thin core with woody scales around it in a spiral (like a real cone), pointing out and a little towards
    // the tip. width: 1 = a pine cone, less = thinner (a birch catkin).
    static Mesh ScaleCone(float length, float width, int scales, string name)
    {
        var v = new List<Vector3>();
        var n = new List<Vector3>();
        var uv = new List<Vector2>();
        var tri = new List<int>();

        const int rings = 8, segments = 8;
        int coreStart = v.Count;
        for (int r = 0; r <= rings; r++)
        {
            float t = (float)r / rings;
            float radius = 0.055f * width * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, t));
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                v.Add(dir * radius + Vector3.up * (t - 0.5f) * length * 0.92f);
                n.Add(dir);
                uv.Add(new Vector2(0.5f, 0.05f));
            }
        }
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                int i = coreStart + r * (segments + 1) + s, j = i + segments + 1;
                tri.Add(i); tri.Add(j); tri.Add(i + 1);
                tri.Add(i + 1); tri.Add(j); tri.Add(j + 1);
            }

        const float golden = 2.39996323f;
        for (int k = 0; k < scales; k++)
        {
            float t = (k + 0.5f) / scales;
            float y = (t - 0.5f) * length * 0.86f;
            float a = k * golden;
            var outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            var side = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
            float size = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Lerp(0.12f, 0.96f, t)), 0.75f) * (1.05f - 0.45f * t);
            float len = (0.025f + 0.14f * size) * width;
            float wide = (0.018f + 0.07f * size) * Mathf.Lerp(width, 1f, 0.4f);
            float thick = 0.012f + 0.014f * size;
            var dir = (outward * Mathf.Cos(0.42f) + Vector3.up * Mathf.Sin(0.42f)).normalized;
            var up = Vector3.Cross(side, dir).normalized;
            float core = 0.045f * width * Mathf.Sin(Mathf.PI * Mathf.Lerp(0.05f, 0.95f, t));
            AddScale(v, n, uv, tri, outward * core + Vector3.up * y, dir, side, up, len, wide, thick);
        }
        return Finish(name, v, n, uv, tri, false);
    }

    static void AddScale(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri,
        Vector3 root, Vector3 dir, Vector3 side, Vector3 up, float len, float width, float thick)
    {
        Vector3[] outline =
        {
            root - side * width * 0.3f,
            root - side * width * 0.5f + dir * len * 0.65f,
            root + dir * len,
            root + side * width * 0.5f + dir * len * 0.65f,
            root + side * width * 0.3f,
        };
        float[] along = { 0f, 0.65f, 1f, 0.65f, 0f };
        var top = new Vector3[5];
        var bottom = new Vector3[5];
        for (int i = 0; i < 5; i++)
        {
            float h = Mathf.Lerp(thick * 0.4f, thick, along[i]);
            top[i] = outline[i] + up * h * 0.5f;
            bottom[i] = outline[i] - up * h * 0.5f;
        }
        AddPolygon(v, n, uv, tri, top, along, up, 1f);
        var bottomReversed = new Vector3[5];
        var alongReversed = new float[5];
        for (int i = 0; i < 5; i++) { bottomReversed[i] = bottom[4 - i]; alongReversed[i] = along[4 - i]; }
        AddPolygon(v, n, uv, tri, bottomReversed, alongReversed, -up, 0.7f);
        for (int i = 0; i < 5; i++)
        {
            int j = (i + 1) % 5;
            Vector3 normal = Vector3.Cross(top[j] - top[i], bottom[i] - top[i]).normalized;
            int start = v.Count;
            v.Add(top[i]); v.Add(top[j]); v.Add(bottom[j]); v.Add(bottom[i]);
            for (int q = 0; q < 4; q++) n.Add(normal);
            uv.Add(new Vector2(0.5f, along[i] * 0.95f)); uv.Add(new Vector2(0.5f, along[j] * 0.95f));
            uv.Add(new Vector2(0.5f, along[j] * 0.95f)); uv.Add(new Vector2(0.5f, along[i] * 0.95f));
            tri.Add(start); tri.Add(start + 1); tri.Add(start + 2);
            tri.Add(start); tri.Add(start + 2); tri.Add(start + 3);
        }
    }

    static void AddPolygon(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector3[] points, float[] along, Vector3 normal, float shade)
    {
        int start = v.Count;
        for (int i = 0; i < points.Length; i++)
        {
            v.Add(points[i]);
            n.Add(normal);
            uv.Add(new Vector2(0.5f, along[i] * 0.9f * shade));
        }
        for (int i = 1; i < points.Length - 1; i++)
        {
            tri.Add(start); tri.Add(start + i); tri.Add(start + i + 1);
        }
    }

    // ---------------- acorn and beechnut (shapes turned around their axis) ---------------- //

    // profile: (radius, height) from the bottom to the top; v0..v1: the part of the texture along it
    static void Lathe(List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, Vector2[] profile, int segments,
        float v0, float v1, float twist = 0f, float bumps = 0f)
    {
        int start = v.Count;
        for (int i = 0; i < profile.Length; i++)
        {
            float t = (float)i / (profile.Length - 1);
            for (int s = 0; s <= segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f + twist * t;
                float r = profile[i].x * (1f + bumps * Mathf.Sin(a * 9f + i * 2.1f) * Mathf.Sin(t * Mathf.PI));
                v.Add(new Vector3(Mathf.Cos(a) * r, profile[i].y, Mathf.Sin(a) * r));
                uv.Add(new Vector2((float)s / segments, Mathf.Lerp(v0, v1, t)));
                n.Add(Vector3.up); // worked out below
            }
        }
        for (int i = 0; i < profile.Length - 1; i++)
            for (int s = 0; s < segments; s++)
            {
                int a = start + i * (segments + 1) + s, b = a + segments + 1;
                tri.Add(a); tri.Add(b); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
            }
    }

    // an egg-shaped nut with a scaly cap, ~0.32 m long (lying on its side when on the ground)
    static Mesh Acorn()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        var nut = new List<Vector2>();
        for (int i = 0; i <= 10; i++)
        {
            float t = i / 10f;                       // tip (0) to the cap (1)
            float y = Mathf.Lerp(-0.17f, 0.06f, t);
            float r = 0.105f * Mathf.Sin(Mathf.Lerp(0.02f, 0.62f, t) * Mathf.PI) / Mathf.Sin(0.62f * Mathf.PI) * (t < 0.08f ? 0.6f : 1f);
            nut.Add(new Vector2(Mathf.Max(0.004f, r), y));
        }
        Lathe(v, n, uv, tri, nut.ToArray(), 14, 0.02f, 0.45f);
        // the cap: a cup over the top of the nut, with a little stem
        Vector2[] cap =
        {
            new Vector2(0.104f, 0.03f), new Vector2(0.118f, 0.06f), new Vector2(0.112f, 0.1f),
            new Vector2(0.085f, 0.13f), new Vector2(0.045f, 0.148f), new Vector2(0.018f, 0.152f),
            new Vector2(0.012f, 0.19f), new Vector2(0.004f, 0.195f),
        };
        Lathe(v, n, uv, tri, cap, 14, 0.55f, 0.98f, 0f, 0.06f);
        return Finish("Acorn (made in code)", v, n, uv, tri, true);
    }

    // a three-sided brown nut sitting in an opened spiky husk, ~0.28 m
    static Mesh Beechnut()
    {
        var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
        // the husk: four flaps, open at the top (a cup with four lobes)
        var husk = new List<Vector2>();
        for (int i = 0; i <= 6; i++)
        {
            float t = i / 6f;
            husk.Add(new Vector2(Mathf.Lerp(0.02f, 0.13f, Mathf.Sqrt(t)), Mathf.Lerp(-0.09f, 0.03f, t)));
        }
        Lathe(v, n, uv, tri, husk.ToArray(), 8, 0.02f, 0.4f, 0f, 0.18f);
        // two triangular nuts standing in it
        for (int k = 0; k < 2; k++)
        {
            int start = v.Count;
            Vector2[] nut = { new Vector2(0.05f, -0.06f), new Vector2(0.065f, 0.0f), new Vector2(0.05f, 0.07f), new Vector2(0.004f, 0.13f) };
            Lathe(v, n, uv, tri, nut, 3, 0.55f, 0.95f, 0.3f);
            float a = k * Mathf.PI;
            var shift = new Vector3(Mathf.Cos(a) * 0.035f, 0f, Mathf.Sin(a) * 0.035f);
            var tilt = Quaternion.Euler(Mathf.Cos(a) * 14f, k * 60f, Mathf.Sin(a) * 14f);
            for (int i = start; i < v.Count; i++) v[i] = tilt * v[i] + shift;
        }
        return Finish("Beechnut (made in code)", v, n, uv, tri, true);
    }

    static Mesh Finish(string name, List<Vector3> v, List<Vector3> n, List<Vector2> uv, List<int> tri, bool computeNormals)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(tri, 0);
        if (computeNormals) mesh.RecalculateNormals(); else mesh.SetNormals(n);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------- textures ---------------- //

    // dark at the root of the scales, lighter towards their tips, a darker rim at the very tip
    static Texture2D Gradient(Color root, Color middle, Color tip, Color rim)
    {
        const int h = 64;
        var tex = NewTexture(h);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = t < 0.55f ? Color.Lerp(root, middle, t / 0.55f) : Color.Lerp(middle, tip, (t - 0.55f) / 0.4f);
            if (t > 0.94f) c = Color.Lerp(c, rim, (t - 0.94f) / 0.06f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // the nut: green-brown at the tip, shiny brown; the cap: grey-brown scales
    static Texture2D AcornTexture()
    {
        const int h = 64;
        var tex = NewTexture(h);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c;
            if (t < 0.5f) c = Color.Lerp(new Color(0.3f, 0.24f, 0.08f), new Color(0.55f, 0.33f, 0.12f), Mathf.Clamp01(t / 0.35f));
            else c = Color.Lerp(new Color(0.36f, 0.28f, 0.17f), new Color(0.24f, 0.18f, 0.1f), (t - 0.5f) / 0.5f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    // the husk: brown-olive, the nuts: shiny red-brown
    static Texture2D BeechTexture()
    {
        const int h = 64;
        var tex = NewTexture(h);
        for (int y = 0; y < h; y++)
        {
            float t = (float)y / (h - 1);
            Color c = t < 0.5f
                ? Color.Lerp(new Color(0.2f, 0.16f, 0.08f), new Color(0.42f, 0.36f, 0.2f), t / 0.5f)
                : Color.Lerp(new Color(0.4f, 0.2f, 0.1f), new Color(0.6f, 0.36f, 0.2f), (t - 0.5f) / 0.5f);
            for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
        }
        tex.Apply();
        return tex;
    }

    static Texture2D NewTexture(int h)
    {
        return new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
    }

    // ---------------- the mound of a planted seed ---------------- //

    // a low mound of earth, a bit uneven, its edge sinking into the ground
    static Mesh MakeMound()
    {
        const int rings = 6, segments = 18;
        const float radius = 0.45f, height = 0.13f;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        vertices.Add(new Vector3(0f, height, 0f));
        for (int r = 1; r <= rings; r++)
        {
            float t = (float)r / rings;
            for (int s = 0; s < segments; s++)
            {
                float a = (float)s / segments * Mathf.PI * 2f;
                float bump = 1f + 0.18f * Mathf.Sin(a * 3f + t * 5f) * t + 0.1f * Mathf.Sin(a * 7f + 1.3f) * t;
                float rr = radius * t * (r == rings ? 1.15f : 1f) * bump;
                float y = r == rings ? -0.04f : height * Mathf.Pow(1f - t * t, 0.8f) * (1f + 0.12f * Mathf.Sin(a * 5f + t * 9f));
                vertices.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
            }
        }
        for (int s = 0; s < segments; s++)
        {
            int a = 1 + s, b = 1 + (s + 1) % segments;
            triangles.Add(0); triangles.Add(b); triangles.Add(a);
        }
        for (int r = 1; r < rings; r++)
        {
            int start = 1 + (r - 1) * segments, next = 1 + r * segments;
            for (int s = 0; s < segments; s++)
            {
                int s1 = (s + 1) % segments;
                triangles.Add(start + s); triangles.Add(start + s1); triangles.Add(next + s);
                triangles.Add(start + s1); triangles.Add(next + s1); triangles.Add(next + s);
            }
        }
        var mesh = new Mesh { name = "Planted seed earth (made in code)" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    // the planted seed: a low mound of dark earth with the seed half in it (also the preview while planting)
    public static GameObject MakePlantedModel(string name, int species)
    {
        var root = new GameObject(name);
        var earth = new GameObject("Earth");
        earth.transform.SetParent(root.transform, false);
        earth.AddComponent<MeshFilter>().sharedMesh = Mound;
        earth.AddComponent<MeshRenderer>().sharedMaterial = Soil;
        var seed = new GameObject("Seed");
        seed.transform.SetParent(root.transform, false);
        seed.transform.localPosition = new Vector3(0.05f, species == 0 ? 0.13f : 0.1f, 0f);
        seed.transform.localRotation = Quaternion.Euler(0f, 0f, 35f); // standing in the earth, leaning
        seed.transform.localScale = Vector3.one * (species == 0 ? 0.85f : 1.1f);
        seed.AddComponent<MeshFilter>().sharedMesh = MeshOf(species);
        seed.AddComponent<MeshRenderer>().sharedMaterial = MaterialOf(species);
        return root;
    }
}
