using UnityEngine;

// CLAP: any player can press a key to make a loud, distinct noise.
// TEASE: if that clap happens while the player is close to the Kanamachi,
// it counts as "teasing" and earns bonus points (risk/reward - the closer
// you dare to get, the more likely you are to also get caught).
[RequireComponent(typeof(PlayerController))]
public class ClapController : MonoBehaviour
{
    [Header("Tease Settings")]
    public float teaseRiskRadius = 3f;
    public int teaseBonusPoints = 5;
    public float clapCooldown = 1f;

    private PlayerController controller;
    private AudioSource audioSource;
    private float cooldownTimer;

    void Awake()
    {
        controller = GetComponent<PlayerController>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = GenerateClapClip();
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        // WASD player claps with C, Arrow player claps with M (avoids movement key conflicts)
        bool clapPressed = controller.controls == ControlScheme.WASD
            ? Input.GetKeyDown(KeyCode.C)
            : Input.GetKeyDown(KeyCode.M);

        if (clapPressed && cooldownTimer <= 0f)
        {
            DoClap();
            cooldownTimer = clapCooldown;
        }
    }

    private void DoClap()
    {
        audioSource.volume = 1f;
        audioSource.pitch = 1f;
        audioSource.PlayOneShot(audioSource.clip);

        GameObject kanamachi = GameManager.Instance != null ? GameManager.Instance.GetKanamachiPlayer() : null;
        if (kanamachi != null && kanamachi != gameObject)
        {
            float dist = Vector2.Distance(transform.position, kanamachi.transform.position);
            if (dist <= teaseRiskRadius)
            {
                ScoreManager.Instance?.AddScore(gameObject, teaseBonusPoints);
                Debug.Log($"[ClapController] {gameObject.name} teased the Kanamachi from {dist:F1} units away! +{teaseBonusPoints} points.");
            }
            else
            {
                Debug.Log($"[ClapController] {gameObject.name} clapped, but was too far to tease.");
            }
        }
    }

    // Generates a short white-noise burst that sounds like a clap, so no audio file is needed.
    private AudioClip GenerateClapClip()
    {
        int sampleRate = 44100;
        float duration = 0.15f;
        int sampleCount = Mathf.CeilToInt(sampleRate * duration);
        float[] samples = new float[sampleCount];
        System.Random rng = new System.Random();

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 18f);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            samples[i] = noise * envelope;
        }

        AudioClip clip = AudioClip.Create("ClapSound", sampleCount, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
