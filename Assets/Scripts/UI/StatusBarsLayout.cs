using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The health, food and water bars at the top of the screen were very small (hard to read, mostly on the phone).
// Made bigger here, in code (the scene is not edited): one under the other, each with its name and value in big
// letters ("Health 100 / 100", "Food 1800 / 2500", "Water 87%"); the day line and the clock move down under them.
// Called by GameWindowsLayout when the game scene starts.
public static class StatusBarsLayout
{
    public static void Apply(bool phone)
    {
        var bars = new List<(RectTransform bar, Text counter)>();
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

        // where they are now: the top of the group, and its middle
        float top = float.MinValue, left = float.MaxValue, right = float.MinValue;
        var corners = new Vector3[4];
        foreach (var b in bars)
        {
            b.bar.GetWorldCorners(corners);
            top = Mathf.Max(top, corners[1].y);
            left = Mathf.Min(left, corners[0].x);
            right = Mathf.Max(right, corners[2].x);
        }
        // a little below the top edge of the screen
        float screenTop = ((RectTransform)canvas.transform).rect.yMax * s + canvas.transform.position.y;
        top = Mathf.Min(top, screenTop - 10f * s);
        float centerX = (left + right) * 0.5f;

        float width = phone ? 500f : 430f, height = phone ? 38f : 30f, gap = phone ? 7f : 6f;
        for (int i = 0; i < bars.Count; i++)
        {
            var bar = bars[i].bar;
            bar.anchorMin = bar.anchorMax = new Vector2(0.5f, 0.5f);
            bar.pivot = new Vector2(0.5f, 0.5f);
            Vector3 scale = bar.lossyScale;
            bar.sizeDelta = new Vector2(width * s / Mathf.Max(0.0001f, scale.x), height * s / Mathf.Max(0.0001f, scale.y));
            bar.position = new Vector3(centerX, top - (height * 0.5f + i * (height + gap)) * s, bar.position.z);

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

        // the day line and the clock: under the bars (they were just below the small ones)
        float bottom = top - (bars.Count * (height + gap)) * s;
        var below = new List<RectTransform>();
        if (TimeManager.Instance != null && TimeManager.Instance.dayUI != null) below.Add(TimeManager.Instance.dayUI.rectTransform);
        if (DayNightSystem.Instance != null && DayNightSystem.Instance.timeUI != null) below.Add(DayNightSystem.Instance.timeUI.rectTransform);
        float overlap = 0f;
        foreach (var r in below)
        {
            r.GetWorldCorners(corners);
            float textTop = corners[1].y;
            // only text that sits under the bars (in the same column)
            if (corners[2].x < left - 200f * s || corners[0].x > right + 200f * s) continue;
            overlap = Mathf.Max(overlap, textTop - (bottom - 4f * s));
        }
        if (overlap > 0f)
        {
            var moved = new HashSet<RectTransform>();
            foreach (var r in below)
            {
                if (moved.Contains(r)) continue;
                moved.Add(r);
                r.position += Vector3.down * overlap;
            }
        }
        // (lower now, they would cover the titles of the windows: hidden while a window is open)
        if (canvas.GetComponent<ClockHider>() == null) canvas.gameObject.AddComponent<ClockHider>();
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
