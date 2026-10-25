using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// ===================================================================================================
//  TEST ONLY - remove before the release.
//  "Test settings" in the in-game Settings screen (menu M > Settings), for trying out the time of day,
//  the seasons, the weather and the snow without waiting:
//   - Clock: time passes by itself or stops
//   - Time speed: type any number (1 = normal, 10 = ten times faster) or use the quick buttons
//   - Move speed: how fast the player walks and swims (x times the normal speed), typed or quick buttons
//   - Time of day: slider, plus quick buttons for sunrise, noon (day), sunset and night
//   - Season, Weather (Auto = changes by itself), Snow on the ground
//  It is made in code when the game scene loads (EnvironmentBootstrap), so the scene is not changed.
//  To remove it: delete this file and the DebugEnvironmentPanel.Attach() line in EnvironmentBootstrap.
// ===================================================================================================
public class DebugEnvironmentPanel : MonoBehaviour
{
    // layout, in the menu's units (it is laid out for a 1080 px high screen)
    const float PanelWidth = 1560f;
    const float PanelHeight = 600f;
    const float PanelY = 18f;
    const float RowHeight = 70f;
    const float LabelWidth = 330f;
    const float LabelSize = 32f;
    const float ButtonSize = 30f;

    static readonly Color PanelColor = new Color(0.05f, 0.045f, 0.04f, 0.82f);
    static readonly Color ButtonColor = new Color(0.16f, 0.13f, 0.1f, 0.95f);
    static readonly Color SelectedColor = new Color(0.86f, 0.6f, 0.2f, 1f);
    static readonly Color LabelColor = new Color(0.98f, 0.84f, 0.52f);
    static readonly Color InfoColor = new Color(0.75f, 0.85f, 1f);

    TMP_FontAsset font;
    Sprite sprite;
    ScrollRect scroll;

    readonly List<KeyValuePair<Button, bool>> clockButtons = new List<KeyValuePair<Button, bool>>();
    readonly List<KeyValuePair<Button, TimeManager.Season>> seasonButtons = new List<KeyValuePair<Button, TimeManager.Season>>();
    readonly List<KeyValuePair<Button, int>> weatherButtons = new List<KeyValuePair<Button, int>>(); // -1 = auto
    TMP_InputField speedInput, moveInput;
    PlayerMovement player;
    float normalMoveSpeed = -1f;   // the player's own speed, before any test change
    TextMeshProUGUI speedInfo, moveInfo, timeValue, snowValue, info;
    Slider timeSlider, snowSlider;

    // Puts the panel into the in-game Settings screen (called by EnvironmentBootstrap).
    public static void Attach()
    {
        var menus = FindObjectOfType<MenuManager>(true);
        if (menus == null || menus.settingsMenu == null) return;
        if (menus.settingsMenu.GetComponent<DebugEnvironmentPanel>() != null) return;
        menus.settingsMenu.AddComponent<DebugEnvironmentPanel>().Build();
    }

    void Build()
    {
        // the look of the menu: its font and the Back button's picture
        var title = GetComponentInChildren<TextMeshProUGUI>(true);
        font = title != null ? title.font : TMP_Settings.defaultFontAsset;
        var backImage = GetComponentInChildren<Button>(true)?.GetComponent<Image>();
        sprite = backImage != null ? backImage.sprite : null;

        // built inactive, switched on at the end (the input field must be complete before it wakes up)
        var panel = NewRect("TestSettings (TEST ONLY)", (RectTransform)transform);
        panel.gameObject.SetActive(false);
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.sizeDelta = new Vector2(PanelWidth, PanelHeight);
        panel.anchoredPosition = new Vector2(0f, PanelY);
        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = sprite;
        background.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        background.color = PanelColor;

        var heading = NewText("Heading", panel, "TEST SETTINGS  (for testing, will be removed)", 26f, InfoColor, TextAlignmentOptions.Center);
        var headingRect = (RectTransform)heading.transform;
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(0.5f, 1f);
        headingRect.anchoredPosition = new Vector2(0f, -8f);
        headingRect.sizeDelta = new Vector2(0f, 36f);

        // --- a scrolling list of rows (it fits on a PC screen, a short phone screen can scroll) --- //
        var view = NewRect("Scroll", panel);
        view.anchorMin = Vector2.zero;
        view.anchorMax = Vector2.one;
        view.offsetMin = new Vector2(24f, 16f);
        view.offsetMax = new Vector2(-24f, -50f);
        view.gameObject.AddComponent<RectMask2D>();
        var catcher = view.gameObject.AddComponent<Image>();
        catcher.color = new Color(0f, 0f, 0f, 0f);
        var content = NewRect("Rows", view);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 60f;

        // 1. clock on / off
        var row = NewRow(content, "Clock");
        clockButtons.Add(new KeyValuePair<Button, bool>(NewButton(row, "Time passes", 230f, () => SetAutoTime(true)), true));
        clockButtons.Add(new KeyValuePair<Button, bool>(NewButton(row, "Stopped", 180f, () => SetAutoTime(false)), false));

        // 2. speed of the clock: any number, or a quick button
        row = NewRow(content, "Time speed (x)");
        speedInput = NewInput(row, 170f, OnSpeedTyped);
        NewButton(row, "x1", 90f, () => SetSpeed(1f));
        NewButton(row, "x10", 100f, () => SetSpeed(10f));
        NewButton(row, "x60", 100f, () => SetSpeed(60f));
        NewButton(row, "x300", 120f, () => SetSpeed(300f));
        speedInfo = NewLabel(row, "", 380f, 26f, InfoColor);

        // 3. how fast the player moves: any number, or a quick button
        row = NewRow(content, "Move speed (x)");
        moveInput = NewInput(row, 170f, OnMoveTyped);
        NewButton(row, "x1", 90f, () => SetMoveSpeed(1f));
        NewButton(row, "x2", 90f, () => SetMoveSpeed(2f));
        NewButton(row, "x4", 90f, () => SetMoveSpeed(4f));
        NewButton(row, "x8", 90f, () => SetMoveSpeed(8f));
        moveInfo = NewLabel(row, "", 380f, 26f, InfoColor);

        // 4. time of day
        row = NewRow(content, "Time of day");
        timeSlider = NewSlider(row, 0f, 24f, 760f, value =>
        {
            if (DayNightSystem.Instance != null) DayNightSystem.Instance.TimeOfDay = value / 24f;
        });
        timeValue = NewLabel(row, "", 140f, LabelSize, Color.white);

        // 5. quick times: day / night
        row = NewRow(content, "Jump to");
        NewButton(row, "Sunrise 6:00", 230f, () => SetHour(6f));
        NewButton(row, "Day 12:00", 200f, () => SetHour(12f));
        NewButton(row, "Sunset 18:30", 230f, () => SetHour(18.5f));
        NewButton(row, "Night 0:00", 210f, () => SetHour(0f));

        // 6. season
        row = NewRow(content, "Season");
        AddSeason(row, "Spring", TimeManager.Season.Spring);
        AddSeason(row, "Summer", TimeManager.Season.Summer);
        AddSeason(row, "Autumn", TimeManager.Season.Fall);
        AddSeason(row, "Winter", TimeManager.Season.Winter);

        // 7. weather
        row = NewRow(content, "Weather");
        AddWeather(row, "Auto", -1, 120f);
        AddWeather(row, "Clear", (int)WeatherSystem.WeatherType.Clear, 130f);
        AddWeather(row, "Cloudy", (int)WeatherSystem.WeatherType.Cloudy, 140f);
        AddWeather(row, "Rain", (int)WeatherSystem.WeatherType.Rain, 120f);
        AddWeather(row, "Storm", (int)WeatherSystem.WeatherType.Storm, 130f);
        AddWeather(row, "Snow", (int)WeatherSystem.WeatherType.Snow, 120f);

        // 8. snow lying on the ground
        row = NewRow(content, "Snow on ground");
        snowSlider = NewSlider(row, 0f, 1f, 760f, value =>
        {
            if (WeatherSystem.Instance != null) WeatherSystem.Instance.SetSnowCover(value);
        });
        snowValue = NewLabel(row, "", 140f, LabelSize, Color.white);

        // 8b. the story: jump to the next level (to test a level without playing the ones before)
        row = NewRow(content, "Story");
        NewButton(row, "Next level", 220f, () => { if (StoryManager.Instance != null) StoryManager.Instance.SkipLevel(); });
        NewButton(row, "Give items", 220f, () => { if (StoryManager.Instance != null) StoryManager.Instance.GiveTestItems(); });

        // 8c. any item of the game straight into the inventory: pick it in the list, then Give
        row = NewRow(content, "Give item");
        var itemList = NewDropdown(row, 470f);
        NewButton(row, "Give 1", 170f, () => GiveItem(itemList.value, 1));
        NewButton(row, "Give 5", 170f, () => GiveItem(itemList.value, 5));

        // 9. what the weather is doing now
        row = NewRow(content, "Now");
        info = NewLabel(row, "", 1100f, 26f, InfoColor);

        panel.gameObject.SetActive(true);
        Refresh();
    }

    void OnEnable()
    {
        if (scroll != null) StartCoroutine(OpenAtTop());
        Refresh();
    }

    // the in-game menu canvas is stored at scale 0: the list can only measure itself once it is shown
    IEnumerator OpenAtTop()
    {
        scroll.enabled = false;
        var root = scroll.GetComponentInParent<Canvas>()?.rootCanvas.transform;
        for (int frame = 0; frame < 10; frame++)
        {
            yield return null;
            if (root == null || Mathf.Abs(root.lossyScale.y) > 0.0001f) break;
        }
        scroll.content.anchoredPosition = new Vector2(scroll.content.anchoredPosition.x, 0f);
        scroll.enabled = true;
        scroll.StopMovement();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        var clock = DayNightSystem.Instance;
        var weather = WeatherSystem.Instance;
        var time = TimeManager.Instance;
        if (clock == null || timeSlider == null) return;

        foreach (var b in clockButtons) Highlight(b.Key, b.Value == clock.autoTime);
        if (!speedInput.isFocused) speedInput.SetTextWithoutNotify(clock.timeSpeed.ToString("0.##", CultureInfo.InvariantCulture));
        if (FindPlayer())
        {
            float times = player.speed / Mathf.Max(0.01f, normalMoveSpeed);
            if (!moveInput.isFocused) moveInput.SetTextWithoutNotify(times.ToString("0.##", CultureInfo.InvariantCulture));
            moveInfo.text = "= " + player.speed.ToString("0.#") + " m/s (normal " + normalMoveSpeed.ToString("0.#") + ")";
        }
        float dayMinutes = clock.dayDurationInSeconds / Mathf.Max(0.0001f, clock.timeSpeed) / 60f;
        speedInfo.text = clock.autoTime ? "1 day = " + (dayMinutes >= 1f ? dayMinutes.ToString("0.#") + " min" : (dayMinutes * 60f).ToString("0.#") + " s") : "(clock stopped)";

        timeSlider.SetValueWithoutNotify(clock.Hour);
        int h = Mathf.FloorToInt(clock.Hour);
        int m = Mathf.FloorToInt((clock.Hour - h) * 60f);
        timeValue.text = h + ":" + m.ToString("00");

        if (time != null) foreach (var b in seasonButtons) Highlight(b.Key, b.Value == time.currentSeason);

        if (weather != null)
        {
            foreach (var b in weatherButtons)
            {
                bool on = b.Value < 0 ? weather.autoWeather : (!weather.autoWeather && (int)weather.testWeather == b.Value);
                Highlight(b.Key, on);
            }
            snowSlider.SetValueWithoutNotify(weather.SnowCover);
            snowValue.text = Mathf.RoundToInt(weather.SnowCover * 100f) + "%";
            string next = weather.autoWeather ? ", changes in " + weather.HoursLeft.ToString("0.0") + " h" : " (kept for the test)";
            info.text = weather.Current + next + "   clouds " + Pct(weather.CloudCover) + "  rain " + Pct(weather.Rain) +
                        "  snow " + Pct(weather.Snow) + "  wind " + Pct(weather.Wind) + "  wet " + Pct(weather.Wetness);
        }
    }

    static string Pct(float v) => Mathf.RoundToInt(v * 100f) + "%";

    // ---------------- actions ---------------- //

    void SetAutoTime(bool on)
    {
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.autoTime = on;
    }

    void SetSpeed(float speed)
    {
        if (DayNightSystem.Instance == null) return;
        DayNightSystem.Instance.timeSpeed = Mathf.Clamp(speed, 0.01f, 5000f);
        DayNightSystem.Instance.autoTime = true;
    }

    void OnSpeedTyped(string text)
    {
        text = text.Replace(',', '.');
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float speed) && speed > 0f) SetSpeed(speed);
    }

    // TEST: the player moves 'times' times faster than normal (walking and swimming)
    void SetMoveSpeed(float times)
    {
        if (!FindPlayer()) return;
        player.speed = normalMoveSpeed * Mathf.Clamp(times, 0.1f, 50f);
    }

    void OnMoveTyped(string text)
    {
        text = text.Replace(',', '.');
        if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float times) && times > 0f) SetMoveSpeed(times);
    }

    bool FindPlayer()
    {
        if (player == null) player = FindObjectOfType<PlayerMovement>();
        if (player == null) return false;
        if (normalMoveSpeed < 0f) normalMoveSpeed = player.speed;
        return true;
    }

    void SetHour(float hour)
    {
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.TimeOfDay = hour / 24f;
    }

    void AddSeason(RectTransform row, string text, TimeManager.Season season)
    {
        var button = NewButton(row, text, 170f, () =>
        {
            if (TimeManager.Instance != null) TimeManager.Instance.SetSeason(season);
        });
        seasonButtons.Add(new KeyValuePair<Button, TimeManager.Season>(button, season));
    }

    void AddWeather(RectTransform row, string text, int type, float width)
    {
        var button = NewButton(row, text, width, () =>
        {
            if (WeatherSystem.Instance == null) return;
            if (type < 0) WeatherSystem.Instance.SetTestWeather(null);
            else WeatherSystem.Instance.SetTestWeather((WeatherSystem.WeatherType)type);
        });
        weatherButtons.Add(new KeyValuePair<Button, int>(button, type));
    }

    static void Highlight(Button button, bool on)
    {
        var image = button.targetGraphic as Image;
        if (image != null) image.color = on ? SelectedColor : ButtonColor;
    }

    // ---------------- building blocks ---------------- //

    static RectTransform NewRect(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    TextMeshProUGUI NewText(string name, RectTransform parent, string text, float size, Color color, TextAlignmentOptions alignment)
    {
        var rect = NewRect(name, parent);
        var t = rect.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = alignment;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        return t;
    }

    RectTransform NewRow(RectTransform content, string label)
    {
        var row = NewRect("Row " + label, content);
        var element = row.gameObject.AddComponent<LayoutElement>();
        element.minHeight = element.preferredHeight = RowHeight;
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 12f;
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        NewLabel(row, label, LabelWidth, LabelSize, LabelColor);
        return row;
    }

    TextMeshProUGUI NewLabel(RectTransform row, string text, float width, float size, Color color)
    {
        var t = NewText("Label", row, text, size, color, TextAlignmentOptions.MidlineLeft);
        var e = t.gameObject.AddComponent<LayoutElement>();
        e.minWidth = e.preferredWidth = width;
        e.minHeight = e.preferredHeight = RowHeight - 10f;
        return t;
    }

    Image NewBox(RectTransform parent, string name, float width, float height, Color color)
    {
        var rect = NewRect(name, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        image.color = color;
        var e = rect.gameObject.AddComponent<LayoutElement>();
        e.minWidth = e.preferredWidth = width;
        e.minHeight = e.preferredHeight = height;
        return image;
    }

    // every item that can be in the inventory (its prefab in Resources, its name in the game)
    static readonly string[,] TestItems =
    {
        { "stone", "Stone" }, { "stick", "Stick" }, { "log", "Log" }, { "pinelog", "Pine Log" }, { "plank", "Plank" },
        { "hay", "Hay" }, { "hemp", "Hemp" }, { "rope", "Rope" }, { "ironore", "Iron Ore" },
        { "Axe", "Stone Axe" }, { "ironaxe", "Iron Axe" }, { "torch", "Torch" }, { "Campfire", "Campfire" },
        { "StorageBox", "Storage Box" }, { "foundation", "Foundation" }, { "wall", "Wall" },
        { "porcini", "Porcini" }, { "redmushroom", "Red Mushroom" }, { "fieldmushroom", "Field Mushroom" },
        { "berries", "Berries" }, { "chicory", "Chicory" }, { "heather", "Heather" },
        { "Raw Meat", "Raw Meat" }, { "Cooked Meat", "Cooked Meat" }, { "Rabbit Hide", "Rabbit Hide" },
        { "pinecone", "Pine Cone" }, { "acorn", "Acorn" }, { "beechnut", "Beechnut" }, { "birchseed", "Birch Catkin" },
        { "journal", "Old Journal" }, { "prayerbook", "Prayer Book" },
    };

    void GiveItem(int index, int amount)
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null || index < 0 || index >= TestItems.GetLength(0)) return;
        string item = TestItems[index, 0];
        int given = 0;
        for (int i = 0; i < amount; i++)
        {
            if (!inventory.CheckSlotsAvailable(1)) break;
            if (inventory.AddToIventory(item, false, false)) given++;
        }
        inventory.ShowMessage(given > 0 ? "+" + given + " " + TestItems[index, 1] : "Inventory is full");
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
    }

    // a list to pick from (TextMeshPro's own dropdown, in the panel's colours)
    TMP_Dropdown NewDropdown(RectTransform row, float width)
    {
        var resources = new TMP_DefaultControls.Resources { standard = sprite, background = sprite, inputField = sprite, knob = sprite };
        var go = TMP_DefaultControls.CreateDropdown(resources);
        go.name = "Item list";
        var rect = (RectTransform)go.transform;
        rect.SetParent(row, false);
        var e = go.AddComponent<LayoutElement>();
        e.minWidth = e.preferredWidth = width;
        e.minHeight = e.preferredHeight = RowHeight - 10f;
        var dropdown = go.GetComponent<TMP_Dropdown>();
        foreach (var image in go.GetComponentsInChildren<Image>(true))
        {
            if (image.sprite == sprite && sprite != null && sprite.border != Vector4.zero) image.type = Image.Type.Sliced;
            if (image.gameObject == go) image.color = new Color(0.9f, 0.88f, 0.82f, 1f);
        }
        foreach (var text in go.GetComponentsInChildren<TMP_Text>(true))
        {
            if (font != null) text.font = font;
            text.fontSize = ButtonSize;
            text.color = new Color(0.08f, 0.07f, 0.06f);
            text.enableWordWrapping = false;
        }
        // the open list: tall enough for ~8 items, finger-sized rows
        var template = (RectTransform)go.transform.Find("Template");
        if (template != null) template.sizeDelta = new Vector2(template.sizeDelta.x, 470f);
        var item = go.transform.Find("Template/Viewport/Content/Item") as RectTransform;
        if (item != null) item.sizeDelta = new Vector2(item.sizeDelta.x, 56f);
        var content = go.transform.Find("Template/Viewport/Content") as RectTransform;
        if (content != null) content.sizeDelta = new Vector2(content.sizeDelta.x, 56f);
        var scrollRect = template != null ? template.GetComponent<ScrollRect>() : null;
        if (scrollRect != null) scrollRect.scrollSensitivity = 40f;
        dropdown.options.Clear();
        for (int i = 0; i < TestItems.GetLength(0); i++) dropdown.options.Add(new TMP_Dropdown.OptionData(TestItems[i, 1]));
        dropdown.value = 0;
        dropdown.RefreshShownValue();
        return dropdown;
    }

    Button NewButton(RectTransform row, string text, float width, UnityEngine.Events.UnityAction onClick)
    {
        var image = NewBox(row, "Button " + text, width, RowHeight - 10f, ButtonColor);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        var label = NewText("Text", (RectTransform)image.transform, text, ButtonSize, Color.white, TextAlignmentOptions.Center);
        var lr = (RectTransform)label.transform;
        lr.anchorMin = Vector2.zero;
        lr.anchorMax = Vector2.one;
        lr.offsetMin = lr.offsetMax = Vector2.zero;
        return button;
    }

    TMP_InputField NewInput(RectTransform row, float width, UnityEngine.Events.UnityAction<string> onTyped)
    {
        var image = NewBox(row, "Speed input", width, RowHeight - 10f, new Color(0.9f, 0.88f, 0.82f, 1f));
        var rect = (RectTransform)image.transform;
        var area = NewRect("Text Area", rect);
        area.anchorMin = Vector2.zero;
        area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(14f, 4f);
        area.offsetMax = new Vector2(-14f, -4f);
        area.gameObject.AddComponent<RectMask2D>();
        var placeholder = NewText("Placeholder", area, "1", ButtonSize, new Color(0.3f, 0.3f, 0.3f, 0.6f), TextAlignmentOptions.MidlineLeft);
        var text = NewText("Text", area, "", ButtonSize, new Color(0.08f, 0.07f, 0.06f), TextAlignmentOptions.MidlineLeft);
        foreach (var t in new[] { placeholder, text })
        {
            var r = (RectTransform)t.transform;
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }
        var input = image.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = font;
        input.pointSize = ButtonSize;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.characterLimit = 7;
        input.targetGraphic = image;
        input.onEndEdit.AddListener(onTyped);
        return input;
    }

    Slider NewSlider(RectTransform row, float min, float max, float width, UnityEngine.Events.UnityAction<float> onChange)
    {
        var holder = NewRect("Slider", row);
        var e = holder.gameObject.AddComponent<LayoutElement>();
        e.minWidth = e.preferredWidth = width;
        e.minHeight = e.preferredHeight = RowHeight - 10f;

        var track = NewRect("Track", holder);
        track.anchorMin = new Vector2(0f, 0.5f);
        track.anchorMax = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(-30f, 14f);
        var trackImage = track.gameObject.AddComponent<Image>();
        trackImage.color = new Color(0f, 0f, 0f, 0.6f);
        trackImage.raycastTarget = false;

        var fillArea = NewRect("Fill Area", holder);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.sizeDelta = new Vector2(-30f, 14f);
        var fill = NewRect("Fill", fillArea);
        fill.sizeDelta = Vector2.zero;
        var fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.86f, 0.6f, 0.2f, 0.9f);
        fillImage.raycastTarget = false;

        var handleArea = NewRect("Handle Area", holder);
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.offsetMin = new Vector2(15f, 0f);
        handleArea.offsetMax = new Vector2(-15f, 0f);
        var handle = NewRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(44f, 0f);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.sprite = sprite;
        handleImage.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        handleImage.color = new Color(0.98f, 0.84f, 0.52f, 1f);

        // a big invisible area to grab, easy with a finger
        var hit = holder.gameObject.AddComponent<Image>();
        hit.color = new Color(0f, 0f, 0f, 0f);

        var slider = holder.gameObject.AddComponent<Slider>();
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.onValueChanged.AddListener(onChange);
        return slider;
    }
}
