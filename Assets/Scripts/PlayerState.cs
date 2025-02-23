using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerState : MonoBehaviour
{
    public static PlayerState Instance { get; set; }

    //---- Plyer health -----//
    public float currentHealth;
    public float maxHealth;
    //---- Plyer Calories -----//
    public float currentCalories;
    public float maxCalories;

    float distanceTravelled = 0;
    Vector3 lastPosition;
    public GameObject playerBody;

    //---- Plyer Hydration -----//
    public float currentHydrationPercent;
    public float maxHydrationPercent;
    public bool isHydrationActivce;


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

    void Start()
    {
        isHydrationActivce = true;
        currentHealth = maxHealth;
        currentCalories = maxCalories;
        currentHydrationPercent = maxHydrationPercent;
        StartCoroutine(decreaseHydration());
    }
    IEnumerator decreaseHydration()
    {
        //condition when don't decrease ( now is true)
        while (isHydrationActivce)
        {
            currentHydrationPercent -= 1;
            yield return new WaitForSeconds(5);
        }
    }

    // Update is called once per frame
    void Update()
    {

        distanceTravelled += Vector3.Distance(playerBody.transform.position, lastPosition);
        lastPosition = playerBody.transform.position;
        //5 meters(units) distance
        if (distanceTravelled >= 5)
        {
            distanceTravelled = 0;
            currentCalories -= 1;
        }

        //testing health bar
        if (Input.GetKeyDown(KeyCode.N))
        {
            currentHealth -= 10;
        }
    }
    public void setHealth(float newHealth)
    {
        currentHealth = newHealth;
    }
    public void setCalories(float newCalories)
    {
        currentCalories = newCalories;
    }
    public void setHydration(float newHydration)
    {
        currentHydrationPercent = newHydration;
    }
}
