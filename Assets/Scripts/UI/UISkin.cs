using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// The look of the game windows (inventory, crafting, quick bar...) and of the phone controls.
// Every sprite here is made in code (no image files): rounded boxes for slots and cards, round buttons, pills.
// The window background is the game's own dark stone frame, cut so its corners never stretch (9-slice).
public static class UISkin
{
    // ---------------- colours ---------------- //
    public static readonly Color Gold = new Color(1f, 0.84f, 0.5f);
    public static readonly Color TextColor = new Color(0.94f, 0.91f, 0.85f);
    public static readonly Color TextDim = new Color(0.7f, 0.66f, 0.6f);
    public static readonly Color Good = new Color(0.58f, 0.93f, 0.47f);
    public static readonly Color Bad = new Color(1f, 0.52f, 0.44f);
    public static readonly Color Ink = new Color(0.13f, 0.11f, 0.09f);
    public static readonly Color CraftGreen = new Color(0.33f, 0.62f, 0.27f);
    public static readonly Color ButtonBrown = new Color(0.42f, 0.33f, 0.24f);
    public static readonly Color SelectedGold = new Color(1f, 0.78f, 0.25f);

    static Font font;
    public static Font Font => font != null ? font : (font = Resources.GetBuiltinResource<Font>("Arial.ttf"));

    // ---------------- sprites ---------------- //
    static Sprite disc, ring, pill, slot, slotSelected, card, panel, white;

    public static Sprite Disc { get { if (disc == null) MakeRoundSprites(); return disc; } }
    public static Sprite Ring { get { if (ring == null) MakeRoundSprites(); return ring; } }

    // white rounded rectangle, 9-sliced (tint it with Image.color)
    public static Sprite Pill => pill != null ? pill : (pill = RoundedBox("Pill", 96, 40f, 0f, Color.white, Color.white));

    // an empty slot: dark see-through inside, light frame
    public static Sprite Slot => slot != null ? slot : (slot = RoundedBox("Slot", 64, 12f, 3f,
        new Color(0f, 0f, 0f, 0.42f), new Color(0.66f, 0.58f, 0.49f, 0.95f)));

    // the slot of the item held in the hand: gold frame
    public static Sprite SlotSelected => slotSelected != null ? slotSelected : (slotSelected = RoundedBox("SlotSelected", 64, 12f, 5f,
        new Color(0.32f, 0.24f, 0.08f, 0.6f), new Color(1f, 0.8f, 0.3f, 1f)));

    // a box inside a window (crafting cards and categories, item info)
    public static Sprite Card => card != null ? card : (card = RoundedBox("Card", 64, 14f, 2f,
        new Color(0.17f, 0.15f, 0.13f, 0.92f), new Color(0.56f, 0.47f, 0.36f, 0.9f)));

    // a window without the stone texture (only used if the game's frame picture is missing)
    public static Sprite PlainPanel => panel != null ? panel : (panel = RoundedBox("Panel", 96, 22f, 4f,
        new Color(0.1f, 0.09f, 0.08f, 0.95f), new Color(0.55f, 0.48f, 0.42f, 1f)));

    public static Sprite White
    {
        get
        {
            if (white == null)
            {
                // (an own texture, sure to be fully opaque, so dark screens behind windows really darken)
                var t = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "UISkin white" };
                var pixels = new Color32[16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
                t.SetPixels32(pixels);
                t.Apply(false, true);
                white = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
            }
            return white;
        }
    }

    // The stone window frame of the game (GUI_Parts barmid_ready), cut into 9 pieces so the ornate corners keep
    // their shape at any window size.
    static Sprite stone;
    static Texture stoneSource;
    public const float StoneCornerUnits = 46f; // size of a corner on screen (1080p units)

    public static Sprite StonePanel(Sprite source)
    {
        if (source == null || source.texture == null) return null;
        if (stone != null && stoneSource == source.texture) return stone;
        var tex = source.texture;
        float border = Mathf.Round(tex.width * 0.12f);
        stone = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        stone.name = "StonePanel (9-slice)";
        stoneSource = tex;
        return stone;
    }

    // Puts a window background on an Image: the stone frame when there is one, otherwise a plain dark panel.
    public static void SetWindowBackground(Image image, Sprite stoneSprite, float scale = 1f)
    {
        if (image == null) return;
        image.sprite = stoneSprite != null ? stoneSprite : PlainPanel;
        image.type = Image.Type.Sliced;
        image.fillCenter = true;
        image.color = Color.white;
        image.preserveAspect = false;
        if (stoneSprite != null)
        {
            // corners StoneCornerUnits big on screen, whatever the window's own scale
            image.pixelsPerUnitMultiplier = stoneSprite.border.x / (StoneCornerUnits / Mathf.Max(0.1f, scale));
        }
        else
        {
            image.pixelsPerUnitMultiplier = 1f;
        }
    }

    static void MakeRoundSprites()
    {
        const int size = 256;
        const float radius = size * 0.5f - 1f;
        var discTex = NewTexture(size);
        var ringTex = NewTexture(size);
        var center = new Vector2(size * 0.5f - 0.5f, size * 0.5f - 0.5f);
        var discPixels = new Color32[size * size];
        var ringPixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                float inside = Mathf.Clamp01(radius - d + 0.5f);
                discPixels[y * size + x] = new Color(1f, 1f, 1f, inside);
                float border = Mathf.Clamp01(d - (radius - 12f) + 0.5f) * inside; // 12 px ring
                float fill = 0.18f * inside;
                ringPixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Max(border, fill));
            }
        }
        discTex.SetPixels32(discPixels);
        ringTex.SetPixels32(ringPixels);
        discTex.Apply();
        ringTex.Apply();
        disc = Sprite.Create(discTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        ring = Sprite.Create(ringTex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        disc.name = "Disc";
        ring.name = "Ring";
    }

    // A rounded rectangle with a frame, 9-sliced so it stretches to any size.
    static Sprite RoundedBox(string name, int size, float radius, float border, Color fill, Color frame)
    {
        var tex = NewTexture(size);
        var pixels = new Color32[size * size];
        float half = size * 0.5f;
        float inner = half - radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // signed distance to the rounded edge (negative inside)
                float dx = Mathf.Abs(x + 0.5f - half) - inner;
                float dy = Mathf.Abs(y + 0.5f - half) - inner;
                float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
                float sd = outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - radius;
                float shape = Mathf.Clamp01(0.5f - sd);
                float onFrame = border > 0f ? Mathf.Clamp01(sd + border + 0.5f) : 0f;
                Color c = Color.Lerp(fill, frame, onFrame);
                c.a *= shape;
                pixels[y * size + x] = c;
            }
        }
        tex.SetPixels32(pixels);
        tex.Apply();
        float cut = Mathf.Ceil(radius + border + 2f);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(cut, cut, cut, cut));
        sprite.name = name;
        return sprite;
    }

    static Texture2D NewTexture(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.DontSave;
        return texture;
    }

    // ---------------- helpers ---------------- //

    // a rect with one anchor point, a pivot, a position and a size
    public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition3D = new Vector3(position.x, position.y, 0f);
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        return rect;
    }

    public static RectTransform Centered(RectTransform rect, Vector2 position, Vector2 size)
    {
        return Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
    }

    // stretch over the whole parent, with a margin
    public static RectTransform Fill(RectTransform rect, float margin = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(margin, margin);
        rect.offsetMax = new Vector2(-margin, -margin);
        rect.localScale = Vector3.one;
        return rect;
    }

    public static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static Image NewImage(string name, Transform parent, Sprite sprite, Color color, bool raycast)
    {
        var image = NewRect(name, parent).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
        return image;
    }

    public static Text NewText(string name, Transform parent, string text, int size, Color color, TextAnchor alignment, bool bold = true)
    {
        var label = NewRect(name, parent).gameObject.AddComponent<Text>();
        label.text = text;
        Style(label, size, color, alignment, bold);
        return label;
    }

    public static void Style(Text label, int size, Color color, TextAnchor alignment, bool bold = true)
    {
        if (label == null) return;
        label.font = Font;
        label.fontSize = size;
        label.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
        label.color = color;
        label.alignment = alignment;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.resizeTextForBestFit = false;
        label.supportRichText = true;
        label.raycastTarget = false;
        var shadow = label.GetComponent<Shadow>();
        if (shadow == null) shadow = label.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // a button with a tinted sprite and a label
    public static Button NewButton(string name, Transform parent, Sprite sprite, Color color, string text, int fontSize,
        Color textColor, UnityAction onClick)
    {
        var image = NewImage(name, parent, sprite, color, true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        SetTint(button);
        if (onClick != null) button.onClick.AddListener(onClick);
        if (!string.IsNullOrEmpty(text))
        {
            var label = NewText("Label", image.transform, text, fontSize, textColor, TextAnchor.MiddleCenter);
            Fill(label.rectTransform);
        }
        return button;
    }

    // pressed = darker, disabled = grey and faded
    public static void SetTint(Selectable selectable)
    {
        selectable.transition = Selectable.Transition.ColorTint;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
        colors.fadeDuration = 0.06f;
        selectable.colors = colors;
        // no keyboard/controller navigation: on PC a click would otherwise leave the button "selected"
        var nav = new Navigation { mode = Navigation.Mode.None };
        selectable.navigation = nav;
    }
}
