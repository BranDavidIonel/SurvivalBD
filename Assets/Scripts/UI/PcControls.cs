using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Buttons for the PC (made in code) that look and sit like the phone's: Inventory, Crafting and Menu at the top
// right (the same places as on the phone), Map and Help at the top left; each has its key written under the word,
// so nobody has to remember the keys. The mouse turns the camera while playing (it is locked): Tab or Esc frees it
// to click a button, a click on the game (or Tab / Esc again) takes it back. H opens the Help on any platform, and
// the game's menu gets "Help" and "Map" buttons too (phone and PC).
[DefaultExecutionOrder(-900)]
public class PcControls : MonoBehaviour
{
    GameObject buttons;
    GameObject inventoryButton, craftingButton, menuButton, mapButton, helpButton;
    Text inventoryWord, craftingWord, menuWord, mapWord, helpWord;
    Text inventoryKey, craftingKey, menuKey, mapKey, helpKey;
    GameObject hintBox;
    Text hint;
    bool freeMouse;
    bool windowsBefore;
    Lang.Language wordsIn = (Lang.Language)(-1);
    bool hintFree;
    MouseMovement look;
    PlayerDeath death;

    // the phone's shape (MobileControls / TouchButton): a dark pill with a white bold word; darker than on the phone,
    // so the white word reads well against the bright sky (the UI colours count as linear here: 0.07 already shows as
    // a mid grey, so the brown is given very dark)
    static readonly Color PillColor = new Color(0.016f, 0.012f, 0.009f, 0.92f);
    static readonly Vector2 PillSize = new Vector2(210f, 80f);
    const float PillY = -64f; // (a little higher than on the phone: the health bars are under them on narrow screens)

    void Start()
    {
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null) look = player.GetComponent<MouseMovement>();
        death = FindObjectOfType<PlayerDeath>();
        if (!GameInput.TouchControls) BuildButtons();
        StartCoroutine(AddMenuHelpButtonLater()); // after InGameLoadMenu has put "Load Game" in and moved the others
    }

    void Update()
    {
        bool touch = GameInput.TouchControls;
        // In the editor the phone simulator and the PC Game view can be switched while playing: each gets its own
        // controls (on a real phone or PC this never changes).
        if (touch && MobileControls.Instance == null) MobileControls.EnsureInThisScene();
        if (!touch && buttons == null) BuildButtons();

        // the phone controls turn button presses into "pressed this frame"; the PC buttons need it too
        // (twice in a frame does no harm)
        GameInput.BeginTouchFrame();

        if (Input.GetKeyDown(KeyCode.H)) HelpScreen.Toggle();
        if (Input.GetKeyDown(KeyCode.N) && !MapScreen.IsOpen) MapScreen.Open(); // (N again closes it: MapScreen)

        bool windows = AnyWindow();
        if (!touch)
        {
            // Tab or Esc: free the mouse to click the buttons (not while a window is open: Esc closes those)
            bool toggle = Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.Escape);
            if (toggle && !windows && !windowsBefore) SetFreeMouse(!freeMouse);
            else if (freeMouse && windows) EndFreeMouse(false); // the window keeps the mouse free itself
            else if (freeMouse && Input.GetMouseButtonDown(0) && !OverButton()) SetFreeMouse(false); // back to the game
        }
        windowsBefore = windows;

        if (buttons != null) ShowButtons(touch, windows);
    }

    bool AnyWindow()
    {
        return (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen)
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || HelpScreen.IsOpen || BookReader.IsOpen || MapScreen.IsOpen
            || (death != null && death.IsDead)
            || Time.timeScale == 0f; // (a story page waits for a click)
    }

    static bool OverButton()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
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

    // ---------------- the buttons ---------------- //

    void ShowButtons(bool touch, bool windows)
    {
        bool menuOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        bool storage = StorageManager.Instance != null && StorageManager.Instance.storageUIOpen;
        bool campfire = CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen;
        bool dead = death != null && death.IsDead;
        bool show = !touch && !HelpScreen.IsOpen && !BookReader.IsOpen && !MapScreen.IsOpen;
        Set(buttons, show);
        if (!show) return;
        // like the phone: in the menu only "Menu" stays (to go back to the game)
        bool other = !menuOpen && !dead && !storage && !campfire;
        Set(inventoryButton, other);
        Set(craftingButton, other);
        Set(mapButton, other);
        Set(helpButton, other);
        Set(menuButton, !dead && !storage && !campfire);
        Set(hintBox, !windows);

        if (wordsIn != Lang.Current)
        {
            wordsIn = Lang.Current;
            inventoryWord.text = Lang.T("Inventory", "Inventar");
            craftingWord.text = Lang.T("Crafting", "Meșteșuguri");
            menuWord.text = Lang.T("Menu", "Meniu");
            mapWord.text = Lang.T("Map", "Hartă");
            helpWord.text = Lang.T("Help", "Ajutor");
            string key = Lang.T("key ", "tasta ");
            inventoryKey.text = key + "I";
            craftingKey.text = key + "C";
            menuKey.text = key + "M";
            mapKey.text = key + "N";
            helpKey.text = key + "H";
            hintFree = !freeMouse; // (the hint below is written again too)
        }
        if (hintFree != freeMouse)
        {
            hintFree = freeMouse;
            hint.text = freeMouse
                ? Lang.T("Click a button.  A click on the game or Tab: look with the mouse again",
                         "Apasă un buton.  Clic în joc sau Tab: privești iar cu mouse-ul")
                : Lang.T("Tab or Esc: free the mouse to click these buttons",
                         "Tab sau Esc: eliberezi mouse-ul ca să apeși butoanele");
            hint.color = freeMouse ? UISkin.Gold : Color.white;
        }
    }

    static void Set(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active) target.SetActive(active);
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        buttons.AddComponent<GraphicRaycaster>();

        // top right, where the phone has them
        var right = new Vector2(1f, 1f);
        menuButton = Pill("Menu button", right, new Vector2(-130f, PillY), () => Press(GameInput.TouchAction.Menu), out menuWord, out menuKey);
        craftingButton = Pill("Crafting button", right, new Vector2(-360f, PillY), () => Press(GameInput.TouchAction.Crafting), out craftingWord, out craftingKey);
        inventoryButton = Pill("Inventory button", right, new Vector2(-590f, PillY), () => Press(GameInput.TouchAction.Inventory), out inventoryWord, out inventoryKey);
        // top left: the map and the help
        var left = new Vector2(0f, 1f);
        helpButton = Pill("Help button", left, new Vector2(130f, PillY), () => { if (freeMouse) EndFreeMouse(false); HelpScreen.Toggle(); }, out helpWord, out helpKey);
        mapButton = Pill("Map button", left, new Vector2(360f, PillY), () => { if (freeMouse) EndFreeMouse(false); MapScreen.Toggle(); }, out mapWord, out mapKey);

        // how to click them, under the map and the help
        var box = UISkin.NewImage("Mouse hint", buttons.transform, UISkin.Pill, new Color(0.016f, 0.012f, 0.009f, 0.85f), false);
        UISkin.Place(box.rectTransform, left, new Vector2(0f, 1f), new Vector2(25f, PillY - 52f), new Vector2(440f, 58f));
        hintBox = box.gameObject;
        hint = UISkin.NewText("Text", box.transform, "", 18, Color.white, TextAnchor.MiddleCenter);
        UISkin.Fill(hint.rectTransform, 10f);
        wordsIn = (Lang.Language)(-1);
    }

    GameObject Pill(string name, Vector2 anchor, Vector2 position, UnityEngine.Events.UnityAction click, out Text word, out Text key)
    {
        var b = UISkin.NewButton(name, buttons.transform, UISkin.Pill, PillColor, null, 28, Color.white, click);
        UISkin.Place((RectTransform)b.transform, anchor, new Vector2(0.5f, 0.5f), position, PillSize);
        word = UISkin.NewText("Word", b.transform, "", 28, Color.white, TextAnchor.MiddleCenter);
        UISkin.Place(word.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(PillSize.x - 12f, 36f));
        word.horizontalOverflow = HorizontalWrapMode.Overflow;
        word.GetComponent<Shadow>().effectDistance = new Vector2(2f, -2f);
        key = UISkin.NewText("Key", b.transform, "", 18, UISkin.Gold, TextAnchor.MiddleCenter, false);
        UISkin.Place(key.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -21f), new Vector2(PillSize.x - 12f, 24f));
        return b.gameObject;
    }

    // ---------------- the game's menu ---------------- //

    System.Collections.IEnumerator AddMenuHelpButtonLater()
    {
        yield return null;
        yield return null;
        AddMenuHelpButton();
    }

    // "Help" and "Map" on the game's menu screen (phone and PC): copies of the menu's own "Settings" (the same big
    // yellow word), under it; the six words are then spaced so "Quit" stays on the screen
    void AddMenuHelpButton()
    {
        var menu = MenuManager.Instance != null ? MenuManager.Instance.menu : null;
        if (menu == null || menu.transform.Find("HelpButton (made in code)") != null) return;
        Button settings = null, load = null, quit = null;
        // found by their names (their words may already be in Romanian: Translations), else by their English words
        foreach (var b in menu.GetComponentsInChildren<Button>(true))
        {
            var words = b.GetComponentInChildren<TMPro.TMP_Text>(true);
            string t = words != null ? words.text.Trim() : "";
            if (b.name == "SettingsButton" || (settings == null && t.StartsWith("Settings"))) settings = b;
            else if (b.name == "LoadGameButton" || (load == null && t.StartsWith("Load"))) load = b;
            else if (b.name == "ExitButton" || (quit == null && t.StartsWith("Quit"))) quit = b;
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
            var mapCopy = Instantiate(settings.gameObject, settings.transform.parent);
            mapCopy.name = "MapButton (made in code)";
            var mapWords = mapCopy.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (mapWords != null) mapWords.text = "Map";
            var mapClick = mapCopy.GetComponent<Button>();
            mapClick.onClick = new Button.ButtonClickedEvent();
            mapClick.onClick.AddListener(MapScreen.Open);
            var mr = (RectTransform)mapCopy.transform;
            mr.anchoredPosition = hr.anchoredPosition - new Vector2(0f, line);
            mapCopy.transform.SetSiblingIndex(help.transform.GetSiblingIndex() + 1);
            qr.anchoredPosition = mr.anchoredPosition - new Vector2(0f, gap);
            // six words now: closer together, so "Quit" stays on the screen (it fell below the bottom edge)
            var save = menu.transform.Find("SaveGameButton") as RectTransform;
            if (save != null)
            {
                RectTransform[] column = { save, load != null ? (RectTransform)load.transform : null, sr, hr, mr, qr };
                const float lowestQuit = -400f;  // (the menu's units: 540 is the bottom edge of the screen)
                float top = save.anchoredPosition.y;
                float step = Mathf.Min(Mathf.Abs(line), (top - lowestQuit) / (column.Length - 1));
                for (int i = 0; i < column.Length; i++)
                    if (column[i] != null) column[i].anchoredPosition = new Vector2(column[i].anchoredPosition.x, top - i * step);
            }
            return;
        }
        // (a menu without those words: a small button at the top)
        var pill = UISkin.NewButton("HelpButton (made in code)", menu.transform, UISkin.Pill, new Color(0.42f, 0.33f, 0.24f, 0.95f), "Help", 34, Color.white, HelpScreen.Open);
        UISkin.Place((RectTransform)pill.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(260f, 76f));
        var mapPill = UISkin.NewButton("MapButton (made in code)", menu.transform, UISkin.Pill, new Color(0.42f, 0.33f, 0.24f, 0.95f), "Map", 34, Color.white, MapScreen.Open);
        UISkin.Place((RectTransform)mapPill.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -126f), new Vector2(260f, 76f));
    }
}
