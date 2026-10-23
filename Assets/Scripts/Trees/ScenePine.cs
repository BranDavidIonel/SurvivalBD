using UnityEngine;

// On every pine of a Fir_forest group (in the scene): how tall it is now and how tall it may grow.
// Change "Height" and the pine gets bigger or smaller (you can also just scale it: Height follows).
// When the game starts it becomes a living pine at the same place and size (PineForest): it grows up to
// "Grows Up To", drops cones and can be chopped.
[ExecuteAlways]
[DisallowMultipleComponent]
public class ScenePine : MonoBehaviour
{
    [Tooltip("Height of this pine now, in metres")]
    [Range(0.7f, 40f)] public float height = 20f;
    [Tooltip("How tall it grows in the game, in metres")]
    [Range(15f, 40f)] public float growsUpTo = 35f;

    float shownHeight = -1f;

    void OnValidate()
    {
        if (growsUpTo < height) growsUpTo = Mathf.Min(40f, Mathf.Max(15f, height));
        shownHeight = -1f; // apply in Update (changing the scale inside OnValidate is not allowed everywhere)
    }

    void Update()
    {
        if (Application.isPlaying) return;
        float now = CurrentHeight();
        if (now <= 0f) return;
        if (shownHeight < 0f)
        {
            // the Height field was changed: resize the pine
            if (Mathf.Abs(now - height) > 0.01f) transform.localScale *= height / now;
            shownHeight = height;
        }
        else if (Mathf.Abs(now - shownHeight) > 0.05f)
        {
            // the pine was scaled with the scale tool: show its new height
            height = Mathf.Clamp(Mathf.Round(now * 10f) / 10f, 0.7f, 40f);
            shownHeight = now;
        }
    }

    // the height of the model as it is drawn (metres)
    float CurrentHeight()
    {
        var filter = GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null) return -1f;
        return filter.sharedMesh.bounds.size.y * transform.lossyScale.y;
    }
}
