using UnityEngine;

// A solid boundary obstacle. Physically blocks movement via its Collider2D
// (already set up in the Editor) - this class exists mainly to fit the
// Obstacle hierarchy and mark walls as fully silent and fully blocking.
public class Wall : Obstacle
{
    public override float GetAmbientSoundLevel()
    {
        return 0f;
    }
}
