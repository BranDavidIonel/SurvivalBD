using UnityEngine;
using UnityEngine.Rendering;

// The look of the night shadow (NightShadow.cs): a tall, thin, black human figure (about 2.5 m) built in code
// from stretched capsules, with long arms, long thin fingers and small glowing white eyes. It is a real 3D body
// standing in the world (it turns, you can walk around it). Its see-through amount is uneven and keeps drifting,
// like dark smoke (shader Resources/Environment/SB_ShadowFigure.shader).
// It walks (legs swing, knees bend, arms sway), tilts its head now and then, and slowly raises its arms
// towards the player as the danger grows. NightShadow moves and turns it; this script only poses it.
public class ShadowFigure : MonoBehaviour
{
    public const float EyeHeight = 2.33f; // metres above its feet

    Material figureMat, eyeMat, smokeMat;
    Transform hips, spine, chest, head;
    readonly Transform[] shoulder = new Transform[2], elbow = new Transform[2], wrist = new Transform[2];
    readonly Transform[] hip = new Transform[2], knee = new Transform[2], ankle = new Transform[2];
    readonly Transform[] eyes = new Transform[4];
    readonly LineRenderer[] fingers = new LineRenderer[8];
    readonly Vector3[] fingerPoints = new Vector3[4];
    ParticleSystem smoke;
    float alpha;
    float reach;
    float headTilt, wantedTilt, nextTilt;
    float nextBlink, blinkUntil;

    public Transform Head => head;

    // overall solidity (0 = gone, 1 = solid black); NightShadow changes it all the time
    public float Alpha
    {
        get => alpha;
        set
        {
            alpha = Mathf.Clamp01(value);
            SetAlpha(figureMat, alpha);
            SetAlpha(smokeMat, alpha);
        }
    }

    public static ShadowFigure Build(Transform parent)
    {
        var go = new GameObject("Shadow");
        go.transform.SetParent(parent, false);
        var figure = go.AddComponent<ShadowFigure>();
        figure.Create();
        return figure;
    }

    public void ClearSmoke()
    {
        if (smoke != null) smoke.Clear();
    }

    void OnDestroy()
    {
        foreach (var m in new[] { figureMat, eyeMat, smokeMat })
            if (m != null) Destroy(m);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Pose, every frame

    // camPos: where the player's eyes are. danger: 0..1 (arms rise as it grows). step: 0 = standing, 1 = walking.
    // walkPhase: the walking cycle from NightShadow (one turn = two steps).
    public void Animate(float dt, Vector3 camPos, float danger, float step, float walkPhase, bool attacking)
    {
        float t = Time.time;
        float wantedReach = attacking ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, danger));
        reach = Mathf.MoveTowards(reach, wantedReach, dt * (attacking ? 4f : 1.2f));
        float r = reach;

        // legs: big slow steps; the foot swings forward while the knee bends high, then the leg pushes back straight
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float phase = walkPhase + (i == 0 ? 0f : Mathf.PI);
            float swing = Mathf.Sin(phase) * 34f * step;              // + = leg forward
            float bend = Mathf.Max(0f, Mathf.Cos(phase)) * 65f * step; // knee
            hip[i].localRotation = Quaternion.Euler(-swing, 0f, side * 1.5f);
            knee[i].localRotation = Quaternion.Euler(bend + 3f, 0f, 0f);
            ankle[i].localRotation = Quaternion.Euler(swing - bend * 0.8f - 3f, 0f, 0f); // keeps the foot about flat
        }

        // body: the weight moves over the standing leg (hips slide and the body rocks side to side, easy to see
        // even from the front), hips turn with the steps, the chest the other way; it leans forward to reach
        float rock = Mathf.Cos(walkPhase) * step; // + = standing on the right leg
        hips.localPosition = new Vector3(rock * 0.07f, 1.22f, 0f);
        hips.localRotation = Quaternion.Euler(0f, Mathf.Sin(walkPhase) * 9f * step, rock * 4f);
        float breath = Mathf.Sin(t * 1.4f);
        spine.localRotation = Quaternion.Euler(5f * step + 3f + 10f * r, 0f, -rock * 7f + Mathf.Sin(t * 0.6f) * 2f);
        chest.localRotation = Quaternion.Euler(2f + 6f * r + breath * 1.5f, -Mathf.Sin(walkPhase) * 12f * step, -rock * 3f);

        // arms: hang and swing opposite the legs, then rise slowly towards the player, hands opening and closing
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            float sway = -Mathf.Sin(walkPhase + (i == 0 ? 0f : Mathf.PI)) * 28f * step;
            float raised = 80f + 6f * Mathf.Sin(t * 1.7f + i * 2f);
            float forward = Mathf.Lerp(sway + 2f * Mathf.Sin(t * 0.9f + i), raised, r);
            shoulder[i].localRotation = Quaternion.Euler(-forward, -side * 12f * r, side * Mathf.Lerp(7f, 3f, r));
            float elbowBend = Mathf.Lerp(8f, 20f, r) + 5f * Mathf.Sin(t * 2.3f + i * 1.3f) * (0.3f + r);
            elbow[i].localRotation = Quaternion.Euler(-elbowBend, 0f, 0f);
            wrist[i].localRotation = Quaternion.Euler(-12f * r + 8f * Mathf.Sin(t * 3f + i) * r, 0f, 0f);
        }
        UpdateFingers(r, t);

        // the head looks at the player, tilted a little to one side now and then (unsettling)
        if (t > nextTilt)
        {
            nextTilt = t + Random.Range(2.5f, 6f);
            wantedTilt = Random.value < 0.35f ? 0f : Random.Range(-22f, 22f);
        }
        headTilt = Mathf.MoveTowards(headTilt, wantedTilt, dt * 30f);
        Vector3 toCam = camPos - head.position;
        if (toCam.sqrMagnitude > 0.01f)
        {
            var look = Quaternion.LookRotation(toCam) * Quaternion.Euler(0f, 0f, headTilt);
            // never more than 70 degrees from where the body faces
            var bodyFacing = Quaternion.LookRotation(transform.forward);
            look = Quaternion.RotateTowards(bodyFacing, look, 70f);
            head.rotation = Quaternion.Slerp(head.rotation, look, Mathf.Clamp01(dt * 4f));
        }

        // eyes: always face the camera (they are small glowing spots), blink now and then
        foreach (var eye in eyes) eye.rotation = Quaternion.LookRotation(camPos - eye.position);
        if (t > nextBlink)
        {
            blinkUntil = t + 0.12f;
            nextBlink = t + Random.Range(2.5f, 7f);
        }
        float glow = t < blinkUntil ? 0f : 0.85f + 0.15f * Mathf.Sin(t * 7f);
        SetAlpha(eyeMat, Mathf.Clamp01(alpha * 1.4f) * glow);

        var emission = smoke.emission;
        emission.rateOverTime = 10f * alpha;
    }

    void UpdateFingers(float r, float t)
    {
        float width = transform.lossyScale.x; // lines don't grow with the body by themselves
        foreach (var finger in fingers) finger.widthMultiplier = width;
        float grab = 0.5f + 0.5f * Mathf.Sin(t * (2.5f + 4f * r));
        for (int h = 0; h < 2; h++)
        {
            var w = wrist[h];
            for (int k = 0; k < 4; k++)
            {
                float x = (k - 1.5f) * 0.022f;
                float spread = (k - 1.5f) * (0.015f + 0.035f * r);
                float curl = 0.025f + 0.07f * grab * r; // curls down into a claw when the arm is raised
                float length = k == 0 || k == 3 ? 0.17f : 0.21f;
                fingerPoints[0] = w.TransformPoint(new Vector3(x, -0.14f, 0f));
                fingerPoints[1] = w.TransformPoint(new Vector3(x + spread * 0.5f, -0.14f - length * 0.4f, -curl * 0.4f));
                fingerPoints[2] = w.TransformPoint(new Vector3(x + spread * 0.8f, -0.14f - length * 0.75f, -curl));
                fingerPoints[3] = w.TransformPoint(new Vector3(x + spread, -0.14f - length, -curl * 1.8f));
                fingers[h * 4 + k].SetPositions(fingerPoints);
            }
        }
    }

    static void SetAlpha(Material m, float a)
    {
        if (m == null) return;
        var c = m.color;
        c.a = a;
        m.color = c;
    }

    // ---------------------------------------------------------------------------------------------------------
    // Building it

    void Create()
    {
        var figureShader = Resources.Load<Shader>("Environment/SB_ShadowFigure");
        var glowShader = Resources.Load<Shader>("Environment/SB_Shadow");
        if (figureShader == null) figureShader = Shader.Find("Unlit/Color");
        if (glowShader == null) glowShader = Shader.Find("Sprites/Default");

        figureMat = new Material(figureShader) { name = "SB_ShadowFigure", hideFlags = HideFlags.DontSave, color = Color.black };
        eyeMat = new Material(glowShader) { name = "SB_ShadowEyes", hideFlags = HideFlags.DontSave };
        eyeMat.mainTexture = EnvTextures.Dot;
        eyeMat.color = new Color(0.94f, 0.97f, 1f, 1f); // glowing white, almost human
        eyeMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        eyeMat.SetFloat("_DstBlend", (float)BlendMode.One);
        eyeMat.renderQueue = 3050;
        smokeMat = new Material(glowShader) { name = "SB_ShadowSmoke", hideFlags = HideFlags.DontSave };
        smokeMat.mainTexture = EnvTextures.Smoke;
        smokeMat.color = Color.white;
        smokeMat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        smokeMat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);

        var capsule = PrimitiveMesh(PrimitiveType.Capsule);
        var sphere = PrimitiveMesh(PrimitiveType.Sphere);

        // trunk (local: feet at 0, +Z = where it faces)
        hips = Joint("Hips", transform, new Vector3(0f, 1.22f, 0f));
        Part(hips, capsule, new Vector3(0f, 0.02f, 0f), new Vector3(0.34f, 0.15f, 0.2f));
        spine = Joint("Spine", hips, new Vector3(0f, 0.05f, 0f));
        Part(spine, capsule, new Vector3(0f, 0.3f, 0f), new Vector3(0.28f, 0.34f, 0.17f));
        chest = Joint("Chest", spine, new Vector3(0f, 0.5f, 0f));
        Part(chest, capsule, new Vector3(0f, 0.04f, 0f), new Vector3(0.44f, 0.23f, 0.22f));
        var neck = Joint("Neck", chest, new Vector3(0f, 0.22f, 0f));
        Part(neck, capsule, new Vector3(0f, 0.08f, 0f), new Vector3(0.1f, 0.12f, 0.1f));
        head = Joint("Head", neck, new Vector3(0f, 0.17f, 0f));
        Part(head, sphere, new Vector3(0f, 0.13f, 0.01f), new Vector3(0.22f, 0.29f, 0.25f));

        // small glowing white eyes, a bright core and a soft glow each
        var core = QuadMesh(0.075f, 0.042f, new Color(1f, 1f, 1f, 1f));
        var halo = QuadMesh(0.26f, 0.15f, new Color(1f, 1f, 1f, 0.35f));
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var spot = new Vector3(side * 0.055f, 0.15f, 0.14f);
            eyes[i * 2] = Glow(i == 0 ? "Eye L" : "Eye R", core, spot);
            eyes[i * 2 + 1] = Glow(i == 0 ? "Eye L glow" : "Eye R glow", halo, spot + new Vector3(0f, 0f, -0.005f));
        }

        // long thin arms with long fingers
        var fingerWidth = new AnimationCurve(new Keyframe(0f, 0.028f), new Keyframe(1f, 0.007f));
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            string s = i == 0 ? " L" : " R";
            shoulder[i] = Joint("Shoulder" + s, chest, new Vector3(side * 0.25f, 0.13f, 0f));
            Part(shoulder[i], capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.1f, 0.33f, 0.1f));
            elbow[i] = Joint("Elbow" + s, shoulder[i], new Vector3(0f, -0.6f, 0f));
            Part(elbow[i], capsule, new Vector3(0f, -0.29f, 0f), new Vector3(0.075f, 0.31f, 0.075f));
            wrist[i] = Joint("Wrist" + s, elbow[i], new Vector3(0f, -0.58f, 0f));
            Part(wrist[i], capsule, new Vector3(0f, -0.08f, 0f), new Vector3(0.08f, 0.1f, 0.035f));
            for (int k = 0; k < 4; k++) fingers[i * 4 + k] = NewFinger("Finger" + s, fingerWidth);
        }

        // long legs
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            string s = i == 0 ? " L" : " R";
            hip[i] = Joint("Hip" + s, hips, new Vector3(side * 0.11f, 0f, 0f));
            Part(hip[i], capsule, new Vector3(0f, -0.3f, 0f), new Vector3(0.15f, 0.34f, 0.15f));
            knee[i] = Joint("Knee" + s, hip[i], new Vector3(0f, -0.6f, 0f));
            Part(knee[i], capsule, new Vector3(0f, -0.29f, 0f), new Vector3(0.11f, 0.32f, 0.11f));
            ankle[i] = Joint("Ankle" + s, knee[i], new Vector3(0f, -0.58f, 0f));
            var foot = Part(ankle[i], capsule, new Vector3(0f, -0.01f, 0.07f), new Vector3(0.09f, 0.13f, 0.055f));
            foot.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying along the ground
        }

        smoke = BuildSmoke();
        Alpha = 0f;
    }

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var temp = GameObject.CreatePrimitive(type);
        var mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        DestroyImmediate(temp);
        return mesh;
    }

    static Transform Joint(string name, Transform parent, Vector3 localPosition)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = localPosition;
        return t;
    }

    Transform Part(Transform joint, Mesh mesh, Vector3 localPosition, Vector3 size)
    {
        var go = new GameObject(joint.name + " shape");
        go.transform.SetParent(joint, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = size;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = figureMat;
        NoShadows(r);
        return go.transform;
    }

    Transform Glow(string name, Mesh mesh, Vector3 localPosition)
    {
        var go = new GameObject(name);
        go.transform.SetParent(head, false);
        go.transform.localPosition = localPosition;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = eyeMat;
        NoShadows(r);
        return go.transform;
    }

    LineRenderer NewFinger(string name, AnimationCurve width)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = fingerPoints.Length;
        line.widthCurve = width;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.alignment = LineAlignment.View;
        line.generateLightingData = true; // normals for the shader's soft edges
        line.startColor = line.endColor = Color.black;
        line.sharedMaterial = figureMat;
        NoShadows(line);
        return line;
    }

    static void NoShadows(Renderer r)
    {
        r.shadowCastingMode = ShadowCastingMode.Off;
        r.receiveShadows = false;
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    static Mesh QuadMesh(float width, float height, Color color)
    {
        float w = width * 0.5f, h = height * 0.5f;
        var mesh = new Mesh { name = "Shadow eye" };
        mesh.vertices = new[] { new Vector3(-w, -h, 0f), new Vector3(w, -h, 0f), new Vector3(-w, h, 0f), new Vector3(w, h, 0f) };
        mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
        mesh.colors = new[] { color, color, color, color };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.RecalculateBounds();
        return mesh;
    }

    // thin black smoke drifting off the body (left behind as it moves)
    ParticleSystem BuildSmoke()
    {
        var go = new GameObject("Smoke");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 1.25f, -0.05f);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.45f, 1f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new Color(0f, 0f, 0f, 0.45f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy; // grows with the figure
        main.gravityModifier = -0.02f;
        main.maxParticles = 40;

        var emission = ps.emission;
        emission.rateOverTime = 10f;
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.6f, 2.2f, 0.3f);

        var colour = ps.colorOverLifetime;
        colour.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.25f), new GradientAlphaKey(0f, 1f) });
        colour.color = gradient;
        var size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 1.5f));
        var rotation = ps.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.8f, 0.8f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = smokeMat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        ps.Play();
        return ps;
    }
}
