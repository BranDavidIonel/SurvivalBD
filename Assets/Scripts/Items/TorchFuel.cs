using UnityEngine;

// On the torch in the inventory (Resources/torch): how long it can still burn, in game hours. It burns only while it
// is held in the hand (TorchLight). A loaded game gives every torch a full 6 hours again.
public class TorchFuel : MonoBehaviour
{
    [Tooltip("Game hours this torch can still burn")]
    public float hoursLeft = 6f;
}
