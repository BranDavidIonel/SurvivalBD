using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Health, food (calories) and water of the player.
//  - water: a full bar lasts one game day (drink at the river: FreshWater)
//  - food: a full bar lasts about three game days (walking and chopping use a bit more)
//  - an empty bar hurts: health goes down fast (about a game hour and a half), then the player dies
// The bars follow the game clock (the TEST time speed makes them go faster too).
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

    [Header("Hunger and thirst (game time)")]
    [Tooltip("Game days a full water bar lasts")]
    public float waterDays = 1f;
    [Tooltip("Game days a full food bar lasts when resting (walking and chopping use more: about 3 days in all)")]
    public float foodDays = 3.6f;
    [Tooltip("Metres of walking that cost one calorie")]
    public float metresPerCalorie = 12f;
    [Tooltip("Game hours from full health to dead when the food or the water bar is empty")]
    public float starveHours = 1.5f;

    float warnTimer;

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
        lastPosition = playerBody.transform.position;
    }

    // Update is called once per frame
    void Update()
    {
        float hours = Time.deltaTime * DayNightSystem.GameHoursPerSecond;
        bool dead = currentHealth <= 0f;

        // walking uses food (a teleport, like loading a game, does not count)
        float moved = Vector3.Distance(playerBody.transform.position, lastPosition);
        if (moved < 20f) distanceTravelled += moved;
        lastPosition = playerBody.transform.position;
        if (distanceTravelled >= metresPerCalorie)
        {
            distanceTravelled -= metresPerCalorie;
            currentCalories -= 1;
        }

        if (!dead && hours > 0f)
        {
            if (isHydrationActivce) currentHydrationPercent -= maxHydrationPercent * hours / (24f * Mathf.Max(0.05f, waterDays));
            currentCalories -= maxCalories * hours / (24f * Mathf.Max(0.05f, foodDays));
        }
        currentCalories = Mathf.Clamp(currentCalories, 0f, maxCalories);
        currentHydrationPercent = Mathf.Clamp(currentHydrationPercent, 0f, maxHydrationPercent);

        // an empty bar: health goes down fast
        bool hungry = currentCalories <= 0f;
        bool thirsty = currentHydrationPercent <= 0f;
        if (!dead && (hungry || thirsty) && hours > 0f)
        {
            float loss = maxHealth * hours / Mathf.Max(0.05f, starveHours) * ((hungry ? 1f : 0f) + (thirsty ? 1f : 0f));
            currentHealth = Mathf.Max(0f, currentHealth - loss);
            warnTimer -= Time.deltaTime;
            if (warnTimer <= 0f && InventorySystem.Instance != null)
            {
                warnTimer = 4f;
                InventorySystem.Instance.ShowMessage(thirsty ? "You are dying of thirst! Drink at the river or the lake!" : "You are starving! Eat something!");
            }
            if (currentHealth <= 0f)
            {
                var death = playerBody.GetComponent<PlayerDeath>();
                if (death == null) death = FindObjectOfType<PlayerDeath>();
                if (death != null) death.SetNextDeathMessage(thirsty ? "You died of thirst..." : "You died of hunger...");
            }
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
