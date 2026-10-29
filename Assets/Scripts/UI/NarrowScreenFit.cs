using UnityEngine;
using UnityEngine.UI;

// The game's screens are laid out for 1920 x 1080 and grow or shrink with the screen's height. On a screen narrower
// than 16:9 (a 4:3 or 5:4 monitor, a tablet) that cut off their sides: the level panel covered the bars, the help and
// the map did not fit. There every such canvas fits the width instead and gets more room in height (CanvasScaler
// "Expand"). On 16:9 and wider screens (PCs, phones) nothing changes. Made by itself at the start of the game.
public class NarrowScreenFit : MonoBehaviour
{
    static NarrowScreenFit instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Make()
    {
        if (instance != null) return;
        var go = new GameObject("Narrow screen fit (made in code)");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<NarrowScreenFit>();
    }

    // every frame while the screen is narrow: the windows are made in code at any time
    void LateUpdate()
    {
        if (Screen.height <= 0 || (float)Screen.width / Screen.height >= 16f / 9f - 0.01f) return;
        foreach (var scaler in FindObjectsOfType<CanvasScaler>(true))
        {
            if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize) continue;
            if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight || scaler.matchWidthOrHeight < 0.99f) continue;
            Vector2 r = scaler.referenceResolution;
            if (Mathf.Abs(r.x - 1920f) > 1f || Mathf.Abs(r.y - 1080f) > 1f) continue;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }
    }
}
