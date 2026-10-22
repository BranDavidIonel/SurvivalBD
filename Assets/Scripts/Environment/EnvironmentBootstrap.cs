using UnityEngine;
using UnityEngine.SceneManagement;

// Starts the sky, the weather effects and the snow in every scene that has a DayNightSystem (the game scene),
// without editing the scene: they are made in code when the scene loads.
public static class EnvironmentBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SetUp();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SetUp();

    static void SetUp()
    {
        if (Object.FindObjectOfType<DayNightSystem>() == null) return;
        if (Object.FindObjectOfType<SkyLighting>() != null) return;

        var go = new GameObject("Environment FX (made in code)");
        go.AddComponent<SkyLighting>();
        go.AddComponent<WeatherEffects>();
        go.AddComponent<SnowCover>();

        // TEST ONLY: the test settings in the in-game Settings screen (remove for the release)
        DebugEnvironmentPanel.Attach();
    }
}
