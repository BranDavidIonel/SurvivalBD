using UnityEngine;

// The kinds of trees of the forest (Forest.cs): how tall they get, how fast they grow, which seed they drop,
// which logs they give. The numbers are game values (a young tree is grown in about 7-14 game days).
public static class TreeSpecies
{
    public enum Kind { Pine = 0, Birch = 1, Oak = 2, Beech = 3 }
    public const int Count = 4;

    public class Info
    {
        public Kind kind;
        public string name;                 // "Oak"
        public string[] models;             // Resources paths of its models
        public float minFull, maxFull;      // metres a grown tree reaches
        public float daysToGrowUp;          // a sapling reaches ~95% of its size in this many game days
        public float seedFromHeight;        // only trees this tall drop seeds
        public Vector2 seedEveryHours;      // game hours between two seeds of one tree
        public Vector2 sproutAfterHours;    // a seed lying on the ground becomes a young tree (3-4 game days)
        public Vector2 plantedSproutHours;  // a seed planted by the player comes out sooner
        public string seedItem, seedName;   // inventory item and its name
        public string logItem, logName;
        public int maxLogs;                 // logs from the biggest trees
        public float widthScale;            // crown width compared with the model scaled to the height
        public float trunkPerMetre;         // trunk radius per metre of height (if the model can't be measured: ForestTree)
        public Color barkTint;              // logs and stumps
        public float spacing;               // a seed only grows this far from other trunks (metres)
        public bool deciduous;              // autumn colours, bare in winter
    }

    public static readonly Info[] All =
    {
        new Info
        {
            kind = Kind.Pine, name = "Pine",
            models = new[] { "PineForest/Pine 1", "PineForest/Pine 3", "PineForest/Pine 5" },
            minFull = 30f, maxFull = 40f, daysToGrowUp = 10f, seedFromHeight = 14f,
            seedEveryHours = new Vector2(12f, 40f), sproutAfterHours = new Vector2(72f, 96f), plantedSproutHours = new Vector2(12f, 20f),
            seedItem = "pinecone", seedName = "Pine Cone", logItem = "pinelog", logName = "Pine Log", maxLogs = 5,
            widthScale = 1f, trunkPerMetre = 0.025f, barkTint = new Color(0.3f, 0.26f, 0.24f), spacing = 3.5f, deciduous = false,
        },
        new Info
        {
            kind = Kind.Birch, name = "Birch",
            models = new[] { "Forest/Birch 1", "Forest/Birch 2" },
            minFull = 16f, maxFull = 22f, daysToGrowUp = 7f, seedFromHeight = 9f,
            seedEveryHours = new Vector2(10f, 32f), sproutAfterHours = new Vector2(72f, 96f), plantedSproutHours = new Vector2(12f, 20f),
            seedItem = "birchseed", seedName = "Birch Catkin", logItem = "log", logName = "Birch Log", maxLogs = 3,
            widthScale = 0.8f, trunkPerMetre = 0.028f, barkTint = Color.white, spacing = 3f, deciduous = true,
        },
        new Info
        {
            kind = Kind.Oak, name = "Oak",
            models = new[] { "Forest/Oak 1" },
            minFull = 18f, maxFull = 25f, daysToGrowUp = 14f, seedFromHeight = 11f,
            seedEveryHours = new Vector2(14f, 40f), sproutAfterHours = new Vector2(72f, 96f), plantedSproutHours = new Vector2(14f, 24f),
            seedItem = "acorn", seedName = "Acorn", logItem = "log", logName = "Oak Log", maxLogs = 5,
            widthScale = 0.75f, trunkPerMetre = 0.088f, barkTint = new Color(0.8f, 0.75f, 0.7f), spacing = 4f, deciduous = true,
        },
        new Info
        {
            kind = Kind.Beech, name = "Beech",
            models = new[] { "Forest/Beech 1" },
            minFull = 18f, maxFull = 26f, daysToGrowUp = 12f, seedFromHeight = 11f,
            seedEveryHours = new Vector2(14f, 40f), sproutAfterHours = new Vector2(72f, 96f), plantedSproutHours = new Vector2(14f, 24f),
            seedItem = "beechnut", seedName = "Beechnut", logItem = "log", logName = "Beech Log", maxLogs = 4,
            widthScale = 0.72f, trunkPerMetre = 0.095f, barkTint = new Color(0.85f, 0.83f, 0.8f), spacing = 3.5f, deciduous = true,
        },
    };

    public static Info Get(int species) => All[Mathf.Clamp(species, 0, Count - 1)];

    // which tree grows from this inventory item (-1: it is not a seed)
    public static int FromSeedItem(string item)
    {
        if (string.IsNullOrEmpty(item)) return -1;
        item = item.Replace("(Clone)", "").Trim();
        for (int i = 0; i < All.Length; i++) if (All[i].seedItem == item) return i;
        return -1;
    }

    // small trees fall with one or two hits, the biggest need 8
    public static int Hits(float height) => Mathf.Clamp(Mathf.RoundToInt(1f + height / 5.5f), 1, 8);

    // 1 log from a small tree, up to maxLogs from a fully grown one
    public static int Logs(int species, float height)
    {
        var s = Get(species);
        float t = Mathf.InverseLerp(3f, s.minFull, height);
        return Mathf.Clamp(Mathf.RoundToInt(1f + (s.maxLogs - 1) * t), 1, s.maxLogs);
    }
}
