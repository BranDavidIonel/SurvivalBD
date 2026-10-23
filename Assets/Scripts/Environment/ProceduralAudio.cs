using UnityEngine;

// Sounds made in code (no sound files needed): the crackling of a fire, rain and wind, swimming, splashes,
// an owl and the sea on the beach.
// Each one is made the first time it is needed (a few milliseconds of work).
public static class ProceduralAudio
{
    const int Rate = 22050;
    static AudioClip fire, rain, wind, waves, splash;
    static AudioClip[] strokes, wades, owls;

    public static AudioClip FireCrackle => fire != null ? fire : (fire = MakeFire());
    public static AudioClip Rain => rain != null ? rain : (rain = MakeRain());
    public static AudioClip Wind => wind != null ? wind : (wind = MakeWind());
    public static AudioClip Waves => waves != null ? waves : (waves = MakeWaves());
    public static AudioClip Splash => splash != null ? splash : (splash = MakeWater("Splash (made in code)", 9001, 0.9f, 0.004f, 0.45f, 0.9f, 40, 1f));
    public static AudioClip[] SwimStrokes => strokes != null ? strokes : (strokes = new[]
    {
        MakeWater("Swim stroke 1 (made in code)", 11, 0.65f, 0.12f, 0.22f, 0.6f, 14, 0.8f),
        MakeWater("Swim stroke 2 (made in code)", 12, 0.7f, 0.14f, 0.2f, 0.55f, 16, 0.8f),
        MakeWater("Swim stroke 3 (made in code)", 13, 0.6f, 0.1f, 0.24f, 0.6f, 12, 0.8f),
    });
    // a step in shallow water
    public static AudioClip[] WadeSteps => wades != null ? wades : (wades = new[]
    {
        MakeWater("Wade 1 (made in code)", 21, 0.32f, 0.01f, 0.3f, 0.7f, 6, 0.7f),
        MakeWater("Wade 2 (made in code)", 22, 0.3f, 0.01f, 0.32f, 0.7f, 7, 0.7f),
        MakeWater("Wade 3 (made in code)", 23, 0.34f, 0.012f, 0.28f, 0.65f, 6, 0.7f),
    });
    public static AudioClip[] OwlHoots => owls != null ? owls : (owls = new[] { MakeOwl(31, 0), MakeOwl(32, 1) });
    // a tree coming down: the trunk cracking, the branches rushing through the air, the crash on the ground
    static AudioClip treeCrack, branchRush, treeImpact;
    public static AudioClip TreeCrack => treeCrack != null ? treeCrack : (treeCrack = MakeTreeCrack());
    public static AudioClip BranchRush => branchRush != null ? branchRush : (branchRush = MakeBranchRush());
    public static AudioClip TreeImpact => treeImpact != null ? treeImpact : (treeImpact = MakeTreeImpact());

    // One crack of wood: a sharp click and the short, hollow ring of the trunk.
    static void AddCrack(float[] s, System.Random random, int at, float loud, float tone)
    {
        int n = s.Length;
        float prev = 0f;
        int clickLen = (int)(0.012f * Rate);
        for (int j = 0; j < clickLen && at + j < n; j++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            s[at + j] += (white - prev) * loud * Mathf.Exp(-j / (0.0022f * Rate)); // bright, very short
            prev = white;
        }
        int ringLen = (int)(0.12f * Rate);
        float decay = (0.025f + (float)random.NextDouble() * 0.03f) * Rate;
        for (int j = 0; j < ringLen && at + j < n; j++)
        {
            float t = (float)j / Rate;
            float e = Mathf.Exp(-j / decay) * loud * 0.55f;
            s[at + j] += (Mathf.Sin(6.2832f * tone * t) + 0.35f * Mathf.Sin(6.2832f * tone * 2.7f * t)) * e;
        }
    }

    // The trunk giving way: a few cracks, faster and louder, then a long splintering tear; a low groan under it.
    static AudioClip MakeTreeCrack()
    {
        var random = new System.Random(501);
        float seconds = 1.6f;
        int n = (int)(seconds * Rate);
        var s = new float[n];
        float time = 0.02f, gap = 0.3f, loud = 0.35f;
        while (time < 1.15f)
        {
            AddCrack(s, random, (int)(time * Rate), loud * (0.7f + 0.3f * (float)random.NextDouble()), 160f + (float)random.NextDouble() * 260f);
            if (random.NextDouble() < 0.35) // little splinters right after
                for (int r = 0; r < 3; r++) AddCrack(s, random, (int)((time + 0.008f + r * 0.011f) * Rate), loud * 0.3f, 500f + (float)random.NextDouble() * 400f);
            time += gap * (0.6f + 0.8f * (float)random.NextDouble());
            gap = Mathf.Max(0.035f, gap * 0.72f);
            loud = Mathf.Min(1f, loud * 1.12f);
        }
        // the final tear: many fibres snapping at once
        for (int k = 0; k < 40; k++)
        {
            float at = 1.12f + (float)random.NextDouble() * 0.3f;
            AddCrack(s, random, (int)(at * Rate), 0.25f + (float)random.NextDouble() * 0.5f, 220f + (float)random.NextDouble() * 500f);
        }
        // a low groan of the bending trunk (not a squeak)
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.SmoothStep(0f, 1f, t / 1.1f) * Mathf.Clamp01((seconds - t) / 0.3f);
            phase += 6.2832f * (75f - 15f * t / seconds) / Rate;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.02f;
            s[i] += (Mathf.Sin(phase) * 0.12f + lp * 0.6f) * env;
        }
        Normalize(s, 0.9f);
        return ToClip("Tree crack (made in code)", s);
    }

    // Branches rushing through the air while the tree falls: a swelling, airy noise.
    static AudioClip MakeBranchRush()
    {
        var random = new System.Random(502);
        float seconds = 1.8f;
        int n = (int)(seconds * Rate);
        var s = new float[n];
        float lp = 0f, slow = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Pow(t, 2.2f) * Mathf.Clamp01((1f - t) / 0.04f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * (0.12f + 0.3f * t);
            slow += (lp - slow) * 0.03f;
            float flutter = 0.7f + 0.3f * Mathf.Sin(t * 90f + Mathf.Sin(t * 23f) * 3f);
            s[i] = (lp - slow) * env * flutter;
        }
        // twigs snapping as the crown goes through other branches
        for (int k = 0; k < 18; k++)
        {
            float at = 0.6f + 1.1f * (float)random.NextDouble();
            AddCrack(s, random, (int)(at * Rate), 0.15f + 0.25f * (float)random.NextDouble(), 600f + (float)random.NextDouble() * 700f);
        }
        Normalize(s, 0.75f);
        return ToClip("Branches rushing (made in code)", s);
    }

    // The trunk hits the ground: a deep thump you can feel, the crown crashing and breaking, then leaves settling.
    static AudioClip MakeTreeImpact()
    {
        var random = new System.Random(503);
        float seconds = 2.4f;
        int n = (int)(seconds * Rate);
        var s = new float[n];
        float phase = 0f, low = 0f, mid = 0f, midSlow = 0f, rumble = 0f, hiss = 0f, prevHiss = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float attack = Mathf.Clamp01(t / 0.006f);
            // the thump: a low tone falling in pitch
            float f = Mathf.Lerp(72f, 36f, Mathf.Clamp01(t / 0.5f));
            phase += 6.2832f * f / Rate;
            float thump = Mathf.Sin(phase) * Mathf.Exp(-t / 0.32f) * attack;
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.035f;                       // the body of the hit
            float body = low * Mathf.Exp(-t / 0.22f) * attack * 2.2f;
            mid += (white - mid) * 0.35f;                        // the crown crashing
            midSlow += (mid - midSlow) * 0.06f;
            float crash = (mid - midSlow) * Mathf.Exp(-t / 0.45f) * attack * 0.75f;
            rumble += (white - rumble) * 0.008f;                 // the ground shaking
            float shake = rumble * Mathf.Exp(-t / 0.7f) * 3f;
            hiss = white;                                        // leaves settling
            float leaves = (hiss - prevHiss) * 0.05f * Mathf.Exp(-Mathf.Max(0f, t - 0.3f) / 0.8f) * Mathf.Clamp01(t / 0.25f)
                * (0.6f + 0.4f * Mathf.Sin(t * 17f + Mathf.Sin(t * 5f)));
            prevHiss = hiss;
            s[i] = thump * 1.1f + body + crash + shake + leaves;
        }
        // branches breaking
        for (int k = 0; k < 34; k++)
        {
            float at = 0.01f + Mathf.Pow((float)random.NextDouble(), 1.8f) * 0.8f;
            AddCrack(s, random, (int)(at * Rate), 0.2f + 0.5f * (float)random.NextDouble(), 200f + (float)random.NextDouble() * 600f);
        }
        Normalize(s, 0.95f);
        return ToClip("Tree impact (made in code)", s);
    }

    // a tool swung through the air
    static AudioClip[] swooshes;
    public static AudioClip[] Swooshes => swooshes != null ? swooshes : (swooshes = new[]
    {
        MakeSwoosh("Swoosh 1 (made in code)", 41, 0.3f, 1f),
        MakeSwoosh("Swoosh 2 (made in code)", 42, 0.26f, 1.15f),
        MakeSwoosh("Swoosh 3 (made in code)", 43, 0.34f, 0.88f),
    });

    // Air rushing past a blade: noise through a band that rises and falls (louder and brighter in the middle
    // of the swing), with a soft low "whoom" under it.
    static AudioClip MakeSwoosh(string name, int seed, float seconds, float pitch)
    {
        var random = new System.Random(seed);
        int n = (int)(seconds * Rate);
        var s = new float[n];
        float lp1 = 0f, lp2 = 0f, low = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f)), 1.8f);
            float sweep = Mathf.Sin(Mathf.PI * t);
            float a = Mathf.Clamp((0.05f + 0.35f * sweep) * pitch, 0.02f, 0.9f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp1 += (white - lp1) * a;
            lp2 += (lp1 - lp2) * a * 0.35f;
            low += (white - low) * 0.02f;
            s[i] = ((lp1 - lp2) * 0.9f + low * 0.6f) * env;
        }
        Normalize(s, 0.85f);
        return ToClip(name, s);
    }

    static AudioClip ToClip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // The end of the sound is blended into its start, so the loop has no click.
    static float[] Loop(float[] raw, int fade)
    {
        int length = raw.Length - fade;
        var result = new float[length];
        for (int i = 0; i < length; i++) result[i] = raw[i];
        for (int i = 0; i < fade; i++)
        {
            float t = (float)i / fade;
            result[i] = raw[i] * t + raw[length + i] * (1f - t);
        }
        return result;
    }

    static void Normalize(float[] s, float peak)
    {
        float max = 0.0001f;
        foreach (float v in s) max = Mathf.Max(max, Mathf.Abs(v));
        float k = peak / max;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
    }

    static AudioClip MakeFire()
    {
        var random = new System.Random(1234);
        float seconds = 5f;
        int fade = Rate / 10;
        int n = (int)(seconds * Rate) + fade;
        var s = new float[n];

        // the soft roar of the flames: low, slowly changing noise
        float low = 0f, slow = 0f, amp = 0.6f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            low += (white - low) * 0.035f;
            if (i % 800 == 0) amp = 0.45f + (float)random.NextDouble() * 0.55f;
            slow += (amp - slow) * 0.0008f;
            s[i] = low * 0.55f * slow;
        }

        // crackles and pops: short bursts of noise that die away fast
        int pops = (int)(seconds * 11f);
        for (int p = 0; p < pops; p++)
        {
            int start = random.Next(0, n - Rate / 10);
            float loud = 0.15f + (float)Mathf.Pow((float)random.NextDouble(), 2.2f) * 0.85f;
            float decay = 0.0015f + (float)random.NextDouble() * 0.006f; // seconds
            int len = (int)(decay * 6f * Rate);
            float prev = 0f;
            int repeats = random.NextDouble() < 0.25 ? 2 + random.Next(0, 3) : 1; // sometimes a little rattle
            for (int r = 0; r < repeats; r++)
            {
                int at = start + r * random.Next(Rate / 200, Rate / 40);
                for (int k = 0; k < len && at + k < n; k++)
                {
                    float white = (float)(random.NextDouble() * 2.0 - 1.0);
                    float high = white - prev; // brighter crack
                    prev = white;
                    s[at + k] += high * loud * Mathf.Exp(-k / (decay * Rate)) * (r == 0 ? 1f : 0.5f);
                }
            }
        }
        Normalize(s, 0.9f);
        return ToClip("Fire crackle (made in code)", Loop(s, fade));
    }

    static AudioClip MakeRain()
    {
        var random = new System.Random(777);
        float seconds = 4f;
        int fade = Rate / 5;
        int n = (int)(seconds * Rate) + fade;
        var s = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.45f;    // takes off the hiss
            lp2 += (lp - lp2) * 0.02f;     // the low part, taken away below
            s[i] = (lp - lp2) * 0.5f;
        }
        // many small drops
        int drops = (int)(seconds * 140f);
        for (int d = 0; d < drops; d++)
        {
            int at = random.Next(0, n - 200);
            float loud = 0.05f + (float)random.NextDouble() * 0.25f;
            float prev = 0f;
            for (int k = 0; k < 120 && at + k < n; k++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                s[at + k] += (white - prev) * loud * Mathf.Exp(-k / 18f);
                prev = white;
            }
        }
        Normalize(s, 0.8f);
        return ToClip("Rain (made in code)", Loop(s, fade));
    }

    // Water: a swish of filtered noise that gets darker as it dies away, with small bubbles (short rising tones).
    //  attack: seconds to full loudness; brightness: how much hiss at the start (0..1); darken: how much darker
    //  it gets by the end (0..1); bubbles: how many; peak: final loudness
    static AudioClip MakeWater(string name, int seed, float seconds, float attack, float brightness, float darken, int bubbles, float peak)
    {
        var random = new System.Random(seed);
        int n = (int)(seconds * Rate);
        var s = new float[n];
        float lp = 0f, slow = 0f;
        float sloshRate = 5f + (float)random.NextDouble() * 4f;
        float sloshPhase = (float)random.NextDouble() * 6.28f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            float k = t / seconds;
            float env = t < attack ? Mathf.SmoothStep(0f, 1f, t / Mathf.Max(0.001f, attack))
                                   : Mathf.Pow(1f - (t - attack) / Mathf.Max(0.001f, seconds - attack), 2.2f);
            float cutoff = Mathf.Max(0.02f, Mathf.Lerp(brightness, brightness * (1f - darken), k));
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * cutoff;
            slow += (lp - slow) * 0.012f;          // taken away below: no rumble
            float slosh = 0.65f + 0.35f * Mathf.Sin(6.2832f * sloshRate * t + sloshPhase);
            s[i] = (lp - slow) * env * slosh;
        }
        // bubbles, mostly early in the sound
        for (int b = 0; b < bubbles; b++)
        {
            float at = Mathf.Pow((float)random.NextDouble(), 1.6f) * seconds * 0.85f;
            float f0 = 450f + (float)random.NextDouble() * 1200f;
            float length = 0.015f + (float)random.NextDouble() * 0.035f;
            float loud = 0.12f + (float)random.NextDouble() * 0.35f;
            int start = (int)(at * Rate);
            int len = (int)(length * Rate);
            float phase = 0f;
            for (int j = 0; j < len && start + j < n; j++)
            {
                float u = (float)j / len;
                float f = f0 * (1f + 1.4f * u);
                phase += 6.2832f * f / Rate;
                s[start + j] += Mathf.Sin(phase) * loud * Mathf.Exp(-u * 3.5f) * Mathf.Clamp01(u * 20f);
            }
        }
        Normalize(s, peak);
        return ToClip(name, s);
    }

    // An owl far away: soft low "hoo" notes (pattern 0: hoo ... hoo hoo hooo, pattern 1: hoo-hoo), with an echo.
    static AudioClip MakeOwl(int seed, int pattern)
    {
        var random = new System.Random(seed);
        float[,] notes = pattern == 0
            ? new float[,] { { 0f, 0.42f, 410f }, { 0.85f, 0.17f, 400f }, { 1.1f, 0.17f, 400f }, { 1.38f, 0.62f, 385f } }
            : new float[,] { { 0f, 0.5f, 365f }, { 0.72f, 0.55f, 345f } };
        int count = notes.GetLength(0);
        float total = notes[count - 1, 0] + notes[count - 1, 1] + 0.9f;
        int n = (int)(total * Rate);
        var s = new float[n];
        for (int k = 0; k < count; k++)
        {
            float start = notes[k, 0], length = notes[k, 1], f0 = notes[k, 2];
            int i0 = (int)(start * Rate);
            int len = (int)(length * Rate);
            float phase = 0f, breath = 0f;
            for (int j = 0; j < len && i0 + j < n; j++)
            {
                float t = (float)j / Rate;
                float u = (float)j / len;
                float env = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.07f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((length - t) / 0.16f));
                float f = f0 * (1f - 0.06f * u) * (1f + 0.012f * Mathf.Sin(6.2832f * 5.5f * t));
                phase += 6.2832f * f / Rate;
                float tone = Mathf.Sin(phase) + 0.22f * Mathf.Sin(2f * phase) + 0.06f * Mathf.Sin(3f * phase);
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                breath += (white - breath) * 0.08f;
                s[i0 + j] += (tone * 0.85f + breath * 0.5f) * env;
            }
        }
        // a little echo of the forest
        int delay = (int)(0.075f * Rate);
        float lp = 0f;
        for (int i = delay; i < n; i++)
        {
            lp += (s[i - delay] - lp) * 0.25f;
            s[i] += lp * 0.32f;
        }
        Normalize(s, 0.7f);
        return ToClip("Owl " + (pattern + 1) + " (made in code)", s);
    }

    // The sea on the beach: slow swells of low noise, a bit of hiss when a wave breaks. 10 s loop.
    static AudioClip MakeWaves()
    {
        var random = new System.Random(2468);
        float seconds = 10f;
        int fade = Rate;
        int n = (int)(seconds * Rate) + fade;
        var s = new float[n];
        float lp = 0f, slow = 0f, hissLp = 0f, prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / Rate;
            // two waves in 10 s, not quite the same
            float swell = 0.5f + 0.5f * Mathf.Sin(6.2832f * t / 5f - 1.5f) * (0.85f + 0.15f * Mathf.Sin(6.2832f * t / 10f));
            float crest = Mathf.Pow(Mathf.Clamp01(swell), 3f);
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * (0.03f + 0.1f * swell);
            slow += (lp - slow) * 0.004f;
            hissLp += (white - hissLp) * 0.5f;
            float hiss = hissLp - prev;
            prev = hissLp;
            s[i] = (lp - slow) * (0.25f + 0.75f * swell * swell) + hiss * crest * 0.12f;
        }
        Normalize(s, 0.8f);
        return ToClip("Waves (made in code)", Loop(s, fade));
    }

    static AudioClip MakeWind()
    {
        var random = new System.Random(4321);
        float seconds = 8f;
        int fade = Rate / 2;
        int n = (int)(seconds * Rate) + fade;
        var s = new float[n];
        float lp = 0f, lp2 = 0f, gust = 0.5f, gustTarget = 0.5f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(random.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.02f;
            lp2 += (lp - lp2) * 0.08f;
            if (i % (Rate / 2) == 0) gustTarget = 0.3f + (float)random.NextDouble() * 0.7f;
            gust += (gustTarget - gust) * 0.00012f;
            s[i] = lp2 * gust;
        }
        Normalize(s, 0.8f);
        return ToClip("Wind (made in code)", Loop(s, fade));
    }
}
