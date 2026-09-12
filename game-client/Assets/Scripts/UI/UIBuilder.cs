using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Small helpers for assembling uGUI elements in code.
//
// Building the interface from script rather than by hand in the Inspector
// keeps every screen consistent, makes restyling a one-line change in
// UITheme, and means the layout is reviewable in version control instead of
// buried in a scene file.
public static class UIBuilder
{
    public static GameObject Panel(Transform parent, string name, Color colour,
                                   Vector2 size, Vector2 anchoredPosition)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = obj.GetComponent<Image>();
        image.color = colour;

        return obj;
    }

    // A panel that fills its parent, used for full-screen backdrops.
    public static GameObject FullScreen(Transform parent, string name, Color colour)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        obj.GetComponent<Image>().color = colour;
        return obj;
    }

    public static TextMeshProUGUI Label(Transform parent, string text, int size,
                                        Color colour, Vector2 anchoredPosition,
                                        float width = 600f, float height = 40f,
                                        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        GameObject obj = new GameObject("Label", typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI label = obj.AddComponent<TextMeshProUGUI>();

        // A pixel font, when one has been imported, plus an outline and a
        // shadow. Applied here so no screen has to remember to do it.
        if (UITheme.Font != null) label.font = UITheme.Font;

        label.text = text;
        label.fontSize = size;
        label.color = colour;
        label.alignment = alignment;
        label.raycastTarget = false;

        ApplyOutline(label);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = anchoredPosition;

        return label;
    }

    public static Button TextButton(Transform parent, string text, Vector2 anchoredPosition,
                                    System.Action onClick, bool primary = true,
                                    float width = UITheme.ButtonWidth)
    {
        GameObject obj = new GameObject("Button", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, UITheme.ButtonHeight);
        rect.anchoredPosition = anchoredPosition;

        Image image = obj.GetComponent<Image>();

        // A pixel frame when one has been supplied, otherwise a flat fill.
        Sprite frame = primary ? UITheme.GoldButtonSprite : UITheme.PlainButtonSprite;
        if (frame != null)
        {
            image.sprite = frame;
            image.color = Color.white;

            // Sliced, so one frame sprite stretches to any button width
            // without the corners distorting.
            image.type = Image.Type.Sliced;
        }
        else
        {
            image.color = primary ? UITheme.Accent : UITheme.PanelSoft;
        }

        Button button = obj.GetComponent<Button>();
        ColorBlock colours = button.colors;
        colours.normalColor = Color.white;
        colours.highlightedColor = new Color(1f, 1f, 1f, 0.85f);
        colours.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        button.colors = colours;

        if (onClick != null)
        {
            button.onClick.AddListener(() => onClick());
        }

        TextMeshProUGUI label = Label(obj.transform, text, UITheme.BodySize,
                primary ? UITheme.TextOnAccent : UITheme.TextPrimary,
                Vector2.zero, width, UITheme.ButtonHeight);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        return button;
    }

    // A text-only menu entry: no panel, no border, just the word. This is
    // what a title screen menu looks like, and it keeps the artwork behind
    // it visible instead of covering it with boxes.
    public static Button MenuItem(Transform parent, string text, Vector2 anchoredPosition,
                                  System.Action onClick, int size = 0, float width = 420f)
    {
        GameObject obj = new GameObject("MenuItem", typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 52f);
        rect.anchoredPosition = anchoredPosition;

        // Invisible, but still needed so the whole row is clickable.
        Image image = obj.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0f);

        Button button = obj.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        if (onClick != null) button.onClick.AddListener(() => onClick());

        TextMeshProUGUI label = Label(obj.transform, text,
                size > 0 ? size : UITheme.BodySize,
                UITheme.TextMuted, Vector2.zero, width, 52f);
        label.textWrappingMode = TextWrappingModes.NoWrap;

        AddFeel(obj, UITheme.TextMuted, UITheme.Accent);

        return button;
    }

    private static void AddFeel(GameObject obj, Color normal, Color hover)
    {
        UIButtonFeel feel = obj.AddComponent<UIButtonFeel>();
        feel.normalText = normal;
        feel.hoverText = hover;
    }

    public static TMP_InputField InputField(Transform parent, string placeholder,
                                            Vector2 anchoredPosition, bool password = false,
                                            float width = UITheme.ButtonWidth)
    {
        GameObject obj = new GameObject("InputField", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        obj.transform.SetParent(parent, false);

        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, UITheme.FieldHeight);
        rect.anchoredPosition = anchoredPosition;

        obj.GetComponent<Image>().color = UITheme.PanelSoft;

        // The text area has to be a child with its own rect, otherwise the
        // caret is drawn outside the field.
        GameObject viewport = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D));
        viewport.transform.SetParent(obj.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = new Vector2(14f, 6f);
        viewportRect.offsetMax = new Vector2(-14f, -6f);

        TextMeshProUGUI textComponent = Label(viewport.transform, "", UITheme.BodySize,
                UITheme.TextPrimary, Vector2.zero, width - 28f, UITheme.FieldHeight - 12f,
                TextAlignmentOptions.Left);
        textComponent.raycastTarget = true;
        StretchToParent(textComponent.rectTransform);

        TextMeshProUGUI placeholderComponent = Label(viewport.transform, placeholder,
                UITheme.BodySize, UITheme.TextMuted, Vector2.zero, width - 28f,
                UITheme.FieldHeight - 12f, TextAlignmentOptions.Left);
        StretchToParent(placeholderComponent.rectTransform);

        TMP_InputField field = obj.GetComponent<TMP_InputField>();
        field.textViewport = viewportRect;
        field.textComponent = textComponent;
        field.placeholder = placeholderComponent;
        field.contentType = password ? TMP_InputField.ContentType.Password
                                     : TMP_InputField.ContentType.Standard;
        field.characterLimit = 24;

        return field;
    }

    // TextMeshPro exposes these through the material rather than the
    // component, and each label needs its own material instance or every
    // piece of text in the game changes together.
    private static void ApplyOutline(TextMeshProUGUI label)
    {
        if (UITheme.OutlineWidth <= 0f) return;

        Material material = label.fontMaterial;

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, UITheme.OutlineWidth);
        material.SetColor(ShaderUtilities.ID_OutlineColor, UITheme.OutlineColour);

        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0f, 0f, 0f, 0.65f));
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, UITheme.ShadowOffset);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -UITheme.ShadowOffset);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.05f);

        label.fontMaterial = material;
    }

    public static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
