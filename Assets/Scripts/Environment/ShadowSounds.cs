using System;
using UnityEngine;

// The scary music and sounds of the night shadow (NightShadow.cs), all made in code (no licence needed):
//  - Drone: a low, uneasy hum that loops (8 s)
//  - Strings: a trembling, out-of-tune high chord that gets louder as the shadow comes closer (8 s loop)
//  - Heartbeat: one "lub-dub", played faster and faster
//  - Stinger: a deep hit and a screech when the shadow appears
//  - Scream: when it grabs the player
//  - Whispers: breathy murmurs coming from the shadow
// The low sounds are saturated a bit so a phone speaker (which cannot play deep bass) still hears them.
public static class ShadowSounds
{
    const int Rate = 22050;
    const double TwoPi = Math.PI * 2.0;

    static AudioClip drone, strings, heartbeat, stinger, scream, step;
    static AudioClip[] whispers;

    public static AudioClip Drone => drone != null ? drone : (drone = MakeDrone());
    public static AudioClip Strings => strings != null ? strings : (strings = MakeStrings());
    public static AudioClip Heartbeat => heartbeat != null ? heartbeat : (heartbeat = MakeHeartbeat());
    public static AudioClip Stinger => stinger != null ? stinger : (stinger = MakeStinger());
    public static AudioClip Scream => scream != null ? scream : (scream = MakeScream());
    public static AudioClip Step => step != null ? step : (step = MakeStep());
    public static AudioClip[] Whispers => whispers != null ? whispers : (whispers = new[] { MakeWhisper(11, 1.9f), MakeWhisper(12, 1.5f), MakeWhisper(13, 2.2f) });

    // ---------------------------------------------------------------------------------------------------------
    // All loop frequencies are multiples of 1/8 Hz, so they line up exactly after 8 seconds (no click).

    static AudioClip MakeDrone()
    {
        const double loop = 8.0, fade = 0.5;
        int n = (int)((loop + fade) * Rate);
        var s = new float[n];
        var rnd = new System.Random(71);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            float swell = 0.65f + 0.35f * Sin(0.25, t);
            float low = 0.5f * Sin(49.0, t) + 0.4f * Sin(51.875, t) + 0.2f * Sin(98.0, t);
            // a dissonant pair a bit higher up: this is what a phone speaker plays
            float mid = (0.22f * Saw(146.75 * t) + 0.18f * Saw(155.625 * t)) * (0.55f + 0.45f * Sin(0.125, t));
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.03f;
            lp2 += (lp - lp2) * 0.05f; // deep rumble
            float v = (low + mid) * swell + lp2 * 4f;
            s[i] = (float)Math.Tanh(v * 1.5f);
        }
        s = Loop(s, (int)(fade * Rate));
        Normalize(s, 0.8f);
        return ToClip("Shadow drone (made in code)", s);
    }

    static AudioClip MakeStrings()
    {
        const double loop = 8.0, fade = 0.3;
        // two close notes rubbing against each other, plus a tritone: the classic horror chord
        double[] freq = { 220.0, 233.125, 311.125, 440.0, 466.25 };
        float[] amp = { 0.3f, 0.3f, 0.24f, 0.12f, 0.12f };
        int n = (int)((loop + fade) * Rate);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            float v = 0f;
            for (int k = 0; k < freq.Length; k++)
            {
                double f = freq[k];
                // vibrato (5.5 Hz) and a fast bowing tremolo (7 Hz), different for every voice
                double cycles = f * t - f * 0.004 / (TwoPi * 5.5) * Math.Cos(TwoPi * 5.5 * t + k * 1.3);
                float tremolo = 0.6f + 0.4f * (float)Math.Sin(TwoPi * 7.0 * t + k * 2.1);
                v += amp[k] * Saw(cycles) * tremolo;
            }
            s[i] = v;
        }
        s = Loop(s, (int)(fade * Rate));
        Normalize(s, 0.7f);
        return ToClip("Shadow strings (made in code)", s);
    }

    static AudioClip MakeHeartbeat()
    {
        int n = (int)(0.7f * Rate);
        var s = new float[n];
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            float v = Thump(t, 0.0, 58.0, 1f) + Thump(t, 0.24, 66.0, 0.75f);
            s[i] = (float)Math.Tanh(v * 2.5f);
        }
        Normalize(s, 0.95f);
        return ToClip("Shadow heartbeat (made in code)", s);
    }

    // a soft drum hit whose pitch falls a little
    static float Thump(double t, double start, double f, float amp)
    {
        if (t < start) return 0f;
        double x = t - start;
        const double tau = 0.04;
        double cycles = f * (0.6 * x + 0.4 * tau * (1.0 - Math.Exp(-x / tau)));
        double env = (1.0 - Math.Exp(-x / 0.004)) * Math.Exp(-x / 0.07);
        return amp * (float)(env * Math.Sin(TwoPi * cycles));
    }

    // a heavy, soft footstep: a low thud and a short crunch of grass
    static AudioClip MakeStep()
    {
        int n = (int)(0.3f * Rate);
        var s = new float[n];
        var rnd = new System.Random(21);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.12f;
            float crunch = lp * 1.5f * (float)(Math.Exp(-t / 0.035) * (1.0 - Math.Exp(-t / 0.003)));
            s[i] = (float)Math.Tanh((Thump(t, 0.0, 75.0, 1f) + crunch) * 2f);
        }
        Normalize(s, 0.8f);
        return ToClip("Shadow step (made in code)", s);
    }

    static AudioClip MakeStinger()
    {
        int n = (int)(2.6f * Rate);
        var s = new float[n];
        var rnd = new System.Random(5);
        float lp = 0f;
        double boomPhase = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            // deep hit: 90 Hz falling to 32 Hz
            double f = 32.0 + 58.0 * Math.Exp(-t / 0.25);
            boomPhase += f / Rate;
            float boom = (float)(Math.Sin(TwoPi * boomPhase) * Math.Exp(-t / 0.5));
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.08f;
            float noise = lp * 3f * (float)Math.Exp(-t / 0.12);
            // screech: high, out of tune, trembling
            double env = Math.Min(1.0, t / 0.15) * Math.Exp(-Math.Max(0.0, t - 0.15) / 0.8);
            float tremolo = 0.6f + 0.4f * (float)Math.Sin(TwoPi * 13.0 * t);
            float screech = (0.4f * Saw(1244.5 * t) + 0.35f * Saw(1318.5 * t) + 0.25f * Saw(1864.7 * t)) * tremolo * (float)env;
            s[i] = (float)Math.Tanh((boom * 0.9f + noise * 0.5f) * 1.8f) + screech * 0.35f;
        }
        Normalize(s, 0.9f);
        return ToClip("Shadow stinger (made in code)", s);
    }

    static AudioClip MakeScream()
    {
        int n = (int)(1.5f * Rate);
        var s = new float[n];
        var rnd = new System.Random(9);
        float lp = 0f;
        double phase = 0.0;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            double f = 260.0 + 950.0 * Math.Exp(-t * 0.9);
            phase += f / Rate;
            float saws = Saw(phase) * 0.4f + Saw(phase * 1.06) * 0.35f + Saw(phase * 1.5) * 0.25f;
            float tremolo = 0.55f + 0.45f * (float)Math.Sin(TwoPi * 22.0 * t);
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.3f;
            float hiss = white - lp; // only the high part of the noise
            double env = (1.0 - Math.Exp(-t / 0.03)) * Math.Exp(-t / 0.55);
            s[i] = (float)(Math.Tanh(2.0 * (saws * tremolo * 0.5f + hiss * 0.35f)) * env);
        }
        Normalize(s, 0.95f);
        return ToClip("Shadow scream (made in code)", s);
    }

    static AudioClip MakeWhisper(int seed, float seconds)
    {
        int n = (int)(seconds * Rate);
        var s = new float[n];
        var rnd = new System.Random(seed);
        float fast = 0f, slow = 0f;
        double a = rnd.NextDouble() * 6.0, b = rnd.NextDouble() * 6.0;
        double rate1 = 2.8 + rnd.NextDouble(), rate2 = 1.4 + rnd.NextDouble() * 0.6;
        for (int i = 0; i < n; i++)
        {
            double t = (double)i / Rate;
            float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
            fast += (white - fast) * 0.35f;
            slow += (white - slow) * 0.06f;
            float voice = fast - slow;  // the middle of the noise: like breath through a mouth
            float hiss = white - fast;  // "sss"
            // syllables: the breath comes and goes like words
            float syllables = Mathf.Pow(Mathf.Max(0f, (float)Math.Sin(TwoPi * rate1 * t + a)), 2f)
                * (0.5f + 0.5f * (float)Math.Sin(TwoPi * rate2 * t + b));
            float overall = Mathf.Pow((float)Math.Sin(Math.PI * t / seconds), 1.5f);
            s[i] = (voice * syllables * 1.6f + hiss * 0.25f * syllables) * overall;
        }
        Normalize(s, 0.6f);
        return ToClip("Shadow whisper (made in code)", s);
    }

    // ---------------------------------------------------------------------------------------------------------

    static float Sin(double f, double t) => (float)Math.Sin(TwoPi * f * t);

    // a soft sawtooth (6 harmonics): sounds like bowed strings, without the harsh top
    static float[] sawTable;
    static float Saw(double cycles)
    {
        if (sawTable == null)
        {
            sawTable = new float[1024];
            for (int i = 0; i < sawTable.Length; i++)
            {
                double p = (double)i / sawTable.Length;
                double v = 0.0;
                for (int h = 1; h <= 6; h++) v += Math.Sin(TwoPi * h * p) / h;
                sawTable[i] = (float)(v * 0.55);
            }
        }
        double frac = cycles - Math.Floor(cycles);
        return sawTable[(int)(frac * sawTable.Length) & (sawTable.Length - 1)];
    }

    static AudioClip ToClip(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, Rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // the end of the sound is blended into its start, so the loop has no click
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
}
