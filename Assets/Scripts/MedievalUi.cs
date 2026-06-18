using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class MedievalUi
{
    public static readonly Color Ink = new(0.035f, 0.028f, 0.022f, 0.94f);
    public static readonly Color Parchment = new(0.78f, 0.68f, 0.47f, 0.97f);
    public static readonly Color Gold = new(0.92f, 0.68f, 0.22f);
    public static readonly Color Bone = new(0.94f, 0.9f, 0.78f);

    private static Sprite whiteSprite;

    // A solid-white sprite shared by every UI graphic that needs real mesh geometry.
    // A sprite-less Image of type Filled ignores fillAmount (it always renders the full
    // quad), so progress bars MUST have a sprite to deplete. See BattleHud.BuildBar.
    public static Sprite WhiteSprite
    {
        get
        {
            if (whiteSprite == null)
            {
                Texture2D tex = Texture2D.whiteTexture;
                whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), 100f);
                whiteSprite.name = "MedievalUi White";
            }
            return whiteSprite;
        }
    }

    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder = 10)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        Canvas canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        CanvasScaler scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        EnsureEventSystem();
        return canvas;
    }

    public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image), typeof(Outline));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color ?? Ink;
        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = Gold;
        outline.effectDistance = new Vector2(2f, -2f);
        return rect;
    }

    public static Text Label(Transform parent, string name, string value, int size, TextAnchor alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color ?? Bone;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = Mathf.Max(10, size / 2);
        text.resizeTextMaxSize = size;
        return text;
    }

    public static Button Button(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, UnityAction action)
    {
        RectTransform panel = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, Parchment);
        Button button = panel.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Parchment;
        colors.highlightedColor = new Color(0.98f, 0.82f, 0.42f);
        colors.pressedColor = Gold;
        button.colors = colors;
        button.targetGraphic = panel.GetComponent<Image>();
        button.onClick.AddListener(action);
        Label(panel, "Label", label, 28, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.12f, 0.075f, 0.035f));
        return button;
    }

    public static Slider Slider(Transform parent, string name, string label, float value, Vector2 anchorMin,
        Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, UnityAction<float> action)
    {
        RectTransform row = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, new Color(0.08f, 0.06f, 0.04f, 0.8f));
        Label(row, "Label", label, 22, TextAnchor.MiddleLeft, Vector2.zero, new Vector2(0.42f, 1f), new Vector2(18f, 0f), Vector2.zero);
        GameObject go = new("Slider", typeof(RectTransform), typeof(Slider));
        go.transform.SetParent(row, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.45f, 0.2f);
        rect.anchorMax = new Vector2(0.96f, 0.8f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        RectTransform background = Panel(rect, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.15f, 0.12f, 0.08f));
        RectTransform fillArea = Panel(rect, "Fill", Vector2.zero, new Vector2(value, 1f), Vector2.zero, Vector2.zero, Gold);
        Slider slider = go.GetComponent<Slider>();
        slider.targetGraphic = background.GetComponent<Image>();
        slider.fillRect = fillArea;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(v =>
        {
            fillArea.anchorMax = new Vector2(v, 1f);
            action(v);
        });
        return slider;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;
        // Keep the EventSystem at the scene root and persistent. Parenting it under the
        // requesting canvas would let a mode teardown (Destroy(battleRoot/mapRoot)) take
        // the only EventSystem with it, leaving every later canvas unclickable.
        GameObject eventSystem = new("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        Object.DontDestroyOnLoad(eventSystem);
    }
}
