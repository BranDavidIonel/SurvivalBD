using System.Collections.Generic;
using TMPro;
using UnityEngine;

// All the rabbits of the island, made in code when the game scene loads (EnvironmentBootstrap).
//  - every rabbit is male or female (RabbitInfo in the scene); a label above the near ones says which
//    ("Male" blue, "Female" pink, "Baby ...", "Female (expecting)")
//  - a grown male and a grown female that are both ready walk to each other; when they meet the female expects
//    babies, and after about a game day 1-3 little rabbits are born next to her; they grow up in about 1.5 days
//  - not more than maxRabbits (phone speed); far from the player rabbits stand still and are not drawn
//  - hunting works by distance (the axe reaches a rabbit within ~4 m) and the meat and hide fall on the ground
// The save keeps every living rabbit (EnviromentData.rabbits).
public class RabbitPopulation : MonoBehaviour
{
    public static RabbitPopulation Instance { get; private set; }

    public const float AdultHours = 36f;           // game hours to grow up

    [Tooltip("Not more rabbits than this on the island")]
    public int maxRabbits = 170;
    [Tooltip("Rabbits farther than this from the player stand still")]
    public float activeDistance = 140f;
    [Tooltip("Rabbits farther than this are not drawn")]
    public float drawDistance = 170f;
    [Tooltip("A ready rabbit looks for a partner this far")]
    public float findMateDistance = 30f;
    public Vector2 pregnancyHours = new Vector2(20f, 30f);
    public Vector2Int babies = new Vector2Int(1, 3);
    public float maleRestHours = 8f;
    public float femaleRestHours = 16f;
    [Tooltip("The Male / Female label shows on rabbits this close")]
    public float labelDistance = 22f;

    readonly List<RabbitLife> rabbits = new List<RabbitLife>();
    readonly List<TextMeshPro> labels = new List<TextMeshPro>();
    GameObject template;
    Vector3 adultScale = Vector3.one * 1.4f;
    Transform rabbitParent;
    Transform player;
    Material bloodMaterial;
    float tick, activeTick;
    bool started;

    static readonly Color MaleColor = new Color(0.45f, 0.78f, 1f);
    static readonly Color FemaleColor = new Color(1f, 0.55f, 0.82f);

    public int Count { get { int n = 0; foreach (var r in rabbits) if (r != null && r.Alive) n++; return n; } }
    public Vector3 PlayerPosition => player != null ? player.position : (Camera.main != null ? Camera.main.transform.position : Vector3.zero);

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (started) return;
        started = true;
        var movement = FindObjectOfType<PlayerMovement>();
        if (movement != null) player = movement.transform;

        var found = new List<Animal>();
        foreach (var a in FindObjectsOfType<Animal>()) if (a.animalName != null && a.animalName.Contains("abbit")) found.Add(a);
        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        if (found.Count > 0)
        {
            rabbitParent = found[0].transform.parent;
            adultScale = found[0].transform.localScale;
            // a hidden copy to make new rabbits from (babies, loaded games)
            template = Instantiate(found[0].gameObject, transform);
            template.name = "Rabbit";
            template.SetActive(false);
        }
        int i = 0;
        foreach (var a in found)
        {
            var info = a.GetComponent<RabbitInfo>();
            var sex = info != null ? info.sex : (i % 2 == 0 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female);
            // most of the scene's rabbits are grown up, a few are still young
            float age = Mathf.Repeat(a.transform.position.x * 3.1f + a.transform.position.z * 1.7f, 1f) < 0.15f ? 18f : AdultHours + 20f;
            var life = AddRabbit(a.gameObject, sex, age, a.transform.localScale);
            life.restHours = Random.Range(0f, 30f); // not all of them ready at the same moment
            i++;
        }
    }

    RabbitLife AddRabbit(GameObject go, RabbitInfo.Sex sex, float age, Vector3 scale)
    {
        var life = go.GetComponent<RabbitLife>();
        if (life == null) life = go.AddComponent<RabbitLife>();
        life.Setup(sex, age, scale);
        FixBloodParticles(go);
        rabbits.Add(life);
        return life;
    }

    // the blood splash used a material of another render pipeline (pink squares): it gets one that works here
    void FixBloodParticles(GameObject go)
    {
        foreach (var r in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            var m = r.sharedMaterial;
            if (m != null && m.shader != null && m.shader.isSupported && !m.shader.name.Contains("Universal")) continue;
            if (bloodMaterial == null)
            {
                var shader = Resources.Load<Shader>("Environment/SB_Particle");
                if (shader == null) return;
                bloodMaterial = new Material(shader) { name = "Blood (made in code)", hideFlags = HideFlags.DontSave, mainTexture = EnvTextures.Dot };
                bloodMaterial.SetColor("_Tint", new Color(0.55f, 0.02f, 0.02f, 1f));
                bloodMaterial.SetFloat("_Opacity", 1f);
            }
            r.sharedMaterial = bloodMaterial;
        }
    }

    RabbitLife Spawn(Vector3 position, float rotation, RabbitInfo.Sex sex, float age)
    {
        if (template == null) return null;
        var go = Instantiate(template, rabbitParent);
        go.name = "Rabbit";
        go.transform.position = position;
        go.transform.rotation = Quaternion.Euler(0f, rotation, 0f);
        go.SetActive(true);
        var info = go.GetComponent<RabbitInfo>();
        if (info != null) info.sex = sex;
        return AddRabbit(go, sex, age, adultScale);
    }

    void Update()
    {
        activeTick -= Time.deltaTime;
        if (activeTick <= 0f) { activeTick = 0.5f; UpdateActive(); }

        tick += Time.deltaTime;
        if (tick < 1f) return;
        float hours = DayNightSystem.GameHoursPerSecond * tick;
        tick = 0f;
        if (hours > 0f) Live(hours);
    }

    // far rabbits stand still and are not drawn (phone speed)
    void UpdateActive()
    {
        Vector3 p = PlayerPosition;
        for (int i = rabbits.Count - 1; i >= 0; i--)
        {
            var r = rabbits[i];
            if (r == null) { rabbits.RemoveAt(i); continue; } // looted
            Vector3 d = r.transform.position - p; d.y = 0f;
            float dist = d.magnitude;
            r.SetActive(dist < activeDistance);
            bool draw = dist < drawDistance;
            foreach (var renderer in r.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (renderer.enabled != draw) renderer.enabled = draw;
        }
    }

    // growing up, finding a partner, babies
    void Live(float hours)
    {
        int alive = Count;
        Vector3 player = PlayerPosition;
        foreach (var r in rabbits.ToArray())
        {
            if (r == null || !r.Alive) { if (r != null) r.mate = null; continue; }
            bool wasAdult = r.IsAdult;
            r.ageHours += hours;
            if (r.restHours > 0f) r.restHours -= hours;
            r.ApplyGrowth();
            if (wasAdult != r.IsAdult) r.UpdateName();

            if (r.Expecting)
            {
                r.pregnantHours -= hours;
                if (r.pregnantHours <= 0f)
                {
                    r.pregnantHours = -1f;
                    r.restHours = femaleRestHours;
                    int count = Random.Range(babies.x, babies.y + 1);
                    for (int b = 0; b < count && alive < maxRabbits; b++)
                    {
                        Vector2 off = Random.insideUnitCircle * 1.5f;
                        Vector3 at = r.transform.position + new Vector3(off.x, 0f, off.y);
                        at.y = Forest.GroundY(at);
                        if (Spawn(at, Random.Range(0f, 360f), Random.value < 0.5f ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female, 0f) != null) alive++;
                    }
                }
            }
        }

        // a ready female and the nearest ready male find each other
        foreach (var female in rabbits)
        {
            if (female == null || female.sex != RabbitInfo.Sex.Female) continue;
            if (female.mate != null && (!female.mate.Alive || !female.mate.ReadyToMate || !female.ReadyToMate))
            {
                if (female.mate.mate == female) female.mate.mate = null;
                female.mate = null;
            }
            if (!female.ReadyToMate || alive >= maxRabbits) continue;
            if (female.mate == null)
            {
                RabbitLife best = null;
                float bestDistance = findMateDistance * findMateDistance;
                foreach (var male in rabbits)
                {
                    if (male == null || male.sex != RabbitInfo.Sex.Male || !male.ReadyToMate || male.mate != null) continue;
                    float d = (male.transform.position - female.transform.position).sqrMagnitude;
                    if (d < bestDistance) { bestDistance = d; best = male; }
                }
                if (best == null) continue;
                female.mate = best;
                best.mate = female;
            }
            // together (or both far from the player, where nobody sees them walk): babies on the way
            var partner = female.mate;
            Vector3 gap = partner.transform.position - female.transform.position; gap.y = 0f;
            bool unseen = !female.Active && !partner.Active;
            if (gap.sqrMagnitude < 1.6f * 1.6f || unseen)
            {
                female.pregnantHours = Random.Range(pregnancyHours.x, pregnancyHours.y);
                partner.restHours = maleRestHours;
                female.mate = null;
                partner.mate = null;
            }
        }
    }

    // ---------------- labels: Male / Female above the near rabbits ---------------- //

    void LateUpdate()
    {
        var cam = Camera.main;
        int used = 0;
        bool windowOpen = (InventorySystem.Instance != null && InventorySystem.Instance.isOpen)
            || (MenuManager.Instance != null && MenuManager.Instance.isMenuOpen);
        if (cam != null && !windowOpen)
        {
            Vector3 c = cam.transform.position;
            foreach (var r in rabbits)
            {
                if (r == null || !r.Alive) continue;
                Vector3 head = r.transform.position + Vector3.up * (0.9f + 1.0f * r.transform.localScale.y);
                Vector3 d = head - c;
                if (d.sqrMagnitude > labelDistance * labelDistance) continue;
                if (Vector3.Dot(d, cam.transform.forward) < 0f) continue; // behind the camera
                var label = Label(used++);
                label.transform.position = head;
                label.transform.rotation = Quaternion.LookRotation(d, Vector3.up);
                float scale = Mathf.Lerp(0.55f, 1.1f, Mathf.InverseLerp(3f, labelDistance, d.magnitude));
                label.transform.localScale = Vector3.one * scale;
                string text = r.LabelText;
                if (label.text != text) label.text = text;
                label.color = r.sex == RabbitInfo.Sex.Male ? MaleColor : FemaleColor;
            }
        }
        for (int i = used; i < labels.Count; i++) if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
    }

    TextMeshPro Label(int index)
    {
        while (labels.Count <= index)
        {
            var go = new GameObject("Rabbit label");
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<TextMeshPro>();
            if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = 3f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            text.rectTransform.sizeDelta = new Vector2(8f, 1.5f);
            text.outlineWidth = 0.25f;
            text.outlineColor = new Color32(0, 0, 0, 200);
            var r = text.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            labels.Add(text);
        }
        var label = labels[index];
        if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
        return label;
    }

    // ---------------- save / load ---------------- //

    public RabbitSaveData GetData()
    {
        var data = new RabbitSaveData { version = 1 };
        foreach (var r in rabbits)
        {
            if (r == null || !r.Alive) continue;
            data.rabbits.Add(new RabbitData
            {
                position = r.transform.position,
                rotation = r.transform.eulerAngles.y,
                sex = (int)r.sex,
                ageHours = r.ageHours,
                pregnantHours = r.pregnantHours,
                restHours = r.restHours,
            });
        }
        return data;
    }

    public void SetData(RabbitSaveData data)
    {
        if (!started) Start();
        if (data == null || data.version < 1) return; // an old save: the rabbits of the scene stay
        foreach (var r in rabbits) if (r != null) { r.gameObject.SetActive(false); Destroy(r.gameObject); }
        rabbits.Clear();
        if (data.rabbits == null) return;
        foreach (var d in data.rabbits)
        {
            var life = Spawn(d.position, d.rotation, d.sex == 1 ? RabbitInfo.Sex.Female : RabbitInfo.Sex.Male, d.ageHours);
            if (life == null) continue;
            life.pregnantHours = d.pregnantHours;
            life.restHours = d.restHours;
            life.UpdateName();
        }
        UpdateActive();
    }
}
