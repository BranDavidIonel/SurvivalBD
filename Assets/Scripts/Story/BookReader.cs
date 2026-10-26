using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// An open book in front of the player (made in code): the monk's journal or his prayer book, found at the end of the
// cave (Level 5). Use the item from the inventory or the quick bar to open it; "<" ">" turn the pages, X closes it.
// The prayer book opens at a different prayer each time; opened at night it drives the shadow away for a while
// (with the fire, the second way to be safe from it).
public class BookReader : MonoBehaviour
{
    public enum Book { Journal, PrayerBook }

    public static BookReader Instance { get; private set; }
    public static bool IsOpen => Instance != null && Instance.root != null && Instance.root.activeSelf;

    // a book was opened (the story's goals listen to this)
    public static event System.Action<Book> Opened;

    // the last time the prayer book was opened, the shadow was coming (and it fled)
    public static bool DroveShadowAway { get; private set; }

    [Tooltip("Game hours the shadow keeps away after the prayer book was opened at night")]
    public static float prayerProtectionHours = 4f;

    struct Page
    {
        public string title, text;
        public bool cross;
        public Page(string title, string text, bool cross = false) { this.title = title; this.text = text; this.cross = cross; }
    }

    static Page[] JournalPages => Lang.Ro ? JournalPagesRo : JournalPagesEn; // (the language of the Settings)
    static readonly Page[] JournalPagesEn =
    {
        new Page("The journal of Brother Ioan", "In the year of our Lord 1894, the third day of the Great Fast.\n\nThe fishermen left me on this island, as I asked them. Here I will live in silence and prayer, far from the noise of the world."),
        new Page("The fortieth day", "The island is good to me. The river gives me water, the forest gives me mushrooms and berries, the oaks give me acorns.\n\nI keep a small fire and I pray the hours, as I did in the monastery."),
        new Page("At night", "Something walks between the trees at night. A shadow, tall and thin, with eyes like two cold stars.\n\nIt comes when the fire goes out. It waits at the edge of the light. I am afraid."),
        new Page("I have understood", "The shadow did not come with the night. It came with me.\n\nIt is made of my fears, of my sins, of all the darkness I carried here in my heart. The fire keeps it away from the body. Only prayer keeps it away from the soul."),
        new Page("Every man has one", "When I say the prayers, it cannot come near me. When I forget them, it comes back and waits.\n\nEvery man carries such a shadow. Most of them never see it. On this island, it can be seen."),
        new Page("The last page", "I am old and weak now. I go down into the mountain, to pray until the end.\n\nWhoever you are, you who read this: do not be afraid. Light a fire for your body and say the prayers for your soul. Take my book.\n\nLord, have mercy.\n                                        Brother Ioan"),
    };

    static readonly Page[] JournalPagesRo =
    {
        new Page("Jurnalul Fratelui Ioan", "În anul Domnului 1894, a treia zi a Postului Mare.\n\nPescarii m-au lăsat pe insula aceasta, așa cum i-am rugat. Aici voi trăi în liniște și rugăciune, departe de zgomotul lumii."),
        new Page("A patruzecea zi", "Insula e bună cu mine. Râul îmi dă apă, pădurea îmi dă ciuperci și fructe, stejarii îmi dau ghinde.\n\nȚin un foc mic și citesc Ceasurile, ca la mănăstire."),
        new Page("Noaptea", "Ceva umblă printre copaci noaptea. O umbră, înaltă și slabă, cu ochii ca două stele reci.\n\nVine când se stinge focul. Așteaptă la marginea luminii. Mi-e frică."),
        new Page("Am înțeles", "Umbra n-a venit odată cu noaptea. A venit odată cu mine.\n\nE făcută din fricile mele, din păcatele mele, din tot întunericul pe care l-am adus aici în inima mea. Focul o ține departe de trup. Numai rugăciunea o ține departe de suflet."),
        new Page("Fiecare om are una", "Când spun rugăciunile, nu se poate apropia de mine. Când le uit, se întoarce și așteaptă.\n\nFiecare om poartă o asemenea umbră. Cei mai mulți n-o văd niciodată. Pe insula aceasta, se vede."),
        new Page("Ultima pagină", "Sunt bătrân și slab acum. Cobor în munte, să mă rog până la sfârșit.\n\nOricine ai fi, tu care citești: nu te teme. Aprinde un foc pentru trupul tău și spune rugăciunile pentru sufletul tău. Ia cartea mea.\n\nDoamne, miluiește.\n                                        Fratele Ioan"),
    };

    GameObject root;
    Text leftTitle, leftText, rightTitle, rightText, pageNumber;
    GameObject leftCross, rightCross;
    Button previous, next;
    Page[] pages;
    int spread;          // the two pages shown: pages[spread * 2], pages[spread * 2 + 1]
    float oldTimeScale = 1f;

    public static void Open(Book book)
    {
        if (Instance == null) new GameObject("Book reader (made in code)").AddComponent<BookReader>();
        Instance.Show(book);
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
        if (IsOpenInternal) Time.timeScale = 1f;
    }

    bool IsOpenInternal => root != null && root.activeSelf;

    void Show(Book book)
    {
        if (book == Book.Journal)
        {
            pages = JournalPages;
            spread = 0;
        }
        else
        {
            // a different prayer first each time
            // the official prayers (Prayers.cs), in the language of the Settings
            var list = new List<Page>();
            foreach (var p in Prayers.All) list.Add(new Page(p.Title, p.Text, true));
            int first = Random.Range(0, list.Count);
            var ordered = new List<Page>();
            for (int i = 0; i < list.Count; i++) ordered.Add(list[(first + i) % list.Count]);
            pages = ordered.ToArray();
            spread = 0;
            // at night the prayer drives the shadow away
            bool night = SkyLighting.Instance != null ? SkyLighting.Instance.DayLight < 0.3f
                : DayNightSystem.Instance != null && (DayNightSystem.Instance.Hour >= 20f || DayNightSystem.Instance.Hour < 6f);
            DroveShadowAway = NightShadow.Instance != null && NightShadow.Instance.IsHunting;
            if (night && NightShadow.Instance != null) NightShadow.Instance.Banish(prayerProtectionHours);
        }
        if (!root.activeSelf)
        {
            oldTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            root.SetActive(true);
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = false;
        if (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) InventorySystem.Instance.CloseUI();
        Refresh();
        if (Opened != null) Opened(book);
    }

    void Close()
    {
        root.SetActive(false);
        Time.timeScale = oldTimeScale;
        if (!GameInput.TouchControls)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        if (SelectionManager.Instance != null) { SelectionManager.Instance.enabled = true; SelectionManager.Instance.EnableSelection(); }
    }

    void Update()
    {
        if (!IsOpenInternal) return;
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.X)) Close();
        else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Turn(1);
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Turn(-1);
    }

    void Turn(int step)
    {
        int spreads = (pages.Length + 1) / 2;
        spread = Mathf.Clamp(spread + step, 0, spreads - 1);
        Refresh();
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);
    }

    void Refresh()
    {
        int spreads = (pages.Length + 1) / 2;
        SetPage(spread * 2, leftTitle, leftText, leftCross);
        SetPage(spread * 2 + 1, rightTitle, rightText, rightCross);
        previous.interactable = spread > 0;
        next.interactable = spread < spreads - 1;
        pageNumber.text = (spread + 1) + " / " + spreads;
    }

    void SetPage(int index, Text title, Text text, GameObject cross)
    {
        bool has = index < pages.Length;
        title.text = has ? pages[index].title : "";
        text.text = has ? pages[index].text : "";
        cross.SetActive(has && pages[index].cross);
    }

    // ---------------- the book (UI) ---------------- //

    static readonly Color Paper = new Color(0.93f, 0.87f, 0.72f);
    static readonly Color Ink = new Color(0.22f, 0.14f, 0.08f);
    static readonly Color Red = new Color(0.55f, 0.08f, 0.06f);
    static readonly Color Leather = new Color(0.25f, 0.12f, 0.07f);

    void Build()
    {
        root = new GameObject("Book");
        root.transform.SetParent(transform, false);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2100;
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        root.AddComponent<GraphicRaycaster>();

        var dark = UISkin.NewImage("Dark", root.transform, UISkin.White, new Color(0f, 0f, 0f, 0.7f), true);
        UISkin.Fill(dark.rectTransform);

        var cover = UISkin.NewImage("Cover", root.transform, UISkin.PlainPanel, Leather, false);
        UISkin.Centered(cover.rectTransform, new Vector2(0f, 20f), new Vector2(1440f, 860f));
        float pageW = 680f, pageH = 800f;
        var left = UISkin.NewImage("Left page", cover.transform, UISkin.White, Paper, false);
        UISkin.Centered(left.rectTransform, new Vector2(-pageW * 0.5f - 6f, 0f), new Vector2(pageW, pageH));
        var right = UISkin.NewImage("Right page", cover.transform, UISkin.White, Paper, false);
        UISkin.Centered(right.rectTransform, new Vector2(pageW * 0.5f + 6f, 0f), new Vector2(pageW, pageH));
        var spine = UISkin.NewImage("Spine", cover.transform, UISkin.White, new Color(0.35f, 0.25f, 0.15f, 0.55f), false);
        UISkin.Centered(spine.rectTransform, Vector2.zero, new Vector2(14f, pageH));

        MakePage(left.transform, pageW, pageH, out leftTitle, out leftText, out leftCross);
        MakePage(right.transform, pageW, pageH, out rightTitle, out rightText, out rightCross);

        previous = UISkin.NewButton("Previous", root.transform, UISkin.Pill, UISkin.ButtonBrown, "<", 44, Color.white, () => Turn(-1));
        UISkin.Place((RectTransform)previous.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220f, 22f), new Vector2(160f, 76f));
        next = UISkin.NewButton("Next", root.transform, UISkin.Pill, UISkin.ButtonBrown, ">", 44, Color.white, () => Turn(1));
        UISkin.Place((RectTransform)next.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(220f, 22f), new Vector2(160f, 76f));
        pageNumber = UISkin.NewText("Page", root.transform, "", 28, UISkin.TextColor, TextAnchor.MiddleCenter);
        UISkin.Place(pageNumber.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(220f, 50f));
        var close = UISkin.NewButton("Close", root.transform, UISkin.Pill, new Color(0.55f, 0.12f, 0.1f), "X", 40, Color.white, Close);
        UISkin.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-36f, -30f), new Vector2(110f, 90f));
    }

    static void MakePage(Transform page, float w, float h, out Text title, out Text text, out GameObject cross)
    {
        cross = new GameObject("Cross", typeof(RectTransform));
        cross.transform.SetParent(page, false);
        UISkin.Place((RectTransform)cross.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(60f, 90f));
        // an Orthodox cross: a short bar on top, the long bar, a slanted bar at the foot
        Bar(cross.transform, new Vector2(0f, -45f), new Vector2(7f, 90f), 0f);
        Bar(cross.transform, new Vector2(0f, -14f), new Vector2(24f, 6f), 0f);
        Bar(cross.transform, new Vector2(0f, -30f), new Vector2(46f, 7f), 0f);
        Bar(cross.transform, new Vector2(0f, -70f), new Vector2(30f, 6f), -20f);

        title = UISkin.NewText("Title", page, "", 34, Red, TextAnchor.MiddleCenter);
        UISkin.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(w - 80f, 60f));
        title.fontStyle = FontStyle.BoldAndItalic;
        text = UISkin.NewText("Text", page, "", 29, Ink, TextAnchor.UpperLeft, false);
        UISkin.Place(text.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -210f), new Vector2(w - 100f, h - 250f));
        text.lineSpacing = 1.12f;
        // the long prayers (the Creed, St Basil...) get smaller letters, so they fit on the page
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = 29;
        title.resizeTextForBestFit = true;
        title.resizeTextMinSize = 20;
        title.resizeTextMaxSize = 34;
        var shadow = text.GetComponent<UnityEngine.UI.Shadow>();
        if (shadow != null) shadow.enabled = false;
        var titleShadow = title.GetComponent<UnityEngine.UI.Shadow>();
        if (titleShadow != null) titleShadow.enabled = false;
    }

    static void Bar(Transform parent, Vector2 position, Vector2 size, float angle)
    {
        var bar = UISkin.NewImage("Bar", parent, UISkin.White, Red, false);
        UISkin.Place(bar.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), position, size);
        bar.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }
}
