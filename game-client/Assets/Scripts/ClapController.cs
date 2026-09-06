using UnityEngine;

// CLAP: a human player presses a key to make a loud, distinct noise.
// TEASE: if that clap happens while close to the Kanamachi, it counts as
// "teasing" and earns bonus points (risk/reward).
[RequireComponent(typeof(HumanPlayer))]
public class ClapController : MonoBehaviour
{
    [Header("Tease Settings")]
    public float teaseRiskRadius = 3f;
    public int teaseBonusPoints = 5;
    public float clapCooldown = 1f;

    private HumanPlayer humanPlayer;
    private AudioSource audioSource;
    private float cooldownTimer;

    void Awake()
    {
        humanPlayer = GetComponent<HumanPlayer>();
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.clip = GenerateClapClip();
    }

    void Update()
    {
        cooldownTimer -= Time.deltaTime;

        bool clapPressed = humanPlayer.controls == ControlScheme.WASD
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

        Player kanamachi = GameManager.Instance != null ? GameManager.Instance.GetKanamachiPlayer() : null;
        if (kanamachi != null && kanamachi != humanPlayer)
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
