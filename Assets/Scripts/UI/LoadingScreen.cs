using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// The screen shown while a saved game loads (SceneSystem): the picture taken when the game was saved fills the
// screen, with the game's name, "Loading..." and the credit. (It used to show a prayer in red over the picture; the
// owner took it out, so it now stays only as long as the game needs to load, then fades away.)
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Tooltip("Seconds it stays at least (from when it appears)")]
    public float minimumSeconds = 2.5f;

    CanvasGroup group;
    Text dots;
    float shownAt;
    Texture2D picture;

    public static void Show(int slot)
    {
        if (Instance == null) new GameObject("Loading screen (made in code)").AddComponent<LoadingScreen>();
        Instance.Build(slot);
    }

    // hides it once it has been there long enough (the game is loaded by then)
    public static void HideWhenRead()
    {
        if (Instance != null) Instance.StartCoroutine(Instance.FadeOut());
    }

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (picture != null) Destroy(picture);
    }

    // (not "GetComponent() ?? AddComponent()": in the editor a missing component is a fake null that ?? keeps)
    T Need<T>() where T : Component
    {
        var c = gameObject.GetComponent<T>();
        return c != null ? c : gameObject.AddComponent<T>();
    }

    void Build(int slot)
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
        shownAt = Time.unscaledTime;
        var canvas = Need<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000; // over everything (the joysticks too)
        var scaler = Need<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        if (gameObject.GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        group = Need<CanvasGroup>();
        group.alpha = 1f;
        group.blocksRaycasts = true;

        // the background: the saved game's picture (or a dark night blue)
        var back = UISkin.NewImage("Background", transform, UISkin.White, new Color(0.05f, 0.06f, 0.09f, 1f), true);
        UISkin.Fill(back.rectTransform);
        picture = LoadPicture(slot);
        if (picture != null)
        {
            var holder = UISkin.NewRect("Picture", transform);
            UISkin.Fill(holder);
            var raw = holder.gameObject.AddComponent<RawImage>();
            raw.texture = picture;
            raw.raycastTarget = false;
            var fitter = holder.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // fills the screen, cut at the edges
            fitter.aspectRatio = picture.width / (float)Mathf.Max(1, picture.height);
        }
        var dim = UISkin.NewImage("Dim", transform, UISkin.White, new Color(0f, 0f, 0f, 0.25f), false);
        UISkin.Fill(dim.rectTransform);

        // the game's name and "Loading"
        var name = UISkin.NewText("Game", transform, "SurvivalBD", 46, UISkin.Gold, TextAnchor.UpperLeft);
        UISkin.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -40f), new Vector2(700f, 70f));
        dots = UISkin.NewText("Loading", transform, Lang.T("Loading", "Se încarcă"), 40, Color.white, TextAnchor.LowerLeft);
        UISkin.Place(dots.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(60f, 50f), new Vector2(700f, 70f));
        var credit = UISkin.NewText("Credit", transform, "© 2026 Bran David Ionel", 24, new Color(1f, 1f, 1f, 0.7f), TextAnchor.LowerRight);
        UISkin.Place(credit.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-60f, 50f), new Vector2(700f, 50f));
    }

    static Texture2D LoadPicture(int slot)
    {
        try
        {
            if (SaveManager.Instance == null) return null;
            string path = SaveManager.Instance.ThumbnailPath(slot);
            if (!File.Exists(path)) return null;
            var tex = new Texture2D(2, 2, TextureFormat.RGB24, false) { name = "Saved game picture" };
            if (!tex.LoadImage(File.ReadAllBytes(path))) { Destroy(tex); return null; }
            return tex;
        }
        catch (System.Exception) { return null; }
    }

    void Update()
    {
        if (dots == null) return;
        int n = 1 + Mathf.FloorToInt(Time.unscaledTime * 2f) % 3;
        dots.text = Lang.T("Loading", "Se încarcă") + new string('.', n);
    }

    IEnumerator FadeOut()
    {
        while (Time.unscaledTime - shownAt < minimumSeconds) yield return null;
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.5f)
        {
            if (group != null) group.alpha = t;
            yield return null;
        }
        Destroy(gameObject);
    }
}
