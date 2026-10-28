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
    [Tooltip("Slots in the inventory: the scene has 18, the rest are made in code as copies of an empty one")]
    public int slotCount = 32;

    private void PopulateSlotList()
    {
        foreach (Transform child in inventoryScreenUI.transform)
        {
            if (child.CompareTag("Slot"))
            {
                slotList.Add(child.gameObject);
            }
        }
        // more room: copies of the last slot, without what lies in it (GameWindowsLayout puts them in the grid)
        if (slotList.Count == 0) return;
        var template = slotList[slotList.Count - 1];
        while (slotList.Count < slotCount)
        {
            var copy = Instantiate(template, template.transform.parent);
            copy.name = "Slot (" + slotList.Count + ")";
            for (int i = copy.transform.childCount - 1; i >= 0; i--) DestroyImmediate(copy.transform.GetChild(i).gameObject);
            slotList.Add(copy);
        }
        numberOfSlots = slotList.Count;
    }


    void Update()
    {

        bool toggle = GameInput.InventoryToggleDown; // I on PC, "Inventar" button on the phone
        if (toggle && !isOpen && !ConstructionManager.Instance.inConstructionMode && !CampfireUIManager.Instance.isUiOpen)
        {
            OpenUI();
        }
        else if (toggle && isOpen)
        {

            CloseUI();
        }
    }
    public void OpenUI()
    {
        if (ItemInfoUI != null) ItemInfoUI.SetActive(false); // no old item info from the last time
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
        bool otherWindowOpen = CraftingSystem.Instance.isOpen
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen);
        if (!otherWindowOpen)
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
    // false when the inventory is full (then nothing is added)
    public bool AddToIventory(string itemName, bool withSound = true, bool showPopup = true)
    {
        whatSlotToEquip = FindNextEmptySlot();
        GameObject prefab = Resources.Load<GameObject>(itemName);
        if (whatSlotToEquip == null || prefab == null)
        {
            if (whatSlotToEquip == null) ShowMessage(Lang.T("Inventory is full", "Inventarul e plin"));
            else Debug.LogWarning("No item prefab Resources/" + itemName);
            return false;
        }

        if (withSound && SoundManager.Instance != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.pickupItemSound);
        }

        itemToAdd = Instantiate(prefab, whatSlotToEquip.transform.position, whatSlotToEquip.transform.rotation);
        itemToAdd.transform.SetParent(whatSlotToEquip.transform);
        itemList.Add(itemName);

        Sprite spriteImageItem = itemToAdd.GetComponent<Image>().sprite;
        var info = itemToAdd.GetComponent<InventoryItem>();
        string shownName = info != null && !string.IsNullOrEmpty(info.thisName) ? info.thisName : itemName;
        if (showPopup) TriggerPickupPopUp("+1  " + Translations.Name(shownName), spriteImageItem);

        RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();
        return true;
    }

    // ---- the pickup message (top left): goes away by itself after a moment ---- //
    const float PopupSeconds = 2.5f;
    Coroutine hidePopup;

    void TriggerPickupPopUp(string itemName, Sprite itemSprite)
    {
        if (pickupAlert == null) return;
        pickupAlert.SetActive(true);
        pickupName.text = itemName;
        pickupImage.sprite = itemSprite;
        pickupImage.enabled = itemSprite != null;
        if (hidePopup != null) StopCoroutine(hidePopup);
        hidePopup = StartCoroutine(HidePopupLater());
    }

    // a short message in the same place (e.g. "Inventory is full")
    public void ShowMessage(string message)
    {
        TriggerPickupPopUp(message, null);
    }

    IEnumerator HidePopupLater()
    {
        yield return new WaitForSecondsRealtime(PopupSeconds);
        if (pickupAlert != null) pickupAlert.SetActive(false);
        hidePopup = null;
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
        return null;
    }

    public int EmptySlotCount()
    {
        int empty = 0;
        foreach (GameObject slot in slotList)
        {
            if (slot.transform.childCount == 0) empty++;
        }
        return empty;
    }

    // "stone(Clone)" / "stone" -> "stone" (items taken out of a storage box have no "(Clone)")
    public static string CleanName(string name)
    {
        return name.Replace("(Clone)", "").Trim();
    }

    // how many of this item are in the inventory and in the quick bar
    public int CountItem(string itemName)
    {
        if (string.IsNullOrEmpty(itemName)) return 0;
        int count = CountInInventory(itemName);
        if (EquipSystem.Instance != null) count += CountIn(EquipSystem.Instance.quickSlotsList, itemName, true);
        return count;
    }

    public int CountInInventory(string itemName)
    {
        return CountIn(slotList, itemName, false);
    }

    // Items that also count as another one for crafting: a pine log is a log too (planks...).
    static readonly Dictionary<string, string[]> Substitutes = new Dictionary<string, string[]>
    {
        { "log", new[] { "pinelog" } },
    };

    static bool Matches(string clean, string wanted)
    {
        if (clean == wanted) return true;
        return Substitutes.TryGetValue(wanted, out var others) && Array.IndexOf(others, clean) >= 0;
    }

    static int CountIn(List<GameObject> slots, string itemName, bool skipHeldItem)
    {
        int count = 0;
        foreach (GameObject slot in slots)
        {
            if (slot == null || slot.transform.childCount == 0) continue;
            Transform item = slot.transform.GetChild(0);
            if (!Matches(CleanName(item.name), itemName)) continue;
            if (skipHeldItem && IsHeld(item)) continue;
            count++;
        }
        return count;
    }

    // the item taken in the hand from the quick bar is never used up by crafting
    static bool IsHeld(Transform item)
    {
        var inventoryItem = item.GetComponent<InventoryItem>();
        return inventoryItem != null && inventoryItem.isSelected;
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

    // takes the items from the inventory first (last slots first), then from the quick bar (never the item in the hand)
    // (the item itself first, then what can stand in for it: logs before pine logs)
    public void RemoveItem(string nameToRemove,int amountToRemove)
    {
        var names = new List<string> { nameToRemove };
        if (Substitutes.TryGetValue(nameToRemove, out var others)) names.AddRange(others);
        int counter = amountToRemove;
        foreach (string name in names)
        {
            counter = RemoveFrom(slotList, name, counter, false);
            if (counter > 0 && EquipSystem.Instance != null)
            {
                counter = RemoveFrom(EquipSystem.Instance.quickSlotsList, name, counter, true);
            }
        }
        RecalculateList();
        CraftingSystem.Instance.RefreshNeededItems();
    }

    static int RemoveFrom(List<GameObject> slots, string nameToRemove, int counter, bool skipHeldItem)
    {
        for (var i = slots.Count - 1; i >= 0 && counter > 0; i--)
        {
            if (slots[i] == null || slots[i].transform.childCount == 0) continue;
            Transform item = slots[i].transform.GetChild(0);
            if (CleanName(item.name) != nameToRemove) continue;
            if (skipHeldItem && IsHeld(item)) continue;
            DestroyImmediate(item.gameObject);
            counter--;
        }
        return counter;
    }
    public void RecalculateList()
    {
        itemList.Clear();
        foreach(GameObject slot in slotList)
        {
            if (slot.transform.childCount > 0)
            {
                string name = slot.transform.GetChild(0).name;//Stone (Clone) or Stone (from a storage box)
                string findStr = "(Clone)";
                string result = name.Replace(findStr, "");
                itemList.Add(result);
            }
        }
    }
}
