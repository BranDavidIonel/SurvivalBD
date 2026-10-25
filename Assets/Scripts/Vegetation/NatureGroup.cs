using UnityEngine;

// Marks a group of trees, plants or rabbits made by Tools > Nature > Fill The Island (so the tool can find and
// replace its own groups later). Move, delete or copy the objects inside as you like.
[DisallowMultipleComponent]
public class NatureGroup : MonoBehaviour
{
    [Tooltip("What kind of place this is (Oak forest, Meadow, Beach...)")]
    public string kind = "";
}
