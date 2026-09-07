using UnityEngine;

// Moves closer to the Kanamachi and teases (claps) frequently -
// a high-risk, high-reward personality.
[RequireComponent(typeof(ClapController))]
public class AggressiveAI : AIPlayer
{
    [Header("Aggressive Settings")]
    public float teaseInterval = 2.5f;

    private ClapController clapController;
    private float teaseTimer;

    protected override void Awake()
    {
        base.Awake();
        clapController = GetComponent<ClapController>();
    }

    protected override void UpdateMovementInput()
    {
        Player kanamachi = GameManager.Instance != null ? GameManager.Instance.GetKanamachiPlayer() : null;

        if (kanamachi == null || kanamachi == this)
        {
            base.UpdateMovementInput(); // fall back to wandering
            return;
        }

        Vector2 direction = (Vector2)(kanamachi.transform.position - transform.position);
        moveInput = direction.sqrMagnitude > 0.01f ? direction.normalized : Vector2.zero;

        teaseTimer -= Time.deltaTime;
        if (teaseTimer <= 0f)
        {
            clapController.PerformClap();
            teaseTimer = teaseInterval;
        }
    }
}
