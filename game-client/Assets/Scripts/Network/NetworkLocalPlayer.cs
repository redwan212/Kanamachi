using UnityEngine;

// The player sitting at this keyboard, in an online match.
//
// It behaves like HumanPlayer with one addition: after moving, it reports its
// position to the server. Everyone else's copy of this player is a
// NetworkRemotePlayer being driven by those reports.
public class NetworkLocalPlayer : Player
{
    // Accepts both control schemes, since online there is only one player
    // per keyboard and no reason to split the keys.
    protected override void UpdateMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) vertical = -1f;

        moveInput = new Vector2(horizontal, vertical).normalized;
    }

    protected override void FixedUpdate()
    {
        base.FixedUpdate();

        // NetworkClient throttles this internally, so calling it every
        // physics step is fine.
        if (NetworkClient.Instance != null)
        {
            NetworkClient.Instance.SendPosition(rb.position);
        }
    }
}
