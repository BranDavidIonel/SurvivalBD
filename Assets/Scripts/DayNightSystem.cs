using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// The clock of the game: time of day, the time shown on screen and the start of each new day.
// The sky, sun, moon and stars are drawn by SkyLighting (made in code at the start of the game).
// "Use New Sky" off = the old way again (one skybox material per hour from Time Mapping).
public class DayNightSystem : MonoBehaviour
{
    public static DayNightSystem Instance { get; private set; }

    public Light directionlLight;

    public float dayDurationInSeconds = 24.0f; //adjust the duration of a full dau in seconds
    public int curretHour;
    float currentTimeOfDay = 0.35f; // 35 equals to 8 in the morning

    float blendedValue = 0.0f;

    public List<SkyboxTimeMapping> timeMapping;

    public TextMeshProUGUI timeUI;

    public WeatherSystem weatherSystem;

    [Tooltip("On: the new sky (sun, moon, stars, clouds that follow the weather). Off: the old skybox materials above.")]
    public bool useNewSky = true;

    // ---------- TEST settings (changed from the in-game Settings > Test panel; remove for release) ---------- //
    [Header("TEST")]
    [Tooltip("TEST: off = the clock stops (the sun stays where it is)")]
    public bool autoTime = true;
    [Tooltip("TEST: how fast the time goes. 1 = normal (one day = Day Duration In Seconds), 10 = ten times faster")]
    public float timeSpeed = 1f;

    // time of day: 0 = midnight, 0.5 = noon
    public float TimeOfDay
    {
        get => currentTimeOfDay;
        set => currentTimeOfDay = Mathf.Repeat(value, 1f);
    }

    // 0 .. 24 (with the minutes, 13.5 = 13:30)
    public float Hour => currentTimeOfDay * 24f;

    // How many game hours pass in one real second (0 while the clock is stopped).
    public static float GameHoursPerSecond
    {
        get
        {
            if (Instance == null) return 24f / 360f;
            if (!Instance.autoTime) return 0f;
            return 24f * Mathf.Max(0f, Instance.timeSpeed) / Mathf.Max(1f, Instance.dayDurationInSeconds);
        }
    }

    // Weather and fires keep going at the normal speed even while the clock is stopped (for testing).
    public static float WeatherHoursPerSecond
    {
        get
        {
            if (Instance == null) return 24f / 360f;
            if (!Instance.autoTime) return 24f / Mathf.Max(1f, Instance.dayDurationInSeconds);
            return GameHoursPerSecond;
        }
    }

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        // calculate the current time of day based on the game time
        if (autoTime)
        {
            currentTimeOfDay += Time.deltaTime * Mathf.Max(0f, timeSpeed) / Mathf.Max(1f, dayDurationInSeconds);
            // passed midnight: a new day (also when a very fast clock jumps over several days at once)
            int safety = 0;
            while (currentTimeOfDay >= 1f && safety++ < 30)
            {
                currentTimeOfDay -= 1f;
                if (TimeManager.Instance != null) TimeManager.Instance.TriggerNextDay();
            }
            currentTimeOfDay = Mathf.Repeat(currentTimeOfDay, 1f);
        }

        curretHour = Mathf.FloorToInt(currentTimeOfDay * 24);
        int minutes = Mathf.FloorToInt((currentTimeOfDay * 24f - curretHour) * 60f) / 10 * 10; // 10 minute steps
        timeUI.text = $"{curretHour}:{minutes:00}";

        if (useNewSky) return; // SkyLighting moves the sun and the moon

        //update the directionl light's rotation
        directionlLight.transform.rotation = Quaternion.Euler(new Vector3((currentTimeOfDay * 360) - 90, 170, 0));

        //update the skybox material based on the time of day
        if (weatherSystem == null || weatherSystem.isSpecialWeather == false)
        {
            UpdateSkybox();
        }
    }

    private void UpdateSkybox()
    {
        //find the appropriate skybox material fot the current hour
        Material currentSkybox = null;
        foreach(SkyboxTimeMapping mapping in timeMapping)
        {
            if(curretHour == mapping.hour)
            {
                currentSkybox = mapping.skyboxMaterial;
                if(currentSkybox.shader != null)
                {
                    if(currentSkybox.shader.name == "Custom/SkyboxTransition")
                    {
                        blendedValue += Time.deltaTime;
                        blendedValue = Mathf.Clamp01(blendedValue);
                        currentSkybox.SetFloat("_TransitionFactor", blendedValue);
                    }
                    else
                    {
                        blendedValue = 0;
                    }
                }

                break;
            }
        }



        if(currentSkybox != null)
        {
            RenderSettings.skybox = currentSkybox;
        }
    }
}
[System.Serializable]
public class SkyboxTimeMapping
{
    public string phaseName;
    public int hour;//the hour of the day (0,23)
    public Material skyboxMaterial;// the corresponding skybox material for this hour

}
