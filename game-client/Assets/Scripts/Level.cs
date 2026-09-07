using UnityEngine;

// Abstract base for a playable level/setting (Courtyard, Open Field, etc.)
// Each concrete level overrides these to change difficulty, sound behavior,
// and environment setup. This is the polymorphism example from the spec:
// the same method calls behave differently depending on which Level is active.
public abstract class Level : MonoBehaviour
{
    public abstract void GenerateObstacles();
    public abstract float GetAmbientNoise();
    public abstract int GetDifficulty();
    public abstract void ConfigureEnvironment();

    // Lets each level swap in a different sound cue behavior -
    // e.g. a storm level can make sound cues less reliable.
    public abstract ISoundCueSystem GetSoundCueSystem();
}
