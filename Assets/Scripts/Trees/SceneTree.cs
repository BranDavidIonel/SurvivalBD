using UnityEngine;

// On every tree of the forests placed in the scene (Oak_forest, Birch_forest, Beech_forest, Mixed_forest...): what
// kind of tree it is, how tall it is now and how tall it may grow. Change "Height" and the tree gets bigger or
// smaller (you can also just scale it: Height follows). When the game starts it becomes a living tree at the same
// place and size (Forest.cs): it grows up to "Grows Up To", drops seeds and can be chopped.
// (The pines of the Fir_forest groups have a ScenePine instead: the same thing for pines.)
[ExecuteAlways]
[DisallowMultipleComponent]
public class SceneTree : MonoBehaviour
{
    public TreeSpecies.Kind species = TreeSpecies.Kind.Oak;
    [Tooltip("Height of this tree now, in metres")]
    [Range(0.5f, 30f)] public float height = 18f;
    [Tooltip("How tall it grows in the game, in metres")]
    [Range(10f, 30f)] public float growsUpTo = 22f;

    float shownHeight = -1f;

    void Awake()
    {
        if (Application.isPlaying) enabled = false; // only for the editor (no work every frame in the game)
    }

    void OnValidate()
    {
        if (growsUpTo < height) growsUpTo = Mathf.Min(30f, Mathf.Max(10f, height));
        shownHeight = -1f; // apply in Update (changing the scale inside OnValidate is not allowed everywhere)
    }

    void Update()
    {
        if (Application.isPlaying) return;
        float now = CurrentHeight();
        if (now <= 0f) return;
        if (shownHeight < 0f)
        {
            if (Mathf.Abs(now - height) > 0.01f) transform.localScale *= height / now;
            shownHeight = height;
        }
        else if (Mathf.Abs(now - shownHeight) > 0.05f)
        {
            height = Mathf.Clamp(Mathf.Round(now * 10f) / 10f, 0.5f, 30f);
            shownHeight = now;
        }
    }

    float CurrentHeight()
    {
        var filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return -1f;
        return filter.sharedMesh.bounds.size.y * transform.lossyScale.y;
    }
}
