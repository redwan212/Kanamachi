using UnityEngine;

// Level 3: The Storm Night - dusk/night before the festival's final round.
// Sound cues become less reliable due to wind and rain (see StormSoundCueSystem).
public class StormNightLevel : Level
{
    public override void GenerateObstacles()
    {
        Debug.Log("[StormNightLevel] Obstacles: same layout as the fairground, now in darkness and rain.");
    }

    public override float GetAmbientNoise()
    {
        return 0.6f; // wind and rain add heavy ambient noise
    }

    public override int GetDifficulty()
    {
        return 3;
    }

    public override void ConfigureEnvironment()
    {
        Debug.Log("[StormNightLevel] Environment configured: dark, windy, rain approaching.");
    }

    public override ISoundCueSystem GetSoundCueSystem()
    {
        return new StormSoundCueSystem();
    }
}
