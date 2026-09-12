using UnityEngine;

// All sound in one place.
//
// The important part is the footstep. In this game hearing is the mechanic,
// not decoration: the blindfolded player is meant to work out where somebody
// is by listening. That only works if the sound carries direction, so
// footsteps are panned left or right by where the nearest player actually
// is, and pitched by which character it is.
//
// Clips are optional. When none are assigned the game falls back to a
// generated tone, so nothing breaks before the audio assets arrive.
public class GameAudio : MonoBehaviour
{
    public static GameAudio Instance;

    [Header("Clips (optional - a tone is generated when empty)")]
    public AudioClip footstepClip;
    public AudioClip clapClip;
    public AudioClip catchClip;
    public AudioClip correctGuessClip;
    public AudioClip wrongGuessClip;

    [Header("Ambience per level")]
    public AudioClip courtyardAmbience;
    public AudioClip melaAmbience;
    public AudioClip stormAmbience;
    public AudioClip finalAmbience;

    [Header("Mix")]
    [Range(0f, 1f)] public float ambienceVolume = 0.35f;
    [Range(0f, 1f)] public float effectsVolume = 0.9f;

    [Tooltip("How far to the side a player must be for the sound to be fully panned.")]
    public float panRange = 6f;

    [Header("Diagnostics")]
    [Tooltip("Logs the first few footsteps so a silent game can be told apart from a game that is not trying to play anything.")]
    public bool logFirstFootsteps = false;

    private int footstepsLogged;

    private AudioSource effectsSource;
    private AudioSource ambienceSource;
    private AudioClip generatedTone;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;

        ambienceSource = gameObject.AddComponent<AudioSource>();
        ambienceSource.playOnAwake = false;
        ambienceSource.loop = true;
        ambienceSource.volume = ambienceVolume;

        generatedTone = BuildTone();
    }

    // ---------- Footsteps ----------

    // listener is whoever is blindfolded; source is the player being heard.
    public void PlayFootstep(Vector2 listener, Vector2 source, float volume,
                             float pitch, float eventMultiplier)
    {
        AudioClip clip = footstepClip != null ? footstepClip : generatedTone;
        if (clip == null) return;

        // Negative is left, positive is right, from the listener's point of view.
        float pan = Mathf.Clamp((source.x - listener.x) / panRange, -1f, 1f);

        float finalVolume = Mathf.Clamp01(volume * eventMultiplier) * effectsVolume;

        effectsSource.panStereo = pan;
        effectsSource.pitch = Mathf.Clamp(pitch, 0.4f, 2.5f);
        effectsSource.PlayOneShot(clip, finalVolume);

        if (logFirstFootsteps && footstepsLogged < 5)
        {
            footstepsLogged++;
            Debug.Log($"[Audio] footstep {footstepsLogged}: volume={finalVolume:F2} " +
                      $"pan={pan:F2} pitch={effectsSource.pitch:F2} " +
                      $"clip={(footstepClip != null ? "custom" : "generated")}");
        }
    }

    // ---------- One-off effects ----------

    public void PlayClap(Vector2 listener, Vector2 source)
    {
        PlayPanned(clapClip, listener, source, 1f, 1f);
    }

    public void PlayCatch()
    {
        PlayCentred(catchClip, 1f, 0.9f);
    }

    public void PlayGuessResult(bool correct)
    {
        PlayCentred(correct ? correctGuessClip : wrongGuessClip, 1f, correct ? 1.2f : 0.7f);
    }

    private void PlayPanned(AudioClip clip, Vector2 listener, Vector2 source,
                            float volume, float pitch)
    {
        if (clip == null) clip = generatedTone;
        if (clip == null) return;

        effectsSource.panStereo = Mathf.Clamp((source.x - listener.x) / panRange, -1f, 1f);
        effectsSource.pitch = pitch;
        effectsSource.PlayOneShot(clip, volume * effectsVolume);
    }

    private void PlayCentred(AudioClip clip, float volume, float pitch)
    {
        if (clip == null) clip = generatedTone;
        if (clip == null) return;

        effectsSource.panStereo = 0f;
        effectsSource.pitch = pitch;
        effectsSource.PlayOneShot(clip, volume * effectsVolume);
    }

    // ---------- Ambience ----------

    public void SetLevelAmbience(int levelIndex)
    {
        AudioClip clip = levelIndex switch
        {
            0 => courtyardAmbience,
            1 => melaAmbience,
            2 => stormAmbience,
            _ => finalAmbience
        };

        if (clip == null)
        {
            ambienceSource.Stop();
            return;
        }

        if (ambienceSource.clip == clip && ambienceSource.isPlaying) return;

        ambienceSource.clip = clip;
        ambienceSource.volume = ambienceVolume;
        ambienceSource.Play();
    }

    // ---------- Fallback tone ----------

    // A short percussive blip, so footsteps are audible before any real
    // recordings are imported. Deliberately dull - it is a placeholder, and
    // its dullness is a reminder to replace it.
    private AudioClip BuildTone()
    {
        const int sampleRate = 44100;
        const float duration = 0.16f;

        int sampleCount = (int)(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 22f);

            // Two close frequencies plus a little noise reads as a footfall
            // rather than a beep.
            float body = Mathf.Sin(2f * Mathf.PI * 150f * t) * 0.8f
                       + Mathf.Sin(2f * Mathf.PI * 225f * t) * 0.35f;
            float grit = (Random.value * 2f - 1f) * 0.22f;

            samples[i] = Mathf.Clamp(body + grit, -1f, 1f) * envelope;
        }

        AudioClip clip = AudioClip.Create("FootstepTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
