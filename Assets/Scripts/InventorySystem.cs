using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InventorySystem : MonoBehaviour
{
    public GameObject ItemInfoUI;
    public static InventorySystem Instance { get; set; }

    public GameObject inventoryScreenUI;
    public List<GameObject> slotList = new List<GameObject>();
    public List<string> itemList = new List<string>();
    public int numberOfSlots = 18;
    private GameObject itemToAdd;
    private GameObject whatSlotToEquip;
   

    public bool isOpen;

    //public bool isFull;

    //Pickup Popup
    public GameObject pickupAlert;
    public Text pickupName;
    public Image pickupImage;
    public Button pickupButtonClose;

    public List<string> itemsPickedup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }


    void Start()
    {
        isOpen = false;
        PopulateSlotList();
        Cursor.visible = false;
        pickupButtonClose.onClick.AddListener(delegate { ClosePickUpAlert(); });
    }
    private void PopulateSlotList()
    {
        foreach (Transform child in inventoryScreenUI.transform)
        {
            if (child.CompareTag("Slot"))
            {
                slotList.Add(child.gameObject);
            }
        }
    }


    void Update()
    {

        if (Input.GetKeyDown(KeyCode.I) && !isOpen && !ConstructionManager.Instance.inConstructionMode && !CampfireUIManager.Instance.isUiOpen)
        {
            OpenUI();
        }
        else if (Input.GetKeyDown(KeyCode.I) && isOpen)
        {

            CloseUI();
        }
    }
    public void OpenUI()
    {
        inventoryScreenUI.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SelectionManager.Instance.DisableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = false;

        isOpen = true;
    }

    public void CloseUI()
    {
        inventoryScreenUI.SetActive(false);
        if (!CraftingSystem.Instance.isOpen)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            SelectionManager.Instance.EnableSelection();
            SelectionManager.Instance.GetComponent<SelectionManager>().enabled = true;
        }
        isOpen = false;
    }




    void ClosePickUpAlert()
    {
        pickupAlert.SetActive(false);
    }
    public void AddToIventory(string itemName, bool withSound = true)
    {
        if (withSound)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.pickupItemSound);
        } 

        whatSlotToEquip = FindNextEmptySlot();
        Debug.Log("item name:"+itemName);
        itemToAdd = Instantiate(Resources.Load<GameObject>(itemName), whatSlotToEquip.transform.position, whatSlotToEquip.transform.rotation);
        itemToAdd.transform.SetParent(whatSlotToEquip.transform);
        itemList.Add(itemName);

        Sprite spriteImageItem = itemToAdd.GetComponent<Image>().sprite;
        TriggerPickupPopUp(itemName, spriteImageItem);

        RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();

    }

    void TriggerPickupPopUp(string itemName, Sprite itemSprite)
    {
        pickupAlert.SetActive(true);
        pickupName.text = itemName;
        pickupImage.sprite = itemSprite;
    }

    private GameObject FindNextEmptySlot()
    {
       foreach(GameObject slot in slotList)
        {
            if (slot.transform.childCount == 0)
            {
                return slot;
            }
           
        }
        return new GameObject();
    }

    public bool CheckSlotsAvailable(int emptyMeeded)
    {
        int emptySlots = 0;
        foreach (GameObject slot in slotList)
        {
            if (slot.transform.childCount <= 0)
            {
                emptySlots++;
            }


        }
        if (emptySlots >= emptyMeeded)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public void RemoveItem(string nameToRemove,int amountToRemove)
    {
        int counter = amountToRemove;
        for(var i = slotList.Count - 1; i >= 0; i--)
        {
            if (slotList[i].transform.childCount > 0)
            {
                if (slotList[i].transform.GetChild(0).name==nameToRemove+"(Clone)" && counter!=0)
                {
                    //Destroy(slotList[i].transform.GetChild(0).gameObject);
                    DestroyImmediate(slotList[i].transform.GetChild(0).gameObject);
                    counter--;
                }
            }
        }
        RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();
    }
    public void RecalculateList()
    {
        itemList.Clear();
        foreach(GameObject slot in slotList)
        {
            if (slot.transform.childCount > 0)
            {
                string name = slot.transform.GetChild(0).name;//Stone (Clone)
                string findStr = "(Clone)";
                string result = name.Replace(findStr, "");
                itemList.Add(result);
            }
        }
    }
}
