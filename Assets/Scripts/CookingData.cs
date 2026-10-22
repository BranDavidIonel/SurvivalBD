using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CookingData", menuName = "ScriptableObject/CookingData", order = 1)]
public class CookingData : ScriptableObject
{
    // Items with this tag can be put on a campfire (the wood items: Stick, Plank, Log).
    public const string BurnableTag = "Burnable";

    [Tooltip("Old list of fuels by name (still accepted, together with the Burnable tag)")]
    public List<string> validFuels = new List<string>();
    public List<CookableFood> validFoods = new List<CookableFood>();

    [Tooltip("How long each burnable item keeps a campfire going, in game minutes")]
    public List<FuelBurnTime> fuels = new List<FuelBurnTime>
    {
        new FuelBurnTime("Stick", 30f),
        new FuelBurnTime("Plank", 90f),
        new FuelBurnTime("Log", 180f),
    };
    [Tooltip("Burn time of a Burnable item that is not in the list above (game minutes)")]
    public float defaultBurnMinutes = 30f;

    public bool IsBurnable(InventoryItem item)
    {
        if (item == null) return false;
        if (item.gameObject.tag == BurnableTag) return true;
        if (validFuels.Contains(item.thisName)) return true;
        return FindFuel(item.thisName) != null;
    }

    // game minutes this item keeps the fire burning (0 = it does not burn)
    public float BurnMinutes(InventoryItem item)
    {
        if (!IsBurnable(item)) return 0f;
        var fuel = FindFuel(item.thisName);
        return fuel != null ? fuel.minutes : defaultBurnMinutes;
    }

    FuelBurnTime FindFuel(string itemName)
    {
        foreach (var fuel in fuels)
        {
            if (fuel != null && string.Equals(fuel.name, itemName, System.StringComparison.OrdinalIgnoreCase)) return fuel;
        }
        return null;
    }
}

[System.Serializable]
public class CookableFood
{
    public string name;
    public float timeToCook;
    public string cookedFoodName;
}

[System.Serializable]
public class FuelBurnTime
{
    [Tooltip("The item's name (Inventory Item > This Name)")]
    public string name;
    [Tooltip("Game minutes it burns")]
    public float minutes;

    public FuelBurnTime() { }
    public FuelBurnTime(string name, float minutes)
    {
        this.name = name;
        this.minutes = minutes;
    }
}
