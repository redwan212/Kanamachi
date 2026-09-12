using UnityEngine;

// The player sitting at this keyboard, in an online match.
//
// It behaves like HumanPlayer with one addition: after moving, it reports its
// position to the server. Everyone else's copy of this player is a
// NetworkRemotePlayer being driven by those reports.
public class NetworkLocalPlayer : Player
{
    [Header("Tease")]
    [Tooltip("Clapping taunts the blindfolded player - and tells them exactly where you are.")]
    public KeyCode clapKey = KeyCode.C;
    public float clapCooldown = 1.5f;

    private float clapReadyAt;

    // Accepts both control schemes, since online there is only one player
    // per keyboard and no reason to split the keys.
    protected override void UpdateMovementInput()
    {
        // Handled here rather than in an Update of its own: Player.Update is
        // not virtual, so declaring one in this class would hide it and stop
        // the player moving at all.
        CheckClap();

        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;

        moveInput = new Vector2(horizontal, vertical).normalized;
    }

    // Clapping is the one way a sighted player gives themselves away on
    // purpose. It is only worth doing because the blindfolded player cannot
    // tell teasing from carelessness.
    private void CheckClap()
    {
        if (IsBlindfolded()) return;
        if (Time.time < clapReadyAt) return;
        if (!Input.GetKeyDown(clapKey)) return;

        clapReadyAt = Time.time + clapCooldown;

        if (NetworkClient.Instance != null) NetworkClient.Instance.SendClap();

        // The server does not echo a clap back to whoever made it, so
        // without this the person clapping hears nothing and assumes the
        // key is broken.
        if (GameAudio.Instance != null)
        {
            GameAudio.Instance.PlayClap(rb.position, rb.position);
        }
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        ClampToArena();

        // NetworkClient throttles this internally, so calling it every
        // physics step is fine.
        if (NetworkClient.Instance != null)
        {
            NetworkClient.Instance.SendPosition(rb.position);
        }
    }

    // The walls should stop anyone leaving, but a missing collider or a
    // fast diagonal can slip through. Keeping the player inside here means
    // a scene mistake never strands somebody outside the courtyard.
    private void ClampToArena()
    {
        if (EnvironmentBuilder.Instance == null) return;

        Vector2 limit = EnvironmentBuilder.Instance.arenaHalfSize - new Vector2(0.4f, 0.4f);
        Vector2 position = rb.position;

        float x = Mathf.Clamp(position.x, -limit.x, limit.x);
        float y = Mathf.Clamp(position.y, -limit.y, limit.y);

        if (!Mathf.Approximately(x, position.x) || !Mathf.Approximately(y, position.y))
        {
            rb.position = new Vector2(x, y);
        }
    }
}
