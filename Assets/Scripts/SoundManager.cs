using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Sound effects of the game. The AudioSources are on the SoundManager object of the scene; their sounds are
// free CC0 sounds loaded from Assets/Resources/Sounds (see Assets/Sound/SOUND_CREDITS.txt), a different one
// each time when there are several. The background music was removed (its licence was unknown): the forest
// sounds (AmbienceAudio) play instead.
public class SoundManager : MonoBehaviour
{
    //Sound efect
    public static SoundManager Instance { get; set; }
    public AudioSource dropItemSound;
    public AudioSource craftingSound;
    public AudioSource toolSwingSound;
    public AudioSource chopSound;
    public AudioSource pickupItemSound;
    public AudioSource grassWalkSound; // not used any more: FootstepAudio plays the steps

    //music (removed - kept only so the scene's link to it stays valid)
    public AudioSource startingZoneBgMusic;

    // volumes from Settings (0..1): Effects and Music (the music slider now sets the forest sounds)
    public static float EffectsVolume = 1f;
    public static float AmbienceVolume = 1f;

    readonly Dictionary<AudioSource, AudioClip[]> variants = new Dictionary<AudioSource, AudioClip[]>();
    readonly Dictionary<AudioSource, float> baseVolume = new Dictionary<AudioSource, float>();
    AudioClip[] chopClips, craftClips;
    AudioSource oneShots;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        StopMusic();
        LoadVolumeSettings();

        chopClips = Load("Chop");
        craftClips = Load("Craft");
        Use(pickupItemSound, Load("UI/Pickup"), 0.55f);
        Use(dropItemSound, Load("UI/Drop"), 0.6f);
        Use(chopSound, chopClips, 0.9f);
        Use(craftingSound, craftClips, 0.7f);
        if (toolSwingSound != null) baseVolume[toolSwingSound] = toolSwingSound.volume;

        oneShots = gameObject.AddComponent<AudioSource>();
        oneShots.playOnAwake = false;
        oneShots.spatialBlend = 0f;
    }

    void Start()
    {
        StopMusic(); // in case its AudioSource started on its own before Awake
    }

    void StopMusic()
    {
        if (startingZoneBgMusic == null) return;
        startingZoneBgMusic.Stop();
        startingZoneBgMusic.playOnAwake = false;
        startingZoneBgMusic.enabled = false;
    }

    // Master -> the whole game, Effects -> the sound effects, Music -> the forest sounds (0-100 in the menu)
    public static void LoadVolumeSettings()
    {
        string json = PlayerPrefs.GetString("Volume", "");
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            var settings = JsonUtility.FromJson<SaveManager.VolumeSettings>(json);
            if (settings == null) return;
            AudioListener.volume = Mathf.Clamp01(settings.master / 100f);
            EffectsVolume = Mathf.Clamp01(settings.effects / 100f);
            AmbienceVolume = Mathf.Clamp01(settings.music / 100f);
        }
        catch (System.Exception) { }
    }

    static AudioClip[] Load(string folder)
    {
        var clips = Resources.LoadAll<AudioClip>("Sounds/" + folder);
        if (clips.Length == 0) Debug.LogWarning("No sounds in Resources/Sounds/" + folder);
        return clips;
    }

    void Use(AudioSource source, AudioClip[] clips, float volume)
    {
        if (source == null) return;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        if (clips != null && clips.Length > 0)
        {
            variants[source] = clips;
            source.clip = clips[0];
        }
        baseVolume[source] = volume;
    }

    public void PlaySound(AudioSource soundToPlay)
    {
        if (soundToPlay == null || soundToPlay == grassWalkSound) return;
        if (soundToPlay.isPlaying) return;
        if (variants.TryGetValue(soundToPlay, out var clips) && clips.Length > 0)
        {
            soundToPlay.clip = Pick(clips);
            soundToPlay.pitch = Random.Range(0.94f, 1.06f);
        }
        float volume = baseVolume.TryGetValue(soundToPlay, out var v) ? v : soundToPlay.volume;
        soundToPlay.volume = volume * EffectsVolume;
        soundToPlay.Play();
    }

    // a sound that may overlap others (2D)
    public void PlayOneShot(AudioClip[] clips, float volume, float pitchJitter = 0.06f)
    {
        if (clips == null || clips.Length == 0 || oneShots == null) return;
        oneShots.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
        oneShots.PlayOneShot(Pick(clips), volume * EffectsVolume);
    }

    // a sound at a place in the world (3D: quieter far away)
    public void PlayAt(AudioClip clip, Vector3 position, float volume, float pitch = 1f, float maxDistance = 60f, float minDistance = 4f)
    {
        if (clip == null) return;
        var go = new GameObject("Sound " + clip.name);
        go.transform.position = position;
        var source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = volume * EffectsVolume;
        source.pitch = pitch;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.Play();
        Destroy(go, clip.length / Mathf.Max(0.1f, pitch) + 0.2f);
    }

    // the axe hits a tree
    public void PlayChop()
    {
        PlayOneShot(chopClips, 0.85f, 0.08f);
    }

    // crafting: three quick knocks of wood and stone
    public void PlayCraft()
    {
        if (craftClips == null || craftClips.Length == 0) return;
        StartCoroutine(CraftKnocks());
    }

    IEnumerator CraftKnocks()
    {
        for (int i = 0; i < 3; i++)
        {
            PlayOneShot(craftClips, 0.6f, 0.1f);
            yield return new WaitForSeconds(Random.Range(0.13f, 0.2f));
        }
    }

    // the axe (or another tool) cuts through the air: on every swing, also when it hits nothing
    public void PlaySwing()
    {
        PlayOneShot(ProceduralAudio.Swooshes, 0.55f, 0.1f);
    }

    // a tree comes down: the trunk cracks, then it hits the ground
    public void PlayTreeFall(Vector3 position)
    {
        StartCoroutine(TreeFall(position));
    }

    IEnumerator TreeFall(Vector3 position)
    {
        PlayTreeCrack(position + Vector3.up * 2f, 1f);
        yield return new WaitForSeconds(1.1f);
        PlayTreeImpact(position, 0.8f);
    }

    // the trunk cracking as a tree starts to fall (loudness 0..1: big trees louder) - sounds made in code
    public void PlayTreeCrack(Vector3 position, float loudness)
    {
        PlayAt(ProceduralAudio.TreeCrack, position, 0.85f * loudness, Random.Range(0.92f, 1.06f), 110f, 8f);
    }

    // the crown rushing through the air just before it lands
    public void PlayBranchRush(Vector3 position, float loudness)
    {
        PlayAt(ProceduralAudio.BranchRush, position, 0.55f * loudness, Random.Range(0.94f, 1.06f), 90f, 8f);
    }

    // the fallen tree hits the ground
    public void PlayTreeImpact(Vector3 position, float loudness)
    {
        PlayAt(ProceduralAudio.TreeImpact, position, 1f * loudness, Random.Range(0.9f, 1.04f), 160f, 12f);
    }

    static int lastPick = -1;
    static AudioClip Pick(AudioClip[] clips)
    {
        if (clips.Length == 1) return clips[0];
        int i = Random.Range(0, clips.Length);
        if (i == lastPick) i = (i + 1) % clips.Length; // not the same one twice in a row
        lastPick = i;
        return clips[i];
    }
}
