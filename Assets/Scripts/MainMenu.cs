using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    public Button LoadGameBTN;
    /*
  private void Start()
  {

      LoadGameBTN.onClick.AddListener(() =>
      {
          SaveManager.Instance.StartLoadedGame(1);
      });
      
    }
    */

    // the language of the story, chosen before a new game starts (also in Settings while playing)
    Button english, romanian;

    void Start()
    {
        // the big title a little lower, clear of the language buttons (in Romanian it is long)
        var title = transform.Find("Title") as RectTransform;
        if (title != null) title.anchoredPosition += new Vector2(0f, -60f);
        if (GameObject.Find("Language buttons (made in code)") != null) return;
        var go = new GameObject("Language buttons (made in code)");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        go.AddComponent<GraphicRaycaster>();
        english = UISkin.NewButton("English", go.transform, UISkin.Pill, Color.white, "English", 30, Color.white, () => Choose(Lang.Language.English));
        UISkin.Place((RectTransform)english.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-250f, -30f), new Vector2(200f, 66f));
        romanian = UISkin.NewButton("Romana", go.transform, UISkin.Pill, Color.white, "Română", 30, Color.white, () => Choose(Lang.Language.Romanian));
        UISkin.Place((RectTransform)romanian.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -30f), new Vector2(200f, 66f));
        ShowChoice();
    }

    void Choose(Lang.Language language)
    {
        Lang.Current = language;
        ShowChoice();
    }

    void ShowChoice()
    {
        var on = new Color(0.86f, 0.62f, 0.16f, 0.98f);   // the chosen one: gold
        var off = new Color(0.25f, 0.2f, 0.16f, 0.85f);
        ((Image)english.targetGraphic).color = Lang.Current == Lang.Language.English ? on : off;
        ((Image)romanian.targetGraphic).color = Lang.Ro ? on : off;
    }

    public void NewGame()
    {
        SceneManager.LoadScene("GameScene");
    }
    public void ExitGame()
    {
        Debug.Log("Quitting Game");
        Application.Quit();
    }

}
