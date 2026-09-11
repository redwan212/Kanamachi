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

    // Network players are created at runtime, so their Character arrives
    // after Awake() has already run. This lets it be applied afterwards.
    public void ApplyCharacter(Character newCharacter)
    {
        character = newCharacter;
        ApplyCharacterProfile();
    }

    // The blindfolded Kanamachi wears a red gamcha over the eyes, so the
    // other players can see at a glance who is "it".
    private void RefreshSprite()
    {
        if (character == null) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        Sprite wanted = isBlindfolded && character.blindfoldedSprite != null
                ? character.blindfoldedSprite
                : character.avatarSprite;

        if (wanted != null) sr.sprite = wanted;
    }

    protected void ApplyCharacterProfile()
    {
        if (character == null) return;

        moveSpeed *= character.moveSpeedMultiplier;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = character.spriteColor;
        }

        RefreshSprite();
    }

    void LateUpdate()
    {
        // Three-quarter view: whoever is lower on screen is nearer the
        // camera, so sorting follows the y position.
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f) + 1;
        }
    }

    void Update()
    {
        // Freeze movement while a guess is pending or the match is over.
        // In local mode that decision comes from GameManager; in an online
        // match it comes from NetworkGameManager.
        if (IsInputFrozen())
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

    private bool IsInputFrozen()
    {
        if (GameManager.Instance != null && GameManager.Instance.IsInputFrozen) return true;
        if (NetworkGameManager.Instance != null && NetworkGameManager.Instance.IsInputFrozen) return true;
        return false;
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
        RefreshSprite();
    }
}
