using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StorageManager : MonoBehaviour
{
    public static StorageManager Instance { get; set; }

    [SerializeField] GameObject storageBoxSmallUI;
    [SerializeField] StorageBox selectedStorage;
    public bool storageUIOpen;

    // the storage box window (restyled by GameWindowsLayout)
    public GameObject StorageUI => storageBoxSmallUI;

    // the box opens the inventory next to it (so items can be moved between them, also on the phone)
    bool inventoryOpenedByBox;

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

    public void OpenBox(StorageBox storage)
    {
        // the crafting window uses the same side of the screen
        if (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen) CraftingSystem.Instance.CloseUI();

        SetSelectedStorage(storage);

        PopulateStorage(GetRelevantUI(selectedStorage));

        GetRelevantUI(selectedStorage).SetActive(true);
        storageUIOpen = true;

        inventoryOpenedByBox = false;
        if (InventorySystem.Instance != null && !InventorySystem.Instance.isOpen)
        {
            InventorySystem.Instance.OpenUI();
            inventoryOpenedByBox = true;
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SelectionManager.Instance.DisableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = false;
    }

    // only the slots of the window (not its title or its X button)
    static List<GameObject> SlotsOf(GameObject storageUI)
    {
        List<GameObject> uiSlots = new List<GameObject>();
        foreach (Transform child in storageUI.transform)
        {
            if (child.GetComponent<ItemSlot>() != null) uiSlots.Add(child.gameObject);
        }
        return uiSlots;
    }

    private void PopulateStorage(GameObject storageUI)
    {
        // Get all slots of the ui
        List<GameObject> uiSlots = SlotsOf(storageUI);

        // Now, instantiate the prefab and set it as a child of each GameObject
        foreach (string name in selectedStorage.items)
        {
            foreach (GameObject slot in uiSlots)
            {
                if (slot.transform.childCount < 1)
                {
                    var itemToAdd = Instantiate(Resources.Load<GameObject>(name), slot.transform.position, slot.transform.rotation);

                    itemToAdd.name = name;

                    itemToAdd.transform.SetParent(slot.transform);
                    break;
                }
            }
        }
    }

    public void CloseBox()
    {
        RecalculateStorage(GetRelevantUI(selectedStorage));

        GetRelevantUI(selectedStorage).SetActive(false);
        storageUIOpen = false;

        if (inventoryOpenedByBox && InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
        {
            InventorySystem.Instance.CloseUI();
        }
        inventoryOpenedByBox = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SelectionManager.Instance.EnableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = true;
    }

    private void RecalculateStorage(GameObject storageUI)
    {
        //get all slots of the ui
        List<GameObject> uiSlots = SlotsOf(storageUI);
        //clear list of items
        selectedStorage.items.Clear();

        List<GameObject> toBeDeleted = new List<GameObject>();
        //take the inventory itesm and convert them into string
        foreach(GameObject slot in uiSlots)
        {
            if(slot.transform.childCount > 0)
            {
                //remove "Clone" text
                string name = slot.transform.GetChild(0).name;
                string str2 = "(Clone)";
                string result = name.Replace(str2,"");

                selectedStorage.items.Add(result);
                toBeDeleted.Add(slot.transform.GetChild(0).gameObject);
            }
        }
        foreach (GameObject obj in toBeDeleted)
        {
            Destroy(obj);
        }



    }

    public void SetSelectedStorage(StorageBox storage)
    {
        selectedStorage = storage;
    }

    private GameObject GetRelevantUI(StorageBox storage)
    {
        // Create a switch for other types
        return storageBoxSmallUI;
    }
}
