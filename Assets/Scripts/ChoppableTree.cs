using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// A tree the axe can chop. The birch trees (Tree_Parent) shake with their Animator and leave a ChoppedTree;
// the pines of the pine forest (PineTree on the same object) shake and fall over in code.
[RequireComponent(typeof(BoxCollider))]
public class ChoppableTree : MonoBehaviour
{

    public bool playerInRange;
    public bool canBeChopped;

    public float treeMaxHealth;
    public float treeHealth;

    public Animator animator;

    public float caloriesSpentChoppingWood = 20;

    PineTree pine;

    private void Start()
    {
        treeHealth = treeMaxHealth;
        pine = GetComponent<PineTree>();
        // birch: tree_base -> birch_tree (Animator) -> Tree_Parent
        if (animator == null && pine == null && transform.parent != null && transform.parent.parent != null)
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
        if (pine != null) pine.Shake();
        else if (animator != null) animator.SetTrigger("shake");
        treeHealth -= 1;
        PlayerState.Instance.currentCalories -= caloriesSpentChoppingWood;

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

        if (pine != null)
        {
            pine.Fall(); // it falls over, then leaves its logs and a stump (PineForest)
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
