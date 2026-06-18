using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Shared Final Fantasy–style menu panel (blue border, command rows) used by main and pause menus.
/// </summary>
public static class FfStyleMenuUi
{
    public const string StyleSheetResource = "Combat/CombatBattleHud";
    public const string ThemeResource = "Combat/UnityDefaultRuntimeTheme";

    public static readonly Color TextColor = new(1f, 1f, 1f, 1f);
    public static readonly Color DisabledTextColor = new(0.65f, 0.65f, 0.7f, 1f);
    public static readonly Color SubtitleColor = new(0.85f, 0.85f, 0.95f, 1f);

    public readonly struct MenuRow
    {
        public readonly string Label;
        public readonly bool Enabled;
        public readonly Action OnSelect;

        public MenuRow(string label, Action onSelect, bool enabled = true)
        {
            Label = label;
            OnSelect = onSelect;
            Enabled = enabled;
        }
    }

    public static void ConfigureDocument(UIDocument document, int sortingOrder)
    {
        if (document == null)
            return;

        if (document.panelSettings == null)
        {
            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "FfStyleMenuPanelSettings";
            TryAssignDefaultTheme(panelSettings);
            document.panelSettings = panelSettings;
        }

        var ps = document.panelSettings;
        TryAssignDefaultTheme(ps);
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(800, 600);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 0.5f;
        ps.sortingOrder = sortingOrder;

        document.visualTreeAsset = null;
        document.sortingOrder = sortingOrder;
    }

    public static VisualElement BuildScreen(
        VisualElement documentRoot,
        string title,
        string subtitle,
        out VisualElement commandsList)
    {
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.pickingMode = PickingMode.Ignore;

        var styleSheet = Resources.Load<StyleSheet>(StyleSheetResource);
        if (styleSheet != null)
            documentRoot.styleSheets.Add(styleSheet);

        var overlay = new VisualElement { name = "menu-overlay" };
        overlay.style.flexGrow = 1f;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        overlay.pickingMode = PickingMode.Position;
        documentRoot.Add(overlay);

        var panel = new VisualElement { name = "menu-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.minWidth = 280;
        panel.style.maxWidth = 360;
        panel.style.paddingTop = 16;
        panel.style.paddingBottom = 16;
        panel.style.paddingLeft = 20;
        panel.style.paddingRight = 20;
        overlay.Add(panel);

        var titleLabel = new Label(title);
        titleLabel.name = "menu-title";
        ApplyLabelStyle(titleLabel, 22f, true);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.marginBottom = 12;
        panel.Add(titleLabel);

        var subtitleLabel = new Label(subtitle);
        subtitleLabel.name = "menu-subtitle";
        ApplyLabelStyle(subtitleLabel, 13f, false, SubtitleColor);
        subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitleLabel.style.marginBottom = 14;
        panel.Add(subtitleLabel);

        commandsList = new VisualElement { name = "commands-list" };
        commandsList.AddToClassList("command-list");
        commandsList.pickingMode = PickingMode.Position;
        panel.Add(commandsList);

        return overlay;
    }

    public static void RefreshCommandRows(
        VisualElement commandsList,
        IReadOnlyList<MenuRow> rows,
        int selectedIndex,
        Action<int> onSelectedIndexChanged,
        Action<int> onActivate)
    {
        if (commandsList == null)
            return;

        commandsList.Clear();

        if (rows == null || rows.Count == 0)
            return;

        if (selectedIndex < 0 || selectedIndex >= rows.Count)
            selectedIndex = 0;

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var index = i;
            var isSelected = i == selectedIndex;

            var rowElement = new VisualElement();
            rowElement.AddToClassList("command-row");
            rowElement.style.flexDirection = FlexDirection.Row;
            rowElement.style.alignItems = Align.Center;
            rowElement.pickingMode = row.Enabled ? PickingMode.Position : PickingMode.Ignore;
            rowElement.focusable = row.Enabled;

            if (isSelected && row.Enabled)
            {
                rowElement.AddToClassList("command-row--selected");
                rowElement.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
            }

            var cursor = new Label("\u25ba");
            cursor.AddToClassList("command-cursor");
            ApplyLabelStyle(cursor, 14f, false, row.Enabled ? TextColor : DisabledTextColor);
            cursor.style.width = 18;
            cursor.style.visibility = isSelected && row.Enabled ? Visibility.Visible : Visibility.Hidden;
            rowElement.Add(cursor);

            var label = new Label(row.Label);
            label.AddToClassList("command-label");
            ApplyLabelStyle(label, 17f, true, row.Enabled ? TextColor : DisabledTextColor);
            rowElement.Add(label);

            if (row.Enabled)
            {
                rowElement.RegisterCallback<ClickEvent>(_ =>
                {
                    onSelectedIndexChanged?.Invoke(index);
                    onActivate?.Invoke(index);
                });
                rowElement.RegisterCallback<PointerUpEvent>(evt =>
                {
                    if (evt.button != 0)
                        return;

                    onSelectedIndexChanged?.Invoke(index);
                    onActivate?.Invoke(index);
                });
            }

            commandsList.Add(rowElement);
        }
    }

    public static void ApplyLabelStyle(Label label, float fontSize, bool bold, Color? color = null)
    {
        label.style.color = color ?? TextColor;
        label.style.fontSize = fontSize;
        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;
    }

    /// <summary>Bottom dialogue box matching menu panels (speaker + body).</summary>
    public static VisualElement BuildDialoguePanel(
        VisualElement documentRoot,
        out Label speakerLabel,
        out Label lineLabel)
    {
        var overlay = BuildDialogueOverlayShell(documentRoot, out var panel);

        speakerLabel = new Label { name = "dialogue-speaker" };
        ApplyLabelStyle(speakerLabel, 18f, true, SubtitleColor);
        speakerLabel.style.marginBottom = 8;
        panel.Add(speakerLabel);

        lineLabel = new Label { name = "dialogue-line" };
        ApplyLabelStyle(lineLabel, 16f, false);
        lineLabel.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(lineLabel);

        return overlay;
    }

    /// <summary>
    /// Party merc chat: same shell as <see cref="BuildDialoguePanel"/>, plus morale hint and a reply field.
    /// </summary>
    public static VisualElement BuildCompanionConversationPanel(
        VisualElement documentRoot,
        out Label speakerLabel,
        out Label lineLabel,
        out Label statusLabel,
        out TextField inputField)
    {
        var overlay = BuildDialogueOverlayShell(documentRoot, out var panel);

        speakerLabel = new Label { name = "dialogue-speaker" };
        ApplyLabelStyle(speakerLabel, 18f, true, SubtitleColor);
        speakerLabel.style.marginBottom = 8;
        panel.Add(speakerLabel);

        lineLabel = new Label { name = "dialogue-line" };
        ApplyLabelStyle(lineLabel, 16f, false);
        lineLabel.style.whiteSpace = WhiteSpace.Normal;
        lineLabel.style.marginBottom = 6;
        panel.Add(lineLabel);

        statusLabel = new Label { name = "companion-dialogue-status" };
        ApplyLabelStyle(statusLabel, 12f, false, SubtitleColor);
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.marginBottom = 8;
        panel.Add(statusLabel);

        var inputRow = new VisualElement { name = "companion-dialogue-input-row" };
        inputRow.AddToClassList("companion-dialogue-input-row");
        panel.Add(inputRow);

        var inputLabel = new Label("›");
        ApplyLabelStyle(inputLabel, 18f, true);
        inputLabel.style.marginRight = 6;
        inputRow.Add(inputLabel);

        inputField = new TextField { name = "companion-dialogue-input" };
        inputField.AddToClassList("companion-dialogue-input");
        inputField.style.flexGrow = 1f;
        inputRow.Add(inputField);

        return overlay;
    }

    /// <summary>Top-left quest objective — same panel and typography as dialogue UI.</summary>
    public static VisualElement BuildQuestObjectivePanel(
        VisualElement documentRoot,
        string headerText,
        out Label headerLabel,
        out Label bodyLabel)
    {
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.style.position = Position.Absolute;
        documentRoot.style.left = 0;
        documentRoot.style.right = 0;
        documentRoot.style.top = 0;
        documentRoot.style.bottom = 0;
        documentRoot.pickingMode = PickingMode.Ignore;

        AttachStyleSheet(documentRoot);

        var panel = new VisualElement { name = "dialogue-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.position = Position.Absolute;
        panel.style.top = 16;
        panel.style.left = 16;
        panel.style.flexGrow = 0f;
        panel.style.flexShrink = 0f;
        panel.style.flexBasis = StyleKeyword.Auto;
        panel.style.minWidth = 220;
        panel.style.maxWidth = 420;
        panel.style.paddingTop = 12;
        panel.style.paddingBottom = 12;
        panel.style.paddingLeft = 16;
        panel.style.paddingRight = 16;
        panel.pickingMode = PickingMode.Ignore;
        documentRoot.Add(panel);

        headerLabel = new Label { name = "dialogue-speaker", text = headerText };
        ApplyLabelStyle(headerLabel, 18f, true, SubtitleColor);
        headerLabel.style.marginBottom = 8;
        panel.Add(headerLabel);

        bodyLabel = new Label { name = "dialogue-line" };
        ApplyLabelStyle(bodyLabel, 16f, false);
        bodyLabel.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(bodyLabel);

        return panel;
    }

    private static VisualElement BuildDialogueOverlayShell(VisualElement documentRoot, out VisualElement panel)
    {
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.style.position = Position.Absolute;
        documentRoot.style.left = 0;
        documentRoot.style.right = 0;
        documentRoot.style.top = 0;
        documentRoot.style.bottom = 0;
        documentRoot.pickingMode = PickingMode.Position;

        AttachStyleSheet(documentRoot);

        var overlay = new VisualElement { name = "dialogue-overlay" };
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0;
        overlay.style.right = 0;
        overlay.style.top = 0;
        overlay.style.bottom = 0;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        overlay.pickingMode = PickingMode.Position;
        documentRoot.Add(overlay);

        var host = new VisualElement { name = "dialogue-host" };
        host.style.position = Position.Absolute;
        host.style.left = 0;
        host.style.right = 0;
        host.style.bottom = 0;
        host.style.paddingLeft = 16;
        host.style.paddingRight = 16;
        host.style.paddingBottom = 20;
        host.style.paddingTop = 0;
        host.style.flexDirection = FlexDirection.Column;
        host.style.alignItems = Align.Stretch;
        host.pickingMode = PickingMode.Position;
        overlay.Add(host);

        panel = new VisualElement { name = "dialogue-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.flexGrow = 0f;
        panel.style.flexShrink = 0f;
        panel.style.flexBasis = StyleKeyword.Auto;
        panel.style.minHeight = 120;
        panel.style.width = Length.Percent(100);
        panel.style.maxWidth = 720;
        panel.style.alignSelf = Align.Center;
        panel.style.paddingTop = 12;
        panel.style.paddingBottom = 12;
        panel.style.paddingLeft = 16;
        panel.style.paddingRight = 16;
        host.Add(panel);

        return overlay;
    }

    public static VisualElement BuildClickableCommandRow(string label, Action onClick, bool enabled = true)
    {
        var row = new VisualElement();
        row.AddToClassList("command-row");
        row.pickingMode = PickingMode.Position;
        if (!enabled)
            row.AddToClassList("command-row--disabled");

        var cursor = new Label("▶");
        cursor.AddToClassList("command-cursor");
        row.Add(cursor);

        var text = new Label(label);
        text.AddToClassList("command-label");
        text.pickingMode = PickingMode.Ignore;
        row.Add(text);

        if (enabled && onClick != null)
        {
            row.RegisterCallback<ClickEvent>(_ => onClick());
            row.RegisterCallback<PointerEnterEvent>(_ => row.AddToClassList("command-row--selected"));
            row.RegisterCallback<PointerLeaveEvent>(_ => row.RemoveFromClassList("command-row--selected"));
        }

        return row;
    }

    /// <summary>Small anchored HUD panel (gold, inventory, etc.).</summary>
    public static VisualElement BuildAnchoredHudPanel(
        VisualElement documentRoot,
        string elementName,
        float top,
        float right,
        float minWidth,
        out Label contentLabel)
    {
        AttachStyleSheet(documentRoot);

        var panel = new VisualElement { name = elementName };
        panel.AddToClassList("hud-panel");
        panel.style.position = Position.Absolute;
        panel.style.top = top;
        panel.style.right = right;
        panel.style.minWidth = minWidth;
        panel.style.paddingTop = 8;
        panel.style.paddingBottom = 8;
        panel.style.paddingLeft = 12;
        panel.style.paddingRight = 12;
        panel.pickingMode = PickingMode.Ignore;
        documentRoot.Add(panel);

        contentLabel = new Label();
        ApplyLabelStyle(contentLabel, 15f, true);
        contentLabel.style.whiteSpace = WhiteSpace.Normal;
        panel.Add(contentLabel);

        return panel;
    }

    /// <summary>Centered command menu without title (forge choices, etc.).</summary>
    public static VisualElement BuildChoiceOverlay(
        VisualElement documentRoot,
        string subtitle,
        out VisualElement commandsList)
    {
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.pickingMode = PickingMode.Position;

        AttachStyleSheet(documentRoot);

        var overlay = new VisualElement { name = "choice-overlay" };
        overlay.style.flexGrow = 1f;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.alignItems = Align.Center;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.2f);
        overlay.pickingMode = PickingMode.Position;
        documentRoot.Add(overlay);

        var panel = new VisualElement { name = "choice-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.minWidth = 300;
        panel.style.maxWidth = 420;
        panel.style.paddingTop = 14;
        panel.style.paddingBottom = 14;
        panel.style.paddingLeft = 18;
        panel.style.paddingRight = 18;
        overlay.Add(panel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            var subtitleLabel = new Label(subtitle);
            ApplyLabelStyle(subtitleLabel, 13f, false, SubtitleColor);
            subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            subtitleLabel.style.marginBottom = 10;
            panel.Add(subtitleLabel);
        }

        commandsList = new VisualElement { name = "commands-list" };
        commandsList.AddToClassList("command-list");
        commandsList.pickingMode = PickingMode.Position;
        panel.Add(commandsList);

        return overlay;
    }

    /// <summary>Left-bottom inventory panel matching main / pause menus.</summary>
    public static VisualElement BuildInventoryOverlay(
        VisualElement documentRoot,
        string title,
        string subtitle,
        out VisualElement slotList,
        out Label goldLabel)
    {
        goldLabel = null;
        documentRoot.Clear();
        documentRoot.style.flexGrow = 1f;
        documentRoot.pickingMode = PickingMode.Ignore;

        AttachStyleSheet(documentRoot);

        var overlay = new VisualElement { name = "inventory-overlay" };
        overlay.style.flexGrow = 1f;
        overlay.style.justifyContent = Justify.FlexEnd;
        overlay.style.alignItems = Align.FlexStart;
        overlay.style.paddingLeft = 20;
        overlay.style.paddingBottom = 24;
        overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.15f);
        overlay.pickingMode = PickingMode.Ignore;
        documentRoot.Add(overlay);

        var panel = new VisualElement { name = "inventory-panel" };
        panel.AddToClassList("hud-panel");
        panel.style.flexDirection = FlexDirection.Column;
        panel.style.minWidth = 280;
        panel.style.maxWidth = 400;
        panel.style.paddingTop = 14;
        panel.style.paddingBottom = 14;
        panel.style.paddingLeft = 16;
        panel.style.paddingRight = 16;
        overlay.Add(panel);

        var titleLabel = new Label(title);
        titleLabel.name = "inventory-title";
        ApplyLabelStyle(titleLabel, 20f, true);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.marginBottom = 6;
        panel.Add(titleLabel);

        var subtitleLabel = new Label(subtitle);
        subtitleLabel.name = "inventory-subtitle";
        ApplyLabelStyle(subtitleLabel, 12f, false, SubtitleColor);
        subtitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        subtitleLabel.style.marginBottom = 10;
        panel.Add(subtitleLabel);

        slotList = new VisualElement { name = "inventory-slots" };
        slotList.AddToClassList("command-list");
        slotList.style.flexGrow = 1f;
        slotList.style.flexShrink = 1f;
        slotList.pickingMode = PickingMode.Ignore;
        panel.Add(slotList);

        var footer = new VisualElement { name = "inventory-footer" };
        footer.style.flexDirection = FlexDirection.Row;
        footer.style.justifyContent = Justify.FlexEnd;
        footer.style.alignItems = Align.FlexEnd;
        footer.style.marginTop = 8;
        footer.style.paddingTop = 4;
        footer.pickingMode = PickingMode.Ignore;
        panel.Add(footer);

        goldLabel = new Label { name = "inventory-gold", text = "Gold: 0" };
        ApplyLabelStyle(goldLabel, 15f, true);
        goldLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        goldLabel.style.whiteSpace = WhiteSpace.Normal;
        footer.Add(goldLabel);

        return overlay;
    }

    public static void RefreshInventorySlotRows(VisualElement slotList, IReadOnlyList<string> slotLines)
    {
        if (slotList == null)
            return;

        slotList.Clear();

        if (slotLines == null || slotLines.Count == 0)
            return;

        for (var i = 0; i < slotLines.Count; i++)
        {
            var row = new VisualElement();
            row.AddToClassList("command-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.minHeight = 22;
            row.pickingMode = PickingMode.Ignore;

            var cursor = new Label("\u00a0");
            cursor.AddToClassList("command-cursor");
            cursor.style.width = 18;
            cursor.style.visibility = Visibility.Hidden;
            row.Add(cursor);

            var label = new Label(slotLines[i]);
            label.AddToClassList("command-label");
            ApplyLabelStyle(label, 15f, true);
            row.Add(label);

            slotList.Add(row);
        }
    }

    public static void RefreshControlReferenceRows(
        VisualElement container,
        IReadOnlyList<GameControlsReference.Entry> bindings)
    {
        if (container == null)
            return;

        container.Clear();

        if (bindings == null || bindings.Count == 0)
            return;

        for (var i = 0; i < bindings.Count; i++)
        {
            var binding = bindings[i];
            var row = new VisualElement();
            row.name = $"control-row-{i}";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.marginBottom = 8;
            row.pickingMode = PickingMode.Ignore;

            var keys = new Label(binding.Keys);
            keys.style.minWidth = 148;
            keys.style.maxWidth = 148;
            keys.style.whiteSpace = WhiteSpace.Normal;
            ApplyLabelStyle(keys, 14f, true);

            var desc = new Label(binding.Description);
            desc.style.flexGrow = 1f;
            desc.style.flexShrink = 1f;
            desc.style.whiteSpace = WhiteSpace.Normal;
            ApplyLabelStyle(desc, 13f, false, SubtitleColor);

            row.Add(keys);
            row.Add(desc);
            container.Add(row);
        }
    }

    public static void AttachStyleSheet(VisualElement root)
    {
        if (root == null)
            return;

        var styleSheet = Resources.Load<StyleSheet>(StyleSheetResource);
        if (styleSheet != null && !root.styleSheets.Contains(styleSheet))
            root.styleSheets.Add(styleSheet);
    }

    public static void ReleaseFocus(UIDocument document)
    {
        if (document == null)
            return;

        var root = document.rootVisualElement;
        if (root == null)
            return;

        root.focusController?.focusedElement?.Blur();
        if (root.panel != null)
            root.panel.focusController?.focusedElement?.Blur();
    }

    private static void TryAssignDefaultTheme(PanelSettings panelSettings)
    {
        if (panelSettings == null || panelSettings.themeStyleSheet != null)
            return;

        var theme = Resources.Load<ThemeStyleSheet>(ThemeResource);
#if UNITY_EDITOR
        if (theme == null)
        {
            theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Packages/com.unity.ui/PackageResources/StyleSheets/UnityThemes/UnityDefaultRuntimeTheme.tss");
        }
#endif
        if (theme != null)
            panelSettings.themeStyleSheet = theme;
    }
}
