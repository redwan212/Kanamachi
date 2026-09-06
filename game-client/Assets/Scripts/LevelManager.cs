using UnityEngine;

// Holds a reference to whichever Level is currently active and calls its
// methods. This is the polymorphism demonstration: the same method calls
// (ConfigureEnvironment, GenerateObstacles, GetAmbientNoise, GetDifficulty)
// will behave differently once more Level subclasses exist (Phase 6).
public class LevelManager : MonoBehaviour
{
    [Header("Assign the active Level component here")]
    public Level currentLevel;

    void Start()
    {
        if (currentLevel == null)
        {
            Debug.LogWarning("[LevelManager] No Level assigned.");
            return;
        }

        currentLevel.ConfigureEnvironment();
        currentLevel.GenerateObstacles();

        Debug.Log($"[LevelManager] Ambient noise: {currentLevel.GetAmbientNoise()}, Difficulty: {currentLevel.GetDifficulty()}");
    }
}
