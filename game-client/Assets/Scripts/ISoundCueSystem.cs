// Defines how "close" a sound cue feels based on distance.
// Different levels (e.g. a storm level) can implement this differently
// by creating another class that implements ISoundCueSystem.
public enum SoundCueLevel { Quiet, Normal, Close, VeryClose }

public interface ISoundCueSystem
{
    SoundCueLevel GetCueLevel(float distance);
    float GetVolumeForLevel(SoundCueLevel level);
    float GetPitchForLevel(SoundCueLevel level);
}
