using UnityEngine;

// Gives a static sprite the appearance of being alive.
//
// There is no animation asset here and no spritesheet. Everything is done by
// scaling and offsetting the existing sprite, which costs nothing and works
// with whatever art is imported later. A character that squashes as it runs
// and breathes while it waits reads as hand-made; the same sprite sliding
// flatly across the ground reads as a placeholder.
//
// Added at runtime by the spawning code, so no prefab setup is needed.
public class PlayerAnimator : MonoBehaviour
{
    [Header("Idle")]
    public float breathSpeed = 2.4f;
    public float breathAmount = 0.035f;

    [Header("Walking")]
    public float bobSpeed = 11f;
    public float bobHeight = 0.07f;
    public float squashAmount = 0.09f;
    public float leanAmount = 4f;

    [Header("Dust")]
    public Sprite dustSprite;
    public float dustInterval = 0.22f;

    private SpriteRenderer spriteRenderer;
    private Transform visual;
    private Vector3 baseScale;

    private Vector2 lastPosition;
    private float phase;
    private float dustTimer;
    private bool facingLeft;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // The sprite is moved and scaled, while the parent keeps the true
        // position that physics and the network care about.
        visual = transform;
        baseScale = transform.localScale;

        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        Vector2 position = transform.position;
        Vector2 delta = position - lastPosition;
        lastPosition = position;

        float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        bool moving = speed > 0.35f;

        if (moving)
        {
            AnimateWalk(delta, speed);
            EmitDust();
        }
        else
        {
            AnimateIdle();
        }
    }

    private void AnimateWalk(Vector2 delta, float speed)
    {
        phase += Time.deltaTime * bobSpeed;

        // One squash per step: widest at the bottom of the bob.
        float wave = Mathf.Sin(phase);
        float squash = wave * squashAmount;

        visual.localScale = new Vector3(
                baseScale.x * (1f + squash) * (facingLeft ? -1f : 1f),
                baseScale.y * (1f - squash),
                baseScale.z);

        // A slight lean in the direction of travel.
        float lean = Mathf.Clamp(-delta.x * 140f, -leanAmount, leanAmount);
        visual.localRotation = Quaternion.Euler(0f, 0f, lean);

        if (Mathf.Abs(delta.x) > 0.001f)
        {
            facingLeft = delta.x < 0f;
        }
    }

    private void AnimateIdle()
    {
        phase += Time.deltaTime * breathSpeed;

        float breath = Mathf.Sin(phase) * breathAmount;

        visual.localScale = new Vector3(
                baseScale.x * (1f - breath * 0.5f) * (facingLeft ? -1f : 1f),
                baseScale.y * (1f + breath),
                baseScale.z);

        visual.localRotation = Quaternion.identity;
    }

    private void EmitDust()
    {
        if (dustSprite == null) return;

        dustTimer -= Time.deltaTime;
        if (dustTimer > 0f) return;

        dustTimer = dustInterval;

        GameObject puff = new GameObject("Dust");
        puff.transform.position = transform.position + new Vector3(
                Random.Range(-0.12f, 0.12f), -0.45f, 0f);

        SpriteRenderer renderer = puff.AddComponent<SpriteRenderer>();
        renderer.sprite = dustSprite;
        renderer.sortingOrder = spriteRenderer != null ? spriteRenderer.sortingOrder - 1 : 0;
        if (spriteRenderer != null) renderer.sharedMaterial = spriteRenderer.sharedMaterial;

        puff.AddComponent<DustPuff>();
    }
}

// Fades and drifts, then removes itself.
public class DustPuff : MonoBehaviour
{
    public float lifetime = 0.55f;
    public float rise = 0.4f;

    private SpriteRenderer renderer2D;
    private float age;
    private Vector3 drift;

    void Awake()
    {
        renderer2D = GetComponent<SpriteRenderer>();
        drift = new Vector3(Random.Range(-0.25f, 0.25f), rise, 0f);
    }

    void Update()
    {
        age += Time.deltaTime;

        float t = age / lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += drift * Time.deltaTime;
        transform.localScale = Vector3.one * (0.7f + t * 0.8f);

        if (renderer2D != null)
        {
            Color c = renderer2D.color;
            c.a = 1f - t;
            renderer2D.color = c;
        }
    }
}
