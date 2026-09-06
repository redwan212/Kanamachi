using UnityEngine;

// Plays a repeating "footstep" cue whose loudness, pitch, and tempo reflect
// how close the nearest non-Kanamachi player is. This is the Kanamachi's
// main way of sensing others while blindfolded.
public class SoundCueManager : MonoBehaviour
{
    [Header("Footstep Timing")]
    public float minInterval = 0.25f; // fastest tempo (very close)
    public float maxInterval = 1.2f;  // slowest tempo (quiet/far)

    [Header("Debug Display")]
    public bool showDebugLabel = true;

    private AudioSource audioSource;
    private ISoundCueSystem cueSystem;
    private float timer;
    private SoundCueLevel currentLevel = SoundCueLevel.Quiet;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // fully 2D, represents what the Kanamachi "hears"
        audioSource.clip = GenerateFootstepClip();

        cueSystem = new NormalSoundCueSystem();
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        GameObject kanamachi = GameManager.Instance.GetKanamachiPlayer();
        if (kanamachi == null) return;

        float closestDistance = float.MaxValue;
        foreach (var player in GameManager.Instance.players)
        {
            if (player == kanamachi) continue;
            float dist = Vector2.Distance(kanamachi.transform.position, player.transform.position);
            if (dist < closestDistance) closestDistance = dist;
        }

        currentLevel = cueSystem.GetCueLevel(closestDistance);

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PlayFootstep();
            timer = GetIntervalForLevel(currentLevel);
        }
    }

    private void PlayFootstep()
    {
        audioSource.volume = cueSystem.GetVolumeForLevel(currentLevel);
        audioSource.pitch = cueSystem.GetPitchForLevel(currentLevel);
        audioSource.PlayOneShot(audioSource.clip);
    }

    private float GetIntervalForLevel(SoundCueLevel level)
    {
        switch (level)
        {
            case SoundCueLevel.VeryClose: return minInterval;
            case SoundCueLevel.Close: return Mathf.Lerp(minInterval, maxInterval, 0.3f);
            case SoundCueLevel.Normal: return Mathf.Lerp(minInterval, maxInterval, 0.6f);
            default: return maxInterval;
        }
    }

    // Generates a short soft "thump" tone procedurally, so no external audio file is needed.
    private AudioClip GenerateFootstepClip()
    {
        int sampleRate = 44100;
        float duration = 0.12f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 30f);
            float tone = Mathf.Sin(2f * Mathf.PI * 90f * t);
            samples[i] = tone * envelope;
        }

        AudioClip clip = AudioClip.Create("FootstepTone", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    void OnGUI()
    {
        if (!showDebugLabel) return;

        GUIStyle style = new GUIStyle();
        style.fontSize = 18;
        style.normal.textColor = Color.cyan;
        GUI.Label(new Rect(10, 40, 400, 30), $"Sound Cue: {currentLevel}", style);
    }
}
