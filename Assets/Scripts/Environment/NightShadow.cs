using UnityEngine;
using UnityEngine.UI;

// At night, when no campfire burns near the player, a tall, thin black figure with small glowing white eyes
// walks out of the dark (its look and pose: ShadowFigure.cs).
// A warning at the top of the screen counts down (secondsUntilAttack, 20 by default) while the shadow walks
// closer and closer, raising its long arms towards the player, and the music turns scary (a low drone,
// trembling strings, a heartbeat that gets faster). When the time runs out it grabs the player and they die.
// Lighting a fire (or going back to one that burns) makes it fade away; so does the morning.
// It stands in the world (it does not move along with the player); now and then it vanishes for a moment and
// shows up again a little to the left or right, and how see-through it is keeps changing.
// It sits in the game scene as "Night Shadow" (added by Editor/NightShadowSceneSetup.cs, menu SurvivalBD > Night
// Shadow): select it to change the numbers. If a scene has none, EnvironmentBootstrap makes one from the prefab
// Assets/Resources/NightShadow.prefab.
public class NightShadow : MonoBehaviour
{
    [Header("Danger")]
    [Tooltip("Seconds from when the shadow appears until it grabs you, if you don't light a fire")]
    public float secondsUntilAttack = 20f;
    [Tooltip("A burning campfire closer than this (metres) keeps the shadow away")]
    public float fireSafeDistance = 20f;
    [Tooltip("Seconds in the dark without a fire before the shadow appears")]
    public float secondsBeforeItAppears = 3f;
    [Tooltip("No shadow for this many seconds after the game starts or after you die")]
    public float safeSecondsAfterStartOrDeath = 15f;
    [Tooltip("The shadow can come when the daylight is below this (0 = deep night, 1 = full day)")]
    [Range(0f, 1f)] public float darkness = 0.1f;

    [Header("Look and sound")]
    [Tooltip("How far away the shadow appears (metres)")]
    public float startDistance = 40f;
    [Tooltip("How big the shadow is (1 = 2.5 m tall, 1.4 = 3.5 m)")]
    [Range(0.5f, 3f)] public float size = 1.4f;
    [Tooltip("Volume of the scary music (the Ambience slider in Settings changes it too)")]
    [Range(0f, 1f)] public float musicVolume = 0.8f;

    public static NightShadow Instance { get; private set; }
    // 0 = no shadow, 1 = it is about to grab you
    public static float Fear { get; private set; }
    // how much the scary music plays (0..1); the forest sounds get quieter while it does (AmbienceAudio)
    public static float Presence { get; private set; }

    public bool IsHunting => state == State.Hunting || state == State.Attacking;
    public float TimeLeft => timeLeft;

    const float CloseDistance = 2.2f;  // where it stands when the time is up
    const float AttackSeconds = 0.9f;  // the final lunge
    const float MaxWalkSpeed = 4f;     // metres per second (if you run away, it jumps closer when it reappears)

    enum State { Safe, Waiting, Hunting, Retreating, Attacking }
    State state = State.Safe;
    float stateTime;
    float timeLeft;
    float safeUntil;
    float nextFireCheck;
    bool fireNear, fireFar;

    PlayerMovement player;
    PlayerDeath death;

    // the creature
    ShadowFigure figure;
    float visible;      // 0..1 fade in / out
    float walkPhase;    // the walking cycle (one full turn = two steps)
    float stepAmount;   // 0 = standing still, 1 = walking
    bool jumped;        // it was moved at once this frame (appeared or reappeared): no steps for that
    Vector3 moveDir;    // flat direction it walks in (smoothed)
    float wanderSeed;   // its zig-zag path
    int lastStep;
    Vector3 attackFrom;

    // vanishing for a moment and reappearing a bit to the side
    int glitchStage;    // 0 = normal, 1 = vanishing, 2 = coming back
    float glitchAlpha = 1f;
    float nextGlitch;

    // sound
    AudioSource drone, strings, oneShots, whisper, footsteps;
    float nextBeat, beatPulse, nextWhisper;

    // screen
    GameObject ui;
    Text title, message;
    RawImage vignette;
    Texture2D vignetteTexture;
    float vignetteAlpha;
    float messageUntil;

    // Called by EnvironmentBootstrap when the game scene loads.
    public static void Create()
    {
        if (FindObjectOfType<NightShadow>() != null) return;
        var prefab = Resources.Load<GameObject>("NightShadow");
        var go = prefab != null ? Instantiate(prefab) : new GameObject();
        go.name = "Night Shadow (made in code)";
        if (go.GetComponent<NightShadow>() == null) go.AddComponent<NightShadow>();
    }

    void Awake() => Instance = this;

    void Start()
    {
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        safeUntil = Time.time + safeSecondsAfterStartOrDeath;
        timeLeft = Mathf.Max(1f, secondsUntilAttack * GameSettings.ShadowTime);
        figure = ShadowFigure.Build(transform);
        BuildSound();
        BuildUI();
        figure.gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            Fear = 0f;
            Presence = 0f;
        }
        if (ui != null) Destroy(ui);
        if (vignetteTexture != null) Destroy(vignetteTexture);
    }

    void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerMovement>();
            if (player == null) return;
            death = player.GetComponent<PlayerDeath>();
        }
        var cam = Camera.main;
        if (cam == null) return;

        float dt = Time.deltaTime;
        float total = Mathf.Max(1f, secondsUntilAttack * GameSettings.ShadowTime);
        Vector3 playerPos = player.transform.position;

        bool dead = (death != null && death.IsDead) || PlayerState.Instance == null || PlayerState.Instance.currentHealth <= 0f;
        if (dead)
        {
            safeUntil = Time.time + safeSecondsAfterStartOrDeath;
            if (state != State.Safe) Vanish();
        }

        bool night = IsNight();
        if (night && Time.time >= nextFireCheck)
        {
            nextFireCheck = Time.time + 0.25f;
            CheckFires(playerPos);
        }
        bool danger = night && !fireNear && !dead && Time.time >= safeUntil;

        stateTime += dt;
        switch (state)
        {
            case State.Safe:
                Refill(dt, total);
                if (danger) SetState(State.Waiting);
                break;
            case State.Waiting:
                Refill(dt, total);
                if (!danger) SetState(State.Safe);
                else if (stateTime >= secondsBeforeItAppears) Appear(cam, playerPos, total);
                break;
            case State.Hunting:
                if (!danger)
                {
                    Retreat(night);
                    break;
                }
                timeLeft -= dt;
                if (timeLeft <= 0f)
                {
                    timeLeft = 0f;
                    attackFrom = figure.transform.position;
                    oneShots.PlayOneShot(ShadowSounds.Scream, SoundManager.EffectsVolume);
                    SetState(State.Attacking);
                }
                break;
            case State.Retreating:
                Refill(dt, total);
                if (danger) SetState(State.Hunting); // it comes back
                break;
            case State.Attacking:
                if (stateTime >= AttackSeconds) Grab();
                break;
        }

        UpdateCreature(cam, playerPos, dt, total);
        UpdateSound(dt);
        UpdateUI(dt);
    }

    // ---------------------------------------------------------------------------------------------------------
    // What the shadow does

    void SetState(State next)
    {
        state = next;
        stateTime = 0f;
    }

    // while safe the time comes back (full again after 5 seconds)
    void Refill(float dt, float total) => timeLeft = Mathf.Min(total, timeLeft + dt * total / 5f);

    // how far from the player the countdown wants it
    float DistanceFor(float total) => Mathf.Lerp(CloseDistance, Mathf.Max(startDistance, CloseDistance + 3f), Mathf.Clamp01(timeLeft / total));

    void Appear(Camera cam, Vector3 playerPos, float total)
    {
        // somewhere in front of the player, so they see it coming
        var direction = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * Flat(cam.transform.forward);
        var p = playerPos + direction * DistanceFor(total);
        p.y = GroundAt(p);
        var root = figure.transform;
        root.position = p;
        root.rotation = Quaternion.LookRotation(-direction);
        root.localScale = Vector3.one * Size;
        moveDir = -direction;
        wanderSeed = Random.Range(0f, 100f);
        figure.gameObject.SetActive(true);
        figure.ClearSmoke();

        visible = 0f;
        stepAmount = 0f;
        jumped = true;
        glitchStage = 0;
        glitchAlpha = 1f;
        nextGlitch = Time.time + Random.Range(3f, 6f);
        oneShots.PlayOneShot(ShadowSounds.Stinger, SoundManager.EffectsVolume * 0.9f);
        nextBeat = Time.time + 0.8f;
        nextWhisper = Time.time + Random.Range(2f, 4f);
        SetState(State.Hunting);
    }

    void Retreat(bool night)
    {
        SetState(State.Retreating);
        messageUntil = Time.time + 3f;
        message.text = prayed ? Lang.T("The prayer drives the shadow away...", "Rugăciunea alungă umbra...")
            : fireNear ? (TorchLight.Burning ? Lang.T("The torch drives the shadow away...", "Torța alungă umbra...") : Lang.T("The fire drives the shadow away...", "Focul alungă umbra..."))
            : night ? Lang.T("The shadow fades away...", "Umbra se risipește...") : Lang.T("The daylight drives the shadow away...", "Lumina zilei alungă umbra...");
        prayed = false;
    }

    // The prayer book was opened (BookReader): the shadow goes away and keeps away for a while (game hours).
    // With the fire it is the second way to be safe from it.
    bool prayed;
    public void Banish(float gameHours)
    {
        float hoursPerSecond = Mathf.Max(0.0001f, DayNightSystem.WeatherHoursPerSecond);
        safeUntil = Mathf.Max(safeUntil, Time.time + gameHours / hoursPerSecond);
        if (state == State.Hunting || state == State.Attacking)
        {
            prayed = true;
            Retreat(true);
        }
        else if (state == State.Waiting) SetState(State.Safe);
    }

    void Grab()
    {
        if (death != null) death.SetNextDeathMessage(Lang.T("The shadow took you...", "Umbra te-a luat..."));
        if (PlayerState.Instance != null) PlayerState.Instance.currentHealth = 0f; // PlayerDeath takes it from here
        safeUntil = Time.time + safeSecondsAfterStartOrDeath;
        Vanish();
    }

    void Vanish()
    {
        SetState(State.Safe);
        visible = 0f;
        Fear = 0f;
        if (figure != null) figure.gameObject.SetActive(false);
    }

    bool IsNight()
    {
        if (SkyLighting.Instance != null) return SkyLighting.Instance.DayLight < darkness;
        if (DayNightSystem.Instance != null)
        {
            float hour = DayNightSystem.Instance.Hour;
            return hour >= 21f || hour < 5f;
        }
        return false;
    }

    void CheckFires(Vector3 playerPos)
    {
        fireNear = fireFar = false;
        // a burning torch in the hand is fire too: the shadow keeps away from it (outside the cave as in it)
        if (TorchLight.Burning) { fireNear = true; return; }
        foreach (var fire in FindObjectsOfType<Campfire>())
        {
            if (!fire.IsBurning) continue;
            if ((fire.FireCentre - playerPos).sqrMagnitude <= fireSafeDistance * fireSafeDistance) fireNear = true;
            else fireFar = true;
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // How it moves

    void UpdateCreature(Camera cam, Vector3 playerPos, float dt, float total)
    {
        if (state == State.Safe || state == State.Waiting)
        {
            Fear = 0f;
            return;
        }

        bool leaving = state == State.Retreating;
        visible = Mathf.MoveTowards(visible, leaving ? 0f : 1f, dt / (leaving ? 1.5f : 1.2f));
        if (leaving && visible <= 0f)
        {
            Vanish();
            return;
        }

        float fear = state == State.Attacking ? 1f : 1f - Mathf.Clamp01(timeLeft / total);
        Fear = fear * visible;
        Vector3 camPos = cam.transform.position;
        var root = figure.transform;
        Vector3 before = root.position;

        UpdateGlitch(dt, playerPos, total);

        float scale = Size;
        root.localScale = Vector3.one * scale;
        float current = 0f;

        if (state == State.Attacking)
        {
            // it rushes at the player's face
            float k = Mathf.SmoothStep(0f, 1f, stateTime / AttackSeconds);
            Vector3 front = camPos + Flat(cam.transform.forward) * (0.8f * scale);
            front.y = camPos.y - ShadowFigure.EyeHeight * scale; // its eyes at the height of the player's eyes
            root.position = Vector3.Lerp(attackFrom, front, k);
        }
        else
        {
            // it stays where it is in the world and walks towards the player, as fast as the countdown asks
            // (it never backs away if the player comes closer). It does not come in a straight line: it drifts
            // left and right on the way, so you can see it walk.
            Vector3 offset = root.position - playerPos;
            offset.y = 0f;
            current = offset.magnitude;
            Vector3 dir = current > 0.01f ? offset / current : -root.forward;
            float wanted = DistanceFor(total);
            float next;
            if (leaving) next = current + dt * 2.5f; // backs off while it fades
            else if (current > wanted)
            {
                next = Mathf.MoveTowards(current, wanted, dt * MaxWalkSpeed);
                float sideways = Mathf.Sin(Time.time * 0.45f + wanderSeed) * 1.4f; // m/s, changes slowly
                if (current < 8f) sideways *= current / 8f; // straight at you at the end
                dir = Quaternion.Euler(0f, sideways * dt / Mathf.Max(current, 1f) * Mathf.Rad2Deg, 0f) * dir;
            }
            else next = Mathf.Max(current, CloseDistance);
            Vector3 p = playerPos + dir * next;
            p.y = GroundAt(p) + 0.05f * scale * stepAmount * Mathf.Abs(Mathf.Cos(walkPhase)); // a small rise at every step
            root.position = p;
        }

        // walking: the steps follow how far it really moved
        Vector3 moved = root.position - before;
        moved.y = 0f;
        float speed = jumped || dt <= 0f ? 0f : moved.magnitude / dt;
        jumped = false;
        stepAmount = Mathf.MoveTowards(stepAmount, Mathf.Clamp01(speed / 0.6f), dt * 3f);
        if (speed > 0.05f) moveDir = Vector3.Slerp(moveDir, moved / (speed * dt), Mathf.Clamp01(dt * 5f)).normalized;
        float stride = 1.1f * scale; // metres per step
        walkPhase += Mathf.Min(speed, 5f) * dt / stride * Mathf.PI;
        PlayFootsteps(scale);

        // it faces where it walks (its head still turns to look at you); close by, or standing, it faces you
        Vector3 toPlayer = Flat(camPos - root.position);
        bool walking = stepAmount > 0.3f && moveDir.sqrMagnitude > 0.5f && state != State.Attacking && (leaving || current > 6f);
        Vector3 face = walking ? Flat(moveDir) : toPlayer;
        root.rotation = Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(face), dt * (state == State.Attacking ? 720f : 120f));

        // how see-through it is keeps changing (the shader also makes it uneven over the body)
        float t = Time.time;
        float flicker = 0.6f + 0.4f * Mathf.PerlinNoise(t * 0.9f, 3.7f);
        if (Mathf.PerlinNoise(t * 2.3f, 9.1f) > 0.78f) flicker *= 0.55f; // short moments when it almost fades out
        figure.Alpha = state == State.Attacking ? 1f : visible * glitchAlpha * flicker;
        figure.Animate(dt, camPos, fear, stepAmount, walkPhase, state == State.Attacking);
    }

    float Size => Mathf.Max(0.3f, size);

    // a soft heavy step each time one of its feet comes down
    void PlayFootsteps(float scale)
    {
        int step = Mathf.FloorToInt((walkPhase - Mathf.PI * 0.5f) / Mathf.PI);
        if (step == lastStep) return;
        lastStep = step;
        if (stepAmount < 0.4f || visible < 0.3f) return;
        footsteps.pitch = Random.Range(0.8f, 0.95f) / Mathf.Sqrt(scale / 1.4f);
        footsteps.PlayOneShot(ShadowSounds.Step, SoundManager.EffectsVolume * 0.8f);
    }

    // Every few seconds it vanishes for a moment and comes back a little to the left or right
    // (and at the distance the countdown wants, so running away does not help for long).
    void UpdateGlitch(float dt, Vector3 playerPos, float total)
    {
        if (state != State.Hunting)
        {
            glitchStage = 0;
            glitchAlpha = Mathf.MoveTowards(glitchAlpha, 1f, dt * 4f);
            return;
        }

        if (glitchStage == 0 && Time.time >= nextGlitch && visible >= 1f) glitchStage = 1;

        if (glitchStage == 1)
        {
            glitchAlpha = Mathf.MoveTowards(glitchAlpha, 0f, dt / 0.12f);
            if (glitchAlpha > 0f) return;

            var root = figure.transform;
            Vector3 offset = root.position - playerPos;
            offset.y = 0f;
            float distance = Mathf.Max(CloseDistance, Mathf.Min(offset.magnitude, DistanceFor(total)));
            Vector3 dir = offset.sqrMagnitude > 0.01f ? offset.normalized : -root.forward;
            float metres = Random.Range(1.2f, 3f) * (Random.value < 0.5f ? -1f : 1f);
            float angle = Mathf.Clamp(metres / Mathf.Max(distance, 1f) * Mathf.Rad2Deg, -35f, 35f);
            Vector3 p = playerPos + Quaternion.Euler(0f, angle, 0f) * dir * distance;
            p.y = GroundAt(p);
            root.position = p;
            jumped = true;
            glitchStage = 2;
        }
        else if (glitchStage == 2)
        {
            glitchAlpha = Mathf.MoveTowards(glitchAlpha, 1f, dt / 0.3f);
            if (glitchAlpha >= 1f)
            {
                glitchStage = 0;
                nextGlitch = Time.time + Random.Range(2.5f, 6f);
            }
        }
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 1e-6f ? Vector3.forward : v.normalized;
    }

    static float GroundAt(Vector3 p)
    {
        float y = p.y - 1.5f;
        var terrain = Terrain.activeTerrain;
        if (terrain != null) y = terrain.SampleHeight(p) + terrain.transform.position.y;
        if (Ocean.Instance != null) y = Mathf.Max(y, Ocean.Instance.Level); // over the sea it walks on the water
        return y;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Sound

    void BuildSound()
    {
        // made now, so nothing stutters when the shadow appears
        _ = ShadowSounds.Heartbeat;
        _ = ShadowSounds.Stinger;
        _ = ShadowSounds.Scream;
        _ = ShadowSounds.Whispers;
        _ = ShadowSounds.Step;

        drone = NewSource("Shadow music (drone)", transform, ShadowSounds.Drone, true);
        strings = NewSource("Shadow music (strings)", transform, ShadowSounds.Strings, true);
        oneShots = NewSource("Shadow sounds", transform, null, false);
        whisper = NewSource("Shadow whispers", figure.Head, null, false);
        whisper.spatialBlend = 1f;
        whisper.rolloffMode = AudioRolloffMode.Linear;
        whisper.minDistance = 4f;
        whisper.maxDistance = 60f;
        whisper.dopplerLevel = 0f;
        whisper.volume = 1f;
        oneShots.volume = 1f;

        footsteps = NewSource("Shadow footsteps", figure.transform, null, false);
        footsteps.spatialBlend = 1f;
        footsteps.rolloffMode = AudioRolloffMode.Linear;
        footsteps.minDistance = 3f;
        footsteps.maxDistance = 45f;
        footsteps.dopplerLevel = 0f;
        footsteps.volume = 1f;
    }

    static AudioSource NewSource(string name, Transform parent, AudioClip clip, bool loop)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.priority = 10;
        return source;
    }

    void UpdateSound(float dt)
    {
        bool hunting = state == State.Hunting || state == State.Attacking;
        Presence = Mathf.MoveTowards(Presence, hunting ? 1f : 0f, dt * (hunting ? 0.8f : 0.35f));
        float music = SoundManager.AmbienceVolume * musicVolume;
        float fear = Fear;

        SetLoop(drone, music * 0.75f * Presence, 1f + 0.06f * fear);
        SetLoop(strings, music * 0.55f * Presence * (0.12f + 0.88f * Mathf.Pow(fear, 1.3f)), 1f + 0.05f * fear);

        // the heartbeat gets faster and louder as the time runs out
        beatPulse = Mathf.MoveTowards(beatPulse, 0f, dt * 4f);
        if (Presence > 0.2f && Time.time >= nextBeat)
        {
            oneShots.PlayOneShot(ShadowSounds.Heartbeat, music * Presence * Mathf.Lerp(0.5f, 1f, fear));
            nextBeat = Time.time + Mathf.Lerp(1.05f, 0.38f, fear);
            beatPulse = 1f;
        }

        if (state == State.Hunting && visible > 0.5f && Time.time >= nextWhisper)
        {
            nextWhisper = Time.time + Random.Range(3f, 7f);
            var clips = ShadowSounds.Whispers;
            whisper.pitch = Random.Range(0.85f, 1.05f);
            whisper.PlayOneShot(clips[Random.Range(0, clips.Length)], SoundManager.EffectsVolume * 0.9f);
        }
    }

    static void SetLoop(AudioSource source, float volume, float pitch)
    {
        source.volume = volume;
        source.pitch = pitch;
        if (volume > 0.001f)
        {
            if (!source.isPlaying) source.Play();
        }
        else if (source.isPlaying) source.Stop();
    }

    // ---------------------------------------------------------------------------------------------------------
    // Screen: the countdown at the top and the darkness closing in from the edges

    void UpdateUI(float dt)
    {
        bool hunting = state == State.Hunting || state == State.Attacking;
        bool showMessage = hunting || Time.time < messageUntil;
        float darkTarget = state == State.Attacking ? 1f : hunting ? visible * (0.35f + 0.6f * Fear) : 0f;
        vignetteAlpha = Mathf.MoveTowards(vignetteAlpha, darkTarget, dt * (darkTarget > vignetteAlpha ? 2f : 1.5f));

        bool show = showMessage || vignetteAlpha > 0.005f;
        if (ui.activeSelf != show) ui.SetActive(show);
        if (!show) return;

        vignette.color = new Color(0f, 0f, 0f, Mathf.Clamp01(vignetteAlpha * (0.9f + 0.1f * beatPulse)));
        float scale = state == State.Attacking
            ? Mathf.Lerp(1f, 0.55f, stateTime / AttackSeconds)
            : Mathf.Lerp(1.45f, 1f, Fear) - 0.03f * beatPulse;
        vignette.rectTransform.localScale = new Vector3(scale, scale, 1f);

        if (hunting)
        {
            title.gameObject.SetActive(true);
            message.gameObject.SetActive(true);
            title.text = fireFar ? Lang.T("GET BACK TO YOUR FIRE!", "ÎNTOARCE-TE LA FOC!") : Lang.T("LIGHT A FIRE!", "APRINDE UN FOC!");
            message.text = state == State.Attacking ? "..." : Lang.T("The shadow will take you in ", "Umbra te ia în ") + Mathf.CeilToInt(timeLeft) + "...";
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(5f, 14f, Fear));
            title.color = Color.Lerp(new Color(1f, 0.2f, 0.15f), new Color(1f, 0.85f, 0.3f), pulse * 0.45f);
            message.color = Color.Lerp(Color.white, new Color(1f, 0.55f, 0.5f), Fear);
        }
        else
        {
            title.gameObject.SetActive(false);
            message.gameObject.SetActive(showMessage);
            message.color = new Color(1f, 0.8f, 0.45f);
        }
    }

    void BuildUI()
    {
        ui = new GameObject("NightShadowWarning");
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 890; // under the deep water warning and the death screen
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        vignetteTexture = MakeVignetteTexture();
        vignette = new GameObject("Darkness").AddComponent<RawImage>();
        vignette.transform.SetParent(ui.transform, false);
        vignette.texture = vignetteTexture;
        vignette.raycastTarget = false;
        vignette.color = new Color(0f, 0f, 0f, 0f);
        var rect = vignette.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        title = CreateText("Title", 64, new Vector2(0f, 230f));
        message = CreateText("Message", 40, new Vector2(0f, 160f));
        ui.SetActive(false);
    }

    Text CreateText(string name, int size, Vector2 position)
    {
        var text = new GameObject(name).AddComponent<Text>();
        text.transform.SetParent(ui.transform, false);
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.raycastTarget = false;
        text.color = Color.white;
        var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>(); // not QuickOutline's Outline
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1600f, size * 1.5f);
        rect.anchoredPosition = position;
        return text;
    }

    // black edges with dark fingers creeping in towards the middle
    static Texture2D MakeVignetteTexture()
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "SB_Shadow_Darkness",
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.DontSave,
        };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dy, dx);
                float big = Mathf.PerlinNoise(Mathf.Cos(angle) * 2.2f + 5f, Mathf.Sin(angle) * 2.2f + 5f);
                float fine = Mathf.PerlinNoise(Mathf.Cos(angle) * 6f + 9f, Mathf.Sin(angle) * 6f + 9f);
                float rr = r + (big - 0.5f) * 0.45f + (fine - 0.5f) * 0.15f;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1.15f, rr));
                px[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }
}
