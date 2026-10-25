using System.Collections.Generic;
using UnityEngine;

// What a save keeps of the rabbits (RabbitPopulation): every living rabbit.
[System.Serializable]
public class RabbitSaveData
{
    public int version;          // 0: a save from before (the old save only had the rabbits' names)
    public List<RabbitData> rabbits = new List<RabbitData>();
}

[System.Serializable]
public class RabbitData
{
    public Vector3 position;
    public float rotation;
    public int sex;              // 0 male, 1 female
    public float ageHours;       // game hours old
    public float pregnantHours;  // game hours until the babies come (-1: not expecting)
    public float restHours;      // game hours until it can mate again
}
