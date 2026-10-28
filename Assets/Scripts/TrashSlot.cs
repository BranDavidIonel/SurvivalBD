using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// The trash can at the bottom of the inventory, and the question asked before an item leaves the inventory:
//  - dropped on the trash can: "Throw away the Stone Axe?" (gone for good)
//  - dragged out of the windows (DragDrop): "Drop the Stone Axe on the ground?" (DroppedItems: it can be picked up
//    again)
// The can is drawn in code (closed, and open while an item is held over it).
public class TrashSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    public static TrashSlot Instance { get; private set; }

    public GameObject trashAlertUI;

    private Text textToModify;

    public Sprite trash_closed;
    public Sprite trash_opened;

    private Image imageComponent;

    Button YesBTN, NoBTN;

    GameObject draggedItem => DragDrop.itemBeingDragged;

    GameObject itemToBeDeleted;
    bool dropping; // the question is "drop it on the ground?" (else: "throw it away?")

    static Sprite closedSprite, openSprite;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        imageComponent = transform.Find("backgroundTrash").GetComponent<Image>();
        if (closedSprite == null) { closedSprite = DrawCan(false); openSprite = DrawCan(true); }
        trash_closed = closedSprite;
        trash_opened = openSprite;
        imageComponent.sprite = trash_closed;
        imageComponent.color = Color.white;
        imageComponent.preserveAspect = true;

        textToModify = trashAlertUI.transform.Find("Text").GetComponent<Text>();

        YesBTN = trashAlertUI.transform.Find("yes").GetComponent<Button>();
        YesBTN.onClick.AddListener(delegate { DeleteItem(); });

        NoBTN = trashAlertUI.transform.Find("no").GetComponent<Button>();
        NoBTN.onClick.AddListener(delegate { CancelDeletion(); });
    }

    static string ShownName(GameObject item)
    {
        var info = item != null ? item.GetComponent<InventoryItem>() : null;
        if (info != null && !string.IsNullOrEmpty(info.thisName)) return Translations.Name(info.thisName);
        return item != null ? InventorySystem.CleanName(item.name) : "";
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (draggedItem == null) return;
        if (draggedItem.GetComponent<InventoryItem>().isTrashable == true)
        {
            itemToBeDeleted = draggedItem.gameObject;
            dropping = false;
            Ask(Lang.T("Throw away the " + ShownName(itemToBeDeleted) + "?\nIt will be gone for good.", "Arunci la gunoi: " + ShownName(itemToBeDeleted) + "?\nDispare pentru totdeauna."));
        }
    }

    // DragDrop: the item was let go outside the windows
    public void AskDrop(GameObject item)
    {
        if (item == null) return;
        itemToBeDeleted = item;
        dropping = true;
        if (RabbitPopulation.IsLiveRabbit(InventorySystem.CleanName(item.name)))
            Ask(Lang.T("Let the rabbit go?\nOutside a closed pen it runs away!", "Dai drumul iepurelui?\nÎn afara unui țarc închis fuge!"));
        else
            Ask(Lang.T("Drop the " + ShownName(item) + " on the ground?\nYou can pick it up again.", "Arunci pe jos: " + ShownName(item) + "?\nÎl poți lua din nou."));
    }

    // the words on the Yes / No buttons (in the language of the Settings)
    static void Label(Button button, string words)
    {
        if (button == null) return;
        var text = button.GetComponentInChildren<Text>(true);
        if (text != null) { text.text = words; return; }
        var tmp = button.GetComponentInChildren<TMPro.TMP_Text>(true);
        if (tmp != null) tmp.text = words;
    }

    void Ask(string question)
    {
        trashAlertUI.SetActive(true);
        trashAlertUI.transform.SetAsLastSibling();
        textToModify.text = question;
        Label(YesBTN, Lang.T("Yes", "Da"));
        Label(NoBTN, Lang.T("No", "Nu"));
    }

    private void CancelDeletion()
    {
        imageComponent.sprite = trash_closed;
        trashAlertUI.SetActive(false);
        itemToBeDeleted = null;
    }

    private void DeleteItem()
    {
        imageComponent.sprite = trash_closed;
        trashAlertUI.SetActive(false);
        if (itemToBeDeleted == null) return;
        if (dropping)
        {
            DroppedItems.DropFromInventory(itemToBeDeleted);
        }
        else
        {
            var equip = EquipSystem.Instance;
            if (equip != null && equip.selectedItem == itemToBeDeleted)
            {
                if (equip.selectedItemModel != null) Destroy(equip.selectedItemModel);
                equip.selectedItemModel = null;
                equip.selectedItem = null;
                equip.selectedNumber = -1;
            }
            DestroyImmediate(itemToBeDeleted.gameObject);
            InventorySystem.Instance.RecalculateList();
            CraftingSystem.Instance.RefreshNeededItems();
            QuickBarUI.Changed();
        }
        itemToBeDeleted = null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (draggedItem != null && draggedItem.GetComponent<InventoryItem>().isTrashable == true)
        {
            imageComponent.sprite = trash_opened;
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (draggedItem != null && draggedItem.GetComponent<InventoryItem>().isTrashable == true)
        {
            imageComponent.sprite = trash_closed;
        }
    }

    // ---- the can, drawn: a grey metal can with a lid and a handle (the lid raised and tilted when open) ----

    static Sprite DrawCan(bool open)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = open ? "Trash open" : "Trash closed", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        Color dark = new Color(0.16f, 0.13f, 0.11f, 1f), metal = new Color(0.82f, 0.8f, 0.76f, 1f), shade = new Color(0.62f, 0.6f, 0.57f, 1f);
        // body (a little narrower at the bottom), with three grooves
        Vector2[] body = { new Vector2(34, 10), new Vector2(94, 10), new Vector2(102, 84), new Vector2(26, 84) };
        // the lid and its handle; open: lifted and turned about its left end
        Vector2[] lid = { new Vector2(20, 88), new Vector2(108, 88), new Vector2(108, 100), new Vector2(20, 100) };
        Vector2[] handle = { new Vector2(50, 100), new Vector2(78, 100), new Vector2(78, 110), new Vector2(50, 110) };
        if (open)
        {
            var pivot = new Vector2(20, 100);
            for (int i = 0; i < 4; i++) { lid[i] = Turn(lid[i], pivot, 28f) + new Vector2(-4, 8); handle[i] = Turn(handle[i], pivot, 28f) + new Vector2(-4, 8); }
        }
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // 2x2 samples per pixel: soft edges
                Color sum = Color.clear;
                for (int sy = 0; sy < 2; sy++)
                    for (int sx = 0; sx < 2; sx++)
                    {
                        var p = new Vector2(x + 0.25f + sx * 0.5f, y + 0.25f + sy * 0.5f);
                        Color c = Color.clear;
                        if (Inside(body, p, 4f) || Inside(lid, p, 4f) || Inside(handle, p, 4f)) c = dark;
                        if (Inside(handle, p, 0f)) c = shade;
                        if (Inside(lid, p, 0f)) c = metal;
                        if (Inside(body, p, 0f))
                        {
                            c = Color.Lerp(shade, metal, Mathf.Clamp01((p.x - 26f) / 50f));
                            foreach (float gx in new[] { 48f, 64f, 80f })
                                if (Mathf.Abs(p.x - gx - (p.y - 47f) * (gx - 64f) / 300f) < 2.4f && p.y > 20f && p.y < 76f) c = dark;
                        }
                        sum += c;
                    }
                pixels[y * size + x] = sum * 0.25f;
            }
        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    static Vector2 Turn(Vector2 p, Vector2 pivot, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        Vector2 d = p - pivot;
        return pivot + new Vector2(d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a), d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a));
    }

    // inside the polygon (grown by "grow" pixels: the dark outline)
    static bool Inside(Vector2[] poly, Vector2 p, float grow)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) && (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        if (inside || grow <= 0f) return inside;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            Vector2 a = poly[j], b = poly[i], ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(0.0001f, ab.sqrMagnitude));
            if (Vector2.Distance(a + ab * t, p) <= grow) return true;
        }
        return false;
    }
}
