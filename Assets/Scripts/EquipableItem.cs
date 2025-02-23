using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class EquipableItem : MonoBehaviour
{

    private Animator animator;
    public bool swingWait = false;
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0) && // left mouse button
            InventorySystem.Instance.isOpen == false && 
            CraftingSystem.Instance.isOpen == false && 
            SelectionManager.Instance.handIsVisible == false &&
            swingWait == false &&
            !ConstructionManager.Instance.inConstructionMode
            )
        {
            //wait for the swing to complete, before allowing another swing 
            swingWait = true;
            //SoundManager.Instance.PlaySound(SoundManager.Instance.toolSwingSound);
            StartCoroutine(SwingSoundDelay());
            animator.SetTrigger("hit");
            StartCoroutine(NewSwingDelay());
        }
    }
    public void GetHit()
    {
        GameObject selectedTree = SelectionManager.Instance.selectedTree;
        if(selectedTree != null)
        {
            SoundManager.Instance.PlaySound(SoundManager.Instance.chopSound);
            selectedTree.GetComponent<ChoppableTree>().GetHit();
        }

    }
    IEnumerator SwingSoundDelay()
    {
        yield return new WaitForSeconds(0.1f);
        SoundManager.Instance.PlaySound(SoundManager.Instance.toolSwingSound);
    }
    IEnumerator NewSwingDelay()
    {
        yield return new WaitForSeconds(1f); //Delay is hardcoded for the axe
        swingWait = false;

    }
}
