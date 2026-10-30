using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EquipSystem : MonoBehaviour
{
    public static EquipSystem Instance { get; set; }

    // -- UI -- //
    public GameObject quickSlotsPanel;

    public List<GameObject> quickSlotsList = new List<GameObject>();

    public GameObject numbersHolder;

    public int selectedNumber = -1;
    public GameObject selectedItem;
    public GameObject toolHolder;

    public GameObject selectedItemModel;


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


    private void Start()
    {
        PopulateSlotList();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectQuickSlot(0);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectQuickSlot(1);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectQuickSlot(2);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            SelectQuickSlot(3);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            SelectQuickSlot(4);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            SelectQuickSlot(5);
        }
    }
    // tapping a quick slot on the phone (same as pressing its number key)
    public void SelectQuickSlotOf(GameObject slot)
    {
        int number = quickSlotsList.IndexOf(slot);
        if (number >= 0) SelectQuickSlot(number);
    }

    // the white number buttons of the quick bar (phone): take the item in the hand / put it away
    public void ToggleQuickSlot(int number)
    {
        if (number >= 0 && number < quickSlotsList.Count) SelectQuickSlot(number);
    }

    void SelectQuickSlot(int number)
    {
        if (checkIfSlotIsFull(number) == true)
        {
            // something that is used, not held (a pine cone, a campfire, a storage box, food): keys 1-6 use it,
            // like a tap on it on the phone (plant / place / eat)
            var slotItem = quickSlotsList[number].transform.GetChild(0).GetComponent<InventoryItem>();
            if (slotItem != null && (slotItem.isUseable || slotItem.isConsumable))
            {
                slotItem.UseFromQuickSlot();
                return;
            }

            // "already selected" only if the item in the hand really is the one in this slot
            bool alreadyHeld = selectedNumber == number && selectedItem != null
                && selectedItem.transform.parent == quickSlotsList[number].transform;
            if (!alreadyHeld)
            {

                selectedNumber = number;

                //unselect previously selected item
                if (selectedItem != null)
                {
                    selectedItem.gameObject.GetComponent<InventoryItem>().isSelected = false;
                }


                selectedItem = GetSelectedItem(number);
                selectedItem.GetComponent<InventoryItem>().isSelected = true;

                SetEquippedModel(selectedItem);

                // the quick bar shows the selected slot with a gold frame and a gold number button
                QuickBarUI.Changed();
            }
            else //we try to select the same slot
            {
                selectedNumber = -1;//null
                //unselect previously selected item
                if (selectedItem != null)
                {
                    selectedItem.gameObject.GetComponent<InventoryItem>().isSelected = false;
                    selectedItem = null;
                }

                if(selectedItemModel != null)
                {
                    DestroyImmediate(selectedItemModel.gameObject);
                    selectedItemModel = null;
                }

                QuickBarUI.Changed();
            }
        }
     
    }

    internal bool IsHoldingWeapon()
    {
        if(selectedItem != null)
        {
            if(selectedItem.GetComponent<Weapon>() != null)
            {
                return true;
            }
        }
        return false;
    }

    internal int GetWeaponDamage()
    {
        if(selectedItem != null)
        {
            return selectedItem.GetComponent<Weapon>().weaponDamage;
        }
        else
        {
            return 0;
        }
    }

    internal bool IsThereASwingLock()
    {
       if(selectedItemModel && selectedItemModel.GetComponent<EquipableItem>())
        {
            return selectedItemModel.GetComponent<EquipableItem>().swingWait; 
        }
        else
        {
            return false;
        }
    }

    private void SetEquippedModel(GameObject selectedItem)
    {
        if (selectedItemModel != null)
        {
            DestroyImmediate(selectedItemModel.gameObject);
            selectedItemModel = null;
        }

        string selectedItemName = selectedItem.name.Replace("(Clone)", "");
        if (Spear.IsSpear(selectedItemName))
        {
            // the spear is made in code: held, aimed at the "+" and thrown (SpearHeld)
            selectedItemModel = SpearHeld.Create(toolHolder.transform);
            return;
        }
        GameObject modelPrefab = Resources.Load<GameObject>(selectedItemName + "_Model");
        if (modelPrefab == null)
        {
            // e.g. a stick or a log dragged into a quick slot: there is nothing to hold in the hand
            Debug.LogWarning("No hand model Resources/" + selectedItemName + "_Model for the quick slot item");
            return;
        }
        selectedItemModel = Instantiate(modelPrefab,new Vector3(2f,0,1f),Quaternion.Euler(-50,-200f,2f));
        selectedItemModel.transform.SetParent(toolHolder.transform, false);
    }

    GameObject GetSelectedItem(int slotNumber)
    {
        return quickSlotsList[slotNumber].transform.GetChild(0).gameObject;
    }

    bool checkIfSlotIsFull(int slotNumber)
    {
        //Debug.Log(slotNumber);
        if(quickSlotsList[slotNumber].transform.childCount > 0)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    private void PopulateSlotList()
    {
        foreach (Transform child in quickSlotsPanel.transform)
        {
            if (child.CompareTag("QuickSlot"))
            {
                quickSlotsList.Add(child.gameObject);
            }
        }
    }

    public void AddToQuickSlots(GameObject itemToEquip)
    {
        // Find next free slot
        GameObject availableSlot = FindNextEmptySlot();
        // Set transform of our object
        itemToEquip.transform.SetParent(availableSlot.transform, false);
        // Getting clean name
        string cleanName = itemToEquip.name.Replace("(Clone)", "");

        InventorySystem.Instance.RecalculateList();

    }


    public GameObject FindNextEmptySlot()
    {
        foreach (GameObject slot in quickSlotsList)
        {
            if (slot.transform.childCount == 0)
            {
                return slot;
            }
        }
        return new GameObject();
    }

    public bool CheckIfFull()
    {

        int counter = 0;

        foreach (GameObject slot in quickSlotsList)
        {
            if (slot.transform.childCount > 0)
            {
                counter += 1;
            }
        }

        if (counter == 6)
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
