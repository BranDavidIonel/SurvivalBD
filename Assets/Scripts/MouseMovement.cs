using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MouseMovement : MonoBehaviour
{
    public float mouseSensitivity = 100f;

    float xRotation = 0f;
    float YRotation = 0f;

    // turns the view (degrees): yaw left/right, pitch up/down (the story start, a loaded game)
    public void SetLook(float yaw, float pitch)
    {
        YRotation = yaw;
        xRotation = Mathf.Clamp(pitch, -90f, 90f);
        transform.localRotation = Quaternion.Euler(xRotation, YRotation, 0f);
    }

    void Start()
    {
        //Locking the cursor to the middle of the screen and making it invisible
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        if (InventorySystem.Instance.isOpen==false && CraftingSystem.Instance.isOpen==false && !MenuManager.Instance.isMenuOpen && !StorageManager.Instance.storageUIOpen && !CampfireUIManager.Instance.isUiOpen) {
            float mouseX, mouseY;
            if (GameInput.TouchControls)
            {
                //right joystick / swiping on the phone
                mouseX = GameInput.touchLookDegrees.x;
                mouseY = GameInput.touchLookDegrees.y;
            }
            else
            {
                mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
                mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
            }

            //control rotation around x axis (Look up and down)
            xRotation -= mouseY;

            //we clamp the rotation so we cant Over-rotate (like in real life)
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);

            //control rotation around y axis (Look up and down)
            YRotation += mouseX;

            //applying both rotations
            transform.localRotation = Quaternion.Euler(xRotation, YRotation, 0f);
        }

    }
}
