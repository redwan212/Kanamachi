using UnityEngine;

public enum ControlScheme { WASD, Arrows }

// A human-controlled Player. Reads keyboard input directly.
// This replaces the old standalone PlayerController script.
public class HumanPlayer : Player
{
    [Header("Human Setup")]
    public ControlScheme controls = ControlScheme.WASD;

    protected override void UpdateMovementInput()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (controls == ControlScheme.WASD)
        {
            if (Input.GetKey(KeyCode.A)) horizontal = -1f;
            if (Input.GetKey(KeyCode.D)) horizontal = 1f;
            if (Input.GetKey(KeyCode.W)) vertical = 1f;
            if (Input.GetKey(KeyCode.S)) vertical = -1f;
        }
        else
        {
            if (Input.GetKey(KeyCode.LeftArrow)) horizontal = -1f;
            if (Input.GetKey(KeyCode.RightArrow)) horizontal = 1f;
            if (Input.GetKey(KeyCode.UpArrow)) vertical = 1f;
            if (Input.GetKey(KeyCode.DownArrow)) vertical = -1f;
        }

        moveInput = new Vector2(horizontal, vertical).normalized;
    }
}
