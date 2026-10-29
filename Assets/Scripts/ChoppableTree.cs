using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A tree the axe can chop. The trees of the forests (ForestTree on the same object) shake and fall over in code;
// an old birch (Tree_Parent) shakes with its Animator and leaves a ChoppedTree (they are turned into forest trees
// when the game starts, this is kept for safety).
public class ChoppableTree : MonoBehaviour
{

    public bool playerInRange;

    // true while the crosshair is on it (SelectionManager). A forest tree only runs Update while it is
    // targeted: thousands of trees each doing a little work every frame would slow the phone down.
    public bool canBeChopped
    {
        get => targeted;
        set
        {
            targeted = value;
            if (forestTree != null && enabled != value) enabled = value;
        }
    }
    bool targeted;

    public float treeMaxHealth;
    public float treeHealth;

    public Animator animator;

    public float caloriesSpentChoppingWood = 20;

    ForestTree forestTree;

    void Awake()
    {
        forestTree = GetComponent<ForestTree>();
        if (forestTree != null) enabled = false;
    }

    private void Start()
    {
        if (treeHealth <= 0f) treeHealth = treeMaxHealth;
        forestTree = GetComponent<ForestTree>();
        // birch: tree_base -> birch_tree (Animator) -> Tree_Parent
        if (animator == null && forestTree == null && transform.parent != null && transform.parent.parent != null)
        {
            animator = transform.parent.parent.GetComponent<Animator>();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
    public void GetHit()
    {
        if (treeHealth <= 0) return; // already falling
        if (forestTree != null) forestTree.Shake();
        else if (animator != null) animator.SetTrigger("shake");
        // the iron axe fells any tree in 3 blows, the stone axe needs one blow per piece of its health
        var held = EquipSystem.Instance != null ? EquipSystem.Instance.selectedItem : null;
        bool ironAxe = held != null && held.name.StartsWith("ironaxe");
        treeHealth -= ironAxe ? Mathf.Max(1f, Mathf.Ceil(treeMaxHealth / 3f)) : 1f;
        PlayerState.Instance.currentCalories -= caloriesSpentChoppingWood;
        GlobalState.Instance.resourceHealth = treeHealth;
        GlobalState.Instance.resourceMaxHealth = treeMaxHealth;

        if (treeHealth <= 0)
        {
            TreeIsDead();
        }

    }

    void TreeIsDead()
    {
        Vector3 treePosition = transform.position;
        canBeChopped = false;
        SelectionManager.Instance.selectedTree = null;
        //health bar of trees
        SelectionManager.Instance.chopHolder.SetActive(false);

        if (forestTree != null)
        {
            forestTree.Fall(); // it falls over, then leaves its logs, branches and a stump (Forest)
            return;
        }

        if (SoundManager.Instance != null) SoundManager.Instance.PlayTreeFall(treePosition);

        Destroy(transform.parent.transform.parent.gameObject);

        Vector3 newTreePosition = new Vector3(treePosition.x, treePosition.y, treePosition.z);
        //default rotation Quaternion.Euler(0,0,0)
        GameObject brokenTree = Instantiate(Resources.Load<GameObject>("ChoppedTree"), newTreePosition, Quaternion.Euler(0,0,0));
        brokenTree.transform.SetParent(transform.parent.transform.parent.transform.parent);//from tree_base->birch_tree->Tree_parent->Trees
    }

    private void Update()
    {
        if (canBeChopped)
        {
            GlobalState.Instance.resourceHealth = treeHealth;
            GlobalState.Instance.resourceMaxHealth = treeMaxHealth;
        }
    }

}
