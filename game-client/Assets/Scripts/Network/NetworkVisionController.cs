using UnityEngine;
using UnityEngine.Rendering.Universal;

// Implements the blindfold effect for the Kanamachi player.
//
// When a player becomes Kanamachi:
// 1. Dark vision overlay around that player (completely black & opaque outside radius).
// 2. Kanamachi and surroundings outside the small vision radius cannot be seen.
// 3. Small circular vision area around the player.
// 4. Slightly illuminated vision light matching the small vision radius.
// 5. Alpona ring around the player matching the small vision radius.
// 6. Dynamic application when a different player becomes Kanamachi.
// 7. Normal visibility restored when no longer Kanamachi.
public class NetworkVisionController : MonoBehaviour
{
    [Header("References")]
    public Light2D globalLight;

    [Header("Darkness")]
    [Tooltip("Global light level while a player is the blindfolded one.")]
    public float blindfoldIntensity = 0f;
    [Tooltip("Global light level while player can see normally.")]
    public float normalIntensity = 1f;

    [Header("Alpona ring")]
    [Tooltip("The rice-paste motif drawn at the edge of what the blindfolded player can perceive.")]
    public Sprite alponaSprite;
    public float alponaScale = 1f;
    [Range(0f, 1f)] public float alponaOpacity = 0.8f;
    public float alponaSpinSpeed = 6f;

    [Header("Flicker")]
    [Tooltip("The circle breathes like lantern light rather than sitting perfectly still.")]
    public float flickerAmount = 0.05f;
    public float flickerSpeed = 5.5f;

    [Header("Vision Circle")]
    public float visionOuterRadius = 1.6f;
    public float visionInnerRadius = 1.1f;
    public float visionIntensity = 0.55f;
    public Color visionColor = new Color(1f, 0.95f, 0.85f);

    private Light2D visionLight;
    private Transform alponaRing;
    private GameObject darkOverlay;
    private Material overlayMaterial;
    private Mesh overlayMesh;
    private float currentMeshRadius = -1f;
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

        if (alponaSprite == null)
        {
            FindAlponaSprite();
        }
    }

    private void FindAlponaSprite()
    {
        Sprite[] sprites = Resources.FindObjectsOfTypeAll<Sprite>();
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null && sprites[i].name.Contains("alpona"))
            {
                alponaSprite = sprites[i];
                break;
            }
        }
    }

    void Update()
    {
        // If NetworkGameManager is not actively controlling vision, fall back to GameManager
        if (NetworkGameManager.Instance == null && GameManager.Instance != null)
        {
            Player kanamachi = GameManager.Instance.GetKanamachiPlayer();
            bool active = kanamachi != null && !GameManager.Instance.IsMatchOver;
            SetState(kanamachi, active);
        }
    }

    // Called by NetworkGameManager every frame or whenever Kanamachi state changes.
    public void SetState(Player player, bool isKanamachi)
    {
        if (logLightState && Time.time >= nextLogTime && globalLight != null)
        {
            nextLogTime = Time.time + 2f;
            Debug.Log($"[Vision] light='{globalLight.gameObject.name}' " +
                      $"intensity={globalLight.intensity:F2} " +
                      $"kanamachi={isKanamachi} " +
                      $"player={(player != null ? player.DisplayName : "null")}");
        }

        bool changed = isKanamachi != trackedIsKanamachi || player != trackedPlayer;

        trackedIsKanamachi = isKanamachi;
        trackedPlayer = player;

        if (changed)
        {
            Apply(player, isKanamachi);
            return;
        }

        float wanted = isKanamachi ? blindfoldIntensity : normalIntensity;

        if (isKanamachi && player != null && (visionLight == null || darkOverlay == null))
        {
            AttachVisionTo(player);
        }

        if (globalLight != null && !Mathf.Approximately(globalLight.intensity, wanted))
        {
            globalLight.intensity = wanted;
        }
    }

    // Kept so existing callers still work.
    public void Refresh(Player player, bool isKanamachi)
    {
        Apply(player, isKanamachi);
    }

    public void ApplyBlindfold(Player player)
    {
        SetState(player, true);
    }

    public void RestoreNormalVisibility()
    {
        SetState(null, false);
    }

    private void Apply(Player player, bool isKanamachi)
    {
        if (globalLight != null)
        {
            globalLight.intensity = isKanamachi ? blindfoldIntensity : normalIntensity;
        }

        if (isKanamachi && player != null)
        {
            AttachVisionTo(player);
        }
        else
        {
            RemoveVision();
        }
    }

    void LateUpdate()
    {
        if (visionLight != null)
        {
            flickerPhase += Time.deltaTime * flickerSpeed;
            float flicker = (Mathf.Sin(flickerPhase) + Mathf.Sin(flickerPhase * 2.3f) * 0.4f) * flickerAmount;
            visionLight.intensity = visionIntensity * (1f + flicker);
        }

        if (alponaRing != null)
        {
            alponaRing.Rotate(0f, 0f, alponaSpinSpeed * Time.deltaTime);
        }

        if (darkOverlay != null)
        {
            darkOverlay.transform.rotation = Quaternion.identity;
        }
    }

    private void AttachVisionTo(Player player)
    {
        RemoveVision();

        // 1. Point light on player with matching vision radius and reduced intensity
        GameObject lightObj = new GameObject("VisionLight");
        lightObj.transform.SetParent(player.transform);
        lightObj.transform.localPosition = Vector3.zero;

        visionLight = lightObj.AddComponent<Light2D>();
        visionLight.lightType = Light2D.LightType.Point;
        visionLight.pointLightOuterRadius = visionOuterRadius + 0.25f;
        visionLight.pointLightInnerRadius = visionInnerRadius;
        visionLight.intensity = visionIntensity;
        visionLight.color = visionColor;

        // 2. Alpona ring matching vision radius
        AttachAlponaTo(player);

        // 3. Dark vision overlay covering outside of vision radius
        AttachDarkOverlayTo(player);
    }

    private void AttachAlponaTo(Player player)
    {
        if (alponaSprite == null)
        {
            FindAlponaSprite();
        }
        if (alponaSprite == null) return;

        GameObject ringObj = new GameObject("AlponaRing");
        ringObj.transform.SetParent(player.transform);
        ringObj.transform.localPosition = Vector3.zero;

        SpriteRenderer renderer = ringObj.AddComponent<SpriteRenderer>();
        renderer.sprite = alponaSprite;
        renderer.color = new Color(1f, 0.96f, 0.88f, alponaOpacity);

        // Drawn over the ground but under the players
        renderer.sortingOrder = -500;

        float spriteWorldSize = alponaSprite.rect.width / alponaSprite.pixelsPerUnit;
        float scale = alponaScale > 0f ? alponaScale : 1f;
        float wanted = visionOuterRadius * 2f * scale;
        ringObj.transform.localScale = Vector3.one * (wanted / spriteWorldSize);

        alponaRing = ringObj.transform;
    }

    private void AttachDarkOverlayTo(Player player)
    {
        if (darkOverlay != null)
        {
            Destroy(darkOverlay);
            darkOverlay = null;
        }

        darkOverlay = new GameObject("DarkVisionOverlay");
        darkOverlay.transform.SetParent(player.transform);
        darkOverlay.transform.localPosition = Vector3.zero;
        darkOverlay.transform.localRotation = Quaternion.identity;

        MeshFilter filter = darkOverlay.AddComponent<MeshFilter>();
        MeshRenderer renderer = darkOverlay.AddComponent<MeshRenderer>();

        float holeRadius = visionOuterRadius + 0.15f;
        if (overlayMesh == null || !Mathf.Approximately(currentMeshRadius, holeRadius))
        {
            if (overlayMesh != null) Destroy(overlayMesh);
            overlayMesh = GenerateVisionHoleMesh(holeRadius, 120f, 64);
            currentMeshRadius = holeRadius;
        }
        filter.mesh = overlayMesh;

        if (overlayMaterial == null)
        {
            overlayMaterial = CreateDarkOverlayMaterial();
        }
        renderer.material = overlayMaterial;

        // Render in front of players, obstacles, and environment (5000), but below screen-space UI
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 5000;
    }

    private Material CreateDarkOverlayMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("UI/Default");

        Material mat = new Material(shader);
        mat.color = Color.black;
        mat.renderQueue = 3000;
        return mat;
    }

    private Mesh GenerateVisionHoleMesh(float innerRadius, float outerRadius, int segments)
    {
        Mesh mesh = new Mesh();
        mesh.name = "DarkVisionHoleMesh";

        int vertCount = segments * 2;
        Vector3[] vertices = new Vector3[vertCount];
        Color[] colors = new Color[vertCount];
        Vector2[] uvs = new Vector2[vertCount];

        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float cos = Mathf.Cos(angle);
            float sin = Mathf.Sin(angle);

            // Inner boundary (hole at vision radius)
            vertices[i] = new Vector3(cos * innerRadius, sin * innerRadius, 0f);
            colors[i] = Color.black;
            uvs[i] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);

            // Outer boundary (enclosing full screen)
            vertices[segments + i] = new Vector3(cos * outerRadius, sin * outerRadius, 0f);
            colors[segments + i] = Color.black;
            uvs[segments + i] = new Vector2(0.5f + cos * 0.5f, 0.5f + sin * 0.5f);
        }

        int quadCount = segments;
        // Two double-sided triangles per quad: 12 indices per segment
        int[] triangles = new int[quadCount * 12];
        int t = 0;

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            int innerCurrent = i;
            int innerNext = next;
            int outerCurrent = segments + i;
            int outerNext = segments + next;

            // Front face
            triangles[t++] = innerCurrent;
            triangles[t++] = outerCurrent;
            triangles[t++] = outerNext;

            triangles[t++] = innerCurrent;
            triangles[t++] = outerNext;
            triangles[t++] = innerNext;

            // Back face
            triangles[t++] = innerCurrent;
            triangles[t++] = outerNext;
            triangles[t++] = outerCurrent;

            triangles[t++] = innerCurrent;
            triangles[t++] = innerNext;
            triangles[t++] = outerNext;
        }

        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    private void RemoveVision()
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

        if (darkOverlay != null)
        {
            Destroy(darkOverlay);
            darkOverlay = null;
        }
    }

    void OnDestroy()
    {
        RemoveVision();
        if (overlayMaterial != null)
        {
            Destroy(overlayMaterial);
            overlayMaterial = null;
        }
        if (overlayMesh != null)
        {
            Destroy(overlayMesh);
            overlayMesh = null;
        }
    }
}
