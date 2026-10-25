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
        if (GameInput.PrimaryDown && // left mouse button / "Loveste" on the phone
            InventorySystem.Instance.isOpen == false &&
            CraftingSystem.Instance.isOpen == false &&
            SelectionManager.Instance.handIsVisible == false &&
            swingWait == false &&
            !ConstructionManager.Instance.inConstructionMode &&
            !OtherWindowOpen() &&
            !SeedPlanting.Active && // planting a seed: the click plants it

            !windowOpenLastFrame // the click that closed a window (its X button) is not a swing
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
    static bool OtherWindowOpen()
    {
        return (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen);
    }

    bool windowOpenLastFrame;

    void LateUpdate()
    {
        windowOpenLastFrame = InventorySystem.Instance.isOpen || CraftingSystem.Instance.isOpen || OtherWindowOpen();
    }

    public void GetHit()
    {
        GameObject selectedTree = SelectionManager.Instance.selectedTree;
        if(selectedTree != null)
        {
            SoundManager.Instance.PlayChop();
            selectedTree.GetComponent<ChoppableTree>().GetHit();
        }

    }
    IEnumerator SwingSoundDelay()
    {
        yield return new WaitForSeconds(0.1f);
        SoundManager.Instance.PlaySwing(); // a whoosh on every swing, also in the air
    }
    IEnumerator NewSwingDelay()
    {
        yield return new WaitForSeconds(1f); //Delay is hardcoded for the axe
        swingWait = false;

    }
}
