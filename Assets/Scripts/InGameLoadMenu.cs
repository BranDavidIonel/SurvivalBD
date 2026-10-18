using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Adds "Load Game" to the in-game menu (Save Game / Load Game / Settings / Quit), built at runtime from
// the existing buttons and the Save Game screen, so the scene does not have to be edited.
// Picking a saved slot reloads the game from it, the same way Load Game in the main menu does.
public class InGameLoadMenu : MonoBehaviour
{
    GameObject loadMenu;
    readonly TextMeshProUGUI[] slotTexts = new TextMeshProUGUI[4];

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
        if (mm == null || mm.menu == null || mm.saveMenu == null) return;

        var saveButton = mm.menu.transform.Find("SaveGameButton") as RectTransform;
        var settingsButton = mm.menu.transform.Find("SettingsButton") as RectTransform;
        var quitButton = mm.menu.transform.Find("ExitButton") as RectTransform;
        if (saveButton == null || settingsButton == null) return;

        // --- the Load Game screen: a copy of the Save Game screen whose slots load instead of save --- //
        loadMenu = Instantiate(mm.saveMenu, mm.saveMenu.transform.parent);
        loadMenu.name = "LoadGameMenu";
        loadMenu.SetActive(false);
        var overrideAlert = loadMenu.transform.Find("OverrideAlert");
        if (overrideAlert != null) Destroy(overrideAlert.gameObject);
        var title = loadMenu.transform.Find("Title")?.GetComponent<TMP_Text>();
        if (title != null) title.text = "Load Game";

        foreach (var saveSlot in loadMenu.GetComponentsInChildren<SaveSlot>(true))
        {
            int slot = saveSlot.slotNumber;
            var button = saveSlot.GetComponent<Button>();
            if (slot >= 0 && slot < slotTexts.Length) slotTexts[slot] = saveSlot.transform.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            DestroyImmediate(saveSlot); // never woke up (the copy is inactive), so it saves nothing
            button.onClick.AddListener(() =>
            {
                if (SaveManager.Instance != null && !SaveManager.Instance.IsSlotEmpty(slot))
                    SaveManager.Instance.StartLoadedGame(slot); // reloads the game scene from that save
            });
        }
        // (its Back button already returns to the menu: the copy's own links point to the copy)

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

        if (!loadMenu.activeInHierarchy || SaveManager.Instance == null) return;
        for (int slot = 0; slot < slotTexts.Length; slot++)
        {
            if (slotTexts[slot] == null) continue;
            string description = PlayerPrefs.GetString("Slot" + slot + "Description");
            if (string.IsNullOrEmpty(description)) description = "Saved Game " + slot; // older saves have no description
            slotTexts[slot].text = SaveManager.Instance.IsSlotEmpty(slot) ? "Empty" : description;
        }
    }
}
