using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    private Slider slider;
    public Text healthCounter;

    public GameObject playerState;

    private float currentHealth, maxHealth;
    void Awake()
    {

        slider = GetComponent<Slider>();
    }

    void Update()
    {
        currentHealth = playerState.GetComponent<PlayerState>().currentHealth;
        maxHealth= playerState.GetComponent<PlayerState>().maxHealth;
        float fillValue = currentHealth / maxHealth;//0 or 1 , betwen 0.9 etc
        slider.value = fillValue;
        healthCounter.text = currentHealth + "/" + maxHealth;



    }
}
