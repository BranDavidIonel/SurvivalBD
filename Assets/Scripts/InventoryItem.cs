using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InventoryItem : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    // --- Is this item trashable --- //
    public bool isTrashable;

    // --- Item Info UI --- //
    private GameObject itemInfoUI;

    private Text itemInfoUI_itemName;
    private Text itemInfoUI_itemDescription;
    private Text itemInfoUI_itemFunctionality;

    public string thisName, thisDescription, thisFunctionality;

    // --- Consumption --- //
    private GameObject itemPendingConsumption;
    public bool isConsumable;

    public float healthEffect;
    public float caloriesEffect;
    public float hydrationEffect;

    // --- Equipping --- //
    public bool isEquippable;
    private GameObject itemPendingEquipping;
    public bool isInsideQuickSlot;

    public bool isSelected;

    public bool isUseable;

    // an item was eaten (its inventory name, e.g. "porcini")
    public static event System.Action<string> Consumed;


    private void Start()
    {
        itemInfoUI = InventorySystem.Instance.ItemInfoUI;
        itemInfoUI_itemName = itemInfoUI.transform.Find("itemName").GetComponent<Text>();
        itemInfoUI_itemDescription = itemInfoUI.transform.Find("itemDescription").GetComponent<Text>();
        itemInfoUI_itemFunctionality = itemInfoUI.transform.Find("itemFunctionality").GetComponent<Text>();
    }
    void Update()
    {
        if (isSelected)
        {
            gameObject.GetComponent<DragDrop>().enabled = false;

        }
        else
        {
            gameObject.GetComponent<DragDrop>().enabled = true;
        }
    }
    
    // Triggered when the mouse enters into the area of the item that has this script.
    public void OnPointerEnter(PointerEventData eventData)
    {
        itemInfoUI.SetActive(true);
        string shownName = thisName, shownDescription = thisDescription, shownUse = thisFunctionality;
        Translations.Item(InventorySystem.CleanName(gameObject.name), ref shownName, ref shownDescription, ref shownUse); // (Romanian)
        itemInfoUI_itemName.text = shownName;
        itemInfoUI_itemDescription.text = shownDescription;
        itemInfoUI_itemFunctionality.text = shownUse;
    }

    // Triggered when the mouse exits the area of the item that has this script.
    public void OnPointerExit(PointerEventData eventData)
    {
        // on the phone the finger "leaves" as soon as it lifts: keep showing the last tapped item
        if (GameInput.TouchControls) return;
        itemInfoUI.SetActive(false);
    }

    // Triggered when the mouse is clicked over the item that has this script.
    public void OnPointerDown(PointerEventData eventData)
    {
        //Right Mouse Button Click on
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            UseLikeRightClick();
        }

    }

    // --- Phone: there is no right click, so a double tap does the same. A tap on a quick slot selects it (keys 1-6 on PC). --- //
    static InventoryItem lastTappedItem;
    static float lastTapTime;
    const float DoubleTapSeconds = 0.4f;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!GameInput.TouchControls) return;

        if (isInsideQuickSlot && !InventorySystem.Instance.isOpen)
        {
            UseFromQuickSlot();
            return;
        }

        if (lastTappedItem == this && Time.unscaledTime - lastTapTime < DoubleTapSeconds)
        {
            lastTappedItem = null;
            // same as right button down + up on PC
            UseLikeRightClick();
            FinishConsumption();
        }
        else
        {
            lastTappedItem = this;
            lastTapTime = Time.unscaledTime;
        }
    }

    // A tap on this item in the quick bar, or on its white number button: a tool goes in the hand (tap again: put
    // away); a campfire, storage box, foundation or wall starts being placed; food is eaten.
    public void UseFromQuickSlot()
    {
        if (!isInsideQuickSlot || transform.parent == null) return;
        if (isUseable || isConsumable)
        {
            UseLikeRightClick();
            FinishConsumption();
        }
        else
        {
            EquipSystem.Instance.SelectQuickSlotOf(transform.parent.gameObject);
        }
    }

    private void UseLikeRightClick()
    {
        if (isConsumable)
        {
            // Setting this specific gameobject to be the item we want to destroy later
            itemPendingConsumption = gameObject;
            consumingFunction(healthEffect, caloriesEffect, hydrationEffect);
        }

        if (isEquippable && isInsideQuickSlot == false && EquipSystem.Instance.CheckIfFull() == false)
        {
            EquipSystem.Instance.AddToQuickSlots(gameObject);
            isInsideQuickSlot = true;
        }
        if (isUseable)
        {
            //ConstructionManager.Instance.itemToBeDestroyed = gameObject;
            gameObject.SetActive(false);
            UseItem();
        }
    }

    private void UseItem()
    {
        if (itemInfoUI == null) itemInfoUI = InventorySystem.Instance.ItemInfoUI; // (used from the quick bar before the inventory was ever opened)
        itemInfoUI.SetActive(true);
        InventorySystem.Instance.isOpen = false;
        InventorySystem.Instance.inventoryScreenUI.SetActive(false);

        CraftingSystem.Instance.isOpen = false;
        CraftingSystem.Instance.craftingScreenUI.SetActive(false);
        CraftingSystem.Instance.toolsScreenUI.SetActive(false);
        CraftingSystem.Instance.survivalScreenUI.SetActive(false);
        CraftingSystem.Instance.refineScreenUI.SetActive(false);
        CraftingSystem.Instance.constructionScreenUI.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SelectionManager.Instance.EnableSelection();
        SelectionManager.Instance.enabled = true;
        switch (gameObject.name)
        {
            case "foundation(Clone)":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement("FoundationModel");
                break;
            case "foundation":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement("FoundationModel"); //for testing
                break;
            case "wall(Clone)":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject; // so it leaves the inventory when placed (and comes back on Cancel)
                ConstructionManager.Instance.ActivateConstructionPlacement("WallModel");
                break;
            case "wall":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement("WallModel"); //for testing
                break;
            case "StorageBox(Clone)":
                PlacementSystem.Instance.inventoryItemToDestory = gameObject;
                PlacementSystem.Instance.ActivatePlacementMode("StorageBoxModel");
                break;
            case "StorageBox":
                PlacementSystem.Instance.inventoryItemToDestory = gameObject;
                PlacementSystem.Instance.ActivatePlacementMode("StorageBoxModel");
                break;
            case "Campfire(Clone)":
                PlacementSystem.Instance.inventoryItemToDestory = gameObject;
                PlacementSystem.Instance.ActivatePlacementMode("CampfireModel");
                break;
            case "Campfire"://for testing
                PlacementSystem.Instance.inventoryItemToDestory = gameObject;
                PlacementSystem.Instance.ActivatePlacementMode("CampfireModel");
                break;
            case "journal(Clone)":
            case "journal":
            case "prayerbook(Clone)":
            case "prayerbook":
                // the monk's books open in front of the player (they stay in the inventory)
                itemInfoUI.SetActive(false);
                gameObject.SetActive(true);
                BookReader.Open(gameObject.name.StartsWith("journal") ? BookReader.Book.Journal : BookReader.Book.PrayerBook);
                break;
            case "doorwall(Clone)":
            case "doorwall":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement(Buildings.DoorWallModel);
                break;
            case "windowwall(Clone)":
            case "windowwall":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement(Buildings.WindowWallModel);
                break;
            case "roof(Clone)":
            case "roof":
                ConstructionManager.Instance.itemToBeDestroyed = gameObject;
                ConstructionManager.Instance.ActivateConstructionPlacement(Buildings.RoofModel);
                break;
            case "fence(Clone)":
            case "fence":
            case "fencegate(Clone)":
            case "fencegate":
                // a fence panel / a gate: set up where the player looks, joined to the fence already standing
                itemInfoUI.SetActive(false);
                if (FencePlacing.Instance != null) FencePlacing.Instance.Begin(gameObject);
                else gameObject.SetActive(true);
                break;
            case "snare(Clone)":
            case "snare":
                // set it on the ground where the player looks (SnarePlacing)
                itemInfoUI.SetActive(false);
                if (SnarePlacing.Instance != null) SnarePlacing.Instance.Begin(gameObject);
                else gameObject.SetActive(true);
                break;
            case "cross(Clone)":
            case "cross":
                // the canon's cross: raised on the top of the other mountain (level 9)
                itemInfoUI.SetActive(false);
                if (SnarePlacing.Instance != null) SnarePlacing.Instance.Begin(gameObject, SnarePlacing.Thing.Cross);
                else gameObject.SetActive(true);
                break;
            case "rabbitmale(Clone)":
            case "rabbitmale":
            case "rabbitfemale(Clone)":
            case "rabbitfemale":
                // a live rabbit: let it go in front of the player (in the pen it stays, outside it runs away)
                itemInfoUI.SetActive(false);
                RabbitPopulation.ReleaseFromInventory(gameObject);
                break;
            default:
                // a seed (pine cone, acorn, beechnut, birch catkin): plant it where the player looks
                // (the seed in a mound of earth shows the spot: green = a tree can grow there)
                if (TreeSpecies.FromSeedItem(gameObject.name) >= 0)
                {
                    itemInfoUI.SetActive(false);
                    if (SeedPlanting.Instance != null) SeedPlanting.Instance.Begin(gameObject);
                    else gameObject.SetActive(true);
                }
                break;
        }



    }

    // Triggered when the mouse button is released over the item that has this script.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            FinishConsumption();
        }

    }

    // the eaten item leaves the inventory
    private void FinishConsumption()
    {
        if (isConsumable && itemPendingConsumption == gameObject)
        {
            DestroyImmediate(gameObject);
            InventorySystem.Instance.RecalculateList();
            CraftingSystem.Instance.RefreshNeededItems();
        }
    }


    private void consumingFunction(float healthEffect, float caloriesEffect, float hydrationEffect)
    {
        if (itemInfoUI == null) itemInfoUI = InventorySystem.Instance.ItemInfoUI;
        itemInfoUI.SetActive(false);

        healthEffectCalculation(healthEffect);

        caloriesEffectCalculation(caloriesEffect);

        hydrationEffectCalculation(hydrationEffect);

        // the story's goals listen to this (eat mushrooms...)
        if (Consumed != null) Consumed(InventorySystem.CleanName(gameObject.name));

    }


    private static void healthEffectCalculation(float healthEffect)
    {
        // --- Health --- //

        float healthBeforeConsumption = PlayerState.Instance.currentHealth;
        float maxHealth = PlayerState.Instance.maxHealth;
    
        if (healthEffect != 0)
        {
            if ((healthBeforeConsumption + healthEffect) > maxHealth)
            {
                PlayerState.Instance.setHealth(maxHealth);
            }
            else
            {
                PlayerState.Instance.setHealth(healthBeforeConsumption + healthEffect);
            }
        }
    
    }


    private static void caloriesEffectCalculation(float caloriesEffect)
    {
        // --- Calories --- //

        float caloriesBeforeConsumption = PlayerState.Instance.currentCalories;
        float maxCalories = PlayerState.Instance.maxCalories;
    
        if (caloriesEffect != 0)
        {
            if ((caloriesBeforeConsumption + caloriesEffect) > maxCalories)
            {
                PlayerState.Instance.setCalories(maxCalories);
            }
            else
            {
                PlayerState.Instance.setCalories(caloriesBeforeConsumption + caloriesEffect);
            }
        }
    
    }


    private static void hydrationEffectCalculation(float hydrationEffect)
    {
        // --- Hydration --- //

        float hydrationBeforeConsumption = PlayerState.Instance.currentHydrationPercent;
        float maxHydration = PlayerState.Instance.maxHydrationPercent;
    
        if (hydrationEffect != 0)
        {
            if ((hydrationBeforeConsumption + hydrationEffect) > maxHydration)
            {
                PlayerState.Instance.setHydration(maxHydration);
            }
            else
            {
                PlayerState.Instance.setHydration(hydrationBeforeConsumption + hydrationEffect);
            }
        }
    
    }


}
