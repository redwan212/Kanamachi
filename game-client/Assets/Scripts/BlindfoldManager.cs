using UnityEngine;
using UnityEngine.Rendering.Universal;

// Darkens the whole scene using the Global Light 2D, then attaches a small
// "vision circle" light to whichever Player is currently the Kanamachi.
public class BlindfoldManager : MonoBehaviour
{
    [Header("References (assign in Inspector)")]
    public Light2D globalLight;

    [Header("Darkness Settings")]
    public float blindfoldIntensity = 0.08f;
    public float normalIntensity = 1f;

    [Header("Vision Circle Settings")]
    public float visionOuterRadius = 2.5f;
    public float visionInnerRadius = 0.7f;
    public float visionIntensity = 1.3f;
    public Color visionColor = new Color(1f, 0.95f, 0.8f);

    private Light2D kanamachiVisionLight;
    private Player currentKanamachiTracked;

    void Start()
    {
        if (globalLight != null)
        {
            globalLight.intensity = blindfoldIntensity;
        }
        else
        {
            Debug.LogWarning("[BlindfoldManager] No Global Light 2D assigned in the Inspector.");
        }
    }

    void Update()
    {
        if (GameManager.Instance == null) return;

        Player kanamachi = GameManager.Instance.GetKanamachiPlayer();
        if (kanamachi == null) return;

        if (currentKanamachiTracked != kanamachi)
        {
            AttachVisionLightTo(kanamachi);
            currentKanamachiTracked = kanamachi;
        }
    }

    private void AttachVisionLightTo(Player player)
    {
        if (kanamachiVisionLight != null)
        {
            Destroy(kanamachiVisionLight.gameObject);
        }

        GameObject lightObj = new GameObject("KanamachiVisionLight");
        lightObj.transform.SetParent(player.transform);
        lightObj.transform.localPosition = Vector3.zero;

        kanamachiVisionLight = lightObj.AddComponent<Light2D>();
        kanamachiVisionLight.lightType = Light2D.LightType.Point;
        kanamachiVisionLight.pointLightOuterRadius = visionOuterRadius;
        kanamachiVisionLight.pointLightInnerRadius = visionInnerRadius;
        kanamachiVisionLight.intensity = visionIntensity;
        kanamachiVisionLight.color = visionColor;
    }

    public void RestoreNormalLighting()
    {
        if (globalLight != null) globalLight.intensity = normalIntensity;
        if (kanamachiVisionLight != null) Destroy(kanamachiVisionLight.gameObject);
    }
}
