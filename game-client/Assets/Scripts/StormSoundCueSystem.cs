using UnityEngine;

// Used during The Storm Night level - wind and rain make sound cues
// less reliable, per the spec's requirement that this level "modify
// the effectiveness of sound cues."
public class StormSoundCueSystem : ISoundCueSystem
{
    private readonly System.Random random = new System.Random();

    public SoundCueLevel GetCueLevel(float distance)
    {
        SoundCueLevel trueLevel;
        if (distance <= 1.5f) trueLevel = SoundCueLevel.VeryClose;
        else if (distance <= 3f) trueLevel = SoundCueLevel.Close;
        else if (distance <= 5f) trueLevel = SoundCueLevel.Normal;
        else trueLevel = SoundCueLevel.Quiet;

        // 30% chance the storm distorts the reading by one level in
        // either direction - the Kanamachi can no longer fully trust it.
        if (random.NextDouble() < 0.3)
        {
            return DistortByOneLevel(trueLevel);
        }

        return trueLevel;
    }

    private SoundCueLevel DistortByOneLevel(SoundCueLevel level)
    {
        int index = (int)level;
        int direction = random.Next(0, 2) == 0 ? -1 : 1;
        int distorted = Mathf.Clamp(index + direction, 0, 3);
        return (SoundCueLevel)distorted;
    }

    public float GetVolumeForLevel(SoundCueLevel level)
    {
        switch (level)
        {
            case SoundCueLevel.VeryClose: return 0.8f; // wind muffles even close sounds
            case SoundCueLevel.Close: return 0.55f;
            case SoundCueLevel.Normal: return 0.3f;
            default: return 0.1f;
        }
    }

    public float GetPitchForLevel(SoundCueLevel level)
    {
        switch (level)
        {
            case SoundCueLevel.VeryClose: return 1.2f;
            case SoundCueLevel.Close: return 1.05f;
            case SoundCueLevel.Normal: return 0.95f;
            default: return 0.8f;
        }
    }
}
