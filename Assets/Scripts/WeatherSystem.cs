using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WeatherSystem : MonoBehaviour
{
    [Range(0f,1f)]
    public float chanceToRainSpring = 0.3f;//30%
    [Range(0f, 1f)]
    public float chanceToRainSummer = 0.00f;
    [Range(0f, 1f)]
    public float chanceToRainFall = 0.4f;
    [Range(0f, 1f)]
    public float chanceToRainWinter = 0.7f;//70%

    public GameObject rainEffect;
    public Material rainSkyBox;

    public bool isSpecialWeather;

    public enum WeatherCondition
    {
        Sunny,
        Rainy
    }

    private WeatherCondition currentWeatherCondition = WeatherCondition.Sunny;
    private void Start()
    {
        //is not good i don't have this method ( video 36 Day night system..)
        //TimeManager.Instance.OnDayPass.AddListener(GenerateRandomWeather);
        GenerateRandomWeather();
    }
    private void GenerateRandomWeather()
    {
        TimeManager.Season currentSeason = TimeManager.Instance.currentSeason;
        float chanceToRain = 0f;
        switch (currentSeason)
        {
            case TimeManager.Season.Spring:
                chanceToRain = chanceToRainSpring;
                break;
            case TimeManager.Season.Summer:
                chanceToRain = chanceToRainSummer;
                break;
            case TimeManager.Season.Fall:
                chanceToRain = chanceToRainFall;
                break;
            case TimeManager.Season.Winter:
                chanceToRain = chanceToRainWinter;
                break;
        }
        //generate random number for the chance of rain
        if(Random.value <= chanceToRain)
        {
            currentWeatherCondition = WeatherCondition.Rainy;
            isSpecialWeather = true;
            
            Invoke("StartRain", 1f);
            //StartRain();
        }
        else
        {
            currentWeatherCondition = WeatherCondition.Sunny;
            isSpecialWeather = false;
            StopRain();
        }
    }
    private void StartRain()
    {
        RenderSettings.skybox = rainSkyBox;
        rainEffect.SetActive(true);
    }

    private void StopRain()
    {
        rainEffect.SetActive(false);
    }


}
