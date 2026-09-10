using UnityEngine;
using UnityEngine.Rendering.Universal;

// Implements the blindfold from the point of view of THIS machine only.
//
// In an online match every player looks at their own screen, so the darkness
// is personal: if you are the Kanamachi your world shrinks to a small circle
// of light around you, and if you are not, you see the whole courtyard and
// can watch the blind player fumbling around.
//
// This is the difference from the local BlindfoldManager, which darkened the
// one shared screen for everybody.
public class NetworkVisionController : MonoBehaviour
{
    [Header("References")]
    public Light2D globalLight;

    [Header("Darkness")]
    [Tooltip("Global light level while this player is the blindfolded one.")]
    public float blindfoldIntensity = 0.08f;
    [Tooltip("Global light level while this player can see normally.")]
    public float normalIntensity = 1f;

    [Header("Vision Circle")]
    public float visionOuterRadius = 2.5f;
    public float visionInnerRadius = 0.7f;
    public float visionIntensity = 1.3f;
    public Color visionColor = new Color(1f, 0.95f, 0.8f);

    private Light2D visionLight;

    void Start()
    {
        if (globalLight == null)
        {
            Debug.LogWarning("[NetworkVisionController] No Global Light 2D assigned.");
            return;
        }

        // Until the server says who the Kanamachi is, let the player see.
        globalLight.intensity = normalIntensity;
    }

    // Called whenever the Kanamachi changes. localPlayer may be null if this
    // player's own object has not been spawned yet.
    public void Refresh(Player localPlayer, bool localPlayerIsKanamachi)
    {
        if (globalLight == null) return;

        if (localPlayerIsKanamachi && localPlayer != null)
        {
            globalLight.intensity = blindfoldIntensity;
            AttachVisionLightTo(localPlayer);
        }
        else
        {
            globalLight.intensity = normalIntensity;
            RemoveVisionLight();
        }
    }

    private void AttachVisionLightTo(Player player)
    {
        RemoveVisionLight();

        GameObject lightObj = new GameObject("VisionLight");
        lightObj.transform.SetParent(player.transform);
        lightObj.transform.localPosition = Vector3.zero;

        visionLight = lightObj.AddComponent<Light2D>();
        visionLight.lightType = Light2D.LightType.Point;
        visionLight.pointLightOuterRadius = visionOuterRadius;
        visionLight.pointLightInnerRadius = visionInnerRadius;
        visionLight.intensity = visionIntensity;
        visionLight.color = visionColor;
    }

    private void RemoveVisionLight()
    {
        if (visionLight != null)
        {
            Destroy(visionLight.gameObject);
            visionLight = null;
        }
    }
}
