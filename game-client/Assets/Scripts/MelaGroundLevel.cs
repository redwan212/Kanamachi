using UnityEngine;

// Level 2: The Mela Ground - a Bangladeshi village fair.
// More players, more obstacles (stalls), and busier ambient sound
// than the quiet Courtyard.
public class MelaGroundLevel : Level
{
    public override void GenerateObstacles()
    {
        Debug.Log("[MelaGroundLevel] Obstacles: mela stalls, carts, and crowd clusters.");
    }

    public override float GetAmbientNoise()
    {
        return 0.4f; // busier fairground - harder to isolate footsteps
    }

    public override int GetDifficulty()
    {
        return 2;
    }

    public override void ConfigureEnvironment()
    {
        Debug.Log("[MelaGroundLevel] Environment configured: bustling village fair.");
    }

    public override ISoundCueSystem GetSoundCueSystem()
    {
        return new NormalSoundCueSystem();
    }
}
