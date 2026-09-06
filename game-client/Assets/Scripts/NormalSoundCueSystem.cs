using UnityEngine;

// The standard sound cue behavior used in normal levels (e.g. the Courtyard, Open Field).
// A different level (like a future "Narrow Lane" dusk level) could implement
// ISoundCueSystem differently to make sound less reliable.
public class NormalSoundCueSystem : ISoundCueSystem
{
    public SoundCueLevel GetCueLevel(float distance)
    {
        if (distance <= 1.5f) return SoundCueLevel.VeryClose;
        if (distance <= 3f) return SoundCueLevel.Close;
        if (distance <= 5f) return SoundCueLevel.Normal;
        return SoundCueLevel.Quiet;
    }

    public float GetVolumeForLevel(SoundCueLevel level)
    {
        switch (level)
        {
            case SoundCueLevel.VeryClose: return 1f;
            case SoundCueLevel.Close: return 0.7f;
            case SoundCueLevel.Normal: return 0.4f;
            default: return 0.15f;
        }
    }

    public float GetPitchForLevel(SoundCueLevel level)
    {
        switch (level)
        {
            case SoundCueLevel.VeryClose: return 1.3f;
            case SoundCueLevel.Close: return 1.15f;
            case SoundCueLevel.Normal: return 1f;
            default: return 0.85f;
        }
    }
}
