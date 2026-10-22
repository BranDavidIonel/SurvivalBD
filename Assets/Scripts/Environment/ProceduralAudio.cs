using UnityEngine;

// Sounds made in code (no sound files needed): the crackling of a fire, rain and wind.
// Each one is a short loop made the first time it is needed (a few milliseconds of work).
public static class ProceduralAudio
{
    const int Rate = 22050;
    static AudioClip fire, rain, wind;

    public static AudioClip FireCrackle => fire != null ? fire : (fire = MakeFire());
    public static AudioClip Rain => rain != null ? rain : (rain = MakeRain());
    public static AudioClip Wind => wind != null ? wind : (wind = MakeWind());

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
