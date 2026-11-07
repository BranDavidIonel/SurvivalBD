using UnityEngine;

// The picture of the backpack on its crafting card (Crafting > Survival), drawn in code: a brown leather bag with a
// flap, a gold buckle, a front pocket, side straps and a handle. The backpack is not an inventory item (made, it
// gives the inventory more slots), so it has no item prefab with an icon.
public static class BackpackIcon
{
    static Sprite sprite;

    public static Sprite Sprite
    {
        get
        {
            if (sprite == null) sprite = Make();
            return sprite;
        }
    }

    const int Size = 128;

    static Sprite Make()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) // (sRGB: shows the colours as written)
        {
            name = "Backpack icon (made in code)",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color[Size * Size];
        Color leather = new Color(0.58f, 0.35f, 0.17f), dark = new Color(0.36f, 0.2f, 0.09f);
        Color flap = new Color(0.48f, 0.28f, 0.13f), pocket = new Color(0.66f, 0.42f, 0.21f);
        Color strap = new Color(0.3f, 0.17f, 0.08f), gold = new Color(0.9f, 0.74f, 0.32f), line = new Color(0.16f, 0.09f, 0.04f);
        Color stitch = new Color(0.86f, 0.74f, 0.52f);

        for (int y = 0; y < Size; y++)
        for (int x = 0; x < Size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Color c = new Color(0f, 0f, 0f, 0f);

            // the handle on top: a ring
            float ring = Mathf.Abs(Vector2.Distance(p, new Vector2(64f, 100f)) - 11f) - 3.2f;
            c = Over(c, strap, ring, line);

            // side straps
            c = Over(c, strap, Box(p, new Vector2(29f, 54f), new Vector2(4f, 34f), 2f), line);
            c = Over(c, strap, Box(p, new Vector2(99f, 54f), new Vector2(4f, 34f), 2f), line);

            // the bag: lighter in the middle, darker at the sides (round leather)
            float body = Box(p, new Vector2(64f, 54f), new Vector2(33f, 42f), 15f);
            float shade = 1f - 0.35f * Mathf.Pow(Mathf.Abs(p.x - 64f) / 33f, 2f) - 0.15f * Mathf.Clamp01((40f - p.y) / 30f);
            c = Over(c, Color.Lerp(dark, leather, shade), body, line);

            // the front pocket with its stitches
            float pk = Box(p, new Vector2(64f, 34f), new Vector2(21f, 16f), 7f);
            c = Over(c, pocket, pk, line);
            if (pk < -2.5f && pk > -4.2f && ((x + y) / 4) % 2 == 0) c = Color.Lerp(c, stitch, 0.85f);

            // the flap over the top, round at the bottom
            float fl = Box(p, new Vector2(64f, 80f), new Vector2(31f, 17f), 12f);
            c = Over(c, flap, fl, line);
            if (fl < -2.5f && fl > -4.2f && ((x + y) / 4) % 2 == 0) c = Color.Lerp(c, stitch, 0.7f);

            // the strap and the gold buckle that close it
            c = Over(c, strap, Box(p, new Vector2(64f, 63f), new Vector2(4.5f, 12f), 1.5f), line);
            float buckle = Mathf.Abs(Box(p, new Vector2(64f, 59f), new Vector2(7f, 6f), 1.5f)) - 1.6f;
            c = Over(c, gold, buckle, new Color(0.45f, 0.33f, 0.1f));

            pixels[y * Size + x] = c;
        }
        tex.SetPixels(pixels);
        tex.Apply(false, true);
        return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
    }

    // a rounded box: < 0 inside (distance to its edge)
    static float Box(Vector2 p, Vector2 centre, Vector2 half, float radius)
    {
        Vector2 d = new Vector2(Mathf.Abs(p.x - centre.x), Mathf.Abs(p.y - centre.y)) - half + Vector2.one * radius;
        return Vector2.Max(d, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0f) - radius;
    }

    // paints a shape over what is there: its colour inside, a dark outline 2 px wide, soft edges
    static Color Over(Color under, Color fill, float distance, Color outline)
    {
        if (distance > 1f) return under;
        Color shape = distance > -2f ? outline : fill;
        float a = Mathf.Clamp01(0.5f - distance);
        Color result = Color.Lerp(under, shape, a);
        result.a = Mathf.Max(under.a, a);
        return result;
    }
}
