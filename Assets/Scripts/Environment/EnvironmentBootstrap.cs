using UnityEngine;
using UnityEngine.SceneManagement;

// Starts the sky, the weather effects, the snow, the forests, the plants, the rabbits, the story and the forest /
// footstep sounds in every scene that has a DayNightSystem (the game scene), without editing the scene: they are
// made in code when the scene loads.
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
        go.AddComponent<AmbienceAudio>(); // forest sounds by day and night, the sea, an owl

        // the story first: a new game puts the player on the beach before the forest looks around them
        new GameObject("Story (made in code)").AddComponent<StoryManager>();

        var forest = new GameObject("Forest (made in code)");
        forest.AddComponent<Forest>();        // trees that grow, drop seeds, spread and can be chopped
        forest.AddComponent<SeedPlanting>();  // planting a seed where the player looks
        new GameObject("Vegetation (made in code)").AddComponent<Vegetation>();     // plants to pick, growing back
        new GameObject("Fresh water (made in code)").AddComponent<FreshWater>();    // drinking: river, lake, rain puddles
        new GameObject("Rabbits (made in code)").AddComponent<RabbitPopulation>(); // male / female, babies
        new GameObject("PC buttons and Help (made in code)").AddComponent<PcControls>(); // Inventory, Crafting, Menu, Help

        // the shadow that comes for the player at night when no fire burns near them
        // (its settings, like the 20 seconds, are in Assets/Resources/NightShadow.prefab)
        NightShadow.Create();

        // footsteps and swimming sounds on the player
        var player = Object.FindObjectOfType<PlayerMovement>();
        if (player != null && player.GetComponent<FootstepAudio>() == null) player.gameObject.AddComponent<FootstepAudio>();

        // TEST ONLY: the test settings in the in-game Settings screen (remove for the release)
        DebugEnvironmentPanel.Attach();
    }
}
