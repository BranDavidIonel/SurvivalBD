using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public bool playerInRange;
    public string ItemName;

    public string GetItemName()
    {
        return ItemName;
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
        if (Input.GetKeyDown(KeyCode.Mouse0)&& playerInRange && SelectionManager.Instance.onTargetSelectItem && SelectionManager.Instance.selectedObject == gameObject)
        {
            //if the ivenory is not full then add items
            if (InventorySystem.Instance.CheckSlotsAvailable(1))
            {
                InventorySystem.Instance.AddToIventory(ItemName);

                InventorySystem.Instance.itemsPickedup.Add(gameObject.name);
                Debug.Log("Item added to iventory");
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Ivenotory is full from IteractableObject.cs");
            }
        }
    }
}
