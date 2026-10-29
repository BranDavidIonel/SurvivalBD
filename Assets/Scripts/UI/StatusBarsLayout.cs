using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The health, food and water bars at the top of the screen were very small (hard to read, mostly on the phone).
// Made bigger here, in code (the scene is not edited): one under the other, each with its name and value in big
// letters ("Health 100 / 100", "Food 1800 / 2500", "Water 87%"); the day line and the clock move under them.
// They sit at the very top edge, in the middle, between the buttons (Inventory, Crafting, Menu at the top right; on
// the PC Help and Map at the top left); on a narrow screen where they would touch those buttons, just under them.
// Laid out again when the screen changes size (a PC window, the editor's phone simulator).
// Called by GameWindowsLayout when the game scene starts.
public static class StatusBarsLayout
{
    static bool phoneLayout;
    // the bottom of the three bars, in the game canvas' units from its middle (NaN: not laid out yet): the windows stay under it
    public static float BottomInCanvas = float.NaN;
    static readonly List<(RectTransform bar, Text counter)> bars = new List<(RectTransform, Text)>();

    public static void Apply(bool phone)
    {
        phoneLayout = phone;
        bars.Clear();
        var health = Object.FindObjectOfType<HealthBar>();
        var food = Object.FindObjectOfType<CaloriesBar>();
        var water = Object.FindObjectOfType<HydrationBar>();
        if (health != null) bars.Add(((RectTransform)health.transform, health.healthCounter));
        if (food != null) bars.Add(((RectTransform)food.transform, food.caloriesCounter));
        if (water != null) bars.Add(((RectTransform)water.transform, water.hydrationCounter));
        if (bars.Count == 0) return;
        var canvas = bars[0].bar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        float s = canvas.transform.lossyScale.y; // world units of one canvas unit (1920 x 1080 reference)

        float width = phone ? 500f : 430f, height = phone ? 38f : 30f;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i].bar;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            Vector3 scale = bar.lossyScale;
            bar.sizeDelta = new Vector2(width * s / Mathf.Max(0.0001f, scale.x), height * s / Mathf.Max(0.0001f, scale.y));
            // the colour fills the whole bar (it was a thin strip in the middle), on a dark see-through background
            var slider = bar.GetComponent<Slider>();
            if (slider != null && slider.fillRect != null)
            {
                var fill = slider.fillRect;
                var area = fill.parent as RectTransform;
                if (area != null && area != bar)
                {
                    area.anchorMin = Vector2.zero;
                    area.anchorMax = Vector2.one;
                    area.offsetMin = new Vector2(3f, 3f);
                    area.offsetMax = new Vector2(-3f, -3f);
                }
                fill.anchorMin = new Vector2(fill.anchorMin.x, 0f);
                fill.anchorMax = new Vector2(fill.anchorMax.x, 1f);
                fill.sizeDelta = Vector2.zero;
                fill.anchoredPosition = Vector2.zero;
            }
            var background = bar.Find("Background");
            var backgroundImage = background != null ? background.GetComponent<Image>() : null;
            if (backgroundImage != null)
            {
                var br = (RectTransform)background;
                br.anchorMin = Vector2.zero;
                br.anchorMax = Vector2.one;
                br.offsetMin = br.offsetMax = Vector2.zero;
                backgroundImage.color = new Color(0.05f, 0.04f, 0.03f, 0.62f);
            }

            // the value written on the bar, big and easy to read
            var text = bars[i].counter;
            if (text == null) continue;
            var tr = text.rectTransform;
            if (tr.IsChildOf(bar))
            {
                tr.anchorMin = Vector2.zero;
                tr.anchorMax = Vector2.one;
                tr.offsetMin = tr.offsetMax = Vector2.zero;
            }
            else
            {
                tr.anchorMin = tr.anchorMax = tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = new Vector2(width * s / Mathf.Max(0.0001f, tr.lossyScale.x), height * s / Mathf.Max(0.0001f, tr.lossyScale.y));
                tr.position = bar.position;
                tr.SetAsLastSibling();
            }
            text.fontSize = Mathf.RoundToInt(height * 0.62f * s / Mathf.Max(0.0001f, tr.lossyScale.y));
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            if (text.GetComponent<UnityEngine.UI.Outline>() == null)
            {
                var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
                outline.effectDistance = new Vector2(1.5f, -1.5f);
            }
        }

        Place();
        var canvasObject = canvas.gameObject;
        // (lower now, they would cover the titles of the windows: hidden while a window is open)
        if (canvasObject.GetComponent<ClockHider>() == null) canvasObject.AddComponent<ClockHider>();
        // laid out again when the screen size changes
        if (canvasObject.GetComponent<StatusBarsWatcher>() == null) canvasObject.AddComponent<StatusBarsWatcher>();
    }

    // where the bars, the day line and the clock go for the screen as it is now
    public static void Place()
    {
        if (bars.Count == 0 || bars[0].bar == null) return;
        var canvas = bars[0].bar.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;
        var canvasRect = (RectTransform)canvas.transform;
        float s = canvas.transform.lossyScale.y;
        if (s <= 0f) return;
        float width = phoneLayout ? 500f : 430f, height = phoneLayout ? 38f : 30f, gap = phoneLayout ? 7f : 6f;

        // the top edge of the screen, in the middle; the buttons: the last ~700 units on the right (Inventory,
        // Crafting, Menu), on the PC also the first ~470 on the left (Help, Map)
        float canvasWidth = canvasRect.rect.width;
        float centreX = canvasRect.position.x;
        float screenTop = canvasRect.rect.yMax * s + canvasRect.position.y;
        float left = canvasWidth * 0.5f - width * 0.5f, right = canvasWidth * 0.5f + width * 0.5f;
        bool roomAtTop = right <= canvasWidth - 710f && left >= (phoneLayout ? 10f : 480f);
        float top = screenTop - (roomAtTop ? 14f : 122f) * s;

        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i].bar;
            if (bar == null) continue;
            bar.position = new Vector3(centreX, top - (height * 0.5f + i * (height + gap)) * s, bar.position.z);
            var text = bars[i].counter;
            if (text != null && !text.rectTransform.IsChildOf(bar)) text.rectTransform.position = bar.position;
        }

        // the day line and the clock: just under the bars (the ones in the same column)
        float bottom = top - (bars.Count * (height + gap)) * s;
        BottomInCanvas = (bottom - canvasRect.position.y) / s;
        var below = new List<RectTransform>();
        if (TimeManager.Instance != null && TimeManager.Instance.dayUI != null) below.Add(TimeManager.Instance.dayUI.rectTransform);
        if (DayNightSystem.Instance != null && DayNightSystem.Instance.timeUI != null) below.Add(DayNightSystem.Instance.timeUI.rectTransform);
        var corners = new Vector3[4];
        float textTop = float.MinValue;
        var column = new List<RectTransform>();
        foreach (var r in below)
        {
            if (r == null || column.Contains(r)) continue;
            r.GetWorldCorners(corners);
            float mid = (corners[0].x + corners[2].x) * 0.5f;
            if (Mathf.Abs(mid - centreX) > 450f * s) continue;
            column.Add(r);
            textTop = Mathf.Max(textTop, corners[1].y);
        }
        if (column.Count == 0) return;
        float shift = (bottom - 2f * s) - textTop;
        if (Mathf.Abs(shift) < 0.5f * s) return;
        foreach (var r in column)
        {
            r.position += Vector3.up * shift;
            // in the middle under the bars
            r.GetWorldCorners(corners);
            r.position += Vector3.right * (centreX - (corners[0].x + corners[2].x) * 0.5f);
        }
    }
}

// Lays the bars out again when the screen changes size (StatusBarsLayout.Place).
public class StatusBarsWatcher : MonoBehaviour
{
    int width, height;
    float canvasWidth, timer;

    void LateUpdate()
    {
        timer -= Time.unscaledDeltaTime;
        if (timer > 0f) return;
        timer = 0.5f;
        // (the canvas can change width without the screen: NarrowScreenFit on a narrow screen)
        var canvas = GetComponent<Canvas>();
        float w = canvas != null ? ((RectTransform)canvas.rootCanvas.transform).rect.width : 0f;
        if (Screen.width == width && Screen.height == height && Mathf.Abs(w - canvasWidth) < 1f) return;
        width = Screen.width;
        height = Screen.height;
        canvasWidth = w;
        StatusBarsLayout.Place();
    }
}

// Hides the day line and the clock while a window is open (inventory, crafting, storage box, campfire), and under
// the "LEVEL N" banner and the shadow / deep water countdowns (they are written in the same place).
public class ClockHider : MonoBehaviour
{
    void LateUpdate()
    {
        bool window = (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen)
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || (StoryManager.Instance != null && StoryManager.Instance.BannerShowing)   // "LEVEL N" in the same place
            || (NightShadow.Instance != null && NightShadow.Instance.IsHunting)        // the shadow's countdown
            || OceanSafety.Warning;                                                      // the deep water countdown
        var day = TimeManager.Instance != null ? TimeManager.Instance.dayUI : null;
        var clock = DayNightSystem.Instance != null ? DayNightSystem.Instance.timeUI : null;
        if (day != null && day.enabled == window) day.enabled = !window;
        if (clock != null && clock.enabled == window) clock.enabled = !window;
    }
}
