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

    [Header("Diagnostics")]
    [Tooltip("Logs what the global light is actually set to, to catch anything else changing it.")]
    public bool logLightState = true;

    private Player trackedPlayer;
    private bool trackedIsKanamachi;
    private float nextLogTime;

    void Start()
    {
        if (globalLight == null)
        {
            Debug.LogWarning("[NetworkVisionController] No Global Light 2D assigned.");
        }
    }

    // The game manager pushes its state in every frame. Pulling from a
    // static reference turned out to be fragile: a duplicate manager could
    // own the static while a different one held the real state.
    public void SetState(Player localPlayer, bool localPlayerIsKanamachi)
    {
        if (globalLight == null) return;

        if (logLightState && Time.time >= nextLogTime)
        {
            nextLogTime = Time.time + 2f;
            Debug.Log($"[Vision] light='{globalLight.gameObject.name}' " +
                      $"intensity={globalLight.intensity:F2} " +
                      $"kanamachi={localPlayerIsKanamachi} " +
                      $"player={(localPlayer != null ? localPlayer.DisplayName : "null")}");
        }

        bool changed = localPlayerIsKanamachi != trackedIsKanamachi || localPlayer != trackedPlayer;

        trackedIsKanamachi = localPlayerIsKanamachi;
        trackedPlayer = localPlayer;

        if (changed)
        {
            Apply(localPlayer, localPlayerIsKanamachi);
            return;
        }

        // Keep enforcing it, so anything else writing to the light is undone
        // on the next frame rather than winning permanently.
        float wanted = localPlayerIsKanamachi && localPlayer != null
                ? blindfoldIntensity
                : normalIntensity;

        if (!Mathf.Approximately(globalLight.intensity, wanted))
        {
            globalLight.intensity = wanted;
        }
    }

    // Kept so existing callers still work; the per-frame check above is
    // what actually guarantees the result.
    public void Refresh(Player localPlayer, bool localPlayerIsKanamachi)
    {
        Apply(localPlayer, localPlayerIsKanamachi);
    }

    private void Apply(Player localPlayer, bool localPlayerIsKanamachi)
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
