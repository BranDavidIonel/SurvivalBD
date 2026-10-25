using UnityEngine;

// On every plant (and small thing lying around) placed in the scene under Enviroment/Vegetation: mushrooms, grass
// that gives hay, berry bushes, herbs, ferns, flowers, branches (sticks), small stones...
// What it gives when picked and how long it takes to grow back. In the game Vegetation.cs looks after all of them
// (only the plants near the player are drawn, they are picked with the hand, and they grow back).
[DisallowMultipleComponent]
public class ScenePlant : MonoBehaviour
{
    public enum PlantSize { Small, Medium, Large }

    [Tooltip("Inventory item given when it is picked (empty: it can't be picked, it is only there to be seen)")]
    public string item = "";
    [Tooltip("Name shown when you look at it")]
    public string displayName = "";
    [Tooltip("How many items one pick gives")]
    [Range(1, 5)] public int amount = 1;
    [Tooltip("Only a part of it is picked (berries): the plant stays, its child named \"Fruit\" disappears")]
    public bool keepPlant;
    [Tooltip("Game hours until it grows back after it was picked (0: never)")]
    public float regrowHours = 48f;
    [Tooltip("Small things are drawn only close to the player, big ones farther away")]
    public PlantSize size = PlantSize.Medium;
    [Tooltip("Hidden while deep snow lies on the ground")]
    public bool hideUnderSnow = true;
}
