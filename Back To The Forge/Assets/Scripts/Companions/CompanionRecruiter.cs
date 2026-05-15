using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// NPC the player talks to: opening dialogue, then a hire / decline prompt. When hired, this GameObject
/// persists (<c>DontDestroyOnLoad</c>) and follows the player until a new day clears the roster, then returns
/// to its original scene and post position.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class CompanionRecruiter : MonoBehaviour
{
    private const string DontDestroySceneName = "DontDestroyOnLoad";

    [SerializeField] private HireableCompanionOffer offer;
    [Tooltip("If true, fills the first empty companion slot (max 3). If false, uses partySlotIndex only.")]
    [SerializeField] private bool autoAssignPartySlot = true;
    [Tooltip("Used when autoAssignPartySlot is false.")]
    [SerializeField] private int partySlotIndex;
    [SerializeField] private BlacksmithMaster blacksmith;
    [SerializeField] private ForgeQuestChoiceUI choiceUi;
    [SerializeField] private SimpleRpgDialogueUI dialogueUi;

    [Header("Speakers")]
    [SerializeField] private string npcDisplayName = "Mercenary";
    [Tooltip("If empty, uses the offer's unit display name when the companion speaks.")]
    [SerializeField] private string companionSpeakerOverride = string.Empty;

    [Header("Dialogue")]
    [Tooltip("Shown first when the player interacts. If empty, goes straight to the hire prompt.")]
    [SerializeField] [TextArea(2, 6)] private string openingLine =
        "Need muscle? I fight for coin — fair wage, until tomorrow.";

    [SerializeField] private string hireButtonCustomText = string.Empty;
    [SerializeField] private string declineButtonText = "No thanks";

    [SerializeField] [TextArea(2, 4)] private string cannotAffordLine =
        "Your purse is too light. Come back when you can pay.";

    [SerializeField] [TextArea(2, 4)] private string companionJoinLine =
        "I'm with you. Point me at the trouble.";

    [Header("Proximity & interact")]
    [SerializeField] private InputActionReference interactAction;

    private readonly HashSet<Collider2D> _playerProximity = new();
    private bool _busy;

    private Vector3 _recruitWorldPosition;
    private Quaternion _recruitWorldRotation;
    private string _homeSceneName;
    private SpriteRenderer[] _spriteRenderers;
    private Collider2D _rangeCollider;
    private GridWanderNpc2D _wander;
    private bool _capturedSpawn;
    private bool _isFollowingPlayer;
    private Coroutine _returnHomeRoutine;
    private int _activePartySlot = -1;

    public void ConfigureFromOffer(HireableCompanionOffer configuredOffer, Color? spriteTint = null)
    {
        if (configuredOffer != null)
        {
            offer = configuredOffer;
            npcDisplayName = configuredOffer.NpcDisplayName;
            openingLine = configuredOffer.OpeningLine ?? string.Empty;
            cannotAffordLine = configuredOffer.CannotAffordLine;
            companionJoinLine = configuredOffer.CompanionJoinLine;
        }

        if (spriteTint.HasValue)
        {
            var sprite = GetComponentInChildren<SpriteRenderer>();
            if (sprite != null)
                sprite.color = spriteTint.Value;
        }
    }

    private void Awake()
    {
        var c = GetComponent<Collider2D>();
        if (c != null && !c.isTrigger)
            Debug.LogWarning($"{nameof(CompanionRecruiter)}: use a trigger for talk range.", this);

        _rangeCollider = c;
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _wander = GetComponent<GridWanderNpc2D>();
        _homeSceneName = gameObject.scene.name;

        CaptureRecruitSpawnIfNeeded();
    }

    private void CaptureRecruitSpawnIfNeeded()
    {
        if (_capturedSpawn)
            return;

        _recruitWorldPosition = transform.position;
        _recruitWorldRotation = transform.rotation;
        _capturedSpawn = true;
    }

    /// <summary>
    /// Call after the instance is placed in-world (e.g. by <see cref="MercenaryCampSpawner"/>) so hire-post and return-home use the real pose.
    /// Instantiate assigns transform before Awake; this still runs after <see cref="ConfigureFromOffer"/> so roster/UI hooks cannot stamp an incorrect cached pose.
    /// </summary>
    public void CommitSpawnPoseSnapshot()
    {
        _recruitWorldPosition = transform.position;
        _recruitWorldRotation = transform.rotation;
        _capturedSpawn = true;
    }

    private void OnEnable()
    {
        if (interactAction != null)
            interactAction.action.Enable();

        var mgr = HiredCompanionManager.GetOrCreate();
        mgr.OnRosterChanged -= OnHireRosterChanged;
        mgr.OnRosterChanged += OnHireRosterChanged;

        CaptureRecruitSpawnIfNeeded();
        ApplyRecruiterWorldVisibility();
    }

    private void OnDisable()
    {
        if (interactAction != null)
            interactAction.action.Disable();

        var mgr = HiredCompanionManager.Instance;
        if (mgr != null)
            mgr.OnRosterChanged -= OnHireRosterChanged;

        StopAllCoroutines();
        _busy = false;
        _returnHomeRoutine = null;
        _playerProximity.Clear();
    }

    private void OnHireRosterChanged()
    {
        ApplyRecruiterWorldVisibility();
    }

    private void ApplyRecruiterWorldVisibility()
    {
        var mgr = HiredCompanionManager.Instance;
        _activePartySlot = -1;

        if (mgr != null && offer != null)
        {
            _activePartySlot = mgr.FindSlotWithUnitId(offer.UnitId);
            if (_activePartySlot >= 0 && mgr.GetPhysicalFollowerRoot(_activePartySlot) == gameObject)
            {
                BeginFollowingPlayer();
                return;
            }

            _activePartySlot = -1;
        }

        RestoreRecruiterAtPost();
    }

    private void BeginFollowingPlayer()
    {
        if (_isFollowingPlayer)
        {
            EnsureFollowerConfigured();
            return;
        }

        _isFollowingPlayer = true;
        CaptureRecruitSpawnIfNeeded();
        DontDestroyOnLoad(gameObject);

        var mgr = HiredCompanionManager.Instance;
        if (mgr != null && _activePartySlot >= 0)
            mgr.BindPhysicalFollowerToSlot(_activePartySlot, gameObject);

        if (_returnHomeRoutine != null)
        {
            StopCoroutine(_returnHomeRoutine);
            _returnHomeRoutine = null;
        }

        if (_wander != null)
            _wander.enabled = false;

        if (_rangeCollider != null)
            _rangeCollider.enabled = false;

        for (var i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] != null)
                _spriteRenderers[i].enabled = true;
        }

        foreach (var cu in GetComponentsInChildren<CombatUnit>(true))
            cu.enabled = false;

        foreach (var hb in GetComponentsInChildren<CombatUnitHealthBar>(true))
            hb.enabled = false;

        var hbRoot = transform.Find("HealthBarRoot");
        if (hbRoot != null)
            hbRoot.gameObject.SetActive(false);

        var follower = GetComponent<CompanionFollower2D>();
        if (follower == null)
            follower = gameObject.AddComponent<CompanionFollower2D>();

        EnsureFollowerConfigured();
        follower.enabled = true;
    }

    private void EnsureFollowerConfigured()
    {
        var follower = GetComponent<CompanionFollower2D>();
        if (follower == null)
            return;

        var playerT = PlayerMovement2D.Instance != null
            ? PlayerMovement2D.Instance.transform
            : null;
        if (playerT == null)
        {
            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged != null)
                playerT = tagged.transform;
        }

        var slot = _activePartySlot >= 0 ? _activePartySlot : partySlotIndex;
        follower.Configure(playerT, slot);
    }

    private void RestoreRecruiterAtPost()
    {
        var wasFollowing = _isFollowingPlayer;

        if (_returnHomeRoutine != null)
        {
            StopCoroutine(_returnHomeRoutine);
            _returnHomeRoutine = null;
        }

        if (_isFollowingPlayer)
        {
            _isFollowingPlayer = false;
            if (_activePartySlot >= 0)
                HiredCompanionManager.Instance?.UnbindPhysicalFollowerSlot(_activePartySlot);

            var follower = GetComponent<CompanionFollower2D>();
            if (follower != null)
                follower.enabled = false;
        }

        if (IsInDontDestroyOnLoadScene())
        {
            var home = SceneManager.GetSceneByName(_homeSceneName);
            if (home.IsValid() && home.isLoaded)
            {
                SceneManager.MoveGameObjectToScene(gameObject, home);
                ApplyPostVisualAndWanderAtSavedSpawn();
            }
            else
                _returnHomeRoutine = StartCoroutine(ReturnToHomeSceneWhenLoaded());
            return;
        }

        if (wasFollowing)
            ApplyPostVisualAndWanderAtSavedSpawn();
    }

    private bool IsInDontDestroyOnLoadScene()
    {
        return gameObject.scene.name == DontDestroySceneName;
    }

    private IEnumerator ReturnToHomeSceneWhenLoaded()
    {
        Scene home = default;
        while (!(home = SceneManager.GetSceneByName(_homeSceneName)).IsValid() || !home.isLoaded)
            yield return null;

        SceneManager.MoveGameObjectToScene(gameObject, home);
        ApplyPostVisualAndWanderAtSavedSpawn();
        _returnHomeRoutine = null;
    }

    private void ApplyPostVisualAndWanderAtSavedSpawn()
    {
        transform.SetPositionAndRotation(_recruitWorldPosition, _recruitWorldRotation);

        for (var i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] != null)
                _spriteRenderers[i].enabled = true;
        }

        if (_rangeCollider != null)
            _rangeCollider.enabled = true;

        if (_wander != null)
            _wander.enabled = true;
    }

    private void Update()
    {
        var mgr = HiredCompanionManager.Instance;
        if (mgr != null && offer != null && mgr.FindSlotWithUnitId(offer.UnitId) >= 0
            && mgr.GetPhysicalFollowerRoot(mgr.FindSlotWithUnitId(offer.UnitId)) == gameObject)
            return;

        if (SimpleRpgDialogueUI.IsDialogueOpen || ForgeQuestChoiceUI.IsBlockingGameplay || PauseMenuController.IsOpen)
            return;

        if (_playerProximity.Count <= 0 || _busy || offer == null)
            return;

        if (!WasInteractPressedThisFrame())
            return;

        if (dialogueUi == null)
            dialogueUi = SimpleRpgDialogueUI.GetOrCreate();
        if (choiceUi == null)
            choiceUi = ForgeQuestChoiceUI.GetOrCreate();
        if (blacksmith == null)
            blacksmith = BlacksmithMaster.ResolveEconomy();

        StartCoroutine(HireRoutine());
    }

    private IEnumerator HireRoutine()
    {
        _busy = true;

        var label = offer.DisplayLabel;
        var cost = offer.HireCost;
        var uid = offer.UnitId;

        if (uid <= 0 || blacksmith == null)
        {
            dialogueUi.Show(ResolveNpcDisplayName(), "Something's wrong with this posting.");
            yield return StartCoroutine(WaitDialogueClosed());
            _busy = false;
            yield break;
        }

        var speaker = ResolveNpcDisplayName();
        var openLine = ResolveOpeningLine();

        if (!string.IsNullOrWhiteSpace(openLine))
        {
            dialogueUi.Show(speaker, openLine);
            yield return StartCoroutine(WaitDialogueClosed());
        }

        var hireBtn = !string.IsNullOrWhiteSpace(hireButtonCustomText)
            ? hireButtonCustomText.Trim()
            : cost > 0
                ? $"Hire {label} ({cost}g)"
                : $"Hire {label}";

        yield return StartCoroutine(choiceUi.RunRoutine(hireBtn, declineButtonText));

        if (choiceUi.LastChoice != 0)
        {
            _busy = false;
            yield break;
        }

        var mgr = HiredCompanionManager.GetOrCreate();
        if (blacksmith.PlayerGold < cost)
        {
            dialogueUi.Show(speaker, ResolveCannotAffordLine());
            yield return StartCoroutine(WaitDialogueClosed());
            _busy = false;
            yield break;
        }

        if (autoAssignPartySlot && mgr.IsPartyFull && mgr.FindSlotWithUnitId(uid) < 0)
        {
            dialogueUi.Show(speaker, ResolvePartyFullLine());
            yield return StartCoroutine(WaitDialogueClosed());
            _busy = false;
            yield break;
        }

        var hired = autoAssignPartySlot
            ? mgr.TryHireAuto(uid, cost, blacksmith, gameObject, out _activePartySlot)
            : mgr.TryHire(partySlotIndex, uid, cost, blacksmith, gameObject);

        if (hired)
        {
            if (!autoAssignPartySlot)
                _activePartySlot = partySlotIndex;

            dialogueUi.Show(CompanionSpeakerName(), ResolveCompanionJoinLine());
            yield return StartCoroutine(WaitDialogueClosed());
        }
        else
        {
            dialogueUi.Show(speaker, "Couldn't seal the deal. Try again.");
            yield return StartCoroutine(WaitDialogueClosed());
        }

        _busy = false;
    }

    private IEnumerator WaitDialogueClosed()
    {
        yield return null;
        yield return new WaitUntil(() => !SimpleRpgDialogueUI.IsDialogueOpen);
    }

    private string ResolveNpcDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(npcDisplayName))
            return npcDisplayName.Trim();

        return offer != null ? offer.NpcDisplayName : "Mercenary";
    }

    private string ResolveOpeningLine()
    {
        if (!string.IsNullOrWhiteSpace(openingLine))
            return openingLine.Trim();

        return offer != null ? offer.OpeningLine : string.Empty;
    }

    private string ResolveCannotAffordLine()
    {
        if (!string.IsNullOrWhiteSpace(cannotAffordLine))
            return cannotAffordLine.Trim();

        return offer != null ? offer.CannotAffordLine : "Your purse is too light.";
    }

    private string ResolveCompanionJoinLine()
    {
        if (!string.IsNullOrWhiteSpace(companionJoinLine))
            return companionJoinLine.Trim();

        return offer != null ? offer.CompanionJoinLine : "I'm with you.";
    }

    private string ResolvePartyFullLine()
    {
        return offer != null && !string.IsNullOrWhiteSpace(offer.PartyFullLine)
            ? offer.PartyFullLine
            : "You've already hired three companions. End the day at the forge to refresh your roster.";
    }

    private string CompanionSpeakerName()
    {
        if (!string.IsNullOrWhiteSpace(companionSpeakerOverride))
            return companionSpeakerOverride.Trim();

        return offer != null ? offer.NpcDisplayName : ResolveNpcDisplayName();
    }

    private bool WasInteractPressedThisFrame()
    {
        if (SimpleRpgDialogueUI.InteractConsumedByDialogueFrame == Time.frameCount)
            return false;

        if (interactAction != null && interactAction.action != null)
            return interactAction.action.WasPressedThisFrame();

        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Add(other);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!PlayerMovement2D.IsPlayerCharacterCollider(other))
            return;

        _playerProximity.Remove(other);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (partySlotIndex < 0)
            partySlotIndex = 0;
        if (partySlotIndex > 2)
            partySlotIndex = 2;
    }
#endif
}
