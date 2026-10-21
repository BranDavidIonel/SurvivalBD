using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds "Load Game" to the in-game menu (Save Game / Load Game / Settings / Quit), built at runtime from
// the existing buttons and the Save Game screen, so the scene does not have to be edited.
// Picking a saved slot reloads the game from it, the same way Load Game in the main menu does.
// Both screens show their slots with SaveSlotList (a scrolling list with a picture on each slot).
public class InGameLoadMenu : MonoBehaviour
{
    GameObject loadMenu;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToScene();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToScene();

    static void AddToScene()
    {
        var menuManager = FindObjectOfType<MenuManager>();
        if (menuManager != null && menuManager.GetComponent<InGameLoadMenu>() == null)
            menuManager.gameObject.AddComponent<InGameLoadMenu>();
    }

    void Start()
    {
        var mm = MenuManager.Instance != null ? MenuManager.Instance : GetComponent<MenuManager>();
        if (mm == null || mm.saveMenu == null) return;

        var saveButton = mm.menu != null ? mm.menu.transform.Find("SaveGameButton") as RectTransform : null;
        var settingsButton = mm.menu != null ? mm.menu.transform.Find("SettingsButton") as RectTransform : null;
        var quitButton = mm.menu != null ? mm.menu.transform.Find("ExitButton") as RectTransform : null;
        if (saveButton == null || settingsButton == null)
        {
            SaveSlotList.Attach(mm.saveMenu, SaveSlotList.Mode.Save);
            return;
        }

        // --- the Load Game screen: a copy of the Save Game screen whose slots load instead of save --- //
        // (copied before the Save Game screen gets its slot list, so the copy gets its own list)
        loadMenu = Instantiate(mm.saveMenu, mm.saveMenu.transform.parent);
        loadMenu.name = "LoadGameMenu";
        loadMenu.SetActive(false);
        var overrideAlert = loadMenu.transform.Find("OverrideAlert");
        if (overrideAlert != null) DestroyImmediate(overrideAlert.gameObject);
        var title = loadMenu.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = "Load Game";
        // picking a saved slot reloads the game scene from that save
        // (its Back button already returns to the menu: the copy's own links point to the copy)
        SaveSlotList.Attach(loadMenu, SaveSlotList.Mode.Load);

        SaveSlotList.Attach(mm.saveMenu, SaveSlotList.Mode.Save);

        // --- the Load Game button: a copy of the Settings button, placed under Save Game --- //
        var loadButton = (RectTransform)Instantiate(settingsButton.gameObject, settingsButton.parent).transform;
        loadButton.name = "LoadGameButton";
        loadButton.SetSiblingIndex(settingsButton.GetSiblingIndex());
        var loadText = loadButton.GetComponentInChildren<TMP_Text>();
        if (loadText != null) loadText.text = "Load Game";
        var load = loadButton.GetComponent<Button>();
        load.onClick = new Button.ButtonClickedEvent(); // drop the copied "open Settings" links
        load.onClick.AddListener(() =>
        {
            mm.menu.SetActive(false);
            loadMenu.SetActive(true);
        });

        // Save Game, Load Game, Settings one step apart; Quit stays a bit lower
        float step = settingsButton.anchoredPosition.y - saveButton.anchoredPosition.y;
        loadButton.anchoredPosition = settingsButton.anchoredPosition;
        settingsButton.anchoredPosition += new Vector2(0f, step);
        if (quitButton != null)
        {
            float lowestQuitY = settingsButton.anchoredPosition.y + step * 1.2f;
            if (quitButton.anchoredPosition.y > lowestQuitY)
                quitButton.anchoredPosition = new Vector2(quitButton.anchoredPosition.x, lowestQuitY);
        }
    }

    void Update()
    {
        if (loadMenu == null) return;

        // the menu was closed (M / Menu button) while the Load Game screen was open
        if (loadMenu.activeSelf && MenuManager.Instance != null && !MenuManager.Instance.isMenuOpen)
            loadMenu.SetActive(false);
    }
}
