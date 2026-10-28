using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; set; }

    public enum Season
    {
        Spring,
        Summer,
        Fall, 
        Winter
    }
    public Season currentSeason = Season.Spring;
    //private int daysPerSeason = 30;
    private int daysPerSeason = 2;//test
    private int daysInCurrentSeason = 1;

    public enum DayOfWeek
    {
        Monday,//0 is index
        Tuesday,
        Wednesday,
        Thursday,
        Friday,
        Saturday,
        Sunday//6
    }
    public DayOfWeek currentDayOfWeek = DayOfWeek.Monday;
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    public int dayInGame = 1;
    public int yearInGame = 0;
    public TextMeshProUGUI dayUI;
    private void Start()
    {
        updateUI();
    }

    public void TriggerNextDay()
    {
        dayInGame += 1;
        daysInCurrentSeason += 1;
        currentDayOfWeek = (DayOfWeek)((int)(currentDayOfWeek + 1) % 7);
        if(daysInCurrentSeason > daysPerSeason)
        {
            //swich to nest season
            daysInCurrentSeason = 1;
            currentSeason = GetNextSeason();
        }
        updateUI();
    }
    private Season GetNextSeason()
    {
        int currentSeasonIndex = (int)currentSeason;//0 -> Spring 
        int nextSeasonIndex = (currentSeasonIndex + 1) % 4;
        //increase the year
        if(nextSeasonIndex == 0)
        {
            yearInGame += 1;
        }
        return (Season)nextSeasonIndex;
    }
    private void updateUI()
    {
        if (dayUI == null) return;
        if (Lang.Ro)
        {
            // "Luni, ziua 1, Primăvara, anul 0"
            string[] days = { "Luni", "Marți", "Miercuri", "Joi", "Vineri", "Sâmbătă", "Duminică" };
            string[] seasons = { "Primăvara", "Vara", "Toamna", "Iarna" };
            dayUI.text = days[(int)currentDayOfWeek] + ", ziua " + daysInCurrentSeason + ", " + seasons[(int)currentSeason] + ", anul " + yearInGame;
        }
        else dayUI.text = $"{currentDayOfWeek} day: {daysInCurrentSeason}, Season:{currentSeason}, Year:{yearInGame}";
    }

    // the line in the language of the Settings at once
    void OnEnable() => Lang.Changed += updateUI;
    void OnDisable() => Lang.Changed -= updateUI;

    public int DaysPerSeason => daysPerSeason;
    public int DayInSeason => daysInCurrentSeason;

    // TEST (in-game Settings > Test panel): jump to another season, starting on its first day
    public void SetSeason(Season season)
    {
        currentSeason = season;
        daysInCurrentSeason = 1;
        updateUI();
    }


}
