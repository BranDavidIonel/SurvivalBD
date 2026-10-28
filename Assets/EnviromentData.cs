
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class EnviromentData
{
    public List<string> pickedupItems;

    public List<TreeData> treeData;

    public List<string> animals;

    public List<StorageData> storage;

    // 0 (saves made before this field existed): treeData has every direct child of the Trees object,
    // scenery too (the firs as "Tree_Parent", the Birch_forest groups as "Stump"), and no tree inside a group.
    // 1: treeData has only the choppable trees and the stumps, wherever they are under Trees.
    // No initializer on purpose: an old save has no such field and must read as 0.
    public int treeSaveVersion;
    public const int CurrentTreeSaveVersion = 1;

    // the forests (trees, seeds, stumps); the old name of the pine forest, so old saves still load it.
    // Saves made before it existed read it as an empty object (version 0).
    public ForestData pineForest;

    // plants picked and growing back (mushrooms, hay, berries...); version 0 in older saves
    public VegetationData vegetation;

    // every rabbit (male / female, age, babies on the way); version 0 in older saves
    public RabbitSaveData rabbits;

    // the story: which level, which goals are done; version 0 in older saves
    public StoryData story;

    // the campfires placed in the world and how much wood is still burning in them (null in older saves)
    public List<CampfireData> campfires;

    // the rain puddles (fresh water for 2-3 days after the rain); null in older saves
    public List<PuddleData> puddles;

    // the things the player dropped on the ground from the inventory; null in older saves
    public List<DroppedItemData> dropped;

    // what was built (foundations, walls, door walls with their doors open or shut, windows, roofs); version 0 in
    // older saves (before, buildings were not saved at all)
    public BuildingSaveData buildings;

    // the rabbit snares and the rabbits caught in them; null in older saves
    public List<SnareData> snares;


    public EnviromentData(List<string> _pickedupItems, List<TreeData> _treeData, List<string> _animals, List<StorageData> _storage)
    {
        pickedupItems = _pickedupItems;
        treeData = _treeData;
        animals = _animals;
        storage = _storage;
        treeSaveVersion = CurrentTreeSaveVersion;
    }



}
[System.Serializable]
public class CampfireData
{
    public Vector3 position;
    public Vector3 rotation;
    public float fuelMinutes;
    public float burnedMinutes;
}

[System.Serializable]
public class StorageData
{
    public List<string> items;
    public Vector3 position;
    public Vector3 rotation;
}

[System.Serializable]
public class TreeData
{
    public string name;//tree or stump
    public Vector3 position;
    public Vector3 rotation;
}