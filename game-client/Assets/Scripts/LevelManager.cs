using UnityEngine;

// Owns the ordered list of levels and drives progression through them.
// Every level is asked - through the same abstract Level API - to configure
// itself, generate its obstacles, and supply its own sound cue behavior.
// This is where the polymorphism in the Level hierarchy actually gets used:
// LevelManager never knows or cares which concrete level it is holding.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance;

    [Header("All levels, in play order")]
    [Tooltip("Courtyard -> Mela Ground -> Storm Night -> Final Catch")]
    public Level[] levels;

    [Header("Optional")]
    [Tooltip("If assigned, the matching story chapter is shown when a level loads.")]
    public StoryManager storyManager;

    private int currentIndex = -1;

    public Level CurrentLevel { get; private set; }
    public int CurrentLevelIndex { get { return currentIndex; } }

    public bool HasNextLevel
    {
        get { return levels != null && currentIndex < levels.Length - 1; }
    }

    void Awake()
    {
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        if (levels == null || levels.Length == 0)
        {
            Debug.LogWarning("[LevelManager] No levels assigned in the Inspector.");
            return;
        }

        LoadLevel(0);
    }

    public void LoadLevel(int index)
    {
        if (levels == null || index < 0 || index >= levels.Length) return;
        if (levels[index] == null)
        {
            Debug.LogWarning($"[LevelManager] Level slot {index} is empty.");
            return;
        }

        currentIndex = index;
        CurrentLevel = levels[index];

        // The same three calls every time - different behavior per level.
        CurrentLevel.ConfigureEnvironment();
        CurrentLevel.GenerateObstacles();

        Debug.Log($"[LevelManager] Level {index + 1}/{levels.Length}: " +
                  $"{CurrentLevel.GetType().Name} | Ambient noise: {CurrentLevel.GetAmbientNoise()} " +
                  $"| Difficulty: {CurrentLevel.GetDifficulty()}");

        // Dress the arena for this level: ground, props, colliders, light.
        if (EnvironmentBuilder.Instance != null)
        {
            EnvironmentBuilder.Instance.Build(index, CurrentLevel.GetType().Name);
        }

        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.SetLevelAmbience(index);
        }

        // Let the sound system pick up this level's cue behavior
        // (Normal for most levels, distorted for the Storm Night).
        if (SoundCueManager.Instance != null)
        {
            SoundCueManager.Instance.RefreshSoundCueSystem();
        }

        // Show the story chapter that belongs to this level.
        if (storyManager != null)
        {
            storyManager.ShowChapter(index);
        }
    }

    // Returns false when the last level has already been played,
    // which tells the GameManager that the match is over.
    public bool AdvanceToNextLevel()
    {
        if (!HasNextLevel) return false;

        LoadLevel(currentIndex + 1);
        return true;
    }

    public string GetCurrentLevelName()
    {
        return CurrentLevel != null ? CurrentLevel.GetType().Name : "-";
    }
}
