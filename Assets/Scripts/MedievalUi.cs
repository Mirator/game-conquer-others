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

    // Kenney "Fantasy UI Borders" 9-slice sources, shipped as raw bytes under
    // Resources/UI so they need no TextureImporter sprite settings (the project
    // builds everything from primitives and avoids hand-authored .meta files).
    // The art is white-on-alpha, so a single sprite is tinted to any palette colour.
    private const string PanelResource = "UI/panel";   // filled body + corner motif
    private const string FrameResource = "UI/frame";   // border outline only
    private const string DividerResource = "UI/divider"; // line + end ornament
    private const float SourceBorder = 16f;            // 9-slice inset on the 48px source

    private static Sprite whiteSprite;
    private static Sprite panelSprite;
    private static Sprite frameSprite;
    private static Sprite dividerSprite;

    public static Sprite PanelSprite => panelSprite != null ? panelSprite : panelSprite = LoadSliced(PanelResource, SourceBorder);
    public static Sprite FrameSprite => frameSprite != null ? frameSprite : frameSprite = LoadSliced(FrameResource, SourceBorder);
    public static Sprite DividerSprite => dividerSprite != null ? dividerSprite : dividerSprite = LoadSliced(DividerResource, 0f);

    // Decode a packed PNG into a point-filtered sprite with an explicit 9-slice
    // border. Returns null if the resource is missing so callers degrade to a
    // plain tinted rectangle rather than throwing.
    private static Sprite LoadSliced(string resource, float border)
    {
        TextAsset raw = Resources.Load<TextAsset>(resource);
        if (raw == null)
            return null;
        Texture2D tex = new(2, 2, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.LoadImage(raw.bytes);
        tex.name = resource;
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
        sprite.name = resource;
        return sprite;
    }

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

    // A plain tinted rectangle: backdrops, full-screen overlays, progress-bar
    // fills, and slider internals. Carries no border art so thin bars stay clean.
    public static RectTransform Panel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        go.GetComponent<Image>().color = color ?? Ink;
        return rect;
    }

    // A framed card: a Kenney filled panel tinted to the body colour with the
    // ornate fantasy border laid on top in the accent colour. This is the shared
    // look for every menu, HUD, and campaign container. Falls back to a plain
    // tinted rectangle if the border art is unavailable.
    public static RectTransform Frame(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color? body = null, Color? border = null)
    {
        RectTransform rect = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, body ?? Ink);
        Image fill = rect.GetComponent<Image>();
        if (PanelSprite != null)
        {
            fill.sprite = PanelSprite;
            fill.type = Image.Type.Sliced;
            fill.pixelsPerUnitMultiplier = 1f;
        }
        AddBorder(rect, border ?? Gold);
        return rect;
    }

    // Overlay the border outline as a non-interactive child so it sits above the
    // body fill yet never intercepts clicks meant for the owning panel/button.
    private static void AddBorder(RectTransform parent, Color color)
    {
        if (FrameSprite == null)
            return;
        GameObject go = new("Border", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = go.GetComponent<Image>();
        image.sprite = FrameSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = color;
        image.raycastTarget = false;
    }

    // A centred horizontal flourish for section headers: the Kenney divider plus
    // its mirror, so the end ornament meets in the middle like the pack sample.
    public static void Divider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax, Color? color = null)
    {
        if (DividerSprite == null)
            return;
        RectTransform row = Panel(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, Color.clear);
        row.GetComponent<Image>().raycastTarget = false;
        Color tint = color ?? Gold;
        AddDividerHalf(row, "Left", true, tint);
        AddDividerHalf(row, "Right", false, tint);
    }

    private static void AddDividerHalf(RectTransform parent, string name, bool leftHalf, Color tint)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        // Pivot at the rect centre so the right half's horizontal flip mirrors the
        // end ornament toward the middle in place, instead of reflecting it away.
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchorMin = new Vector2(leftHalf ? 0f : 0.5f, 0f);
        rect.anchorMax = new Vector2(leftHalf ? 0.5f : 1f, 1f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        rect.localScale = new Vector3(leftHalf ? 1f : -1f, 1f, 1f);
        Image image = go.GetComponent<Image>();
        image.sprite = DividerSprite;
        image.type = Image.Type.Simple;
        image.color = tint;
        image.raycastTarget = false;
        image.preserveAspect = false;
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
        RectTransform panel = Frame(parent, name, anchorMin, anchorMax, offsetMin, offsetMax, Parchment, new Color(0.45f, 0.31f, 0.12f));
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
