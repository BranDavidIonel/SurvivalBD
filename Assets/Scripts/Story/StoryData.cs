using UnityEngine;

// What a save keeps of the story (StoryManager): which level, which goals are done.
[System.Serializable]
public class StoryData
{
    // 1: up to 9 levels (9 was the raft); 2: the canon is level 9 and the raft level 10
    public const int CurrentVersion = 2;

    public int version;          // 0: a save from before the story
    public int level = 1;        // 1..10 (10 = the raft, the last level), 11 = the end
    public int stepsDone;        // one bit per goal of the current level
    public bool introSeen;
    public bool riverFound;
    public bool drank;
    public int mushroomsPicked;
    public int mushroomsEaten;
    public bool nightStarted;    // level 4: the night has begun
    public bool journalRead;     // level 6: the monk's journal
    public bool prayerOpened;    // level 7: the prayer book opened while the shadow came
    public bool caughtMale;      // level 9 (the canon): a male rabbit taken alive from a snare
    public bool caughtFemale;    //   and a female
    public int treesPlanted;     //   trees planted since the canon began
    public bool crossPlaced;     //   the cross stands on the other mountain (then the new stream flows)
    public Vector3 crossPosition;
    public float crossYaw;
    public float winterInHours = -1f; // after the canon (level 9): game hours until winter comes (-1: not yet started)
    public bool winterCame;      // the first winter came (a loaded game is put back in winter)
    public int[] raft;           // level 10: what is already on the raft (Raft.Needs)
}
