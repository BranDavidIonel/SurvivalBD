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
//   Level 9  The raft (the last) hemp -> ropes; logs, planks, sticks, hay brought to the raft on the beach (many
//                                trips) -> it slides into the sea -> board it:
//                                the island gets smaller behind it, the end (new levels go before this one)
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

    static readonly string[] IntroLines =
    {
        "The storm came out of nowhere.",
        "For three nights the waves threw our ship around, until it broke apart on the rocks.",
        "I remember the cold water... the screams... and then nothing.",
        "When I opened my eyes, I was lying on the sand. Alone.",
        "The ship is gone. The others are gone. I have nothing but my hands.",
        "Night will come. First of all, I need a fire.",
    };

    static readonly Level[] Levels =
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
            hint = "Look around the slopes of the mountain.",
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
                "Take the prayer book",
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
            hint = "Use the prayer book from the inventory or the quick bar when the shadow is near (away from your fire).",
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
        // the last level: keep it the last one (new levels go before it)
        new Level
        {
            title = "THE RAFT",
            story = new[]
            {
                "This island is not my home. Somewhere beyond the sea, people are waiting for me.",
                "I will build a raft on the beach, by the wreck: logs, planks, rope, branches and hay.",
                "Hemp for the rope grows on the slopes near the mountain. It will take many trips.",
            },
            steps = new[]
            {
                "Pick 18 hemp (on the slopes near the mountain)",
                "Make 6 ropes (Crafting > Refine: 3 hemp each)",
                "Go to the raft on the beach, by the wreck",
                "Bring it 12 logs, 8 planks, 6 ropes, 16 sticks, 10 hay",
                "Board the raft in the sea",
            },
            hint = "Look at the raft and click: what it needs and you carry goes onto it. Planks: Crafting > Refine (1 log = 2). The iron axe fells a tree in 3 blows.",
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
    Text bannerTitle, bannerSub;
    CanvasGroup toastGroup;
    Text toastText;
    Coroutine bannerRoutine, toastRoutine;

    static readonly HashSet<string> Mushrooms = new HashSet<string> { "porcini", "redmushroom", "fieldmushroom" };

    public int CurrentLevel => data != null ? data.level : 0;

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
    }

    void OnDisable()
    {
        FreshWater.Drank -= OnDrank;
        FreshWater.Found -= OnWaterFound;
        Vegetation.Picked -= OnPicked;
        InventoryItem.Consumed -= OnConsumed;
        BookReader.Opened -= OnBookOpened;
        Raft.Boarded -= OnRaftBoarded;
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
        if (data == null) return new StoryData { version = 1, level = 1, introSeen = true };
        data.version = 1;
        if (Raft.Instance != null) data.raft = (int[])Raft.Instance.have.Clone();
        return data;
    }

    public void SetData(StoryData saved)
    {
        loading = false;
        data = saved != null && saved.version >= 1 ? saved : new StoryData { version = 1, level = 1, introSeen = true };
        data.introSeen = true;
        if (panel == null) BuildUI();
        RefreshPanel();
        UpdateCave();
    }

    // ---------------- goals ---------------- //

    void Update()
    {
        if (data == null)
        {
            if (loading && (waitForLoad -= Time.unscaledDeltaTime) <= 0f) SetData(null);
            return;
        }
        UpdatePanelVisibility();
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
        if (first) ShowToast("Fresh water! You can drink from the river and the lake, and from rain puddles. Never from the sea.", 8f);
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
        data.stepsDone = 0;
        modal = true; // no checks meanwhile
        RefreshPanel();
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();
        yield return Banner("LEVEL " + finished + " COMPLETE", level.done, 4.5f);
        modal = false;
        if (data.level <= Levels.Length) yield return BeginLevel();
        else
        {
            yield return Banner("TO BE CONTINUED...", "More levels are coming. Survive as long as you can!", 5f);
            RefreshPanel();
        }
    }

    IEnumerator BeginLevel()
    {
        var level = Levels[data.level - 1];
        UpdateCave();
        if (level.story.Length > 0) yield return StoryText(level.story, false);
        yield return Banner("LEVEL " + data.level, level.title, 3.5f);
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

    IEnumerator Intro()
    {
        yield return StoryText(IntroLines, true);
        data.introSeen = true;
        yield return Banner("LEVEL 1", Levels[0].title, 3.5f);
        RefreshPanel();
    }

    // Lines told one after the other on a dark screen (black for the intro); the game waits meanwhile.
    // Tap / click / Space shows the whole line, again goes to the next one; "Skip" ends it.
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
        var help = UISkin.NewText("Help", root, GameInput.TouchControls ? "Tap to continue" : "Click or press Space to continue",
            24, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleCenter, false);
        UISkin.Place(help.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(900f, 40f));
        bool skip = false;
        var skipButton = UISkin.NewButton("Skip", root, UISkin.Pill, new Color(1f, 1f, 1f, 0.18f), "Skip", 26, Color.white, () => skip = true);
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
            while (shown < line.Length && !skip)
            {
                if (tapped || Advance()) { shown = line.Length; tapped = false; break; }
                shown += Time.unscaledDeltaTime * lettersPerSecond;
                text.text = line.Substring(0, Mathf.Min(line.Length, Mathf.FloorToInt(shown)));
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
        UISkin.Place(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-24f, phone ? -136f : -108f), new Vector2(phone ? 560f : 500f, 200f));
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
            panelTitle.text = "FREE PLAY";
            panelHint.text = "All levels done. More levels are coming soon!";
            foreach (var t in stepTexts) t.gameObject.SetActive(false);
            foreach (var i in stepIcons) i.gameObject.SetActive(false);
            UISkin.Place(panelTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y), new Vector2(width - 40f, 34f));
            UISkin.Place(panelHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y - 40f), new Vector2(width - 40f, 50f));
            panel.sizeDelta = new Vector2(width, 110f);
            return;
        }
        var level = Levels[data.level - 1];
        panelTitle.text = "LEVEL " + data.level + "  " + level.title;
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
            UISkin.Place(stepTexts[i].rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(52f, y - line * 0.5f), new Vector2(width - 70f, line));
            y -= line;
        }
        panelHint.text = level.hint;
        UISkin.Place(panelHint.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(20f, y - 8f), new Vector2(width - 40f, 54f));
        y -= 66f;
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
