using UnityEngine;

// A basic AI-controlled Player (random wandering for now).
// Specific personalities (AggressiveAI, SneakyAI, CarefulAI, RandomAI)
// will extend this further in Phase 6 (Content).
public class AIPlayer : Player
{
    [Header("AI Setup")]
    public float directionChangeInterval = 2f;

    private float timer;

    protected override void UpdateMovementInput()
    {
        timer -= Time.deltaTime;
        if (timer <= 0f)
        {
            moveInput = Random.insideUnitCircle.normalized;
            timer = directionChangeInterval;
        }
    }
}
