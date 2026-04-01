using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public class SimulationDashboardStyler : MonoBehaviour
{
    [Header("Theme")]
    public TMP_FontAsset preferredFont;

    private const string TopScrimName = "__TopScrim";
    private const string BottomScrimName = "__BottomScrim";
    private const string HeaderCardName = "__HeaderCard";
    private const string HeaderBodyName = "__HeaderBody";
    private const string SubtitleName = "__SubtitleText";
    private const string StatusShellName = "__StatusShell";
    private const string RightSidebarName = "__RightSidebar";
    private const string HintCardName = "__HintCard";
    private const string ControlCardName = "__ControlCard";
    private const string TaskCardName = "__TaskCard";

    private bool isApplying;
    private bool applyQueued;

    private static readonly Color TopScrimColor = new Color(0.02f, 0.07f, 0.12f, 0.28f);
    private static readonly Color BottomScrimColor = new Color(0.01f, 0.04f, 0.08f, 0.42f);
    private static readonly Color CardColor = new Color(0.04f, 0.10f, 0.17f, 0.88f);
    private static readonly Color CardStroke = new Color(0.15f, 0.44f, 0.55f, 0.28f);
    private static readonly Color AccentColor = new Color(0.12f, 0.78f, 0.78f, 0.95f);
    private static readonly Color TextPrimary = new Color(0.95f, 0.98f, 1.0f, 1.0f);
    private static readonly Color TextSecondary = new Color(0.62f, 0.77f, 0.84f, 1.0f);
    private static readonly Color StatusBackground = new Color(0.08f, 0.21f, 0.29f, 0.90f);
    private static readonly Color StatusTextColor = new Color(0.73f, 0.97f, 0.94f, 1.0f);

    private void OnEnable()
    {
        RequestApplyTheme();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall -= HandleDelayedApplyTheme;
#endif
        applyQueued = false;
    }

    private void OnValidate()
    {
        RequestApplyTheme();
    }

    private void OnTransformChildrenChanged()
    {
        RequestApplyTheme();
    }

    private void RequestApplyTheme()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (Application.isPlaying)
        {
            ApplyTheme();
            return;
        }

#if UNITY_EDITOR
        if (applyQueued)
        {
            return;
        }

        applyQueued = true;
        EditorApplication.delayCall -= HandleDelayedApplyTheme;
        EditorApplication.delayCall += HandleDelayedApplyTheme;
#else
        ApplyTheme();
#endif
    }

#if UNITY_EDITOR
    private void HandleDelayedApplyTheme()
    {
        EditorApplication.delayCall -= HandleDelayedApplyTheme;
        applyQueued = false;

        if (this == null || !isActiveAndEnabled)
        {
            return;
        }

        ApplyTheme();
    }
#endif

    private void ApplyTheme()
    {
        if (isApplying || !gameObject.scene.IsValid())
        {
            return;
        }

        applyQueued = false;
        isApplying = true;

        try
        {
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            TMP_Text titleText = FindText("TitleText");
            TMP_Text statusText = FindText("StatusText");
            RectTransform controlPanel = FindRect("ControlPanel");
            RectTransform pointPanel = FindRect("PointButton");

            if (titleText == null || statusText == null || controlPanel == null || pointPanel == null)
            {
                return;
            }

            TMP_FontAsset font = preferredFont != null ? preferredFont : titleText.font;

            NormalizeCanvas(canvasRect);
            RemoveLegacySidebar(canvasRect);
            SetupBackdrop(canvasRect);
            SetupHeader(canvasRect, titleText, statusText, font);
            SetupHintCard(canvasRect, font);
            SetupActionCard(
                canvasRect,
                controlPanel,
                ControlCardName,
                "SIMULATION CONTROLS",
                new Vector2(36f, 32f),
                new Vector2(620f, 150f),
                font,
                textAnchor: TextAnchor.LowerLeft);
            SetupActionCard(
                canvasRect,
                pointPanel,
                TaskCardName,
                "TASK TOOLKIT",
                new Vector2(-36f, 32f),
                new Vector2(700f, 150f),
                font,
                textAnchor: TextAnchor.LowerRight);

            StyleButton(FindButton("StartButton"), "开始仿真", new Color(0.06f, 0.60f, 0.47f, 0.96f), font, new Vector2(176f, 58f));
            StyleButton(FindButton("PauseButton"), "暂停", new Color(0.86f, 0.58f, 0.10f, 0.96f), font, new Vector2(146f, 58f));
            StyleButton(FindButton("ResetButton"), "重置", new Color(0.74f, 0.23f, 0.23f, 0.96f), font, new Vector2(146f, 58f));

            StyleButton(FindButton("Btn_AddTask"), "新增任务点", new Color(0.17f, 0.47f, 0.82f, 0.96f), font, new Vector2(182f, 58f));
            StyleButton(FindButton("Btn_Import"), "导入任务", new Color(0.12f, 0.66f, 0.72f, 0.96f), font, new Vector2(172f, 58f));
            StyleButton(FindButton("Btn_clearTask"), "清空任务", new Color(0.29f, 0.35f, 0.42f, 0.96f), font, new Vector2(172f, 58f));
        }
        finally
        {
            isApplying = false;
        }
    }

    private void NormalizeCanvas(RectTransform canvasRect)
    {
        canvasRect.localScale = Vector3.one;
        canvasRect.localRotation = Quaternion.identity;
    }

    private void RemoveLegacySidebar(RectTransform canvasRect)
    {
        Transform legacySidebar = canvasRect.Find(RightSidebarName);
        if (legacySidebar == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(legacySidebar.gameObject);
            return;
        }

#if UNITY_EDITOR
        DestroyImmediate(legacySidebar.gameObject);
#else
        Destroy(legacySidebar.gameObject);
#endif
    }

    private void SetupBackdrop(RectTransform canvasRect)
    {
        RectTransform top = CreateOrGetRect(TopScrimName, canvasRect);
        top.SetSiblingIndex(0);
        StretchTop(top, 180f);
        StyleFlatImage(top.gameObject, TopScrimColor);

        RectTransform bottom = CreateOrGetRect(BottomScrimName, canvasRect);
        bottom.SetSiblingIndex(1);
        StretchBottom(bottom, 180f);
        StyleFlatImage(bottom.gameObject, BottomScrimColor);
    }

    private void SetupHeader(RectTransform canvasRect, TMP_Text titleText, TMP_Text statusText, TMP_FontAsset font)
    {
        RectTransform headerCard = CreateOrGetRect(HeaderCardName, canvasRect);
        AnchorToCorner(headerCard, new Vector2(36f, -32f), new Vector2(480f, 176f), new Vector2(0f, 1f));
        StyleCard(headerCard.gameObject, CardColor, CardStroke);

        RectTransform accent = CreateOrGetRect("__HeaderAccent", headerCard);
        accent.anchorMin = new Vector2(0f, 1f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.pivot = new Vector2(0.5f, 1f);
        accent.offsetMin = new Vector2(24f, -6f);
        accent.offsetMax = new Vector2(-24f, 0f);
        StyleFlatImage(accent.gameObject, AccentColor);

        RectTransform headerBody = CreateOrGetRect(HeaderBodyName, headerCard);
        StretchInside(headerBody, new Vector2(24f, 24f), new Vector2(24f, 26f));
        VerticalLayoutGroup bodyLayout = EnsureVerticalLayout(headerBody.gameObject);
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.spacing = 8f;
        bodyLayout.padding = new RectOffset(0, 0, 14, 0);
        bodyLayout.childControlWidth = true;
        bodyLayout.childControlHeight = false;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        titleText.transform.SetParent(headerBody, false);
        StyleText(titleText, font, 34f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.Left);
        titleText.text = "KY UAV 指挥台";
        EnsureLayoutHeight(titleText.gameObject, 46f);

        TMP_Text subtitle = CreateOrGetText(SubtitleName, headerBody, font);
        StyleText(subtitle, font, 18f, TextSecondary, FontStyles.Normal, TextAlignmentOptions.Left);
        subtitle.text = "多无人机协同巡检与算法仿真平台";
        subtitle.enableWordWrapping = false;
        EnsureLayoutHeight(subtitle.gameObject, 26f);

        RectTransform statusShell = CreateOrGetRect(StatusShellName, headerBody);
        statusShell.SetSiblingIndex(headerBody.childCount - 1);
        StretchAnchored(statusShell, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 44f));
        StyleCard(statusShell.gameObject, StatusBackground, new Color(0.28f, 0.83f, 0.79f, 0.18f));
        EnsureLayoutHeight(statusShell.gameObject, 44f);

        statusText.transform.SetParent(statusShell, false);
        RectTransform statusRect = statusText.rectTransform;
        statusRect.anchorMin = Vector2.zero;
        statusRect.anchorMax = Vector2.one;
        statusRect.offsetMin = new Vector2(18f, 8f);
        statusRect.offsetMax = new Vector2(-18f, -8f);
        StyleText(statusText, font, 18f, StatusTextColor, FontStyles.Bold, TextAlignmentOptions.Left);
        statusText.enableWordWrapping = false;

        if (!Application.isPlaying && statusText.text.StartsWith("Status"))
        {
            statusText.text = "状态：就绪";
        }
    }

    private void SetupHintCard(RectTransform canvasRect, TMP_FontAsset font)
    {
        RectTransform hintCard = CreateOrGetRect(HintCardName, canvasRect);
        AnchorToCorner(hintCard, new Vector2(-36f, -32f), new Vector2(280f, 132f), new Vector2(1f, 1f));
        StyleCard(hintCard.gameObject, new Color(0.03f, 0.09f, 0.14f, 0.82f), new Color(0.21f, 0.49f, 0.64f, 0.22f));

        TMP_Text hintTitle = CreateOrGetText("__HintTitle", hintCard, font);
        RectTransform titleRect = hintTitle.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);
        titleRect.sizeDelta = new Vector2(-40f, 24f);
        StyleText(hintTitle, font, 16f, AccentColor, FontStyles.Bold, TextAlignmentOptions.Left);
        hintTitle.text = "VIEW SHORTCUTS";
        hintTitle.enableWordWrapping = false;

        TMP_Text hintBody = CreateOrGetText("__HintBody", hintCard, font);
        RectTransform bodyRect = hintBody.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(20f, 18f);
        bodyRect.offsetMax = new Vector2(-20f, -48f);
        StyleText(hintBody, font, 18f, TextPrimary, FontStyles.Normal, TextAlignmentOptions.Left);
        hintBody.text = "1  总览视角\n2  跟随视角";
        hintBody.lineSpacing = 10f;
        hintBody.enableWordWrapping = false;
    }

    private void SetupActionCard(
        RectTransform canvasRect,
        RectTransform sourcePanel,
        string cardName,
        string label,
        Vector2 offset,
        Vector2 size,
        TMP_FontAsset font,
        TextAnchor textAnchor)
    {
        RectTransform card = CreateOrGetRect(cardName, canvasRect);
        Vector2 pivot = offset.x >= 0 ? new Vector2(0f, 0f) : new Vector2(1f, 0f);
        AnchorToCorner(card, offset, size, pivot);
        StyleCard(card.gameObject, CardColor, CardStroke);

        TMP_Text cardLabel = CreateOrGetText(cardName + "_Label", card, font);
        RectTransform labelRect = cardLabel.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -16f);
        labelRect.sizeDelta = new Vector2(-44f, 24f);
        StyleText(cardLabel, font, 15f, AccentColor, FontStyles.Bold, ToAlignment(textAnchor));
        cardLabel.text = label;
        cardLabel.enableWordWrapping = false;

        sourcePanel.SetParent(card, false);
        sourcePanel.anchorMin = new Vector2(0f, 0f);
        sourcePanel.anchorMax = new Vector2(1f, 1f);
        sourcePanel.offsetMin = new Vector2(20f, 18f);
        sourcePanel.offsetMax = new Vector2(-20f, -48f);

        Image panelImage = sourcePanel.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0f, 0f, 0f, 0f);
        }

        HorizontalLayoutGroup layout = sourcePanel.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.padding = new RectOffset(0, 0, 0, 0);
        }
    }

    private void StyleButton(Button button, string label, Color fill, TMP_FontAsset font, Vector2 size)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text buttonText = button.GetComponent<TMP_Text>();
        if (buttonText == null)
        {
            return;
        }

        RectTransform buttonRect = button.GetComponent<RectTransform>();
        RectTransform shell = EnsureButtonShell(button, size);
        Image shellImage = EnsureGraphic(shell.gameObject);
        shellImage.color = fill;
        shellImage.type = Image.Type.Simple;
        shellImage.raycastTarget = false;
        EnsureShadow(shell.gameObject, new Color(0f, 0f, 0f, 0.35f), new Vector2(0f, -10f));

        buttonRect.SetParent(shell, false);
        buttonRect.anchorMin = Vector2.zero;
        buttonRect.anchorMax = Vector2.one;
        buttonRect.offsetMin = Vector2.zero;
        buttonRect.offsetMax = Vector2.zero;
        buttonRect.pivot = new Vector2(0.5f, 0.5f);

        button.targetGraphic = shellImage;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = fill;
        colors.highlightedColor = Brighten(fill, 1.08f);
        colors.pressedColor = Brighten(fill, 0.84f);
        colors.selectedColor = Brighten(fill, 1.02f);
        colors.disabledColor = new Color(fill.r * 0.5f, fill.g * 0.5f, fill.b * 0.5f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        StyleText(buttonText, font != null ? font : buttonText.font, 21f, TextPrimary, FontStyles.Bold, TextAlignmentOptions.Center);
        buttonText.text = label;
        buttonText.enableWordWrapping = false;
        buttonText.raycastTarget = true;
    }

    private RectTransform EnsureButtonShell(Button button, Vector2 size)
    {
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        Transform currentParent = buttonRect.parent;

        string shellName = button.gameObject.name + "_Shell";
        RectTransform shell;

        if (currentParent != null && currentParent.name == shellName)
        {
            shell = currentParent as RectTransform;
        }
        else
        {
            int siblingIndex = buttonRect.GetSiblingIndex();
            shell = CreateOrGetRect(shellName, currentParent);
            shell.SetSiblingIndex(siblingIndex);
        }

        shell.anchorMin = new Vector2(0f, 0f);
        shell.anchorMax = new Vector2(0f, 0f);
        shell.pivot = new Vector2(0.5f, 0.5f);
        shell.sizeDelta = size;
        shell.localScale = Vector3.one;
        EnsureLayoutWidth(shell.gameObject, size.x, size.y);

        return shell;
    }

    private TMP_Text FindText(string name)
    {
        Transform child = FindDescendant(transform, name);
        return child != null ? child.GetComponent<TMP_Text>() : null;
    }

    private Button FindButton(string name)
    {
        Transform child = FindDescendant(transform, name);
        return child != null ? child.GetComponent<Button>() : null;
    }

    private RectTransform FindRect(string name)
    {
        Transform child = FindDescendant(transform, name);
        return child as RectTransform;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        if (root.name == name)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDescendant(root.GetChild(i), name);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static RectTransform CreateOrGetRect(string name, Transform parent)
    {
        Transform child = parent.Find(name);
        if (child == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            child = go.transform;
            child.SetParent(parent, false);
        }

        return child as RectTransform;
    }

    private static TMP_Text CreateOrGetText(string name, Transform parent, TMP_FontAsset font)
    {
        Transform existing = parent.Find(name);
        TMP_Text text;

        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            text = go.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = existing.GetComponent<TMP_Text>();
        }

        if (font != null)
        {
            text.font = font;
        }

        return text;
    }

    private static VerticalLayoutGroup EnsureVerticalLayout(GameObject go)
    {
        VerticalLayoutGroup layout = go.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
        {
            layout = go.AddComponent<VerticalLayoutGroup>();
        }

        return layout;
    }

    private static LayoutElement EnsureLayoutWidth(GameObject go, float width, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = go.AddComponent<LayoutElement>();
        }

        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = 0f;
        element.flexibleHeight = 0f;
        return element;
    }

    private static LayoutElement EnsureLayoutHeight(GameObject go, float height)
    {
        LayoutElement element = go.GetComponent<LayoutElement>();
        if (element == null)
        {
            element = go.AddComponent<LayoutElement>();
        }

        element.preferredHeight = height;
        element.flexibleHeight = 0f;
        return element;
    }

    private static Image EnsureGraphic(GameObject go)
    {
        Image image = go.GetComponent<Image>();
        if (image == null)
        {
            image = go.AddComponent<Image>();
        }

        return image;
    }

    private static Shadow EnsureShadow(GameObject go, Color color, Vector2 distance)
    {
        Shadow shadow = go.GetComponent<Shadow>();
        if (shadow == null)
        {
            shadow = go.AddComponent<Shadow>();
        }

        shadow.effectColor = color;
        shadow.effectDistance = distance;
        shadow.useGraphicAlpha = true;
        return shadow;
    }

    private static void StyleCard(GameObject go, Color fill, Color stroke)
    {
        Image image = EnsureGraphic(go);
        image.color = fill;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;

        Outline outline = go.GetComponent<Outline>();
        if (outline == null)
        {
            outline = go.AddComponent<Outline>();
        }

        outline.effectColor = stroke;
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        EnsureShadow(go, new Color(0f, 0f, 0f, 0.24f), new Vector2(0f, -8f));
    }

    private static void StyleFlatImage(GameObject go, Color fill)
    {
        Image image = EnsureGraphic(go);
        image.color = fill;
        image.type = Image.Type.Simple;
        image.raycastTarget = false;
    }

    private static void StyleText(
        TMP_Text text,
        TMP_FontAsset font,
        float size,
        Color color,
        FontStyles style,
        TextAlignmentOptions alignment)
    {
        if (font != null)
        {
            text.font = font;
        }

        text.fontSize = size;
        text.color = color;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.margin = Vector4.zero;
    }

    private static void AnchorToCorner(RectTransform rect, Vector2 anchoredPosition, Vector2 size, Vector2 pivot)
    {
        rect.anchorMin = pivot;
        rect.anchorMax = pivot;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void StretchTop(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -height);
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchBottom(RectTransform rect, float height)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = new Vector2(0f, height);
    }

    private static void StretchInside(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = min;
        rect.offsetMax = -max;
    }

    private static void StretchAnchored(RectTransform rect, Vector2 min, Vector2 max, Vector2 size)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
    }

    private static Color Brighten(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a);
    }

    private static TextAlignmentOptions ToAlignment(TextAnchor anchor)
    {
        return anchor switch
        {
            TextAnchor.LowerRight => TextAlignmentOptions.Right,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.UpperRight => TextAlignmentOptions.Right,
            _ => TextAlignmentOptions.Left
        };
    }
}
