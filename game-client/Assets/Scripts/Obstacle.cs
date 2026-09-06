using UnityEngine;

// Abstract base for anything in the arena that affects movement or sound
// without being a player - walls, trees, stalls, benches, etc.
public abstract class Obstacle : MonoBehaviour
{
    [Header("Obstacle Properties")]
    public bool blocksMovement = true;

    // How much ambient sound this obstacle contributes (0 = silent).
    // Some obstacles (e.g. a busy stall) might make it harder to hear footsteps.
    public abstract float GetAmbientSoundLevel();
}
