using UnityEngine;

// Unpredictable movement - simply relies on AIPlayer's default random
// wander behavior with no special logic layered on top.
public class RandomAI : AIPlayer
{
    // Intentionally empty - AIPlayer's base UpdateMovementInput() already
    // wanders randomly, which is exactly what this personality needs.
}
