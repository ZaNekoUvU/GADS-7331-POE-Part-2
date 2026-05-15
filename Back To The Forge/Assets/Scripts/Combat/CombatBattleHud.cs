using System;

using System.Collections;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;

using UnityEngine.UIElements;

#if UNITY_EDITOR

using UnityEditor;

#endif



/// <summary>

/// FF1-style bottom battle bar: allies + HP (left), commands / target pick (center), enemies + HP (right).

/// </summary>

[DefaultExecutionOrder(50)]

public class CombatBattleHud : MonoBehaviour

{

    private const string StyleSheetResource = "Combat/CombatBattleHud";

    private const string ThemeResource = "Combat/UnityDefaultRuntimeTheme";



    private const string AlliesListName = "allies-list";

    private const string EnemiesListName = "enemies-list";

    private const string CommandsListName = "commands-list";



    private enum HudPhase

    {

        Commands,

        PickTarget

    }



    [SerializeField] private CombatTurnManager turnManager;

    [SerializeField] private CombatUnitSpawner spawner;

    [SerializeField] private CombatSceneController sceneController;



    private UIDocument _document;

    private VisualElement _alliesList;

    private VisualElement _enemiesList;

    private VisualElement _commandsList;



    private readonly List<CombatUnit> _hpSubscriptions = new();

    private readonly List<CommandEntry> _commands = new();

    private readonly List<CombatUnit> _targetChoices = new();

    private int _selectedCommandIndex;

    private HudPhase _phase = HudPhase.Commands;

    private int _pendingMoveId;
    private bool _lastCanAct;

    private struct CommandEntry

    {

        public string Label;

        public bool Enabled;

        public int MoveId;

        public Action OnSelect;

    }



    private static readonly Color HudTextColor = new(1f, 1f, 1f, 1f);

    private static readonly Color HudTextDisabledColor = new(0.75f, 0.75f, 0.85f, 1f);

    private static readonly Color HudTextMutedColor = new(0.55f, 0.55f, 0.65f, 1f);



    private void Awake()

    {

        EnsurePanelSettings();

    }



    private void OnEnable()

    {

        if (turnManager == null)

            turnManager = FindAnyObjectByType<CombatTurnManager>();

        if (spawner == null)

            spawner = FindAnyObjectByType<CombatUnitSpawner>();

        if (sceneController == null)

            sceneController = FindAnyObjectByType<CombatSceneController>();



        if (turnManager != null)

            turnManager.TurnChanged += OnTurnChanged;

    }



    private void Start()

    {

        StartCoroutine(InitializeHudRoutine());

    }



    private IEnumerator InitializeHudRoutine()

    {

        yield return null;



        BuildLayout();

        BuildCommands();

        RefreshAll();
        _lastCanAct = turnManager != null && turnManager.IsAwaitingPlayerCommand;
    }



    private void OnDisable()

    {

        if (turnManager != null)

            turnManager.TurnChanged -= OnTurnChanged;



        UnsubscribeHp();

    }



    private void Update()

    {

        if (turnManager == null || _commands.Count == 0)

            return;



        var kb = Keyboard.current;

        if (kb == null)

            return;



        if (kb.escapeKey.wasPressedThisFrame || kb.xKey.wasPressedThisFrame)

        {

            if (_phase == HudPhase.PickTarget)

            {

                _phase = HudPhase.Commands;

                BuildCommands();

                RefreshCommands();

            }



            return;

        }



        if (!turnManager.IsAwaitingPlayerCommand)

            return;



        if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)

            MoveCommandSelection(-1);

        else if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)

            MoveCommandSelection(1);

        else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.zKey.wasPressedThisFrame)

            TryExecuteSelectedCommand();

    }

    private void LateUpdate()
    {
        if (turnManager == null || _commands.Count == 0)
            return;

        var canAct = turnManager.IsAwaitingPlayerCommand;
        if (canAct == _lastCanAct)
            return;

        _lastCanAct = canAct;
        RefreshCommands();
    }



    private void EnsurePanelSettings()

    {

        _document = GetComponent<UIDocument>();

        if (_document == null)

            _document = gameObject.AddComponent<UIDocument>();



        if (_document.panelSettings == null)

        {

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();

            panelSettings.name = "CombatBattleHudPanelSettings";

            TryAssignDefaultTheme(panelSettings);

            _document.panelSettings = panelSettings;

        }



        var ps = _document.panelSettings;

        TryAssignDefaultTheme(ps);

        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;

        ps.referenceResolution = new Vector2Int(800, 600);

        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

        ps.match = 0.5f;

        ps.sortingOrder = 100;



        _document.visualTreeAsset = null;

        _document.sortingOrder = 100;

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



    private bool EnsureListsResolved()

    {

        if (_document == null)

            EnsurePanelSettings();



        var root = _document?.rootVisualElement;

        if (root == null)

            return false;



        if (root.Q("battle-root") == null)

            BuildLayout();

        else

        {

            _alliesList = root.Q<VisualElement>(AlliesListName);

            _commandsList = root.Q<VisualElement>(CommandsListName);

            _enemiesList = root.Q<VisualElement>(EnemiesListName);

        }



        return _alliesList != null && _commandsList != null && _enemiesList != null;

    }



    private static void ApplyHudLabelStyle(Label label, float fontSize, bool bold, Color color)

    {

        label.style.color = color;

        label.style.fontSize = fontSize;

        label.style.unityFontStyleAndWeight = bold ? FontStyle.Bold : FontStyle.Normal;

        label.style.unityTextAlign = TextAnchor.MiddleLeft;

    }



    private void BuildLayout()

    {

        if (_document == null)

            return;



        var root = _document.rootVisualElement;

        if (root == null)

        {

            Debug.LogError($"{nameof(CombatBattleHud)}: UIDocument has no root visual element.", this);

            return;

        }



        root.Clear();

        root.style.flexGrow = 1f;

        root.pickingMode = PickingMode.Ignore;



        var styleSheet = Resources.Load<StyleSheet>(StyleSheetResource);

        if (styleSheet != null)

            root.styleSheets.Add(styleSheet);



        var battleRoot = new VisualElement { name = "battle-root" };

        battleRoot.AddToClassList("battle-hud");

        battleRoot.pickingMode = PickingMode.Position;

        root.Add(battleRoot);



        var alliesPanel = new VisualElement { name = "panel-allies" };

        alliesPanel.AddToClassList("hud-panel");

        alliesPanel.AddToClassList("hud-panel--allies");

        alliesPanel.pickingMode = PickingMode.Ignore;

        _alliesList = new VisualElement { name = AlliesListName };

        _alliesList.AddToClassList("unit-list");
        _alliesList.style.flexGrow = 1;

        alliesPanel.Add(_alliesList);

        battleRoot.Add(alliesPanel);



        var commandsPanel = new VisualElement { name = "panel-commands" };

        commandsPanel.AddToClassList("hud-panel");

        commandsPanel.AddToClassList("hud-panel--commands");

        commandsPanel.pickingMode = PickingMode.Position;

        _commandsList = new VisualElement { name = CommandsListName };

        _commandsList.AddToClassList("command-list");

        _commandsList.pickingMode = PickingMode.Position;

        commandsPanel.Add(_commandsList);

        battleRoot.Add(commandsPanel);



        var enemiesPanel = new VisualElement { name = "panel-enemies" };

        enemiesPanel.AddToClassList("hud-panel");

        enemiesPanel.AddToClassList("hud-panel--enemies");

        enemiesPanel.pickingMode = PickingMode.Ignore;

        _enemiesList = new VisualElement { name = EnemiesListName };

        _enemiesList.AddToClassList("unit-list");
        _enemiesList.style.flexGrow = 1;

        enemiesPanel.Add(_enemiesList);

        battleRoot.Add(enemiesPanel);

    }



    private void BuildCommands()

    {

        _phase = HudPhase.Commands;

        _commands.Clear();

        _targetChoices.Clear();



        _commands.Add(new CommandEntry

        {

            Label = "Attack",

            Enabled = true,

            MoveId = CombatTurnManager.MoveIdStrike,

            OnSelect = () => BeginAttack(CombatTurnManager.MoveIdStrike)

        });

        _commands.Add(new CommandEntry

        {

            Label = $"Power Strike ({CombatTurnManager.PowerStrikeManaCost} MP)",

            Enabled = true,

            MoveId = CombatTurnManager.MoveIdPowerStrike,

            OnSelect = () => BeginAttack(CombatTurnManager.MoveIdPowerStrike)

        });

        _commands.Add(new CommandEntry

        {

            Label = "Flee",

            Enabled = true,

            MoveId = 0,

            OnSelect = () => turnManager?.TryFlee()

        });



        _selectedCommandIndex = 0;

    }



    private void BeginAttack(int moveId)

    {

        if (turnManager == null || !turnManager.IsAwaitingPlayerCommand)

            return;

        if (moveId == CombatTurnManager.MoveIdPowerStrike && !turnManager.CanPlayerUsePowerStrike())

            return;

        var targets = turnManager.GetLivingOpponentsFor(turnManager.CurrentActor);

        if (targets.Count == 0)

            return;



        if (targets.Count == 1)

        {

            turnManager.PerformPlayerStrike(targets[0], moveId);

            _phase = HudPhase.Commands;

            BuildCommands();

            return;

        }



        _pendingMoveId = moveId;

        _phase = HudPhase.PickTarget;

        BuildTargetChoices(targets);

        RefreshCommands();

    }



    private void BuildTargetChoices(IReadOnlyList<CombatUnit> targets)

    {

        _commands.Clear();

        _targetChoices.Clear();



        _commands.Add(new CommandEntry

        {

            Label = "← Back",

            Enabled = true,

            OnSelect = () =>

            {

                _phase = HudPhase.Commands;

                BuildCommands();

                RefreshCommands();

            }

        });



        foreach (var enemy in targets)

        {

            if (enemy == null || !enemy.IsAlive)

                continue;



            _targetChoices.Add(enemy);

            var name = enemy.Definition != null ? enemy.Definition.DisplayName : enemy.gameObject.name;

            var captured = enemy;

            _commands.Add(new CommandEntry

            {

                Label = name,

                Enabled = true,

                OnSelect = () =>

                {

                    turnManager?.PerformPlayerStrike(captured, _pendingMoveId);

                    _phase = HudPhase.Commands;

                    BuildCommands();

                }

            });

        }



        _selectedCommandIndex = 1;

    }



    private void OnTurnChanged()

    {

        if (_commands.Count == 0)

            return;



        if (!turnManager.IsAwaitingPlayerCommand)

            _phase = HudPhase.Commands;



        if (_phase == HudPhase.PickTarget && !turnManager.IsAwaitingPlayerCommand)

            BuildCommands();



        RefreshAll();

    }



    private void RefreshAll()

    {

        if (!EnsureListsResolved())

            return;



        SubscribeHp();

        RefreshAllies();

        RefreshEnemies();

        RefreshCommands();

    }



    private IEnumerable<CombatUnit> EnumerateAllies()

    {

        if (spawner != null)

        {

            foreach (var ally in spawner.SpawnedAllies)

            {

                if (ally != null)

                    yield return ally;

            }



            yield break;

        }



        foreach (var unit in FindObjectsByType<CombatUnit>())

        {

            if (unit != null && unit.IsAlly)

                yield return unit;

        }

    }



    private IEnumerable<CombatUnit> EnumerateEnemies()

    {

        if (spawner != null)

        {

            foreach (var enemy in spawner.SpawnedEnemies)

            {

                if (enemy != null)

                    yield return enemy;

            }



            yield break;

        }



        foreach (var unit in FindObjectsByType<CombatUnit>())

        {

            if (unit != null && !unit.IsAlly)

                yield return unit;

        }

    }



    private void SubscribeHp()

    {

        UnsubscribeHp();



        void Add(CombatUnit u)

        {

            if (u == null)

                return;

            u.HpChanged += OnUnitHpChanged;
            u.ManaChanged += OnUnitManaChanged;

            _hpSubscriptions.Add(u);

        }



        foreach (var a in EnumerateAllies())

            Add(a);

        foreach (var e in EnumerateEnemies())

            Add(e);

    }



    private void UnsubscribeHp()

    {

        foreach (var u in _hpSubscriptions)

        {

            if (u != null)

                u.HpChanged -= OnUnitHpChanged;
                u.ManaChanged -= OnUnitManaChanged;

        }



        _hpSubscriptions.Clear();

    }



    private void OnUnitHpChanged(int _, int __)

    {

        RefreshAllies();

        RefreshEnemies();

    }

    private void OnUnitManaChanged(int _, int __)

    {

        RefreshAllies();

        RefreshCommands();

    }



    private void RefreshAllies()

    {

        if (_alliesList == null)

            return;



        _alliesList.Clear();

        var active = turnManager != null ? turnManager.CurrentActor : null;



        foreach (var ally in EnumerateAllies())

        {

            AddUnitRow(_alliesList, ally, active);

        }

    }



    private void RefreshEnemies()

    {

        if (_enemiesList == null)

            return;



        _enemiesList.Clear();

        var active = turnManager != null ? turnManager.CurrentActor : null;



        foreach (var enemy in EnumerateEnemies())

        {

            if (!enemy.IsAlive)

                continue;



            AddUnitRow(_enemiesList, enemy, active);

        }

    }



    private static void AddUnitRow(VisualElement list, CombatUnit unit, CombatUnit active)

    {

        var row = new VisualElement();

        row.AddToClassList("unit-row");

        row.style.flexDirection = FlexDirection.Row;

        row.style.justifyContent = Justify.SpaceBetween;

        row.style.alignItems = Align.Center;

        row.style.minHeight = 0;

        row.style.paddingTop = 1;

        row.style.paddingBottom = 1;



        if (unit == active)

        {

            row.AddToClassList("unit-row--active");

            row.style.backgroundColor = new Color(1f, 1f, 1f, 0.22f);

        }



        if (!unit.IsAlive)

        {

            row.AddToClassList("unit-row--defeated");

            row.style.opacity = 0.45f;

        }



        var name = unit.Definition != null ? unit.Definition.DisplayName : unit.gameObject.name;

        var nameLabel = new Label(name);

        nameLabel.AddToClassList("unit-name");

        ApplyHudLabelStyle(nameLabel, 11f, bold: true, HudTextColor);
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;

        row.Add(nameLabel);



        var statsRow = new VisualElement();
        statsRow.AddToClassList("unit-stats-row");
        statsRow.style.flexDirection = FlexDirection.Row;
        statsRow.style.alignItems = Align.Center;
        statsRow.style.flexShrink = 0;

        var hpLabel = new Label($"HP {unit.CurrentHp}/{unit.MaxHp}");
        hpLabel.AddToClassList("unit-hp");
        ApplyHudLabelStyle(hpLabel, 10f, bold: false, HudTextColor);
        statsRow.Add(hpLabel);

        if (unit.UsesMana)
        {
            var mpLabel = new Label($"MP {unit.CurrentMana}/{unit.MaxMana}");
            mpLabel.AddToClassList("unit-mp");
            ApplyHudLabelStyle(mpLabel, 10f, bold: false, HudTextColor);
            statsRow.Add(mpLabel);
        }

        row.Add(statsRow);

        list.Add(row);

    }

    private bool IsCommandSelectable(CommandEntry cmd, bool canAct)
    {
        if (!canAct || !cmd.Enabled)
            return false;

        if (cmd.MoveId == CombatTurnManager.MoveIdPowerStrike)
            return turnManager != null && turnManager.CanPlayerUsePowerStrike();

        return true;
    }



    private void RefreshCommands()

    {

        if (_commandsList == null || _commands.Count == 0)

            return;



        _commandsList.Clear();

        var canAct = turnManager != null && turnManager.IsAwaitingPlayerCommand;

        var heroTurnButUiLocked = turnManager != null
            && turnManager.IsPlayerCommandActor(turnManager.CurrentActor)
            && !canAct;

        var waitingForTurn = !canAct
            && _phase == HudPhase.Commands
            && !heroTurnButUiLocked
            && turnManager != null
            && (turnManager.IsAutoTurnRoutineActive
                || !turnManager.IsPlayerCommandActor(turnManager.CurrentActor));



        if (_selectedCommandIndex < 0 || _selectedCommandIndex >= _commands.Count)

            _selectedCommandIndex = 0;



        if (canAct && !IsCommandSelectable(_commands[_selectedCommandIndex], canAct))

        {

            for (var i = 0; i < _commands.Count; i++)

            {

                if (IsCommandSelectable(_commands[i], canAct))

                {

                    _selectedCommandIndex = i;

                    break;

                }

            }

        }



        for (var i = 0; i < _commands.Count; i++)

        {

            var cmd = _commands[i];

            var interactable = IsCommandSelectable(cmd, canAct);



            var row = new VisualElement();

            row.AddToClassList("command-row");

            row.style.flexDirection = FlexDirection.Row;

            row.style.alignItems = Align.Center;

            row.pickingMode = PickingMode.Position;

            row.focusable = true;



            if (i == _selectedCommandIndex && interactable)

            {

                row.AddToClassList("command-row--selected");

                row.style.backgroundColor = new Color(1f, 1f, 1f, 0.25f);

            }



            var cursor = new Label("\u25ba");

            cursor.AddToClassList("command-cursor");

            cursor.pickingMode = PickingMode.Ignore;

            ApplyHudLabelStyle(cursor, 14f, bold: false, HudTextColor);

            cursor.style.width = 18;

            cursor.style.visibility = i == _selectedCommandIndex && interactable

                ? Visibility.Visible

                : Visibility.Hidden;

            row.Add(cursor);



            var label = new Label(cmd.Label);

            label.AddToClassList("command-label");

            label.pickingMode = PickingMode.Ignore;



            Color textColor;

            if (interactable)

                textColor = HudTextColor;

            else if (waitingForTurn && cmd.Enabled)

                textColor = HudTextMutedColor;

            else

                textColor = HudTextDisabledColor;



            ApplyHudLabelStyle(label, 14f, bold: true, textColor);

            row.Add(label);



            if (_phase == HudPhase.PickTarget && i == 0)

            {

                ApplyHudLabelStyle(label, 15f, bold: false, HudTextMutedColor);

            }



            var index = i;

            if (interactable)

            {

                row.RegisterCallback<ClickEvent>(_ => SelectAndExecute(index));

                row.RegisterCallback<PointerUpEvent>(evt =>

                {

                    if (evt.button == 0)

                        SelectAndExecute(index);

                });

            }



            _commandsList.Add(row);

        }



        if (waitingForTurn && _commandsList.childCount > 0)

        {

            var hint = new Label("…waiting");

            hint.pickingMode = PickingMode.Ignore;

            ApplyHudLabelStyle(hint, 13f, bold: false, HudTextMutedColor);

            _commandsList.Add(hint);

        }

    }



    private void MoveCommandSelection(int delta)

    {

        if (_commands.Count == 0)

            return;



        var start = _selectedCommandIndex;

        do

        {

            _selectedCommandIndex = (_selectedCommandIndex + delta + _commands.Count) % _commands.Count;

            if (IsCommandSelectable(_commands[_selectedCommandIndex], true))

                break;

        } while (_selectedCommandIndex != start);



        RefreshCommands();

    }



    private void SelectAndExecute(int index)

    {

        _selectedCommandIndex = index;

        TryExecuteSelectedCommand();

    }



    private void TryExecuteSelectedCommand()

    {

        if (turnManager == null || !turnManager.IsAwaitingPlayerCommand)

            return;

        if (_selectedCommandIndex < 0 || _selectedCommandIndex >= _commands.Count)

            return;



        var cmd = _commands[_selectedCommandIndex];

        if (!IsCommandSelectable(cmd, true))

            return;



        cmd.OnSelect?.Invoke();



        if (_phase == HudPhase.Commands)

            RefreshAll();

        else

            RefreshCommands();

    }

}


