using UnityEngine;
using UnityEngine.UI;

// Keeps the player near the island. Wading or swimming more than `warnDistance` metres out from the waterline
// shows a warning with a countdown; if the player does not head back to the beach before it runs out, they drown
// (PlayerDeath shows the death screen and puts them back at the start). Swimming back towards the beach pauses
// the countdown, and getting back closer than `warnDistance` cancels it.
[RequireComponent(typeof(Ocean))]
public class OceanSafety : MonoBehaviour
{
    public float warnDistance = 30f;
    public float secondsToTurnBack = 10f;

    Ocean ocean;
    PlayerMovement player;
    PlayerDeath death;

    bool warning;
    public static bool Warning { get; private set; } // the deep water warning is on the screen (the clock hides under it)
    float timeLeft;
    float lastDistance;
    float speedToBeach;

    GameObject ui;
    Text title;
    Text message;
    Image background;

    void Start()
    {
        ocean = GetComponent<Ocean>();
        CreateUI();
    }

    void OnDestroy()
    {
        Warning = false;
        if (ui != null) Destroy(ui);
    }

    void Update()
    {
        if (player == null)
        {
            player = FindObjectOfType<PlayerMovement>();
            if (player == null) return;
            death = player.GetComponent<PlayerDeath>();
            lastDistance = ocean.DistanceFromShore(player.transform.position);
        }

        var distance = ocean.DistanceFromShore(player.transform.position);
        var dead = (death != null && death.IsDead) || PlayerState.Instance == null || PlayerState.Instance.currentHealth <= 0f;

        // how fast the player is heading back to the beach (smoothed)
        if (Time.deltaTime > 0f)
        {
            var speed = (lastDistance - distance) / Time.deltaTime;
            speedToBeach = Mathf.Lerp(speedToBeach, speed, Mathf.Clamp01(Time.deltaTime * 6f));
        }
        lastDistance = distance;

        if (dead || distance < warnDistance - 1f)
        {
            SetWarning(false);
            return;
        }
        if (!warning)
        {
            if (distance <= warnDistance) return;
            SetWarning(true);
        }

        var goingBack = speedToBeach > 0.5f;
        if (!goingBack) timeLeft -= Time.deltaTime;
        if (timeLeft <= 0f)
        {
            SetWarning(false);
            PlayerState.Instance.currentHealth = 0f; // PlayerDeath takes it from here
            return;
        }

        var seconds = Mathf.CeilToInt(timeLeft);
        message.text = goingBack
            ? Lang.T("Good, keep going back to the beach! (", "Bine, întoarce-te la plajă! (") + seconds + ")"
            : Lang.T("Turn back now or you will drown in ", "Întoarce-te acum sau te îneci în ") + seconds + "...";
        title.text = Lang.T("DANGER! DEEP WATER!", "PERICOL! APĂ ADÂNCĂ!");
        // pulse
        var pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 8f);
        title.color = Color.Lerp(new Color(1f, 0.25f, 0.2f), new Color(1f, 0.85f, 0.3f), goingBack ? 0.7f : pulse * 0.4f);
        background.color = new Color(0.45f, 0f, 0f, goingBack ? 0.15f : 0.18f + 0.12f * pulse);
    }

    void SetWarning(bool on)
    {
        if (on == warning) return;
        warning = on;
        Warning = on;
        timeLeft = secondsToTurnBack;
        if (ui != null) ui.SetActive(on);
    }

    void CreateUI()
    {
        ui = new GameObject("DeepWaterWarning");
        var canvas = ui.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // under the death screen
        var scaler = ui.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // red tint over the whole screen
        background = new GameObject("Tint").AddComponent<Image>();
        background.transform.SetParent(ui.transform, false);
        background.raycastTarget = false;
        background.color = new Color(0.45f, 0f, 0f, 0.2f);
        var rect = background.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        title = CreateText("Title", 64, new Vector2(0f, 230f));
        title.text = "DANGER! DEEP WATER!";
        message = CreateText("Message", 40, new Vector2(0f, 160f));
        message.color = Color.white;

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
        text.color = new Color(1f, 0.25f, 0.2f);
        var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>(); // not QuickOutline's Outline
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(3f, -3f);
        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1600f, size * 1.5f);
        rect.anchoredPosition = position;
        return text;
    }
}
