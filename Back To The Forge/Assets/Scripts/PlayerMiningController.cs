using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

/// <summary>
/// Put on the player (needs <see cref="Rigidbody2D"/> + non-trigger collider). While Interact is held, adds items from the
/// nearest in-range <see cref="IronVein"/> (ore, wood, stone, etc.): one unit per full second (scaled time).
/// Shows a gather prompt when in range.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMiningController : MonoBehaviour
{
    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private Inventory inventory;
    [SerializeField] private string gatherPromptFormat = "Hold {0} to gather";

    private readonly HashSet<IronVein> _veinsInRange = new();
    private float _mineAccumulator;

    private UIDocument _gatherPromptDocument;
    private VisualElement _gatherPromptPanel;
    private Label _gatherPromptLabel;
    private bool _gatherPromptUiBuilt;

    private void Awake()
    {
        if (inventory == null)
            inventory = GetComponent<Inventory>();
    }

    private void Start()
    {
        BuildGatherPromptUi();
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        _mineAccumulator = 0f;
    }

    private void LateUpdate()
    {
        if (!_gatherPromptUiBuilt)
            BuildGatherPromptUi();

        RefreshGatherPrompt();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var vein = other.GetComponent<IronVein>() ?? other.GetComponentInParent<IronVein>();
        if (vein != null)
            _veinsInRange.Add(vein);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var vein = other.GetComponent<IronVein>() ?? other.GetComponentInParent<IronVein>();
        if (vein != null)
            _veinsInRange.Remove(vein);
    }

    private void Update()
    {
        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
        {
            _mineAccumulator = 0f;
            return;
        }

        if (inventory == null)
            return;

        var vein = GetClosestVein();
        if (vein == null || !vein.HasOreLeft)
        {
            _mineAccumulator = 0f;
            return;
        }

        if (!IsInteractHeld())
        {
            _mineAccumulator = 0f;
            return;
        }

        _mineAccumulator += Time.deltaTime;

        while (_mineAccumulator >= 1f)
        {
            _mineAccumulator -= 1f;

            vein = GetClosestVein();
            if (vein == null || !vein.HasOreLeft)
                break;

            var leftover = inventory.TryAdd(vein.OreDefinition, vein.OrePerTick);
            if (leftover > 0)
            {
                Debug.LogWarning("Inventory full — cannot add more.", this);
                break;
            }

            vein.RegisterSuccessfulMine();
        }
    }

    private IronVein GetClosestVein()
    {
        if (IronVein.TryGetGatherNodeAtPosition(transform.position, out var atPosition))
            return atPosition;

        IronVein best = null;
        var bestSqr = float.PositiveInfinity;
        var p = transform.position;

        _veinsInRange.RemoveWhere(v => v == null);

        foreach (var vein in _veinsInRange)
        {
            if (vein == null || !vein.HasOreLeft)
                continue;

            var d = (vein.transform.position - p).sqrMagnitude;
            if (d < bestSqr)
            {
                bestSqr = d;
                best = vein;
            }
        }

        return best;
    }

    /// <summary>Closest gatherable vein the player is overlapping, if any.</summary>
    public bool TryGetClosestGatherTarget(out IronVein vein)
    {
        return IronVein.TryGetGatherNodeAtPosition(transform.position, out vein);
    }

    /// <summary>Binding label for UI prompts (keyboard only, e.g. E).</summary>
    public string GetInteractKeyLabel()
    {
        if (interactAction != null && interactAction.action != null)
        {
            var label = interactAction.action.GetBindingDisplayString(
                InputBinding.MaskByGroup("Keyboard&Mouse"),
                InputBinding.DisplayStringOptions.DontUseShortDisplayNames);
            if (!string.IsNullOrWhiteSpace(label))
                return label.Trim().ToUpperInvariant();
        }

        return "E";
    }

    private void BuildGatherPromptUi()
    {
        if (_gatherPromptUiBuilt)
            return;

        _gatherPromptDocument = GetComponent<UIDocument>();
        if (_gatherPromptDocument == null)
            _gatherPromptDocument = gameObject.AddComponent<UIDocument>();

        FfStyleMenuUi.ConfigureDocument(_gatherPromptDocument, 4480);

        var root = _gatherPromptDocument.rootVisualElement;
        if (root == null)
            return;

        root.Clear();
        root.style.flexGrow = 1f;
        root.pickingMode = PickingMode.Ignore;
        FfStyleMenuUi.AttachStyleSheet(root);

        _gatherPromptPanel = new VisualElement { name = "gather-prompt-panel" };
        _gatherPromptPanel.AddToClassList("hud-panel");
        _gatherPromptPanel.AddToClassList("hud-panel--faded");
        _gatherPromptPanel.style.position = Position.Absolute;
        _gatherPromptPanel.style.bottom = 72;
        _gatherPromptPanel.style.left = Length.Percent(50);
        _gatherPromptPanel.style.translate = new Translate(Length.Percent(-50), 0);
        _gatherPromptPanel.style.flexGrow = 0f;
        _gatherPromptPanel.style.flexShrink = 0f;
        _gatherPromptPanel.style.paddingTop = 8;
        _gatherPromptPanel.style.paddingBottom = 8;
        _gatherPromptPanel.style.paddingLeft = 14;
        _gatherPromptPanel.style.paddingRight = 14;
        _gatherPromptPanel.style.display = DisplayStyle.None;
        _gatherPromptPanel.pickingMode = PickingMode.Ignore;
        root.Add(_gatherPromptPanel);

        _gatherPromptLabel = new Label { name = "gather-prompt-label" };
        FfStyleMenuUi.ApplyLabelStyle(_gatherPromptLabel, 12f, true);
        _gatherPromptLabel.style.whiteSpace = WhiteSpace.Normal;
        _gatherPromptLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        _gatherPromptPanel.Add(_gatherPromptLabel);

        _gatherPromptUiBuilt = true;
    }

    private void RefreshGatherPrompt()
    {
        if (!_gatherPromptUiBuilt || _gatherPromptPanel == null || _gatherPromptLabel == null)
            return;

        if (ShouldHideGatherPrompt() || !IronVein.TryGetGatherNodeAtPosition(transform.position, out _))
        {
            _gatherPromptPanel.style.display = DisplayStyle.None;
            return;
        }

        _gatherPromptLabel.text = string.Format(gatherPromptFormat, GetInteractKeyLabel());
        _gatherPromptPanel.style.display = DisplayStyle.Flex;
    }

    private static bool ShouldHideGatherPrompt()
    {
        return PauseMenuController.IsOpen
               || SimpleRpgDialogueUI.IsDialogueOpen
               || ForgeQuestChoiceUI.IsBlockingGameplay;
    }

    private bool IsInteractHeld()
    {
        if (interactAction != null && interactAction.action != null)
            return interactAction.action.IsPressed();

        return Keyboard.current != null && Keyboard.current.eKey.isPressed;
    }
}
