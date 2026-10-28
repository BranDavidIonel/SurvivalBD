using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public bool playerInRange;
    public string ItemName;
    public string displayName; // shown when looking at it (e.g. "Pine Log" for the item "pinelog"); empty: ItemName

    // Set by Vegetation: the plant is not destroyed when picked (it grows back), the callback hides it instead.
    public System.Action<InteractableObject> onPicked;

    public string GetItemName()
    {
        // (in Romanian when it is chosen: Translations)
        return Translations.Name(string.IsNullOrEmpty(displayName) ? ItemName : displayName);
    }
    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
    private void Update()
    {
        if (GameInput.PrimaryDown && playerInRange && SelectionManager.Instance.onTargetSelectItem && SelectionManager.Instance.selectedObject == gameObject)
        {
            //if the ivenory is not full then add items
            if (InventorySystem.Instance.CheckSlotsAvailable(1))
            {
                InventorySystem.Instance.AddToIventory(ItemName);
                if (onPicked != null)
                {
                    onPicked(this);
                    return;
                }

                InventorySystem.Instance.itemsPickedup.Add(gameObject.name);
                Debug.Log("Item added to iventory");
                Destroy(gameObject);
            }
            else
            {
                InventorySystem.Instance.ShowMessage(Lang.T("Inventory is full", "Inventarul e plin"));
            }
        }
    }
}
