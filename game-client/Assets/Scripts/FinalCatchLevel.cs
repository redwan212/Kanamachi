using UnityEngine;

// Level 4: The Final Catch - the whole village, all remaining players,
// hardest round, decides the winner.
public class FinalCatchLevel : Level
{
    public override void GenerateObstacles()
    {
        Debug.Log("[FinalCatchLevel] Obstacles: full village layout - courtyard, fairground, and lanes combined.");
    }

    public override float GetAmbientNoise()
    {
        return 0.5f; // whole village watching - crowd noise, but calmer than the storm
    }

    public override int GetDifficulty()
    {
        return 4;
    }

    public override void ConfigureEnvironment()
    {
        Debug.Log("[FinalCatchLevel] Environment configured: the whole village gathers for the final round.");
    }

    public override ISoundCueSystem GetSoundCueSystem()
    {
        return new NormalSoundCueSystem();
    }
}
