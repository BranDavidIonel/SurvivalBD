using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// The raft of the last level (Resources/RaftModel, made by Tools > Nature). When Level 9 begins it lies on the beach
// by the wreck, only started. The player brings the materials in as many trips as needed (look at it and click:
// everything needed that the player carries goes onto it) and it grows: logs, beams, ropes, the mast, the sail.
// Finished, it slides into the sea and floats on the waves. Board it and the game ends: the raft sails away, the
// island gets smaller behind it, the end is told, then back to the main menu.
public class Raft : MonoBehaviour
{
    public static Raft Instance { get; private set; }
    public static event System.Action Boarded;

    public static readonly string[] Needs = { "log", "plank", "rope", "stick", "hay", "Cooked Meat" }; // the meat: food for the journey
    static readonly string[] NeedNamesEn = { "logs", "planks", "ropes", "sticks", "hay", "cooked meat" };
    static readonly string[] NeedNamesRo = { "bușteni", "scânduri", "frânghii", "bețe", "fân", "carne gătită" };
    public static string[] NeedNames => Lang.Ro ? NeedNamesRo : NeedNamesEn;
    public static readonly int[] Amounts = { 12, 8, 6, 16, 10, 5 };

    public int[] have = new int[6];
    public bool IsBoarded { get; private set; }
    public bool InWater { get; private set; }
    public bool IsComplete { get { for (int i = 0; i < Needs.Length; i++) if (have[i] < Amounts[i]) return false; return true; } }
    public bool Started { get { foreach (int h in have) if (h > 0) return true; return false; } }

    const float SlideDistance = 12f; // from the beach into the sea, when it is finished
    float phase;
    bool launching;

    void Awake()
    {
        Instance = this;
        phase = Random.Range(0f, 10f);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Level 9: the raft (only started) on the beach by the wreck, where the story began
    public static Raft MakeSite(int[] saved)
    {
        if (Instance != null) return Instance;
        var prefab = Resources.Load<GameObject>("RaftModel");
        var ocean = Ocean.Instance;
        var start = FindObjectOfType<StoryStart>();
        if (prefab == null || ocean == null) return null;
        Vector3 from = start != null ? start.transform.position : (PlayerState.Instance != null ? PlayerState.Instance.playerBody.transform.position : ocean.transform.position);
        Vector3 seaward = from - ocean.transform.position; seaward.y = 0f; seaward.Normalize();
        Vector3 spot = from;
        for (float d = 0f; d < 80f; d += 1f)
        {
            Vector3 q = from + seaward * d;
            if (Forest.GroundY(q) < ocean.Level + 0.15f) { spot = q - seaward * 4.5f; break; }
        }
        spot += Vector3.Cross(Vector3.up, seaward) * 6f; // a little beside the wreck
        spot.y = Forest.GroundY(spot);
        var go = Instantiate(prefab, spot, Quaternion.LookRotation(seaward, Vector3.up));
        go.name = "Raft";
        var raft = go.GetComponent<Raft>();
        if (saved != null) for (int i = 0; i < Mathf.Min(saved.Length, Needs.Length); i++) raft.have[i] = saved[i]; // (an older save has no meat yet)
        raft.ShowParts();
        if (raft.IsComplete) raft.PutInWater(true);
        return raft;
    }

    // the interaction text when the player looks at it
    public string Status
    {
        get
        {
            if (InWater) return Lang.T("Board the raft (leave the island)", "Urcă pe plută (pleci de pe insulă)");
            var sb = new System.Text.StringBuilder(Lang.T("Raft:", "Pluta:"));
            for (int i = 0; i < Needs.Length; i++) sb.Append((i == 3 ? "\n" : "  ") + NeedNames[i] + " " + have[i] + "/" + Amounts[i]);
            sb.Append(Lang.T("\n(click: add what you carry)", "\n(clic: pui ce ai la tine)"));
            return sb.ToString();
        }
    }

    // the player clicked it: what is needed and carried goes onto the raft
    public void Use()
    {
        if (InWater) { Board(); return; }
        if (launching) return;
        var inventory = InventorySystem.Instance;
        var added = new System.Text.StringBuilder();
        for (int i = 0; i < Needs.Length; i++)
        {
            int take = Mathf.Min(Amounts[i] - have[i], inventory.CountItem(Needs[i]));
            if (take <= 0) continue;
            inventory.RemoveItem(Needs[i], take);
            have[i] += take;
            added.Append((added.Length > 0 ? ", " : "") + "+" + take + " " + NeedNames[i]);
        }
        if (added.Length == 0)
        {
            var missing = new System.Text.StringBuilder(Lang.T("Still needed:", "Mai trebuie:"));
            for (int i = 0; i < Needs.Length; i++) if (have[i] < Amounts[i]) missing.Append(" " + (Amounts[i] - have[i]) + " " + NeedNames[i]);
            inventory.ShowMessage(missing.ToString());
            return;
        }
        if (SoundManager.Instance != null) SoundManager.Instance.PlayCraft();
        inventory.ShowMessage(Lang.T("Raft: ", "Pluta: ") + added);
        ShowParts();
        if (IsComplete) PutInWater(false);
    }

    // the parts of the model appear as the materials come
    void ShowParts()
    {
        int logs = Mathf.CeilToInt(7f * have[0] / Amounts[0]);
        int beams = have[1] >= Amounts[1] ? 2 : have[1] > 0 ? 1 : 0;
        int ropes = Mathf.FloorToInt(3f * have[2] / Amounts[2]);
        bool mast = have[3] >= Amounts[3];
        bool done = IsComplete;
        int logIndex = 0, beamIndex = 0, ropeIndex = 0;
        foreach (Transform part in transform)
        {
            bool on = true;
            if (part.name.StartsWith("Log")) on = logIndex++ < Mathf.Max(1, logs);
            else if (part.name == "Beam") on = beamIndex++ < beams;
            else if (part.name == "Rope") on = ropeIndex++ < ropes;
            else if (part.name == "Mast" || part.name == "Yard") on = mast;
            else if (part.name == "Sail" || part.name == "Bundle") on = done;
            if (part.gameObject.activeSelf != on) part.gameObject.SetActive(on);
        }
    }

    // finished: it slides into the sea and floats
    void PutInWater(bool instant)
    {
        if (InWater || launching) return;
        if (instant)
        {
            transform.position += transform.forward * SlideDistance;
            InWater = true;
            return;
        }
        StartCoroutine(Slide());
    }

    IEnumerator Slide()
    {
        launching = true;
        if (InventorySystem.Instance != null) InventorySystem.Instance.ShowMessage(Lang.T("The raft is finished! It slides into the sea...", "Pluta e gata! Alunecă în mare..."));
        Vector3 from = transform.position, to = from + transform.forward * SlideDistance;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 4f)
        {
            Vector3 p = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
            float ground = Forest.GroundY(p);
            float water = Ocean.Instance != null ? Ocean.Instance.SurfaceHeight(p) + 0.12f : ground;
            p.y = Mathf.Max(ground, water);
            transform.position = p;
            yield return null;
        }
        launching = false;
        InWater = true;
    }

    void Update()
    {
        if (IsBoarded || !InWater) return;
        Float(transform.position);
    }

    // on the waves: up and down, rocking a little
    void Float(Vector3 at)
    {
        var ocean = Ocean.Instance;
        float h = ocean != null ? ocean.SurfaceHeight(at) : at.y;
        float t = Time.time + phase;
        at.y = Mathf.Max(h + 0.12f, Forest.GroundY(at) + 0.05f);
        transform.position = at;
        var yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        transform.rotation = yaw * Quaternion.Euler(Mathf.Sin(t * 0.9f) * 2.5f, 0f, Mathf.Sin(t * 0.7f + 1f) * 3.5f);
    }

    public void Board()
    {
        if (IsBoarded || !InWater) return;
        IsBoarded = true;
        if (Boarded != null) Boarded();
        StartCoroutine(Ending());
    }

    // ---------------- the end of the game ---------------- //

    IEnumerator Ending()
    {
        // nothing of the game any more: no moving, no windows, no shadow
        var player = FindObjectOfType<PlayerMovement>();
        if (player != null)
        {
            player.enabled = false;
            var look = player.GetComponent<MouseMovement>();
            if (look != null) look.enabled = false;
            var controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
        }
        if (SelectionManager.Instance != null) SelectionManager.Instance.enabled = false;
        if (NightShadow.Instance != null) NightShadow.Instance.Banish(999f);
        var equip = EquipSystem.Instance;
        if (equip != null && equip.selectedItemModel != null) Destroy(equip.selectedItemModel);
        foreach (var canvas in FindObjectsOfType<Canvas>()) if (canvas.isRootCanvas) canvas.enabled = false;
        if (DayNightSystem.Instance != null)
        {
            DayNightSystem.Instance.TimeOfDay = 17.8f / 24f; // sailing into the evening sun
            DayNightSystem.Instance.autoTime = false;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var cam = Camera.main;
        var ocean = Ocean.Instance;
        Vector3 island = ocean != null ? ocean.transform.position : Vector3.zero;
        Vector3 away = transform.position - island; away.y = 0f; away.Normalize();
        cam.transform.SetParent(transform, false);
        cam.transform.localPosition = new Vector3(0.3f, 1.55f, -0.9f);
        cam.farClipPlane = Mathf.Max(cam.farClipPlane, 4000f);

        var text = MakeText();
        float time = 0f, speed = 0f;
        Quaternion heading = Quaternion.LookRotation(away, Vector3.up);
        string[] lines = Lang.Ro ? new[]
        {
            "Am făcut o plută cu mâinile mele.",
            "Insula se face tot mai mică în urma mea: plaja, pădurile, muntele cu crucea lui.",
            "Rugăciunile Fratelui Ioan sunt cu mine. Umbra... poate rămâne pe insulă.",
            "Sau poate a fost mereu o parte din mine. Dar nu mă mai tem de ea.",
        } : new[]
        {
            "I built a raft with my own hands.",
            "The island gets smaller behind me: the beach, the forests, the mountain with its cross.",
            "Brother Ioan's prayers are with me. The shadow... maybe it stays on the island.",
            "Or maybe it was always a part of me. But I am not afraid of it any more.",
        };
        int shown = -1;
        bool end = false;
        while (true)
        {
            float dt = Time.deltaTime;
            time += dt;
            speed = Mathf.MoveTowards(speed, 7f, dt * 0.6f);
            // it turns slowly away from the island, then sails straight
            Quaternion current = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Quaternion turn = Quaternion.RotateTowards(current, heading, dt * 12f);
            Vector3 at = transform.position + (turn * Vector3.forward) * speed * dt;
            Float(at);
            transform.rotation = turn * Quaternion.Euler(Mathf.Sin(time) * 2f, 0f, Mathf.Sin(time * 0.8f) * 3f);

            // the camera: first on the raft looking back at the island, then it rises behind the raft
            Vector3 toIsland = island - transform.position; toIsland.y = 0f;
            if (time < 16f)
            {
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(toIsland.normalized + Vector3.up * 0.08f), dt * 1.5f);
            }
            else
            {
                // first up (above the mast), then back over the raft: never through the sail
                float up = Mathf.SmoothStep(0f, 1f, (time - 16f) / 4f);
                float back = Mathf.SmoothStep(0f, 1f, (time - 20f) / 12f);
                Vector3 target = transform.position + away * Mathf.Lerp(-1.5f, 22f, back) + Vector3.up * Mathf.Lerp(1.6f, 14f, up);
                cam.transform.position = Vector3.Lerp(cam.transform.position, target, dt * 1.2f);
                Vector3 look = Vector3.Lerp(transform.position, island, 0.35f) - cam.transform.position;
                cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.LookRotation(look), dt * 1.2f);
            }

            int line = Mathf.FloorToInt(time / 6.5f);
            if (line != shown && line < lines.Length)
            {
                shown = line;
                StartCoroutine(Say(text, lines[line]));
            }
            if (!end && time > lines.Length * 6.5f + 1f)
            {
                end = true;
                StartCoroutine(TheEnd());
            }
            if (end && time > lines.Length * 6.5f + 14f) break;
            yield return null;
        }
        if (DayNightSystem.Instance != null) DayNightSystem.Instance.autoTime = true;
        SceneManager.LoadScene("MainMenu");
    }

    CanvasGroup endGroup;

    Text MakeText()
    {
        var go = new GameObject("The end (made in code)");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        var text = UISkin.NewText("Story", go.transform, "", 38, new Color(1f, 0.95f, 0.85f), TextAnchor.MiddleCenter, false);
        UISkin.Place(text.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 120f), new Vector2(1500f, 140f));
        var outline = text.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.6f);

        var end = UISkin.NewRect("End", go.transform);
        UISkin.Fill(end);
        endGroup = end.gameObject.AddComponent<CanvasGroup>();
        endGroup.alpha = 0f;
        var dark = UISkin.NewImage("Dark", end, UISkin.White, new Color(0f, 0f, 0f, 0.55f), false);
        UISkin.Fill(dark.rectTransform);
        var title = UISkin.NewText("Title", end, Lang.T("THE END", "SFÂRȘIT"), 110, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Centered(title.rectTransform, new Vector2(0f, 90f), new Vector2(1400f, 160f));
        var credits = UISkin.NewText("Credits", end, Lang.T("SurvivalBD\na game by Bran David Ionel\n\nThank you for playing!", "SurvivalBD\nun joc de Bran David Ionel\n\nMulțumim că ai jucat!"), 40, UISkin.TextColor, TextAnchor.UpperCenter);
        UISkin.Centered(credits.rectTransform, new Vector2(0f, -110f), new Vector2(1400f, 260f));
        return text;
    }

    IEnumerator Say(Text text, string line)
    {
        for (float t = 1f; t > 0f; t -= Time.deltaTime / 0.5f) { text.color = new Color(1f, 0.95f, 0.85f, t); yield return null; }
        text.text = line;
        for (float t = 0f; t < 1f; t += Time.deltaTime / 0.8f) { text.color = new Color(1f, 0.95f, 0.85f, t); yield return null; }
        text.color = new Color(1f, 0.95f, 0.85f, 1f);
    }

    IEnumerator TheEnd()
    {
        for (float t = 0f; t < 1f; t += Time.deltaTime / 2.5f) { endGroup.alpha = t; yield return null; }
        endGroup.alpha = 1f;
    }
}
