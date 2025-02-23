using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class ChoppableTree : MonoBehaviour
{

    public bool playerInRange;
    public bool canBeChopped;

    public float treeMaxHealth;
    public float treeHealth;

    public Animator animator;

    public float caloriesSpentChoppingWood = 20;

    private void Start()
    {
        treeHealth = treeMaxHealth;
        animator = transform.parent.transform.parent.GetComponent<Animator>();
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
        animator.SetTrigger("shake");
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

        Destroy(transform.parent.transform.parent.gameObject);
        canBeChopped = false;
        SelectionManager.Instance.selectedTree = null;
        //health bar of trees
        SelectionManager.Instance.chopHolder.SetActive(false);

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
