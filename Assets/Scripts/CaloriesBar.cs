using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CaloriesBar : MonoBehaviour
{
    private Slider slider;
    public Text caloriesCounter;

    public GameObject playerState;

    private float currenCalories, maxCalories;
    void Awake()
    {

        slider = GetComponent<Slider>();
    }

    void Update()
    {
        currenCalories = playerState.GetComponent<PlayerState>().currentCalories;
        maxCalories = playerState.GetComponent<PlayerState>().maxCalories;
        float fillValue = currenCalories / maxCalories;//0 or 1 , betwen 0.9 etc
        slider.value = fillValue;
        caloriesCounter.text = currenCalories + "/" + maxCalories;

    }
}
