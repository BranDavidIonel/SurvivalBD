
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

    // the pine forest (pines, cones, stumps); null in saves made before it existed
    public PineForestData pineForest;


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