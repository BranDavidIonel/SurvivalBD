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

    Text helpTitle;

    void ShowTab(int index)
    {
        tab = index;
        for (int i = 0; i < tabs.Length; i++)
        {
            var img = tabs[i].GetComponent<Image>();
            if (img != null) img.color = i == tab ? UISkin.SelectedGold : UISkin.ButtonBrown;
        }
        // (in the language of the Settings)
        if (helpTitle != null) helpTitle.text = Lang.T("HELP", "AJUTOR");
        string[] names = { Lang.T("Controls", "Controale"), Lang.T("Levels", "Niveluri"), Lang.T("Tips", "Sfaturi") };
        for (int i = 0; i < tabs.Length && i < names.Length; i++) { var label = tabs[i].GetComponentInChildren<Text>(); if (label != null) label.text = names[i]; }
        body.text = tab == 0 ? Controls() : tab == 1 ? Levels() : Tips();
    }

    static string Controls()
    {
        if (Lang.Ro)
            return
                "<b><color=#FFD780>Pe PC</color></b>\n" +
                "W A S D  mers          Mouse  privit          Space  sărit (în apă: sus)          Left Ctrl  jos, în apă\n" +
                "Clic stânga  lovești cu unealta din mână / iei / deschizi / bei / așezi\n" +
                "I  inventar          C  meșteșuguri          M  meniu (salvare, încărcare, setări)          H  acest ajutor          N  harta\n" +
                "1 - 6  bara rapidă: iei o unealtă în mână, mănânci, plantezi o sămânță, așezi un foc\n" +
                "Clic dreapta pe un obiect  îl folosești / îl mănânci / îl pui în bara rapidă          X  anulezi așezarea\n" +
                "Butoanele de sus: Inventar, Meșteșuguri, Meniu (dreapta), Ajutor, Hartă (stânga)\n" +
                "Tab sau Esc  eliberezi mouse-ul ca să apeși pe ele          Clic în joc  privești iar cu mouse-ul\n" +
                "Tragi un obiect afară din fereastră  îl arunci pe jos (te întreabă înainte)\n\n" +
                "<b><color=#FFD780>Pe telefon</color></b>\n" +
                "Joystick stânga  mers          Joystick dreapta sau glisare  privit\n" +
                "Butonul mare  Lovește / Ia / Deschide / Așază (se schimbă după ce privești)\n" +
                "Sari (în apă: sus) și Jos          Inventar, Meșteșuguri, Meniu  în dreapta sus\n" +
                "Dublu tap pe un obiect  îl folosești / îl mănânci          Tap pe un loc din bara rapidă  îl iei în mână\n" +
                "Anulează  oprești așezarea";
        return
            "<b><color=#FFD780>On the PC</color></b>\n" +
            "W A S D  walk          Mouse  look          Space  jump (in water: up)          Left Ctrl  down in water\n" +
            "Left click  hit with the tool in your hand / take / open / drink / place\n" +
            "I  inventory          C  crafting          M  menu (save, load, settings)          H  this help          N  map\n" +
            "1 - 6  the quick bar: take a tool in your hand, eat, plant a seed, place a campfire\n" +
            "Right click on an item  use it / eat it / put it in the quick bar          X  cancel placing\n" +
            "The buttons at the top: Inventory, Crafting, Menu (right), Help, Map (left)\n" +
            "Tab or Esc  free the mouse to click them          Click on the game  look with the mouse again\n" +
            "Drag an item out of the window  drop it on the ground (you are asked first)\n\n" +
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
            sb.Append("<color=" + color + "><b>" + Lang.T("Level ", "Nivelul ") + i + "  " + StoryManager.LevelTitle(i) + "</b>");
            if (i < current) sb.Append(Lang.T("   (done)", "   (terminat)"));
            if (i == current) sb.Append(Lang.T("   (you are here)", "   (ești aici)"));
            sb.Append("</color>\n");
            if (i == current)
            {
                foreach (var step in StoryManager.LevelSteps(i)) sb.Append("      - " + step + "\n");
            }
        }
        if (current > StoryManager.LevelCount) sb.Append("\n<color=#94ED78>" + Lang.T("You left the island. The end!", "Ai plecat de pe insulă. Sfârșit!") + "</color>");
        return sb.ToString();
    }

    static string Tips()
    {
        if (Lang.Ro)
            return
                "<b>Apa</b>: bara de apă plină ține o zi. Bea din râu, din lac sau din băltoacele de după ploaie (2-3 zile). Niciodată din mare!\n" +
                "<b>Hrana</b>: bara de hrană plină ține cam trei zile. Ciuperci, fructe de pădure, ierburi, carne gătită. Barele goale te omoară.\n" +
                "<b>Focul</b>: un foc de tabără e din 6 pietre. Ca să-l aprinzi: lemne în foc și 1 fân (smulgi iarbă de pe pajiști).\n" +
                "<b>Umbra</b>: vine noaptea când nu arde niciun foc lângă tine. Focul, torța aprinsă în mână și cartea de rugăciuni o alungă.\n" +
                "<b>Peștera</b>: beznă totală. Intră doar cu o torță aprinsă în mână (6 ore fiecare). Ia una de rezervă.\n" +
                "<b>Copacii</b>: semințele cad sub ei și cresc copaci noi după 2-3 zile. Culege semințe și plantează-le.\n" +
                "<b>Iepurii</b>: masculii (albastru) și femelele (roz) fac 2-5 pui. Puii omorâți nu dau nimic: lasă-i să crească.\n" +
                "<b>Lațul</b> (1 băț + 5 cânepă; cânepa crește mai ales în jurul lacului): prinde iepuri mari. Cu mâinile goale îl iei viu,\n" +
                "      cu toporul îl omori. Dă-i drumul în țarc și <b>închide ușa</b>: o ușă lăsată deschisă îi lasă pe toți să fugă!\n" +
                "<b>Harta</b>: Meniu > Hartă (N pe PC). Săgeata roșie ești tu, cercul auriu e ținta. Coloanele de lumină arată drumul.\n" +
                "<b>Pluta</b> (ultimul nivel): unde pârâul cel nou se varsă în mare. Adu-i ce îi trebuie în câte drumuri vrei.\n" +
                "<b>Salvarea</b>: Meniu > Salvează. <b>Limba și dificultatea</b>: Meniu > Setări.";
        return
            "<b>Water</b>: a full water bar lasts one day. Drink from the river, the lake or rain puddles (2-3 days after rain). Never the sea!\n" +
            "<b>Food</b>: a full food bar lasts about three days. Mushrooms, berries, herbs, cooked meat. Empty bars kill you.\n" +
            "<b>Fire</b>: a campfire is 6 stones. To light it: wood in the fire and 1 hay (pick grass on the meadows).\n" +
            "<b>The shadow</b>: it comes at night when no fire burns near you. A fire, a burning torch in your hand and the prayer book drive it away.\n" +
            "<b>The cave</b>: pitch dark. Go in only with a burning torch in your hand (6 hours each). Take a spare one.\n" +
            "<b>Trees</b>: seeds fall under them and grow into new trees after 2-3 days. Pick seeds and plant them.\n" +
            "<b>Rabbits</b>: males (blue) and females (pink) make 2-5 babies. A killed baby gives nothing: let them grow.\n" +
            "<b>Snares</b> (1 stick + 5 hemp; hemp grows mostly around the lake) catch grown rabbits. Empty hands take it alive,\n" +
            "      the axe kills it. Let it go in your pen and <b>shut the door</b>: a door left open lets them all out!\n" +
            "<b>Map</b>: Menu > Map (N on the PC). The red arrow is you, the gold ring is the goal. Columns of light show the way.\n" +
            "<b>The raft</b> (the last level): where the new stream flows into the sea. Bring it what it needs in as many trips as you like.\n" +
            "<b>Saving</b>: Menu > Save Game. <b>Language and difficulty</b>: Menu > Settings.";
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

        var dark = UISkin.NewImage("Dark", root.transform, UISkin.White, new Color(0f, 0f, 0f, 0.95f), true); // (linear colours: it needs a high alpha to hide the game behind)
        UISkin.Fill(dark.rectTransform);
        var panel = UISkin.NewImage("Panel", root.transform, UISkin.White, new Color(0.11f, 0.09f, 0.07f, 0.985f), true);
        UISkin.Centered(panel.rectTransform, new Vector2(0f, 0f), new Vector2(1600f, 900f));
        var title = UISkin.NewText("Title", panel.transform, "HELP", 46, UISkin.Gold, TextAnchor.MiddleCenter);
        helpTitle = title;
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
