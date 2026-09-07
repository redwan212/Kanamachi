using UnityEngine;

// A character's cosmetic and personality profile. Differences here are
// kept small and are meant only to add personality and replayability -
// not to create unfair advantages between characters (per spec Section 19).
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Kanamachi/Character")]
public class Character : ScriptableObject
{
    [Header("Identity")]
    public string characterName;

    [TextArea(2, 4)]
    public string description;

    public Sprite avatarSprite;
    public Color spriteColor = Color.white;

    [Header("Movement Profile")]
    [Tooltip("Small multiplier only - keep close to 1 so no character is unfairly fast or slow.")]
    [Range(0.9f, 1.1f)]
    public float moveSpeedMultiplier = 1f;

    [Header("Sound Profile")]
    [Tooltip("Slight pitch shift on this character's clap/footstep sounds - a subtle identity clue during guessing.")]
    [Range(-0.2f, 0.2f)]
    public float footstepPitchOffset = 0f;

    [Header("Personality")]
    [TextArea(2, 4)]
    public string personality;
}
