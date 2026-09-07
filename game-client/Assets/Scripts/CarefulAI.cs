using UnityEngine;

// Maintains a safe distance and avoids risky areas - similar to SneakyAI
// but reacts sooner, moves a bit slower, and prefers to stay still over
// wandering into danger.
public class CarefulAI : AIPlayer
{
    [Header("Careful Settings")]
    public float cautionDistance = 5f;
    public float moveSpeedMultiplier = 0.8f;

    protected override void Awake()
    {
        base.Awake();
        moveSpeed *= moveSpeedMultiplier;
    }

    protected override void UpdateMovementInput()
    {
        Player kanamachi = GameManager.Instance != null ? GameManager.Instance.GetKanamachiPlayer() : null;

        if (kanamachi == null || kanamachi == this)
        {
            moveInput = Vector2.zero; // stay put rather than wander into danger
            return;
        }

        float distance = Vector2.Distance(transform.position, kanamachi.transform.position);

        if (distance < cautionDistance)
        {
            Vector2 away = (Vector2)(transform.position - kanamachi.transform.position);
            moveInput = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.zero;
        }
        else
        {
            moveInput = Vector2.zero;
        }
    }
}
