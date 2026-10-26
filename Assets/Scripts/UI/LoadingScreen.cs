using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

// The screen shown while a saved game loads (SceneSystem): the picture taken when the game was saved fills the
// screen, and over it an Orthodox prayer chosen at random (Prayers.cs, in the language of the Settings), in red so
// it stands out, on a dark band. It stays a few seconds so the prayer can be read, then fades away.
public class LoadingScreen : MonoBehaviour
{
    public static LoadingScreen Instance { get; private set; }

    [Tooltip("Seconds it stays at least, to read the prayer")]
    public float minimumSeconds = 6f;

    CanvasGroup group;
    Text dots;
    float shownAt;
    Texture2D picture;

    static readonly Color PrayerRed = new Color(0.95f, 0.16f, 0.12f);

    public static void Show(int slot)
    {
        if (Instance == null) new GameObject("Loading screen (made in code)").AddComponent<LoadingScreen>();
        Instance.Build(slot);
    }

    // hides it when it has been shown long enough (the game is loaded by then)
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
        var dim = UISkin.NewImage("Dim", transform, UISkin.White, new Color(0f, 0f, 0f, 0.35f), false);
        UISkin.Fill(dim.rectTransform);

        // the prayer, on a dark band across the middle
        var prayer = Prayers.RandomShort(420);
        var band = UISkin.NewImage("Band", transform, UISkin.White, new Color(0f, 0f, 0f, 0.62f), false);
        var bandRect = band.rectTransform;
        bandRect.anchorMin = new Vector2(0f, 0.5f);
        bandRect.anchorMax = new Vector2(1f, 0.5f);
        bandRect.pivot = new Vector2(0.5f, 0.5f);
        bandRect.sizeDelta = new Vector2(0f, 560f);
        bandRect.anchoredPosition = new Vector2(0f, 20f);
        var title = UISkin.NewText("Prayer title", transform, prayer.Title, 40, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Centered(title.rectTransform, new Vector2(0f, 240f), new Vector2(1600f, 70f));
        title.fontStyle = FontStyle.BoldAndItalic;
        var text = UISkin.NewText("Prayer", transform, prayer.Text, 40, PrayerRed, TextAnchor.MiddleCenter, false);
        UISkin.Centered(text.rectTransform, new Vector2(0f, 0f), new Vector2(1560f, 400f));
        text.fontStyle = FontStyle.Bold;
        text.lineSpacing = 1.1f;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 24;
        text.resizeTextMaxSize = 44;
        var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);

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
        for (float t = 1f; t > 0f; t -= Time.unscaledDeltaTime / 0.7f)
        {
            if (group != null) group.alpha = t;
            yield return null;
        }
        Destroy(gameObject);
    }
}
