using UnityEngine;
using UnityEngine.UI;

// Buttons for the PC (made in code), like the phone has: Inventory, Crafting, Menu and Help at the top right, each
// with its key written on it (I, C, M, H), so nobody has to remember the keys. The mouse is locked while playing:
// Tab frees it to click a button (Tab again, or opening a window, locks it back). H opens the Help on any platform,
// and the game's menu gets a "Help" button too (phone and PC).
[DefaultExecutionOrder(-900)]
public class PcControls : MonoBehaviour
{
    GameObject buttons;
    bool hasMobileControls;
    bool freeMouse;
    MouseMovement look;

    void Start()
    {
        hasMobileControls = FindObjectOfType<MobileControls>() != null;
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null) look = player.GetComponent<MouseMovement>();
        if (!GameInput.TouchControls) BuildButtons();
        StartCoroutine(AddMenuHelpButtonLater()); // after InGameLoadMenu has put "Load Game" in and moved the others
    }

    void Update()
    {
        // the phone controls turn button presses into "pressed this frame"; without them this does it
        if (!hasMobileControls) GameInput.BeginTouchFrame();

        if (Input.GetKeyDown(KeyCode.H)) HelpScreen.Toggle();
        if (Input.GetKeyDown(KeyCode.N) && !MapScreen.IsOpen) MapScreen.Open(); // (N again closes it: MapScreen)

        bool windows = (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen)
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || HelpScreen.IsOpen || BookReader.IsOpen || MapScreen.IsOpen;
        if (!GameInput.TouchControls)
        {
            if (Input.GetKeyDown(KeyCode.Tab) && !windows) SetFreeMouse(!freeMouse);
            if (freeMouse && windows) EndFreeMouse(false); // the window keeps the mouse free itself
        }
        if (buttons != null)
        {
            bool show = GameInput.TouchControls == false && !HelpScreen.IsOpen && !BookReader.IsOpen && !MapScreen.IsOpen;
            if (buttons.activeSelf != show) buttons.SetActive(show);
        }
    }

    void SetFreeMouse(bool on)
    {
        if (on)
        {
            freeMouse = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (look != null) look.enabled = false;
        }
        else EndFreeMouse(true);
    }

    void EndFreeMouse(bool lockAgain)
    {
        freeMouse = false;
        if (look != null) look.enabled = true;
        if (lockAgain)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void Press(GameInput.TouchAction action)
    {
        GameInput.PressTouch(action);
        GameInput.ReleaseTouch(action);
        if (freeMouse) EndFreeMouse(false);
    }

    void BuildButtons()
    {
        buttons = new GameObject("PC buttons (made in code)");
        buttons.transform.SetParent(transform, false);
        var canvas = buttons.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 70;
        var scaler = buttons.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        buttons.AddComponent<GraphicRaycaster>();
        var color = new Color(0.16f, 0.13f, 0.1f, 0.8f);
        void Add(string text, float x, UnityEngine.Events.UnityAction click)
        {
            var b = UISkin.NewButton(text, buttons.transform, UISkin.Pill, color, text, 24, Color.white, click);
            UISkin.Place((RectTransform)b.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(x, -18f), new Vector2(196f, 56f));
        }
        Add("Inventory  [I]", -642f, () => Press(GameInput.TouchAction.Inventory));
        Add("Crafting  [C]", -436f, () => Press(GameInput.TouchAction.Crafting));
        Add("Menu  [M]", -230f, () => Press(GameInput.TouchAction.Menu));
        Add("Help  [H]", -24f, HelpScreen.Toggle);
        Add("Map  [N]", -848f, MapScreen.Toggle);
        var hint = UISkin.NewText("Hint", buttons.transform, "Tab: free the mouse to click", 18, new Color(1f, 1f, 1f, 0.6f), TextAnchor.MiddleRight, false);
        UISkin.Place(hint.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -76f), new Vector2(500f, 26f));
    }

    System.Collections.IEnumerator AddMenuHelpButtonLater()
    {
        yield return null;
        yield return null;
        AddMenuHelpButton();
    }

    // "Help" on the game's menu screen (phone and PC): a copy of the menu's own "Settings" (the same big yellow
    // word), one line under it; "Quit" goes one line lower
    void AddMenuHelpButton()
    {
        var menu = MenuManager.Instance != null ? MenuManager.Instance.menu : null;
        if (menu == null || menu.transform.Find("HelpButton (made in code)") != null) return;
        Button settings = null, load = null, quit = null;
        foreach (var b in menu.GetComponentsInChildren<Button>(true))
        {
            var words = b.GetComponentInChildren<TMPro.TMP_Text>(true);
            string t = words != null ? words.text.Trim() : b.name;
            if (t.StartsWith("Settings")) settings = b;
            else if (t.StartsWith("Load")) load = b;
            else if (t.StartsWith("Quit")) quit = b;
        }
        if (settings != null && quit != null && settings.transform.parent == quit.transform.parent)
        {
            var help = Instantiate(settings.gameObject, settings.transform.parent);
            help.name = "HelpButton (made in code)";
            var words = help.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (words != null) words.text = "Help";
            var button = help.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent(); // (not the copied "open Settings")
            button.onClick.AddListener(HelpScreen.Open);
            var sr = (RectTransform)settings.transform;
            var qr = (RectTransform)quit.transform;
            var hr = (RectTransform)help.transform;
            float line = load != null ? ((RectTransform)load.transform).anchoredPosition.y - sr.anchoredPosition.y : 70f;
            float gap = sr.anchoredPosition.y - qr.anchoredPosition.y;
            hr.anchoredPosition = sr.anchoredPosition - new Vector2(0f, line);
            help.transform.SetSiblingIndex(settings.transform.GetSiblingIndex() + 1);
            // "Map" under "Help": the island with the player and where to go
            var mapButton = Instantiate(settings.gameObject, settings.transform.parent);
            mapButton.name = "MapButton (made in code)";
            var mapWords = mapButton.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (mapWords != null) mapWords.text = "Map";
            var mapClick = mapButton.GetComponent<Button>();
            mapClick.onClick = new Button.ButtonClickedEvent();
            mapClick.onClick.AddListener(MapScreen.Open);
            var mr = (RectTransform)mapButton.transform;
            mr.anchoredPosition = hr.anchoredPosition - new Vector2(0f, line);
            mapButton.transform.SetSiblingIndex(help.transform.GetSiblingIndex() + 1);
            qr.anchoredPosition = mr.anchoredPosition - new Vector2(0f, gap);
            return;
        }
        // (a menu without those words: a small button at the top)
        var pill = UISkin.NewButton("HelpButton (made in code)", menu.transform, UISkin.Pill, new Color(0.42f, 0.33f, 0.24f, 0.95f), "Help", 34, Color.white, HelpScreen.Open);
        UISkin.Place((RectTransform)pill.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(260f, 76f));
        var mapPill = UISkin.NewButton("MapButton (made in code)", menu.transform, UISkin.Pill, new Color(0.42f, 0.33f, 0.24f, 0.95f), "Map", 34, Color.white, MapScreen.Open);
        UISkin.Place((RectTransform)mapPill.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(260f, 76f));
    }
}
