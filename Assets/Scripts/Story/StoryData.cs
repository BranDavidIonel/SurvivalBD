using UnityEngine;

// What a save keeps of the story (StoryManager): which level, which goals are done.
[System.Serializable]
public class StoryData
{
    // 1: up to 9 levels (9 was the raft); 2: the canon is level 9 and the raft level 10;
    // 3: the facts below kept from the start of the game (a night by the fire, the axe, the torch...)
    public const int CurrentVersion = 3;

    public int version;          // 0: a save from before the story
    public int level = 1;        // 1..10 (10 = the raft, the last level), 11 = the end
    public int stepsDone;        // one bit per goal of the current level
    public bool introSeen;
    public bool riverFound;
    public bool drank;
    public int mushroomsPicked;
    public int mushroomsEaten;
    public bool nightStarted;    // (old saves) level 4: the night had begun; not used any more (see nightByFire)
    public bool journalRead;     // level 6: the monk's journal (read at any level)
    public bool prayerOpened;    // level 7: the prayer book opened at night or while the shadow came (at any level)

    // What the player did since the game began, whatever the level was then (StoryManager.Remember): a level that
    // asks for it later is done at once. (Old saves: all false, they are set again as the player plays.)
    public bool fireLit;         // a placed campfire burned
    public bool fireAtNight;     // a placed campfire burned during a night
    public bool byFireTonight;   // this night the player was by a burning campfire (morning: nightByFire)
    public bool nightByFire;     // the morning came after a night spent by a burning campfire
    public bool firewood;        // firewood for a night: 3 logs or 8 sticks carried, or 4 hours of wood in the fires
    public bool hadAxe;          // a stone axe (or the iron axe) was in the inventory or the hand
    public bool hadTorch;        // a torch was in the inventory or the hand
    public bool torchLit;        // a torch burned in the player's hand
    public bool hadIronAxe;      // the iron axe was in the inventory or the hand
    public bool caughtMale;      // level 9 (the canon): a male rabbit taken alive from a snare
    public bool caughtFemale;    //   and a female
    public int treesPlanted;     //   trees planted (since the game began; old saves: since the canon began)
    public bool crossPlaced;     //   the cross stands on the other mountain (then the new stream flows)
    public Vector3 crossPosition;
    public float crossYaw;
    public float winterInHours = -1f; // after the canon (level 9): game hours until winter comes (-1: not yet started)
    public bool winterCame;      // the first winter came (a loaded game is put back in winter)
    public int[] raft;           // level 10: what is already on the raft (Raft.Needs)
}
