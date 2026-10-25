using System.Collections.Generic;
using UnityEngine;

// What a save keeps of the plants (Vegetation.cs): only the ones that were picked and are growing back.
// The plants themselves are in the scene; a picked one is found again by its position.
[System.Serializable]
public class VegetationData
{
    public int version;          // 0: a save from before the plants
    public float clock;          // game hours the plants have been growing
    public List<PickedPlantData> picked = new List<PickedPlantData>();
}

[System.Serializable]
public class PickedPlantData
{
    public Vector3 position;
    public float regrowAt;       // clock hour it grows back (-1: never)
}
