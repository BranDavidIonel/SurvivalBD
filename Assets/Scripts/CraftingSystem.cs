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
        GameObject gO5 = new GameObject("WallBLP");
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
        SoundManager.Instance.PlaySound(SoundManager.Instance.craftingSound);
        StartCoroutine(craftedDelayForSound(blueprintToCraft));



        //remove resources from iventory
        if (blueprintToCraft.numeOfRequirements == 1)
        {
            InventorySystem.Instance.RemoveItem(blueprintToCraft.Req1, blueprintToCraft.Req1amount);
        }
        else if (blueprintToCraft.numeOfRequirements == 2)
        {
            InventorySystem.Instance.RemoveItem(blueprintToCraft.Req1, blueprintToCraft.Req1amount);
            InventorySystem.Instance.RemoveItem(blueprintToCraft.Req2, blueprintToCraft.Req2amount);
           
        }
        StartCoroutine(calculate());
    }
    public IEnumerator calculate()
    {
        yield return 0;// so there is no dealay
        InventorySystem.Instance.RecalculateList();
        RefreshNeededItems();

        //yield return new WaitForSeconds(1f);
        //InventorySystem.Instance.RecalculateList();
    }
    public IEnumerator craftedDelayForSound(Blueprint blueprintToCraft)
    {
        yield return new WaitForSeconds(1f);
        //add items into iventory
        for (var i = 0; i < blueprintToCraft.numberOfItemsToProduce; i++)
        {
            InventorySystem.Instance.AddToIventory(blueprintToCraft.itemName);
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C) && !isOpen && !ConstructionManager.Instance.inConstructionMode)
        {
            craftingScreenUI.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            SelectionManager.Instance.DisableSelection();
            SelectionManager.Instance.GetComponent<SelectionManager>().enabled = false;

            isOpen = true;

        }
        else if (Input.GetKeyDown(KeyCode.C) && isOpen)
        {
            craftingScreenUI.SetActive(false);
            toolsScreenUI.SetActive(false);
            survivalScreenUI.SetActive(false);
            refineScreenUI.SetActive(false);
            constructionScreenUI.SetActive(false);
            if (!InventorySystem.Instance.isOpen)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                SelectionManager.Instance.EnableSelection();
                SelectionManager.Instance.GetComponent<SelectionManager>().enabled = true;
            }
            isOpen = false;
        }
    }
    public void RefreshNeededItems()
    {
        int stoneCount = 0;
        int stickCount = 0;
        int logCount = 0;
        int plankCount = 0;
        inventoryItemList = InventorySystem.Instance.itemList;
        foreach(string itemName in inventoryItemList)
        {
            switch (itemName)
            {
                case "stone":
                    stoneCount+=1;
                    break;
                case "stick":
                    stickCount+=1;
                    break;
                case "log":
                    logCount += 1;
                    break;
                case "plank":
                    plankCount += 1;
                    break;
            }
        }
        // ------ Axe --------//
        AxeReq1.text = "3 Stone[" + stoneCount + "]";
     
        //Debug.Log("3 Stone[" + stoneCount + "]");
        AxeReq2.text = "3 Stone[" + stickCount + "]";
        if(stoneCount >= 3 && stickCount >= 3 && InventorySystem.Instance.CheckSlotsAvailable(1))
        {
            craftAxeBTN.gameObject.SetActive(true);
        }
        else
        {
            craftAxeBTN.gameObject.SetActive(false);
        }

        // ------ Plank X 2--------//
        PlankReq1.text = "1 Log[" + logCount + "]";
        if (logCount >= 1 && InventorySystem.Instance.CheckSlotsAvailable(2))
        {
            craftPlankBTN.gameObject.SetActive(true);
        }
        else
        {
            craftPlankBTN.gameObject.SetActive(false);
        }

        // ------ Foundation --------//
        FoundationReq1.text = "4 Plank[" + plankCount + "]";
        if (plankCount >= 4 && InventorySystem.Instance.CheckSlotsAvailable(1))
        {
            craftFoundationBTN.gameObject.SetActive(true);
        }
        else
        {
            craftFoundationBTN.gameObject.SetActive(false);
        }
        // ------ Wall --------//
        WallReq1.text = "2 Plank[" + plankCount + "]";
        if (plankCount >= 2 && InventorySystem.Instance.CheckSlotsAvailable(1))
        {
            craftWallBTN.gameObject.SetActive(true);
        }
        else
        {
            craftWallBTN.gameObject.SetActive(false);
        }

        // ------ Storage box small --------//
        StorageBoxReq1.text = "2 Plank[" + plankCount + "]";
        if (plankCount >= 2 && InventorySystem.Instance.CheckSlotsAvailable(1))
        {
            craftStorageBoxBTN.gameObject.SetActive(true);
        }
        else
        {
            craftStorageBoxBTN.gameObject.SetActive(false);
        }

    }
}
