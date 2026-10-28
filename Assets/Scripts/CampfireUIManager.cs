using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// The campfire window: a fuel slot (wood), a food slot, "Cook" and "Light Fire" / "Add Fuel".
// The "Light Fire" button, the fire's status line, the fuel bar and the slot labels are made in code the
// first time (copies of the Cook button's look), so the scene does not need editing.
public class CampfireUIManager : MonoBehaviour
{
    public static CampfireUIManager Instance { get; set; }
    public Button cookButton;
    public Button exitButton;

    public GameObject foodSlot;
    public GameObject fuelSlot;

    public GameObject campfirePanel;
    public bool isUiOpen;

    public Campfire selectedCampfire;

    public CookingData cookingData;

    // the dry grass that lights a fire that is out (the wood alone does not catch fire)
    public const string HayItem = "hay";

    // made in code
    Button lightFireButton;
    Text lightFireLabel;
    Text fireStatus;
    Text fireNote;
    Image fuelBar;
    Text fuelLabel;
    Text foodLabel;

    static readonly Color FireColor = new Color(1f, 0.72f, 0.3f);
    static readonly Color OutColor = new Color(0.75f, 0.72f, 0.68f);
    static readonly Color NoteColor = new Color(0.62f, 0.78f, 1f);

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

    private void Start()
    {
        BuildExtraUI();
    }

    private void Update()
    {
        if (!isUiOpen || selectedCampfire == null)
        {
            cookButton.interactable = false;
            return;
        }

        var fire = selectedCampfire;
        InventoryItem fuel = fuelSlot.GetComponentInChildren<InventoryItem>();
        InventoryItem food = foodSlot.GetComponentInChildren<InventoryItem>();
        bool fuelOk = fuel != null && cookingData.IsBurnable(fuel);
        bool foodOk = food != null && cookingData.validFoods.Any(cookableFood => cookableFood.name == food.thisName);

        // food finished cooking while the window is open
        TakeReadyFood(fire);

        // lighting a fire that is out needs hay (dry grass) as well as the wood
        bool hasHay = InventorySystem.Instance.CountItem(HayItem) > 0;
        bool canLight = fire.IsBurning || hasHay;

        // Cook: needs food, and a burning fire or wood (and hay) to light it with
        cookButton.interactable = foodOk && !fire.isCooking && (fire.IsBurning || (fuelOk && hasHay));

        // Light Fire / Add Fuel: needs wood in the fuel slot, and room on the fire for all of it
        // (a full fire lasts a whole night when it does not rain: that is the limit)
        bool fits = fuelOk && WoodFits(fire, fuel);
        bool full = fire.IsFull || (fuelOk && !fits);
        if (lightFireButton != null)
        {
            lightFireButton.interactable = fuelOk && fits && canLight;
            lightFireLabel.text = full ? Lang.T("Fire is full", "Focul e plin") : fire.IsBurning ? Lang.T("Add Fuel", "Pune lemne") : Lang.T("Light Fire", "Aprinde focul");
        }

        // status of the fire
        if (fireStatus != null)
        {
            if (fire.IsBurning)
            {
                float seconds = fire.RealSecondsLeft;
                string real = float.IsInfinity(seconds) ? "" : " (" + Mathf.CeilToInt(seconds) + " s)";
                fireStatus.text = Lang.T("Fire: burning, ", "Focul: arde, încă ") + FormatGameMinutes(fire.fuelMinutes) + Lang.T(" left", "") + real;
                fireStatus.color = FireColor;
            }
            else
            {
                fireStatus.text = Lang.T("Fire: out", "Focul: stins");
                fireStatus.color = OutColor;
            }
            fuelBar.fillAmount = Mathf.Clamp01(fire.fuelMinutes / Mathf.Max(1f, fire.maxFuelMinutes));
            float speed = fire.BurnSpeed;
            var w = WeatherSystem.Instance;
            if (full)
            {
                fireNote.text = Lang.T("The fire is full: it can't take more wood now", "Focul e plin: nu mai încap lemne acum");
            }
            else if (speed > 1.05f && w != null)
            {
                string why = w.Rain > 0.1f ? Lang.T("Rain", "Ploaia") : w.Snow > 0.1f ? Lang.T("Snow", "Ninsoarea") : Lang.T("Wind", "Vântul");
                fireNote.text = why + Lang.T(": the wood burns ", ": lemnele ard de ") + speed.ToString("0.0") + Lang.T("x faster", "x mai repede");
            }
            else
            {
                fireNote.text = fire.IsBurning ? "" : !hasHay ? Lang.T("To light it you need wood and 1 hay (pick dry grass)", "Ca să-l aprinzi îți trebuie lemne și 1 fân (smulge iarbă uscată)") : Lang.T("Put wood in the fuel slot (+1 hay to light it)", "Pune lemne în locul pentru lemne (+1 fân ca să-l aprinzi)");
            }
        }

        if (fuelLabel != null)
        {
            if (fuel == null) fuelLabel.text = Lang.T("Fuel (wood)", "Lemne");
            else if (fuelOk && !fits) fuelLabel.text = Lang.T("Room left: ", "Mai încap: ") + FormatGameMinutes(Mathf.Max(0f, fire.maxFuelMinutes - fire.fuelMinutes));
            else if (fuelOk) fuelLabel.text = Translations.Name(fuel.thisName) + ": +" + FormatGameMinutes(cookingData.BurnMinutes(fuel));
            else fuelLabel.text = Lang.T("This does not burn", "Asta nu arde");
        }

        if (foodLabel != null)
        {
            if (fire.isCooking)
            {
                float total = Mathf.Max(0.01f, fire.foodBeingCooked != null ? fire.foodBeingCooked.timeToCook : 1f);
                int percent = Mathf.Clamp(Mathf.RoundToInt((1f - fire.cookingTimer / total) * 100f), 0, 99);
                foodLabel.text = fire.IsBurning ? Lang.T("Cooking ", "Se gătește ") + percent + "%" : Lang.T("Fire is out: cooking stopped", "Focul s-a stins: gătitul s-a oprit");
            }
            else if (food != null && !foodOk) foodLabel.text = Lang.T("This cannot be cooked", "Asta nu se gătește");
            else foodLabel.text = Lang.T("Food", "Mâncare");
        }
    }

    // "2h 15m" / "45m" (game time)
    public static string FormatGameMinutes(float minutes)
    {
        int m = Mathf.CeilToInt(Mathf.Max(0f, minutes));
        int h = m / 60;
        m %= 60;
        if (h <= 0) return m + "m";
        return m == 0 ? h + "h" : h + "h " + m + "m";
    }

    public void CookButtonPressed()
    {
        if (selectedCampfire == null) return;
        InventoryItem food = foodSlot.GetComponentInChildren<InventoryItem>();
        if (food == null) return;

        // the fire is out: light it with the wood from the fuel slot and 1 hay first
        if (!selectedCampfire.IsBurning)
        {
            InventoryItem fuel = fuelSlot.GetComponentInChildren<InventoryItem>();
            if (fuel == null || !cookingData.IsBurnable(fuel)) return;
            if (!UseHay()) return;
            selectedCampfire.AddFuel(cookingData.BurnMinutes(fuel));
            Destroy(fuel.gameObject);
        }

        selectedCampfire.StartCooking(food);
        Destroy(food.gameObject);
    }

    // one hay from the inventory or the quick bar lights the wood; false: there is none
    bool UseHay()
    {
        if (InventorySystem.Instance.CountItem(HayItem) <= 0)
        {
            InventorySystem.Instance.ShowMessage(Lang.T("You need hay to light the fire (pick dry grass)", "Îți trebuie fân ca să aprinzi focul (smulge iarbă uscată)"));
            return false;
        }
        InventorySystem.Instance.RemoveItem(HayItem, 1);
        return true;
    }

    // there is room on the fire for all of this wood
    bool WoodFits(Campfire fire, InventoryItem fuel)
    {
        return cookingData.BurnMinutes(fuel) <= fire.maxFuelMinutes - fire.fuelMinutes + 0.5f;
    }

    public void LightFireButtonPressed()
    {
        if (selectedCampfire == null) return;
        InventoryItem fuel = fuelSlot.GetComponentInChildren<InventoryItem>();
        if (fuel == null || !cookingData.IsBurnable(fuel) || !WoodFits(selectedCampfire, fuel)) return;
        if (!selectedCampfire.IsBurning && !UseHay()) return;
        if (selectedCampfire.AddFuel(cookingData.BurnMinutes(fuel)))
        {
            Destroy(fuel.gameObject);
            if (SoundManager.Instance != null) SoundManager.Instance.PlaySound(SoundManager.Instance.dropItemSound);
        }
    }

    // cooked food waiting at this campfire goes into the food slot (when it is free)
    public void TakeReadyFood(Campfire fire)
    {
        if (fire == null || string.IsNullOrEmpty(fire.readyFood)) return;
        if (foodSlot.transform.childCount > 0) return;
        var prefab = Resources.Load<GameObject>(fire.readyFood);
        fire.readyFood = "";
        if (prefab == null) return;
        GameObject rf = Instantiate(prefab, foodSlot.transform.position, foodSlot.transform.rotation);
        rf.transform.SetParent(foodSlot.transform);
    }

    // the campfire window opens the inventory next to it, and closes it again if it was closed before
    bool inventoryOpenedByFire;

    public void OpenUI()
    {
        // the crafting window uses the same side of the screen
        if (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen) CraftingSystem.Instance.CloseUI();

        campfirePanel.SetActive(true);
        isUiOpen = true;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        SelectionManager.Instance.DisableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = false;


        inventoryOpenedByFire = !InventorySystem.Instance.isOpen;
        InventorySystem.Instance.OpenUI();
    }
    public void CloseUI()
    {
        ReturnItemsToInventory();
        campfirePanel.SetActive(false);
        isUiOpen = false;

        if (inventoryOpenedByFire && InventorySystem.Instance.isOpen) InventorySystem.Instance.CloseUI();
        inventoryOpenedByFire = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        SelectionManager.Instance.EnableSelection();
        SelectionManager.Instance.GetComponent<SelectionManager>().enabled = true;
    }

    // Wood or food left in the campfire's slots goes back into the inventory when the window closes
    // (before, it stayed hidden in the slot and was lost from the inventory).
    void ReturnItemsToInventory()
    {
        var inventory = InventorySystem.Instance;
        if (inventory == null) return;
        bool moved = false;
        foreach (var slot in new[] { fuelSlot, foodSlot })
        {
            if (slot == null || slot.transform.childCount == 0) continue;
            var item = slot.transform.GetChild(0);
            GameObject free = inventory.slotList.FirstOrDefault(s => s != null && s.transform.childCount == 0);
            if (free == null) break; // inventory full: it stays in the slot
            item.SetParent(free.transform, false);
            item.localPosition = Vector3.zero;
            var inv = item.GetComponent<InventoryItem>();
            if (inv != null) inv.isInsideQuickSlot = false;
            moved = true;
        }
        if (moved)
        {
            inventory.RecalculateList();
            if (CraftingSystem.Instance != null) CraftingSystem.Instance.RefreshNeededItems();
        }
    }

    // ---------------- extra controls, made in code ---------------- //

    void BuildExtraUI()
    {
        if (cookButton == null || campfirePanel == null || lightFireButton != null) return;
        var cookRect = (RectTransform)cookButton.transform;
        var cookText = cookButton.GetComponentInChildren<Text>(true);
        Font font = cookText != null ? cookText.font : Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Cook and Light Fire side by side, where Cook was
        Vector2 at = cookRect.anchoredPosition;
        float width = 150f;
        cookRect.sizeDelta = new Vector2(width, cookRect.sizeDelta.y);
        cookRect.anchoredPosition = new Vector2(-width / 2f - 6f, at.y);

        var copy = Instantiate(cookButton.gameObject, cookButton.transform.parent);
        copy.name = "LightFireButton";
        lightFireButton = copy.GetComponent<Button>();
        lightFireButton.onClick = new Button.ButtonClickedEvent(); // the copy must not cook
        lightFireButton.onClick.AddListener(LightFireButtonPressed);
        var lightRect = (RectTransform)copy.transform;
        lightRect.anchoredPosition = new Vector2(width / 2f + 6f, at.y);
        lightFireLabel = copy.GetComponentInChildren<Text>(true);
        if (lightFireLabel != null) lightFireLabel.text = Lang.T("Light Fire", "Aprinde focul");
        if (cookText != null) cookText.fontSize = Mathf.Max(cookText.fontSize, 16);
        if (lightFireLabel != null) lightFireLabel.fontSize = Mathf.Max(lightFireLabel.fontSize, 16);

        var panel = (RectTransform)campfirePanel.transform;

        // status of the fire and a bar of the wood left, at the top of the window
        fireStatus = NewText("FireStatus", panel, font, 17, TextAnchor.MiddleCenter, new Vector2(0f, 158f), new Vector2(330f, 24f));
        var barBack = NewImage("FuelBar", panel, new Color(0f, 0f, 0f, 0.55f), new Vector2(0f, 139f), new Vector2(230f, 9f));
        fuelBar = NewImage("Fill", (RectTransform)barBack.transform, new Color(1f, 0.55f, 0.15f, 0.95f), Vector2.zero, Vector2.zero);
        var fillRect = (RectTransform)fuelBar.transform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(1.5f, 1.5f);
        fillRect.offsetMax = new Vector2(-1.5f, -1.5f);
        fuelBar.type = Image.Type.Filled;
        fuelBar.fillMethod = Image.FillMethod.Horizontal;
        fuelBar.sprite = WhiteSprite();
        // a hint in the empty space left of the fuel slot (two lines if needed)
        fireNote = NewText("FireNote", panel, font, 14, TextAnchor.MiddleCenter, new Vector2(-75f, 98f), new Vector2(200f, 40f));
        fireNote.horizontalOverflow = HorizontalWrapMode.Wrap;
        fireNote.color = NoteColor;

        // labels under the two slots
        var fuelRect = (RectTransform)fuelSlot.transform;
        var foodRect = (RectTransform)foodSlot.transform;
        fuelLabel = NewText("FuelLabel", panel, font, 15, TextAnchor.UpperCenter,
            fuelRect.anchoredPosition + new Vector2(0f, -fuelRect.sizeDelta.y / 2f - 14f), new Vector2(200f, 22f));
        foodLabel = NewText("FoodLabel", panel, font, 15, TextAnchor.UpperCenter,
            foodRect.anchoredPosition + new Vector2(0f, -foodRect.sizeDelta.y / 2f - 14f), new Vector2(220f, 22f));
        fuelLabel.color = foodLabel.color = new Color(0.92f, 0.88f, 0.8f);

        // the X button stays on top of everything
        if (exitButton != null) exitButton.transform.SetAsLastSibling();
    }

    static Sprite whiteSprite;
    static Sprite WhiteSprite()
    {
        if (whiteSprite == null)
        {
            var t = Texture2D.whiteTexture;
            whiteSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f));
        }
        return whiteSprite;
    }

    static Text NewText(string name, RectTransform parent, Font font, int fontSize, TextAnchor alignment, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var text = go.AddComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(1f, -1f);
        return text;
    }

    static Image NewImage(string name, RectTransform parent, Color color, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }
}
