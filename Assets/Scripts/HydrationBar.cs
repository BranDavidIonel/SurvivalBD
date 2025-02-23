using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HydrationBar : MonoBehaviour
{
    private Slider slider;
    public Text hydrationCounter;

    public GameObject playerState;

    private float currenHydration, maxHydration;
    void Awake()
    {

        slider = GetComponent<Slider>();
    }

    void Update()
    {
        currenHydration = playerState.GetComponent<PlayerState>().currentHydrationPercent;
        maxHydration = playerState.GetComponent<PlayerState>().maxHydrationPercent;
        float fillValue = currenHydration / maxHydration;//0 or 1 , betwen 0.9 etc
        slider.value = fillValue;
        hydrationCounter.text = currenHydration + "%";

    }
}
