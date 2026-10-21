using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The Save Game and Load Game screens: a list of SaveManager.SlotCount save slots that scrolls by
// dragging a finger (phone) or with the mouse wheel / scrollbar (PC). Each slot shows the picture
// taken when it was saved, its name and the date.
// Built at runtime in place of the scene's three slot buttons (same place, same look), so the scenes
// do not have to be edited:
//  - main menu: the Load Game screen (the one with the LoadSlot buttons), set up automatically
//  - in-game menu: the Save Game screen and its Load Game copy, set up by InGameLoadMenu
public class SaveSlotList : MonoBehaviour
{
    public enum Mode { Save, Load }

    // layout, in the menus' units (they are laid out for a 1080 px high screen)
    const float RowHeight = 160f;
    const float RowSpacing = 12f;
    const float RowInset = 16f;       // inside the slot's frame
    const float ScrollbarWidth = 28f;
    const float WheelSpeed = 90f;     // per mouse wheel step
    const float TapSlopMm = 2f;       // a finger may move this much and still count as a tap

    static readonly Color DateColor = new Color(0.98f, 0.80f, 0.40f);
    static readonly Color EmptyColor = new Color(0.72f, 0.70f, 0.66f);

    Mode mode;
    GameObject overrideAlert;
    ScrollRect scroll;
    readonly List<Row> rows = new List<Row>();
    int oldDragThreshold = -1;

    class Row
    {
        public int slot;
        public Button button;
        public RawImage picture;
        public TextMeshProUGUI pictureLabel;
        public TextMeshProUGUI title;
        public TextMeshProUGUI date;
        public Texture2D texture;
        public DateTime textureTime;
    }

    // ---------------- automatic set-up (main menu) ---------------- //

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetUpLoadScreens();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUpLoadScreens();

    static void SetUpLoadScreens()
    {
        var screens = new HashSet<GameObject>();
        foreach (var slot in FindObjectsOfType<LoadSlot>(true))
        {
            if (slot.transform.parent != null) screens.Add(slot.transform.parent.gameObject);
        }
        foreach (var screen in screens) Attach(screen, Mode.Load);
    }

    // Replaces the slot buttons (SaveSlot / LoadSlot) on this screen with the scrolling list.
    public static SaveSlotList Attach(GameObject screen, Mode mode)
    {
        if (screen == null) return null;
        var existing = screen.GetComponent<SaveSlotList>();
        if (existing != null) return existing;

        var oldSlots = new List<RectTransform>();
        foreach (var slot in screen.GetComponentsInChildren<SaveSlot>(true)) oldSlots.Add((RectTransform)slot.transform);
        foreach (var slot in screen.GetComponentsInChildren<LoadSlot>(true)) oldSlots.Add((RectTransform)slot.transform);
        if (oldSlots.Count == 0) return null;

        var list = screen.AddComponent<SaveSlotList>();
        list.mode = mode;
        var alert = screen.transform.Find("OverrideAlert");
        if (alert != null) list.overrideAlert = alert.gameObject;
        list.Build(oldSlots);
        return list;
    }

    // ---------------- building the list ---------------- //

    void Build(List<RectTransform> oldSlots)
    {
        var first = oldSlots[0];
        var parent = (RectTransform)first.parent;

        // the list takes the place of the old buttons: from the top of the first to the bottom of the last
        float top = float.MinValue, bottom = float.MaxValue, left = float.MaxValue, right = float.MinValue;
        foreach (var slot in oldSlots)
        {
            if (slot.parent != parent) continue;
            Vector2 min = slot.anchoredPosition - Vector2.Scale(slot.pivot, slot.sizeDelta);
            Vector2 max = min + slot.sizeDelta;
            top = Mathf.Max(top, max.y);
            bottom = Mathf.Min(bottom, min.y);
            left = Mathf.Min(left, min.x);
            right = Mathf.Max(right, max.x);
        }

        // the old buttons' look
        var templateImage = first.GetComponent<Image>();
        var templateButton = first.GetComponent<Button>();
        var templateText = first.GetComponentInChildren<TextMeshProUGUI>(true);
        Sprite rowSprite = MakeSlicedSprite(templateImage != null ? templateImage.sprite : null);

        // --- scroll view --- //
        var view = NewRect("SaveSlotList", parent);
        view.SetSiblingIndex(first.GetSiblingIndex()); // under the "overwrite?" question, like the old buttons
        view.anchorMin = view.anchorMax = first.anchorMin;
        view.pivot = new Vector2(0.5f, 0.5f);
        view.anchoredPosition = new Vector2((left + right) / 2f, (top + bottom) / 2f);
        view.sizeDelta = new Vector2(right - left, top - bottom);

        var viewport = NewRect("Viewport", view);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();
        var catcher = viewport.gameObject.AddComponent<Image>(); // invisible: lets drags between the slots scroll too
        catcher.color = new Color(0f, 0f, 0f, 0f);

        var content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowSpacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = content;
        scroll.viewport = viewport;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.inertia = true;
        scroll.decelerationRate = 0.135f;
        scroll.scrollSensitivity = WheelSpeed;

        // --- scrollbar, just right of the list --- //
        var bar = NewRect("Scrollbar", view);
        bar.anchorMin = new Vector2(1f, 0f);
        bar.anchorMax = new Vector2(1f, 1f);
        bar.pivot = new Vector2(0f, 0.5f);
        bar.anchoredPosition = new Vector2(14f, 0f);
        bar.sizeDelta = new Vector2(ScrollbarWidth, -8f);
        var track = bar.gameObject.AddComponent<Image>();
        track.color = new Color(0f, 0f, 0f, 0.45f);
        var slidingArea = NewRect("Sliding Area", bar);
        Stretch(slidingArea);
        slidingArea.offsetMin = new Vector2(3f, 3f);
        slidingArea.offsetMax = new Vector2(-3f, -3f);
        var handle = NewRect("Handle", slidingArea);
        Stretch(handle);
        var handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.86f, 0.66f, 0.28f, 0.9f);
        var scrollbar = bar.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        // --- the slots --- //
        for (int slot = 1; slot <= SaveManager.SlotCount; slot++)
        {
            rows.Add(NewRow(slot, content, rowSprite, templateImage, templateButton, templateText));
        }

        // the old buttons are not needed any more (hidden, so they never run)
        foreach (var slot in oldSlots) slot.gameObject.SetActive(false);

        scroll.verticalNormalizedPosition = 1f;
        if (isActiveAndEnabled) Refresh();
    }

    Row NewRow(int slot, RectTransform content, Sprite sprite, Image templateImage, Button templateButton, TextMeshProUGUI templateText)
    {
        var row = new Row { slot = slot };

        var rect = NewRect("Slot" + slot, content);
        var element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = RowHeight;
        element.minHeight = RowHeight;

        var background = rect.gameObject.AddComponent<Image>();
        if (sprite != null)
        {
            background.sprite = sprite;
            background.type = sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            background.pixelsPerUnitMultiplier = 1.3f;
        }
        else
        {
            background.color = new Color(0.12f, 0.1f, 0.1f, 0.92f);
        }
        if (templateImage != null && sprite != null) background.color = templateImage.color;

        row.button = rect.gameObject.AddComponent<Button>();
        row.button.targetGraphic = background;
        if (templateButton != null)
        {
            var colors = templateButton.colors;
            colors.selectedColor = colors.normalColor; // no lasting highlight after a tap
            row.button.colors = colors;
        }
        row.button.navigation = new Navigation { mode = Navigation.Mode.None };
        row.button.onClick.AddListener(() => OnSlotClicked(row));

        // picture (16:9) on the left
        float pictureHeight = RowHeight - 2f * RowInset;
        float pictureWidth = pictureHeight * 16f / 9f;
        var frame = NewRect("PictureFrame", rect);
        frame.anchorMin = frame.anchorMax = new Vector2(0f, 0.5f);
        frame.pivot = new Vector2(0f, 0.5f);
        frame.anchoredPosition = new Vector2(RowInset + 6f, 0f);
        frame.sizeDelta = new Vector2(pictureWidth + 6f, pictureHeight + 6f);
        var frameImage = frame.gameObject.AddComponent<Image>();
        frameImage.color = new Color(0.55f, 0.47f, 0.36f, 1f);
        frameImage.raycastTarget = false;

        var pictureRect = NewRect("Picture", frame);
        Stretch(pictureRect);
        pictureRect.offsetMin = new Vector2(3f, 3f);
        pictureRect.offsetMax = new Vector2(-3f, -3f);
        row.picture = pictureRect.gameObject.AddComponent<RawImage>();
        row.picture.color = new Color(0.07f, 0.065f, 0.06f, 1f);
        row.picture.raycastTarget = false;

        row.pictureLabel = NewText("PictureLabel", pictureRect, templateText, 26f, TextAlignmentOptions.Center);
        Stretch((RectTransform)row.pictureLabel.transform);
        row.pictureLabel.color = EmptyColor;

        // name and date on the right of the picture
        float textLeft = RowInset + 6f + pictureWidth + 6f + 34f;
        row.title = NewText("Title", rect, templateText, 46f, TextAlignmentOptions.BottomLeft);
        var titleRect = (RectTransform)row.title.transform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(textLeft, 4f);
        titleRect.offsetMax = new Vector2(-RowInset - 10f, -RowInset);

        row.date = NewText("Date", rect, templateText, 34f, TextAlignmentOptions.TopLeft);
        var dateRect = (RectTransform)row.date.transform;
        dateRect.anchorMin = new Vector2(0f, 0f);
        dateRect.anchorMax = new Vector2(1f, 0.5f);
        dateRect.offsetMin = new Vector2(textLeft, RowInset);
        dateRect.offsetMax = new Vector2(-RowInset - 10f, -6f);
        row.date.color = DateColor;

        return row;
    }

    // ---------------- showing the slots ---------------- //

    void OnEnable()
    {
        if (rows.Count > 0) Refresh();
        if (overrideAlert != null) overrideAlert.SetActive(false);
        if (scroll != null) StartCoroutine(OpenAtTop());

        // a finger always moves a little: make a tap on a slot still count as a tap, not as a scroll
        var events = EventSystem.current;
        if (events != null && Screen.dpi > 0f)
        {
            int slop = Mathf.RoundToInt(Screen.dpi / 25.4f * TapSlopMm);
            if (slop > events.pixelDragThreshold)
            {
                oldDragThreshold = events.pixelDragThreshold;
                events.pixelDragThreshold = slop;
            }
        }
    }

    // The list opens at slot 1. The in-game menu canvas is stored at scale 0 and gets its real size only
    // after it is shown: until then the list cannot measure itself and would jump, so it waits.
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

    void OnDisable()
    {
        if (overrideAlert != null) overrideAlert.SetActive(false);
        if (oldDragThreshold >= 0 && EventSystem.current != null) EventSystem.current.pixelDragThreshold = oldDragThreshold;
        oldDragThreshold = -1;
    }

    void OnDestroy()
    {
        foreach (var row in rows)
        {
            if (row.texture != null) Destroy(row.texture);
        }
    }

    public void Refresh()
    {
        foreach (var row in rows) RefreshRow(row);
    }

    void RefreshRow(Row row)
    {
        var saves = SaveManager.Instance;
        bool saved = saves != null && !saves.IsSlotEmpty(row.slot);

        if (saved)
        {
            row.title.text = "Saved Game " + row.slot;
            row.title.color = Color.white;
            row.date.text = saves.GetSlotDate(row.slot);
        }
        else
        {
            row.title.text = "Slot " + row.slot;
            row.title.color = EmptyColor;
            row.date.text = "Empty";
        }
        row.date.color = saved ? DateColor : EmptyColor;

        // on the Load screen an empty slot cannot be picked
        row.button.interactable = saved || mode == Mode.Save;

        bool hasPicture = saved && LoadPicture(row, saves.ThumbnailPath(row.slot));
        if (hasPicture)
        {
            row.picture.texture = row.texture;
            row.picture.color = Color.white;
            row.picture.uvRect = CropTo16By9(row.texture);
            row.pictureLabel.text = "";
        }
        else
        {
            row.picture.texture = null;
            row.picture.color = new Color(0.07f, 0.065f, 0.06f, 1f);
            row.picture.uvRect = new Rect(0f, 0f, 1f, 1f);
            // an old save (from before the pictures) / an empty slot ("+" = a new save can go here)
            row.pictureLabel.text = saved ? "No picture" : (mode == Mode.Save ? "+" : "");
            row.pictureLabel.fontSize = saved ? 26f : 64f;
        }
    }

    // Loads the slot's picture (only again when the file changed). False when there is none.
    static bool LoadPicture(Row row, string path)
    {
        try
        {
            if (!File.Exists(path)) return false;
            DateTime time = File.GetLastWriteTimeUtc(path);
            if (row.texture != null && row.textureTime == time) return true;

            byte[] bytes = File.ReadAllBytes(path);
            var texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return false;
            }
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            if (row.texture != null) Destroy(row.texture);
            row.texture = texture;
            row.textureTime = time;
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("Could not show the picture of save slot " + row.slot + ": " + e.Message);
            return false;
        }
    }

    // pictures of another shape (an older one, another screen) are cut to the frame, not squashed
    static Rect CropTo16By9(Texture texture)
    {
        float aspect = (float)texture.width / Mathf.Max(1, texture.height);
        const float target = 16f / 9f;
        if (aspect > target)
        {
            float w = target / aspect;
            return new Rect((1f - w) / 2f, 0f, w, 1f);
        }
        float h = aspect / target;
        return new Rect(0f, (1f - h) / 2f, 1f, h);
    }

    // ---------------- picking a slot ---------------- //

    void OnSlotClicked(Row row)
    {
        var saves = SaveManager.Instance;
        if (saves == null) return;
        Deselect();

        if (mode == Mode.Load)
        {
            if (!saves.IsSlotEmpty(row.slot)) saves.StartLoadedGame(row.slot);
            return;
        }

        if (saves.IsSlotEmpty(row.slot) || overrideAlert == null)
        {
            SaveTo(row);
        }
        else
        {
            AskToOverwrite(row);
        }
    }

    void SaveTo(Row row)
    {
        SaveManager.Instance.SaveGame(row.slot);
        RefreshRow(row);
        Deselect();
    }

    // "overwrite this save?" - the scene's own question box, with Yes / No
    void AskToOverwrite(Row row)
    {
        var yes = overrideAlert.transform.Find("YesButton")?.GetComponent<Button>();
        var no = overrideAlert.transform.Find("NoButton")?.GetComponent<Button>();
        if (yes == null || no == null)
        {
            SaveTo(row);
            return;
        }

        yes.onClick.RemoveAllListeners(); // only this slot: earlier questions must not save too
        no.onClick.RemoveAllListeners();
        yes.onClick.AddListener(() =>
        {
            overrideAlert.SetActive(false);
            SaveTo(row);
        });
        no.onClick.AddListener(() =>
        {
            overrideAlert.SetActive(false);
            Deselect();
        });

        overrideAlert.transform.SetAsLastSibling(); // in front of the list
        overrideAlert.SetActive(true);
    }

    static void Deselect()
    {
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }

    // ---------------- helpers ---------------- //

    static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static TextMeshProUGUI NewText(string name, Transform parent, TextMeshProUGUI template, float size, TextAlignmentOptions alignment)
    {
        var text = NewRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (template != null)
        {
            text.font = template.font;
            text.fontSharedMaterial = template.fontSharedMaterial;
            text.fontStyle = template.fontStyle;
        }
        text.fontSize = size;
        text.alignment = alignment;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        text.color = Color.white;
        return text;
    }

    // The slot frame picture has no border settings, so stretching it to a lower row would squash its
    // frame. A copy with borders keeps the frame's thickness at any size.
    static Sprite MakeSlicedSprite(Sprite sprite)
    {
        if (sprite == null) return null;
        if (sprite.border != Vector4.zero || sprite.packed) return sprite;
        Rect r = sprite.rect;
        float border = Mathf.Round(Mathf.Min(r.width, r.height) * 0.09f); // the frame is about 9% of the height
        return Sprite.Create(sprite.texture, r, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0,
            SpriteMeshType.FullRect, new Vector4(border, border, border, border));
    }
}
