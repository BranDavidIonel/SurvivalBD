#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Collections;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

// TEMPORARY (Claude): records the presentation video of the game (Temp/<Folder>/SurvivalBD.mp4, 3840x2160, 30 fps,
// with sound) and three big pictures, playing the game by itself shot after shot: cinematic cameras, and gameplay with
// the HUD. Time.captureFramerate = 30: every recorded frame is 1/30 s of game time however slow the recording is, so
// all the waits here count frames. The game is in English while recording (the player's language is put back after).
// The sound is mixed here frame by frame in game time (the real audio would run ahead of the slow recording): every
// AudioSource the game plays is followed (its clip, volume, distance), plus the short sounds the game plays with
// PlayOneShot (axe, steps, building knocks), which are added by the shots themselves.
// The Game view must be 3840x2160 (the driver's job name ends with "4K").
public class __VideoDirector : MonoBehaviour
{
    public static string Folder = "Video";
    string Dir => Application.dataPath + "/../Temp/" + Folder + "/";
    const int W = 3840, H = 2160, FPS = 30;
    const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    StreamWriter log;
    MediaEncoder encoder;
    bool recording;
    int frames;
    Texture2D frameTex;
    RenderTexture frameRT;

    Transform player;
    MouseMovement look;
    StoryManager story;
    Camera cam;
    Transform camParent;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    float camFar;
    bool detached;

    Canvas overlay;
    Image black;
    Text caption, title, subtitle;
    CanvasGroup captionGroup, titleGroup;
    GameObject hintBox;
    CanvasGroup storyHud;
    readonly List<Canvas> hidden = new List<Canvas>();
    Vector3 S, F, R, centre;
    Lang.Language oldLanguage;
    Campfire fire;

    void Log(string s) { if (log != null) log.WriteLine((frames / (float)FPS).ToString("0.0") + "  " + s); }

    IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(Dir);
        log = new StreamWriter(Dir + "video.txt", false) { AutoFlush = true };
        yield return Safe("setup", Setup(), 60f);
        if (player == null) { Log("no player: stop"); Finish(); yield break; }
        try
        {
            string path = Dir + "SurvivalBD.mp4";
            if (File.Exists(path)) File.Delete(path);
            var attrs = new VideoTrackAttributes { frameRate = new MediaRational(FPS), width = W, height = H, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High };
            var sound = new AudioTrackAttributes { sampleRate = new MediaRational(Rate), channelCount = 2, language = "en" };
            encoder = new MediaEncoder(path, attrs, sound);
            audioOut = new NativeArray<float>(Spf * 2, Allocator.Persistent);
        }
        catch (Exception e) { Log("encoder: " + e); Finish(); yield break; }
        frameRT = new RenderTexture(W, H, 0);
        frameTex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        StartCoroutine(Recorder());

        var shots = new List<(string name, Func<IEnumerator> body, float timeout)>
        {
            ("title", TitleShot, 40f), ("beach", BeachShot, 40f), ("rabbits", RabbitsShot, 50f), ("rain", RainShot, 50f),
            ("chop", ChopShot, 60f), ("fire", FireShot, 60f), ("shadow", ShadowShot, 80f), ("cave", CaveShot, 50f),
            ("spear", SpearShot, 70f), ("pen", PenShot, 60f), ("island still", IslandStill, 20f),
            ("winter", WinterShot, 50f), ("raft build", RaftBuildShot, 50f), ("raft", RaftShot, 120f),
        };
        // a short try of some shots only (a job named "VideoTry...")
        if (Folder.StartsWith("VideoTry")) shots.RemoveAll(s => s.name != "title" && s.name != "chop" && s.name != "fire" && s.name != "shadow");
        foreach (var s in shots)
        {
            Log("shot " + s.name);
            yield return Safe(s.name, s.body(), s.timeout);
            recording = false;
            if (s.name != "raft") Attach();
        }
        Finish();
    }

    void Finish()
    {
        recording = false;
        try { if (encoder != null) { encoder.Dispose(); encoder = null; } } catch (Exception e) { Log("encoder close: " + e.Message); }
        if (audioOut.IsCreated) audioOut.Dispose();
        try { Restore(); } catch (Exception e) { Log("restore: " + e.Message); }
        Log("frames " + frames + " = " + (frames / (float)FPS).ToString("0.0") + " s of video; loudest sample " + loudest.ToString("0.00"));
        if (log != null) log.Close();
        log = null;
        File.WriteAllText(Dir + "finished.txt", "ok");
    }

    void Restore()
    {
        Time.captureFramerate = 0;
        Lang.Current = oldLanguage;
        GameInput.TestDriven = false;
        GameInput.ResetTouch();
        __VideoWalk.On = false;
        GameSettings.Immortal = false;
        if (hiddenCross != null) hiddenCross.SetActive(true);
        SetAdmin(false);
        if (look != null) look.enabled = true;
        if (story != null) SetField(story, "modal", false);
        var weather = WeatherSystem.Instance;
        if (weather != null) weather.autoWeather = true;
        var clock = DayNightSystem.Instance;
        if (clock != null) { clock.autoTime = true; clock.timeSpeed = 1f; }
        if (quality != null) quality();
        SoundManager.EffectsVolume = oldEffects;
        SoundManager.AmbienceVolume = oldAmbience;
    }

    // ---------------- the best picture while filming (put back after) ---------------- //

    Action quality;
    float oldEffects = 1f, oldAmbience = 1f;

    void BestQuality()
    {
        int aa = QualitySettings.antiAliasing;
        var shadowRes = QualitySettings.shadowResolution;
        float shadowDistance = QualitySettings.shadowDistance, lodBias = QualitySettings.lodBias;
        int cascades = QualitySettings.shadowCascades;
        var aniso = QualitySettings.anisotropicFiltering;
        quality = () =>
        {
            QualitySettings.antiAliasing = aa;
            QualitySettings.shadowResolution = shadowRes;
            QualitySettings.shadowDistance = shadowDistance;
            QualitySettings.shadowCascades = cascades;
            QualitySettings.lodBias = lodBias;
            QualitySettings.anisotropicFiltering = aniso;
        };
        QualitySettings.antiAliasing = 4;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 220f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.lodBias = 2.5f;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        // whole trees much farther (the far ones are flat pictures), grass farther, a finer ground
        var forest = FindObjectOfType<Forest>();
        if (forest != null) forest.modelDistancePC = 420f;
        foreach (var terrain in Terrain.activeTerrains)
        {
            terrain.detailObjectDistance = 250f;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 2000f;
        }
        Log("quality: AA 4x, shadows very high 220 m, trees 420 m");
    }

    // one shot, never longer than its timeout (in frames), an exception ends only that shot
    IEnumerator Safe(string name, IEnumerator body, float timeout)
    {
        int limit = Mathf.RoundToInt(timeout * FPS), n = 0;
        var stack = new Stack<IEnumerator>();
        stack.Push(body);
        while (stack.Count > 0)
        {
            if (++n > limit) { Log(name + ": timeout"); yield break; }
            var top = stack.Peek();
            bool more;
            try { more = top.MoveNext(); }
            catch (Exception e) { Log(name + ": " + e.GetType().Name + " " + e.Message + " " + e.StackTrace); yield break; }
            if (!more) { stack.Pop(); continue; }
            object y = top.Current;
            if (y is IEnumerator inner && !(y is CustomYieldInstruction)) { stack.Push(inner); continue; }
            yield return y;
        }
    }

    // ---------------- recording ---------------- //

    IEnumerator Recorder()
    {
        var eof = new WaitForEndOfFrame();
        while (true)
        {
            yield return eof;
            if (!recording || encoder == null) continue;
            Texture2D shot = null;
            try
            {
                shot = ScreenCapture.CaptureScreenshotAsTexture();
                // the pixels as they are on the screen (a Blit through a render texture changed the colours: lighter)
                if (shot.width == W && shot.height == H) frameTex.SetPixels32(shot.GetPixels32());
                else
                {
                    var rt = RenderTexture.GetTemporary(W, H, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                    Graphics.Blit(shot, rt);
                    var before = RenderTexture.active;
                    RenderTexture.active = rt;
                    frameTex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    RenderTexture.active = before;
                    RenderTexture.ReleaseTemporary(rt);
                }
                frameTex.Apply(false);
                encoder.AddFrame(frameTex);
                MixFrame();
                encoder.AddSamples(audioOut);
                frames++;
                if (frames % (FPS * 5) == 1) File.WriteAllBytes(Dir + "frame_" + (frames / FPS).ToString("000") + ".jpg", frameTex.EncodeToJPG(75));
            }
            catch (Exception e) { Log("frame: " + e.Message); recording = false; }
            finally { if (shot != null) Destroy(shot); }
        }
    }

    // a big picture of what the camera sees now, without the HUD and without the captions (3840 x 2160)
    IEnumerator Still(string name)
    {
        bool was = recording;
        recording = false;
        overlay.enabled = false;
        yield return null;
        ScreenCapture.CaptureScreenshot(Dir + name + ".png", Screen.width >= 3000 ? 1 : 2);
        yield return null;
        yield return null;
        overlay.enabled = true;
        recording = was;
        Log("still " + name);
    }

    // ---------------- the sound: mixed in game time, one video frame at a time ---------------- //

    const int Rate = 48000, Spf = Rate / FPS; // 1600 samples in a frame
    const float MasterGain = 2.6f;            // (the game's own levels are quiet for a video)
    // the shadow's music is meant to frighten in the game: quieter here, next to the forest and the fire
    static float ClipScale(string clip) => clip.StartsWith("Shadow drone") ? 0.3f : clip.StartsWith("Shadow strings") ? 0.5f : 1f;
    NativeArray<float> audioOut;
    readonly float[] mix = new float[Spf * 2];
    float loudest, lastMaster;

    class Pcm { public float[] data; public int channels, length, freq; }

    class Voice
    {
        public Pcm pcm;
        public double pos;
        public float gain, lastGain, pan, lastPan, pitch = 1f;
        public bool loop, done, fading;
        public int delay; // samples before it begins
    }

    readonly List<Voice> voices = new List<Voice>();
    readonly Dictionary<AudioSource, (Voice voice, int samples)> followed = new Dictionary<AudioSource, (Voice, int)>();
    readonly Dictionary<AudioClip, Pcm> pcmOf = new Dictionary<AudioClip, Pcm>();
    readonly Dictionary<string, Pcm> files = new Dictionary<string, Pcm>(); // the game's sound files, by name
    readonly HashSet<string> unreadable = new HashSet<string>();
    Pcm[] chopSounds, craftSounds, rockSteps;

    // the game's sound files are compressed (their samples can't be read): loaded again here, uncompressed
    IEnumerator LoadSoundFiles()
    {
        string root = Application.dataPath + "/Resources/Sounds/";
        if (!Directory.Exists(root)) yield break;
        foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(file).ToLowerInvariant();
            AudioType type = ext == ".ogg" ? AudioType.OGGVORBIS : ext == ".mp3" ? AudioType.MPEG : ext == ".wav" ? AudioType.WAV : AudioType.UNKNOWN;
            if (type == AudioType.UNKNOWN) continue;
            using (var request = UnityWebRequestMultimedia.GetAudioClip("file:///" + file.Replace('\\', '/'), type))
            {
                var handler = (DownloadHandlerAudioClip)request.downloadHandler;
                handler.compressed = false;
                handler.streamAudio = false;
                var op = request.SendWebRequest();
                for (int i = 0; i < 300 && !op.isDone; i++) yield return null;
                if (request.result != UnityWebRequest.Result.Success) { Log("sound " + Path.GetFileName(file) + ": " + request.error); continue; }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip == null) continue;
                for (int i = 0; i < 300 && clip.loadState == AudioDataLoadState.Loading; i++) yield return null;
                var pcm = Read(clip);
                if (pcm != null) files[Path.GetFileNameWithoutExtension(file)] = pcm;
                Destroy(clip);
            }
        }
        chopSounds = Sounds("impactWood_heavy");
        craftSounds = Sounds("impactWood_medium");
        rockSteps = Sounds("footstep_concrete");
        Log("sound files: " + files.Count + " (chop " + chopSounds.Length + ", craft " + craftSounds.Length + ", steps " + rockSteps.Length + ")");
    }

    Pcm[] Sounds(string prefix)
    {
        var list = new List<Pcm>();
        foreach (var kv in files) if (kv.Key.StartsWith(prefix)) list.Add(kv.Value);
        return list.ToArray();
    }

    static Pcm Read(AudioClip clip)
    {
        if (clip == null || clip.samples <= 0 || clip.channels <= 0) return null;
        var data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0)) return null;
        return new Pcm { data = data, channels = clip.channels, length = clip.samples, freq = clip.frequency };
    }

    Pcm PcmOf(AudioClip clip)
    {
        if (clip == null) return null;
        if (pcmOf.TryGetValue(clip, out var p)) return p;
        if (!files.TryGetValue(clip.name, out p))
        {
            p = null;
            // (made in code, or not compressed: readable as it is)
            if (!UnityEditor.AssetDatabase.Contains(clip) || clip.loadType == AudioClipLoadType.DecompressOnLoad) p = Read(clip);
            if (p == null && unreadable.Add(clip.name)) Log("sound not readable: " + clip.name);
        }
        pcmOf[clip] = p;
        return p;
    }

    // a sound the game plays with PlayOneShot (it can't be followed): added by the shot itself
    void Sfx(Pcm pcm, float volume, float pitch = 1f, float delay = 0f)
    {
        if (pcm == null || !recording) return;
        voices.Add(new Voice
        {
            pcm = pcm, gain = volume, lastGain = volume,
            pitch = pitch * (1f + UnityEngine.Random.Range(-0.07f, 0.07f)), delay = Mathf.RoundToInt(delay * Rate),
        });
    }

    void Sfx(AudioClip clip, float volume, float pitch = 1f, float delay = 0f) => Sfx(PcmOf(clip), volume, pitch, delay);

    // a sound that goes on until stopped (the torch in the hand, a cold wind): Stop() fades it out
    Voice Loop(AudioClip clip, float volume)
    {
        var pcm = PcmOf(clip);
        if (pcm == null) return null;
        var v = new Voice { pcm = pcm, loop = true, gain = volume, lastGain = 0f, pos = UnityEngine.Random.Range(0, pcm.length) };
        voices.Add(v);
        return v;
    }

    static void Stop(Voice v)
    {
        if (v == null) return;
        v.gain = 0f;
        v.fading = true;
    }
    static Pcm Pick(Pcm[] list) => list != null && list.Length > 0 ? list[UnityEngine.Random.Range(0, list.Length)] : null;
    static AudioClip Pick(AudioClip[] list) => list != null && list.Length > 0 ? list[UnityEngine.Random.Range(0, list.Length)] : null;

    // a new shot: nothing of the last one goes on
    readonly HashSet<string> heard = new HashSet<string>(); // (the sounds of this shot, written once in the log)

    void ResetSound()
    {
        voices.Clear();
        followed.Clear();
        heard.Clear();
    }

    // every AudioSource the game is playing, heard from the camera (its volume, distance and side, every frame)
    void FollowSources()
    {
        var ear = cam != null ? cam.transform : null;
        if (ear == null) return;
        var playing = new HashSet<AudioSource>();
        foreach (var s in FindObjectsOfType<AudioSource>())
        {
            if (s == null || !s.isActiveAndEnabled || !s.isPlaying || s.mute || s.clip == null) continue;
            if (s.clip.name.IndexOf("Rabbit", StringComparison.OrdinalIgnoreCase) >= 0) continue; // (recordings of unknown origin)
            var pcm = PcmOf(s.clip);
            if (pcm == null) continue;
            playing.Add(s);
            float gain = GainOf(s, ear, out float pan) * ClipScale(s.clip.name);
            int samples = s.timeSamples;
            if (followed.TryGetValue(s, out var f))
            {
                if (f.voice.pcm == pcm && !f.voice.done && (s.loop || samples >= f.samples))
                {
                    f.voice.gain = gain;
                    f.voice.pan = pan;
                    f.voice.pitch = s.pitch;
                    followed[s] = (f.voice, samples);
                    continue;
                }
                if (f.voice.loop) { f.voice.gain = 0f; f.voice.fading = true; }
            }
            // it began (or began again, or another clip): from where the game's source is now
            var v = new Voice { pcm = pcm, pos = Mathf.Clamp(samples, 0, pcm.length - 1), loop = s.loop, gain = gain, lastGain = gain, pan = pan, lastPan = pan, pitch = s.pitch };
            if (heard.Add(s.clip.name)) Log("sound: " + s.clip.name + " (" + gain.ToString("0.00") + (s.loop ? ", loop" : "") + ")");
            voices.Add(v);
            followed[s] = (v, samples);
        }
        // stopped: a loop fades out now; a sound played once goes on to its end (the game's sources run in real time,
        // ahead of the slow recording, so they end too early)
        var gone = new List<AudioSource>();
        foreach (var kv in followed) if (kv.Key == null || !playing.Contains(kv.Key)) gone.Add(kv.Key);
        foreach (var s in gone)
        {
            var v = followed[s].voice;
            if (v.loop) { v.gain = 0f; v.fading = true; }
            followed.Remove(s);
        }
    }

    static float GainOf(AudioSource s, Transform ear, out float pan)
    {
        float gain = s.volume;
        pan = Mathf.Clamp(s.panStereo, -1f, 1f);
        float blend = Mathf.Clamp01(s.spatialBlend);
        if (blend <= 0f) return gain;
        Vector3 d = s.transform.position - ear.position;
        float dist = d.magnitude;
        float min = Mathf.Max(0.01f, s.minDistance), max = Mathf.Max(min + 0.01f, s.maxDistance);
        float att;
        switch (s.rolloffMode)
        {
            case AudioRolloffMode.Linear: att = Mathf.Clamp01(1f - (dist - min) / (max - min)); break;
            case AudioRolloffMode.Custom:
                var curve = s.GetCustomCurve(AudioSourceCurveType.CustomRolloff);
                att = curve != null && curve.length > 0 ? Mathf.Clamp01(curve.Evaluate(dist / max)) : 1f;
                break;
            default: att = dist <= min ? 1f : min / Mathf.Min(dist, max); break;
        }
        gain *= Mathf.Lerp(1f, att, blend);
        float side = dist > 0.01f ? Vector3.Dot(d / dist, ear.right) : 0f;
        pan = Mathf.Lerp(pan, side * 0.7f, blend);
        return gain;
    }

    // this frame's 1600 samples: every voice, then the fade of the picture (black = silence) and a soft ceiling
    void MixFrame()
    {
        FollowSources();
        Array.Clear(mix, 0, mix.Length);
        for (int k = voices.Count - 1; k >= 0; k--)
        {
            var v = voices[k];
            PlayVoice(v);
            if (v.done || (v.fading && v.lastGain <= 0.0001f)) voices.RemoveAt(k);
        }
        float master = (black != null ? 1f - black.color.a : 1f) * MasterGain;
        for (int i = 0; i < Spf; i++)
        {
            float m = Mathf.Lerp(lastMaster, master, i / (float)Spf);
            float l = mix[i * 2] * m, r = mix[i * 2 + 1] * m;
            // a limiter: a loud moment (a tree crashing down) is turned down smoothly, never cut flat
            float a = Mathf.Max(Mathf.Abs(l), Mathf.Abs(r));
            if (a > loudest) loudest = a;
            envelope = Mathf.Max(a, envelope * 0.99985f);
            float g = envelope > Ceiling ? Ceiling / envelope : 1f;
            l *= g;
            r *= g;
            audioOut[i * 2] = l;
            audioOut[i * 2 + 1] = r;
            secondSum += l * l + r * r;
            secondPeak = Mathf.Max(secondPeak, Mathf.Abs(l), Mathf.Abs(r));
        }
        lastMaster = master;
        // the level of each second in the log (to check the mix without listening)
        if (++secondFrames >= FPS)
        {
            Log("level " + Mathf.Sqrt(secondSum / (secondFrames * Spf * 2)).ToString("0.000") + " peak " + secondPeak.ToString("0.00") + " voices " + voices.Count);
            secondSum = 0f;
            secondPeak = 0f;
            secondFrames = 0;
        }
    }

    const float Ceiling = 0.85f;
    float envelope, secondSum, secondPeak;
    int secondFrames;

    void PlayVoice(Voice v)
    {
        var p = v.pcm;
        int ch = p.channels;
        double step = p.freq / (double)Rate * Mathf.Clamp(v.pitch, 0.05f, 4f);
        for (int i = 0; i < Spf; i++)
        {
            if (v.delay > 0) { v.delay--; continue; }
            int i0 = (int)v.pos;
            if (i0 >= p.length)
            {
                if (!v.loop) { v.done = true; break; }
                v.pos -= p.length * Math.Floor(v.pos / p.length);
                i0 = (int)v.pos;
            }
            int i1 = i0 + 1 < p.length ? i0 + 1 : (v.loop ? 0 : i0);
            float fr = (float)(v.pos - i0);
            float t = i / (float)Spf;
            float g = v.lastGain + (v.gain - v.lastGain) * t;
            float pan = v.lastPan + (v.pan - v.lastPan) * t;
            float l = p.data[i0 * ch] + (p.data[i1 * ch] - p.data[i0 * ch]) * fr;
            float r = ch > 1 ? p.data[i0 * ch + 1] + (p.data[i1 * ch + 1] - p.data[i0 * ch + 1]) * fr : l;
            mix[i * 2] += l * g * (pan > 0f ? 1f - pan : 1f);
            mix[i * 2 + 1] += r * g * (pan < 0f ? 1f + pan : 1f);
            v.pos += step;
        }
        v.lastGain = v.gain;
        v.lastPan = v.pan;
    }

    // ---------------- the look of the video: black, captions, the title ---------------- //

    void MakeOverlay()
    {
        var go = new GameObject("Video overlay");
        overlay = go.AddComponent<Canvas>();
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 6000;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f); // (the captions are laid out for 1920 x 1080)
        scaler.matchWidthOrHeight = 0.5f;

        var band = UISkin.NewRect("Caption", go.transform);
        UISkin.Place(band, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(70f, 80f), new Vector2(1150f, 92f));
        captionGroup = band.gameObject.AddComponent<CanvasGroup>();
        captionGroup.alpha = 0f;
        var back = UISkin.NewImage("Back", band, UISkin.White, new Color(0f, 0f, 0f, 0.55f), false);
        UISkin.Fill(back.rectTransform);
        var accent = UISkin.NewImage("Accent", band, UISkin.White, UISkin.Gold, false);
        UISkin.Place(accent.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(8f, 92f));
        caption = UISkin.NewText("Text", band, "", 40, Color.white, TextAnchor.MiddleLeft);
        UISkin.Fill(caption.rectTransform, 30f);

        var titleRoot = UISkin.NewRect("Title", go.transform);
        UISkin.Fill(titleRoot);
        titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
        titleGroup.alpha = 0f;
        title = UISkin.NewText("Name", titleRoot, "SurvivalBD", 160, UISkin.Gold, TextAnchor.MiddleCenter);
        UISkin.Centered(title.rectTransform, new Vector2(0f, 70f), new Vector2(1800f, 240f));
        subtitle = UISkin.NewText("By", titleRoot, "A survival game by Bran David Ionel", 50, Color.white, TextAnchor.MiddleCenter);
        UISkin.Centered(subtitle.rectTransform, new Vector2(0f, -75f), new Vector2(1800f, 90f));
        foreach (var t in new[] { title, subtitle, caption })
        {
            var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = new Color(0f, 0f, 0f, 0.7f);
            o.effectDistance = new Vector2(2f, -2f);
        }

        black = UISkin.NewImage("Black", go.transform, UISkin.White, Color.black, false);
        UISkin.Fill(black.rectTransform);
    }

    IEnumerator FadeBlack(float from, float to, float seconds)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(seconds * FPS));
        for (int i = 0; i <= n; i++)
        {
            black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, i / (float)n));
            yield return null;
        }
    }

    // a caption for this long: in, held, out
    IEnumerator Caption(string text, float seconds, float delay = 0.5f)
    {
        caption.text = text;
        int n = Mathf.RoundToInt(seconds * FPS), d = Mathf.RoundToInt(delay * FPS), ramp = Mathf.RoundToInt(0.4f * FPS);
        for (int i = 0; i < n; i++)
        {
            float a = i < d ? 0f : Mathf.Clamp01((i - d) / (float)ramp);
            a = Mathf.Min(a, Mathf.Clamp01((n - i) / (float)ramp));
            captionGroup.alpha = a;
            yield return null;
        }
        captionGroup.alpha = 0f;
    }

    // a shot begins: recording, out of the black, its caption over the given time
    IEnumerator Open(string text, float seconds)
    {
        ResetSound();
        recording = true;
        // over the quick bar when the HUD shows, lower in the cinematic shots
        var rect = (RectTransform)captionGroup.transform;
        rect.anchoredPosition = new Vector2(70f, hidden.Count == 0 ? 140f : 80f);
        if (!string.IsNullOrEmpty(text)) StartCoroutine(Caption(text, seconds));
        yield return FadeBlack(1f, 0f, 0.45f);
    }

    IEnumerator Close()
    {
        yield return FadeBlack(0f, 1f, 0.45f);
        recording = false;
        captionGroup.alpha = 0f;
    }

    static IEnumerator Frames(float seconds)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(seconds * FPS));
        for (int i = 0; i < n; i++) yield return null;
    }

    // ---------------- the HUD and the camera ---------------- //

    void Hud(bool on)
    {
        if (!on)
        {
            foreach (var c in FindObjectsOfType<Canvas>())
                if (c != null && c.isRootCanvas && c != overlay && c.enabled && c.renderMode != RenderMode.WorldSpace) { c.enabled = false; hidden.Add(c); }
        }
        else
        {
            foreach (var c in hidden) if (c != null) c.enabled = true;
            hidden.Clear();
        }
    }

    bool hideFireLabel;
    GameObject hiddenCross;

    void LateUpdate()
    {
        if (hintBox != null) hintBox.transform.localScale = Vector3.zero; // ("Tab or Esc..." on the PC)
        if (storyHud != null) storyHud.alpha = 0f;                      // (the level panel: the story is held still)
        if (pinned != null && pinned.Alive) pinned.transform.position = pinAt;  // (the rabbit of the spear shot stays where it is)
        if (CanonCross.Instance != null && CanonCross.Instance.gameObject.activeSelf)  // (no cross on the mountain in this video)
        {
            hiddenCross = CanonCross.Instance.gameObject;
            hiddenCross.SetActive(false);
        }
        if (hideFireLabel)                                              // (the fire's seconds over it, in the cinematic shot)
        {
            var labelObject = GameObject.Find("Fire label");
            if (labelObject != null) foreach (var r in labelObject.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
        }
    }

    void Detach()
    {
        if (detached || cam == null) return;
        cam.transform.SetParent(null, true);
        var guard = cam.GetComponent<CameraGuard>();
        if (guard != null) guard.enabled = false;
        cam.farClipPlane = Mathf.Max(camFar, 4000f);
        detached = true;
    }

    void Attach()
    {
        if (!detached || cam == null || camParent == null) return;
        cam.transform.SetParent(camParent, false);
        cam.transform.localPosition = camLocalPos;
        cam.transform.localRotation = camLocalRot;
        var guard = cam.GetComponent<CameraGuard>();
        if (guard != null) guard.enabled = true;
        cam.farClipPlane = camFar;
        detached = false;
    }

    void Aim(Vector3 pos, Vector3 at)
    {
        cam.transform.position = pos;
        Vector3 d = at - pos;
        if (d.sqrMagnitude > 1e-4f) cam.transform.rotation = Quaternion.LookRotation(d);
    }

    static float Ease(float t) => t * t * (3f - 2f * t);

    // the camera along a path for this long (t from 0 to 1, eased)
    IEnumerator Fly(float seconds, Func<float, (Vector3 pos, Vector3 at)> path, Action<float> each = null)
    {
        int n = Mathf.Max(2, Mathf.RoundToInt(seconds * FPS));
        for (int i = 0; i < n; i++)
        {
            float t = Ease(i / (n - 1f));
            var p = path(t);
            Aim(p.pos, p.at);
            each?.Invoke(t);
            yield return null;
        }
    }

    // the player out of the picture, but near enough for the trees there to be full models
    void Park(Vector3 near, Vector3 away)
    {
        Vector3 p = near + away.normalized * 45f;
        Teleport(p, 0f, 0f);
    }

    static void SetTime(float hour)
    {
        var clock = DayNightSystem.Instance;
        if (clock != null) clock.TimeOfDay = hour / 24f;
    }

    // ---------------- setup ---------------- //

    IEnumerator Setup()
    {
        for (int i = 0; i < 900 && (StoryManager.Instance == null || FindObjectOfType<PlayerMovement>() == null || EquipSystem.Instance == null); i++) yield return null;
        story = StoryManager.Instance;
        var movement = FindObjectOfType<PlayerMovement>();
        if (story == null || movement == null) yield break;
        oldLanguage = Lang.Current;
        Lang.Current = Lang.Language.English;
        for (int i = 0; i < 40; i++)
        {
            var skip = GameObject.Find("Skip");
            if (skip != null && skip.GetComponent<Button>() != null) skip.GetComponent<Button>().onClick.Invoke();
            var d = GetField<StoryData>(story, "data");
            if (i >= 4 && d != null && d.introSeen && !GetField<bool>(story, "modal") && Time.timeScale > 0f && GameObject.Find("Skip") == null) break;
            yield return Frames(0.5f);
        }
        yield return Frames(0.8f);
        SetField(story, "modal", true);
        player = movement.transform;
        look = player.GetComponent<MouseMovement>();
        if (look != null) look.enabled = false;
        cam = Camera.main;
        camParent = cam.transform.parent;
        camLocalPos = cam.transform.localPosition;
        camLocalRot = cam.transform.localRotation;
        camFar = cam.farClipPlane;
        GameInput.TestDriven = true; // (no Immortal: its "(Immortal: it can't)" would show in the shadow's warning)
        var clock = DayNightSystem.Instance;
        clock.autoTime = false;
        clock.timeSpeed = 1f;
        var weather = WeatherSystem.Instance;
        if (weather != null) { weather.autoWeather = false; weather.SetTestWeather(WeatherSystem.WeatherType.Clear); weather.SetSnowCover(0f); }
        var start = FindObjectOfType<StoryStart>();
        S = start != null ? start.transform.position : player.position;
        F = start != null ? Flat(start.transform.forward).normalized : Vector3.forward;
        R = Vector3.Cross(Vector3.up, F);
        centre = Ocean.Instance != null ? Ocean.Instance.transform.position : S;
        var pc = FindObjectOfType<PcControls>();
        if (pc != null) hintBox = GetField<GameObject>(pc, "hintBox");
        var hud = GetField<Canvas>(story, "hudCanvas");
        if (hud != null)
        {
            storyHud = hud.gameObject.GetComponent<CanvasGroup>();
            if (storyHud == null) storyHud = hud.gameObject.AddComponent<CanvasGroup>();
        }
        if (GetComponent<__VideoWalk>() == null) gameObject.AddComponent<__VideoWalk>();
        MakeOverlay();
        black.color = Color.black;
        // the sound at the game's normal levels (not the player's sliders), the best picture
        oldEffects = SoundManager.EffectsVolume;
        oldAmbience = SoundManager.AmbienceVolume;
        SoundManager.EffectsVolume = 1f;
        SoundManager.AmbienceVolume = 1f;
        BestQuality();
        yield return LoadSoundFiles();
        Time.captureFramerate = FPS;
        Log("setup: screen " + Screen.width + "x" + Screen.height + ", touch " + GameInput.TouchControls + ", start " + S);
    }

    // ---------------- the shots ---------------- //

    IEnumerator TitleShot()
    {
        SetTime(9f);
        Hud(false);
        Detach();
        Vector3 a = centre + new Vector3(-1150f, 330f, -950f), b = centre + new Vector3(-560f, 200f, -450f);
        Vector3 look0 = centre + new Vector3(0f, -10f, 120f), look1 = centre + new Vector3(80f, -30f, 160f);
        Park(centre, Vector3.left);
        Aim(a, look0);
        yield return Frames(1f); // (the far trees and the sounds are ready)
        ResetSound();
        recording = true;
        StartCoroutine(TitleFade(7f));
        StartCoroutine(FadeBlack(1f, 0f, 1f));
        yield return Fly(7.5f, t => (Vector3.Lerp(a, b, t), Vector3.Lerp(look0, look1, t)));
        yield return Close();
    }

    IEnumerator TitleFade(float seconds)
    {
        int n = Mathf.RoundToInt(seconds * FPS), d = FPS, ramp = FPS;
        for (int i = 0; i < n; i++)
        {
            titleGroup.alpha = Mathf.Min(Mathf.Clamp01((i - d) / (float)ramp), Mathf.Clamp01((n - i) / (float)ramp));
            yield return null;
        }
        titleGroup.alpha = 0f;
    }

    IEnumerator BeachShot()
    {
        SetTime(8.2f);
        Detach();
        Park(S, R);
        Vector3 a = S - F * 70f + R * 14f + Vector3.up * 9f, b = S + F * 22f - R * 4f + Vector3.up * 5f;
        Vector3 la = S + Vector3.up * 1.5f, lb = S + F * 90f + Vector3.up * 3f;
        Aim(a, la);
        yield return Frames(0.5f);
        yield return Open("Shipwrecked, alone, on a wild island", 6.5f);
        yield return Fly(6.5f, t => (Vector3.Lerp(a, b, t), Vector3.Lerp(la, lb, Mathf.Clamp01(t * 1.4f - 0.2f))));
        yield return Close();
    }

    IEnumerator RabbitsShot()
    {
        SetTime(10.5f);
        Detach();
        var rabbit = FindRabbit(S + F * 80f, 500f);
        Vector3 c = rabbit != null ? rabbit.transform.position : S + F * 80f;
        Park(c, -F);
        yield return Frames(0.5f); // (the trees around become models)
        float a0 = UnityEngine.Random.Range(0f, 360f);
        (Vector3, Vector3) Orbit(float t)
        {
            Vector3 at = rabbit != null ? rabbit.transform.position : c;
            float ang = (a0 + 95f * t) * Mathf.Deg2Rad;
            Vector3 pos = at + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 7f;
            pos.y = Mathf.Max(Forest.GroundY(pos), Forest.GroundY(at)) + 2.4f;
            return (pos, at + Vector3.up * 0.3f);
        }
        var first = Orbit(0f);
        Aim(first.Item1, first.Item2);
        yield return Open("Living forests: the trees spread, rabbits multiply", 7f);
        yield return Fly(7f, t => Orbit(t));
        yield return Close();
    }

    // rain over the meadow, the forest and the mountains behind (the rain falls around the camera)
    IEnumerator RainShot()
    {
        SetTime(14f);
        var weather = WeatherSystem.Instance;
        if (weather == null) yield break;
        weather.SetTestWeather(WeatherSystem.WeatherType.Rain);
        Detach();
        Hud(false);
        Vector3 a = S + F * 30f + R * 22f, b = S + F * 62f + R * 8f;
        a.y = Forest.GroundY(a) + 6f;
        b.y = Forest.GroundY(b) + 4.5f;
        Vector3 la = S + F * 170f + R * 10f + Vector3.up * 8f, lb = S + F * 190f - R * 10f + Vector3.up * 4f;
        Teleport(Vector3.Lerp(a, b, 0.5f), 0f, 0f);
        Aim(a, la);
        for (int i = 0; i < 15 * FPS && weather.Rain < 0.6f; i++) yield return null;
        Log("rain " + weather.Rain.ToString("0.00"));
        yield return Open("Rain, wind and seasons: the weather keeps changing", 7f);
        yield return Fly(7f, t => (Vector3.Lerp(a, b, t), Vector3.Lerp(la, lb, t)));
        yield return Close();
        weather.SetTestWeather(WeatherSystem.WeatherType.Clear);
    }

    IEnumerator ChopShot()
    {
        SetTime(11f);
        var weather = WeatherSystem.Instance;
        for (int i = 0; i < 10 * FPS && weather != null && weather.Rain > 0.05f; i++) yield return null; // (the rain is over)
        Attach();
        Hud(true);
        EmptyHands();
        Give("Axe", 1);
        yield return Hold("Axe");
        // a tree alone on the meadow (a birch if one is: its white trunk shows), no leaves of others before the camera
        var tree = FindLoneTree(S + F * 40f, 6f, 14f, 1, 8f) ?? FindLoneTree(S + F * 40f, 5f, 12f, -1, 8f) ?? FindTree(S + F * 40f, 5f, 11f);
        if (tree == null) { Log("chop: no tree"); yield break; }
        var choppable = tree.Choppable;
        Vector3 basePos = tree.data.position;
        Vector3 trunk = basePos + Vector3.up * 1.4f;
        Vector3 stand = basePos + Flat(player.position - basePos).normalized * (tree.TrunkRadius + 2.2f);
        Teleport(stand, YawTo(stand, basePos), 0f);
        yield return Frames(0.6f);
        AimAt(trunk);
        yield return Open("Craft tools and fell trees for firewood", 7f);
        float health = choppable != null ? choppable.treeHealth : 0f;
        for (int i = 0; i < 6 && tree != null && !tree.Falling; i++)
        {
            AimAt(trunk);
            yield return Tap(GameInput.TouchAction.Primary);
            Sfx(Pick(ProceduralAudio.Swooshes), 0.55f, 1f, 0.1f); // (the game's whoosh, 0.1 s after the swing begins)
            // the axe can swing again after 1 s; the knock when it hits the trunk
            for (int k = 0; k < Mathf.RoundToInt(1.05f * FPS); k++)
            {
                if (choppable != null && choppable.treeHealth < health - 0.001f)
                {
                    health = choppable.treeHealth;
                    Sfx(Pick(chopSounds), 0.85f);
                }
                if (k % 3 == 0) AimAt(trunk);
                yield return null;
            }
        }
        // the tree falls: the view follows it a little up
        for (int i = 0; i < 3 * FPS; i++)
        {
            if (look != null) look.SetLook(YawTo(player.position, basePos), Mathf.Lerp(5f, -12f, i / (3f * FPS)));
            yield return null;
        }
        yield return Close();
        EmptyHands();
    }

    IEnumerator FireShot()
    {
        SetTime(19.6f);
        Attach();
        Hud(false);
        EmptyHands();
        Vector3 spot = S + F * 32f;
        Teleport(spot, YawTo(spot, spot + F), 38f);
        yield return Frames(0.6f);
        Give("Campfire", 1);
        var before = new HashSet<Campfire>(FindObjectsOfType<Campfire>());
        UseItem(FindItem("Campfire"));
        yield return Frames(0.8f);
        for (int i = 0; i < 8 && !GetField<bool>(PlacementSystem.Instance, "isValidPlacement"); i++)
        {
            look.SetLook(cam.transform.eulerAngles.y + 45f, 38f);
            yield return Frames(0.4f);
        }
        yield return Tap(GameInput.TouchAction.Primary);
        yield return Frames(1.2f);
        foreach (var c in FindObjectsOfType<Campfire>()) if (!before.Contains(c) && !UnderHolding(c.transform)) fire = c;
        if (fire == null) { Log("fire: not placed"); yield break; }
        fire.AddFuel(600f);
        yield return Frames(1f);
        Detach();
        Vector3 f = fire.FireCentre;
        // the player stays by the fire (out of the picture: the body is not drawn), so the shadow does not come
        Teleport(f - F * 2.5f, 0f, 0f);
        hideFireLabel = true;
        (Vector3, Vector3) Orbit(float t)
        {
            float ang = (200f + 90f * t) * Mathf.Deg2Rad;
            Vector3 pos = f + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 5.2f;
            pos.y = Forest.GroundY(pos) + 1.7f;
            return (pos, f + Vector3.up * 0.7f);
        }
        var first = Orbit(0f);
        Aim(first.Item1, first.Item2);
        yield return Open("Light a fire before night falls", 8f);
        yield return Fly(8f, t => Orbit(t), t => SetTime(Mathf.Lerp(19.6f, 21.6f, t)));
        yield return Still("2_campfire_night");
        yield return Close();
        hideFireLabel = false;
    }

    IEnumerator ShadowShot()
    {
        SetTime(23f);
        Attach();
        Hud(true);
        EmptyHands();
        // far from the fire, at the edge of the forest
        Vector3 spot = (fire != null ? fire.transform.position : S + F * 32f) + F * 90f + R * 70f;
        Teleport(spot, YawTo(spot, spot + F), 0f);
        var shadow = NightShadow.Instance;
        for (int i = 0; i < 20 * FPS && (shadow == null || !shadow.IsHunting); i++) yield return null;
        if (shadow == null || !shadow.IsHunting) { Log("shadow: it did not come"); yield break; }
        yield return Frames(1f);
        float yaw = YawTo(player.position, shadow.transform.position);
        look.SetLook(yaw, 0f);
        yield return Open("At night, a shadow comes for anyone without a fire...", 5f);
        int beat = 0;
        Voice torchSound = null;
        for (int i = 0; i < Mathf.RoundToInt(8.5f * FPS); i++)
        {
            if (i == Mathf.RoundToInt(4.5f * FPS))
            {
                Give("torch", 1);
                StartCoroutine(Caption("...but a burning torch keeps it away", 4f, 0.2f));
                StartCoroutine(Hold("torch"));
                torchSound = Loop(ProceduralAudio.FireCrackle, 0.16f);
            }
            if (shadow != null)
            {
                Vector3 at = Centre(shadow.transform);
                float want = YawTo(player.position, at);
                yaw = Mathf.LerpAngle(yaw, want, 0.08f);
                look.SetLook(yaw, Mathf.Clamp(-Mathf.Atan2(at.y - cam.transform.position.y, Flat(at - cam.transform.position).magnitude) * Mathf.Rad2Deg, -10f, 20f));
            }
            // its heartbeat (the game plays it with PlayOneShot)
            float presence = NightShadow.Presence;
            if (--beat <= 0 && presence > 0.25f)
            {
                Sfx(ShadowSounds.Heartbeat, 0.5f * presence);
                beat = Mathf.RoundToInt(Mathf.Lerp(1f, 0.7f, presence) * FPS);
            }
            yield return null;
        }
        Stop(torchSound);
        yield return Close();
        EmptyHands();
    }

    IEnumerator CaveShot()
    {
        var cave = Cave.Instance;
        if (cave == null || cave.path.Count < 8) { Log("cave: none"); yield break; }
        if (cave.rockfall != null) cave.rockfall.SetActive(false);
        Attach();
        Hud(true);
        EmptyHands();
        Give("torch", 1);
        Vector3 a = cave.path[2], b = cave.path[5];
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.position = a + Vector3.up * 0.3f;
        cc.enabled = true;
        look.SetLook(YawTo(a, b), 6f);
        yield return Hold("torch");
        yield return Frames(2.5f); // (the cave's shaders and the torch light are ready before the picture)
        int next = 4;
        yield return Open("Explore the old monk's cave, with a torch in hand", 7.5f);
        var torchSound = Loop(ProceduralAudio.FireCrackle, 0.16f);
        __VideoWalk.Move = new Vector2(0f, 0.5f);
        __VideoWalk.On = true;
        float yaw = YawTo(player.position, cave.path[next]);
        Vector3 lastStep = player.position;
        for (int i = 0; i < Mathf.RoundToInt(7.5f * FPS); i++)
        {
            Vector3 at = cam.transform.position;
            while (next < cave.path.Count - 1 && Flat(cave.path[next] - at).magnitude < 3f) next++;
            yaw = Mathf.LerpAngle(yaw, YawTo(at, cave.path[next]), 0.06f);
            look.SetLook(yaw, 6f);
            // steps on the rock, one every 3.2 m (as the game's FootstepAudio)
            if (Flat(player.position - lastStep).magnitude > 3.2f)
            {
                lastStep = player.position;
                Sfx(Pick(rockSteps), 0.45f);
            }
            yield return null;
        }
        __VideoWalk.On = false;
        GameInput.touchMove = Vector2.zero; // (else the player keeps walking forward in the next shots)
        Stop(torchSound);
        yield return Close();
        EmptyHands();
    }

    IEnumerator SpearShot()
    {
        SetTime(15f);
        Attach();
        Hud(true);
        EmptyHands();
        if (Resources.Load<GameObject>(Spear.Item) == null) { Log("spear: no item"); yield break; }
        Give("Axe", 1);
        Give(Spear.Item, 3);
        RabbitLife target = FindRabbit(S + F * 80f, 600f);
        if (target == null) { Log("spear: no rabbit"); yield break; }
        // it sits still for the shot (a hunter waits for that moment): stopped, its own steps off, held in place
        try { target.GetType().GetMethod("Stop", Any).Invoke(target, null); } catch (Exception e) { Log("spear stop: " + e.Message); }
        target.enabled = false;
        Vector3 rp = target.transform.position;
        pinned = target;
        pinAt = rp;
        // 12 m away on land, with nothing between the eyes and the rabbit
        Vector3 stand = rp + Vector3.forward * 12f;
        for (int k = 0; k < 16; k++)
        {
            Vector3 d = Quaternion.Euler(0f, k * 22.5f, 0f) * Vector3.forward;
            Vector3 p = rp + d * 12f;
            if (!IsLand(p)) continue;
            Vector3 eye = new Vector3(p.x, Forest.GroundY(p) + 3.2f, p.z), aim = RabbitCentre(target);
            if (Physics.Raycast(eye, (aim - eye).normalized, out RaycastHit hit, Vector3.Distance(eye, aim) - 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<RabbitLife>() != target && hit.collider.GetComponentInParent<PlayerMovement>() == null) continue;
            stand = p;
            break;
        }
        Teleport(stand, YawTo(stand, rp), 5f);
        yield return Hold(Spear.Item);
        yield return Frames(0.5f);
        AimAt(RabbitCentre(target));
        yield return Open("Hunt with a thrown spear, from 10-15 metres", 6f);
        for (int i = 0; i < Mathf.RoundToInt(1.2f * FPS); i++) { if (target != null) AimAt(RabbitCentre(target)); yield return null; }
        for (int attempt = 0; attempt < 3 && target != null && !target.animal.isDead; attempt++)
        {
            AimAt(RabbitCentre(target));
            var lastBefore = ThrownSpear.Last;
            float distance = Vector3.Distance(cam.transform.position, RabbitCentre(target));
            bool began = Spear.ThrowNow();
            for (int i = 0; i < Mathf.RoundToInt(1f * FPS) && ThrownSpear.Last == lastBefore; i++)
            {
                if (target != null) AimAt(RabbitCentre(target));
                yield return null;
            }
            var thrown = ThrownSpear.Last != lastBefore ? ThrownSpear.Last : null;
            if (thrown != null) Sfx(Pick(ProceduralAudio.Swooshes), 0.6f); // (the game's PlaySwing)
            for (int i = 0; i < Mathf.RoundToInt(1.2f * FPS) && thrown != null && thrown.Flying; i++) yield return null;
            if (thrown != null && thrown.HitAnimal != null) Sfx(ProceduralAudio.TreeImpact, 0.45f, 2.2f); // a dull thud
            Log("spear throw " + attempt + ": from " + distance.ToString("0.0") + " m, began " + began + ", thrown " + (thrown != null)
                + ", hit " + (thrown != null && thrown.HitAnimal != null ? thrown.HitAnimal.name : "nothing") + ", dead " + (target != null && target.animal.isDead));
            yield return Frames(0.6f);
            if (target != null && !target.animal.isDead) { yield return Hold(Spear.Item); yield return Frames(0.5f); }
        }
        pinned = null;
        if (target != null) target.enabled = true;
        // a look at the rabbit lying there
        for (int i = 0; i < Mathf.RoundToInt(1.8f * FPS); i++) { if (target != null) AimAt(RabbitCentre(target)); yield return null; }
        yield return Close();
        Log("spear: rabbit dead " + (target != null && target.animal.isDead));
        EmptyHands();
    }

    RabbitLife pinned;
    Vector3 pinAt;

    IEnumerator PenShot()
    {
        SetTime(12.5f);
        Detach();
        Hud(false);
        EmptyHands();
        Vector3 c = S + F * 70f + R * 25f;
        c.y = Forest.GroundY(c);
        Vector3[] corners = { c - R * 6f - F * 4f, c + R * 6f - F * 4f, c + R * 6f + F * 4f, c - R * 6f + F * 4f };
        var panels = new List<(BuildingKind kind, Vector3 mid, float yaw, float length)>();
        Vector3 gate = c - F * 4f;
        for (int k = 0; k < 4; k++)
        {
            Vector3 a = corners[k], b = corners[(k + 1) % 4];
            int n = Mathf.RoundToInt(Flat(b - a).magnitude / Fences.Length);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / n), q = Vector3.Lerp(a, b, (float)(i + 1) / n);
                Vector3 mid = (p + q) * 0.5f;
                mid.y = Forest.GroundY(mid);
                var kind = panels.Count == 1 ? BuildingKind.FenceGate : BuildingKind.Fence;
                if (kind == BuildingKind.FenceGate) gate = mid;
                panels.Add((kind, mid, Quaternion.LookRotation(Flat(q - p)).eulerAngles.y, Flat(q - p).magnitude));
            }
        }
        Park(c, R);
        // the camera goes round the yard from the gate's side while the fence goes up, panel after panel
        (Vector3, Vector3) Orbit(float t)
        {
            float a = Mathf.Lerp(-55f, 55f, t) * Mathf.Deg2Rad;
            Vector3 pos = c + (-F * Mathf.Cos(a) + R * Mathf.Sin(a)) * 17f;
            pos.y = Mathf.Max(c.y, Forest.GroundY(pos)) + 8f;
            return (pos, c + Vector3.up * 0.3f);
        }
        var first = Orbit(0f);
        Aim(first.Item1, first.Item2);
        yield return Frames(0.5f);
        var rising = new List<(Transform t, Vector3 to, int start)>();
        int frame = 0, built = 0, every = Mathf.RoundToInt(0.22f * FPS), rise = Mathf.RoundToInt(0.25f * FPS);
        int releaseAt = Mathf.RoundToInt(3.6f * FPS);
        var population = RabbitPopulation.Instance;
        var spawn = typeof(RabbitPopulation).GetMethod("Spawn", Any);
        void Each(float t)
        {
            // a new panel every 0.22 s, coming up out of the ground with a knock of wood
            if (built < panels.Count && frame >= 8 + built * every)
            {
                var d = panels[built++];
                var go = Fences.Create(d.kind, d.mid, d.yaw, d.length, true);
                Buildings.OnPlaced(go, d.kind, d.length);
                rising.Add((go.transform, go.transform.position, frame));
                Sfx(Pick(craftSounds), 0.6f);
            }
            foreach (var r in rising)
            {
                if (r.t == null) continue;
                float k = Mathf.Clamp01((frame - r.start) / (float)rise);
                r.t.position = r.to - Vector3.up * (1f - Ease(k)) * 2.6f;
            }
            // then two pairs of rabbits are let in at the gate and run inside
            if (frame == releaseAt && population != null && spawn != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 at = gate + F * 1.3f + R * (i - 1.5f) * 0.9f;
                    at.y = Buildings.GroundAt(at);
                    var sex = i % 2 == 0 ? RabbitInfo.Sex.Male : RabbitInfo.Sex.Female;
                    var life = spawn.Invoke(population, new object[] { at, YawTo(at, c), sex, RabbitPopulation.AdultHours + 12f }) as RabbitLife;
                    if (life == null) continue;
                    life.restHours = 2f;
                    life.RunFrom(gate - F * 2f, 5f);
                }
                try { Invoke(population, "UpdateActive"); } catch (Exception e) { Log("pen: " + e.Message); }
                StartCoroutine(Caption("...let in a male and a female rabbit: they multiply", 4.6f, 0.2f));
            }
            frame++;
        }
        yield return Open("Build a pen with fence panels and a gate", 3.9f);
        yield return Fly(8.6f, t => Orbit(t), Each);
        yield return Close();
    }

    IEnumerator IslandStill()
    {
        SetTime(11f);
        Detach();
        Hud(false);
        Park(centre, Vector3.left);
        Aim(centre + new Vector3(-470f, 175f, -420f), centre + new Vector3(140f, -25f, 200f));
        yield return Frames(1f);
        yield return Still("1_island_from_above");
    }

    IEnumerator WinterShot()
    {
        SetTime(12f);
        var time = TimeManager.Instance;
        if (time != null) time.SetSeason(TimeManager.Season.Winter);
        var weather = WeatherSystem.Instance;
        if (weather != null) { weather.BeginSnow(24f); weather.SetSnowCover(1f); }
        Detach();
        Hud(false);
        Vector3 c = S + F * 120f;
        Park(c, Vector3.zero - F);
        yield return Frames(2f);
        Vector3 a = c - F * 40f + R * 30f + Vector3.up * 26f, b = c + F * 30f + R * 10f + Vector3.up * 14f;
        Vector3 la = c + F * 80f, lb = c + F * 160f + Vector3.up * 20f;
        // the third picture: the snowy island, seen from halfway along the flight
        Vector3 midPos = Vector3.Lerp(a, b, 0.5f), midAt = Vector3.Lerp(la, lb, 0.5f);
        Park(midPos, -F);
        Aim(midPos, midAt);
        yield return Frames(1f);
        yield return Still("3_winter_on_the_island");
        Park(c, -F);
        Aim(a, la);
        yield return Frames(0.5f);
        yield return Open("...and winter covers the island in snow", 7f);
        var cold = Loop(ProceduralAudio.Wind, 0.2f);
        yield return Fly(7f, t => (Vector3.Lerp(a, b, t), Vector3.Lerp(la, lb, t)), t => { if (Mathf.Abs(t - 0.5f) < 0.01f) Park(Vector3.Lerp(a, b, t), -F); });
        Stop(cold);
        yield return Close();
        if (time != null) time.SetSeason(TimeManager.Season.Summer);
        if (weather != null) { weather.SetTestWeather(WeatherSystem.WeatherType.Clear); weather.SetSnowCover(0f); }
    }

    // the raft on the beach, its logs, beams, ropes, mast and sail coming as the materials do; then it slides into the sea
    IEnumerator RaftBuildShot()
    {
        SetTime(16.2f);
        Detach();
        Hud(false);
        EmptyHands();
        var raft = Raft.Instance != null ? Raft.Instance : Raft.MakeSite(new int[Raft.Needs.Length]);
        if (raft == null) { Log("raft build: none"); yield break; }
        if (raft.InWater) { Log("raft build: already in the sea"); yield break; }
        for (int i = 0; i < raft.have.Length; i++) raft.have[i] = 0;
        Invoke(raft, "ShowParts");
        var beacon = GameObject.Find("Raft beacon (made in code)"); // (the light over the site: seen from far away, not here)
        if (beacon != null) Destroy(beacon);
        Vector3 rp = raft.transform.position;
        Vector3 sea = Flat(raft.transform.forward).normalized, side = Vector3.Cross(Vector3.up, sea);
        Park(rp, -sea);
        // from the sea's side, over the sand and the water (no trees in the way), the island behind it; a little
        // after the raft when it slides
        (Vector3, Vector3) Orbit(float t)
        {
            float a = Mathf.Lerp(100f, 150f, t) * Mathf.Deg2Rad;
            Vector3 at = raft != null ? raft.transform.position : rp;
            Vector3 pos = rp + (at - rp) * 0.6f + (-sea * Mathf.Cos(a) + side * Mathf.Sin(a)) * 23f;
            pos.y = Mathf.Max(rp.y, Forest.GroundY(pos)) + 9f;
            return (pos, at + Vector3.up * 1.3f);
        }
        var first = Orbit(0f);
        Aim(first.Item1, first.Item2);
        yield return Frames(0.6f);
        // what comes in each step (logs, planks, rope, sticks first; the food, hides and rabbits for the trip at the end)
        int steps = 7, frame = 0, every = Mathf.RoundToInt(0.5f * FPS), done = 0;
        int launchAt = 12 + steps * every;
        void Each(float t)
        {
            if (done < steps && frame >= 12 + done * every)
            {
                done++;
                for (int i = 0; i < 4; i++) raft.have[i] = Mathf.CeilToInt(Raft.Amounts[i] * done / (float)steps);
                if (done == steps) for (int i = 0; i < raft.have.Length; i++) raft.have[i] = Raft.Amounts[i];
                Invoke(raft, "ShowParts");
                // the game's PlayCraft: three quick knocks of wood
                for (int k = 0; k < 3; k++) Sfx(Pick(craftSounds), 0.6f, 1f, k * 0.15f);
            }
            if (frame == launchAt)
            {
                try { raft.GetType().GetMethod("PutInWater", Any).Invoke(raft, new object[] { false }); } catch (Exception e) { Log("raft build: " + e.Message); }
                StartCoroutine(Caption("Finished: it slides into the sea", 4f, 0.2f));
                Sfx(ProceduralAudio.Splash, 0.5f, 0.8f, 1.6f);
                Sfx(ProceduralAudio.Splash, 0.4f, 0.7f, 2.6f);
            }
            frame++;
        }
        yield return Open("Build a raft on the beach, log by log", 4.2f);
        yield return Fly(9f, t => Orbit(t), Each);
        yield return Close();
    }

    // on the raft, away from the island: the game's own ending, shortened (its first words, then its last and the end)
    IEnumerator RaftShot()
    {
        SetTime(17f);
        Attach();
        Hud(true);
        EmptyHands();
        var raft = Raft.Instance != null ? Raft.Instance : Raft.MakeSite((int[])Raft.Amounts.Clone());
        if (raft == null) { Log("raft: none"); yield break; }
        for (int i = 0; i < 12 * FPS && !raft.InWater; i++) yield return null;
        Vector3 rp = raft.transform.position;
        Vector3 shore = rp - Flat(rp - centre).normalized * 9f;
        Teleport(shore, YawTo(shore, rp), 10f);
        yield return Frames(0.5f);
        raft.Board();
        int startFrame = Time.frameCount;
        yield return null;
        overlay.enabled = true; // (the ending hides every canvas once)
        yield return Open("", 7f);
        overlay.enabled = true;
        yield return Frames(6.3f);
        yield return FadeBlack(0f, 1f, 0.5f);
        recording = false;
        // (the ending goes on: its 2nd and 3rd lines are left out; on to just before its last line, at 19.5 s)
        while (Time.frameCount - startFrame < Mathf.RoundToInt(19.2f * FPS)) yield return null;
        ResetSound();
        recording = true;
        yield return FadeBlack(1f, 0f, 0.6f);
        yield return Frames(13.2f); // the last line, then THE END (at 27 s)
        yield return FadeBlack(0f, 1f, 1.5f);
        recording = false;
    }

    // ---------------- helpers (as in the systems test, waits in frames) ---------------- //

    static T GetField<T>(object o, string name)
    {
        if (o == null) return default;
        var f = o.GetType().GetField(name, Any);
        return f != null && f.GetValue(o) is T v ? v : default;
    }

    static void SetField(object o, string name, object value)
    {
        var f = o != null ? o.GetType().GetField(name, Any) : null;
        if (f != null) f.SetValue(o, value);
    }

    static void SetAdmin(bool unlocked)
    {
        var p = typeof(DebugEnvironmentPanel).GetProperty("AdminUnlocked", Any);
        var set = p != null ? p.GetSetMethod(true) : null;
        if (set != null) set.Invoke(null, new object[] { unlocked });
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }
    static float YawTo(Vector3 from, Vector3 to) { var d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

    void Teleport(Vector3 p, float yaw, float pitch)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        player.position = new Vector3(p.x, Forest.GroundY(p) + 1.95f, p.z);
        if (cc != null) cc.enabled = true;
        if (look != null) look.SetLook(yaw, pitch);
    }

    void AimAt(Vector3 target)
    {
        if (cam == null || look == null) return;
        for (int i = 0; i < 4; i++)
        {
            Vector3 d = target - cam.transform.position;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            look.SetLook(yaw, Mathf.Clamp(pitch, -89f, 89f));
        }
    }

    static IEnumerator Tap(GameInput.TouchAction action)
    {
        GameInput.PressTouch(action);
        GameInput.ReleaseTouch(action);
        yield return null;
        yield return null;
    }

    static Vector3 Centre(Transform t)
    {
        if (t == null) return Vector3.zero;
        bool any = false;
        var b = new Bounds(t.position, Vector3.zero);
        foreach (var r in t.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled || r is ParticleSystemRenderer) continue;
            if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
        }
        return any ? b.center : t.position;
    }

    static Vector3 RabbitCentre(RabbitLife r)
    {
        if (r == null) return Vector3.zero;
        var box = r.GetComponent<Collider>();
        return box != null && box.enabled ? box.bounds.center : Centre(r.transform);
    }

    static bool IsLand(Vector3 p)
    {
        float y = Forest.GroundY(p);
        if (Ocean.Instance != null && y < Ocean.Instance.Level + 0.8f) return false;
        return !River.IsWater(p, 1.5f);
    }

    static void Give(string item, int n) { for (int i = 0; i < n; i++) InventorySystem.Instance.AddToIventory(item, false, false); }

    static string HeldName()
    {
        var equip = EquipSystem.Instance;
        return equip != null && equip.selectedItem != null ? InventorySystem.CleanName(equip.selectedItem.name) : "";
    }

    static GameObject FindItem(string name)
    {
        foreach (var slot in InventorySystem.Instance.slotList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        foreach (var slot in EquipSystem.Instance.quickSlotsList)
            if (slot != null && slot.transform.childCount > 0 && InventorySystem.CleanName(slot.transform.GetChild(0).name) == name) return slot.transform.GetChild(0).gameObject;
        return null;
    }

    static void Invoke(object o, string method) { o.GetType().GetMethod(method, Any).Invoke(o, null); }

    void UseItem(GameObject item)
    {
        if (item == null) return;
        var info = item.GetComponent<InventoryItem>();
        if (info.isConsumable) { Invoke(info, "UseLikeRightClick"); Invoke(info, "FinishConsumption"); }
        else if (info.isUseable) { item.SetActive(false); Invoke(info, "UseItem"); }
        else if (info.isEquippable)
        {
            Invoke(info, "UseLikeRightClick");
            if (item.transform.parent != null) EquipSystem.Instance.SelectQuickSlotOf(item.transform.parent.gameObject);
        }
    }

    IEnumerator Hold(string name)
    {
        if (HeldName() == name) yield break;
        EmptyHands();
        var item = FindItem(name);
        if (item == null) yield break;
        var equip = EquipSystem.Instance;
        bool inQuick = item.transform.parent != null && equip.quickSlotsList.Contains(item.transform.parent.gameObject);
        if (!inQuick)
        {
            if (equip.CheckIfFull()) ClearQuickBar();
            Invoke(item.GetComponent<InventoryItem>(), "UseLikeRightClick");
        }
        if (HeldName() != name && item.transform.parent != null) equip.SelectQuickSlotOf(item.transform.parent.gameObject);
        yield return Frames(0.45f);
    }

    static void EmptyHands()
    {
        var equip = EquipSystem.Instance;
        if (equip == null || equip.selectedItem == null) return;
        if (equip.selectedItem.transform.parent != null) equip.SelectQuickSlotOf(equip.selectedItem.transform.parent.gameObject);
        if (equip.selectedItem != null)
        {
            var info = equip.selectedItem.GetComponent<InventoryItem>();
            if (info != null) info.isSelected = false;
            equip.selectedItem = null;
            equip.selectedNumber = -1;
            if (equip.selectedItemModel != null) { DestroyImmediate(equip.selectedItemModel); equip.selectedItemModel = null; }
            QuickBarUI.Changed();
        }
    }

    static void ClearQuickBar()
    {
        var equip = EquipSystem.Instance;
        foreach (var slot in equip.quickSlotsList)
        {
            if (slot == null || slot.transform.childCount == 0) continue;
            var item = slot.transform.GetChild(0).gameObject;
            if (equip.selectedItem == item) EmptyHands();
            DestroyImmediate(item);
        }
        InventorySystem.Instance.RecalculateList();
        QuickBarUI.Changed();
    }

    static bool UnderHolding(Transform t)
    {
        var placing = PlacementSystem.Instance;
        return placing != null && placing.placementHoldingSpot != null && t.IsChildOf(placing.placementHoldingSpot.transform);
    }

    static ForestTree FindTree(Vector3 near, float minHeight, float maxHeight, int species = -1)
    {
        var forest = Forest.Instance;
        if (forest == null) return null;
        ForestTree best = null;
        float bestD = float.MaxValue;
        foreach (var t in forest.Trees)
        {
            if (t == null || t.Falling || t.data == null || t.data.height < minHeight || t.data.height > maxHeight) continue;
            if (species >= 0 && t.data.species != species) continue;
            if (!IsLand(t.data.position)) continue;
            float d = Flat(t.data.position - near).magnitude;
            if (d < bestD) { bestD = d; best = t; }
        }
        return best;
    }

    // the nearest tree with no other tree within 'space' metres (species -1: any)
    static ForestTree FindLoneTree(Vector3 near, float minHeight, float maxHeight, int species, float space)
    {
        var forest = Forest.Instance;
        if (forest == null) return null;
        var candidates = new List<ForestTree>();
        foreach (var t in forest.Trees)
        {
            if (t == null || t.Falling || t.data == null || t.data.height < minHeight || t.data.height > maxHeight) continue;
            if (species >= 0 && t.data.species != species) continue;
            if (Flat(t.data.position - near).magnitude > 300f || !IsLand(t.data.position)) continue;
            candidates.Add(t);
        }
        candidates.Sort((a, b) => Flat(a.data.position - near).sqrMagnitude.CompareTo(Flat(b.data.position - near).sqrMagnitude));
        foreach (var t in candidates)
        {
            bool alone = true;
            foreach (var other in forest.Trees)
            {
                if (other == null || other == t || other.data == null) continue;
                if (Flat(other.data.position - t.data.position).magnitude < space) { alone = false; break; }
            }
            if (alone) return t;
        }
        return null;
    }

    static RabbitLife FindRabbit(Vector3 near, float radius)
    {
        var population = RabbitPopulation.Instance;
        if (population == null) return null;
        RabbitLife best = null;
        float bestD = radius;
        foreach (var r in population.Rabbits)
        {
            if (r == null || !r.Alive || !r.IsAdult || r.caughtIn != null || r.lure != null || RabbitPopulation.InPen(r)) continue;
            if (!r.gameObject.activeInHierarchy || !IsLand(r.transform.position)) continue;
            float d = Flat(r.transform.position - near).magnitude;
            if (d < bestD) { bestD = d; best = r; }
        }
        return best;
    }
}

// holds the left joystick for the director (after MobileControls sets it, before the player moves)
[DefaultExecutionOrder(-400)]
public class __VideoWalk : MonoBehaviour
{
    public static bool On;
    public static Vector2 Move;
    void Update() { if (On) GameInput.touchMove = Move; }
}
#endif
