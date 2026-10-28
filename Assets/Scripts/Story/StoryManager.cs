using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The story and its levels, made in code when the game scene loads (EnvironmentBootstrap).
// A new game: the player wakes up on the beach (Story Start) after the shipwreck; the story is told line by line
// on a black screen, then the levels begin, each with its goals in a panel at the top right:
//   Level 1  Make a fire         stones -> craft a campfire -> place it -> wood and hay -> light it
//   Level 2  Fresh water         find the river that comes down from the mountain -> drink from it
//   Level 3  Something to eat    pick 2 mushrooms in the forest -> eat them
//   Level 4  The first night     gather firewood (something strange is on the island) -> a burning fire
//                                when night falls -> stay by the fire until morning
//   Level 5  The mountain        the cave opens: explore the mountain -> find it (pitch dark: no going in)
//   Level 6  Into the dark       craft a torch -> hold it -> down to the end of the cave -> read the journal ->
//                                take the prayer book (without light in the cave the shadow kills)
//   Level 7  The prayer          at night, when the shadow comes, open the prayer book: it flees
//   Level 8  Iron                10 iron ore from the cave + a log -> the iron axe (any tree in 3 blows)
//   Level 9  Brother Ioan's canon  snares -> a male and a female rabbit alive -> a pen (foundation, walls, door wall,
//                                roof) -> 5 rabbits in it, door shut -> plant 10 trees -> the cross (5 planks), raised
//                                last on the top of the other mountain: the miracle, a new stream to the sea
//   Level 10 The raft (the last) hemp -> ropes; logs, planks, sticks, hay, cooked meat, rabbit hides and 2 live rabbits
//                                brought to the raft where the new stream meets the sea (many trips) -> it slides into
//                                the sea -> board it: the island gets smaller behind it, the end (new levels go before it)
// The save keeps the level and its goals (EnviromentData.story).
public class StoryManager : MonoBehaviour
{
    public static StoryManager Instance { get; private set; }

    [Tooltip("Hour of the day a new game starts")]
    public float startHour = 7f;
    [Tooltip("Letters per second of the story text")]
    public float lettersPerSecond = 32f;

    class Level
    {
        public string title;
        public string[] story;   // told when the level begins
        public string[] steps;
        public string hint;
        public string done;      // told when it is finished
    }

    static string[] IntroLines => Lang.Ro ? IntroLinesRo : IntroLinesEn; // (the language of the Settings)
    static readonly string[] IntroLinesEn =
    {
        "The storm came out of nowhere.",
        "For three nights the waves threw our ship around, until it broke apart on the rocks.",
        "I remember the cold water... the screams... and then nothing.",
        "When I opened my eyes, I was lying on the sand. Alone.",
        "The ship is gone. The others are gone. I have nothing but my hands.",
        "Night will come. First of all, I need a fire.",
    };

    static Level[] Levels => Lang.Ro ? LevelsRo : LevelsEn;
    static readonly Level[] LevelsEn =
    {
        new Level
        {
            title = "MAKE A FIRE",
            story = new string[0],
            steps = new[]
            {
                "Collect 6 stones",
                "Craft a campfire (Crafting > Survival)",
                "Place the campfire",
                "Find wood (sticks) and hay (pick dry grass)",
                "Light the fire (wood in the fire + 1 hay)",
            },
            hint = "Stones and branches lie on the beach and in the grass. Dry grass gives hay.",
            done = "The fire crackles. Its warmth gives me hope.",
        },
        new Level
        {
            title = "FRESH WATER",
            story = new[]
            {
                "My mouth is dry and the sea water is salty.",
                "I need fresh water. A river must come down from the mountain... maybe to the lake I saw.",
            },
            steps = new[]
            {
                "Find fresh water: the river or the lake",
                "Drink (look down at the water: Drink water)",
            },
            hint = "The river comes down from the mountain into the lake. The sea is salty: never drink it. After rain, puddles are fresh water too.",
            done = "Cold, clear water. I must remember where the river and the lake are.",
        },
        new Level
        {
            title = "SOMETHING TO EAT",
            story = new[]
            {
                "My stomach hurts with hunger.",
                "In the forest, in the shade of the trees, I saw mushrooms.",
            },
            steps = new[]
            {
                "Pick 2 mushrooms in the forest",
                "Eat them (inventory: double tap / right click)",
            },
            hint = "Porcini are the best. Berries and herbs help a little too.",
            done = "Not much, but it will keep me going.",
        },
        new Level
        {
            title = "THE FIRST NIGHT",
            story = new[]
            {
                "The sun is going down.",
                "Something is wrong on this island... Last night I saw a dark shape between the trees, watching me.",
                "It stayed away from the fire.",
                "I need much more wood to keep the fire burning all night long.",
            },
            steps = new[]
            {
                "Gather firewood: 3 logs or 8 sticks",
                "Have a burning fire when night falls",
                "Stay by your fire until morning",
            },
            hint = "Craft a stone axe (1 stone + 2 sticks) and chop trees for logs. Logs burn the longest. Don't let the fire go out!",
            done = "The sun rises. I made it through the night... but the shadow will come back.",
        },
        new Level
        {
            title = "THE MOUNTAIN",
            story = new[]
            {
                "Last night the earth shook.",
                "At dawn I heard rocks falling, up on the mountain. Something has opened up there.",
                "I must see what the mountain hides.",
            },
            steps = new[]
            {
                "Explore the mountain",
                "Find what the falling rocks opened",
            },
            hint = "Look around the slopes of the mountain: a column of light shines where the rocks fell.",
            done = "A cave! It is pitch dark in there, and something moves in the dark. I won't go in without light.",
        },
        new Level
        {
            title = "INTO THE DARK",
            story = new[]
            {
                "I need a light I can carry.",
                "A stick with dry grass tied around it will burn for a few hours. Maybe two, to be safe.",
            },
            steps = new[]
            {
                "Craft a torch (Crafting > Tools)",
                "Hold the torch in your hand (it lights up)",
                "Go down to the end of the cave",
                "Read the old journal",
                "Take the prayer book (on the stone before the cross)",
            },
            hint = "A torch burns about 6 hours in the hand. Never be in the cave without light: the shadow lives in the dark!",
            done = "Brother Ioan fought the same shadow. He left me his prayers.",
        },
        new Level
        {
            title = "THE PRAYER",
            story = new[]
            {
                "Brother Ioan wrote that the shadow cannot stand the prayer.",
                "Tonight, when it comes for me, I will see if he was right.",
            },
            steps = new[]
            {
                "Wait for the night",
                "When the shadow comes, open the prayer book",
            },
            hint = "Use the prayer book from the inventory or the quick bar when the shadow is near (away from your fire, without a torch).",
            done = "The shadow fled from the prayer. Now I have two ways against it: the fire and the prayer.",
        },
        new Level
        {
            title = "IRON",
            story = new[]
            {
                "To leave this island I will need a lot of wood. The stone axe is too slow.",
                "In the cave I saw rusty stones: iron ore! With a log for the handle it can become a real axe.",
            },
            steps = new[]
            {
                "Find 10 iron ore in the cave (take a torch!)",
                "Have a log",
                "Craft the iron axe (Crafting > Tools)",
            },
            hint = "The iron ore lies along the cave's tunnel. With the iron axe any tree falls in 3 blows.",
            done = "Now I can cut all the wood I need.",
        },
        new Level
        {
            title = "BROTHER IOAN'S CANON",
            story = new[]
            {
                "Among Brother Ioan's pages I found a canon, written in his own hand:",
                "\"Do not spoil what is beautiful on this island. Care for the creatures and they will multiply; plant trees for those who come after you.\"",
                "<b><color=#FFD780>\"You must make paradise out of everything you have.\"</color></b>",
                "\"And when your work is done, raise a cross on the other mountain.\"",
                "He kept it, and God kept him alive on this island for many years. With His help, I will keep it too.",
                "Rabbits multiply quickly: a male and a female meet, and a day later there are little ones. Hemp for the snares grows around the lake.",
            },
            steps = new[]
            {
                "Catch a male and a female rabbit alive (snare: Crafting > Survival)",
                "Build a pen: foundation, walls, door wall, roof (Construction)",
                "Shut them in the pen and wait: 5 rabbits in the pen",
                "Plant 10 trees (seeds from under the trees)",
                "Make the cross (Crafting > Construction: 5 planks)",
                "Last: raise the cross on the other mountain (Menu > Map)",
            },
            hint = "Empty hands take a caught rabbit alive, the axe kills it. Shut the pen's door: an open door lets them all out! Babies give nothing. The cross goes up last.",
            done = "Water springs from the rock under the cross and runs down to the sea! Now I understand where the other river came from: Brother Ioan raised a cross there too. He was a saint... and now I feel that I am protected as well.",
        },
        // the last level: keep it the last one (new levels go before it)
        new Level
        {
            title = "THE RAFT",
            story = new[]
            {
                "This island is not my home. Somewhere beyond the sea, people are waiting for me.",
                "I will build a raft where the new stream flows into the sea: logs, planks, rope, branches and hay.",
                "Food for the journey: cooked meat that will not go bad, and two live rabbits. Rabbit hides against the cold nights at sea.",
                "Hemp for the rope grows thickest around the lake. It will take many trips.",
            },
            steps = new[]
            {
                "Pick 18 hemp (most of it grows around the lake)",
                "Make 6 ropes (Crafting > Refine: 3 hemp each)",
                "Go to the raft, where the new stream flows into the sea (follow the light, Menu > Map)",
                "Bring it 12 logs, 8 planks, 6 ropes, 16 sticks, 10 hay, 5 cooked meat, 3 rabbit hides and 2 live rabbits",
                "Board the raft in the sea",
            },
            hint = "Look at the raft and click: what it needs and you carry goes onto it. Planks: Crafting > Refine (1 log = 2). Live rabbits: from your pen (a snare inside it) or from the snares.",
            done = "",
        },
    };

    // ---- the same story in Romanian (Settings > Language): the same levels, the same steps in the same order ----

    static readonly string[] IntroLinesRo =
    {
        "Furtuna a venit din senin.",
        "Trei nopți la rând valurile ne-au aruncat corabia încoace și încolo, până când s-a sfărâmat de stânci.",
        "Îmi amintesc apa rece... strigătele... și apoi nimic.",
        "Când am deschis ochii, zăceam pe nisip. Singur.",
        "Corabia s-a dus. Ceilalți s-au dus. Nu mai am nimic în afară de mâinile mele.",
        "Va veni noaptea. Înainte de toate, am nevoie de un foc.",
    };

    static readonly Level[] LevelsRo =
    {
        new Level
        {
            title = "FĂ UN FOC",
            story = new string[0],
            steps = new[]
            {
                "Strânge 6 pietre",
                "Fă un foc de tabără (Meșteșuguri > Supraviețuire)",
                "Așază focul de tabără",
                "Găsește lemne (bețe) și fân (smulge iarbă uscată)",
                "Aprinde focul (lemne în foc + 1 fân)",
            },
            hint = "Pietrele și crengile stau pe plajă și prin iarbă. Iarba uscată dă fân.",
            done = "Focul trosnește. Căldura lui îmi dă speranță.",
        },
        new Level
        {
            title = "APĂ DULCE",
            story = new[]
            {
                "Gura mi s-a uscat, iar apa mării e sărată.",
                "Am nevoie de apă dulce. Din munte trebuie să coboare un râu... poate spre lacul pe care l-am văzut.",
            },
            steps = new[]
            {
                "Găsește apă dulce: râul sau lacul",
                "Bea (privește în jos, la apă: Drink water)",
            },
            hint = "Râul coboară din munte în lac. Marea e sărată: nu bea niciodată din ea. După ploaie, și băltoacele sunt apă bună de băut.",
            done = "Apă rece și limpede. Trebuie să țin minte unde sunt râul și lacul.",
        },
        new Level
        {
            title = "CEVA DE MÂNCARE",
            story = new[]
            {
                "Mă doare stomacul de foame.",
                "În pădure, la umbra copacilor, am văzut ciuperci.",
            },
            steps = new[]
            {
                "Culege 2 ciuperci din pădure",
                "Mănâncă-le (inventar: dublu tap / clic dreapta)",
            },
            hint = "Hribii sunt cei mai buni. Fructele de pădure și ierburile ajută și ele puțin.",
            done = "Nu e mult, dar mă ține în picioare.",
        },
        new Level
        {
            title = "PRIMA NOAPTE",
            story = new[]
            {
                "Soarele apune.",
                "Ceva nu e în regulă pe insula asta... Azi-noapte am văzut o formă întunecată printre copaci, care mă privea.",
                "Stătea departe de foc.",
                "Am nevoie de mult mai multe lemne, ca focul să ardă toată noaptea.",
            },
            steps = new[]
            {
                "Strânge lemne de foc: 3 bușteni sau 8 bețe",
                "Să ai focul aprins când se lasă noaptea",
                "Rămâi lângă foc până dimineață",
            },
            hint = "Fă un topor de piatră (1 piatră + 2 bețe) și taie copaci pentru bușteni. Buștenii ard cel mai mult. Nu lăsa focul să se stingă!",
            done = "Răsare soarele. Am trecut de noapte... dar umbra se va întoarce.",
        },
        new Level
        {
            title = "MUNTELE",
            story = new[]
            {
                "Azi-noapte s-a cutremurat pământul.",
                "În zori am auzit pietre căzând, sus pe munte. Ceva s-a deschis acolo.",
                "Trebuie să văd ce ascunde muntele.",
            },
            steps = new[]
            {
                "Explorează muntele",
                "Găsește ce au deschis pietrele căzute",
            },
            hint = "Caută pe pantele muntelui: o coloană de lumină strălucește acolo unde au căzut pietrele.",
            done = "O peșteră! Înăuntru e beznă și ceva se mișcă în întuneric. Nu intru fără lumină.",
        },
        new Level
        {
            title = "ÎN ÎNTUNERIC",
            story = new[]
            {
                "Am nevoie de o lumină pe care s-o pot purta cu mine.",
                "Un băț cu iarbă uscată legată în jurul lui va arde câteva ore. Poate două, ca să fiu sigur.",
            },
            steps = new[]
            {
                "Fă o torță (Meșteșuguri > Unelte)",
                "Ține torța în mână (se aprinde)",
                "Coboară până la capătul peșterii",
                "Citește jurnalul vechi",
                "Ia cartea de rugăciuni (pe piatra din fața crucii)",
            },
            hint = "O torță arde cam 6 ore în mână. Nu sta niciodată în peșteră fără lumină: umbra trăiește în întuneric!",
            done = "Fratele Ioan s-a luptat cu aceeași umbră. Mi-a lăsat rugăciunile lui.",
        },
        new Level
        {
            title = "RUGĂCIUNEA",
            story = new[]
            {
                "Fratele Ioan a scris că umbra nu suportă rugăciunea.",
                "În noaptea asta, când va veni după mine, voi vedea dacă avea dreptate.",
            },
            steps = new[]
            {
                "Așteaptă noaptea",
                "Când vine umbra, deschide cartea de rugăciuni",
            },
            hint = "Folosește cartea de rugăciuni din inventar sau din bara rapidă când umbra e aproape (departe de foc, fără torță).",
            done = "Umbra a fugit de rugăciune. Acum am două arme împotriva ei: focul și rugăciunea.",
        },
        new Level
        {
            title = "FIERUL",
            story = new[]
            {
                "Ca să plec de pe insula asta, voi avea nevoie de mult lemn. Toporul de piatră e prea încet.",
                "În peșteră am văzut pietre ruginite: minereu de fier! Cu un buștean drept coadă poate deveni un topor adevărat.",
            },
            steps = new[]
            {
                "Găsește 10 minereuri de fier în peșteră (ia o torță!)",
                "Să ai un buștean",
                "Fă toporul de fier (Meșteșuguri > Unelte)",
            },
            hint = "Minereul de fier stă de-a lungul tunelului peșterii. Cu toporul de fier orice copac cade din 3 lovituri.",
            done = "Acum pot tăia tot lemnul de care am nevoie.",
        },
        new Level
        {
            title = "CANONUL FRATELUI IOAN",
            story = new[]
            {
                "Printre paginile fratelui Ioan am găsit un canon, scris cu mâna lui:",
                "„Nu strica ce este frumos pe insula aceasta. Îngrijește făpturile și ele se vor înmulți; sădește copaci pentru cei ce vin după tine.”",
                "<b><color=#FFD780>„Trebuie să faci rai din tot ce ai.”</color></b>",
                "„Iar când lucrul tău e gata, înalță o cruce pe celălalt munte.”",
                "El l-a ținut, și Dumnezeu l-a ținut în viață pe insula asta mulți ani. Cu ajutorul Lui, îl voi ține și eu.",
                "Iepurii se înmulțesc repede: un mascul și o femelă se întâlnesc, iar o zi mai târziu apar puii. Cânepa pentru lațuri crește în jurul lacului.",
            },
            steps = new[]
            {
                "Prinde vii un iepure mascul și o femelă (laț: Meșteșuguri > Supraviețuire)",
                "Construiește un țarc: fundație, pereți, perete cu ușă, acoperiș (Construcții)",
                "Închide-i în țarc și așteaptă: 5 iepuri în țarc",
                "Plantează 10 copaci (semințe de sub copaci)",
                "Fă crucea (Meșteșuguri > Construcții: 5 scânduri)",
                "La sfârșit: înalță crucea pe celălalt munte (Meniu > Hartă)",
            },
            hint = "Cu mâinile goale iei viu iepurele prins, cu toporul îl omori. Închide ușa țarcului: deschisă, îi lasă pe toți să fugă! Puii nu dau nimic. Crucea se pune ultima.",
            done = "Din stâncă, de sub cruce, izvorăște apă și curge până în mare! Acum înțeleg de unde a apărut celălalt râu: și acolo fratele Ioan a înălțat o cruce. A fost un sfânt... și acum simt că și eu voi fi ocrotit.",
        },
        new Level
        {
            title = "PLUTA",
            story = new[]
            {
                "Insula asta nu e casa mea. Undeva, dincolo de mare, oamenii mă așteaptă.",
                "Voi construi o plută acolo unde pârâul cel nou se varsă în mare: bușteni, scânduri, frânghie, crengi și fân.",
                "Hrană pentru drum: carne gătită, care să nu se strice, și doi iepuri vii. Blănuri de iepure pentru nopțile reci pe mare.",
                "Cânepa pentru frânghie crește cel mai deasă în jurul lacului. Vor fi multe drumuri.",
            },
            steps = new[]
            {
                "Culege 18 cânepă (cea mai multă crește în jurul lacului)",
                "Fă 6 frânghii (Meșteșuguri > Prelucrare: câte 3 cânepă)",
                "Mergi la plută, unde pârâul cel nou se varsă în mare (urmează lumina, Meniu > Hartă)",
                "Adu-i 12 bușteni, 8 scânduri, 6 frânghii, 16 bețe, 10 fân, 5 carne gătită, 3 blănuri de iepure și 2 iepuri vii",
                "Urcă pe plută, în mare",
            },
            hint = "Privește pluta și apasă: ce îi trebuie și ai la tine se pune pe ea. Scânduri: Meșteșuguri > Prelucrare (1 buștean = 2). Iepuri vii: din țarcul tău (un laț pus în el) sau din lațuri.",
            done = "",
        },
    };

    // for the Help screen
    public static int LevelCount => Levels.Length;
    public static string LevelTitle(int level) => Levels[Mathf.Clamp(level, 1, Levels.Length) - 1].title;
    public static string[] LevelSteps(int level) => Levels[Mathf.Clamp(level, 1, Levels.Length) - 1].steps;

    StoryData data;
    bool loading;
    bool modal;        // the story text is on the screen (the game waits)
    float checkTimer;
    float waitForLoad;

    // UI
    Canvas hudCanvas, overlayCanvas;
    RectTransform panel;
    Text panelTitle, panelHint;
    readonly List<Image> stepIcons = new List<Image>();
    readonly List<Text> stepTexts = new List<Text>();
    CanvasGroup bannerGroup;
    // the "LEVEL N" banner is on the screen (the clock hides under it)
    public bool BannerShowing => bannerGroup != null && bannerGroup.alpha > 0.01f;
    Text bannerTitle, bannerSub;
    CanvasGroup toastGroup;
    Text toastText;
    Coroutine bannerRoutine, toastRoutine;

    static readonly HashSet<string> Mushrooms = new HashSet<string> { "porcini", "redmushroom", "fieldmushroom" };

    public int CurrentLevel => data != null ? data.level : 0;
    CaveBeacon crossBeacon;

    void Awake()
    {
        Instance = this;
        // a saved game is being loaded (SceneSystem reads this flag a moment later)
        loading = PlayerPrefs.GetInt("GameSceneLoad", -1) > 0 || SceneSystem.PendingLoadSlot > 0;
        if (!loading) PlacePlayerAtStart();
    }

    void OnEnable()
    {
        FreshWater.Drank += OnDrank;
        FreshWater.Found += OnWaterFound;
        Vegetation.Picked += OnPicked;
        InventoryItem.Consumed += OnConsumed;
        BookReader.Opened += OnBookOpened;
        Raft.Boarded += OnRaftBoarded;
        RabbitPopulation.TakenAlive += OnTakenAlive;
        SeedPlanting.Planted += OnTreePlanted;
        Lang.Changed += RefreshPanel; // the goals in the other language at once
    }

    void OnDisable()
    {
        FreshWater.Drank -= OnDrank;
        FreshWater.Found -= OnWaterFound;
        Vegetation.Picked -= OnPicked;
        InventoryItem.Consumed -= OnConsumed;
        BookReader.Opened -= OnBookOpened;
        Raft.Boarded -= OnRaftBoarded;
        RabbitPopulation.TakenAlive -= OnTakenAlive;
        SeedPlanting.Planted -= OnTreePlanted;
        Lang.Changed -= RefreshPanel;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (modal) Time.timeScale = 1f;
    }

    // a new game: on the beach of the shipwreck, early in the morning
    void PlacePlayerAtStart()
    {
        var start = FindObjectOfType<StoryStart>();
        var player = FindObjectOfType<PlayerMovement>();
        if (start != null && player != null)
        {
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            Vector3 p = start.transform.position;
            p.y = Forest.GroundY(p) + 1.2f;
            player.transform.position = p;
            if (controller != null) controller.enabled = true;
            var look = player.GetComponent<MouseMovement>();
            if (look != null) look.SetLook(start.transform.eulerAngles.y, 8f);
        }
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.TimeOfDay = startHour / 24f;
    }

    void Start()
    {
        BuildUI();
        if (!loading)
        {
            data = new StoryData { version = 1, level = 1 };
            StartCoroutine(EmptyHands());
            StartCoroutine(Intro());
        }
        else
        {
            waitForLoad = 8f; // SaveManager calls SetData; if it never does, the story goes on from level 1
        }
    }

    // ---------------- save / load ---------------- //

    public StoryData GetData()
    {
        if (data == null) return new StoryData { version = StoryData.CurrentVersion, level = 1, introSeen = true };
        data.version = StoryData.CurrentVersion;
        if (Raft.Instance != null) data.raft = (int[])Raft.Instance.have.Clone();
        return data;
    }

    public void SetData(StoryData saved)
    {
        loading = false;
        data = saved != null && saved.version >= 1 ? saved : new StoryData { version = StoryData.CurrentVersion, level = 1, introSeen = true };
        // a save from before the rabbit pen (level 9): the raft (and the end) moved one level on
        if (data.version < 2 && data.level >= 9) data.level++;
        data.version = StoryData.CurrentVersion;
        data.introSeen = true;
        if (panel == null) BuildUI();
        RefreshPanel();
        UpdateCave();
        if (data.winterCame) StartWinter(false); // (the season is not in the save)
    }

    // ---------------- the first winter (a day after the canon) ---------------- //

    void UpdateWinter()
    {
        if (data == null || data.winterCame || data.winterInHours < 0f) return;
        data.winterInHours -= Time.deltaTime * DayNightSystem.GameHoursPerSecond;
        if (data.winterInHours <= 0f) StartWinter(true);
    }

    void StartWinter(bool now)
    {
        data.winterCame = true;
        data.winterInHours = 0f;
        var time = TimeManager.Instance;
        if (time != null && time.currentSeason != TimeManager.Season.Winter) time.SetSeason(TimeManager.Season.Winter);
        var weather = WeatherSystem.Instance;
        if (weather == null) return;
        if (now)
        {
            weather.BeginSnow(12f);
            ShowToast(Lang.T("Winter has come! Snow is falling on the island. Keep a fire burning: the nights are long and cold.",
                "A venit iarna! Ninge peste insulă. Ține focul aprins: nopțile sunt lungi și reci."), 9f);
        }
        else if (weather.SnowCover < 0.5f) weather.SetSnowCover(0.6f);
    }

    // ---------------- goals ---------------- //

    void Update()
    {
        // the goals panel and the banners hide under the full-screen pages (the map, the help, a book): they showed
        // through them and mixed with their words
        if (hudCanvas != null)
        {
            bool page = MapScreen.IsOpen || HelpScreen.IsOpen || BookReader.IsOpen;
            if (hudCanvas.enabled == page) hudCanvas.enabled = !page;
        }
        if (data == null)
        {
            if (loading && (waitForLoad -= Time.unscaledDeltaTime) <= 0f) SetData(null);
            return;
        }
        UpdatePanelVisibility();
        UpdateWinter();
        if (modal || !data.introSeen || data.level > Levels.Length) return;
        checkTimer -= Time.deltaTime;
        if (checkTimer > 0f) return;
        checkTimer = 0.5f;
        CheckGoals();
    }

    void CheckGoals()
    {
        var done = Status();
        int before = data.stepsDone;
        for (int i = 0; i < done.Length; i++) if (done[i]) data.stepsDone |= 1 << i;
        int all = (1 << done.Length) - 1;
        if (data.stepsDone != before)
        {
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.pickupItemSound);
            RefreshPanel();
        }
        if ((data.stepsDone & all) == all) StartCoroutine(FinishLevel());
    }

    bool StepDone(int i) => (data.stepsDone & (1 << i)) != 0;

    // which goals of the current level are done now (a later goal done means the earlier ones are too)
    bool[] Status()
    {
        var inventory = InventorySystem.Instance;
        int Count(string item) => inventory != null ? inventory.CountItem(item) : 0;
        var level = Levels[data.level - 1];
        var s = new bool[level.steps.Length];
        switch (data.level)
        {
            case 1:
            {
                var fires = PlacedFires();
                bool burning = false;
                foreach (var f in fires) if (f.IsBurning) burning = true;
                s[4] = burning;
                s[3] = s[4] || ((Count("stick") + Count("log") + Count("plank")) > 0 && Count("hay") > 0 && fires.Count > 0);
                s[2] = s[3] || fires.Count > 0;
                s[1] = s[2] || Count("Campfire") > 0;
                s[0] = s[1] || Count("stone") >= 6;
                break;
            }
            case 2:
                s[1] = data.drank;
                s[0] = s[1] || data.riverFound;
                break;
            case 3:
                s[1] = data.mushroomsEaten >= 2;
                s[0] = s[1] || data.mushroomsPicked >= 2 || CountMushrooms() >= 2;
                break;
            case 4:
            {
                float hour = DayNightSystem.Instance != null ? DayNightSystem.Instance.Hour : 12f;
                bool night = hour >= 20.5f || hour < 4.5f;
                bool burning = false;
                float fuel = 0f;
                foreach (var f in PlacedFires()) { if (f.IsBurning) burning = true; fuel += f.fuelMinutes; }
                int wood = Count("log") * 4 + Count("stick") + Count("plank") * 2;
                if (night && StepDone(1)) data.nightStarted = true;
                s[2] = data.nightStarted && hour >= 6f && hour < 12f && burning;
                s[1] = s[2] || StepDone(1) || ((hour >= 19f || hour < 5f) && burning);
                s[0] = s[1] || wood >= 12 || fuel >= 8f * 60f;
                break;
            }
            case 5:
            {
                var cave = Cave.Instance;
                Vector3 p = PlayerPosition();
                bool deep = false, inside = false, nearMouth = false;
                if (cave != null && cave.path.Count > 1)
                {
                    float d = cave.DistanceToPath(p, out float along);
                    inside = cave.PlayerInside || (d < cave.radius * 1.8f && along > 3f);
                    deep = inside && along > cave.Length - 30f;
                    Vector3 m = cave.Mouth - p; m.y = 0f;
                    nearMouth = m.magnitude < 70f;
                }
                s[1] = inside || (cave != null && Vector3.Distance(p, cave.Mouth) < 9f);
                s[0] = s[1] || nearMouth;
                break;
            }
            case 6:
            {
                var cave = Cave.Instance;
                Vector3 p = PlayerPosition();
                bool deep = false;
                if (cave != null && cave.path.Count > 1)
                {
                    float d = cave.DistanceToPath(p, out float along);
                    deep = d < cave.radius * 2.6f && along > cave.Length - 30f;
                }
                s[4] = Count("prayerbook") > 0;
                s[3] = s[4] || data.journalRead;
                s[2] = s[3] || deep;
                s[1] = s[2] || TorchLight.Burning;
                s[0] = s[1] || Count("torch") > 0;
                break;
            }
            case 7:
            {
                float hour = DayNightSystem.Instance != null ? DayNightSystem.Instance.Hour : 12f;
                bool shadow = NightShadow.Instance != null && NightShadow.Instance.IsHunting;
                s[1] = data.prayerOpened;
                s[0] = s[1] || shadow || hour >= 20.5f || hour < 4.5f;
                break;
            }
            case 8:
                s[2] = Count("ironaxe") > 0;
                s[1] = s[2] || Count("log") > 0;
                s[0] = s[2] || Count("ironore") >= 10;
                break;
            case 9:
            {
                // Brother Ioan's canon: rabbits in a pen, trees, and last the cross on the other mountain
                int penned = PennedRabbits();
                s[5] = data.crossPlaced;
                s[4] = s[5] || Count("cross") > 0;
                s[3] = data.treesPlanted >= CanonTrees;
                s[2] = penned >= CanonRabbits;
                s[1] = s[2] || PenBuilt();
                bool male = data.caughtMale || Count(RabbitPopulation.LiveMale) > 0;
                bool female = data.caughtFemale || Count(RabbitPopulation.LiveFemale) > 0;
                s[0] = s[2] || (male && female) || penned >= 2;
                break;
            }
            case 10:
            {
                var raft = Raft.Instance;
                if (raft == null) raft = Raft.MakeSite(data.raft);
                int ropes = Count("rope") + (raft != null ? raft.have[2] : 0);
                bool near = raft != null && Vector3.Distance(PlayerPosition(), raft.transform.position) < 14f;
                s[4] = raft != null && raft.IsBoarded;
                s[3] = s[4] || (raft != null && raft.IsComplete);
                s[2] = s[3] || near || (raft != null && raft.Started);
                s[1] = s[3] || ropes >= 6;
                s[0] = s[1] || Count("hemp") + ropes * 3 >= 18;
                break;
            }
        }
        return s;
    }

    // a pen: a foundation, at least 4 walls (one with a door) and a roof
    static bool PenBuilt()
    {
        int walls = Buildings.Count(BuildingKind.Wall) + Buildings.Count(BuildingKind.DoorWall) + Buildings.Count(BuildingKind.WindowWall);
        return Buildings.Count(BuildingKind.Foundation) > 0 && Buildings.Count(BuildingKind.DoorWall) > 0 && walls >= 4
            && Buildings.Count(BuildingKind.Roof) > 0;
    }

    public const int CanonRabbits = 5, CanonTrees = 10;

    // the most rabbits (any age) inside one pen whose doors are shut
    public static int PennedRabbits()
    {
        var population = RabbitPopulation.Instance;
        if (population == null || PenDoor.All.Count == 0) return 0;
        int most = 0;
        foreach (var door in PenDoor.All)
        {
            if (door == null) continue;
            Vector3 c = door.transform.position;
            if (PenDoor.AnyOpenNear(c, 16f)) continue;
            int n = 0;
            foreach (var r in population.Rabbits)
            {
                if (r == null || !r.Alive || r.caughtIn != null) continue;
                Vector3 d = r.transform.position - c; d.y = 0f;
                if (d.sqrMagnitude > 16f * 16f || !Buildings.IsEnclosed(r.transform.position)) continue;
                n++;
            }
            most = Mathf.Max(most, n);
        }
        return most;
    }

    // the canon: the cross goes up last (when the rabbits, the pen and the trees are done)
    public bool CanonReadyForCross => data != null && (data.level > 9 || (data.level == 9 && (data.stepsDone & 15) == 15));
    public bool CrossPlaced => data != null && data.crossPlaced;

    // CanonCross: the cross was raised on the other mountain: the miracle
    public void OnCrossRaised(Vector3 at, float yaw)
    {
        if (data == null) return;
        data.crossPlaced = true;
        data.crossPosition = at;
        data.crossYaw = yaw;
        if (MiracleStream.Instance != null) MiracleStream.Instance.Begin(at, true);
        ShowToast(Lang.T("Listen... water!", "Ascultă... apă!"), 6f);
    }

    // the Map (Menu > Map): where the current goal is, if it is a place
    public bool MapGoal(out Vector3 at, out string label)
    {
        at = Vector3.zero;
        label = "";
        if (data == null) return false;
        switch (data.level)
        {
            case 2:
                if (River.Instance != null && River.Instance.points.Count > 0)
                {
                    River.Instance.DistanceToWater(PlayerPosition(), out at);
                    label = Lang.T("Fresh water: the river", "Apă dulce: râul");
                    return true;
                }
                return false;
            case 5:
            case 6:
            case 8:
                if (Cave.Instance == null) return false;
                at = Cave.Instance.Mouth;
                label = data.level == 8 ? Lang.T("Iron ore: in the cave", "Minereu de fier: în peșteră") : Lang.T("The cave", "Peștera");
                return true;
            case 9:
                if (MiracleStream.Instance == null) return false;
                if (CanonReadyForCross && !data.crossPlaced)
                {
                    at = MiracleStream.Instance.Summit;
                    label = Lang.T("Raise the cross here", "Înalță crucea aici");
                    return true;
                }
                if (River.Instance != null && River.Instance.lakeRadius > 0f)
                {
                    at = River.Instance.lakeCentre;
                    label = Lang.T("Hemp for snares: around the lake", "Cânepă pentru lațuri: în jurul lacului");
                    return true;
                }
                return false;
            case 10:
                if (Raft.Instance == null) return false;
                at = Raft.Instance.transform.position;
                label = Lang.T("Build the raft here", "Construiește pluta aici");
                return true;
        }
        return false;
    }

    void OnTreePlanted()
    {
        if (data != null && data.level == 9) data.treesPlanted++;
    }

    void OnTakenAlive(RabbitInfo.Sex sex)
    {
        if (data == null) return;
        if (sex == RabbitInfo.Sex.Male) data.caughtMale = true; else data.caughtFemale = true;
    }

    static Vector3 PlayerPosition()
    {
        var state = PlayerState.Instance;
        if (state != null && state.playerBody != null) return state.playerBody.transform.position;
        return Camera.main != null ? Camera.main.transform.position : Vector3.zero;
    }

    // TEST (Settings > Test panel): finish the current level now and go to the next one
    public void SkipLevel()
    {
        if (data == null || modal || data.level > Levels.Length) return;
        data.introSeen = true;
        data.stepsDone = (1 << Levels[data.level - 1].steps.Length) - 1;
        if (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen) MenuManager.Instance.CloseMenu();
        StartCoroutine(FinishLevel());
    }

    // TEST: a few things to try the levels quickly
    public void GiveTestItems()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null) return;
        foreach (var item in new[] { "stone", "stone", "stone", "stone", "stone", "stone", "stick", "stick", "hay", "hay", "log", "porcini" })
        {
            if (!inventory.CheckSlotsAvailable(1)) break;
            inventory.AddToIventory(item, false, false);
        }
    }

    // the end of the game: the raft takes over (no more goals)
    void OnRaftBoarded()
    {
        if (data == null) return;
        data.level = Levels.Length + 1;
        modal = true;
        if (panel != null) panel.gameObject.SetActive(false);
    }

    void OnBookOpened(BookReader.Book book)
    {
        if (data == null) return;
        // the journal is found in Level 6; Level 7: the prayer book opened while the shadow comes
        if (book == BookReader.Book.Journal) { if (data.level == 6) data.journalRead = true; }
        else if (data.level == 7 && BookReader.DroveShadowAway) data.prayerOpened = true;
    }

    // the cave in the mountain opens with Level 5 (until then a fall of rocks closes it)
    void UpdateCave()
    {
        if (Cave.Instance != null && data != null) Cave.Instance.SetOpen(data.level >= 5);
        // the canon's cross on the other mountain and the stream it brought (also when level 9 was skipped: TEST)
        var stream = MiracleStream.Instance;
        // a light on the top of the other mountain while the cross is to be raised (canon ready, cross not up yet)
        if (data != null && stream != null && data.level == 9 && crossBeacon == null)
            crossBeacon = CaveBeacon.Make("Cross beacon (made in code)", stream.Summit, new Color(1f, 0.92f, 0.62f, 1f),
                () => data != null && data.level == 9 && !data.crossPlaced && CanonReadyForCross);
        if (data != null && stream != null && (data.crossPlaced || data.level > 9) && !stream.Flowing)
        {
            Vector3 at = data.crossPlaced ? data.crossPosition : stream.Summit;
            CanonCross.Show(at, data.crossYaw);
            stream.Begin(at, false);
        }
        // the last level: the raft, only started, lies on the beach by the wreck
        if (data != null && data.level == Levels.Length && Raft.Instance == null) Raft.MakeSite(data.raft);
    }

    int CountMushrooms()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null) return 0;
        int n = 0;
        foreach (var m in Mushrooms) n += inventory.CountItem(m);
        return n;
    }

    // campfires placed in the world (not the one still being placed)
    static List<Campfire> PlacedFires()
    {
        var list = new List<Campfire>();
        Transform holding = PlacementSystem.Instance != null && PlacementSystem.Instance.placementHoldingSpot != null
            ? PlacementSystem.Instance.placementHoldingSpot.transform : null;
        foreach (var f in FindObjectsOfType<Campfire>())
        {
            if (holding != null && f.transform.IsChildOf(holding)) continue;
            list.Add(f);
        }
        return list;
    }

    void OnDrank()
    {
        if (data == null) return;
        data.drank = true;
        data.riverFound = true;
    }

    void OnWaterFound(FreshWater.Source source)
    {
        if (data == null) return;
        bool first = !data.riverFound;
        data.riverFound = true;
        if (first) ShowToast(Lang.T("Fresh water! You can drink from the river and the lake, and from rain puddles. Never from the sea.", "Apă dulce! Poți bea din râu și din lac, și din băltoacele de după ploaie. Niciodată din mare."), 8f);
    }

    void OnPicked(string item)
    {
        if (data != null && Mushrooms.Contains(item)) data.mushroomsPicked++;
    }

    void OnConsumed(string item)
    {
        if (data != null && Mushrooms.Contains(item)) data.mushroomsEaten++;
    }

    IEnumerator FinishLevel()
    {
        var level = Levels[data.level - 1];
        int finished = data.level;
        data.level++;
        // after the canon: a day later the first winter comes (snow)
        if (finished == 9 && !data.winterCame && data.winterInHours < 0f) data.winterInHours = 24f;
        data.stepsDone = 0;
        modal = true; // no checks meanwhile
        RefreshPanel();
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();
        yield return Banner(Lang.T("LEVEL " + finished + " COMPLETE", "NIVELUL " + finished + " TERMINAT"), level.done, 4.5f);
        modal = false;
        if (data.level <= Levels.Length) yield return BeginLevel();
        else
        {
            yield return Banner(Lang.T("TO BE CONTINUED...", "VA URMA..."), Lang.T("More levels are coming. Survive as long as you can!", "Vin și alte niveluri. Supraviețuiește cât poți de mult!"), 5f);
            RefreshPanel();
        }
    }

    IEnumerator BeginLevel()
    {
        var level = Levels[data.level - 1];
        UpdateCave();
        if (level.story.Length > 0) yield return StoryText(level.story, false);
        yield return Banner(Lang.T("LEVEL ", "NIVELUL ") + data.level, level.title, 3.5f);
        RefreshPanel();
    }

    // A shipwreck: a new game starts with nothing (the scene's inventory and quick bar may hold things put there for
    // testing). The TEST panel in the Settings has "Give items" to try things quickly.
    IEnumerator EmptyHands()
    {
        yield return null; // after the inventory and the quick bar have found their slots
        var inventory = InventorySystem.Instance;
        if (inventory != null)
            foreach (var slot in inventory.slotList)
                if (slot != null) for (int i = slot.transform.childCount - 1; i >= 0; i--) DestroyImmediate(slot.transform.GetChild(i).gameObject);
        var equip = EquipSystem.Instance;
        if (equip != null)
        {
            if (equip.selectedItemModel != null) { DestroyImmediate(equip.selectedItemModel); equip.selectedItemModel = null; }
            equip.selectedItem = null;
            equip.selectedNumber = -1;
            foreach (var slot in equip.quickSlotsList)
                if (slot != null) for (int i = slot.transform.childCount - 1; i >= 0; i--) DestroyImmediate(slot.transform.GetChild(i).gameObject);
        }
        if (inventory != null) inventory.RecalculateList();
        if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        QuickBarUI.Changed();
    }

    // ---------------- the intro ---------------- //

    // The shipwreck story is told only the first time on this device: once it was seen (or there is a saved game),
    // a new game begins right away with Level 1.
    const string IntroSeenKey = "SurvivalBD.IntroSeen";

    static bool IntroSeenBefore()
    {
        if (PlayerPrefs.GetInt(IntroSeenKey, 0) == 1) return true;
        for (int i = 0; i <= 12; i++) if (PlayerPrefs.HasKey("Slot" + i + "Description")) return true;
        return false;
    }

    IEnumerator Intro()
    {
        if (!IntroSeenBefore())
        {
            yield return StoryText(IntroLines, true);
            PlayerPrefs.SetInt(IntroSeenKey, 1);
            PlayerPrefs.Save();
        }
        data.introSeen = true;
        yield return Banner(Lang.T("LEVEL 1", "NIVELUL 1"), Levels[0].title, 3.5f);
        RefreshPanel();
    }

    // Lines told one after the other on a dark screen (black for the intro); the game waits meanwhile.
    // Tap / click / Space shows the whole line, again goes to the next one; "Skip" ends it.
    // The story lines may have <b> (bold) or <color=...> in them: the letters come one by one, the tags never show.
    static int VisibleLength(string line)
    {
        int n = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (line[i] == '<') { int end = line.IndexOf('>', i); if (end > i) { i = end; continue; } }
            n++;
        }
        return n;
    }

    static string Typed(string line, int count)
    {
        var sb = new System.Text.StringBuilder();
        var open = new List<string>();
        int seen = 0;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '<')
            {
                int end = line.IndexOf('>', i);
                if (end > i)
                {
                    string tag = line.Substring(i, end - i + 1);
                    if (seen >= count && !tag.StartsWith("</")) break;
                    sb.Append(tag);
                    if (tag.StartsWith("</")) { if (open.Count > 0) open.RemoveAt(open.Count - 1); }
                    else open.Add(tag.Substring(1, tag.Length - 2).Split('=')[0]);
                    i = end;
                    continue;
                }
            }
            if (seen >= count) break;
            sb.Append(c);
            seen++;
        }
        for (int k = open.Count - 1; k >= 0; k--) sb.Append("</" + open[k] + ">");
        return sb.ToString();
    }

    IEnumerator StoryText(string[] lines, bool black)
    {
        modal = true;
        float oldScale = Time.timeScale;
        Time.timeScale = 0f;
        var oldLock = Cursor.lockState;
        bool oldVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = false;

        var root = UISkin.NewRect("Story text", overlayCanvas.transform);
        UISkin.Fill(root);
        var group = root.gameObject.AddComponent<CanvasGroup>();
        var back = UISkin.NewImage("Dark", root, UISkin.White, new Color(0f, 0f, 0f, black ? 1f : 0.72f), true);
        UISkin.Fill(back.rectTransform);
        var text = UISkin.NewText("Line", root, "", 40, new Color(0.95f, 0.92f, 0.85f), TextAnchor.MiddleCenter, false);
        UISkin.Centered(text.rectTransform, new Vector2(0f, 40f), new Vector2(1300f, 400f));
        text.lineSpacing = 1.15f;
        var help = UISkin.NewText("Help", root, GameInput.TouchControls ? Lang.T("Tap to continue", "Atinge ecranul ca să continui") : Lang.T("Click or press Space to continue", "Clic sau Space ca să continui"),
            24, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, false);
        UISkin.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 40f));
        bool skip = false;
        var skipButton = UISkin.NewButton("Skip", root, UISkin.Pill, new Color(1f, 1f, 1f, 0.18f), Lang.T("Skip", "Sari peste"), 26, Color.white, () => skip = true);
        UISkin.Place((RectTransform)skipButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-40f, -40f), new Vector2(160f, 64f));
        var tapArea = back.gameObject.AddComponent<Button>();
        tapArea.transition = Selectable.Transition.None;
        bool tapped = false;
        tapArea.onClick.AddListener(() => tapped = true);
        skipButton.transform.SetAsLastSibling();

        group.alpha = 0f;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.6f) { group.alpha = t; yield return null; }
        group.alpha = 1f;

        foreach (var line in lines)
        {
            if (skip) break;
            tapped = false;
            float shown = 0f;
            int letters = VisibleLength(line);
            while (shown < letters && !skip)
            {
                if (tapped || Advance()) { shown = letters; tapped = false; break; }
                shown += Time.unscaledDeltaTime * lettersPerSecond;
                text.text = Typed(line, Mathf.Min(letters, Mathf.FloorToInt(shown)));
                yield return null;
            }
            text.text = line;
            float wait = 1.6f + line.Length * 0.03f;
            while (wait > 0f && !skip)
            {
                if (tapped || Advance()) break;
                wait -= Time.unscaledDeltaTime;
                yield return null;
            }
            yield return null;
        }

        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / (black ? 1.6f : 0.5f)) { group.alpha = t; yield return null; }
        Destroy(root.gameObject);

        Time.timeScale = oldScale > 0f ? oldScale : 1f;
        bool windowOpen = (InventorySystem.Instance != null && InventorySystem.Instance.isOpen) || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen);
        if (!windowOpen)
        {
            // back to playing: on a PC the mouse turns the view again (the phone controls keep the cursor free)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = true;
        }
        else
        {
            Cursor.lockState = oldLock;
            Cursor.visible = oldVisible;
        }
        modal = false;
    }

    static bool Advance()
    {
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
    }

    // ---------------- UI ---------------- //

    void BuildUI()
    {
        if (panel != null) return;
        hudCanvas = NewCanvas("Story HUD (made in code)", 60);
        overlayCanvas = NewCanvas("Story screen (made in code)", 2000);

        // goals panel, top right (under the phone's menu buttons)
        bool phone = GameInput.TouchControls;
        panel = UISkin.NewRect("Goals", hudCanvas.transform);
        var background = panel.gameObject.AddComponent<Image>();
        background.sprite = UISkin.PlainPanel;
        background.type = Image.Type.Sliced;
        background.color = new Color(0.06f, 0.05f, 0.04f, 0.72f);
        background.raycastTarget = false;
        UISkin.Place(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, phone ? -136f : -120f), new Vector2(phone ? 560f : 500f, 200f));
        panelTitle = UISkin.NewText("Title", panel, "", phone ? 26 : 23, UISkin.Gold, TextAnchor.UpperLeft);
        panelHint = UISkin.NewText("Hint", panel, "", phone ? 19 : 17, UISkin.TextDim, TextAnchor.UpperLeft, false);
        panel.gameObject.SetActive(false);

        // the big title in the middle of the screen
        var banner = UISkin.NewRect("Banner", hudCanvas.transform);
        UISkin.Centered(banner, new Vector2(0f, 170f), new Vector2(1400f, 220f));
        bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
        bannerGroup.blocksRaycasts = false;
        bannerGroup.alpha = 0f;
        bannerTitle = UISkin.NewText("Title", banner, "", 64, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Place(bannerTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(1400f, 90f));
        bannerSub = UISkin.NewText("Subtitle", banner, "", 34, UISkin.TextColor, TextAnchor.UpperCenter);
        UISkin.Place(bannerSub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(1300f, 120f));

        // a message near the bottom (the river...)
        var toast = UISkin.NewImage("Message", hudCanvas.transform, UISkin.PlainPanel, new Color(0.05f, 0.08f, 0.1f, 0.8f), false);
        UISkin.Place(toast.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, phone ? 300f : 230f), new Vector2(980f, 84f));
        toastGroup = toast.gameObject.AddComponent<CanvasGroup>();
        toastGroup.blocksRaycasts = false;
        toastGroup.alpha = 0f;
        toastText = UISkin.NewText("Text", toast.transform, "", 26, new Color(0.75f, 0.9f, 1f), TextAnchor.MiddleCenter);
        UISkin.Fill(toastText.rectTransform, 14f);
    }

    Canvas NewCanvas(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    void RefreshPanel()
    {
        if (panel == null || data == null) return;
        bool phone = GameInput.TouchControls;
        float width = panel.sizeDelta.x;
        float y = -16f;
        if (data.level > Levels.Length)
        {
            panelTitle.text = Lang.T("FREE PLAY", "JOC LIBER");
            panelHint.text = Lang.T("All levels done. More levels are coming soon!", "Toate nivelurile sunt gata. Vin curând altele!");
            foreach (var t in stepTexts) t.gameObject.SetActive(false);
            foreach (var i in stepIcons) i.gameObject.SetActive(false);
            UISkin.Place(panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(width - 40f, 34f));
            UISkin.Place(panelHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y - 40f), new Vector2(width - 40f, 50f));
            panel.sizeDelta = new Vector2(width, 110f);
            return;
        }
        var level = Levels[data.level - 1];
        panelTitle.text = Lang.T("LEVEL ", "NIVELUL ") + data.level + "  " + level.title;
        UISkin.Place(panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(width - 40f, 34f));
        y -= phone ? 44f : 40f;
        int current = -1;
        for (int i = 0; i < level.steps.Length; i++) if (!StepDone(i)) { current = i; break; }
        float line = phone ? 36f : 32f;
        for (int i = 0; i < Mathf.Max(level.steps.Length, stepTexts.Count); i++)
        {
            if (i >= stepTexts.Count)
            {
                stepIcons.Add(UISkin.NewImage("Icon", panel, UISkin.Ring, Color.white, false));
                stepTexts.Add(UISkin.NewText("Step", panel, "", phone ? 21 : 19, UISkin.TextColor, TextAnchor.MiddleLeft, false));
            }
            bool used = i < level.steps.Length;
            stepIcons[i].gameObject.SetActive(used);
            stepTexts[i].gameObject.SetActive(used);
            if (!used) continue;
            bool done = StepDone(i);
            stepIcons[i].sprite = done ? UISkin.Disc : UISkin.Ring;
            stepIcons[i].color = done ? UISkin.Good : i == current ? UISkin.Gold : new Color(1f, 1f, 1f, 0.35f);
            stepTexts[i].text = level.steps[i];
            stepTexts[i].color = done ? new Color(0.6f, 0.85f, 0.55f) : i == current ? Color.white : UISkin.TextDim;
            UISkin.Place(stepIcons[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(22f, y - line * 0.5f), new Vector2(20f, 20f));
            // a long goal takes two lines (or more): the next one goes under it
            var stepText = stepTexts[i];
            stepText.horizontalOverflow = HorizontalWrapMode.Wrap;
            stepText.verticalOverflow = VerticalWrapMode.Overflow;
            UISkin.Place(stepText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, y), new Vector2(width - 70f, line));
            float tall = Mathf.Max(line, stepText.preferredHeight + 8f);
            stepText.alignment = tall > line ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            UISkin.Place(stepText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, tall > line ? y - 4f : y), new Vector2(width - 70f, tall));
            y -= tall;
        }
        panelHint.text = level.hint;
        panelHint.verticalOverflow = VerticalWrapMode.Overflow;
        UISkin.Place(panelHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y - 8f), new Vector2(width - 40f, 54f));
        float hintTall = Mathf.Max(54f, panelHint.preferredHeight + 4f);
        UISkin.Place(panelHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y - 8f), new Vector2(width - 40f, hintTall));
        y -= hintTall + 12f;
        panel.sizeDelta = new Vector2(width, -y);
    }

    // hidden while a window or the menu is open, and before the story has started
    void UpdatePanelVisibility()
    {
        bool windows = (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen)
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen);
        bool show = data != null && data.introSeen && !modal && !windows;
        if (panel != null && panel.gameObject.activeSelf != show) panel.gameObject.SetActive(show);
        // the "LEVEL N" banner and the messages wait behind the game's menu (they were drawn over Settings)
        bool menu = MenuManager.Instance != null && MenuManager.Instance.isMenuOpen;
        if (bannerGroup != null && bannerGroup.gameObject.activeSelf == menu) bannerGroup.gameObject.SetActive(!menu);
        if (toastGroup != null && toastGroup.gameObject.activeSelf == menu) toastGroup.gameObject.SetActive(!menu);
    }

    IEnumerator Banner(string title, string subtitle, float seconds)
    {
        bannerTitle.text = title;
        bannerSub.text = subtitle;
        for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / 0.5f) { bannerGroup.alpha = t; yield return null; }
        bannerGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.8f) { bannerGroup.alpha = t; yield return null; }
        bannerGroup.alpha = 0f;
    }

    public void ShowToast(string text, float seconds)
    {
        if (toastText == null) return;
        if (toastRoutine != null) StopCoroutine(toastRoutine);
        toastRoutine = StartCoroutine(Toast(text, seconds));
    }

    IEnumerator Toast(string text, float seconds)
    {
        toastText.text = text;
        for (float t = toastGroup.alpha; t < 1f; t += Time.unscaledDeltaTime / 0.3f) { toastGroup.alpha = t; yield return null; }
        toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(seconds);
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.6f) { toastGroup.alpha = t; yield return null; }
        toastGroup.alpha = 0f;
        toastRoutine = null;
    }
}
