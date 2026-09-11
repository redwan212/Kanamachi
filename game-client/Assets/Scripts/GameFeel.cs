using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The small reactions that make a game feel responsive rather than correct.
//
// None of this changes a rule. A catch already worked before the screen
// shook; the shake exists so the moment lands. These are the cheapest
// improvements available to a project that already plays properly, and
// their absence is most of what separates a prototype from a finished game.
public class GameFeel : MonoBehaviour
{
    public static GameFeel Instance;

    [Header("Camera shake")]
    public float catchShake = 0.35f;
    public float correctGuessShake = 0.2f;
    public float wrongGuessShake = 0.12f;
    public float shakeDuration = 0.28f;

    [Header("Fade")]
    public float fadeDuration = 0.45f;
    public Color fadeColour = new Color(0.04f, 0.05f, 0.11f, 1f);

    private Camera targetCamera;
    private Vector3 cameraHome;
    private Coroutine shakeRoutine;

    private Image fadeImage;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        targetCamera = Camera.main;
        if (targetCamera != null) cameraHome = targetCamera.transform.position;

        BuildFadeOverlay();
    }

    // ---------- Shake ----------

    public void Shake(float strength)
    {
        if (targetCamera == null) return;

        if (shakeRoutine != null) StopCoroutine(shakeRoutine);
        shakeRoutine = StartCoroutine(ShakeRoutine(strength));
    }

    public void ShakeForCatch() { Shake(catchShake); }
    public void ShakeForGuess(bool correct) { Shake(correct ? correctGuessShake : wrongGuessShake); }

    private IEnumerator ShakeRoutine(float strength)
    {
        float elapsed = 0f;
        cameraHome = targetCamera.transform.position;

        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;

            // Decaying, so it hits hard and settles rather than rattling.
            float falloff = 1f - (elapsed / shakeDuration);
            float amount = strength * falloff * falloff;

            targetCamera.transform.position = cameraHome + new Vector3(
                    Random.Range(-amount, amount),
                    Random.Range(-amount, amount),
                    0f);

            yield return null;
        }

        targetCamera.transform.position = cameraHome;
        shakeRoutine = null;
    }

    // ---------- Fade ----------

    private void BuildFadeOverlay()
    {
        GameObject canvasObj = new GameObject("Fade Canvas", typeof(Canvas), typeof(CanvasScaler));
        canvasObj.transform.SetParent(transform, false);

        Canvas canvas = canvasObj.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Above everything, including the menus.
        canvas.sortingOrder = 500;

        GameObject imageObj = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        imageObj.transform.SetParent(canvasObj.transform, false);

        RectTransform rect = imageObj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeImage = imageObj.GetComponent<Image>();
        fadeImage.color = new Color(fadeColour.r, fadeColour.g, fadeColour.b, 0f);

        // Transparent overlays still swallow clicks unless this is off.
        fadeImage.raycastTarget = false;
    }

    // Fades to black, runs the change, then fades back. Used between levels
    // so the arena is never seen rebuilding itself.
    public void FadeThrough(System.Action midpoint)
    {
        StartCoroutine(FadeRoutine(midpoint));
    }

    private IEnumerator FadeRoutine(System.Action midpoint)
    {
        yield return FadeTo(1f);

        midpoint?.Invoke();
        yield return new WaitForSeconds(0.1f);

        yield return FadeTo(0f);
    }

    private IEnumerator FadeTo(float target)
    {
        if (fadeImage == null) yield break;

        float start = fadeImage.color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(start, target, elapsed / fadeDuration);
            fadeImage.color = new Color(fadeColour.r, fadeColour.g, fadeColour.b, a);
            yield return null;
        }

        fadeImage.color = new Color(fadeColour.r, fadeColour.g, fadeColour.b, target);
    }
}
