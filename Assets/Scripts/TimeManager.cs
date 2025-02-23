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
    private int daysPerSeason = 30;
    private int daysInCurrentSeason = 1;
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
        dayUI.text = $"Day: {daysInCurrentSeason}, Season:{currentSeason}, Year:{yearInGame}";
    }


}
