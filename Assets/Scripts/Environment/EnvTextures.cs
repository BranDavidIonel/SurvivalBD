using UnityEngine;

// Small textures made in code (no image files needed): cloud/snow noise, flame shapes, sparks, smoke,
// rain streaks, splashes and snowflakes. Each one is made the first time it is asked for and then reused.
public static class EnvTextures
{
    static Texture2D noise, flames, dot, smoke, streak, splash, flake;

    // 256x256, tiles seamlessly. R: big soft shapes (clouds), G: smaller detail, B: other big shapes
    // (moon spots, snow patches), A: fine grain.
    public static Texture2D Noise => noise != null ? noise : (noise = MakeNoise());

    // 4 x 2 different flame shapes (each frame 64 x 128). Colour = heat (white-yellow core, red edges).
    public static Texture2D Flames => flames != null ? flames : (flames = MakeFlames());
    public const int FlameColumns = 4, FlameRows = 2;

    // soft round light (sparks, glow, coals)
    public static Texture2D Dot => dot != null ? dot : (dot = MakeDot(64, 2.2f));

    // soft puff of smoke with a cloudy inside
    public static Texture2D Smoke => smoke != null ? smoke : (smoke = MakeSmoke());

    // thin vertical rain streak
    public static Texture2D Streak => streak != null ? streak : (streak = MakeStreak());

    // little crown of drops where rain hits the ground
    public static Texture2D Splash => splash != null ? splash : (splash = MakeSplash());

    // snowflake: soft dot with a brighter middle
    public static Texture2D Flake => flake != null ? flake : (flake = MakeDot(32, 1.4f));

    // ash left in a campfire, seen from above: black charcoal in the middle, grey ash, lighter at the edge
    public static Texture2D Ash => ash != null ? ash : (ash = MakeAsh());
    static Texture2D ash;

    static Texture2D MakeAsh()
    {
        const int size = 128;
        var t = NewTexture("SB_Ash", size, size, false);
        var px = new Color32[size * size];
        Color charcoal = new Color(0.07f, 0.065f, 0.06f);
        Color grey = new Color(0.4f, 0.39f, 0.37f);
        Color light = new Color(0.63f, 0.61f, 0.58f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(u, v, 4, 4, 611);
                float fine = Fbm(u, v, 16, 2, 677);
                Color c = Color.Lerp(charcoal, grey, Smooth(0.05f, 0.6f, r + (n - 0.5f) * 0.5f));
                c = Color.Lerp(c, light, Smooth(0.55f, 0.95f, r + (fine - 0.5f) * 0.3f) * 0.8f);
                // lumps of charcoal and light flecks
                if (fine < 0.3f && r < 0.6f) c = Color.Lerp(c, charcoal, 0.7f);
                uint h = Hash(x, y, 5);
                if ((h & 1023) < 14) c = Color.Lerp(c, light * 1.2f, 0.7f);
                c *= 0.85f + 0.3f * fine;
                px[y * size + x] = new Color32((byte)(Mathf.Clamp01(c.r) * 255f), (byte)(Mathf.Clamp01(c.g) * 255f), (byte)(Mathf.Clamp01(c.b) * 255f), 255);
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    // ---------------- noise helpers ---------------- //

    static Vector2[] gradients;

    static uint Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }
    }

    static int Wrap(int v, int period) { int m = v % period; return m < 0 ? m + period : m; }

    // Perlin noise that repeats every 'period' units. Returns about -1..1.
    static float Perlin(float x, float y, int period, int seed)
    {
        if (gradients == null)
        {
            gradients = new Vector2[256];
            for (int i = 0; i < 256; i++)
            {
                float a = i / 256f * Mathf.PI * 2f;
                gradients[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
        }
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        int X0 = Wrap(x0, period), X1 = Wrap(x0 + 1, period);
        int Y0 = Wrap(y0, period), Y1 = Wrap(y0 + 1, period);
        Vector2 g00 = gradients[Hash(X0, Y0, seed) & 255];
        Vector2 g10 = gradients[Hash(X1, Y0, seed) & 255];
        Vector2 g01 = gradients[Hash(X0, Y1, seed) & 255];
        Vector2 g11 = gradients[Hash(X1, Y1, seed) & 255];
        float n00 = g00.x * fx + g00.y * fy;
        float n10 = g10.x * (fx - 1f) + g10.y * fy;
        float n01 = g01.x * fx + g01.y * (fy - 1f);
        float n11 = g11.x * (fx - 1f) + g11.y * (fy - 1f);
        float u = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
        float v = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
        return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) * 1.414f;
    }

    // several layers of Perlin noise; u, v in 0..1 (tiles), result 0..1
    static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float gain = 0.5f)
    {
        float sum = 0f, amp = 0.5f, norm = 0f;
        int period = basePeriod;
        for (int o = 0; o < octaves; o++)
        {
            sum += amp * Perlin(u * period, v * period, period, seed + o * 31);
            norm += amp;
            amp *= gain;
            period *= 2;
        }
        return Mathf.Clamp01(sum / norm * 0.5f + 0.5f);
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Texture2D NewTexture(string name, int w, int h, bool repeat, bool mips = true)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, mips, false);
        t.name = name;
        t.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        t.filterMode = FilterMode.Bilinear;
        t.anisoLevel = repeat ? 2 : 0;
        t.hideFlags = HideFlags.DontSave;
        return t;
    }

    // ---------------- the textures ---------------- //

    static Texture2D MakeNoise()
    {
        const int size = 256;
        var t = NewTexture("SB_Noise", size, size, true);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            float v = (y + 0.5f) / size;
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size;
                float r = Fbm(u, v, 4, 5, 11);
                float g = Fbm(u, v, 8, 4, 57);
                float b = Fbm(u, v, 3, 4, 91);
                float a = Fbm(u, v, 32, 2, 133);
                // a bit more contrast so clouds get real edges
                r = Smooth(0.18f, 0.82f, r);
                g = Smooth(0.15f, 0.85f, g);
                b = Smooth(0.2f, 0.8f, b);
                px[y * size + x] = new Color32((byte)(r * 255f), (byte)(g * 255f), (byte)(b * 255f), (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    static Color HeatColor(float heat)
    {
        // dark red -> orange -> yellow -> almost white
        Color c0 = new Color(0.55f, 0.06f, 0.0f);
        Color c1 = new Color(1.0f, 0.36f, 0.04f);
        Color c2 = new Color(1.0f, 0.72f, 0.22f);
        Color c3 = new Color(1.0f, 0.95f, 0.75f);
        if (heat < 0.35f) return Color.Lerp(c0, c1, heat / 0.35f);
        if (heat < 0.7f) return Color.Lerp(c1, c2, (heat - 0.35f) / 0.35f);
        return Color.Lerp(c2, c3, (heat - 0.7f) / 0.3f);
    }

    static Texture2D MakeFlames()
    {
        const int fw = 64, fh = 128;
        int w = fw * FlameColumns, h = fh * FlameRows;
        var t = NewTexture("SB_Flames", w, h, false);
        var px = new Color32[w * h];
        for (int frame = 0; frame < FlameColumns * FlameRows; frame++)
        {
            int col = frame % FlameColumns;
            int row = frame / FlameColumns;
            int seed = 200 + frame * 17;
            float lean = (Hash(frame, 3, 7) & 1023) / 1023f - 0.5f;        // each flame bends its own way
            float width = 0.62f + ((Hash(frame, 5, 9) & 1023) / 1023f) * 0.28f;
            float tall = 0.82f + ((Hash(frame, 8, 2) & 1023) / 1023f) * 0.18f;
            for (int y = 0; y < fh; y++)
            {
                float v = (y + 0.5f) / fh;       // 0 bottom, 1 top
                float vv = v / tall;
                for (int x = 0; x < fw; x++)
                {
                    float u = (x + 0.5f) / fw * 2f - 1f; // -1 .. 1
                    // the flame wobbles more towards its tip
                    float wob = (Fbm(0.13f + frame * 0.21f, vv * 0.8f, 2, 3, seed) - 0.5f) * 1.1f * vv + lean * vv * vv * 0.6f;
                    float xx = u - wob;
                    // round bottom, pointed tip
                    float profile = Mathf.Pow(Mathf.Clamp01(1f - vv), 0.75f) * Smooth(-0.08f, 0.22f, vv) * width;
                    float d = Mathf.Abs(xx) / Mathf.Max(profile, 0.001f);
                    float shape = Mathf.Clamp01(1f - d);
                    // tongues: noise eats into the shape
                    float n = Fbm((xx * 0.5f + 0.5f) * 0.5f + frame * 0.37f, vv * 0.9f, 3, 4, seed + 5);
                    float heat = shape * (0.55f + 0.75f * n) * (1f - Smooth(0.55f, 1.0f, vv) * 0.6f);
                    heat = Mathf.Clamp01(heat * 1.25f);
                    float a = Smooth(0.0f, 0.6f, heat); // soft edges
                    Color c = HeatColor(Mathf.Clamp01(heat * 1.1f));
                    int i = (row * fh + y) * w + col * fw + x;
                    px[i] = new Color32((byte)(c.r * 255f), (byte)(c.g * 255f), (byte)(c.b * 255f), (byte)(a * 255f));
                }
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    static Texture2D MakeDot(int size, float sharpness)
    {
        var t = NewTexture("SB_Dot" + size, size, size, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Exp(-r * r * sharpness * 2.5f) * Mathf.Clamp01(1f - r);
                float core = Mathf.Exp(-r * r * 18f);
                byte c = (byte)(Mathf.Clamp01(0.85f + core * 0.15f) * 255f);
                px[y * size + x] = new Color32(c, c, c, (byte)(Mathf.Clamp01(a + core * 0.3f) * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    static Texture2D MakeSmoke()
    {
        const int size = 128;
        var t = NewTexture("SB_Smoke", size, size, false);
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float n = Fbm(u, v, 3, 4, 401);
                float edge = Mathf.Clamp01(1f - r * (0.85f + n * 0.5f));
                float a = Smooth(0f, 0.6f, edge) * (0.55f + 0.45f * n);
                byte c = (byte)((0.8f + 0.2f * n) * 255f);
                px[y * size + x] = new Color32(c, c, c, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    static Texture2D MakeStreak()
    {
        const int w = 16, h = 128;
        var t = NewTexture("SB_RainStreak", w, h, false);
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = (y + 0.5f) / h;
            float along = Smooth(0f, 0.25f, v) * (1f - Smooth(0.7f, 1f, v));
            for (int x = 0; x < w; x++)
            {
                float dx = (x + 0.5f) / w * 2f - 1f;
                float a = Mathf.Exp(-dx * dx * 9f) * along;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }

    static Texture2D MakeSplash()
    {
        const int size = 64;
        var t = NewTexture("SB_Splash", size, size, false);
        var px = new Color32[size * size];
        // a few drops thrown up and out in a crown
        var drops = new Vector3[9];
        for (int i = 0; i < drops.Length; i++)
        {
            float a = (i + 0.5f) / drops.Length * Mathf.PI - Mathf.PI; // lower half circle turned up
            float spread = 0.45f + ((Hash(i, 1, 3) & 255) / 255f) * 0.35f;
            drops[i] = new Vector3(Mathf.Cos(a) * spread, -0.55f - Mathf.Sin(a) * spread * 0.9f, 0.05f + ((Hash(i, 2, 5) & 255) / 255f) * 0.05f);
        }
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float a = 0f;
                // flat ring at the bottom
                float ry = (dy + 0.75f) * 3.2f;
                float ring = Mathf.Sqrt(dx * dx + ry * ry);
                a = Mathf.Max(a, Mathf.Exp(-Mathf.Pow((ring - 0.55f) * 7f, 2f)) * 0.7f * (dy < -0.4f ? 1f : 0f));
                foreach (var d in drops)
                {
                    float ex = dx - d.x, ey = dy - d.y;
                    a = Mathf.Max(a, Mathf.Exp(-(ex * ex + ey * ey) / (d.z * d.z)));
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255f));
            }
        }
        t.SetPixels32(px);
        t.Apply(true, false);
        return t;
    }
}
