using UnityEngine;

// The language of the story and of the game's messages (Settings > Language): English or Romanian.
// Kept on the device. Texts are written as Lang.T("English", "Română"); a text shown on the screen for a long time
// listens to Lang.Changed to change at once.
public static class Lang
{
    public enum Language { English = 0, Romanian = 1 }

    const string Key = "SurvivalBD.Language";
    static int loaded = -1;

    public static event System.Action Changed;

    public static Language Current
    {
        get
        {
            if (loaded < 0) loaded = Mathf.Clamp(PlayerPrefs.GetInt(Key, (int)Language.English), 0, 1);
            return (Language)loaded;
        }
        set
        {
            if (loaded == (int)value) return;
            loaded = (int)value;
            PlayerPrefs.SetInt(Key, loaded);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }
    }

    public static bool Ro => Current == Language.Romanian;

    // the text in the chosen language (the English one when there is no Romanian one)
    public static string T(string en, string ro) => Ro && !string.IsNullOrEmpty(ro) ? ro : en;
}
