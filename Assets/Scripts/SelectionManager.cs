using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SelectionManager : MonoBehaviour
{
    public static SelectionManager Instance { get; set; }

    public GameObject interaction_Info_UI;

    public bool onTargetSelectItem;

    public GameObject selectedObject;
    Text interaction_text;

    public Image centerDotImage;
    public Image handIcon;

    public bool handIsVisible;

    public GameObject selectedTree;
    public GameObject chopHolder;

    public GameObject selectedStorageBox;
    public GameObject selectedCampfire;
    private void Start()
    {
        onTargetSelectItem = false;
        interaction_text = interaction_Info_UI.GetComponent<Text>();
        // the hand (something to take, open or use): a little bigger, orange, in its own shape (it was squeezed square)
        if (handIcon != null)
        {
            handIcon.preserveAspect = true;
            handIcon.color = new Color(1f, 0.58f, 0.12f, 1f);
            handIcon.rectTransform.localScale = new Vector3(0.42f, 0.42f, 1f);
        }
    }
    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    // the living animal the crosshair is on (in reach): the axe's swing hits it
    Animal aimedAnimal;

    // the pen door the crosshair is on (the phone's button says Open / Close)
    public PenDoor selectedDoor;

    // a rabbit killed before it is half grown gives nothing; half grown: only a little meat
    const float BabyNothing = 0.5f;

    // EquipableItem: a swing of the tool in the hand has started
    public void OnSwing()
    {
        var target = aimedAnimal;
        if (target == null || target.isDead || !target.playerInRange) return;
        if (!EquipSystem.Instance.IsHoldingWeapon()) return;
        StartCoroutine(DealDamageTo(target, 0.3f, EquipSystem.Instance.GetWeaponDamage()));
    }

    void Update()
    {
        aimedAnimal = null;
        Ray ray = GameInput.AimRay();
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            var selectionTransform = hit.transform;

            // a dead rabbit close to the crosshair counts as looked at when nothing else to use is right under it
            // (the body lies low in the grass and is small on a phone screen: no need to aim exactly at it)
            Animal nearBody = null;
            if (selectionTransform.GetComponent<Animal>() == null && selectionTransform.GetComponent<InteractableObject>() == null
                && selectionTransform.GetComponent<StorageBox>() == null && selectionTransform.GetComponent<Campfire>() == null
                && PenDoor.FromCollider(hit.collider) == null)
                nearBody = DeadBodyNear(ray, hit.distance);

            ChoppableTree choppableTree = nearBody != null ? null : selectionTransform.GetComponent<ChoppableTree>();

            if (choppableTree && choppableTree.playerInRange)
            {
                choppableTree.canBeChopped = true;
                selectedTree = choppableTree.gameObject;
                chopHolder.gameObject.SetActive(true);
            }
            else
            {
                if (selectedTree != null)
                {
                    selectedTree.gameObject.GetComponent<ChoppableTree>().canBeChopped = false;
                    selectedTree = null;
                    chopHolder.gameObject.SetActive(false);
                }

            }


            InteractableObject interactableObject = selectionTransform.GetComponent<InteractableObject>();
            // (the last thing looked at does not stay selected: a tap somewhere else used to take it)
            if (!interactableObject || !interactableObject.playerInRange) selectedObject = null;
            if (interactableObject && interactableObject.playerInRange)
            {
                onTargetSelectItem = true;
                selectedObject = interactableObject.gameObject;
                interaction_text.text = interactableObject.GetItemName();
                interaction_Info_UI.SetActive(true);

                centerDotImage.gameObject.SetActive(false);
                handIcon.gameObject.SetActive(true);

                handIsVisible = true;
                
            }
            //Storage box
            StorageBox storageBox = selectionTransform.GetComponent<StorageBox>();

            if(storageBox && storageBox.playerInRange && PlacementSystem.Instance.inPlacementMode == false)
            {
                interaction_text.text = Lang.T("Open", "Deschide");
                interaction_Info_UI.SetActive(true);

                selectedStorageBox = storageBox.gameObject;

                if (GameInput.PrimaryDown)
                {
                    StorageManager.Instance.OpenBox(storageBox);
                }

            }
            else
            {
                if(selectedStorageBox != null)
                {
                    selectedStorageBox = null;
                }
            }

            //Campfire
            Campfire campfire = selectionTransform.GetComponent<Campfire>();

            if (campfire && campfire.playerInRange && PlacementSystem.Instance.inPlacementMode == false)
            {
                interaction_text.text = Translations.Name(campfire.InteractionText);
                interaction_Info_UI.SetActive(true);

                selectedCampfire = campfire.gameObject;

                // the window also opens while food cooks (it shows how far it is)
                if (GameInput.PrimaryDown && CampfireUIManager.Instance.isUiOpen == false)
                {
                    campfire.OpenUI();
                }

            }
            else
            {
                if (selectedCampfire != null)
                {
                    selectedCampfire = null;
                }
            }


            Animal animal = nearBody != null ? nearBody : selectionTransform.GetComponent<Animal>();
            if (animal && animal.playerInRange)
            {
                var rabbit = animal.GetComponent<RabbitLife>();
                if (animal.isDead)
                {
                    // looting: one tap takes the meat and the hide (a baby rabbit is too small: nothing to take).
                    // Busy: the grass under it or the river nearby do not take the text or the tap.
                    onTargetSelectItem = true;
                    interaction_text.text = LootText(rabbit);
                    interaction_Info_UI.SetActive(true);

                    centerDotImage.gameObject.SetActive(false);
                    handIcon.gameObject.SetActive(true);

                    handIsVisible = true;
                    
                    if (GameInput.PrimaryDown)
                    {
                        Lootable lootable = animal.GetComponent<Lootable>();
                        Loot(lootable);
                    }                    

                }
                else if (rabbit != null && (rabbit.caughtIn != null || RabbitPopulation.InPen(rabbit)) && !EquipSystem.Instance.IsHoldingWeapon())
                {
                    // a rabbit in a snare or a grown one in a pen, empty hands: take it alive
                    onTargetSelectItem = true;
                    interaction_Info_UI.SetActive(true);
                    interaction_text.text = rabbit.sex == RabbitInfo.Sex.Female
                        ? Lang.T("Take the rabbit alive (female)", "Ia iepurele viu (femelă)")
                        : Lang.T("Take the rabbit alive (male)", "Ia iepurele viu (mascul)");
                    centerDotImage.gameObject.SetActive(false);
                    handIcon.gameObject.SetActive(true);
                    handIsVisible = true;
                    if (GameInput.PrimaryDown && RabbitPopulation.Instance != null) RabbitPopulation.Instance.TakeAlive(rabbit);
                }
                else
                {
                    onTargetSelectItem = true;
                    interaction_Info_UI.SetActive(true);
                    interaction_text.text = Translations.Name(animal.animalName);
                    // in a snare, with the axe in the hand: hit it, or put the axe away to take it alive
                    if (rabbit != null && rabbit.caughtIn != null)
                        interaction_text.text += Lang.T("\nin a snare (empty hands: take it alive)", "\nîn cursă (cu mâinile goale: îl iei viu)");

                    centerDotImage.gameObject.SetActive(true);
                    handIcon.gameObject.SetActive(false);

                    handIsVisible = false;

                    // the blow itself is given by the swing (OnSwing): the swing and this check used to run in
                    // either order, and when the swing came first the blow was lost
                    aimedAnimal = animal;
                }
            }

            // nothing in reach to take or hit (also something too far away: the hand of a body looked at before
            // does not stay, it kept the axe from swinging)
            if (!(interactableObject && interactableObject.playerInRange) && !(animal && animal.playerInRange))
            {
                onTargetSelectItem = false;
                handIsVisible = false;

                centerDotImage.gameObject.SetActive(true);
                handIcon.gameObject.SetActive(false);
            }

            // the door of a pen: open / shut it
            selectedDoor = null;
            var door = PenDoor.FromCollider(hit.collider);
            if (door != null && hit.distance < 7f && !interactableObject && !animal)
            {
                selectedDoor = door;
                interaction_text.text = door.IsOpen ? Lang.T("Close the door", "Închide ușa") : Lang.T("Open the door", "Deschide ușa");
                interaction_Info_UI.SetActive(true);
                centerDotImage.gameObject.SetActive(false);
                handIcon.gameObject.SetActive(true);
                handIsVisible = true;
                if (GameInput.PrimaryDown)
                {
                    var body = PlayerState.Instance != null && PlayerState.Instance.playerBody != null ? PlayerState.Instance.playerBody.transform.position : ray.origin;
                    door.Toggle(body);
                }
            }
            else if(!interactableObject && !animal && !choppableTree && !storageBox && !campfire)
            {
                interaction_text.text = "";
                interaction_Info_UI.SetActive(false);
            }

        }
        else
        {
            // looking at the sky: no tree is targeted any more (a swing used to chop the last tree looked at)
            ClearSelectedTree();
            selectedDoor = null;
            // and nothing is selected to take or open (a tap at the sky used to take the last thing looked at, and
            // the spear could not be thrown upwards)
            selectedObject = null;
            onTargetSelectItem = false;
            selectedStorageBox = null;
            selectedCampfire = null;
            if (handIsVisible)
            {
                handIsVisible = false;
                handIcon.gameObject.SetActive(false);
                centerDotImage.gameObject.SetActive(true);
                interaction_text.text = "";
                interaction_Info_UI.SetActive(false);
            }
        }

        if (!UpdateRaft(ray)) UpdateDrinking(ray);
    }

    // ---- the spear's aiming cross: a "+" in place of the small dot while the spear is in the hand ---- //
    GameObject aimCross;
    Color dotColor;
    bool dotHidden;

    void LateUpdate()
    {
        if (centerDotImage == null) return;
        bool show = Spear.IsHeld && enabled && centerDotImage.enabled && centerDotImage.gameObject.activeInHierarchy;
        if (show && aimCross == null) aimCross = MakeAimCross();
        if (aimCross != null && aimCross.activeSelf != show) aimCross.SetActive(show);
        // the dot itself is only made see-through (its place and on/off stay as the rest of this script sets them)
        bool hide = Spear.IsHeld;
        if (hide != dotHidden)
        {
            if (hide) { dotColor = centerDotImage.color; centerDotImage.color = new Color(dotColor.r, dotColor.g, dotColor.b, 0f); }
            else centerDotImage.color = dotColor;
            dotHidden = hide;
        }
    }

    // a window, a book, the map...: the selection is switched off, and the cross goes with it
    void OnDisable()
    {
        if (aimCross != null) aimCross.SetActive(false);
    }

    // four short white bars around the middle (a gap where the spear goes), with a dark edge to be seen on snow too
    GameObject MakeAimCross()
    {
        var dot = centerDotImage.rectTransform;
        var go = new GameObject("Aim cross (spear)", typeof(RectTransform));
        go.layer = dot.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(dot.parent, false);
        rect.SetSiblingIndex(dot.GetSiblingIndex() + 1);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(60f, 60f);
        rect.position = dot.TransformPoint(dot.rect.center);
        const float length = 15f, thick = 4f, gap = 7f;
        Bar(rect, new Vector2(0f, gap + length * 0.5f), new Vector2(thick, length));
        Bar(rect, new Vector2(0f, -gap - length * 0.5f), new Vector2(thick, length));
        Bar(rect, new Vector2(gap + length * 0.5f, 0f), new Vector2(length, thick));
        Bar(rect, new Vector2(-gap - length * 0.5f, 0f), new Vector2(length, thick));
        return go;
    }

    static void Bar(RectTransform parent, Vector2 at, Vector2 size)
    {
        var bar = new GameObject("Bar", typeof(RectTransform));
        bar.layer = parent.gameObject.layer;
        var rect = (RectTransform)bar.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = at;
        rect.sizeDelta = size;
        var image = bar.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.92f);
        image.raycastTarget = false;
        var edge = bar.AddComponent<UnityEngine.UI.Outline>(); // (not QuickOutline's Outline)
        edge.effectColor = new Color(0f, 0f, 0f, 0.7f);
        edge.effectDistance = new Vector2(1.5f, -1.5f);
    }

    // the raft of the last level: on the beach it shows what it still needs (click: add what you carry);
    // finished and in the sea, board it to leave the island (the end of the game)
    bool raftShown;
    bool UpdateRaft(Ray ray)
    {
        // looking anywhere at it counts (the deck, the mast or the sail), if nothing solid is in between
        bool can = false;
        var raft = Raft.Instance;
        if (raft != null && !raft.IsBoarded)
        {
            Vector3 at = raft.transform.position;
            float s = Raft.Size; // (the model is shown bigger)
            var box = raft.InWater ? new Bounds(at + Vector3.up * 1.9f * s, new Vector3(5.2f, 4.8f, 5.2f) * s)
                                   : new Bounds(at + Vector3.up * 0.4f * s, new Vector3(5.2f, 1.2f, 5.2f) * s); // (low: the eyes of a player beside it stay over it)
            if (box.IntersectRay(ray, out float distance) && distance < (raft.InWater ? 14f : 8f))
            {
                can = true;
                foreach (var hit in Physics.RaycastAll(ray, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    // the player's own body and the raft itself don't block the view
                    if (hit.collider.GetComponentInParent<Raft>() != null || hit.collider.GetComponentInParent<PlayerMovement>() != null) continue;
                    can = false;
                    break;
                }
            }
        }
        if (can)
        {
            interaction_text.text = Raft.Instance.Status;
            // the raft's list is longer than the usual names: let it spill out of the small box
            interaction_text.horizontalOverflow = HorizontalWrapMode.Overflow;
            interaction_text.verticalOverflow = VerticalWrapMode.Overflow;
            interaction_Info_UI.SetActive(true);
            centerDotImage.gameObject.SetActive(false);
            handIcon.gameObject.SetActive(true);
            handIsVisible = true;
            raftShown = true;
            if (GameInput.PrimaryDown) Raft.Instance.Use();
            return true;
        }
        if (raftShown)
        {
            raftShown = false;
            handIsVisible = false;
            handIcon.gameObject.SetActive(false);
            centerDotImage.gameObject.SetActive(true);
            interaction_Info_UI.SetActive(false);
        }
        return false;
    }

    // ---- water: only the river's water can be drunk (the sea is salty) ---- //
    bool waterShown;

    void UpdateDrinking(Ray ray)
    {
        bool busy = onTargetSelectItem || selectedStorageBox != null || selectedCampfire != null || selectedTree != null || selectedDoor != null;
        var state = PlayerState.Instance;
        bool lookingDown = ray.direction.y < -0.15f;
        Vector3 at = state != null && state.playerBody != null ? state.playerBody.transform.position : ray.origin;
        var fresh = FreshWater.Instance;
        var source = !busy && lookingDown && fresh != null ? fresh.At(at) : FreshWater.Source.None;
        bool atFresh = source != FreshWater.Source.None;
        bool atSea = !busy && !atFresh && lookingDown && Ocean.Instance != null && Ocean.Instance.DistanceFromShore(at) > -4f
            && at.y < Ocean.Instance.Level + 4f;
        Vector3 grassSpot = Vector3.zero;
        bool grass = !busy && !atFresh && !atSea && lookingDown && GrassPicking.CanPick(ray, out grassSpot);
        if (atFresh)
        {
            bool full = state != null && state.currentHydrationPercent >= state.maxHydrationPercent - 0.5f;
            interaction_text.text = full ? Lang.T("Fresh water (not thirsty)", "Apă dulce (nu ți-e sete)") : source == FreshWater.Source.Puddle ? Lang.T("Drink rain water", "Bea apă de ploaie") : Lang.T("Drink water", "Bea apă");
            interaction_Info_UI.SetActive(true);
            centerDotImage.gameObject.SetActive(false);
            handIcon.gameObject.SetActive(true);
            handIsVisible = true;
            waterShown = true;
            if (GameInput.PrimaryDown) fresh.Drink(at);
        }
        else if (grass)
        {
            // dry grass on the meadows: hay for the fire, picked anywhere
            interaction_text.text = Lang.T("Pick grass (hay)", "Smulge iarbă (fân)");
            interaction_Info_UI.SetActive(true);
            centerDotImage.gameObject.SetActive(false);
            handIcon.gameObject.SetActive(true);
            handIsVisible = true;
            waterShown = true;
            if (GameInput.PrimaryDown) GrassPicking.Pick(grassSpot);
        }
        else if (atSea)
        {
            interaction_text.text = Lang.T("Sea water (salty)", "Apă de mare (sărată)");
            interaction_Info_UI.SetActive(true);
            waterShown = true;
            if (GameInput.PrimaryDown && InventorySystem.Instance != null)
                InventorySystem.Instance.ShowMessage(Lang.T("Too salty to drink! Find the river that comes from the mountain", "Prea sărată ca s-o bei! Caută râul care vine din munte"));
        }
        else if (waterShown)
        {
            waterShown = false;
            if (!busy)
            {
                handIsVisible = false;
                handIcon.gameObject.SetActive(false);
                centerDotImage.gameObject.SetActive(true);
                interaction_text.text = "";
                interaction_Info_UI.SetActive(false);
            }
        }
    }

    void ClearSelectedTree()
    {
        if (selectedTree != null)
        {
            var tree = selectedTree.GetComponent<ChoppableTree>();
            if (tree != null) tree.canBeChopped = false;
            selectedTree = null;
        }
        if (chopHolder != null && chopHolder.activeSelf) chopHolder.SetActive(false);
    }

    // what the crosshair says over a dead rabbit
    static string LootText(RabbitLife rabbit)
    {
        float growth = rabbit != null ? rabbit.Growth : 1f;
        if (growth < BabyNothing) return Lang.T("Too small: nothing to take", "Prea mic: nu ai ce lua");
        if (growth < 1f) return Lang.T("Take the meat", "Ia carnea");
        return Lang.T("Take the meat and the hide", "Ia carnea și blana");
    }

    // The dead rabbit nearest to the aiming line, in reach (Animal.playerInRange: RabbitLife, 4.5 m): its middle within
    // 0.9 m of the line, in front of the camera, and nothing solid well before it (the ray stopped there: a wall, a
    // tree, a hill). A body killed from far away (a thrown spear) is taken the same way when the player gets to it.
    Animal DeadBodyNear(Ray ray, float hitDistance)
    {
        var population = RabbitPopulation.Instance;
        if (population == null) return null;
        Animal best = null;
        float bestOff = 0.9f;
        foreach (var r in population.Rabbits)
        {
            if (r == null || r.Alive || r.animal == null || !r.animal.playerInRange || !r.gameObject.activeInHierarchy) continue;
            Vector3 to = r.transform.position + Vector3.up * 0.25f - ray.origin;
            float along = Vector3.Dot(to, ray.direction);
            if (along <= 0.2f || hitDistance < along - 1.2f) continue;
            float off = (to - ray.direction * along).magnitude;
            if (off >= bestOff) continue;
            bestOff = off;
            best = r.animal;
        }
        return best;
    }

    // One "Take" on the body: its meat and hide go straight into the inventory. (Before, they were thrown on the
    // ground one by one and each had to be found and taken again: small in the grass, and in a pen under the
    // foundation's floor, out of reach.) What does not fit in the inventory lies next to the body as dropped things
    // (DroppedItems: on the right floor, kept in the save, easy to aim at).
    private void Loot(Lootable lootable)
    {
        if (lootable == null) return;
        var inventory = InventorySystem.Instance;
        // a baby rabbit gives nothing (only its body goes away); a half grown one only a little meat
        var young = lootable.GetComponent<RabbitLife>();
        float growth = young != null ? young.Growth : 1f;
        if (growth < BabyNothing)
        {
            if (inventory != null) inventory.ShowMessage(Lang.T("Too small: nothing to take. Let them grow!", "Prea mic: nu ai ce lua. Lasă-i să crească!"));
            RemoveBody(lootable);
            return;
        }
        if (growth < 1f && lootable.wasLootCalculated == false)
        {
            var meat = new List<LootRecieved>();
            foreach (LootPossibility loot in lootable.possibleLoot)
                if (loot.item != null && loot.item.name.Contains("Meat")) { meat.Add(new LootRecieved { item = loot.item, amount = 1 }); break; }
            lootable.finalLoot = meat;
            lootable.wasLootCalculated = true;
        }
        if(lootable.wasLootCalculated == false)
        {
            List<LootRecieved> revievedLoot = new List<LootRecieved>();
            foreach(LootPossibility loot in lootable.possibleLoot)
            {
                // 0 -> 1 ( 50% drop rate) 1/2
                //-1 -> 1 (33% drop rate) 1/3
                // at least one of each (a rabbit always gives meat and its hide)
                var lootAmount = Mathf.Max(1, UnityEngine.Random.Range(loot.amountMin, loot.amountMax + 1));
                if(lootAmount > 0) //dropt rate control ( range -2,1) to decrease received
                {
                    LootRecieved lt = new LootRecieved();
                    lt.item = loot.item;
                    lt.amount = lootAmount;

                    revievedLoot.Add(lt);
                }
            }
            lootable.finalLoot = revievedLoot;
            lootable.wasLootCalculated = true;
        }

        // the body goes away first (its box would hold up the things dropped next to it)
        Vector3 bodyAt = lootable.transform.position;
        RemoveBody(lootable);

        // into the inventory; what does not fit lies next to the body
        var shown = new List<string>();
        var counts = new List<int>();
        int left = 0;
        bool sound = true;
        foreach (LootRecieved loot in lootable.finalLoot)
        {
            if (loot == null || loot.item == null) continue;
            string item = loot.item.name;
            for (int i = 0; i < loot.amount; i++)
            {
                if (inventory != null && inventory.CheckSlotsAvailable(1) && inventory.AddToIventory(item, sound, false))
                {
                    sound = false; // (one pickup sound for all of it)
                    string name = Translations.Name(DroppedItems.NameOf(item));
                    int index = shown.IndexOf(name);
                    if (index < 0) { shown.Add(name); counts.Add(1); } else counts[index]++;
                }
                else
                {
                    Vector2 spread = UnityEngine.Random.insideUnitCircle * 0.6f;
                    DroppedItems.Spawn(item, bodyAt + new Vector3(spread.x, 0.3f, spread.y), UnityEngine.Random.Range(0f, 360f));
                    left++;
                }
            }
        }

        // "+2 Raw Meat  +1 Rabbit Hide" (and what is left on the ground)
        string text = "";
        for (int i = 0; i < shown.Count; i++) text += (i > 0 ? "   " : "") + "+" + counts[i] + " " + shown[i];
        if (left > 0) text += (text.Length > 0 ? "\n" : "") + Lang.T("Inventory is full: the rest lies on the ground", "Inventarul e plin: restul e pe jos");
        if (inventory != null && text.Length > 0) inventory.ShowMessage(text);
    }

    // The looted body goes away; its blood stays on the ground, and so does anything stuck in it that can be taken
    // (a thrown spear, for example): only the rabbit itself is removed.
    static void RemoveBody(Lootable lootable)
    {
        var animal = lootable.GetComponent<Animal>();
        if (animal != null && animal.bloodPuddle != null) animal.bloodPuddle.transform.SetParent(lootable.transform.parent);
        foreach (var stuck in lootable.GetComponentsInChildren<InteractableObject>(true))
            if (stuck.gameObject != lootable.gameObject) stuck.transform.SetParent(lootable.transform.parent, true);
        lootable.gameObject.SetActive(false); // (its box is gone at once, not at the end of the frame)
        Destroy(lootable.gameObject);
    }

    IEnumerator DealDamageTo(Animal animal, float delay, int damage)
    {
        yield return new WaitForSeconds(delay);

        animal.TakeDamage(damage);

    }

    public void DisableSelection()
    {
        handIcon.enabled = false;
        centerDotImage.enabled = false;
        interaction_Info_UI.SetActive(false);
        selectedObject = null;
        ClearSelectedTree(); // the tree's health bar does not stay over the windows
    }

    public void EnableSelection()
    {
        handIcon.enabled = true;
        centerDotImage.enabled = true;
        // (hidden until the next frame shows what is looked at: turned on here it showed "default text" in the middle
        // of the screen after a window closed while looking at nothing)
        interaction_Info_UI.SetActive(false);
    }


}
