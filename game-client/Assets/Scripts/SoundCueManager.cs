using UnityEngine;

// Plays a repeating "footstep" cue whose loudness, pitch, and tempo reflect
// how close the nearest non-Kanamachi player is.
public class SoundCueManager : MonoBehaviour
{
    public static SoundCueManager Instance;

    [Header("Footstep Timing")]
    public float minInterval = 0.25f;
    public float maxInterval = 1.2f;

    [Header("Diagnostics")]
    [Tooltip("Logs why footsteps are or are not being produced.")]
    public bool logCueState = false;

    [Header("Debug Display")]
    public bool showDebugLabel = true;

    private AudioSource audioSource;
    private ISoundCueSystem cueSystem;
    private float timer;
    private SoundCueLevel currentLevel = SoundCueLevel.Quiet;
    private Player closestPlayer;
    private float nextLogTime;

    // Where each player was last frame, so a footstep only plays for
    // somebody who is actually walking.
    private readonly System.Collections.Generic.Dictionary<Player, Vector2> lastPositions
            = new System.Collections.Generic.Dictionary<Player, Vector2>();

    // Random events (thunder, crowd noise, firecrackers) temporarily scale
    // how loud the footstep cues come through. 1 = no event active.
    public float EventVolumeMultiplier { get; set; } = 1f;

    void Awake()
    {
        if (Instance == null) Instance = this;

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = GenerateFootstepClip();

        // Fallback until LevelManager loads a level and calls
        // RefreshSoundCueSystem() with that level's own behavior.
        cueSystem = new NormalSoundCueSystem();
    }

    // Asks the active Level which sound cue behavior to use. The Storm Night
    // level returns a StormSoundCueSystem, which makes cues unreliable;
    // every other level returns the normal one. Called by LevelManager
    // whenever a level loads.
    public void RefreshSoundCueSystem()
    {
        Level level = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevel : null;

        cueSystem = level != null ? level.GetSoundCueSystem() : new NormalSoundCueSystem();

        Debug.Log($"[SoundCueManager] Sound cue behavior is now {cueSystem.GetType().Name}.");
    }

    void Update()
    {
        // Works in both modes: locally the GameManager owns the players, and
        // in an online match the NetworkGameManager does.
        Player kanamachi = GetKanamachi();

        // Reports why no footsteps are being produced, rather than leaving
        // silence to be guessed at.
        if (logCueState && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + 2f;

            bool hasNetwork = NetworkGameManager.Instance != null;
            Debug.Log($"[Cue] kanamachi={(kanamachi != null ? kanamachi.DisplayName : "null")} " +
                      $"network={hasNetwork} " +
                      $"localIsKanamachi={(hasNetwork && NetworkGameManager.Instance.LocalPlayerIsKanamachi)} " +
                      $"gameManager={(GameManager.Instance != null)} " +
                      $"audio={(GameAudio.Instance != null)} " +
                      $"players={CountPlayers()}");
        }

        if (kanamachi == null) return;

        // Online, the cues are the blindfolded player's own aid - there is no
        // reason for anyone else's machine to play them.
        if (IsOnline && !NetworkGameManager.Instance.LocalPlayerIsKanamachi) return;

        float closestDistance = float.MaxValue;
        closestPlayer = null;

        foreach (var player in GetPlayers())
        {
            if (player == kanamachi || player == null) continue;

            // Somebody standing still makes no sound. Only moving players
            // are candidates, which is what makes staying put a real choice
            // rather than a pointless one.
            if (!IsMoving(player)) continue;

            float dist = Vector2.Distance(kanamachi.transform.position, player.transform.position);
            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPlayer = player;
            }
        }

        // Nobody is walking, so there is nothing to hear.
        if (closestPlayer == null)
        {
            currentLevel = SoundCueLevel.Quiet;
            return;
        }

        currentLevel = cueSystem.GetCueLevel(closestDistance);

        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            PlayFootstep();
            timer = GetIntervalForLevel(currentLevel);
        }
    }

    private int CountPlayers()
    {
        int count = 0;
        foreach (var p in GetPlayers()) count++;
        return count;
    }

    // True while an online match owns the game state.
    private bool IsOnline
    {
        get { return NetworkGameManager.Instance != null; }
    }

    [Header("Movement")]
    [Tooltip("How far a player must move in a frame to count as walking.")]
    public float movementThreshold = 0.012f;

    private bool IsMoving(Player player)
    {
        Vector2 position = player.transform.position;

        if (!lastPositions.TryGetValue(player, out Vector2 previous))
        {
            lastPositions[player] = position;
            return false;
        }

        lastPositions[player] = position;

        return Vector2.Distance(position, previous) > movementThreshold;
    }

    private Player GetKanamachi()
    {
        // Online wins. The local GameManager's static reference can survive
        // from an earlier session even with its component switched off, and
        // asking it first left the cue system looking at an empty match.
        if (IsOnline) return NetworkGameManager.Instance.KanamachiPlayer;
        if (GameManager.Instance != null) return GameManager.Instance.GetKanamachiPlayer();
        return null;
    }

    private System.Collections.Generic.IEnumerable<Player> GetPlayers()
    {
        if (IsOnline) return NetworkGameManager.Instance.AllPlayers;
        if (GameManager.Instance != null) return GameManager.Instance.players;
        return new Player[0];
    }

    private void PlayFootstep()
    {
        float volume = cueSystem.GetVolumeForLevel(currentLevel);

        // Each character shifts the footstep pitch slightly - the subtle
        // identity clue from spec Section 18.
        float pitchOffset = (closestPlayer != null && closestPlayer.character != null)
            ? closestPlayer.character.footstepPitchOffset
            : 0f;

        float pitch = cueSystem.GetPitchForLevel(currentLevel) + pitchOffset;

        // GameAudio does the panning, so the blindfolded player can tell
        // which side the footsteps are coming from.
        if (GameAudio.Instance != null)
        {
            Player kanamachi = GetKanamachi();

            Vector2 listener = kanamachi != null ? (Vector2)kanamachi.transform.position : Vector2.zero;
            Vector2 source = closestPlayer != null ? (Vector2)closestPlayer.transform.position : listener;

            GameAudio.Instance.PlayFootstep(listener, source, volume, pitch, EventVolumeMultiplier);
            return;
        }

        // Fallback for the local scene, which has its own AudioSource.
        audioSource.volume = Mathf.Clamp01(volume * EventVolumeMultiplier);
        audioSource.pitch = pitch;
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

        string systemName = cueSystem != null ? cueSystem.GetType().Name : "-";

        GUI.Label(new Rect(10, 40, 800, 30),
            $"Sound Cue: {currentLevel}  |  Nearest: {who}  |  Pitch: {audioSource.pitch:F2} " +
            $"(offset {offset:+0.00;-0.00;0.00})  |  {systemName}  |  Event x{EventVolumeMultiplier:F2}",
            style);
    }
}
