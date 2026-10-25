using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// When health reaches 0: the screen goes dark with "Game over!", then the game goes back to the main menu.
// (With backToMainMenu off, the player comes back where the game started instead, with full health and air,
// keeping the inventory.)
public class PlayerDeath : MonoBehaviour
{
    public float deathScreenSeconds = 3f;
    public float fadeSeconds = 0.6f;
    public string deathMessage = "Game over!";
    public bool backToMainMenu = true;
    public string mainMenuScene = "MainMenu";

    public bool IsDead { get; private set; }

    Vector3 spawnPosition;
    CanvasGroup screen;
    Text messageText;
    string nextMessage;

    // Shows a different text on the next death screen only (for example the night shadow's).
    public void SetNextDeathMessage(string text) => nextMessage = text;

    void Start()
    {
        KeepAboveGround();
        spawnPosition = transform.position;
        CreateScreen();
    }

    void Update()
    {
        KeepAboveGround();
        if (!IsDead && PlayerState.Instance != null && PlayerState.Instance.currentHealth <= 0f)
        {
            StartCoroutine(Die());
        }
    }

    IEnumerator Die()
    {
        IsDead = true;
        if (messageText != null) messageText.text = string.IsNullOrEmpty(nextMessage) ? deathMessage : nextMessage;
        nextMessage = null;
        var movement = GetComponent<PlayerMovement>();
        var look = GetComponent<MouseMovement>();
        if (movement != null) movement.enabled = false;
        if (look != null) look.enabled = false;
        if (SoundManager.Instance != null && SoundManager.Instance.grassWalkSound != null) SoundManager.Instance.grassWalkSound.Stop();

        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(deathScreenSeconds);

        if (backToMainMenu)
        {
            // the game is over: back to the main menu (load a save or start a new game from there)
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SceneManager.LoadScene(mainMenuScene);
            yield break;
        }

        // back to the start (the controller has to be off to move the player directly)
        var controller = GetComponent<CharacterController>();
        if (controller != null) controller.enabled = false;
        transform.position = spawnPosition;
        if (controller != null) controller.enabled = true;
        if (movement != null) movement.ResetVelocity();

        PlayerState.Instance.currentHealth = PlayerState.Instance.maxHealth;
        var water = GetComponent<PlayerWater>();
        if (water != null) water.ResetBreath();

        if (movement != null) movement.enabled = true;
        if (look != null) look.enabled = true;
        IsDead = false;

        yield return Fade(1f, 0f);
    }

    // The terrain only blocks from above: a player that ends up under it (for example placed too low in the
    // editor) would fall forever, so put them back on top of the ground.
    void KeepAboveGround()
    {
        var terrain = Terrain.activeTerrain;
        var controller = GetComponent<CharacterController>();
        if (terrain == null || controller == null) return;

        var position = transform.position;
        var ground = terrain.SampleHeight(position) + terrain.transform.position.y;
        var head = position.y + controller.center.y + controller.height / 2f;
        if (head > ground - 1f) return;
        // inside the cave in the mountain the player is under the terrain on purpose
        if (Cave.Instance != null && Cave.Instance.Contains(position)) return;

        controller.enabled = false;
        transform.position = new Vector3(position.x, ground - controller.center.y + controller.height / 2f + 0.2f, position.z);
        controller.enabled = true;
        var movement = GetComponent<PlayerMovement>();
        if (movement != null) movement.ResetVelocity();
    }

    IEnumerator Fade(float from, float to)
    {
        screen.gameObject.SetActive(true);
        for (var t = 0f; t < fadeSeconds; t += Time.deltaTime)
        {
            screen.alpha = Mathf.Lerp(from, to, t / fadeSeconds);
            yield return null;
        }
        screen.alpha = to;
        if (to <= 0f) screen.gameObject.SetActive(false);
    }

    void CreateScreen()
    {
        var root = new GameObject("DeathScreen");
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        screen = root.AddComponent<CanvasGroup>();
        screen.blocksRaycasts = false;

        var background = new GameObject("Background").AddComponent<Image>();
        background.transform.SetParent(root.transform, false);
        background.color = Color.black;
        Stretch(background.rectTransform);

        var text = new GameObject("Message").AddComponent<Text>();
        text.transform.SetParent(root.transform, false);
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 72;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(0.85f, 0.1f, 0.1f);
        text.text = deathMessage;
        Stretch(text.rectTransform);
        messageText = text;

        root.SetActive(false);
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    void OnDestroy()
    {
        if (screen != null) Destroy(screen.gameObject);
    }
}
