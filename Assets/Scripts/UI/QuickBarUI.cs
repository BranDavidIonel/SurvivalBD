using UnityEngine;
using UnityEngine.UI;

// The quick bar (slots 1-6) at the bottom centre of the screen, made bigger and clearer in code:
//  - the slot of the item held in the hand gets a gold frame
//  - every slot has a round white number button on its top-left corner (on the phone only while the slot holds
//    something): tap it to take the item in the hand, tap again to put it away; a campfire / box / wall starts
//    being placed, food is eaten (the same as tapping the item itself, but much easier to hit)
// The scene's number badges (numbersHolder/number0..5) are reused as these buttons.
public class QuickBarUI : MonoBehaviour
{
    static QuickBarUI instance;

    // something changed (an item moved, another slot was selected): redraw on the next frame
    public static void Changed()
    {
        if (instance != null) instance.dirty = true;
    }

    // top edge of the bar, in canvas units from the canvas centre (the windows stay above it)
    public static float TopInCanvas => instance != null ? instance.top : -400f;

    EquipSystem equip;
    RectTransform canvasRect, panel, holder;
    RectTransform[] slots;
    Image[] slotImages;
    Button[] buttons;
    Image[] buttonImages;
    Text[] labels;
    bool phone;
    bool dirty = true;
    float timer;
    float lastCanvasWidth = -1f;
    float top = -400f;

    public void Build(bool phone, RectTransform canvasRect)
    {
        instance = this;
        this.phone = phone;
        this.canvasRect = canvasRect;
        equip = EquipSystem.Instance;
        if (equip == null || equip.quickSlotsPanel == null) { enabled = false; return; }

        panel = (RectTransform)equip.quickSlotsPanel.transform;
        int count = equip.quickSlotsList.Count;
        slots = new RectTransform[count];
        slotImages = new Image[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = (RectTransform)equip.quickSlotsList[i].transform;
            slotImages[i] = slots[i].GetComponent<Image>();
            if (slotImages[i] != null)
            {
                slotImages[i].sprite = UISkin.Slot;
                slotImages[i].type = Image.Type.Sliced;
                slotImages[i].color = Color.white;
                slotImages[i].pixelsPerUnitMultiplier = 1f;
            }
            if (slots[i].GetComponent<SlotFitter>() == null) slots[i].gameObject.AddComponent<SlotFitter>();
        }

        // the number badges become round buttons, drawn above the slots
        buttons = new Button[count];
        buttonImages = new Image[count];
        labels = new Text[count];
        if (equip.numbersHolder != null)
        {
            holder = (RectTransform)equip.numbersHolder.transform;
            holder.SetParent(panel, false);
            holder.SetAsLastSibling();
            UISkin.Fill(holder);
            for (int i = 0; i < count; i++)
            {
                var number = holder.Find("number" + i) as RectTransform;
                if (number == null) continue;
                var image = number.GetComponent<Image>();
                if (image == null) image = number.gameObject.AddComponent<Image>();
                image.sprite = UISkin.Disc;
                image.type = Image.Type.Simple;
                image.raycastTarget = true;
                buttonImages[i] = image;

                var button = number.GetComponent<Button>();
                if (button == null) button = number.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                UISkin.SetTint(button);
                int index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => Press(index));
                buttons[i] = button;

                var label = number.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    UISkin.Style(label, phone ? 32 : 19, UISkin.Ink, TextAnchor.MiddleCenter);
                    label.text = (i + 1).ToString();
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var shadow = label.GetComponent<Shadow>();
                    if (shadow != null) shadow.enabled = false; // dark text on white: no shadow
                    UISkin.Fill(label.rectTransform);
                }
                labels[i] = label;
            }
        }

        Layout();
        Refresh();
    }

    // tap on a number button
    void Press(int index)
    {
        if (equip == null || index < 0 || index >= equip.quickSlotsList.Count) return;
        var slot = equip.quickSlotsList[index];
        if (slot.transform.childCount == 0) return;
        var item = slot.transform.GetChild(0).GetComponent<InventoryItem>();
        if (item != null) item.UseFromQuickSlot();
        else equip.ToggleQuickSlot(index);
        dirty = true;
    }

    void LateUpdate()
    {
        if (canvasRect != null && !Mathf.Approximately(canvasRect.rect.width, lastCanvasWidth)) Layout();

        timer += Time.unscaledDeltaTime;
        if (dirty || timer > 0.25f) Refresh();
    }

    void Layout()
    {
        if (panel == null || canvasRect == null) return;
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;
        lastCanvasWidth = canvasWidth;

        float gap = phone ? 18f : 12f;
        float button = phone ? 58f : 30f;
        float inset = phone ? 8f : 5f;
        float overhang = button * 0.5f - inset; // how far the number buttons stick out on the left and the top
        float slot = phone ? 120f : 92f;
        if (phone)
        {
            // keep clear of the Attack button (bottom right, made by MobileControls: 200 wide, 590 from the edge)
            float attackLeft = canvasWidth - 590f - 100f;
            float half = attackLeft - 24f - canvasWidth * 0.5f;
            float fit = (2f * half - 2f * overhang - 5f * gap) / 6f;
            slot = Mathf.Clamp(fit, 80f, 120f);
        }
        float bottom = phone ? 18f : 14f;
        int count = slots.Length;
        float rowWidth = count * slot + (count - 1) * gap;

        UISkin.Place(panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, bottom),
            new Vector2(rowWidth + 2f * overhang, slot + overhang));

        for (int i = 0; i < count; i++)
        {
            float x = -rowWidth * 0.5f + slot * 0.5f + i * (slot + gap);
            UISkin.Place(slots[i], new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(x, slot * 0.5f), new Vector2(slot, slot));
            var fitter = slots[i].GetComponent<SlotFitter>();
            if (fitter != null) fitter.Fit();

            if (buttons[i] != null)
            {
                UISkin.Place((RectTransform)buttons[i].transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                    new Vector2(x - slot * 0.5f + inset, slot - inset), new Vector2(button, button));
            }
        }
        if (holder != null) UISkin.Fill(holder);

        top = -canvasHeight * 0.5f + bottom + slot + overhang;
    }

    void Refresh()
    {
        dirty = false;
        timer = 0f;
        if (equip == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            bool has = slots[i].childCount > 0;
            bool selected = has && i == equip.selectedNumber && equip.selectedItem != null
                && equip.selectedItem.transform.parent == slots[i];

            if (slotImages[i] != null)
            {
                var sprite = selected ? UISkin.SlotSelected : UISkin.Slot;
                if (slotImages[i].sprite != sprite) slotImages[i].sprite = sprite;
            }
            if (buttons[i] == null) continue;

            bool show = phone ? has : true;
            if (buttons[i].gameObject.activeSelf != show) buttons[i].gameObject.SetActive(show);
            buttonImages[i].color = selected ? UISkin.SelectedGold : has ? Color.white : new Color(1f, 1f, 1f, 0.5f);
            if (labels[i] != null) labels[i].color = UISkin.Ink;
        }
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }
}
