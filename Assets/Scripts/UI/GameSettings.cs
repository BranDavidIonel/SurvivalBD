using UnityEngine;

// The player's choices in Settings that change the game (kept on the device, PlayerPrefs):
//   Difficulty  Easy / Normal / Hard: how fast hunger and thirst grow, how long a torch burns, how much time the
//               shadow (at night, and in the dark of the cave) gives before it takes the player.
// The sound volumes are kept by SoundManager ("Volume"), the admin (TEST) settings are behind a password.
public static class GameSettings
{
    public enum Level { Easy = 0, Normal = 1, Hard = 2 }

    const string Key = "SurvivalBD.Difficulty";
    static int loaded = -1;

    public static Level Difficulty
    {
        get
        {
            if (loaded < 0) loaded = Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Level.Normal), 0, 2);
            return (Level)loaded;
        }
        set
        {
            loaded = (int)value;
            PlayerPrefs.SetInt(Key, loaded);
            PlayerPrefs.Save();
        }
    }

    // hunger and thirst grow this many times as fast (also the harm when they are empty)
    public static float Hunger => Pick(0.7f, 1f, 1.35f);
    // a torch burns down this many times as fast
    public static float TorchBurn => Pick(0.75f, 1f, 1.3f);
    // the time the shadow gives (at night without a fire, and in the dark of the cave) is this many times as long
    public static float ShadowTime => Pick(1.6f, 1f, 0.7f);

    public static string Describe(Level level)
    {
        switch (level)
        {
            case Level.Easy: return Lang.T("Food and water last longer, torches burn longer, the shadow is slower.", "Hrana și apa țin mai mult, torțele ard mai mult, umbra e mai înceată.");
            case Level.Hard: return Lang.T("Hunger and thirst come faster, torches burn out sooner, the shadow is quick.", "Foamea și setea vin mai repede, torțele se sting mai repede, umbra e iute.");
            default: return Lang.T("The game as it is meant to be played.", "Jocul așa cum a fost gândit.");
        }
    }

    // the name of a difficulty (romanian: true gives the Romanian word)
    public static string Name(Level level, bool romanian)
    {
        if (!romanian) return level.ToString();
        return level == Level.Easy ? "Ușor" : level == Level.Hard ? "Greu" : "Normal";
    }

    static float Pick(float easy, float normal, float hard)
    {
        switch (Difficulty)
        {
            case Level.Easy: return easy;
            case Level.Hard: return hard;
            default: return normal;
        }
    }
}
