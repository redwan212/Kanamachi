using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover and press reactions for a menu button.
//
// Unity's built-in colour tint changes the whole graphic at once, which on a
// dark title screen reads as the button briefly breaking. Scaling slightly
// and brightening the label instead keeps the button feeling solid while
// still making it obvious which option is under the cursor.
public class UIButtonFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
{
    [Header("Scale")]
    public float hoverScale = 1.03f;
    public float pressedScale = 0.98f;
    public float speed = 12f;

    [Header("Colour")]
    public Color normalText = Color.white;
    public Color hoverText = Color.white;

    private RectTransform rect;
    private TextMeshProUGUI label;
    private Vector3 baseScale;
    private float target = 1f;

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        label = GetComponentInChildren<TextMeshProUGUI>();
        baseScale = rect.localScale;
    }

    void Update()
    {
        // Eased rather than snapped, so the motion reads as deliberate.
        rect.localScale = Vector3.Lerp(rect.localScale, baseScale * target,
                Time.unscaledDeltaTime * speed);
    }

    public void OnPointerEnter(PointerEventData e) { Highlight(true); }
    public void OnPointerExit(PointerEventData e) { Highlight(false); }
    public void OnSelect(BaseEventData e) { Highlight(true); }
    public void OnDeselect(BaseEventData e) { Highlight(false); }

    public void OnPointerDown(PointerEventData e) { target = pressedScale; }
    public void OnPointerUp(PointerEventData e) { target = hoverScale; }

    private void Highlight(bool on)
    {
        target = on ? hoverScale : 1f;
        if (label != null) label.color = on ? hoverText : normalText;
    }
}
