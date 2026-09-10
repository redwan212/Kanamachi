using UnityEngine;

// Another person's player, mirrored on this machine.
//
// It takes no input of its own. The server sends roughly ten position
// updates a second, and this class eases toward the newest one so the
// movement looks continuous instead of teleporting between packets.
public class NetworkRemotePlayer : Player
{
    [Header("Interpolation")]
    [Tooltip("Higher catches up to the server position faster but looks jumpier.")]
    public float smoothing = 12f;

    private Vector2 targetPosition;
    private bool hasTarget;

    public string UserId { get; set; }
    public string Username { get; set; }

    // Called by NetworkGameManager whenever a PLAYER_MOVED message arrives.
    public void SetTargetPosition(Vector2 position)
    {
        targetPosition = position;
        hasTarget = true;
    }

    // Remote players never read input - their movement comes from the server.
    protected override void UpdateMovementInput()
    {
        moveInput = Vector2.zero;
    }

    protected override void FixedUpdate()
    {
        if (!hasTarget) return;

        // Exponential easing, so the step size does not depend on framerate.
        float t = 1f - Mathf.Exp(-smoothing * Time.fixedDeltaTime);
        rb.MovePosition(Vector2.Lerp(rb.position, targetPosition, t));
    }
}
