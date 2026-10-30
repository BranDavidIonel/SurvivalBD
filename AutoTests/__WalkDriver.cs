#if UNITY_EDITOR
using UnityEngine;

// TEMPORARY (Claude): holds the left joystick for the level test (after MobileControls sets it, before the player
// moves). Removed after the tests.
[DefaultExecutionOrder(-400)]
public class __WalkDriver : MonoBehaviour
{
    public static bool On;
    public static Vector2 Move;
    static __WalkDriver instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (instance != null) return;
        var go = new GameObject("__WalkDriver");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<__WalkDriver>();
    }

    void Update()
    {
        if (On) GameInput.touchMove = Move;
    }
}
#endif
