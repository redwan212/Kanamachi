using UnityEngine;

// Avoids making noise and keeps distance from the Kanamachi.
// Never claps/teases - purely evasive.
public class SneakyAI : AIPlayer
{
    [Header("Sneaky Settings")]
    public float safeDistance = 4f;

    protected override void UpdateMovementInput()
    {
        Player kanamachi = GameManager.Instance != null ? GameManager.Instance.GetKanamachiPlayer() : null;

        if (kanamachi == null || kanamachi == this)
        {
            base.UpdateMovementInput();
            return;
        }

        float distance = Vector2.Distance(transform.position, kanamachi.transform.position);

        if (distance < safeDistance)
        {
            // Too close for comfort - move directly away.
            Vector2 away = (Vector2)(transform.position - kanamachi.transform.position);
            moveInput = away.sqrMagnitude > 0.01f ? away.normalized : Vector2.zero;
        }
        else
        {
            // Already at a safe distance - wander quietly instead of standing still.
            base.UpdateMovementInput();
        }
    }
}
