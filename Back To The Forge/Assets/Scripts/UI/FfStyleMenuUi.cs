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
