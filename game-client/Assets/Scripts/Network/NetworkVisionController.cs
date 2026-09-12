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

    [Header("Alpona ring")]
    [Tooltip("The rice-paste motif drawn at the edge of what the blindfolded player can perceive.")]
    public Sprite alponaSprite;
    public float alponaScale = 1.15f;
    [Range(0f, 1f)] public float alponaOpacity = 0.35f;
    public float alponaSpinSpeed = 6f;

    [Header("Flicker")]
    [Tooltip("The circle breathes like lantern light rather than sitting perfectly still.")]
    public float flickerAmount = 0.09f;
    public float flickerSpeed = 5.5f;

    [Header("Vision Circle")]
    public float visionOuterRadius = 2.5f;
    public float visionInnerRadius = 0.7f;
    public float visionIntensity = 1.3f;
    public Color visionColor = new Color(1f, 0.95f, 0.8f);

    private Light2D visionLight;
    private Transform alponaRing;
    private float flickerPhase;

    [Header("Diagnostics")]
    [Tooltip("Logs what the global light is actually set to, to catch anything else changing it.")]
    public bool logLightState = false;

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
        float wanted = localPlayerIsKanamachi ? blindfoldIntensity : normalIntensity;

        // The light follows whoever is blindfolded; if that body only
        // appeared after the announcement, pick it up now.
        if (localPlayerIsKanamachi && localPlayer != null && visionLight == null)
        {
            AttachVisionLightTo(localPlayer);
        }

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

        if (localPlayerIsKanamachi)
        {
            // The world goes dark as soon as this player is blindfolded.
            // The circle of light needs a body to follow, so it waits for
            // one - but the darkness does not.
            globalLight.intensity = blindfoldIntensity;

            if (localPlayer != null) AttachVisionLightTo(localPlayer);
        }
        else
        {
            globalLight.intensity = normalIntensity;
            RemoveVisionLight();
        }
    }

    void LateUpdate()
    {
        if (visionLight == null) return;

        // Lantern light is never steady. Two offset waves keep the flicker
        // from reading as a regular pulse.
        flickerPhase += Time.deltaTime * flickerSpeed;
        float flicker = (Mathf.Sin(flickerPhase) + Mathf.Sin(flickerPhase * 2.3f) * 0.4f) * flickerAmount;

        visionLight.intensity = visionIntensity * (1f + flicker);

        if (alponaRing != null)
        {
            alponaRing.Rotate(0f, 0f, alponaSpinSpeed * Time.deltaTime);
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

        AttachAlponaTo(player);
    }

    // The ring marks the edge of the blindfolded player's world. It is the
    // one piece of decoration that is also information.
    private void AttachAlponaTo(Player player)
    {
        if (alponaSprite == null) return;

        GameObject ringObj = new GameObject("AlponaRing");
        ringObj.transform.SetParent(player.transform);
        ringObj.transform.localPosition = Vector3.zero;

        SpriteRenderer renderer = ringObj.AddComponent<SpriteRenderer>();
        renderer.sprite = alponaSprite;
        renderer.color = new Color(1f, 1f, 1f, alponaOpacity);

        // Drawn over the ground but under the players.
        renderer.sortingOrder = -500;

        // Clipped to the arena mask, so the motif never spills past the
        // courtyard walls when the player stands near an edge.
        renderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;

        // Sized so the motif sits at the rim of the light rather than on top
        // of the player.
        float spriteWorldSize = alponaSprite.rect.width / alponaSprite.pixelsPerUnit;
        float wanted = visionOuterRadius * 2f * alponaScale;
        ringObj.transform.localScale = Vector3.one * (wanted / spriteWorldSize);

        alponaRing = ringObj.transform;
    }

    private void RemoveVisionLight()
    {
        if (visionLight != null)
        {
            Destroy(visionLight.gameObject);
            visionLight = null;
        }

        if (alponaRing != null)
        {
            Destroy(alponaRing.gameObject);
            alponaRing = null;
        }
    }
}
