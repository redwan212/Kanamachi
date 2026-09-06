using UnityEngine;

// The first, simplest level - a quiet home courtyard.
// Obstacles for this level are placed by hand in the Editor already
// (Ground + 4 walls), so GenerateObstacles() just confirms that.
public class CourtyardLevel : Level
{
    public override void GenerateObstacles()
    {
        Debug.Log("[CourtyardLevel] Obstacles: Ground + 4 boundary walls (placed manually in Editor).");
    }

    public override float GetAmbientNoise()
    {
        return 0.1f; // very quiet - easiest level to hear footsteps in
    }

    public override int GetDifficulty()
    {
        return 1;
    }

    public override void ConfigureEnvironment()
    {
        Debug.Log("[CourtyardLevel] Environment configured: calm afternoon courtyard.");
    }
}
