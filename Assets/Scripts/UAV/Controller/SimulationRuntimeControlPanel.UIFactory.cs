using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SimulationRuntimeControlPanel
{
    private Canvas ResolveCanvas()
    {
        if (simulationManager != null && simulationManager.statusText != null)
        {
            return simulationManager.statusText.canvas;
        }

        return RuntimeSceneRegistry.Get<Canvas>(this);
    }

    private Vector2 GetCanvasSize()
    {
        Canvas canvas = ResolveCanvas();
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        return canvasRect != null ? canvasRect.rect.size : expandedSize;
    }

    private Vector2 GetTargetPanelSize()
    {
        Vector2 canvasSize = GetCanvasSize();
        float width = Mathf.Clamp(canvasSize.x * 0.235f, expandedSize.x, 432f);
        float expandedHeight = Mathf.Clamp(canvasSize.y - 236f, 520f, 760f);
        float collapsedHeight = Mathf.Max(collapsedSize.y, 108f);
        return isExpanded
            ? new Vector2(width, expandedHeight)
            : new Vector2(width, collapsedHeight);
    }

    private RectTransform CreateSectionCard(RectTransform parent, string title, string description)
    {
        GameObject cardObject = new GameObject("SectionCard_" + title, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        RectTransform card = cardObject.GetComponent<RectTransform>();
        card.SetParent(parent, false);

        Image cardImage = card.GetComponent<Image>();
        ConfigureImageGraphic(cardImage);
        cardImage.color = SectionColor;
        cardImage.raycastTarget = false;

        Outline outline = card.GetComponent<Outline>();
        outline.effectColor = new Color(0.12f, 0.34f, 0.44f, 0.18f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        VerticalLayoutGroup layout = card.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = card.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_Text titleText = CreateText("Title", card, title, 13f, AccentColor, FontStyles.Bold);
        titleText.alignment = TextAlignmentOptions.Left;
        titleText.enableWordWrapping = false;

        if (!string.IsNullOrWhiteSpace(description))
        {
            TMP_Text descriptionText = CreateText("Description", card, description, 11f, SecondaryTextColor, FontStyles.Normal);
            descriptionText.alignment = TextAlignmentOptions.Left;
            descriptionText.enableWordWrapping = true;
            descriptionText.overflowMode = TextOverflowModes.Overflow;
        }

        RectTransform contentRoot = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        contentRoot.SetParent(card, false);
        VerticalLayoutGroup contentLayout = contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        contentLayout.spacing = 6f;
        contentLayout.childAlignment = TextAnchor.UpperLeft;
        contentLayout.childControlWidth = true;
        contentLayout.childControlHeight = true;
        contentLayout.childForceExpandWidth = true;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter contentFitter = contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return contentRoot;
    }

    private void CreateSectionLabel(RectTransform parent, string label)
    {
        RectTransform labelRoot = new GameObject("Section_" + label, typeof(RectTransform)).GetComponent<RectTransform>();
        labelRoot.SetParent(parent, false);

        LayoutElement layout = labelRoot.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 18f;

        TMP_Text labelText = CreateText("Label", labelRoot, label, 12f, AccentColor, FontStyles.Bold);
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = Vector2.one;
        labelText.rectTransform.offsetMin = new Vector2(2f, 0f);
        labelText.rectTransform.offsetMax = new Vector2(-2f, 0f);
    }

    private void CreateStepperRow(
        RectTransform parent,
        string label,
        out TMP_Text valueText,
        Action onDecreaseClicked,
        Action onIncreaseClicked)
    {
        CreateStepperRow(parent, label, out valueText, onDecreaseClicked, onIncreaseClicked, 84f);
    }

    private void CreateStepperRow(
        RectTransform parent,
        string label,
        out TMP_Text valueText,
        Action onDecreaseClicked,
        Action onIncreaseClicked,
        float valueWidth)
    {
        RectTransform row = CreateRow(parent, label);
        CreateActionButton(row, "-", SecondaryButtonColor, onDecreaseClicked, 24f);
        valueText = CreateValueBadge(row, "Value", valueWidth);
        CreateActionButton(row, "+", PrimaryButtonColor, onIncreaseClicked, 24f);
    }

    private void CreateToggleRow(RectTransform parent, string label, out Button toggleButton, Action onClicked)
    {
        RectTransform row = CreateRow(parent, label);
        toggleButton = CreateActionButton(row, "ON", PositiveButtonColor, onClicked, 72f);
    }

    private void CreateButtonStripRow(RectTransform parent, string label, ButtonAction[] actions)
    {
        CreateButtonStripRow(parent, label, actions, out _, out _, out _, out _);
    }

    private void CreateButtonStripRow(
        RectTransform parent,
        string label,
        ButtonAction[] actions,
        out Button firstButton,
        out Button secondButton,
        out Button thirdButton,
        out Button fourthButton)
    {
        GameObject rowObject = new GameObject("ActionRow_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = actions.Length >= 4 ? 86f : 74f;
        rowLayout.minHeight = rowLayout.preferredHeight;

        Image rowImage = row.GetComponent<Image>();
        ConfigureImageGraphic(rowImage);
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        VerticalLayoutGroup verticalLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(10, 10, 7, 7);
        verticalLayout.spacing = 6f;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText("Label", row, label, 12f, SecondaryTextColor, FontStyles.Bold);
        labelText.alignment = TextAlignmentOptions.Left;
        labelText.enableWordWrapping = false;

        RectTransform buttonRow = new GameObject("Buttons", typeof(RectTransform)).GetComponent<RectTransform>();
        buttonRow.SetParent(row, false);
        HorizontalLayoutGroup buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 6f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;

        LayoutElement buttonRowLayout = buttonRow.gameObject.AddComponent<LayoutElement>();
        buttonRowLayout.preferredHeight = 34f;

        firstButton = null;
        secondButton = null;
        thirdButton = null;
        fourthButton = null;
        for (int i = 0; i < actions.Length; i++)
        {
            Button button = CreateActionButton(buttonRow, actions[i].label, actions[i].color, actions[i].callback, actions[i].width, flexibleWidth: true);
            switch (i)
            {
                case 0:
                    firstButton = button;
                    break;
                case 1:
                    secondButton = button;
                    break;
                case 2:
                    thirdButton = button;
                    break;
                case 3:
                    fourthButton = button;
                    break;
            }
        }
    }

    private void CreateInputButtonRow(
        RectTransform parent,
        string label,
        out TMP_InputField inputField,
        ButtonAction[] actions)
    {
        GameObject rowObject = new GameObject("Row_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        LayoutElement rowLayout = row.gameObject.AddComponent<LayoutElement>();
        rowLayout.preferredHeight = 96f;
        rowLayout.minHeight = 96f;

        Image rowImage = row.GetComponent<Image>();
        ConfigureImageGraphic(rowImage);
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        VerticalLayoutGroup verticalLayout = row.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.padding = new RectOffset(10, 10, 8, 8);
        verticalLayout.spacing = 6f;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = true;
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandWidth = true;
        verticalLayout.childForceExpandHeight = false;

        RectTransform inputRow = new GameObject("InputRow", typeof(RectTransform)).GetComponent<RectTransform>();
        inputRow.SetParent(row, false);
        HorizontalLayoutGroup inputLayout = inputRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        inputLayout.spacing = 6f;
        inputLayout.childAlignment = TextAnchor.MiddleLeft;
        inputLayout.childControlWidth = true;
        inputLayout.childControlHeight = true;
        inputLayout.childForceExpandWidth = true;
        inputLayout.childForceExpandHeight = false;
        LayoutElement inputRowLayout = inputRow.gameObject.AddComponent<LayoutElement>();
        inputRowLayout.preferredHeight = 28f;

        TMP_Text labelText = CreateText("Label", inputRow, label, 13f, SecondaryTextColor, FontStyles.Bold);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 52f;
        labelLayout.minWidth = 52f;
        labelLayout.flexibleWidth = 0f;
        labelText.alignment = TextAlignmentOptions.Left;

        inputField = CreateInputField(inputRow, label + "_Input", "输入导出目录");

        RectTransform buttonRow = new GameObject("ButtonRow", typeof(RectTransform)).GetComponent<RectTransform>();
        buttonRow.SetParent(row, false);
        HorizontalLayoutGroup buttonLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 6f;
        buttonLayout.childAlignment = TextAnchor.MiddleRight;
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = true;
        buttonLayout.childForceExpandHeight = false;
        LayoutElement buttonRowLayout = buttonRow.gameObject.AddComponent<LayoutElement>();
        buttonRowLayout.preferredHeight = 32f;

        for (int i = 0; i < actions.Length; i++)
        {
            CreateActionButton(buttonRow, actions[i].label, actions[i].color, actions[i].callback, actions[i].width, flexibleWidth: true);
        }
    }

    private void CreateInfoCard(
        RectTransform parent,
        string name,
        out TMP_Text bodyText,
        out LayoutElement layoutElement,
        float preferredHeight)
    {
        GameObject cardObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform card = cardObject.GetComponent<RectTransform>();
        card.SetParent(parent, false);

        layoutElement = card.gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = preferredHeight;
        layoutElement.preferredHeight = preferredHeight;

        Image cardImage = card.GetComponent<Image>();
        ConfigureImageGraphic(cardImage);
        cardImage.color = RowColor;
        cardImage.raycastTarget = false;

        bodyText = CreateText("BodyText", card, string.Empty, 12.5f, PrimaryTextColor, FontStyles.Normal);
        bodyText.enableWordWrapping = true;
        bodyText.overflowMode = TextOverflowModes.Overflow;
        bodyText.alignment = TextAlignmentOptions.TopLeft;
        bodyText.rectTransform.anchorMin = Vector2.zero;
        bodyText.rectTransform.anchorMax = Vector2.one;
        bodyText.rectTransform.offsetMin = new Vector2(10f, 8f);
        bodyText.rectTransform.offsetMax = new Vector2(-10f, -8f);
    }

    private RectTransform CreateRow(RectTransform parent, string label)
    {
        GameObject rowObject = new GameObject("Row_" + label, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform row = rowObject.GetComponent<RectTransform>();
        row.SetParent(parent, false);

        LayoutElement layoutElement = row.gameObject.AddComponent<LayoutElement>();
        layoutElement.preferredHeight = 40f;
        layoutElement.minHeight = 40f;

        Image rowImage = row.GetComponent<Image>();
        ConfigureImageGraphic(rowImage);
        rowImage.color = RowColor;
        rowImage.raycastTarget = false;

        HorizontalLayoutGroup layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 6, 6);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        TMP_Text labelText = CreateText("Label", row, label, 13f, SecondaryTextColor, FontStyles.Bold);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 62f;
        labelLayout.minWidth = 62f;
        labelLayout.flexibleWidth = 0f;
        labelText.alignment = TextAlignmentOptions.Left;

        return row;
    }

    private TMP_Text CreateValueBadge(RectTransform parent, string name, float width)
    {
        GameObject badgeObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform badge = badgeObject.GetComponent<RectTransform>();
        badge.SetParent(parent, false);

        Image badgeImage = badge.GetComponent<Image>();
        ConfigureImageGraphic(badgeImage);
        badgeImage.color = new Color(0.11f, 0.23f, 0.31f, 0.98f);
        badgeImage.raycastTarget = false;

        LayoutElement layout = badge.gameObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = Mathf.Min(88f, width);
        layout.preferredHeight = 28f;
        layout.flexibleWidth = 1f;

        TMP_Text valueText = CreateText("ValueText", badge, "-", 12.5f, AccentColor, FontStyles.Bold);
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.enableAutoSizing = true;
        valueText.fontSizeMin = 10f;
        valueText.fontSizeMax = 12.5f;
        valueText.rectTransform.anchorMin = Vector2.zero;
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = Vector2.zero;
        return valueText;
    }

    private TMP_InputField CreateInputField(RectTransform parent, string name, string placeholderText)
    {
        GameObject inputObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(TMP_InputField));
        RectTransform rect = inputObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement layout = inputObject.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 150f;
        layout.preferredHeight = 28f;

        Image image = inputObject.GetComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = new Color(0.09f, 0.20f, 0.28f, 0.98f);
        image.raycastTarget = true;

        TMP_InputField inputField = inputObject.GetComponent<TMP_InputField>();
        inputField.transition = Selectable.Transition.ColorTint;
        inputField.colors = BuildColorBlock(new Color(0.09f, 0.20f, 0.28f, 0.98f));
        inputField.contentType = TMP_InputField.ContentType.Standard;
        inputField.lineType = TMP_InputField.LineType.SingleLine;
        inputField.characterLimit = 260;
        inputField.customCaretColor = true;
        inputField.caretColor = AccentColor;

        RectTransform textArea = new GameObject("TextArea", typeof(RectTransform)).GetComponent<RectTransform>();
        textArea.SetParent(rect, false);
        textArea.anchorMin = Vector2.zero;
        textArea.anchorMax = Vector2.one;
        textArea.offsetMin = new Vector2(8f, 3f);
        textArea.offsetMax = new Vector2(-8f, -3f);

        TextMeshProUGUI textComponent = (TextMeshProUGUI)CreateText("Text", textArea, string.Empty, 11f, PrimaryTextColor, FontStyles.Normal);
        textComponent.alignment = TextAlignmentOptions.Left;
        textComponent.enableWordWrapping = false;
        textComponent.overflowMode = TextOverflowModes.Ellipsis;
        textComponent.rectTransform.anchorMin = Vector2.zero;
        textComponent.rectTransform.anchorMax = Vector2.one;
        textComponent.rectTransform.offsetMin = Vector2.zero;
        textComponent.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI placeholder = (TextMeshProUGUI)CreateText(
            "Placeholder",
            textArea,
            placeholderText,
            11f,
            new Color(0.60f, 0.73f, 0.80f, 0.68f),
            FontStyles.Normal);
        placeholder.alignment = TextAlignmentOptions.Left;
        placeholder.enableWordWrapping = false;
        placeholder.overflowMode = TextOverflowModes.Ellipsis;
        placeholder.rectTransform.anchorMin = Vector2.zero;
        placeholder.rectTransform.anchorMax = Vector2.one;
        placeholder.rectTransform.offsetMin = Vector2.zero;
        placeholder.rectTransform.offsetMax = Vector2.zero;

        inputField.textViewport = textArea;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        return inputField;
    }

    private Button CreateActionButton(RectTransform parent, string label, Color color, Action onClicked, float width, bool flexibleWidth = false)
    {
        GameObject buttonObject = new GameObject(label + "_Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        LayoutElement layout = buttonObject.AddComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.minWidth = flexibleWidth ? 0f : width;
        layout.flexibleWidth = flexibleWidth ? 1f : 0f;
        layout.preferredHeight = 28f;

        Image image = buttonObject.GetComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = color;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = BuildColorBlock(color);
        if (onClicked != null)
        {
            button.onClick.AddListener(() => onClicked());
        }

        TMP_Text buttonText = CreateText("Label", rect, label, 12f, PrimaryTextColor, FontStyles.Bold);
        buttonText.alignment = TextAlignmentOptions.Center;
        buttonText.enableAutoSizing = true;
        buttonText.fontSizeMin = 9f;
        buttonText.fontSizeMax = 12f;
        buttonText.rectTransform.anchorMin = Vector2.zero;
        buttonText.rectTransform.anchorMax = Vector2.one;
        buttonText.rectTransform.offsetMin = new Vector2(4f, 0f);
        buttonText.rectTransform.offsetMax = new Vector2(-4f, 0f);
        return button;
    }

    private ColorBlock BuildColorBlock(Color baseColor)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = baseColor;
        colors.highlightedColor = new Color(
            Mathf.Clamp01(baseColor.r * 1.08f),
            Mathf.Clamp01(baseColor.g * 1.08f),
            Mathf.Clamp01(baseColor.b * 1.08f),
            baseColor.a);
        colors.pressedColor = new Color(
            Mathf.Clamp01(baseColor.r * 0.84f),
            Mathf.Clamp01(baseColor.g * 0.84f),
            Mathf.Clamp01(baseColor.b * 0.84f),
            baseColor.a);
        colors.selectedColor = baseColor;
        colors.disabledColor = new Color(baseColor.r * 0.45f, baseColor.g * 0.45f, baseColor.b * 0.45f, 0.55f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        return colors;
    }

    private TMP_Text CreateText(string name, RectTransform parent, string content, float fontSize, Color color, FontStyles fontStyle)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.color = color;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        if (runtimeFont != null)
        {
            text.font = runtimeFont;
        }

        return text;
    }

    private RectTransform CreatePanelArea(string name, RectTransform parent, Vector2 topLeftOffset, Vector2 bottomRightOffset)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(topLeftOffset.x, bottomRightOffset.y);
        rect.offsetMax = new Vector2(bottomRightOffset.x, topLeftOffset.y);
        return rect;
    }

    private void ConfigureSection(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        ConfigureImageGraphic(image);
        image.color = color;
        image.raycastTarget = false;
    }

    private void UpdateToggleButton(Button button, bool isEnabled)
    {
        if (button == null)
        {
            return;
        }

        Color baseColor = isEnabled ? PositiveButtonColor : NegativeButtonColor;
        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = baseColor;
        }

        button.colors = BuildColorBlock(baseColor);

        TMP_Text label = button.GetComponentInChildren<TMP_Text>();
        if (label != null)
        {
            label.text = isEnabled ? "ON" : "OFF";
        }
    }

    private void ConfigureImageGraphic(Image image)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
    }

    private void ClearChildren(RectTransform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private int WrapIndex(int value, int length)
    {
        if (length <= 0)
        {
            return 0;
        }

        if (value < 0)
        {
            return length - 1;
        }

        if (value >= length)
        {
            return 0;
        }

        return value;
    }

    private readonly struct ButtonAction
    {
        public readonly string label;
        public readonly Color color;
        public readonly Action callback;
        public readonly float width;

        public ButtonAction(string label, Color color, Action callback, float width)
        {
            this.label = label;
            this.color = color;
            this.callback = callback;
            this.width = width;
        }
    }
}
