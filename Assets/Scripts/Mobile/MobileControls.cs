using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// On-screen controls for phones. Created automatically in the game scene when the game runs on a
// phone (or in the Device Simulator / with Tools > Mobile > Force Touch Controls), so nothing has to
// be added to the scene by hand. On PC nothing is created and the game plays as before.
//
//  left joystick ........ walk (forward / back / left / right)
//  right joystick ....... turn the camera (dragging on any free part of the screen turns it too)
//  Attack ............... left click: hit, pick up (Take), open (Open), place (Place)
//  Jump ................. Space (in water: hold "Up" to swim up, "Down" to dive)
//  Inventory / Crafting / Menu ... I / C / M (the phone's Back button also opens the menu)
//  Cancel ............... X while placing or building
//  quick slots .......... tap a slot to hold that item (keys 1-6), double tap an inventory item = right click
[DefaultExecutionOrder(-500)] // before the game scripts, so a press is seen by all of them in the same frame
public class MobileControls : MonoBehaviour
{
    [Header("Camera turning")]
    public float lookJoystickSpeed = 160f;           // degrees per second at full tilt
    public float swipeDegreesPerScreenHeight = 150f; // dragging a finger over the full screen height

    [Header("Layout (1920x1080 reference)")]
    public float joystickSize = 300f;
    public float primaryButtonSize = 200f;
    public float jumpButtonSize = 160f;
    public float smallButtonSize = 140f;

    // Phones render at 75% resolution: big FPS gain on weak GPUs, hard to see on a small screen.
    const float PhoneRenderScale = 0.75f;
    const int MinRenderHeight = 720;

    TouchJoystick moveStick, lookStick;
    TouchLookArea lookArea;
    TouchButton primaryButton, jumpButton, swimDownButton, cancelButton;
    GameObject gameplayGroup, lookCanvasObject;
    GameObject inventoryButton, craftingButton, menuButton;
    PlayerDeath playerDeath;

    static Sprite discSprite, ringSprite, pillSprite;
    static Font font;
    static bool phoneSettingsApplied;
    static bool hooked;

    // ---------------- automatic creation ---------------- //

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        hooked = false;
        phoneSettingsApplied = false;
        GameInput.ResetTouch();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (!hooked)
        {
            hooked = true;
            SceneManager.sceneLoaded += (scene, mode) => SetUpScene();
        }
        SetUpScene();
    }

    static void SetUpScene()
    {
        if (!GameInput.TouchControls) return;

        ApplyPhoneSettings();
        MakeCanvasesScaleWithScreen();

        // only in the scene with the player (not in the main menu)
        if (FindObjectOfType<PlayerMovement>() == null) return;
        if (FindObjectOfType<MobileControls>() != null) return;

        new GameObject("MobileControls").AddComponent<MobileControls>();
    }

    static void ApplyPhoneSettings()
    {
        if (phoneSettingsApplied || Application.isEditor) return;
        phoneSettingsApplied = true;

        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Application.targetFrameRate = 60;

        int height = Mathf.Max(MinRenderHeight, Mathf.RoundToInt(Screen.height * PhoneRenderScale));
        if (height < Screen.height)
        {
            int width = Mathf.RoundToInt(Screen.width * (float)height / Screen.height);
            Screen.SetResolution(width, height, true);
        }
    }

    // The game UI was laid out in pixels for a 1080 px high screen. On phones (other resolutions,
    // and the lower render resolution above) scale it with the screen so it keeps the same look.
    static void MakeCanvasesScaleWithScreen()
    {
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ConstantPixelSize) continue;
            var canvas = scaler.GetComponent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace) continue;

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
        }
    }

    // ---------------- runtime ---------------- //

    void Awake()
    {
        GameInput.ResetTouch();
        playerDeath = FindObjectOfType<PlayerDeath>();

        // an older joystick from the Joystick Pack may still be in the scene: these controls replace it
        foreach (var oldJoystick in FindObjectsOfType<Joystick>()) oldJoystick.gameObject.SetActive(false);

        var eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem == null)
        {
            eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).GetComponent<EventSystem>();
        }
        // A finger moves a little while it taps. With the default 10 px a tap on an inventory item or a quick slot
        // often turned into a "drag" and the tap was lost. (The joysticks and the camera swipe ignore this.)
        eventSystem.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt(Screen.height * 0.025f));

        LetTouchesThroughHud();
        BuildUI();
    }

    // HUD pieces that only show something (crosshair, clock, bars...). Let touches pass through them
    // to the camera swipe area underneath, instead of swallowing them.
    static readonly string[] DisplayOnlyHud = { "MiddleDotIcon", "HandIcon", "interaction_info_UI", "StatusBarArea", "chop_holder", "TimeUI", "DayUI" };

    static void LetTouchesThroughHud()
    {
        foreach (var graphic in FindObjectsOfType<Graphic>(true))
        {
            if (!graphic.raycastTarget || !IsUnderHud(graphic.transform)) continue;
            if (graphic.GetComponentsInParent<IEventSystemHandler>(true).Length > 0) continue; // it reacts to touches: keep it
            graphic.raycastTarget = false;
        }
    }

    static bool IsUnderHud(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (System.Array.IndexOf(DisplayOnlyHud, t.name) >= 0) return true;
        }
        return false;
    }

    void OnDestroy()
    {
        GameInput.ResetTouch();
    }

    // The quick bar (made big by QuickBarUI) sits at the bottom centre, between the two joystick areas. These areas
    // are above the game UI and would take the touches meant for the quick slots: keep them clear of the bar.
    float nextZoneFit;

    void FitJoysticksAroundQuickBar()
    {
        if (Time.unscaledTime < nextZoneFit) return;
        nextZoneFit = Time.unscaledTime + 0.5f;

        var equip = EquipSystem.Instance;
        if (equip == null || equip.quickSlotsPanel == null || Screen.width <= 0) return;
        var corners = new Vector3[4];
        ((RectTransform)equip.quickSlotsPanel.transform).GetWorldCorners(corners); // overlay canvas: screen pixels
        float left = corners[0].x / Screen.width - 0.01f;
        float right = corners[2].x / Screen.width + 0.01f;

        var moveZone = (RectTransform)moveStick.transform;
        var lookZone = (RectTransform)lookStick.transform;
        moveZone.anchorMax = new Vector2(Mathf.Clamp(left, 0.2f, 0.36f), moveZone.anchorMax.y);
        lookZone.anchorMin = new Vector2(Mathf.Clamp(right, 0.64f, 0.8f), lookZone.anchorMin.y);
    }

    void Update()
    {
        KeepCursorFree(); // runs before the EventSystem, so this frame's drags are not dropped
        GameInput.BeginTouchFrame();

        bool inventoryOpen = InventorySystem.Instance != null && InventorySystem.Instance.isOpen;
        bool craftingOpen = CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen;
        bool storageOpen = StorageManager.Instance != null && StorageManager.Instance.storageUIOpen;
        bool campfireOpen = CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen;
        bool menuOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        bool dead = playerDeath != null && playerDeath.IsDead;
        bool building = ConstructionManager.Instance != null && ConstructionManager.Instance.inConstructionMode;
        bool placing = (PlacementSystem.Instance != null && PlacementSystem.Instance.inPlacementMode && PlacementSystem.Instance.IsHoldingItem)
            || PinePlanting.Active; // planting a pine cone: "Place" and "Cancel"
        bool swimming = PlayerWater.Instance != null && PlayerWater.Instance.IsSwimming;

        bool playing = !inventoryOpen && !craftingOpen && !storageOpen && !campfireOpen && !menuOpen && !dead;

        SetActive(gameplayGroup, playing);
        SetActive(lookCanvasObject, playing);
        SetActive(cancelButton.gameObject, building || placing);
        SetActive(swimDownButton.gameObject, swimming);
        SetActive(inventoryButton, !menuOpen && !dead && !storageOpen && !campfireOpen && !building && !placing);
        SetActive(craftingButton, !menuOpen && !dead && !storageOpen && !campfireOpen && !building && !placing);
        SetActive(menuButton, !dead && !storageOpen && !campfireOpen);

        jumpButton.SetLabel(swimming ? "Up" : "Jump");
        primaryButton.SetLabel(PrimaryLabel(building, placing));

        // --- movement and camera --- //
        if (playing)
        {
            GameInput.touchMove = moveStick.Value;

            Vector2 look = lookStick.Value * lookJoystickSpeed * Time.deltaTime;
            float pixelsToDegrees = swipeDegreesPerScreenHeight / Mathf.Max(1, Screen.height);
            look += lookArea.TakeDelta() * pixelsToDegrees;
            GameInput.touchLookDegrees = look;
        }
        else
        {
            GameInput.touchMove = Vector2.zero;
            GameInput.touchLookDegrees = Vector2.zero;
        }
    }

    void LateUpdate()
    {
        // again after the game scripts, which lock the cursor when a menu closes
        KeepCursorFree();
        FitJoysticksAroundQuickBar();
    }

    // The game locks the mouse cursor (PC style). While it is locked the EventSystem sends no drag
    // events at all, not even for touches (PointerInputModule.ProcessDrag), so on the phone the
    // joysticks and the camera swipe would only get OnPointerDown and stay at 0. In the editor a
    // locked cursor would also stop the mouse from pressing the on-screen buttons. These controls
    // only exist with touch controls on, so keep the cursor free whenever they are there.
    static void KeepCursorFree()
    {
        if (Cursor.lockState == CursorLockMode.None) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    static string PrimaryLabel(bool building, bool placing)
    {
        if (building || placing) return "Place";

        var selection = SelectionManager.Instance;
        if (selection != null && selection.enabled)
        {
            if (selection.selectedStorageBox != null || selection.selectedCampfire != null) return "Open";
            if (selection.handIsVisible) return "Take";
        }
        return "Attack";
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
    }

    // ---------------- building the UI ---------------- //

    void BuildUI()
    {
        CreateSprites();

        // Canvas 1 (under the game UI): drag anywhere free on the screen to turn the camera.
        lookCanvasObject = CreateCanvas("MobileLookCanvas", -10);
        lookCanvasObject.transform.SetParent(transform, false);
        var lookAreaRect = CreateRect("LookArea", lookCanvasObject.transform, Vector2.zero, Vector2.one);
        lookAreaRect.gameObject.AddComponent<TouchZoneGraphic>();
        lookArea = lookAreaRect.gameObject.AddComponent<TouchLookArea>();

        // Canvas 2 (above the game UI): joysticks and buttons.
        var controlsCanvas = CreateCanvas("MobileControlsCanvas", 10);
        controlsCanvas.transform.SetParent(transform, false);
        var root = controlsCanvas.transform;

        gameplayGroup = CreateRect("Gameplay", root, Vector2.zero, Vector2.one).gameObject;
        var gameplay = gameplayGroup.transform;

        // joysticks: the touch zone is a large corner area, the ring rests near the corner
        moveStick = CreateJoystick("MoveJoystick", gameplay, new Vector2(0f, 0f), new Vector2(0.36f, 0.62f),
            new Vector2(0f, 0f), new Vector2(250f, 250f));
        lookStick = CreateJoystick("LookJoystick", gameplay, new Vector2(0.64f, 0f), new Vector2(1f, 0.62f),
            new Vector2(1f, 0f), new Vector2(-250f, 250f));

        // action buttons around the right joystick (bottom-right corner)
        primaryButton = CreateButton("PrimaryButton", gameplay, "Attack", GameInput.TouchAction.Primary,
            new Vector2(1f, 0f), new Vector2(-590f, 190f), Vector2.one * primaryButtonSize, discSprite, 34);
        jumpButton = CreateButton("JumpButton", gameplay, "Jump", GameInput.TouchAction.Jump,
            new Vector2(1f, 0f), new Vector2(-190f, 560f), Vector2.one * jumpButtonSize, discSprite, 32);
        swimDownButton = CreateButton("SwimDownButton", gameplay, "Down", GameInput.TouchAction.SwimDown,
            new Vector2(1f, 0f), new Vector2(-390f, 560f), Vector2.one * smallButtonSize, discSprite, 28);
        cancelButton = CreateButton("CancelButton", gameplay, "Cancel", GameInput.TouchAction.Cancel,
            new Vector2(1f, 0f), new Vector2(-590f, 420f), Vector2.one * smallButtonSize, discSprite, 28);
        cancelButton.normalColor = new Color(0.55f, 0.05f, 0.05f, 0.6f);

        // menu buttons, top-right corner
        var pill = new Vector2(210f, 84f);
        menuButton = CreateButton("MenuButton", root, "Menu", GameInput.TouchAction.Menu,
            new Vector2(1f, 1f), new Vector2(-130f, -70f), pill, pillSprite, 30).gameObject;
        craftingButton = CreateButton("CraftingButton", root, "Crafting", GameInput.TouchAction.Crafting,
            new Vector2(1f, 1f), new Vector2(-360f, -70f), pill, pillSprite, 30).gameObject;
        inventoryButton = CreateButton("InventoryButton", root, "Inventory", GameInput.TouchAction.Inventory,
            new Vector2(1f, 1f), new Vector2(-590f, -70f), pill, pillSprite, 30).gameObject;

        swimDownButton.gameObject.SetActive(false);
        cancelButton.gameObject.SetActive(false);
    }

    static GameObject CreateCanvas(string name, int sortingOrder)
    {
        var canvasObject = new GameObject(name, typeof(RectTransform));
        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        canvasObject.AddComponent<GraphicRaycaster>();
        return canvasObject;
    }

    // a rect stretched between two anchors of its parent
    static RectTransform CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    // a fixed-size rect placed relative to one anchor point of its parent
    static RectTransform CreatePlacedRect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    static Image AddImage(GameObject target, Sprite sprite, Color color, bool raycast)
    {
        var image = target.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = raycast;
        if (sprite == pillSprite) image.type = Image.Type.Sliced;
        return image;
    }

    TouchJoystick CreateJoystick(string name, Transform parent, Vector2 zoneMin, Vector2 zoneMax, Vector2 restAnchor, Vector2 restPosition)
    {
        var zone = CreateRect(name, parent, zoneMin, zoneMax);
        zone.gameObject.AddComponent<TouchZoneGraphic>();

        var background = CreatePlacedRect("Ring", zone, restAnchor, restPosition, Vector2.one * joystickSize);
        AddImage(background.gameObject, ringSprite, new Color(1f, 1f, 1f, 0.55f), false);

        var handle = CreatePlacedRect("Knob", background, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * joystickSize * 0.42f);
        AddImage(handle.gameObject, discSprite, new Color(1f, 1f, 1f, 0.7f), false);

        var joystick = zone.gameObject.AddComponent<TouchJoystick>();
        joystick.background = background;
        joystick.handle = handle;
        return joystick;
    }

    static TouchButton CreateButton(string name, Transform parent, string text, GameInput.TouchAction action,
        Vector2 anchor, Vector2 position, Vector2 size, Sprite sprite, int fontSize)
    {
        var rect = CreatePlacedRect(name, parent, anchor, position, size);
        var button = rect.gameObject.AddComponent<TouchButton>();
        button.action = action;
        button.image = AddImage(rect.gameObject, sprite, button.normalColor, true);

        var labelRect = CreateRect("Label", rect, Vector2.zero, Vector2.one);
        var label = labelRect.gameObject.AddComponent<Text>();
        label.font = font;
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        labelRect.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        button.label = label;

        // white rim around round buttons
        if (sprite == discSprite)
        {
            var rimRect = CreateRect("Rim", rect, Vector2.zero, Vector2.one);
            rimRect.SetAsFirstSibling();
            AddImage(rimRect.gameObject, ringSprite, new Color(1f, 1f, 1f, 0.6f), false);
        }
        return button;
    }

    // ---------------- generated sprites (no image files needed) ---------------- //

    // the same round / rounded sprites as the game windows (UISkin)
    static void CreateSprites()
    {
        font = UISkin.Font;
        discSprite = UISkin.Disc;
        ringSprite = UISkin.Ring;
        pillSprite = UISkin.Pill;
    }
}
