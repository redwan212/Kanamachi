using UnityEngine;

// Plays a repeating "footstep" cue whose loudness, pitch, and tempo reflect
// how close the nearest non-Kanamachi player is.
public class SoundCueManager : MonoBehaviour
{
    [Header("Footstep Timing")]
    public float minInterval = 0.25f;
    public float maxInterval = 1.2f;

    [Header("Debug Display")]
    public bool showDebugLabel = true;

    private AudioSource audioSource;
    private ISoundCueSystem cueSystem;
    private float timer;
    private SoundCueLevel currentLevel = SoundCueLevel.Quiet;
    private Player closestPlayer;

    void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = GenerateFootstepClip();

        cueSystem = new NormalSoundCueSystem();
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        Player kanamachi = GameManager.Instance.GetKanamachiPlayer();
        if (kanamachi == null) return;

        float closestDistance = float.MaxValue;
        closestPlayer = null;
        foreach (var player in GameManager.Instance.players)
        {
            if (player == kanamachi) continue;
            float dist = Vector2.Distance(kanamachi.transform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player;
            }
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

        // Each character shifts the footstep pitch slightly. This is the
        // subtle identity clue from spec Section 18 - the Kanamachi can learn
        // to recognise who is nearby by how their footsteps sound.
        float pitchOffset = (closestPlayer != null && closestPlayer.character != null)
            ? closestPlayer.character.footstepPitchOffset
            : 0f;

        audioSource.pitch = cueSystem.GetPitchForLevel(currentLevel) + pitchOffset;
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

        // TESTING ONLY: shows which player is nearest and what pitch their
        // footstep is playing at, so the per-character pitch offset can be
        // verified. Turn "Show Debug Label" off in the Inspector before demoing -
        // it reveals the identity the Kanamachi is supposed to guess.
        string who = closestPlayer != null ? closestPlayer.DisplayName : "-";
        float offset = (closestPlayer != null && closestPlayer.character != null)
            ? closestPlayer.character.footstepPitchOffset
            : 0f;

        GUI.Label(new Rect(10, 40, 600, 30),
            $"Sound Cue: {currentLevel}  |  Nearest: {who}  |  Pitch: {audioSource.pitch:F2} (offset {offset:+0.00;-0.00;0.00})",
            style);
    }
}
