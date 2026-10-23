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
        AxeBLP.Req1amount = 3;
        AxeBLP.Req2 = "stick";
        AxeBLP.Req2amount = 3;

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
            label.text = can || !enough ? "Craft" : "Inventory full";
            label.color = can ? Color.white : new Color(1f, 1f, 1f, 0.55f);
        }
    }

    static readonly Color EnoughColor = new Color(0.58f, 0.93f, 0.47f);
    static readonly Color MissingColor = new Color(1f, 0.52f, 0.44f);

    static void SetRequirement(Text text, string item, int have, int need)
    {
        if (text == null) return;
        text.text = DisplayName(item) + "  " + Mathf.Min(have, 99) + " / " + need;
        text.color = have >= need ? EnoughColor : MissingColor;
    }

    static string DisplayName(string item)
    {
        if (string.IsNullOrEmpty(item)) return "";
        return char.ToUpper(item[0]) + item.Substring(1);
    }
}
