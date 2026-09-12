using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The backdrop behind the title screen.
//
// Deliberately spare: a night sky, one circle of lantern light with the
// alpona ring turning around it, the blindfolded player, two others waiting
// at the rim, and the ground they stand on. Nothing else competes for
// attention, which is what lets the title and the PLAY button carry the
// right-hand side.
//
// Everything here is decoration and takes no input.
public class UILandingArt : MonoBehaviour
{
    [Header("Sprites")]
    public Sprite skyGradient;
    public Sprite groundMound;
    public Sprite glowSprite;
    public Sprite alponaSprite;
    public Sprite heroSprite;
    public Sprite companionSprite;
    public Sprite fireflySprite;

    [Header("Composition")]
    [Tooltip("Where the circle of light sits, as a fraction of the screen.")]
    public Vector2 circleAnchor = new Vector2(0.30f, 0.54f);
    public float circleSize = 640f;
    public float moundHeight = 220f;

    [Header("Figures")]
    public float heroHeight = 400f;
    public Vector2 heroOffset = new Vector2(0f, -60f);
    public float companionHeight = 200f;
    public Vector2 companionLeftOffset = new Vector2(-330f, -120f);
    public Vector2 companionRightOffset = new Vector2(320f, -135f);
    public Color companionColour = new Color(0.06f, 0.08f, 0.18f, 0.95f);

    [Header("Colour")]
    public Color glowColour = new Color(1f, 0.87f, 0.6f, 0.62f);
    public Color alponaColour = new Color(0.94f, 0.79f, 0.5f, 0.85f);

    [Header("Motion")]
    [Tooltip("Degrees per second. Matches the ring that follows the blindfolded player in a match.")]
    public float ringSpinSpeed = 6f;

    [Tooltip("The same two-wave lantern flicker the vision light uses in game.")]
    public float flickerAmount = 0.09f;
    public float flickerSpeed = 5.5f;

    public float heroBobAmount = 4f;
    public float heroBobSpeed = 1.2f;
    public int fireflyCount = 16;

    // Build() is called once per screen that wants the artwork, so every
    // animated piece is collected rather than kept in a single field. The
    // first version overwrote its references on the second call, which left
    // the visible ring sitting still while a hidden one turned.
    private readonly List<RectTransform> rings = new List<RectTransform>();
    private readonly List<Image> ringImages = new List<Image>();
    private readonly List<RectTransform> glows = new List<RectTransform>();
    private readonly List<Image> glowImages = new List<Image>();
    private readonly List<RectTransform> heroes = new List<RectTransform>();

    private Vector3 glowBaseScale = Vector3.one;
    private float glowBaseAlpha;
    private float ringBaseAlpha;
    private float phase;

    private readonly List<Firefly> fireflies = new List<Firefly>();

    private class Firefly
    {
        public RectTransform rect;
        public Image image;
        public Vector2 drift;
        public float blinkPhase;
        public float blinkSpeed;
    }

    // Built into whichever screen asks for it.
    //
    // Each layer inserts itself at the back, so they are created front to
    // back: the last thing built ends up furthest away.
    public void Build(Transform parent)
    {
        BuildFireflies(parent);
        BuildGround(parent);
        BuildCompanions(parent);
        BuildHero(parent);
        BuildRing(parent);
        BuildGlow(parent);
        BuildSky(parent);
    }

    void Update()
    {
        // Unscaled, so the menu keeps moving regardless of anything the game
        // does to the timescale.
        phase += Time.unscaledDeltaTime;

        // The ring turns continuously, exactly as it does around the
        // blindfolded player during a match.
        float spin = -ringSpinSpeed * Time.unscaledDeltaTime;
        foreach (RectTransform r in rings)
        {
            if (r != null) r.Rotate(0f, 0f, spin);
        }

        // Two offset waves rather than one, so the light flickers like a
        // lantern instead of pulsing on a regular beat.
        float flicker = (Mathf.Sin(phase * flickerSpeed)
                       + Mathf.Sin(phase * flickerSpeed * 2.3f) * 0.4f) * flickerAmount;

        Vector3 glowScale = glowBaseScale * (1f + flicker * 0.3f);
        float glowAlpha = Mathf.Clamp01(glowBaseAlpha * (1f + flicker));

        foreach (RectTransform g in glows)
        {
            if (g != null) g.localScale = glowScale;
        }

        foreach (Image gi in glowImages)
        {
            if (gi == null) continue;
            Color c = gi.color;
            c.a = glowAlpha;
            gi.color = c;
        }

        float ringAlpha = Mathf.Clamp01(ringBaseAlpha * (1f + flicker * 1.2f));
        foreach (Image ri in ringImages)
        {
            if (ri == null) continue;
            Color rc = ri.color;
            rc.a = ringAlpha;
            ri.color = rc;
        }

        float bob = Mathf.Sin(phase * heroBobSpeed) * heroBobAmount;
        foreach (RectTransform h in heroes)
        {
            if (h != null) h.anchoredPosition = heroOffset + new Vector2(0f, bob);
        }

        UpdateFireflies();
    }

    // ---------- Layers ----------

    private void BuildSky(Transform parent)
    {
        if (skyGradient == null) return;

        GameObject obj = NewImage(parent, "Sky", skyGradient, Color.white, 0f);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        obj.GetComponent<Image>().preserveAspect = false;
    }

    private void BuildGlow(Transform parent)
    {
        if (glowSprite == null) return;

        GameObject obj = NewImage(parent, "Glow", glowSprite, glowColour, circleSize * 1.7f);

        RectTransform rect = obj.GetComponent<RectTransform>();
        glows.Add(rect);
        glowImages.Add(obj.GetComponent<Image>());
        glowBaseAlpha = glowColour.a;

        Anchor(rect, circleAnchor);
        glowBaseScale = rect.localScale;
    }

    private void BuildRing(Transform parent)
    {
        if (alponaSprite == null) return;

        GameObject obj = NewImage(parent, "AlponaRing", alponaSprite, alponaColour, circleSize);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rings.Add(rect);
        ringImages.Add(obj.GetComponent<Image>());
        ringBaseAlpha = alponaColour.a;

        Anchor(rect, circleAnchor);
    }

    private void BuildHero(Transform parent)
    {
        if (heroSprite == null) return;

        float width = heroHeight * (heroSprite.rect.width / heroSprite.rect.height);
        GameObject obj = NewImage(parent, "Hero", heroSprite, Color.white, width);

        RectTransform rect = obj.GetComponent<RectTransform>();
        heroes.Add(rect);

        Anchor(rect, circleAnchor, heroOffset);
    }

    // The two others waiting at the edge of the light. They are the reason
    // the blindfolded player is standing there at all.
    private void BuildCompanions(Transform parent)
    {
        if (companionSprite == null) return;

        AddCompanion(parent, companionLeftOffset, companionHeight, false);
        AddCompanion(parent, companionRightOffset, companionHeight * 0.9f, true);
    }

    private void AddCompanion(Transform parent, Vector2 offset, float height, bool flip)
    {
        float width = height * (companionSprite.rect.width / companionSprite.rect.height);
        GameObject obj = NewImage(parent, "Companion", companionSprite, companionColour, width);

        RectTransform rect = obj.GetComponent<RectTransform>();
        Anchor(rect, circleAnchor, offset);

        // Facing inward, toward the light.
        if (flip) rect.localScale = new Vector3(-1f, 1f, 1f);
    }

    // A single band of earth across the bottom, so the figures are standing
    // on something rather than floating in a void.
    private void BuildGround(Transform parent)
    {
        if (groundMound == null) return;

        GameObject obj = NewImage(parent, "Ground", groundMound, Color.white, 0f);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(80f, moundHeight);
        rect.anchoredPosition = new Vector2(0f, -10f);

        obj.GetComponent<Image>().preserveAspect = false;
    }

    private void BuildFireflies(Transform parent)
    {
        if (fireflySprite == null) return;

        for (int i = 0; i < fireflyCount; i++)
        {
            GameObject obj = NewImage(parent, "Firefly", fireflySprite,
                    new Color(1f, 0.9f, 0.62f, 0.5f), Random.Range(5f, 11f));

            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(Random.Range(-900f, 900f),
                                                Random.Range(-420f, 440f));

            fireflies.Add(new Firefly
            {
                rect = rect,
                image = obj.GetComponent<Image>(),
                drift = new Vector2(Random.Range(-8f, 8f), Random.Range(4f, 15f)),
                blinkPhase = Random.Range(0f, 10f),
                blinkSpeed = Random.Range(0.6f, 2f)
            });
        }
    }

    private void UpdateFireflies()
    {
        foreach (Firefly fly in fireflies)
        {
            if (fly.rect == null) continue;

            fly.rect.anchoredPosition += fly.drift * Time.unscaledDeltaTime;

            // Wrap around instead of drifting off and never coming back.
            Vector2 p = fly.rect.anchoredPosition;
            if (p.y > 460f) p.y = -460f;
            if (p.x > 940f) p.x = -940f;
            if (p.x < -940f) p.x = 940f;
            fly.rect.anchoredPosition = p;

            Color c = fly.image.color;
            c.a = 0.2f + Mathf.Abs(Mathf.Sin(phase * fly.blinkSpeed + fly.blinkPhase)) * 0.6f;
            fly.image.color = c;
        }
    }

    // ---------- Helpers ----------

    // A width of 0 means the caller sets the rect itself.
    private GameObject NewImage(Transform parent, string name, Sprite sprite,
                                Color colour, float width)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);

        // Behind whatever the screen already placed, and behind the layers
        // built before it.
        obj.transform.SetAsFirstSibling();

        Image image = obj.GetComponent<Image>();
        image.sprite = sprite;
        image.color = colour;
        image.preserveAspect = true;

        // The menu has to stay clickable through the artwork.
        image.raycastTarget = false;

        if (width > 0f)
        {
            RectTransform rect = obj.GetComponent<RectTransform>();
            float height = width * (sprite.rect.height / sprite.rect.width);
            rect.sizeDelta = new Vector2(width, height);
        }

        return obj;
    }

    private void Anchor(RectTransform rect, Vector2 anchor, Vector2 offset = default)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
    }
}
