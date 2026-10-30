using UnityEngine;

// All player input goes through here.
// On PC it reads keyboard + mouse exactly like before.
// On phones (and in the Device Simulator) it reads the on-screen controls built by MobileControls.
public static class GameInput
{
    public enum TouchAction { Primary, Jump, SwimDown, Inventory, Crafting, Menu, Cancel, Count }

    public const string ForceTouchPrefKey = "SurvivalBD.ForceTouchControls";

    // --- written by MobileControls --- //
    public static Vector2 touchMove;        // -1..1, from the left joystick
    public static Vector2 touchLookDegrees; // degrees to turn this frame, from the right joystick + swiping

    static readonly bool[] pending = new bool[(int)TouchAction.Count];
    static readonly bool[] held = new bool[(int)TouchAction.Count];
    static readonly int[] downFrame = new int[(int)TouchAction.Count];

    static int touchCheckFrame = -1;
    static bool touchControls;

    static GameInput()
    {
        ResetTouch();
    }

    // True on phones/tablets, in the Device Simulator, or when forced from Tools > Mobile in the editor.
    public static bool TouchControls
    {
        get
        {
            if (touchCheckFrame != Time.frameCount)
            {
                touchCheckFrame = Time.frameCount;
                touchControls = UnityEngine.Device.Application.isMobilePlatform;
#if UNITY_EDITOR
                if (UnityEditor.EditorPrefs.GetBool(ForceTouchPrefKey, false)) touchControls = true;
#endif
            }
            return touchControls;
        }
    }

    // ---------------- read by the game ---------------- //

    public static float MoveX => Mathf.Clamp(Input.GetAxis("Horizontal") + touchMove.x, -1f, 1f);
    public static float MoveZ => Mathf.Clamp(Input.GetAxis("Vertical") + touchMove.y, -1f, 1f);

    // Left mouse click on PC, the "Loveste" button on the phone (hit, pick up, open, place).
    // On the phone every touch also counts as a mouse click, so the mouse is ignored there.
    public static bool PrimaryDown => TouchControls || TestDriven ? TouchDown(TouchAction.Primary) : Input.GetMouseButtonDown(0);

    // Only for the automatic play tests (editor): on PC too the test presses through PressTouch and aims at the
    // screen's centre (in the editor the mouse is not where a locked cursor would be). Always false in the game.
    public static bool TestDriven;

    public static bool JumpDown => Input.GetButtonDown("Jump") || TouchDown(TouchAction.Jump);
    public static bool SwimUpHeld => Input.GetKey(KeyCode.Space) || held[(int)TouchAction.Jump];
    public static bool SwimDownHeld => Input.GetKey(KeyCode.LeftControl) || held[(int)TouchAction.SwimDown];

    public static bool InventoryToggleDown => Input.GetKeyDown(KeyCode.I) || TouchDown(TouchAction.Inventory);
    public static bool CraftingToggleDown => Input.GetKeyDown(KeyCode.C) || TouchDown(TouchAction.Crafting);
    // M on PC; the "Meniu" button or the phone's Back button on the phone
    public static bool MenuToggleDown => Input.GetKeyDown(KeyCode.M) || TouchDown(TouchAction.Menu)
        || (TouchControls && Input.GetKeyDown(KeyCode.Escape));
    public static bool CancelDown => Input.GetKeyDown(KeyCode.X) || TouchDown(TouchAction.Cancel);

    // Ray from the crosshair: the screen centre on the phone, the (locked) mouse on PC.
    public static Ray AimRay()
    {
        Vector3 point = TouchControls || TestDriven
            ? new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f)
            : Input.mousePosition;
        return Camera.main.ScreenPointToRay(point);
    }

    // ---------------- used by MobileControls ---------------- //

    public static void PressTouch(TouchAction action)
    {
        pending[(int)action] = true;
        held[(int)action] = true;
    }

    public static void ReleaseTouch(TouchAction action)
    {
        held[(int)action] = false;
    }

    // Called once at the start of every frame (MobileControls runs before the other scripts),
    // so every script sees a button press in the same frame, exactly like Input.GetKeyDown.
    public static void BeginTouchFrame()
    {
        for (int i = 0; i < pending.Length; i++)
        {
            if (pending[i])
            {
                downFrame[i] = Time.frameCount;
                pending[i] = false;
            }
        }
    }

    public static void ResetTouch()
    {
        touchMove = Vector2.zero;
        touchLookDegrees = Vector2.zero;
        for (int i = 0; i < pending.Length; i++)
        {
            pending[i] = false;
            held[i] = false;
            downFrame[i] = -1;
        }
    }

    static bool TouchDown(TouchAction action) => downFrame[(int)action] == Time.frameCount;
}
