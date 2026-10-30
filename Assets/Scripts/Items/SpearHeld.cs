using UnityEngine;

// The spear in the player's hand (made by EquipSystem when the spear is taken from the quick bar; drawn on the layer
// of the things in the hand, by the camera that draws them on top). It is held at the right, pointing at the "+".
// Left click / the big "Throw" button (the same rules as the axe's swing: not while a window is open, not while the
// crosshair shows something to take or open): it is drawn back over the shoulder, thrust forward, and leaves the
// hand as a flying spear (ThrownSpear) aimed at the "+". The spear item leaves the inventory; another spear comes to
// the hand if there is one (Spear.AfterThrow).
public class SpearHeld : MonoBehaviour
{
    public static SpearHeld Current { get; private set; }

    enum Phase { Raising, Ready, WindUp, Thrust, Gone }
    Phase phase = Phase.Raising;
    float phaseTime;
    bool windowOpenLastFrame;
    Vector3 windUpFrom;

    // where the grip is, in front of the camera (metres: right, up, forward)
    static readonly Vector3 ReadyAt = new Vector3(0.30f, -0.27f, 0.48f);
    static readonly Vector3 LoweredAt = new Vector3(0.34f, -0.75f, 0.30f);  // coming up into the hand
    static readonly Vector3 DrawnBackAt = new Vector3(0.36f, -0.10f, 0.02f); // over the shoulder
    static readonly Vector3 ThrownAt = new Vector3(0.20f, -0.14f, 0.95f);   // the arm stretched out
    const float RaiseSeconds = 0.3f, WindUpSeconds = 0.24f, ThrustSeconds = 0.09f;

    public static GameObject Create(Transform holder)
    {
        var go = new GameObject("spear_Model");
        go.transform.SetParent(holder, false);
        var model = Spear.BuildModel("Spear in the hand");
        model.transform.SetParent(go.transform, false);
        Spear.SetLayer(go, holder.gameObject.layer); // drawn by the camera of the things in the hand
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var held = go.AddComponent<SpearHeld>();
        var cam = Camera.main;
        if (cam != null) held.Place(cam.transform, LoweredAt, 0f); // (already in place in the frame it is made)
        return go;
    }

    void OnEnable() => Current = this;

    void OnDisable()
    {
        if (Current == this) Current = null;
    }

    void OnDestroy()
    {
        var equip = EquipSystem.Instance;
        if (equip != null && equip.selectedItemModel == gameObject) equip.selectedItemModel = null;
    }

    void Update()
    {
        if (phase == Phase.Ready && GameInput.PrimaryDown) BeginThrow(false);
    }

    // starts the throw (false when it can't now); 'fromScript': a test, the crosshair rules are not checked
    public bool BeginThrow(bool fromScript)
    {
        if (phase != Phase.Ready) return false;
        if (!fromScript && !CanThrowNow()) return false;
        windUpFrom = ReadyAt;
        phase = Phase.WindUp;
        phaseTime = 0f;
        return true;
    }

    // the same rules as the axe's swing (EquipableItem), and not when the crosshair shows something to take or open
    bool CanThrowNow()
    {
        var selection = SelectionManager.Instance;
        if (selection == null || !selection.enabled) return false;
        if (selection.handIsVisible || selection.selectedStorageBox != null || selection.selectedCampfire != null || selection.selectedDoor != null) return false;
        if (windowOpenLastFrame || WindowOpen()) return false;
        if (ConstructionManager.Instance != null && ConstructionManager.Instance.inConstructionMode) return false;
        if (PlacementSystem.Instance != null && PlacementSystem.Instance.inPlacementMode) return false;
        if (SeedPlanting.Active || SnarePlacing.Active || FencePlacing.Active) return false;
        var player = PlayerState.Instance != null ? PlayerState.Instance.playerBody : null;
        var death = player != null ? player.GetComponent<PlayerDeath>() : null;
        return death == null || !death.IsDead;
    }

    static bool WindowOpen()
    {
        return Time.timeScale <= 0f
            || (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (CraftingSystem.Instance != null && CraftingSystem.Instance.isOpen)
            || (StorageManager.Instance != null && StorageManager.Instance.storageUIOpen)
            || (CampfireUIManager.Instance != null && CampfireUIManager.Instance.isUiOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen)
            || MapScreen.IsOpen || BookReader.IsOpen;
    }

    // (after the camera has moved: the spear stays still in the view)
    void LateUpdate()
    {
        windowOpenLastFrame = WindowOpen();
        var cam = Camera.main;
        if (cam == null) return;
        phaseTime += Time.deltaTime;

        Vector3 at = ReadyAt;
        float tilt = 0f; // + raises the point
        switch (phase)
        {
            case Phase.Raising:
            {
                float k = Mathf.Clamp01(phaseTime / RaiseSeconds);
                at = Vector3.Lerp(LoweredAt, ReadyAt, 1f - (1f - k) * (1f - k));
                if (k >= 1f) { phase = Phase.Ready; phaseTime = 0f; }
                break;
            }
            case Phase.Ready:
                at = ReadyAt + new Vector3(0f, Mathf.Sin(Time.time * 1.7f) * 0.006f, Mathf.Sin(Time.time * 0.9f) * 0.004f); // breathing
                break;
            case Phase.WindUp:
            {
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(phaseTime / WindUpSeconds));
                at = Vector3.Lerp(windUpFrom, DrawnBackAt, k);
                tilt = 9f * k;
                if (phaseTime >= WindUpSeconds) { phase = Phase.Thrust; phaseTime = 0f; }
                break;
            }
            case Phase.Thrust:
            {
                float k = Mathf.Clamp01(phaseTime / ThrustSeconds);
                at = Vector3.Lerp(DrawnBackAt, ThrownAt, k * k);
                tilt = 9f * (1f - k);
                break;
            }
            case Phase.Gone:
                return;
        }

        Place(cam.transform, at, tilt);
        if (phase == Phase.Thrust && phaseTime >= ThrustSeconds) Release();
    }

    // the grip at 'at' in front of the camera, the point towards the "+" (14 m ahead), raised by 'tilt' degrees
    void Place(Transform view, Vector3 at, float tilt)
    {
        Vector3 grip = view.TransformPoint(at);
        Vector3 aim = view.position + view.forward * 14f - grip;
        transform.SetPositionAndRotation(grip, Quaternion.LookRotation(aim, view.up) * Quaternion.Euler(-tilt, 0f, 0f));
    }

    // it leaves the hand: a flying spear from here, aimed at the "+", with a little spread
    void Release()
    {
        phase = Phase.Gone;
        Vector3 tip = transform.position + transform.forward * Spear.TipForward;
        Vector3 target = Spear.AimPoint();
        Vector3 velocity = Spear.LaunchVelocity(tip, target);
        velocity = Quaternion.Euler(Random.Range(-0.4f, 0.4f), Random.Range(-0.5f, 0.5f), 0f) * velocity;
        Spear.Launch(transform.position, Quaternion.LookRotation(velocity), velocity);
        if (SoundManager.Instance != null) SoundManager.Instance.PlaySwing();
        Spear.AfterThrow(gameObject);
    }
}
