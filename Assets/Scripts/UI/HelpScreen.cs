using System.Text;
using UnityEngine;
using UnityEngine.UI;

// The Help screen (made in code): the controls on the PC and on the phone, the levels of the story (with the one
// the player is at), and tips for surviving. Open it with H, the "Help" button at the top right (PC) or "Help" in
// the game's menu (phone and PC). The game waits while it is open.
public class HelpScreen : MonoBehaviour
{
    public static HelpScreen Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

    GameObject root;
    Text body;
    Button[] tabs;
    int tab;
    float oldTimeScale = 1f;
    bool menuWasOpen;

    public static void Toggle()
    {
        if (IsOpen) Instance.Close();
        else Open();
    }

    public static void Open()
    {
        if (Instance == null) new GameObject("Help screen (made in code)").AddComponent<HelpScreen>();
        Instance.Show();
    }

    void Awake()
    {
        Instance = this;
        Build();
        root.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (root != null && root.activeSelf) Time.timeScale = 1f;
    }

    void Show()
    {
        if (root.activeSelf) return;
        oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        root.SetActive(true);
        menuWasOpen = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = false;
        // the levels when the story has started, the controls otherwise
        ShowTab(StoryManager.Instance != null && StoryManager.Instance.CurrentLevel > 1 ? 1 : 0);
    }

    void Close()
    {
        root.SetActive(false);
        Time.timeScale = oldTimeScale;
        bool windows = menuWasOpen || (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen);
        if (!windows)
        {
            if (!GameInput.TouchControls) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (SelectionManager.Instance != null) { SelectionManager.Instance.enabled = true; SelectionManager.Instance.EnableSelection(); }
        }
    }

    void Update()
    {
        if (!IsOpen) return;
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    void ShowTab(int index)
    {
        tab = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            var img = tabs[i].GetComponent<Image>();
            if (img != null) img.color = i == tab ? UISkin.SelectedGold : UISkin.ButtonBrown;
        }
        body.text = tab == 0 ? Controls() : tab == 1 ? Levels() : Tips();
    }

    static string Controls()
    {
        return
            "<b><color=#FFD780>On the PC</color></b>\n" +
            "W A S D  walk          Mouse  look          Space  jump (in water: up)          Left Ctrl  down in water\n" +
            "Left click  hit with the tool in your hand / take / open / drink / place\n" +
            "I  inventory          C  crafting          M  menu (save, load, settings)          H  this help\n" +
            "1 - 6  the quick bar: take a tool in your hand, eat, plant a seed, place a campfire\n" +
            "Right click on an item  use it / eat it / put it in the quick bar          X  cancel placing\n" +
            "Tab  free the mouse to click the buttons at the top right\n\n" +
            "<b><color=#FFD780>On the phone</color></b>\n" +
            "Left joystick  walk          Right joystick or swipe  look\n" +
            "The big button  Attack / Take / Open / Place (it changes with what you look at)\n" +
            "Jump (in water: Up) and Down          Inventory, Crafting, Menu  at the top right\n" +
            "Double tap an item  use it / eat it          Tap a quick slot  take it in your hand\n" +
            "Cancel  stop placing";
    }

    static string Levels()
    {
        var sb = new StringBuilder();
        int current = StoryManager.Instance != null ? StoryManager.Instance.CurrentLevel : 1;
        for (int i = 1; i <= StoryManager.LevelCount; i++)
        {
            string color = i < current ? "#94ED78" : i == current ? "#FFD780" : "#B4AA9A";
            sb.Append("<color=" + color + "><b>Level " + i + "  " + StoryManager.LevelTitle(i) + "</b>");
            if (i < current) sb.Append("   (done)");
            if (i == current) sb.Append("   (you are here)");
            sb.Append("</color>\n");
            if (i == current)
            {
                foreach (var step in StoryManager.LevelSteps(i)) sb.Append("      - " + step + "\n");
            }
        }
        if (current > StoryManager.LevelCount) sb.Append("\n<color=#94ED78>You left the island. The end!</color>");
        return sb.ToString();
    }

    static string Tips()
    {
        return
            "<b>Water</b>: a full water bar lasts one day. Drink from the river, the lake or rain puddles (2-3 days after rain). Never the sea!\n" +
            "<b>Food</b>: a full food bar lasts about three days. Mushrooms, berries, herbs, cooked meat. Empty bars kill you.\n" +
            "<b>Fire</b>: a campfire is 6 stones. To light it: wood in the fire and 1 hay (pick grass on the meadows).\n" +
            "<b>The shadow</b>: it comes at night when no fire burns near you. The prayer book drives it away for a while.\n" +
            "<b>The cave</b>: pitch dark. Go in only with a burning torch in your hand (6 hours each). Take a spare one.\n" +
            "<b>Trees</b>: seeds fall under them and grow into new trees after 2-3 days. Pick seeds and plant them.\n" +
            "<b>Rabbits</b>: males (blue) and females (pink) make babies. Hunting one gives meat and its hide.\n" +
            "<b>The raft</b> (the last level): it lies on the beach by the wreck. Bring it what it needs in as many trips as you like.\n" +
            "<b>Saving</b>: Menu > Save Game.";
    }

    void Build()
    {
        root = new GameObject("Help");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2050;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();

        var dark = UISkin.NewImage("Dark", root.transform, UISkin.White, new Color(0f, 0f, 0f, 0.75f), true);
        UISkin.Fill(dark.rectTransform);
        var panel = UISkin.NewImage("Panel", root.transform, UISkin.White, new Color(0.11f, 0.09f, 0.07f, 0.985f), true);
        UISkin.Centered(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1600f, 900f));
        var title = UISkin.NewText("Title", panel.transform, "HELP", 46, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(600f, 60f));

        string[] names = { "Controls", "Levels", "Tips" };
        tabs = new Button[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            int index = i;
            tabs[i] = UISkin.NewButton(names[i], panel.transform, UISkin.Pill, UISkin.ButtonBrown, names[i], 30, Color.white, () => ShowTab(index));
            UISkin.Place((RectTransform)tabs[i].transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1) * 270f, -95f), new Vector2(250f, 64f));
        }
        body = UISkin.NewText("Text", panel.transform, "", 27, UISkin.TextColor, TextAnchor.UpperLeft, false);
        UISkin.Place(body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -185f), new Vector2(1500f, 690f));
        body.lineSpacing = 1.15f;
        var close = UISkin.NewButton("Close", panel.transform, UISkin.Pill, new Color(0.55f, 0.12f, 0.1f), "X", 36, Color.white, Close);
        UISkin.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -18f), new Vector2(90f, 74f));
    }
}
