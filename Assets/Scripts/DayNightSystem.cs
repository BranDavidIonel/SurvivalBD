using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class DayNightSystem : MonoBehaviour
{
    public Light directionlLight;

    public float dayDurationInSeconds = 24.0f; //adjust the duration of a full dau in seconds
    public int curretHour;
    float currentTimeOfDay = 0.35f; // 35 equals to 8 in the morning

    float blendedValue = 0.0f;

    bool lockNextDayTrigger = false;

    public List<SkyboxTimeMapping> timeMapping;

    public TextMeshProUGUI timeUI;

    public WeatherSystem weatherSystem;
    
    private void Update()
    {
        //calculatte the current time of day base on the game time
        currentTimeOfDay += Time.deltaTime / dayDurationInSeconds;
        currentTimeOfDay %= 1; // ensure it stays between 0 and 1
        curretHour = Mathf.FloorToInt(currentTimeOfDay * 24);

        timeUI.text = $"{curretHour}:00";

        //update the directionl light's rotation
        directionlLight.transform.rotation = Quaternion.Euler(new Vector3((currentTimeOfDay * 360) - 90, 170, 0));

        //update the skybox material based on the time of day
        if (weatherSystem.isSpecialWeather == false)
        {
            UpdateSkybox();
        }
        if (curretHour == 0 && lockNextDayTrigger == false)
        {
            TimeManager.Instance.TriggerNextDay();
            lockNextDayTrigger = true;
        }

        if (curretHour != 0)
        {
            lockNextDayTrigger = false;
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