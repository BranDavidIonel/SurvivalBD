using System.Collections.Generic;
using UnityEngine;

// What a save keeps of the pine forest (see PineForest): every pine, the cones on the ground and the stumps.
[System.Serializable]
public class PineForestData
{
    // 0: not saved (a save from before the pine forest; JsonUtility then still makes this object, empty)
    public int version;
    public List<PineData> trees = new List<PineData>();
    public List<PineConeData> cones = new List<PineConeData>();
    public List<PineStumpData> stumps = new List<PineStumpData>();
    public List<PineConeData> seeds = new List<PineConeData>(); // cones planted by the player, not sprouted yet
}

[System.Serializable]
public class PineData
{
    public Vector3 position;   // where the trunk meets the ground
    public float rotation;     // degrees around the vertical
    public float height;       // metres, now
    public float maxHeight;    // metres it grows to (30-40)
    public int variant;        // which of the pine models
    public float coneHours;    // game hours until it drops its next cone
}

[System.Serializable]
public class PineConeData
{
    public Vector3 position;
    public float age;          // game hours on the ground
    public float sproutAt;     // game hours after which it grows into a young pine
}

[System.Serializable]
public class PineStumpData
{
    public Vector3 position;
    public float radius;
}
