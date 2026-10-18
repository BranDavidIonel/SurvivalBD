using UnityEngine;
using UnityEngine.UI;

// Small "© Bran David Ionel" line in the bottom-left corner of every screen (main menu and game, PC and phone).
// Created automatically when the game starts, nothing has to be added to the scenes.
public class CopyrightLabel : MonoBehaviour
{
    public const string Author = "Bran David Ionel";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (FindObjectOfType<CopyrightLabel>() != null) return;

        var canvasObject = new GameObject("Copyright", typeof(RectTransform));
        DontDestroyOnLoad(canvasObject);
        canvasObject.AddComponent<CopyrightLabel>();

        var canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above everything, also above the menus

        var scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;
        // no GraphicRaycaster: the label never blocks touches or clicks

        var textObject = new GameObject("Text", typeof(RectTransform));
        var rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(canvasObject.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.zero;
        rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(120f, 12f); // clear of the menu frame's corner ornament
        rect.sizeDelta = new Vector2(600f, 36f);

        var text = textObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.text = "© 2026 " + Author;
        text.fontSize = 24;
        text.alignment = TextAnchor.LowerLeft;
        text.color = new Color(1f, 1f, 1f, 0.75f);
        text.raycastTarget = false;
        textObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
    }
}
