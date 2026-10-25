using UnityEngine;

// Where a new game starts: on the beach after the shipwreck (Enviroment/Story Start). Move it or turn it in the
// scene to change where the player wakes up and which way they look. StoryManager puts the player here.
public class StoryStart : MonoBehaviour
{
    void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.9f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.9f, 0.9f);
        Gizmos.DrawLine(transform.position + Vector3.up * 0.9f, transform.position + Vector3.up * 0.9f + transform.forward * 3f);
    }
}
