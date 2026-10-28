using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CraftingSystem : MonoBehaviour
{
    public GameObject craftingScreenUI;
    public GameObject toolsScreenUI, survivalScreenUI, refineScreenUI, constructionScreenUI;
    public List<string> inventoryItemList = new List<string>();

    //category buttons
    Button toolsBTN, survivalBTN, refineBTN, constructionBTN;
    //craft buttons
    Button craftAxeBTN, craftPlankBTN, craftFoundationBTN, craftWallBTN, craftStorageBoxBTN;
    //requirenebt text
    Text AxeReq1, AxeReq2, PlankReq1, FoundationReq1, WallReq1, StorageBoxReq1;
    public bool isOpen;
    //all blueprint
    public Blueprint AxeBLP;
    public Blueprint PlankBLP;
    public Blueprint FoundationBLP;
    public Blueprint WallBLP;
    public Blueprint StorageBoxBLP;
    public Blueprint CampfireBLP;
    Text CampfireReq1;
    Button craftCampfireBTN;
    public Blueprint TorchBLP;
    Text TorchReq1, TorchReq2;
    Button craftTorchBTN;
    //public Blueprint AxeBLP = new Blueprint("Axe", 1, 2, "stone", 3, "stick", 3);
    //public Blueprint PlankBLP = new Blueprint("Plank", 2, 1, "log", 1, "", 0);




    public static CraftingSystem Instance { get; set; }
    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
        // (some error about new)
        // STONE AXE
        GameObject gO1 = new GameObject("AxeBLP");
        AxeBLP = gO1.AddComponent<Blueprint>();
        AxeBLP.itemName = "Axe";
        AxeBLP.numberOfItemsToProduce = 1;
        AxeBLP.numeOfRequirements = 2;
        AxeBLP.Req1 = "stone";
        AxeBLP.Req1amount = 1;   // a primitive stone axe: one stone tied to two sticks
        AxeBLP.Req2 = "stick";
        AxeBLP.Req2amount = 2;

        // Plank
        GameObject gO2 = new GameObject("PlankBLP");
        PlankBLP = gO2.AddComponent<Blueprint>();
        PlankBLP.itemName = "Plank";
        PlankBLP.numberOfItemsToProduce = 2;
        PlankBLP.numeOfRequirements = 1;
        PlankBLP.Req1 = "log";
        PlankBLP.Req1amount = 1;
        PlankBLP.Req2 = "";
        PlankBLP.Req2amount = 0;

        //Foundation
        GameObject gO3 = new GameObject("FoundationBLP");
        FoundationBLP = gO3.AddComponent<Blueprint>();
        FoundationBLP.itemName = "Foundation";
        FoundationBLP.numberOfItemsToProduce = 1;
        FoundationBLP.numeOfRequirements = 1;
        FoundationBLP.Req1 = "plank";
        FoundationBLP.Req1amount = 4;
        FoundationBLP.Req2 = "";
        FoundationBLP.Req2amount = 0;

        //Wall
        GameObject gO4 = new GameObject("WallBLP");
        WallBLP = gO4.AddComponent<Blueprint>();
        WallBLP.itemName = "Wall";
        WallBLP.numberOfItemsToProduce = 1;
        WallBLP.numeOfRequirements = 1;
        WallBLP.Req1 = "plank";
        WallBLP.Req1amount = 2;
        WallBLP.Req2 = "";
        WallBLP.Req2amount = 0;

        //storage box small
        GameObject gO5 = new GameObject("StorageBoxBLP");
        StorageBoxBLP = gO5.AddComponent<Blueprint>();
        StorageBoxBLP.itemName = "StorageBox";
        StorageBoxBLP.numberOfItemsToProduce = 1;
        StorageBoxBLP.numeOfRequirements = 1;
        StorageBoxBLP.Req1 = "plank";
        StorageBoxBLP.Req1amount = 2;
        StorageBoxBLP.Req2 = "";
        StorageBoxBLP.Req2amount = 0;

        // torch: a stick with dry grass tied around it (it lights the dark cave)
        GameObject gO7 = new GameObject("TorchBLP");
        TorchBLP = gO7.AddComponent<Blueprint>();
        TorchBLP.itemName = "torch";
        TorchBLP.numberOfItemsToProduce = 1;
        TorchBLP.numeOfRequirements = 2;
        TorchBLP.Req1 = "stick";
        TorchBLP.Req1amount = 1;
        TorchBLP.Req2 = "hay";
        TorchBLP.Req2amount = 2;

        // campfire: only a ring of stones (to light it you need wood and hay: the campfire window)
        GameObject gO6 = new GameObject("CampfireBLP");
        CampfireBLP = gO6.AddComponent<Blueprint>();
        CampfireBLP.itemName = "Campfire";
        CampfireBLP.numberOfItemsToProduce = 1;
        CampfireBLP.numeOfRequirements = 1;
        CampfireBLP.Req1 = "stone";
        CampfireBLP.Req1amount = 6;
        CampfireBLP.Req2 = "";
        CampfireBLP.Req2amount = 0;


    }
    // Start is called before the first frame update
    void Start()
    {
        //Debug.Log("CraftingSystem start");
        isOpen = false;
        toolsBTN = craftingScreenUI.transform.Find("ToolsButton").GetComponent<Button>();
        toolsBTN.onClick.AddListener(delegate { OpenToolsCategory(); });

        survivalBTN = craftingScreenUI.transform.Find("SurvivalButton").GetComponent<Button>();
        survivalBTN.onClick.AddListener(delegate { OpenSurvivalCategory(); });

        refineBTN = craftingScreenUI.transform.Find("RefineButton").GetComponent<Button>();
        refineBTN.onClick.AddListener(delegate { OpenRefineCategory(); });

        constructionBTN = craftingScreenUI.transform.Find("ConstructionButton").GetComponent<Button>();
        constructionBTN.onClick.AddListener(delegate { OpenConstructionCategory(); });

        //Axe
        AxeReq1 = toolsScreenUI.transform.Find("Axe").transform.Find("req1").GetComponent<Text>();
        AxeReq2 = toolsScreenUI.transform.Find("Axe").transform.Find("req2").GetComponent<Text>();
        craftAxeBTN=toolsScreenUI.transform.Find("Axe").transform.Find("Button").GetComponent<Button>();
        craftAxeBTN.onClick.AddListener(delegate { CraftAnyItem(AxeBLP); });

        //Plank
        PlankReq1 = refineScreenUI.transform.Find("Plank").transform.Find("req1").GetComponent<Text>();
        craftPlankBTN = refineScreenUI.transform.Find("Plank").transform.Find("Button").GetComponent<Button>();
        craftPlankBTN.onClick.AddListener(delegate { CraftAnyItem(PlankBLP); });

        //Foundation
        FoundationReq1 = constructionScreenUI.transform.Find("Foundation").transform.Find("req1").GetComponent<Text>();
        craftFoundationBTN = constructionScreenUI.transform.Find("Foundation").transform.Find("Button").GetComponent<Button>();
        craftFoundationBTN.onClick.AddListener(delegate { CraftAnyItem(FoundationBLP); });

        //wall
        WallReq1 = constructionScreenUI.transform.Find("Wall").transform.Find("req1").GetComponent<Text>();
        craftWallBTN = constructionScreenUI.transform.Find("Wall").transform.Find("Button").GetComponent<Button>();
        craftWallBTN.onClick.AddListener(delegate { CraftAnyItem(WallBLP); });

        //Storage box
        StorageBoxReq1 = survivalScreenUI.transform.Find("StorageBox").transform.Find("req1").GetComponent<Text>();
        craftStorageBoxBTN = survivalScreenUI.transform.Find("StorageBox").transform.Find("Button").GetComponent<Button>();
        craftStorageBoxBTN.onClick.AddListener(delegate { CraftAnyItem(StorageBoxBLP); });

        // Campfire: a copy of the storage box card (made in code, the scene is not edited)
        var boxCard = survivalScreenUI.transform.Find("StorageBox");
        if (boxCard != null && survivalScreenUI.transform.Find("Campfire") == null)
        {
            var fireCard = Instantiate(boxCard.gameObject, boxCard.parent);
            fireCard.name = "Campfire";
            CampfireReq1 = fireCard.transform.Find("req1").GetComponent<Text>();
            var req2 = fireCard.transform.Find("req2");
            if (req2 != null) req2.gameObject.SetActive(false);
            craftCampfireBTN = fireCard.transform.Find("Button").GetComponent<Button>();
            craftCampfireBTN.onClick = new Button.ButtonClickedEvent();
            craftCampfireBTN.onClick.AddListener(delegate { CraftAnyItem(CampfireBLP); });
            var icon = fireCard.transform.Find("Image");
            var item = Resources.Load<GameObject>("Campfire");
            if (icon != null && item != null && icon.GetComponent<Image>() != null && item.GetComponent<Image>() != null)
                icon.GetComponent<Image>().sprite = item.GetComponent<Image>().sprite;
        }
        // Torch: a copy of the axe card
        var axeCard = toolsScreenUI.transform.Find("Axe");
        if (axeCard != null && toolsScreenUI.transform.Find("Torch") == null)
        {
            var torchCard = Instantiate(axeCard.gameObject, axeCard.parent);
            torchCard.name = "Torch";
            TorchReq1 = torchCard.transform.Find("req1").GetComponent<Text>();
            TorchReq2 = torchCard.transform.Find("req2").GetComponent<Text>();
            craftTorchBTN = torchCard.transform.Find("Button").GetComponent<Button>();
            craftTorchBTN.onClick = new Button.ButtonClickedEvent();
            craftTorchBTN.onClick.AddListener(delegate { CraftAnyItem(TorchBLP); });
            var icon = torchCard.transform.Find("Image");
            var item = Resources.Load<GameObject>("torch");
            if (icon != null && item != null && icon.GetComponent<Image>() != null && item.GetComponent<Image>() != null)
                icon.GetComponent<Image>().sprite = item.GetComponent<Image>().sprite;
        }
        // Iron axe (Tools): 10 iron ore from the cave and a log
        IronAxeBLP = NewBlueprint("IronAxeBLP", "ironaxe", "ironore", 10, "log", 1);
        CloneCard(toolsScreenUI.transform, "Axe", "IronAxe", IronAxeBLP, out IronAxeReq1, out IronAxeReq2, out craftIronAxeBTN);
        // Rope (Refine & Process): 3 hemp
        RopeBLP = NewBlueprint("RopeBLP", "rope", "hemp", 3, "", 0);
        CloneCard(refineScreenUI.transform, "Plank", "Rope", RopeBLP, out RopeReq1, out RopeReq2, out craftRopeBTN);

        // Snare (Survival): a stick pushed into the ground holds a loop of hemp cord (rabbits caught alive)
        SnareBLP = NewBlueprint("SnareBLP", "snare", "stick", 1, "hemp", 5);
        CloneCardInto(survivalScreenUI.transform, "Snare", SnareBLP, out SnareReq1, out SnareReq2, out craftSnareBTN);
        // the rabbit pen (Construction): a wall with a door (rope hinges), a wall with a window, a thatched roof
        DoorWallBLP = NewBlueprint("DoorWallBLP", "doorwall", "plank", 3, "rope", 1);
        CloneCardInto(constructionScreenUI.transform, "DoorWall", DoorWallBLP, out DoorWallReq1, out DoorWallReq2, out craftDoorWallBTN);
        WindowWallBLP = NewBlueprint("WindowWallBLP", "windowwall", "plank", 2, "stick", 2);
        CloneCardInto(constructionScreenUI.transform, "WindowWall", WindowWallBLP, out WindowWallReq1, out WindowWallReq2, out craftWindowWallBTN);
        RoofBLP = NewBlueprint("RoofBLP", "roof", "plank", 3, "hay", 4);
        CloneCardInto(constructionScreenUI.transform, "Roof", RoofBLP, out RoofReq1, out RoofReq2, out craftRoofBTN);
        // the cross of Brother Ioan's canon (level 9): 5 planks
        CrossBLP = NewBlueprint("CrossBLP", "cross", "plank", 5, "", 0);
        CloneCardInto(constructionScreenUI.transform, "Cross", CrossBLP, out CrossReq1, out CrossReq2, out craftCrossBTN);

        // the axe card shows the stone axe's picture
        var axeIcon = toolsScreenUI.transform.Find("Axe/Image");
        var axeItem = Resources.Load<GameObject>("Axe");
        if (axeIcon != null && axeItem != null && axeIcon.GetComponent<Image>() != null && axeItem.GetComponent<Image>() != null)
            axeIcon.GetComponent<Image>().sprite = axeItem.GetComponent<Image>().sprite;



    }


    // ---------------- recipes added in code (their cards are copies of the scene's cards) ---------------- //

    public Blueprint IronAxeBLP, RopeBLP, SnareBLP, DoorWallBLP, WindowWallBLP, RoofBLP, CrossBLP;
    Text IronAxeReq1, IronAxeReq2, RopeReq1, RopeReq2, SnareReq1, SnareReq2;
    Text DoorWallReq1, DoorWallReq2, WindowWallReq1, WindowWallReq2, RoofReq1, RoofReq2, CrossReq1, CrossReq2;
    Button craftIronAxeBTN, craftRopeBTN, craftSnareBTN, craftDoorWallBTN, craftWindowWallBTN, craftRoofBTN, craftCrossBTN;

    // a new card on any screen: a copy of the axe card (it has two requirement lines)
    void CloneCardInto(Transform screen, string name, Blueprint blueprint, out Text req1, out Text req2, out Button button)
    {
        req1 = req2 = null;
        button = null;
        var source = toolsScreenUI.transform.Find("Axe");
        if (source == null || screen.Find(name) != null) return;
        var card = Instantiate(source.gameObject, screen);
        card.name = name;
        req1 = card.transform.Find("req1").GetComponent<Text>();
        var r2 = card.transform.Find("req2");
        if (r2 != null)
        {
            r2.gameObject.SetActive(blueprint.numeOfRequirements >= 2);
            req2 = r2.GetComponent<Text>();
        }
        button = card.transform.Find("Button").GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(delegate { CraftAnyItem(blueprint); });
        SetCardIcon(card.transform, blueprint.itemName);
    }

    static Blueprint NewBlueprint(string name, string item, string req1, int amount1, string req2, int amount2)
    {
        var bp = new GameObject(name).AddComponent<Blueprint>();
        bp.itemName = item;
        bp.numberOfItemsToProduce = 1;
        bp.numeOfRequirements = string.IsNullOrEmpty(req2) ? 1 : 2;
        bp.Req1 = req1;
        bp.Req1amount = amount1;
        bp.Req2 = req2;
        bp.Req2amount = amount2;
        return bp;
    }

    void CloneCard(Transform screen, string from, string name, Blueprint blueprint, out Text req1, out Text req2, out Button button)
    {
        req1 = req2 = null;
        button = null;
        var source = screen.Find(from);
        if (source == null || screen.Find(name) != null) return;
        var card = Instantiate(source.gameObject, source.parent);
        card.name = name;
        req1 = card.transform.Find("req1").GetComponent<Text>();
        var r2 = card.transform.Find("req2");
        if (r2 != null)
        {
            r2.gameObject.SetActive(blueprint.numeOfRequirements >= 2);
            req2 = r2.GetComponent<Text>();
        }
        button = card.transform.Find("Button").GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(delegate { CraftAnyItem(blueprint); });
        SetCardIcon(card.transform, blueprint.itemName);
    }

    static void SetCardIcon(Transform card, string item)
    {
        var icon = card.Find("Image");
        var prefab = Resources.Load<GameObject>(item);
        if (icon != null && prefab != null && icon.GetComponent<Image>() != null && prefab.GetComponent<Image>() != null)
            icon.GetComponent<Image>().sprite = prefab.GetComponent<Image>().sprite;
    }

    void OpenToolsCategory()
    {
        craftingScreenUI.SetActive(false);
        toolsScreenUI.SetActive(true);
        survivalScreenUI.SetActive(false);
        refineScreenUI.SetActive(false);
        constructionScreenUI.SetActive(false);
    }

    void OpenSurvivalCategory()
    {
        craftingScreenUI.SetActive(false);
        toolsScreenUI.SetActive(false);
        survivalScreenUI.SetActive(true);
        refineScreenUI.SetActive(false);
        constructionScreenUI.SetActive(false);
    }

    void OpenRefineCategory()
    {
        craftingScreenUI.SetActive(false);
        toolsScreenUI.SetActive(false);
        survivalScreenUI.SetActive(false);
        refineScreenUI.SetActive(true);
        constructionScreenUI.SetActive(false);
    }
    void OpenConstructionCategory()
    {
        craftingScreenUI.SetActive(false);
        toolsScreenUI.SetActive(false);
        survivalScreenUI.SetActive(false);
        refineScreenUI.SetActive(false);
        constructionScreenUI.SetActive(true);
    }


    void CraftAnyItem(Blueprint blueprintToCraft)
    {
        // the button can be out of date (items moved since): check again before taking anything
        if (!CanCraft(blueprintToCraft))
        {
            RefreshNeededItems();
            return;
        }

        //remove resources from iventory (and from the quick bar if needed)
        InventorySystem.Instance.RemoveItem(blueprintToCraft.Req1, blueprintToCraft.Req1amount);
        if (blueprintToCraft.numeOfRequirements >= 2)
        {
            InventorySystem.Instance.RemoveItem(blueprintToCraft.Req2, blueprintToCraft.Req2amount);
        }

        //add the new items right away (before, they came 1 s later without checking for room and could get lost)
        for (var i = 0; i < blueprintToCraft.numberOfItemsToProduce; i++)
        {
            InventorySystem.Instance.AddToIventory(blueprintToCraft.itemName, false);
        }
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();

        InventorySystem.Instance.RecalculateList();
        RefreshNeededItems();
    }

    // enough materials (inventory + quick bar) and room in the inventory for what is made
    bool CanCraft(Blueprint blueprint)
    {
        var inventory = InventorySystem.Instance;
        if (inventory.CountItem(blueprint.Req1) < blueprint.Req1amount) return false;
        if (blueprint.numeOfRequirements >= 2 && inventory.CountItem(blueprint.Req2) < blueprint.Req2amount) return false;

        // the materials taken from the inventory free their slots
        int freed = Mathf.Min(blueprint.Req1amount, inventory.CountInInventory(blueprint.Req1));
        if (blueprint.numeOfRequirements >= 2) freed += Mathf.Min(blueprint.Req2amount, inventory.CountInInventory(blueprint.Req2));
        return inventory.EmptySlotCount() + freed >= blueprint.numberOfItemsToProduce;
    }

    // Update is called once per frame
    void Update()
    {
        bool toggle = GameInput.CraftingToggleDown; // C on PC, "Crafting" button on the phone
        if (toggle && !isOpen && !ConstructionManager.Instance.inConstructionMode && !OtherWindowOpen())
        {
            OpenUI();
        }
        else if (toggle && isOpen)
        {
            CloseUI();
        }
    }

    // the storage box and the campfire use the right side of the screen too
    static bool OtherWindowOpen()
    {
        return (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen);
    }

    public void OpenUI()
    {
        ShowCategories();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SelectionManager.Instance.DisableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = false;

        isOpen = true;
        InventorySystem.Instance.RecalculateList();
        RefreshNeededItems();
    }

    // the X button (and C / the Crafting button again)
    public void CloseUI()
    {
        craftingScreenUI.SetActive(false);
        toolsScreenUI.SetActive(false);
        survivalScreenUI.SetActive(false);
        refineScreenUI.SetActive(false);
        constructionScreenUI.SetActive(false);
        isOpen = false;
        if (!InventorySystem.Instance.isOpen && !OtherWindowOpen())
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SelectionManager.Instance.EnableSelection();
            SelectionManager.Instance.GetComponent<SelectionManager>().enabled = true;
        }
    }

    // the "< Back" button of a category: back to the 4 categories
    public void ShowCategories()
    {
        craftingScreenUI.SetActive(true);
        toolsScreenUI.SetActive(false);
        survivalScreenUI.SetActive(false);
        refineScreenUI.SetActive(false);
        constructionScreenUI.SetActive(false);
    }

    public void RefreshNeededItems()
    {
        if (AxeReq1 == null) return; // before Start
        inventoryItemList = InventorySystem.Instance.itemList;

        RefreshCard(AxeBLP, AxeReq1, AxeReq2, craftAxeBTN);
        RefreshCard(PlankBLP, PlankReq1, null, craftPlankBTN);
        RefreshCard(FoundationBLP, FoundationReq1, null, craftFoundationBTN);
        RefreshCard(WallBLP, WallReq1, null, craftWallBTN);
        RefreshCard(StorageBoxBLP, StorageBoxReq1, null, craftStorageBoxBTN);
        if (CampfireReq1 != null) RefreshCard(CampfireBLP, CampfireReq1, null, craftCampfireBTN);
        if (TorchReq1 != null) RefreshCard(TorchBLP, TorchReq1, TorchReq2, craftTorchBTN);
        if (IronAxeReq1 != null) RefreshCard(IronAxeBLP, IronAxeReq1, IronAxeReq2, craftIronAxeBTN);
        if (RopeReq1 != null) RefreshCard(RopeBLP, RopeReq1, RopeReq2, craftRopeBTN);
        if (SnareReq1 != null) RefreshCard(SnareBLP, SnareReq1, SnareReq2, craftSnareBTN);
        if (DoorWallReq1 != null) RefreshCard(DoorWallBLP, DoorWallReq1, DoorWallReq2, craftDoorWallBTN);
        if (WindowWallReq1 != null) RefreshCard(WindowWallBLP, WindowWallReq1, WindowWallReq2, craftWindowWallBTN);
        if (RoofReq1 != null) RefreshCard(RoofBLP, RoofReq1, RoofReq2, craftRoofBTN);
        if (CrossReq1 != null) RefreshCard(CrossBLP, CrossReq1, CrossReq2, craftCrossBTN);
    }

    // "Stone  2 / 3" in red until there is enough, then green; the Craft button greys out when it can't craft
    void RefreshCard(Blueprint blueprint, Text req1, Text req2, Button craftButton)
    {
        var inventory = InventorySystem.Instance;
        SetRequirement(req1, blueprint.Req1, inventory.CountItem(blueprint.Req1), blueprint.Req1amount);
        if (blueprint.numeOfRequirements >= 2) SetRequirement(req2, blueprint.Req2, inventory.CountItem(blueprint.Req2), blueprint.Req2amount);
        else if (req2 != null) req2.text = "";

        if (craftButton == null) return;
        bool can = CanCraft(blueprint);
        if (!craftButton.gameObject.activeSelf) craftButton.gameObject.SetActive(true);
        craftButton.interactable = can;
        var label = craftButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            bool enough = inventory.CountItem(blueprint.Req1) >= blueprint.Req1amount
                && (blueprint.numeOfRequirements < 2 || inventory.CountItem(blueprint.Req2) >= blueprint.Req2amount);
            label.text = can || !enough ? Lang.T("Craft", "Fă") : Lang.T("Inventory full", "Inventar plin");
            label.color = can ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    static readonly Color EnoughColor = new Color(0.58f, 0.93f, 0.47f);
    static readonly Color MissingColor = new Color(1f, 0.52f, 0.44f);

    static void SetRequirement(Text text, string item, int have, int need)
    {
        if (text == null) return;
        text.text = Translations.Name(DisplayName(item)) + "  " + Mathf.Min(have, 99) + " / " + need;
        text.color = have >= need ? EnoughColor : MissingColor;
    }

    // the item's own name ("ironore" -> "Iron Ore"), from its inventory prefab
    static readonly Dictionary<string, string> names = new Dictionary<string, string>();
    static string DisplayName(string item)
    {
        if (string.IsNullOrEmpty(item)) return "";
        if (!names.TryGetValue(item, out var shown))
        {
            var prefab = Resources.Load<GameObject>(item);
            var info = prefab != null ? prefab.GetComponent<InventoryItem>() : null;
            shown = info != null && !string.IsNullOrEmpty(info.thisName) ? info.thisName : char.ToUpper(item[0]) + item.Substring(1);
            if (item == "log") shown = "Log";
            names[item] = shown;
        }
        return shown;
    }
}
