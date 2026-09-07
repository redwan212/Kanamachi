using UnityEngine;

// Abstract base for anything that can move around the arena as a "player"
// in the Kanamachi game - either a human-controlled character or an AI one.
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Player : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 4f;

    [Header("Character (optional)")]
    public Character character;

    protected Rigidbody2D rb;
    protected Vector2 moveInput;

    private bool isBlindfolded;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        ApplyCharacterProfile();
    }

    // The name shown in the UI, logs, and the guessing screen.
    // Falls back to the GameObject name so a Player without an assigned
    // Character still displays something sensible instead of blank text.
    public string DisplayName
    {
        get
        {
            if (character != null && !string.IsNullOrEmpty(character.characterName))
                return character.characterName;
            return gameObject.name;
        }
    }

    private void ApplyCharacterProfile()
    {
        if (character == null) return;

        moveSpeed *= character.moveSpeedMultiplier;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = character.spriteColor;
            if (character.avatarSprite != null)
            {
                sr.sprite = character.avatarSprite;
            }
        }
    }

    void Update()
    {
        // Freeze movement while the Kanamachi is guessing who they caught.
        if (GameManager.Instance != null && GameManager.Instance.IsGuessingPhase)
        {
            moveInput = Vector2.zero;
            return;
        }

        UpdateMovementInput();
    }

    protected virtual void FixedUpdate()
    {
        rb.MovePosition(rb.position + moveInput * moveSpeed * Time.fixedDeltaTime);
    }

    // Each concrete player type decides HOW moveInput gets set:
    // HumanPlayer reads the keyboard, AIPlayer runs its own decision logic.
    protected abstract void UpdateMovementInput();

    // Encapsulation: internal state kept private, exposed only through methods.
    public bool IsBlindfolded()
    {
        return isBlindfolded;
    }

    public void SetBlindfolded(bool value)
    {
        isBlindfolded = value;
    }
}
