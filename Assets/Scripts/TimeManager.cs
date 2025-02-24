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
        dayUI.text = $"{currentDayOfWeek} day: {daysInCurrentSeason}, Season:{currentSeason}, Year:{yearInGame}";
    }


}
