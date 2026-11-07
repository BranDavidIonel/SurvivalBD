using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerData 
{
    public float[] playerStats;//[0] health, [1] - Calories, [2] - Hydration

    public float[] playerPositionAndRotation; // position x,y,z and rotation x.y,z

    public string[] inventoryContent;

    public string[] quickSlotsContent;

    public bool backpack; // the player made the backpack: more inventory slots (an older save: false)

    public PlayerData(float[] _playerStats, float[] _playerPosAndRot, string[] _inventoryContent, string[] _quickSlotsContent)
    {
        playerStats = _playerStats;
        playerPositionAndRotation = _playerPosAndRot;
        inventoryContent = _inventoryContent;
        quickSlotsContent = _quickSlotsContent;
    }

}
