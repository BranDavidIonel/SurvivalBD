using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Bigger, cleaner game windows, made in code when the game scene loads (the scene file is not edited):
//  - inventory, crafting, storage box and campfire windows on the game's stone frame, with big slots (finger size
//    on the phone), a title and an X to close; the crafting categories also get "< Back"
//  - the inventory sits on the left and the crafting / storage box / campfire window on the right
//    (a window that is open alone is centred), between the clock at the top and the quick bar at the bottom
//  - the item info line and the trash can at the bottom of the inventory, a clearer "throw away?" question,
//    and a pickup message (top left) that goes away by itself
//  - the quick bar is done by QuickBarUI
// Only sizes, positions and pictures change: the names and the order of the slots that the game code looks for
// stay the same (an item must stay the only child of its slot).
[DefaultExecutionOrder(100)] // Start after the game scripts have found their slots and buttons
public class GameWindowsLayout : MonoBehaviour
{
    static bool hooked;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hooked = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) => SetUp();
        }
        SetUp();
    }

    static void SetUp()
    {
        if (FindObjectOfType<InventorySystem>() == null) return;
        if (FindObjectOfType<GameWindowsLayout>() != null) return;
        new GameObject("GameWindowsLayout (made in code)").AddComponent<GameWindowsLayout>();
    }

    // ---------------- sizes (1080p reference units) ---------------- //
    bool phone;
    float slot, gap, pad, titleHeight, infoHeight, closeSize, windowGap;
    int titleFont, textFont, smallFont;
    const float TopLimit = 262f; // the windows stay under the clock and the day line at the top of the screen

    RectTransform canvasRect;
    Sprite stone, closeSprite;

    RectTransform inventoryWindow, storageWindow, campfireWindow, deletionAlert, pickupPopup;
    RectTransform[] craftingScreens;
    Vector2 inventorySize, craftingSize, storageSize, campfireSize;
    float campfireScale = 1f;
    GameObject infoHint;
    GameObject itemInfo;

    void Awake()
    {
        var inventory = FindObjectOfType<InventorySystem>();
        if (inventory == null || inventory.inventoryScreenUI == null) return;
        var canvas = RootCanvasOf(inventory.inventoryScreenUI.transform);
        if (canvas == null) return;
        canvasRect = (RectTransform)canvas.transform;

        // The game UI was laid out for a 1080 px high screen. Scale it with the screen on PC too
        // (the phone controls already do this on phones), so it is the same at any resolution.
        var scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
    }

    static Canvas RootCanvasOf(Transform t)
    {
        Canvas found = null;
        for (; t != null; t = t.parent)
        {
            var c = t.GetComponent<Canvas>();
            if (c != null) found = c;
        }
        return found;
    }

    void Start()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null || canvasRect == null) { enabled = false; return; }

        phone = GameInput.TouchControls;
        slot = phone ? 116f : 88f;
        gap = phone ? 12f : 10f;
        pad = phone ? 24f : 20f;
        titleHeight = phone ? 72f : 58f;
        infoHeight = phone ? 92f : 76f;
        closeSize = phone ? 66f : 50f;
        windowGap = phone ? 24f : 20f;
        titleFont = phone ? 34 : 28;
        textFont = phone ? 26 : 21;
        smallFont = phone ? 22 : 18;

        var background = inventory.inventoryScreenUI.GetComponent<Image>();
        stone = UISkin.StonePanel(background != null ? background.sprite : null);
        if (inventory.pickupButtonClose != null && inventory.pickupButtonClose.image != null)
            closeSprite = inventory.pickupButtonClose.image.sprite; // the wooden X of the game's GUI pack

        Try(BuildInventory, "inventory");
        Try(BuildDeletionAlert, "trash question");
        Try(BuildCrafting, "crafting");
        Try(BuildStorage, "storage box");
        Try(BuildCampfire, "campfire");
        Try(BuildPickupPopup, "pickup message");
        Try(() => gameObject.AddComponent<QuickBarUI>().Build(phone, canvasRect), "quick bar");

        LateUpdate();
    }

    // one part failing (a renamed object in the scene...) must not stop the others
    static void Try(System.Action build, string what)
    {
        try { build(); }
        catch (System.Exception e) { Debug.LogWarning("GameWindowsLayout: could not restyle the " + what + ": " + e); }
    }

    // ---------------- inventory ---------------- //

    void BuildInventory()
    {
        var inventory = InventorySystem.Instance;
        inventoryWindow = (RectTransform)inventory.inventoryScreenUI.transform;

        int columns = inventory.slotList.Count > 24 ? 8 : 6; // 32 slots: 4 rows of 8
        int count = inventory.slotList.Count;
        int rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)columns));
        float gridWidth = columns * slot + (columns - 1) * gap;
        float gridHeight = rows * slot + (rows - 1) * gap;
        inventorySize = new Vector2(gridWidth + 2f * pad, titleHeight + gridHeight + gap + 4f + infoHeight + pad);

        SetupWindow(inventoryWindow, inventorySize, 1f);
        AddTitle(inventoryWindow, null, "Inventory", inventorySize);
        AddClose(inventoryWindow, () => { if (InventorySystem.Instance.isOpen) InventorySystem.Instance.CloseUI(); });

        float left = -inventorySize.x * 0.5f + pad;
        float top = inventorySize.y * 0.5f - titleHeight;
        for (int i = 0; i < count; i++)
        {
            int row = i / columns, column = i % columns;
            var rect = (RectTransform)inventory.slotList[i].transform;
            UISkin.Centered(rect, new Vector2(left + slot * 0.5f + column * (slot + gap), top - slot * 0.5f - row * (slot + gap)),
                new Vector2(slot, slot));
            StyleSlot(rect);
        }

        // bottom line: what the item is (left) and the trash can (right)
        float bottomY = -inventorySize.y * 0.5f + pad + infoHeight * 0.5f;
        float infoWidth = inventorySize.x - 2f * pad - gap - infoHeight;
        Vector2 infoPos = new Vector2(left + infoWidth * 0.5f, bottomY);

        var hintBack = UISkin.NewImage("ItemInfoArea", inventoryWindow, UISkin.Card, Color.white, false);
        UISkin.Centered(hintBack.rectTransform, infoPos, new Vector2(infoWidth, infoHeight));
        string hintText = phone
            ? "Tap an item to see what it is.\nDouble tap: use it / put it in the quick bar.  Drag: move it."
            : "Point at an item to see what it is.\nRight click: use it / put it in the quick bar.  Drag: move it.";
        var hint = UISkin.NewText("Hint", hintBack.transform, hintText, smallFont, UISkin.TextDim, TextAnchor.MiddleCenter, false);
        UISkin.Fill(hint.rectTransform, 8f);
        infoHint = hintBack.gameObject;

        if (inventory.ItemInfoUI != null)
        {
            itemInfo = inventory.ItemInfoUI;
            var info = (RectTransform)itemInfo.transform;
            info.SetParent(inventoryWindow, false);
            info.SetAsLastSibling();
            UISkin.Centered(info, infoPos, new Vector2(infoWidth, infoHeight));
            var infoImage = info.GetComponent<Image>();
            if (infoImage != null)
            {
                infoImage.sprite = UISkin.Card;
                infoImage.type = Image.Type.Sliced;
                infoImage.color = Color.white;
                infoImage.raycastTarget = false;
            }
            float line = (infoHeight - 12f) / 3f;
            InfoLine(info, "itemName", 0, line, infoWidth, textFont, UISkin.Gold, true);
            InfoLine(info, "itemDescription", 1, line, infoWidth, smallFont, UISkin.TextColor, false);
            InfoLine(info, "itemFunctionality", 2, line, infoWidth, smallFont - 1, UISkin.TextDim, false);
        }

        var trash = inventoryWindow.Find("trash") as RectTransform;
        if (trash != null)
        {
            trash.SetAsLastSibling();
            UISkin.Centered(trash, new Vector2(inventorySize.x * 0.5f - pad - infoHeight * 0.5f, bottomY), new Vector2(infoHeight, infoHeight));
            var trashImage = trash.GetComponent<Image>();
            if (trashImage != null)
            {
                trashImage.sprite = UISkin.Slot;
                trashImage.type = Image.Type.Sliced;
                trashImage.color = new Color(1f, 0.85f, 0.85f, 1f);
            }
            var lid = trash.Find("backgroundTrash") as RectTransform;
            if (lid != null)
            {
                UISkin.Centered(lid, Vector2.zero, Vector2.one * infoHeight * 0.62f);
                var lidImage = lid.GetComponent<Image>();
                if (lidImage != null) lidImage.preserveAspect = true;
            }
        }
    }

    void InfoLine(RectTransform info, string name, int index, float line, float width, int fontSize, Color color, bool bold)
    {
        var rect = info.Find(name) as RectTransform;
        if (rect == null) return;
        UISkin.Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -6f - index * line), new Vector2(width - 32f, line));
        var text = rect.GetComponent<Text>();
        UISkin.Style(text, fontSize, color, TextAnchor.MiddleLeft, bold);
        if (text != null) text.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    void BuildDeletionAlert()
    {
        var trash = inventoryWindow != null ? inventoryWindow.Find("trash") : null;
        var trashSlot = trash != null ? trash.GetComponent<TrashSlot>() : null;
        if (trashSlot == null || trashSlot.trashAlertUI == null) return;

        deletionAlert = (RectTransform)trashSlot.trashAlertUI.transform;
        Vector2 size = phone ? new Vector2(500f, 280f) : new Vector2(400f, 230f);
        SetupWindow(deletionAlert, size, 1f);

        var question = deletionAlert.Find("Text") as RectTransform;
        if (question != null)
        {
            UISkin.Place(question, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(size.x - 48f, size.y * 0.36f));
            UISkin.Style(question.GetComponent<Text>(), textFont + 2, UISkin.TextColor, TextAnchor.MiddleCenter);
        }
        float button = phone ? 92f : 70f;
        AlertButton("yes", "Yes", new Vector2(-size.x * 0.2f, -size.y * 0.1f), button);
        AlertButton("no", "No", new Vector2(size.x * 0.2f, -size.y * 0.1f), button);
    }

    void AlertButton(string name, string text, Vector2 position, float size)
    {
        var rect = deletionAlert.Find(name) as RectTransform;
        if (rect == null) return;
        UISkin.Centered(rect, position, new Vector2(size, size));
        var image = rect.GetComponent<Image>();
        if (image != null) image.preserveAspect = true;
        var label = rect.Find(name) as RectTransform; // the label has the same name as its button
        if (label == null) return;
        UISkin.Place(label, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(size * 1.6f, smallFont * 1.6f));
        var labelText = label.GetComponent<Text>();
        UISkin.Style(labelText, smallFont + 2, UISkin.TextColor, TextAnchor.UpperCenter);
        if (labelText != null) labelText.text = text;
    }

    // ---------------- crafting ---------------- //

    void BuildCrafting()
    {
        var crafting = CraftingSystem.Instance;
        if (crafting == null) return;
        craftingScreens = new[]
        {
            (RectTransform)crafting.craftingScreenUI.transform,
            (RectTransform)crafting.toolsScreenUI.transform,
            (RectTransform)crafting.survivalScreenUI.transform,
            (RectTransform)crafting.refineScreenUI.transform,
            (RectTransform)crafting.constructionScreenUI.transform,
        };
        craftingSize = new Vector2(phone ? 700f : 560f, Mathf.Max(inventorySize.y, phone ? 520f : 420f));
        string[] titles = { "Crafting", "Tools", "Survival", "Refine & Process", "Construction" };

        for (int i = 0; i < craftingScreens.Length; i++)
        {
            var screen = craftingScreens[i];
            SetupWindow(screen, craftingSize, 1f);
            var oldTitle = screen.Find("Text (Legacy)");
            AddTitle(screen, oldTitle != null ? oldTitle.GetComponent<Text>() : null, titles[i], craftingSize);
            AddClose(screen, () => { if (CraftingSystem.Instance.isOpen) CraftingSystem.Instance.CloseUI(); });
            if (i > 0) AddBack(screen, () => CraftingSystem.Instance.ShowCategories());
        }

        // the 4 categories: big tiles, 2 x 2
        string[] tiles = { "ToolsButton", "SurvivalButton", "RefineButton", "ConstructionButton" };
        string[] tileNames = { "Tools", "Survival", "Refine & Process", "Construction" };
        var main = craftingScreens[0];
        float tileWidth = (craftingSize.x - 2f * pad - gap) * 0.5f;
        float areaTop = craftingSize.y * 0.5f - titleHeight - 6f;
        float tileHeight = (areaTop - (-craftingSize.y * 0.5f + pad) - gap) * 0.5f;
        for (int i = 0; i < tiles.Length; i++)
        {
            var tile = main.Find(tiles[i]) as RectTransform;
            if (tile == null) continue;
            int row = i / 2, column = i % 2;
            UISkin.Centered(tile, new Vector2(-craftingSize.x * 0.5f + pad + tileWidth * 0.5f + column * (tileWidth + gap),
                areaTop - tileHeight * 0.5f - row * (tileHeight + gap)), new Vector2(tileWidth, tileHeight));
            var image = tile.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = UISkin.Card;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            var button = tile.GetComponent<Button>();
            if (button != null) UISkin.SetTint(button);

            var icon = tile.Find("Image") as RectTransform;
            if (icon != null)
            {
                float iconSize = Mathf.Min(tileHeight * 0.52f, phone ? 120f : 96f);
                UISkin.Centered(icon, new Vector2(0f, tileHeight * 0.1f), new Vector2(iconSize, iconSize));
                var iconImage = icon.GetComponent<Image>();
                if (iconImage != null) { iconImage.preserveAspect = true; iconImage.raycastTarget = false; }
            }
            var label = tile.Find("Text (Legacy)") as RectTransform;
            if (label != null)
            {
                UISkin.Place(label, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(tileWidth - 20f, textFont * 1.7f));
                var text = label.GetComponent<Text>();
                UISkin.Style(text, textFont, UISkin.TextColor, TextAnchor.MiddleCenter);
                if (text != null) text.text = tileNames[i];
            }
        }

        LayoutCards(craftingScreens[1], new[] { "Axe", "Torch", "IronAxe" }, new[] { "Stone Axe", "Torch", "Iron Axe" });
        LayoutCards(craftingScreens[2], new[] { "Campfire", "StorageBox" }, new[] { "Campfire", "Storage Box" });
        LayoutCards(craftingScreens[3], new[] { "Plank", "Rope" }, new[] { "Planks x2", "Rope" });
        LayoutCards(craftingScreens[4], new[] { "Foundation", "Wall" }, new[] { "Foundation", "Wall" });

        crafting.RefreshNeededItems();
    }

    void LayoutCards(RectTransform screen, string[] names, string[] titles)
    {
        int n = names.Length;
        float cardWidth = Mathf.Min(phone ? 300f : 240f, (craftingSize.x - 2f * pad - (n - 1) * gap) / n);
        float areaTop = craftingSize.y * 0.5f - titleHeight - 6f;
        float areaBottom = -craftingSize.y * 0.5f + pad;
        float cardHeight = areaTop - areaBottom;
        float rowWidth = n * cardWidth + (n - 1) * gap;
        float iconSize = phone ? 120f : 92f;
        float nameHeight = textFont * 1.6f;
        float reqHeight = smallFont * 1.6f;

        for (int i = 0; i < n; i++)
        {
            var card = screen.Find(names[i]) as RectTransform;
            if (card == null) continue;
            UISkin.Centered(card, new Vector2(-rowWidth * 0.5f + cardWidth * 0.5f + i * (cardWidth + gap), (areaTop + areaBottom) * 0.5f),
                new Vector2(cardWidth, cardHeight));
            var image = card.GetComponent<Image>();
            if (image != null)
            {
                image.sprite = UISkin.Card;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }

            // a card with many lines (the raft) gets a smaller picture and tighter lines
            bool many = card.Find("req3") != null;
            float cardIcon = many ? iconSize * 0.6f : iconSize;
            float lineHeight = many ? reqHeight * 0.86f : reqHeight;
            float y = -18f;
            var icon = card.Find("Image") as RectTransform;
            if (icon != null)
            {
                UISkin.Place(icon, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardIcon, cardIcon));
                var iconImage = icon.GetComponent<Image>();
                if (iconImage != null) iconImage.preserveAspect = true;
            }
            y -= cardIcon + 8f;

            var name = card.Find("Text") as RectTransform;
            if (name != null)
            {
                UISkin.Place(name, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardWidth - 16f, nameHeight));
                var text = name.GetComponent<Text>();
                UISkin.Style(text, textFont + 2, UISkin.Gold, TextAnchor.MiddleCenter);
                if (text != null) text.text = titles[i];
            }
            y -= nameHeight + 8f;

            foreach (var reqName in new[] { "req1", "req2", "req3", "req4", "req5" })
            {
                var req = card.Find(reqName) as RectTransform;
                if (req == null) continue;
                UISkin.Place(req, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(cardWidth - 16f, lineHeight));
                UISkin.Style(req.GetComponent<Text>(), many ? smallFont : smallFont + 2, UISkin.TextColor, TextAnchor.MiddleCenter);
                y -= lineHeight + 2f;
            }

            var craft = card.Find("Button") as RectTransform;
            if (craft != null)
            {
                UISkin.Place(craft, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f),
                    new Vector2(cardWidth - 36f, phone ? 76f : 56f));
                var craftImage = craft.GetComponent<Image>();
                if (craftImage != null)
                {
                    craftImage.sprite = UISkin.Pill;
                    craftImage.type = Image.Type.Sliced;
                    craftImage.color = UISkin.CraftGreen;
                    craftImage.pixelsPerUnitMultiplier = phone ? 1.2f : 1.6f;
                }
                var button = craft.GetComponent<Button>();
                if (button != null) UISkin.SetTint(button);
                var label = craft.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    UISkin.Fill(label.rectTransform);
                    UISkin.Style(label, textFont + 4, Color.white, TextAnchor.MiddleCenter);
                    label.text = "Craft";
                }
                craft.gameObject.SetActive(true);
            }
        }
    }

    // ---------------- storage box ---------------- //

    void BuildStorage()
    {
        var storage = StorageManager.Instance;
        if (storage == null || storage.StorageUI == null) return;
        storageWindow = (RectTransform)storage.StorageUI.transform;

        var slots = new System.Collections.Generic.List<RectTransform>();
        foreach (Transform child in storageWindow)
        {
            if (child.GetComponent<ItemSlot>() != null) slots.Add((RectTransform)child);
        }
        const int columns = 4;
        int rows = Mathf.Max(1, Mathf.CeilToInt(slots.Count / (float)columns));
        storageSize = new Vector2(columns * slot + (columns - 1) * gap + 2f * pad, titleHeight + rows * slot + (rows - 1) * gap + pad);
        SetupWindow(storageWindow, storageSize, 1f);
        AddTitle(storageWindow, null, "Storage Box", storageSize);

        float left = -storageSize.x * 0.5f + pad;
        float top = storageSize.y * 0.5f - titleHeight;
        for (int i = 0; i < slots.Count; i++)
        {
            int row = i / columns, column = i % columns;
            UISkin.Centered(slots[i], new Vector2(left + slot * 0.5f + column * (slot + gap), top - slot * 0.5f - row * (slot + gap)),
                new Vector2(slot, slot));
            StyleSlot(slots[i]);
        }

        var cancel = storageWindow.Find("ButtonCancel") as RectTransform;
        if (cancel != null) StyleClose(cancel, 1f);
    }

    // ---------------- campfire ---------------- //

    void BuildCampfire()
    {
        var fire = CampfireUIManager.Instance;
        if (fire == null || fire.campfirePanel == null) return;
        campfireWindow = (RectTransform)fire.campfirePanel.transform;
        campfireScale = phone ? 1.45f : 1.15f;

        // its inside was laid out for a 365 x 367 window: keep that and make the whole window bigger
        UISkin.Centered(campfireWindow, Vector2.zero, new Vector2(365f, 367f));
        campfireWindow.localScale = Vector3.one * campfireScale;
        UISkin.SetWindowBackground(campfireWindow.GetComponent<Image>(), stone, campfireScale);
        campfireSize = new Vector2(365f, 367f) * campfireScale;

        if (fire.foodSlot != null) StyleSlot((RectTransform)fire.foodSlot.transform);
        if (fire.fuelSlot != null) StyleSlot((RectTransform)fire.fuelSlot.transform);

        var cancel = fire.exitButton != null ? (RectTransform)fire.exitButton.transform : campfireWindow.Find("ButtonCancel") as RectTransform;
        if (cancel != null) StyleClose(cancel, campfireScale);
    }

    // ---------------- pickup message ---------------- //

    void BuildPickupPopup()
    {
        var inventory = InventorySystem.Instance;
        if (inventory.pickupAlert == null) return;
        pickupPopup = (RectTransform)inventory.pickupAlert.transform;
        Vector2 size = phone ? new Vector2(400f, 92f) : new Vector2(320f, 72f);
        UISkin.Place(pickupPopup, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -24f), size);
        var image = pickupPopup.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = UISkin.Card;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = false;
        }
        // it goes away by itself: touches go through it (it was in the way of the walking joystick)
        var group = pickupPopup.GetComponent<CanvasGroup>();
        if (group == null) group = pickupPopup.gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;
        group.interactable = false;

        float iconSize = size.y - 22f;
        if (inventory.pickupImage != null)
        {
            UISkin.Place(inventory.pickupImage.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(iconSize, iconSize));
            inventory.pickupImage.preserveAspect = true;
        }
        if (inventory.pickupName != null)
        {
            UISkin.Place(inventory.pickupName.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(iconSize + 28f, 0f),
                new Vector2(size.x - iconSize - 40f, size.y - 12f));
            UISkin.Style(inventory.pickupName, textFont, UISkin.TextColor, TextAnchor.MiddleLeft);
        }
        if (inventory.pickupButtonClose != null) inventory.pickupButtonClose.gameObject.SetActive(false);
    }

    // ---------------- shared pieces ---------------- //

    void SetupWindow(RectTransform window, Vector2 size, float scale)
    {
        UISkin.Centered(window, Vector2.zero, size);
        window.localScale = Vector3.one * scale;
        UISkin.SetWindowBackground(window.GetComponent<Image>(), stone, scale);
    }

    void AddTitle(RectTransform window, Text existing, string text, Vector2 size)
    {
        var title = existing != null ? existing : UISkin.NewText("WindowTitle", window, text, titleFont, UISkin.Gold, TextAnchor.MiddleCenter);
        title.text = text;
        UISkin.Style(title, titleFont, UISkin.Gold, TextAnchor.MiddleCenter);
        title.horizontalOverflow = HorizontalWrapMode.Overflow;
        UISkin.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f),
            new Vector2(size.x - 2f * (closeSize + 16f), titleHeight - 6f));

        var line = UISkin.NewImage("TitleLine", window, UISkin.White, new Color(UISkin.Gold.r, UISkin.Gold.g, UISkin.Gold.b, 0.35f), false);
        UISkin.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -titleHeight + 6f),
            new Vector2(size.x - 2f * pad - 24f, 2f));
    }

    void AddClose(RectTransform window, UnityAction onClick)
    {
        var button = UISkin.NewButton("CloseButton", window, closeSprite != null ? closeSprite : UISkin.Disc,
            closeSprite != null ? Color.white : new Color(0.6f, 0.15f, 0.12f), closeSprite != null ? "" : "X", textFont, Color.white, onClick);
        StyleClose((RectTransform)button.transform, 1f);
    }

    // an X button in the top-right corner of its window (scale: the window's own scale, so it ends up the same size)
    void StyleClose(RectTransform rect, float scale)
    {
        float size = closeSize / scale;
        UISkin.Place(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-12f / scale, -10f / scale), new Vector2(size, size));
        rect.SetAsLastSibling();
        var image = rect.GetComponent<Image>();
        if (image != null)
        {
            if (closeSprite != null) { image.sprite = closeSprite; image.color = Color.white; }
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = true;
            if (phone) image.raycastPadding = new Vector4(-14f, -14f, -14f, -14f) / scale; // easier to hit with a finger
        }
        var button = rect.GetComponent<Button>();
        if (button != null) UISkin.SetTint(button);
    }

    void AddBack(RectTransform window, UnityAction onClick)
    {
        Vector2 size = phone ? new Vector2(150f, 54f) : new Vector2(118f, 42f);
        var button = UISkin.NewButton("BackButton", window, UISkin.Pill, UISkin.ButtonBrown, "<  Back", smallFont + 2, Color.white, onClick);
        var image = (Image)button.targetGraphic;
        image.pixelsPerUnitMultiplier = phone ? 1.6f : 2f;
        UISkin.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 0.5f),
            new Vector2(pad * 0.75f, -(titleHeight - 6f) * 0.5f - 2f), size);
        if (phone) image.raycastPadding = new Vector4(-10f, -10f, -10f, -10f);
    }

    void StyleSlot(RectTransform rect)
    {
        var image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.sprite = UISkin.Slot;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
        }
        var fitter = rect.GetComponent<SlotFitter>();
        if (fitter == null) fitter = rect.gameObject.AddComponent<SlotFitter>();
        fitter.Fit();
    }

    // ---------------- where the windows are, every frame ---------------- //

    void LateUpdate()
    {
        if (canvasRect == null) return;
        float canvasWidth = canvasRect.rect.width;

        bool inventoryOpen = inventoryWindow != null && inventoryWindow.gameObject.activeSelf;
        RectTransform right = null;
        Vector2 rightSize = Vector2.zero;
        if (craftingScreens != null)
        {
            foreach (var screen in craftingScreens)
            {
                if (screen != null && screen.gameObject.activeSelf) { right = screen; rightSize = craftingSize; break; }
            }
        }
        if (right == null && storageWindow != null && storageWindow.gameObject.activeSelf) { right = storageWindow; rightSize = storageSize; }
        if (right == null && campfireWindow != null && campfireWindow.gameObject.activeSelf) { right = campfireWindow; rightSize = campfireSize; }

        if (infoHint != null && itemInfo != null) infoHint.SetActive(!itemInfo.activeSelf);
        if (deletionAlert != null && deletionAlert.gameObject.activeSelf && deletionAlert.GetSiblingIndex() != deletionAlert.parent.childCount - 1)
            deletionAlert.SetAsLastSibling();

        if (!inventoryOpen && right == null) return;

        float bottomLimit = QuickBarUI.TopInCanvas + 14f;
        float centerY = (TopLimit + bottomLimit) * 0.5f;
        float band = TopLimit - bottomLimit;

        float width = (inventoryOpen ? inventorySize.x : 0f) + (right != null ? rightSize.x : 0f) + (inventoryOpen && right != null ? windowGap : 0f);
        float height = Mathf.Max(inventoryOpen ? inventorySize.y : 0f, right != null ? rightSize.y : 0f);
        float scale = Mathf.Min(1f, (canvasWidth - 40f) / Mathf.Max(1f, width), band / Mathf.Max(1f, height));

        float x = -width * 0.5f * scale;
        if (inventoryOpen)
        {
            Move(inventoryWindow, new Vector2(x + inventorySize.x * 0.5f * scale, centerY), scale);
            x += (inventorySize.x + windowGap) * scale;
        }
        if (right != null)
        {
            Vector2 at = new Vector2(x + rightSize.x * 0.5f * scale, centerY);
            if (right == campfireWindow) Move(campfireWindow, at, campfireScale * scale);
            else if (right == storageWindow) Move(storageWindow, at, scale);
            else foreach (var screen in craftingScreens) Move(screen, at, scale); // all crafting screens share one place
        }
    }

    static void Move(RectTransform window, Vector2 position, float scale)
    {
        if (window == null) return;
        window.anchoredPosition = position;
        var s = Vector3.one * scale;
        if (window.localScale != s) window.localScale = s;
    }
}
