// What a save keeps of the story (StoryManager): which level, which goals are done.
[System.Serializable]
public class StoryData
{
    public int version;          // 0: a save from before the story
    public int level = 1;        // 1..9 (9 = the raft, the last level), 10 = the end
    public int stepsDone;        // one bit per goal of the current level
    public bool introSeen;
    public bool riverFound;
    public bool drank;
    public int mushroomsPicked;
    public int mushroomsEaten;
    public bool nightStarted;    // level 4: the night has begun
    public bool journalRead;     // level 6: the monk's journal
    public bool prayerOpened;    // level 7: the prayer book opened while the shadow came
    public int[] raft;           // level 9: logs, planks, ropes, sticks, hay already on the raft
}
