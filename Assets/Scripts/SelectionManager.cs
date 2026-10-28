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


            ChoppableTree choppableTree = selectionTransform.GetComponent<ChoppableTree>();

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


            Animal animal = selectionTransform.GetComponent<Animal>();
            if (animal && animal.playerInRange)
            {
                var rabbit = animal.GetComponent<RabbitLife>();
                if (animal.isDead)
                {
                    //looting (a baby rabbit is too small: nothing to take)
                    interaction_text.text = rabbit != null && rabbit.Growth < BabyNothing
                        ? Lang.T("Too small: nothing to take", "Prea mic: nu ai ce lua")
                        : Lang.T("Loot", "Ia");
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
                else if (rabbit != null && rabbit.caughtIn != null && !EquipSystem.Instance.IsHoldingWeapon())
                {
                    // a rabbit in a snare, empty hands: take it alive
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

            if(!interactableObject && !animal)
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
        }

        if (!UpdateRaft(ray)) UpdateDrinking(ray);
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
            var box = raft.InWater ? new Bounds(at + Vector3.up * 1.9f, new Vector3(4.8f, 4.8f, 4.8f))
                                   : new Bounds(at + Vector3.up * 0.4f, new Vector3(4.8f, 1.8f, 4.8f));
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

    private void Loot(Lootable lootable)
    {
        // a baby rabbit gives nothing (only its body goes away); a half grown one only a little meat
        var young = lootable.GetComponent<RabbitLife>();
        float growth = young != null ? young.Growth : 1f;
        if (growth < BabyNothing)
        {
            InventorySystem.Instance.ShowMessage(Lang.T("Too small: nothing to take. Let them grow!", "Prea mic: nu ai ce lua. Lasă-i să crească!"));
            var baby = lootable.GetComponent<Animal>();
            if (baby != null && baby.bloodPuddle != null) baby.bloodPuddle.transform.SetParent(lootable.transform.parent);
            Destroy(lootable.gameObject);
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

        //Spawning the loot on the ground
        Vector3 lootSpawnPosition = lootable.gameObject.transform.position;

        foreach(LootRecieved lootRecieved in lootable.finalLoot)
        {
            for(int i=0; i< lootRecieved.amount; i++)
            {
                Debug.Log(lootRecieved.item.name + "_Model");
                
                // on the ground around the body (before: at height 0.2 of the world, high above the island's ground)
                var lootPrefab = Resources.Load<GameObject>(lootRecieved.item.name + "_Model");
                if (lootPrefab == null) continue;
                Vector2 spread = UnityEngine.Random.insideUnitCircle * 0.9f;
                Vector3 at = new Vector3(lootSpawnPosition.x + spread.x, 0f, lootSpawnPosition.z + spread.y);
                at.y = Forest.GroundY(at) + 0.35f;
                GameObject lootSpawn = Instantiate(lootPrefab, at, Quaternion.Euler(0, UnityEngine.Random.Range(0f, 360f), 0));
                
            }
        }
        //If we want the blood puddle to stay om the ground
        if (lootable.GetComponent<Animal>())
        {
            lootable.GetComponent<Animal>().bloodPuddle.transform.SetParent(lootable.transform.parent);
        }


        //Destroy Looted body
        Destroy(lootable.gameObject);

        //if(chest){don't destroy}






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
        interaction_Info_UI.SetActive(true);
    }


}
