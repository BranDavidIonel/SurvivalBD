using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A campfire the player placed. It burns as long as it has wood (fuel, counted in game minutes):
// "Light Fire" / "Add Fuel" in the campfire window puts the wood from the fuel slot on the fire.
// It burns faster in the rain, in the snow and in strong wind. Food only cooks while it burns.
// The flames, sparks, smoke, light and sound are made by CampfireFX.
public class Campfire : MonoBehaviour
{
    public bool playerInRange;
    public bool isCooking;
    public float cookingTimer;

    public CookableFood foodBeingCooked;
    public string readyFood;

    [Tooltip("The old fire effect of the model (not used any more: CampfireFX draws the fire)")]
    public GameObject fire;

    [Header("Fire")]
    [Tooltip("Game minutes the fire can still burn")]
    public float fuelMinutes;
    [Tooltip("Game minutes of wood burnt in this fire so far (the ash in the pit grows with it)")]
    public float burnedMinutes;
    [Tooltip("Most wood the fire can hold (game hours). 14: a full fire lasts the longest (winter) night when it does not rain")]
    public float maxFuelHours = 14f;
    public float maxFuelMinutes => maxFuelHours * 60f;
    [Tooltip("In full rain the wood burns this many times faster")]
    public float rainBurnFaster = 2.5f;
    [Tooltip("In full snowfall the wood burns this many times faster")]
    public float snowBurnFaster = 1.6f;
    [Tooltip("In a storm wind the wood burns this many times faster")]
    public float windBurnFaster = 1.3f;

    public bool IsBurning => fuelMinutes > 0f;

    // how many times faster than normal the wood burns now (weather)
    public float BurnSpeed
    {
        get
        {
            var w = WeatherSystem.Instance;
            if (w == null) return 1f;
            float speed = 1f + w.Rain * (rainBurnFaster - 1f);
            speed *= 1f + w.Snow * (snowBurnFaster - 1f);
            speed *= 1f + Mathf.Clamp01((w.Wind - 0.4f) / 0.6f) * (windBurnFaster - 1f);
            return speed;
        }
    }

    // 0..1: how big the flames are (small when the wood is nearly gone, and in the rain)
    public float FireStrength
    {
        get
        {
            if (!IsBurning) return 0f;
            // more wood, bigger flames (full size from about 3 hours of wood), small when nearly gone
            float wood = Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(fuelMinutes / 180f));
            float rain = WeatherSystem.Instance != null ? WeatherSystem.Instance.Rain : 0f;
            float snow = WeatherSystem.Instance != null ? WeatherSystem.Instance.Snow : 0f;
            return wood * (1f - 0.3f * rain) * (1f - 0.15f * snow);
        }
    }

    // real seconds until it goes out (with the weather and the speed of the clock now)
    public float RealSecondsLeft
    {
        get
        {
            float minutesPerSecond = DayNightSystem.WeatherHoursPerSecond * 60f * BurnSpeed;
            return minutesPerSecond > 0f ? fuelMinutes / minutesPerSecond : float.PositiveInfinity;
        }
    }

    // shown when the player looks at the campfire
    public string InteractionText
    {
        get
        {
            if (isCooking) return IsBurning ? "Campfire (cooking)" : "Campfire (fire out)";
            if (!string.IsNullOrEmpty(readyFood)) return "Campfire (food ready)";
            return IsBurning ? "Campfire (burning)" : "Campfire";
        }
    }

    public bool IsFull => fuelMinutes >= maxFuelMinutes - 0.5f;

    CampfireFX fx;

    // The wood lying in the fire: the sticks of the model, shown one by one as wood is put on the fire
    // (a new campfire is just the ring of stones) and gone one by one as it burns away.
    Transform[] woodPieces = new Transform[0];
    Vector3[] woodScales;
    float[] woodShown;

    private void Start()
    {
        if (fire != null) fire.SetActive(false); // the old effect
        fx = GetComponent<CampfireFX>();
        if (fx == null) fx = gameObject.AddComponent<CampfireFX>();
        fx.Init(this); // before the sticks are hidden: the fire's place is measured on them
        SetUpWood();
    }

    void SetUpWood()
    {
        var pieces = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Stick")) pieces.Add(child);
        }
        pieces.Sort((a, b) => a.localPosition.y.CompareTo(b.localPosition.y)); // the bottom ones first
        woodPieces = pieces.ToArray();
        woodScales = new Vector3[woodPieces.Length];
        woodShown = new float[woodPieces.Length];
        for (int i = 0; i < woodPieces.Length; i++)
        {
            woodScales[i] = woodPieces[i].localScale;
            woodShown[i] = 0f;
            woodPieces[i].gameObject.SetActive(false);
        }
        UpdateWood(true);
    }

    // how many sticks lie in the fire: one more for every 1/6 of a full fire (at least one while it burns)
    void UpdateWood(bool instant)
    {
        int count = woodPieces.Length;
        if (count == 0) return;
        int wanted = IsBurning ? Mathf.Clamp(Mathf.CeilToInt(fuelMinutes / maxFuelMinutes * count), 1, count) : 0;
        for (int i = 0; i < count; i++)
        {
            float target = i < wanted ? 1f : 0f;
            // a new stick grows into place, a burnt one shrinks away
            woodShown[i] = instant ? target : Mathf.MoveTowards(woodShown[i], target, Time.deltaTime / (target > 0f ? 0.35f : 1.5f));
            bool on = woodShown[i] > 0.001f;
            var piece = woodPieces[i];
            if (piece == null) continue;
            if (piece.gameObject.activeSelf != on) piece.gameObject.SetActive(on);
            if (on) piece.localScale = woodScales[i] * Mathf.SmoothStep(0.25f, 1f, woodShown[i]);
        }
    }

    // the middle of the fire (the model's pivot is far from it)
    public Vector3 FireCentre => fx != null ? fx.Centre : transform.position;

    private void Update()
    {
        float distance = Vector3.Distance(PlayerState.Instance.playerBody.transform.position, FireCentre);
        if (distance < 10f)
        {
            playerInRange = true;
        }
        else
        {
            playerInRange = false;
        }

        // the wood burns away (in game time)
        if (IsBurning)
        {
            float burnt = Mathf.Min(fuelMinutes, Time.deltaTime * DayNightSystem.WeatherHoursPerSecond * 60f * BurnSpeed);
            fuelMinutes -= burnt;
            burnedMinutes += burnt;
            if (fuelMinutes < 0f) fuelMinutes = 0f;
        }

        // food cooks only while the fire burns
        if (isCooking && IsBurning)
        {
            cookingTimer -= Time.deltaTime;
        }

        if(cookingTimer <= 0 && isCooking)
        {
            isCooking = false;
            readyFood = GetCookedFood(foodBeingCooked);
        }

        UpdateWood(false);
    }

    private string GetCookedFood(CookableFood food)
    {
        return food.cookedFoodName;
    }

    // Puts wood on the fire (lights it if it was out). Returns false when the fire is already full.
    public bool AddFuel(float minutes)
    {
        if (minutes <= 0f || IsFull) return false;
        fuelMinutes = Mathf.Min(maxFuelMinutes, fuelMinutes + minutes);
        return true;
    }

    public void OpenUI()
    {
        CampfireUIManager.Instance.OpenUI();
        CampfireUIManager.Instance.selectedCampfire = this;
        CampfireUIManager.Instance.TakeReadyFood(this);
    }

    public void StartCooking(InventoryItem food)
    {
        foodBeingCooked = ConvertIntoCookable(food);
        isCooking = true;
        cookingTimer = TimeToCookFood(foodBeingCooked);
    }

    private CookableFood ConvertIntoCookable(InventoryItem food)
    {
        foreach(CookableFood cookable in CampfireUIManager.Instance.cookingData.validFoods)
        {
            if(cookable.name == food.thisName)
            {
                return cookable;
            }
        }
        return new CookableFood();//this wiil never happen
    }

    private float TimeToCookFood(CookableFood food)
    {
        return food.timeToCook;
    }
}
