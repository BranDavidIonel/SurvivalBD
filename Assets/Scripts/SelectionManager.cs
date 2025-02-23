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

    void Update()
    {
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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
                interaction_text.text = "Open";
                interaction_Info_UI.SetActive(true);

                selectedStorageBox = storageBox.gameObject;

                if (Input.GetMouseButtonDown(0))
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
                interaction_text.text = "Interect";
                interaction_Info_UI.SetActive(true);

                selectedCampfire = campfire.gameObject;

                if (Input.GetMouseButtonDown(0) && campfire.isCooking == false)
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
                if (animal.isDead)
                {
                    //looting
                    interaction_text.text = "Loot";
                    interaction_Info_UI.SetActive(true);

                    centerDotImage.gameObject.SetActive(false);
                    handIcon.gameObject.SetActive(true);

                    handIsVisible = true;
                    
                    if (Input.GetMouseButtonDown(0))
                    {
                        Lootable lootable = animal.GetComponent<Lootable>();
                        Loot(lootable);
                    }                    

                }
                else
                {
                    onTargetSelectItem = true;
                    interaction_Info_UI.SetActive(true);
                    interaction_text.text = animal.animalName;

                    centerDotImage.gameObject.SetActive(true);
                    handIcon.gameObject.SetActive(false);

                    handIsVisible = false;

                    if (Input.GetMouseButtonDown(0) && EquipSystem.Instance.IsHoldingWeapon() && EquipSystem.Instance.IsThereASwingLock() == false)
                    {
                        StartCoroutine(DealDamageTo(animal, 0.3f, EquipSystem.Instance.GetWeaponDamage()));
                    }
                }               
            }

            if(!interactableObject && !animal)
            {
                onTargetSelectItem = false;
                handIsVisible = false;

                centerDotImage.gameObject.SetActive(true);
                handIcon.gameObject.SetActive(false);
            }

            if(!interactableObject && !animal && !choppableTree && !storageBox && !campfire)
            {
                interaction_text.text = "";
                interaction_Info_UI.SetActive(false);
            }

        }
        
    }

    private void Loot(Lootable lootable)
    {
        if(lootable.wasLootCalculated == false)
        {
            List<LootRecieved> revievedLoot = new List<LootRecieved>();
            foreach(LootPossibility loot in lootable.possibleLoot)
            {
                // 0 -> 1 ( 50% drop rate) 1/2
                //-1 -> 1 (33% drop rate) 1/3
                var lootAmount = UnityEngine.Random.Range(loot.amountMin, loot.amountMax + 1);
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
                
                GameObject lootSpawn = Instantiate(Resources.Load<GameObject>(lootRecieved.item.name + "_Model"),
                    new Vector3(lootSpawnPosition.x,0.2f,lootSpawnPosition.z),
                    Quaternion.Euler(0,0,0));
                
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
    }

    public void EnableSelection()
    {
        handIcon.enabled = true;
        centerDotImage.enabled = true;
        interaction_Info_UI.SetActive(true);
    }


}
