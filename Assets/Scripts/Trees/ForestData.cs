using System.Collections.Generic;
using UnityEngine;

// What a save keeps of the forest (see Forest.cs): every tree, the seeds on the ground, the seeds the player
// planted and the stumps. The field names are the ones of the old pine forest save (version 1, only pines), so old
// saves still load: their species is 0 = pine.
[System.Serializable]
public class ForestData
{
    // 0: not saved (a save from before the pine forest), 1: only pines, 2: all the tree species
    public int version;
    public List<ForestTreeData> trees = new List<ForestTreeData>();
    public List<SeedData> cones = new List<SeedData>();      // seeds lying on the ground (cones, acorns...)
    public List<StumpData> stumps = new List<StumpData>();
    public List<SeedData> seeds = new List<SeedData>();      // seeds planted by the player, not sprouted yet
}

[System.Serializable]
public class ForestTreeData
{
    public Vector3 position;   // where the trunk meets the ground
    public float rotation;     // degrees around the vertical
    public float height;       // metres, now
    public float maxHeight;    // metres it grows to
    public int variant;        // which model of its species
    public float coneHours;    // game hours until it drops its next seed
    public int species;        // TreeSpecies.Kind (0 pine, 1 birch, 2 oak, 3 beech)
}

[System.Serializable]
public class SeedData
{
    public Vector3 position;
    public float age;          // game hours on the ground
    public float sproutAt;     // game hours after which it grows into a young tree
    public int species;
}

[System.Serializable]
public class StumpData
{
    public Vector3 position;
    public float radius;
    public int species;
}
